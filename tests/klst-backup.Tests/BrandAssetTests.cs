using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

/// <summary>
/// Guards the brand integration: the runtime mark (<see cref="AppIcon"/>) must stay
/// geometrically identical to the delivered artwork in <c>brand\assets</c>, and every
/// colour and gradient stop must match the design tokens. Without these checks the app
/// icon can silently drift from the brand it was drawn from.
/// </summary>
public class BrandAssetTests
{
    // ----- 1. The mark is drawn from the design's own path data -----

    [Fact]
    public void Plates_ParseAndKeepTheDesignBoundingBoxes()
    {
        // Bounds taken from the brand source comments: x[326,698] per plate.
        var upper = AppIcon.PrimaryPlate.Bounds;
        Assert.Equal(326d, upper.X, 3);
        Assert.Equal(214d, upper.Y, 3);
        Assert.Equal(372d, upper.Width, 3);
        Assert.Equal(269d, upper.Height, 3);

        // The lower plate is the same geometry shifted 327 units down the artboard.
        var lower = AppIcon.AccentPlate.Bounds;
        Assert.Equal(326d, lower.X, 3);
        Assert.Equal(541d, lower.Y, 3);
        Assert.Equal(372d, lower.Width, 3);
        Assert.Equal(269d, lower.Height, 3);
    }

    [Fact]
    public void Plates_UseEvenOddFillRule()
    {
        // Even-odd is what cuts the inner slot out of each plate. Under the default
        // non-zero rule the slot would render as a solid bump instead of a hole.
        Assert.Equal(FillRule.EvenOdd, FillRuleOf(AppIcon.PrimaryPlate));
        Assert.Equal(FillRule.EvenOdd, FillRuleOf(AppIcon.AccentPlate));
    }

    [Fact]
    public void Plates_MatchTheBrandSourceSvgPathData()
    {
        var svg = File.ReadAllText(Path.Combine(RepoRoot(), "brand", "assets", "app-icon-tile.svg"));

        // Both plates are lifted from the same file; the primary plate also appears in
        // app-icon-solid.svg and logo-stacked.svg with identical geometry.
        Assert.Contains(Normalize(AppIcon.PrimaryPlatePath), Normalize(svg), StringComparison.Ordinal);
        Assert.Contains(Normalize(AppIcon.AccentPlatePath), Normalize(svg), StringComparison.Ordinal);
    }

    [Fact]
    public void Plates_MatchTheSolidTileSvgPathData()
    {
        var svg = File.ReadAllText(Path.Combine(RepoRoot(), "brand", "assets", "app-icon-solid.svg"));

        Assert.Contains(Normalize(AppIcon.PrimaryPlatePath), Normalize(svg), StringComparison.Ordinal);
        Assert.Contains(Normalize(AppIcon.AccentPlatePath), Normalize(svg), StringComparison.Ordinal);
    }

    // ----- 2. Colour tokens -----

    [Fact]
    public void AccentColour_MatchesTheDesignAccent()
    {
        Assert.Equal(Color.FromRgb(0x57, 0xC8, 0xFF), AppIcon.AccentColor);
    }

    [Fact]
    public void TileBrush_IsTheFlatSolidTile()
    {
        // The application icon is app-icon-solid.svg: one flat #141733 field at every
        // size, no gradient, no sheen, no hairline.
        var brush = Assert.IsType<SolidColorBrush>(AppIcon.TileBrush);
        Assert.Equal(Color.FromRgb(0x14, 0x17, 0x33), brush.Color);
    }

    [Fact]
    public void LockupTileBrush_RunsTheDesignGradientCornerToCorner()
    {
        // The in-app logo mark keeps the graded tile of logo-horizontal-light.svg.
        var brush = Assert.IsType<LinearGradientBrush>(AppIcon.LockupTileBrush);

        Assert.Equal(new System.Windows.Point(0, 0), brush.StartPoint);
        Assert.Equal(new System.Windows.Point(1, 1), brush.EndPoint);
        Assert.Equal(
            new[] { 0x3355E0u, 0x212A80u, 0x12152Eu },
            brush.GradientStops.Select(s => s.Color).Select(ToHex));
        Assert.Equal(new[] { 0d, 0.46d, 1d }, brush.GradientStops.Select(s => s.Offset));
    }

