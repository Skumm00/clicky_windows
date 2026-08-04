using System.Diagnostics;
using System.Text.RegularExpressions;
using ClickyWindows.Helpers;

namespace ClickyWindows.Services;

internal static class WindowsAppLauncher
{
    private static readonly Regex OpenCommand = new(
        @"^\s*(?:please\s+)?(?:open|launch|start)\s+(?<app>.+?)[.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, (string Target, string DisplayName)> Apps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome"] = ("chrome.exe", "Google Chrome"),
            ["google chrome"] = ("chrome.exe", "Google Chrome"),
            ["edge"] = ("msedge.exe", "Microsoft Edge"),
            ["microsoft edge"] = ("msedge.exe", "Microsoft Edge"),
            ["notepad"] = ("notepad.exe", "Notepad"),
            ["file explorer"] = ("explorer.exe", "File Explorer"),
            ["explorer"] = ("explorer.exe", "File Explorer"),
            ["settings"] = ("ms-settings:", "Windows Settings"),
        };

    public static bool TryLaunch(string prompt, out string result)
    {
        result = "";
        var match = OpenCommand.Match(prompt);
        if (!match.Success)
            return false;

        var appName = match.Groups["app"].Value.Trim();
        if (!Apps.TryGetValue(appName, out var app))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(app.Target) { UseShellExecute = true });
            result = $"Opened {app.DisplayName}.";
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not launch {app.DisplayName}: {ex.Message}");
            result = $"I couldn't open {app.DisplayName}.";
        }
        return true;
    }
}
