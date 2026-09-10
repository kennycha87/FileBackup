using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace KlstBackup.Services;

/// <summary>Draws the application icon at runtime so no binary .ico asset is required.</summary>
public static class AppIcon
{
    private static Drawing.Icon? _cached;

    public static Drawing.Icon CreateIcon()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new Drawing2D.LinearGradientBrush(
                new Drawing.Rectangle(0, 0, 32, 32),
                Drawing.Color.FromArgb(0, 120, 215),
                Drawing.Color.FromArgb(0, 70, 150),
                90f);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Drawing.Font("Segoe UI", 15f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
            var size = g.MeasureString("B", font);
            g.DrawString("B", font, textBrush, (32 - size.Width) / 2f, (32 - size.Height) / 2f + 1f);
        }

        _cached = Drawing.Icon.FromHandle(bmp.GetHicon());
        return _cached;
    }

    public static ImageSource CreateImageSource()
    {
        var icon = CreateIcon();
        return Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }
}
