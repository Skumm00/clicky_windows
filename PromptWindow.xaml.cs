using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClickyWindows.Helpers;
using ClickyWindows.Services;

namespace ClickyWindows;

public partial class PromptWindow : Window
{
    private const double CompactHeight = 126;
    private const double MaximumExpandedHeight = 520;
    private readonly CompanionManager _companion;
    private readonly OverlayWindow _overlay;
    private bool _hasResponse;
    private int _resizeVersion;
    private System.Drawing.Rectangle? _pendingSelection;

    public PromptWindow(CompanionManager companion, OverlayWindow overlay)
    {
        InitializeComponent();
        Icon = AppIcon.CreateWindowIcon();
        _companion = companion;
        _overlay = overlay;
        _companion.ResponseReceived += ShowResponse;
        _companion.FeedbackReceived += ShowError;
        _companion.ScreenCaptureStarting += HideForScreenCapture;
        _companion.ScreenCaptureCompleted += RestoreAfterScreenCapture;
        Closed += (_, _) =>
        {
            _companion.ResponseReceived -= ShowResponse;
            _companion.FeedbackReceived -= ShowError;
            _companion.ScreenCaptureStarting -= HideForScreenCapture;
            _companion.ScreenCaptureCompleted -= RestoreAfterScreenCapture;
        };
        Loaded += (_, _) =>
        {
            PositionInBottomRight();
            FocusPrompt();
        };
    }

    public void FocusPrompt() => PromptBox.Focus();

    public void HideAndClearOutput()
    {
        Hide();
        ClearPreviousOutput();
    }

    public void PrepareForShow() => ClearPreviousOutput();

    public void PositionNearCursor()
    {
        if (!Win32.GetCursorPos(out var cursor))
            return;

        var dpiScale = CoordinateHelper.GetDpiScale();
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var bounds = screen.WorkingArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var desiredLeft = cursor.X / dpiScale + 16;
        var desiredTop = cursor.Y / dpiScale + 18;
        var leftEdge = bounds.Left / dpiScale + 8;
        var topEdge = bounds.Top / dpiScale + 8;
        var rightEdge = bounds.Right / dpiScale - width - 8;
        var bottomEdge = bounds.Bottom / dpiScale - height - 8;
        Left = Math.Clamp(desiredLeft, leftEdge, Math.Max(leftEdge, rightEdge));
        Top = Math.Clamp(desiredTop, topEdge, Math.Max(topEdge, bottomEdge));
    }

    private async void SendClick(object sender, RoutedEventArgs e) => await SendAsync();

    private async void SelectAreaClick(object sender, RoutedEventArgs e) => await BeginAreaSelectionAsync();

    private async void PromptPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((e.Key != Key.Enter && e.Key != Key.Return) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        e.Handled = true;
        await SendAsync();
    }

