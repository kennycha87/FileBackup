using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace KlstBackup.Services;

/// <summary>
/// The product mark, rebuilt from the same vector geometry as the brand sources.
/// Two renderings share one path definition: the flat tile of
/// <c>brand/assets/app-icon-solid.svg</c> is the application icon (executable, title bar,
/// taskbar, tray), while the graded tile of the horizontal lockups is the mark inside
/// the in-app logo on a light field. One definition feeds every surface, so the artwork
/// shipped with the application can never drift from the design.
/// </summary>
public static class AppIcon
{
    /// <summary>Side of the square artboard the brand tile was authored on.</summary>
    public const double DesignSize = 1024d;

    /// <summary>Tile corner radius in design units — 21.875% of the artboard.</summary>
    public const double TileRadius = 224d;

    /// <summary>
    /// Upper vault plate. Path data copied verbatim from the brand source; the leading
    /// <c>F0</c> selects the even-odd rule the design relies on to cut the slot out of
    /// each plate.
    /// </summary>
    public const string PrimaryPlatePath =
        "F0 M326,214 H563.5 A134.5,134.5 0 0 1 563.5,483 H326 Z " +
        "M412,300 H563.5 A48.5,48.5 0 0 1 563.5,397 H412 Z";

    /// <summary>Lower vault plate, shifted 327 units down the same artboard.</summary>
    public const string AccentPlatePath =
        "F0 M326,541 H563.5 A134.5,134.5 0 0 1 563.5,810 H326 Z " +
        "M412,627 H563.5 A48.5,48.5 0 0 1 563.5,724 H412 Z";

    /// <summary>Brand accent <c>#57C8FF</c> — the lower plate on the tile.</summary>
    public static Color AccentColor { get; } = Color.FromRgb(0x57, 0xC8, 0xFF);

    /// <summary>Upper plate, painted white on the tile.</summary>
    public static Geometry PrimaryPlate { get; } = Freeze(Geometry.Parse(PrimaryPlatePath));

    /// <summary>Lower plate, painted in the brand accent.</summary>
    public static Geometry AccentPlate { get; } = Freeze(Geometry.Parse(AccentPlatePath));

