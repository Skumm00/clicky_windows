using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using ClickyWindows.Helpers;

namespace ClickyWindows;

public partial class ScreenSelectionWindow : Window
{
    private readonly System.Windows.Forms.Screen _screen;
    private readonly TaskCompletionSource<System.Drawing.Rectangle?> _completion = new();
    private System.Windows.Point _start;
    private bool _dragging;
    private double _dpiScale = 1;

    public ScreenSelectionWindow()
    {
        InitializeComponent();
        Win32.GetCursorPos(out var cursor);
        _screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            FullDim.Width = SelectionCanvas.ActualWidth;
            FullDim.Height = SelectionCanvas.ActualHeight;
            Activate();
            Focus();
        };
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public Task<System.Drawing.Rectangle?> WaitForSelectionAsync() => _completion.Task;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.SetWindowPos(
            hwnd,
            Win32.HWND_TOPMOST,
            _screen.Bounds.Left,
            _screen.Bounds.Top,
            _screen.Bounds.Width,
            _screen.Bounds.Height,
            Win32.SWP_SHOWWINDOW);
        _dpiScale = Win32.GetDpiForWindow(hwnd) / 96.0;
    }

    private void CanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = Clamp(e.GetPosition(SelectionCanvas));
        _dragging = true;
        SelectionCanvas.CaptureMouse();
        UpdateSelection(_start);
    }

    private void CanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragging)
            UpdateSelection(Clamp(e.GetPosition(SelectionCanvas)));
    }

    private async void CanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        SelectionCanvas.ReleaseMouseCapture();
        var end = Clamp(e.GetPosition(SelectionCanvas));
        var left = Math.Min(_start.X, end.X);
        var top = Math.Min(_start.Y, end.Y);
        var width = Math.Abs(end.X - _start.X);
        var height = Math.Abs(end.Y - _start.Y);
        if (width < 8 || height < 8)
        {
            CancelSelection();
            return;
        }

        var bounds = new System.Drawing.Rectangle(
            _screen.Bounds.Left + (int)Math.Round(left * _dpiScale),
            _screen.Bounds.Top + (int)Math.Round(top * _dpiScale),
            Math.Max(1, (int)Math.Round(width * _dpiScale)),
            Math.Max(1, (int)Math.Round(height * _dpiScale)));
        Hide();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        _completion.TrySetResult(bounds);
        Close();
    }

    private void UpdateSelection(System.Windows.Point current)
    {
        var left = Math.Min(_start.X, current.X);
        var top = Math.Min(_start.Y, current.Y);
        var right = Math.Max(_start.X, current.X);
        var bottom = Math.Max(_start.Y, current.Y);
        var canvasWidth = SelectionCanvas.ActualWidth;
        var canvasHeight = SelectionCanvas.ActualHeight;

        FullDim.Visibility = Visibility.Collapsed;
        SetRect(DimTop, 0, 0, canvasWidth, top);
        SetRect(DimBottom, 0, bottom, canvasWidth, Math.Max(0, canvasHeight - bottom));
        SetRect(DimLeft, 0, top, left, Math.Max(0, bottom - top));
        SetRect(DimRight, right, top, Math.Max(0, canvasWidth - right), Math.Max(0, bottom - top));

        SelectionBox.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBox, left);
        Canvas.SetTop(SelectionBox, top);
        SelectionBox.Width = Math.Max(1, right - left);
        SelectionBox.Height = Math.Max(1, bottom - top);
    }

    private static void SetRect(System.Windows.Shapes.Rectangle rectangle, double left, double top, double width, double height)
    {
        rectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        rectangle.Width = width;
        rectangle.Height = height;
    }

    private System.Windows.Point Clamp(System.Windows.Point point) => new(
        Math.Clamp(point.X, 0, SelectionCanvas.ActualWidth),
        Math.Clamp(point.Y, 0, SelectionCanvas.ActualHeight));

    private void WindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        CancelSelection();
    }

    private void CancelSelection()
    {
        _completion.TrySetResult(null);
        Close();
    }
}