    [Fact]
    public void Wordmark_MatchesTheLightLockupSvg()
    {
        var svg = File.ReadAllText(Path.Combine(RepoRoot(), "brand", "assets", "logo-horizontal-light.svg"));

        // "File" in ink #141733, " Backup" in #2AA8F5 — the light-field wordmark.
        Assert.Contains("#141733", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#2AA8F5", svg, StringComparison.OrdinalIgnoreCase);

        var ink = Assert.IsType<SolidColorBrush>(AppIcon.WordmarkInkBrush);
        var accent = Assert.IsType<SolidColorBrush>(AppIcon.WordmarkAccentBrush);
        Assert.Equal(Color.FromRgb(0x14, 0x17, 0x33), ink.Color);
        Assert.Equal(Color.FromRgb(0x2A, 0xA8, 0xF5), accent.Color);
    }

    [Fact]
    public void SheenBrush_FadesFromThirtyPercent()
    {
        var brush = Assert.IsType<RadialGradientBrush>(AppIcon.SheenBrush);

        Assert.Equal(new System.Windows.Point(0.26, 0.14), brush.Center);
        Assert.Equal(new byte[] { 0x4D, 0x0F, 0x00 },
            brush.GradientStops.Select(s => s.Color.A));
        Assert.All(brush.GradientStops,
            s => Assert.Equal(Color.FromRgb(0x8F, 0xB6, 0xFF), Color.FromRgb(s.Color.R, s.Color.G, s.Color.B)));
    }

    // ----- 3. Shape of the exported mark -----

    [Fact]
    public void ImageSource_IsVectorNotABitmap()
    {
        // Window.Icon must stay resolution-independent so the title bar, taskbar and
        // Alt+Tab each rasterise it at their own size instead of stretching one bitmap.
        Assert.IsType<DrawingImage>(AppIcon.ImageSource);
    }

    [Fact]
    public void BrandPrimitives_AreFrozenForCrossThreadReuse()
    {
        Assert.True(AppIcon.PrimaryPlate.IsFrozen);
        Assert.True(AppIcon.AccentPlate.IsFrozen);
        Assert.True(AppIcon.TileBrush.IsFrozen);
        Assert.True(AppIcon.LockupTileBrush.IsFrozen);
        Assert.True(AppIcon.SheenBrush.IsFrozen);
        Assert.True(AppIcon.WordmarkInkBrush.IsFrozen);
        Assert.True(AppIcon.WordmarkAccentBrush.IsFrozen);
        Assert.True(AppIcon.ImageSource.IsFrozen);
        Assert.True(AppIcon.LockupMarkSource.IsFrozen);
    }

    private static uint ToHex(Color c) => (uint)((c.R << 16) | (c.G << 8) | c.B);

    /// <summary>
    /// <see cref="Geometry.Parse"/> hands back a <see cref="StreamGeometry"/>, while a
    /// geometry authored in XAML is a <see cref="PathGeometry"/>; both carry the rule.
    /// </summary>
    private static FillRule FillRuleOf(Geometry geometry) => geometry switch
    {
        StreamGeometry stream => stream.FillRule,
        PathGeometry path => path.FillRule,
        _ => throw new InvalidOperationException(
            $"Unexpected geometry type {geometry.GetType().Name}."),
    };

    /// <summary>
    /// Strips the <c>F0</c> fill-rule prefix, then commas and whitespace, so SVG path data
    /// and WPF path markup for the same shape compare equal.
    /// </summary>
    private static string Normalize(string pathData)
    {
        var trimmed = pathData.TrimStart();
        if (trimmed.StartsWith("F0", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        return new string(trimmed.Where(c => !char.IsWhiteSpace(c) && c != ',').ToArray())
            .ToUpperInvariant();
    }

    /// <summary>Walks up from the test binaries until the solution file is found.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "klst-backup.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate klst-backup.sln above " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}