    /// <summary>
    /// The application icon tile: flat <c>#141733</c>, exactly as
    /// <c>brand/assets/app-icon-solid.svg</c> paints it. A single flat field keeps the
    /// 16 px tray glyph legible and matches the delivered icon artwork at every size.
    /// </summary>
    public static Brush TileBrush { get; } =
        Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x17, 0x33)));

    /// <summary>
    /// The lockup tile keeps the three-stop indigo gradient of
    /// <c>brand/assets/logo-horizontal-light.svg</c>, where the mark sits large enough
    /// on the light field for the gradient to read.
    /// </summary>
    public static Brush LockupTileBrush { get; } = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0x33, 0x55, 0xE0), 0d),
            new GradientStop(Color.FromRgb(0x21, 0x2A, 0x80), 0.46d),
            new GradientStop(Color.FromRgb(0x12, 0x15, 0x2E), 1d),
        },
    });

    /// <summary>Top-left sheen (<c>#8FB6FF</c> at 30%) on the lockup tile only.</summary>
    public static Brush SheenBrush { get; } = Freeze(new RadialGradientBrush
    {
        Center = new Point(0.26, 0.14),
        RadiusX = 0.92,
        RadiusY = 0.92,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x4D, 0x8F, 0xB6, 0xFF), 0d),
            new GradientStop(Color.FromArgb(0x0F, 0x8F, 0xB6, 0xFF), 0.45d),
            new GradientStop(Color.FromArgb(0x00, 0x8F, 0xB6, 0xFF), 1d),
        },
    });

    /// <summary>Hairline drawn just inside the lockup tile edge in the brand source.</summary>
    public static Brush HairlineBrush { get; } =
        Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)));

    /// <summary>
    /// Wordmark ink on the light field — <c>#141733</c>, the "File" of
    /// <c>logo-horizontal-light.svg</c>. Also the tagline and version ink at reduced opacity.
    /// </summary>
    public static Brush WordmarkInkBrush { get; } =
        Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x17, 0x33)));

    /// <summary>Wordmark accent on the light field — <c>#2AA8F5</c>, the "Backup".</summary>
    public static Brush WordmarkAccentBrush { get; } =
        Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0xA8, 0xF5)));

    private static readonly DrawingGroup IconTile = BuildIconTile();
    private static readonly DrawingGroup LockupTile = BuildLockupTile();
    private static readonly DrawingImage TileImage = Freeze(new DrawingImage(IconTile));
    private static readonly DrawingImage LockupImage = Freeze(new DrawingImage(LockupTile));

    private static Drawing.Icon? _cachedIcon;

    /// <summary>
    /// The flat tile as vector, for <see cref="Window.Icon"/> and the tray. Kept as a
    /// <see cref="DrawingImage"/> rather than a bitmap so the title bar, the taskbar and
    /// Alt+Tab each rasterise it at their own size.
    /// </summary>
    public static ImageSource ImageSource => TileImage;

    /// <summary>
    /// The graded lockup mark for the in-app logo on a light field — the tile half of
    /// <c>logo-horizontal-light.svg</c>.
    /// </summary>
    public static ImageSource LockupMarkSource => LockupImage;

    /// <summary>
    /// Rasterises the flat tile into a GDI+ icon. <c>NotifyIcon</c> predates WPF and
    /// cannot take an <see cref="ImageSource"/>.
    /// </summary>
    public static Drawing.Icon CreateIcon(int size = 32)
    {
        if (_cachedIcon is not null)
        {
            return _cachedIcon;
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(size / DesignSize, size / DesignSize));
            context.DrawDrawing(IconTile);
        }

        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        _cachedIcon = ToIcon(target, size);
        return _cachedIcon;
    }

    /// <summary>Paints the application icon: flat tile, then the two plates.</summary>
    private static DrawingGroup BuildIconTile()
    {
        var group = new DrawingGroup();

        group.Children.Add(new GeometryDrawing(TileBrush, null, TileGeometry()));
        group.Children.Add(new GeometryDrawing(Brushes.White, null, PrimaryPlate));
        group.Children.Add(new GeometryDrawing(
            Freeze(new SolidColorBrush(AccentColor)), null, AccentPlate));

        return Freeze(group);
    }

    /// <summary>Paints the lockup mark in the same order as the brand source.</summary>
    private static DrawingGroup BuildLockupTile()
    {
        var group = new DrawingGroup();

        group.Children.Add(new GeometryDrawing(LockupTileBrush, null, TileGeometry()));
        group.Children.Add(new GeometryDrawing(SheenBrush, null, TileGeometry()));
        group.Children.Add(new GeometryDrawing(
            null,
            new Pen(HairlineBrush, 6),
            RoundedTile(5, DesignSize - 10, TileRadius - 5)));
        group.Children.Add(new GeometryDrawing(Brushes.White, null, PrimaryPlate));
        group.Children.Add(new GeometryDrawing(
            Freeze(new SolidColorBrush(AccentColor)), null, AccentPlate));

        return Freeze(group);
    }

    private static Geometry TileGeometry() => RoundedTile(0, DesignSize, TileRadius);

    private static RectangleGeometry RoundedTile(double inset, double side, double radius) =>
        new(new Rect(inset, inset, side, side), radius, radius);

    private static Drawing.Icon ToIcon(BitmapSource source, int size)
    {
        var stride = size * 4;
        var pixels = new byte[stride * size];
        source.CopyPixels(pixels, stride, 0);

        using var bitmap = new Drawing.Bitmap(size, size, DrawingImaging.PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(
            new Drawing.Rectangle(0, 0, size, size),
            DrawingImaging.ImageLockMode.WriteOnly,
            DrawingImaging.PixelFormat.Format32bppPArgb);

        try
        {
            // Pbgra32 and Format32bppPArgb share one memory layout: premultiplied BGRA.
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Icon does not take ownership of the handle, so clone before destroying it.
            return (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
