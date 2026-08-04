using System.Windows;
using ClickyWindows.Services;
using ClickyWindows.Settings;

namespace ClickyWindows;

/// <summary>
/// Invisible message-pump window. Hosts the HotkeyService (requires an HWND)
/// and drives CompanionManager.
/// </summary>
public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkey;
    private readonly CompanionManager _companion;
    private readonly OverlayWindow _overlay;
    private WindowsUiRescueWindow? _rescueWindow;
    private PromptWindow? _promptWindow;

    public MainWindow(AppSettings settings, CompanionManager companion, OverlayWindow overlay)
    {
        InitializeComponent();

        _companion = companion;
        _overlay = overlay;
        _hotkey = new HotkeyService(settings.HotkeyModifiers, settings.HotkeyVirtualKey);

        // Wire companion events to overlay
        _companion.StateChanged += state =>
            Dispatcher.Invoke(() => _overlay.SetState(state));

        _companion.PointReceived += (x, y, label) =>
            Dispatcher.Invoke(() => _overlay.ShowTargetAt(x, y, label));

        _companion.PointActionReceived += (x, y, label, action) =>
            Dispatcher.Invoke(() => _overlay.ShowTargetAt(x, y, label, action));

        _companion.AudioLevelChanged += level =>
            Dispatcher.Invoke(() => _overlay.SetAudioLevel(level));

        _companion.FeedbackReceived += msg =>
            Dispatcher.Invoke(() => _overlay.ShowFeedback(ToEnglishFeedback(msg)));

        _companion.TranscriptConfirmed +=
            () => Dispatcher.Invoke(() => _overlay.PulseSpinner());

        _companion.WindowsUiRescueRequested += rescue =>
            Dispatcher.Invoke(() => ShowWindowsUiRescue(rescue));

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hotkey.Register(this);
        _hotkey.PushToTalkPressed  += () => _ = _companion.OnPushToTalkPressed();
        _hotkey.PushToTalkReleased += () => _ = _companion.OnPushToTalkReleased();
        _hotkey.TogglePanelRequested += TogglePrompt;
        _hotkey.SelectAreaRequested += OpenAreaSelection;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkey.Dispose();
        _rescueWindow?.Close();
        _promptWindow?.Close();
    }

    private static string ToEnglishFeedback(string message)
    {
        if (message.Contains("duyamad", StringComparison.OrdinalIgnoreCase))
            return "I couldn't hear you.";
        if (message.Contains("lanamad", StringComparison.OrdinalIgnoreCase))
            return "Couldn't connect.";
        if (message.Contains("yan", StringComparison.OrdinalIgnoreCase))
            return "Clicky couldn't answer.";
        return message;
    }

    public void OpenPrompt()
    {
        if (_promptWindow is { IsVisible: true })
        {
            _promptWindow.Activate();
            _promptWindow.FocusPrompt();
            return;
        }

        if (_promptWindow is not null)
        {
            _promptWindow.PrepareForShow();
            _promptWindow.Show();
            _promptWindow.Activate();
            _promptWindow.FocusPrompt();
            return;
        }

        _promptWindow = new PromptWindow(_companion, _overlay);
        _promptWindow.Closed += (_, _) => _promptWindow = null;
        _promptWindow.Show();
        _promptWindow.Activate();
    }

    private void TogglePrompt()
    {
        if (_promptWindow is { IsVisible: true })
        {
            _promptWindow.HideAndClearOutput();
            return;
        }

        if (_promptWindow is null)
        {
            _promptWindow = new PromptWindow(_companion, _overlay);
            _promptWindow.Closed += (_, _) => _promptWindow = null;
        }

        _promptWindow.PrepareForShow();
        _promptWindow.Show();
        _promptWindow.PositionNearCursor();
        _promptWindow.Activate();
        _promptWindow.FocusPrompt();
    }

    public void OpenAreaSelection()
    {
        OpenPrompt();
        if (_promptWindow is not null)
            _ = _promptWindow.BeginAreaSelectionAsync();
    }

    private void ShowWindowsUiRescue(ClickyWindows.Models.WindowsUiRescueKind rescue)
    {
        if (_rescueWindow is { IsVisible: true })
            _rescueWindow.Close();

        _rescueWindow = new WindowsUiRescueWindow(rescue);
        _rescueWindow.Closed += (_, _) => _rescueWindow = null;
        _rescueWindow.Show();
        _rescueWindow.Activate();
    }
}
