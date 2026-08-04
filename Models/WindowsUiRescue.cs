namespace ClickyWindows.Models;

public enum WindowsUiRescueKind
{
    TaskbarNotResponding,
    DesktopIconsMissing,
}

public static class WindowsUiRescue
{
    public static bool TryMatch(string transcript, out WindowsUiRescueKind rescue)
    {
        var text = transcript.ToLowerInvariant();

        if (ContainsAny(text,
                "taskbar not responding", "taskbar isn't responding", "taskbar is not responding",
                "taskbar not working", "taskbar doesn't respond", "taskbar doesnt respond",
                "taskbar won't respond", "taskbar is frozen", "taskbar froze", "taskbar broken",
                "start menu not working", "start menu is frozen", "gorev cubugu calismiyor"))
        {
            rescue = WindowsUiRescueKind.TaskbarNotResponding;
            return true;
        }

        if (ContainsAny(text,
                "desktop icons disappeared", "desktop icon disappeared", "desktop icons are gone", "desktop icons missing",
                "my icons disappeared", "my desktop is empty", "masaustu simgeleri kayboldu"))
        {
            rescue = WindowsUiRescueKind.DesktopIconsMissing;
            return true;
        }

        rescue = default;
        return false;
    }

    public static string Intro(WindowsUiRescueKind rescue) => rescue switch
    {
        WindowsUiRescueKind.TaskbarNotResponding =>
            "I opened a Windows taskbar rescue card. Start with Task Manager, then restart Windows Explorer.",
        WindowsUiRescueKind.DesktopIconsMissing =>
            "I opened a desktop icons rescue card. First, check the Windows desktop icons setting.",
        _ => "I opened a Windows rescue card.",
    };

    private static bool ContainsAny(string text, params string[] phrases) =>
        phrases.Any(text.Contains);
}