    private async Task SendAsync()
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || !SendButton.IsEnabled)
            return;

        _hasResponse = false;
        PromptBox.Clear();
        PromptBox.IsEnabled = false;
        SendButton.IsEnabled = false;
        var selectedBounds = _pendingSelection;
        _pendingSelection = null;
        StatusText.Text = selectedBounds.HasValue ? "Looking at selection..." : "Looking...";
        ResponseScroller.Visibility = Visibility.Collapsed;
        AnimateHeight(CompactHeight);

        Opacity = 0;
        var overlayWasVisible = _overlay.IsVisible;
        _overlay.Hide();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        var requestTask = selectedBounds.HasValue
            ? _companion.AskTypedRegionAsync(prompt, selectedBounds.Value)
            : _companion.AskTypedAsync(prompt);
        Opacity = 1;
        if (overlayWasVisible)
            _overlay.Show();
        await requestTask;

        PromptBox.IsEnabled = true;
        SendButton.IsEnabled = true;
        StatusText.Text = "Ready";
        FocusPrompt();
    }

    public async Task BeginAreaSelectionAsync()
    {
        if (!SendButton.IsEnabled)
            return;

        var overlayWasVisible = _overlay.IsVisible;
        Hide();
        _overlay.Hide();
        var selector = new ScreenSelectionWindow();
        selector.Show();
        var bounds = await selector.WaitForSelectionAsync();
        if (bounds is null)
        {
            Show();
            if (overlayWasVisible)
                _overlay.Show();
            Activate();
            FocusPrompt();
            return;
        }

        _pendingSelection = bounds.Value;
        _hasResponse = false;
        ResponseText.Text = "";
        ResponseScroller.Visibility = Visibility.Collapsed;
        AnimateHeight(CompactHeight);
        Show();
        if (overlayWasVisible)
            _overlay.Show();
        Activate();
        StatusText.Text = "Selection ready";
        FocusPrompt();
    }

    private void ShowResponse(string response)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _hasResponse = true;
            ResponseText.Text = response;
            ResponseText.Foreground = System.Windows.Media.Brushes.White;
            ResponseScroller.Visibility = Visibility.Visible;
            ResizeForResponse();
        });
    }

    private void ShowError(string message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_hasResponse)
                return;
            ResponseText.Text = ToEnglishFeedback(message);
            ResponseText.Foreground = System.Windows.Media.Brushes.Orange;
            ResponseScroller.Visibility = Visibility.Visible;
            ResizeForResponse();
        });
    }

    private void HideForScreenCapture() => Dispatcher.Invoke(() => Opacity = 0);

    private void RestoreAfterScreenCapture() => Dispatcher.Invoke(() => Opacity = 1);

    private void ResizeForResponse()
    {
        ResponseText.Measure(new System.Windows.Size(Math.Max(260, ActualWidth - 38), double.PositiveInfinity));
        var outputHeight = Math.Min(ResponseText.DesiredSize.Height, 360);
        AnimateHeight(Math.Clamp(CompactHeight + outputHeight + 20, CompactHeight + 54, MaximumExpandedHeight));
    }

    private void AnimateHeight(double targetHeight)
    {
        var version = ++_resizeVersion;
        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
        var currentTop = Top;
        var bottom = currentTop + currentHeight;
        var targetTop = bottom - targetHeight;

        BeginAnimation(HeightProperty, null);
        BeginAnimation(TopProperty, null);
        Height = currentHeight;
        Top = currentTop;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(210);
        var heightAnimation = new DoubleAnimation(currentHeight, targetHeight, duration) { EasingFunction = easing };
        var topAnimation = new DoubleAnimation(currentTop, targetTop, duration) { EasingFunction = easing };
        heightAnimation.Completed += (_, _) =>
        {
            if (version != _resizeVersion)
                return;
            BeginAnimation(HeightProperty, null);
            BeginAnimation(TopProperty, null);
            Height = targetHeight;
            Top = targetTop;
        };
        BeginAnimation(HeightProperty, heightAnimation);
        BeginAnimation(TopProperty, topAnimation);
    }

    private void GlassPanelMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInsideInteractiveControl(e.OriginalSource as DependencyObject))
            return;
        DragMove();
    }

    private static bool IsInsideInteractiveControl(DependencyObject? source)
    {
        for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.Primitives.ButtonBase)
                return true;
        }
        return false;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => HideAndClearOutput();

    private void ClearPreviousOutput()
    {
        _hasResponse = false;
        ResponseText.Text = "";
        ResponseText.Foreground = System.Windows.Media.Brushes.White;
        ResponseScroller.Visibility = Visibility.Collapsed;

        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
        var bottom = Top + currentHeight;
        BeginAnimation(HeightProperty, null);
        BeginAnimation(TopProperty, null);
        Height = CompactHeight;
        Top = bottom - CompactHeight;
    }

    private void PositionInBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 18;
        Top = workArea.Bottom - ActualHeight - 18;
    }

    private static string ToEnglishFeedback(string message)
    {
        if (message.Contains("yan", StringComparison.OrdinalIgnoreCase))
            return "Clicky couldn't answer. Check the model and API key in Settings.";
        if (message.Contains("duyamad", StringComparison.OrdinalIgnoreCase))
            return "I couldn't hear you.";
        if (message.Contains("lanamad", StringComparison.OrdinalIgnoreCase))
            return "Couldn't connect.";
        return message;
    }
}
