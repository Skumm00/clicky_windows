using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using ClickyWindows.Helpers;

namespace ClickyWindows.Services;

internal static class MediHacksLocator
{
    private const string FolderName = "MediHacks";

    private static readonly Regex LocateRequest = new(
        @"\b(?:where(?:'s|\s+is)?|find|locate|show|point)\b.*\bmedihacks\b|\bmedihacks\b.*\b(?:folder|where|find|locate|show|point)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsRequest(string prompt) => LocateRequest.IsMatch(prompt);

    public static async Task<(bool Found, double X, double Y, string Message)> LocateAsync(
        CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            FolderName);
        if (!Directory.Exists(folderPath))
            return (false, 0, 0, "The MediHacks folder is not on this desktop.");

        RevealDesktopIfCovered();
        await Task.Delay(260, cancellationToken);

        var desktopList = FindDesktopListView();
        if (desktopList == IntPtr.Zero)
            return (false, 0, 0, "Clicky couldn't access the Windows desktop icons.");

        try
        {
            var desktop = AutomationElement.FromHandle(desktopList);
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.NameProperty, FolderName));
            var item = desktop.FindFirst(TreeScope.Descendants, condition);
            if (item is null)
                return (false, 0, 0, "MediHacks exists, but its desktop icon is not visible.");

            var bounds = item.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                return (false, 0, 0, "MediHacks exists, but Windows did not report its icon position.");

            return (
                true,
                bounds.Left + bounds.Width / 2.0,
                bounds.Top + bounds.Height / 2.0,
                "MediHacks is right here.");
        }
        catch (ElementNotAvailableException)
        {
            return (false, 0, 0, "The desktop refreshed before Clicky could locate MediHacks.");
        }
    }

    private static IntPtr FindDesktopListView()
    {
        var progman = Win32.FindWindow("Progman", null);
        var shellView = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (shellView == IntPtr.Zero)
        {
            Win32.EnumWindows((window, _) =>
            {
                var candidate = Win32.FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (candidate == IntPtr.Zero)
                    return true;

                shellView = candidate;
                return false;
            }, IntPtr.Zero);
        }

        return shellView == IntPtr.Zero
            ? IntPtr.Zero
            : Win32.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    private static void RevealDesktopIfCovered()
    {
        var foreground = Win32.GetForegroundWindow();
        var className = new StringBuilder(64);
        Win32.GetClassName(foreground, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW")
            return;

        Win32.keybd_event(Win32.VK_LWIN, 0, 0, UIntPtr.Zero);
        Win32.keybd_event(Win32.VK_D, 0, 0, UIntPtr.Zero);
        Win32.keybd_event(Win32.VK_D, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
        Win32.keybd_event(Win32.VK_LWIN, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
