using System.Diagnostics;
using System.Text.RegularExpressions;
using ClickyWindows.Helpers;

namespace ClickyWindows.Services;

internal sealed record WindowsLaunchRequest(
    string Target,
    string ProcessName,
    string DisplayName,
    string? FollowUpPrompt);

internal static class WindowsAppLauncher
{
    private static readonly Regex OpenCommand = new(
        @"^\s*(?:please\s+)?(?:open|launch|start)\s+(?<app>google\s+chrome|microsoft\s+edge|file\s+explorer|chrome|edge|notepad|explorer|settings)(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FollowUpCommand = new(
        @"^(?:,\s*)?(?:and\s+then|and|then)\s+(?<task>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, (string Target, string ProcessName, string DisplayName)> Apps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome"] = ("chrome.exe", "chrome", "Google Chrome"),
            ["google chrome"] = ("chrome.exe", "chrome", "Google Chrome"),
            ["edge"] = ("msedge.exe", "msedge", "Microsoft Edge"),
            ["microsoft edge"] = ("msedge.exe", "msedge", "Microsoft Edge"),
            ["notepad"] = ("notepad.exe", "notepad", "Notepad"),
            ["file explorer"] = ("explorer.exe", "explorer", "File Explorer"),
            ["explorer"] = ("explorer.exe", "explorer", "File Explorer"),
            ["settings"] = ("ms-settings:", "SystemSettings", "Windows Settings"),
        };

    public static bool TryParse(string prompt, out WindowsLaunchRequest request)
    {
        request = null!;
        var match = OpenCommand.Match(prompt);
        if (!match.Success)
            return false;

        var appName = Regex.Replace(match.Groups["app"].Value.Trim(), @"\s+", " ");
        if (!Apps.TryGetValue(appName, out var app))
            return false;

        var rest = match.Groups["rest"].Value.Trim().TrimEnd('.', '!').Trim();
        string? followUp = null;
        if (rest.Length > 0)
        {
            var followUpMatch = FollowUpCommand.Match(rest);
            if (!followUpMatch.Success)
                return false;

            followUp = followUpMatch.Groups["task"].Value.Trim();
            if (followUp.Length == 0)
                return false;
        }

        request = new WindowsLaunchRequest(app.Target, app.ProcessName, app.DisplayName, followUp);
        return true;
    }

    public static bool TryLaunch(WindowsLaunchRequest request, out string result)
    {
        try
        {
            Process.Start(new ProcessStartInfo(request.Target) { UseShellExecute = true });
            result = $"Opened {request.DisplayName}.";
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not launch {request.DisplayName}: {ex.Message}");
            result = $"I couldn't open {request.DisplayName}.";
            return false;
        }
    }

    public static async Task<bool> WaitForWindowAsync(
        WindowsLaunchRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(450, cancellationToken);

        for (var attempt = 0; attempt < 45; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var foreground = Win32.GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                Win32.GetWindowThreadProcessId(foreground, out var foregroundProcessId);
                try
                {
                    using var foregroundProcess = Process.GetProcessById((int)foregroundProcessId);
                    if (string.Equals(
                            foregroundProcess.ProcessName,
                            request.ProcessName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Delay(550, cancellationToken);
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // The foreground process exited between the Win32 calls; keep polling.
                }
            }

            foreach (var process in Process.GetProcessesByName(request.ProcessName))
            {
                using (process)
                {
                    var window = process.MainWindowHandle;
                    if (window == IntPtr.Zero)
                        continue;

                    if (Win32.IsIconic(window))
                        Win32.ShowWindow(window, Win32.SW_RESTORE);
                    Win32.SetForegroundWindow(window);
                    await Task.Delay(550, cancellationToken);
                    return true;
                }
            }

            await Task.Delay(100, cancellationToken);
        }

        Logger.Log($"[Windows launcher] Timed out waiting for {request.DisplayName} window");
        return false;
    }
}
