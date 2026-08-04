using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Media;

namespace ClickyWindows.Helpers;

public static class AppIcon
{
    private static readonly PointF[] CursorPoints =
    [
        new(4, 3), new(4, 35), new(35, 19),
    ];

    public static Icon CreateTrayIcon()
    {
        var bitmap = new Bitmap(40, 40);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);
        using var shadow = new SolidBrush(System.Drawing.Color.FromArgb(64, 0, 0, 0));
        using var fill = new SolidBrush(System.Drawing.Color.FromArgb(255, 0, 85, 255));
        using var path = new GraphicsPath();
        path.AddPolygon(CursorPoints);
        graphics.TranslateTransform(1.5f, 1.5f);
        graphics.FillPath(shadow, path);
        graphics.ResetTransform();
        graphics.FillPath(System.Drawing.Brushes.White, path);
        graphics.FillPath(fill, path);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public static ImageSource CreateWindowIcon()
    {
        var image = new DrawingImage(new GeometryDrawing(
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 85, 255)),
            null,
            Geometry.Parse("M4,3 L4,35 L35,19 Z")));
        image.Freeze();
        return image;
    }
}
