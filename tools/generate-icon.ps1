# Generates src\klst-backup\Assets\app.ico from the brand design.
#
# Source of truth: brand\assets\app-icon-solid.svg - the flat #141733 tile is the
# application icon at every size. Services\AppIcon.cs draws the same geometry at
# runtime for the window and tray icons - if the mark changes, change both. The
# path data below is copied verbatim from that SVG.
#
# Run:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\generate-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$icoPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\klst-backup\Assets\app.ico'))

# Design space of the brand artboard. Every coordinate below is in these units.
$Design = 1024.0
$TileRadius = 224.0
$Accent = [System.Drawing.Color]::FromArgb(0x57, 0xC8, 0xFF)
$FlatTile = [System.Drawing.Color]::FromArgb(0x14, 0x17, 0x33)

function New-RoundedRectPath {
    param([double]$X, [double]$Y, [double]$W, [double]$H, [double]$R)

    $d = 2.0 * $R
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc([float]$X, [float]$Y, [float]$d, [float]$d, 180.0, 90.0)
    $p.AddArc([float]($X + $W - $d), [float]$Y, [float]$d, [float]$d, 270.0, 90.0)
    $p.AddArc([float]($X + $W - $d), [float]($Y + $H - $d), [float]$d, [float]$d, 0.0, 90.0)
    $p.AddArc([float]$X, [float]($Y + $H - $d), [float]$d, [float]$d, 90.0, 90.0)
    $p.CloseFigure()
    return $p
}

function New-PlatePath {
    param([double]$OffsetY)

    # Alternate (even-odd) is what turns the inner slot into a hole instead of a
    # second shape - the same reason the brand SVG sets fill-rule="evenodd".
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate

    # Half-round plate: the arc is a 269-unit semicircle bulging to x = 698.
    $top = 214.0 + $OffsetY
    $p.AddLine(326.0, [float]$top, 563.5, [float]$top)
    $p.AddArc((New-Object System.Drawing.RectangleF(429.0, [float]$top, 269.0, 269.0)), -90.0, 180.0)
    $p.AddLine(563.5, [float]($top + 269.0), 326.0, [float]($top + 269.0))
    $p.CloseFigure()

    # Slot cut out of the plate: 97-unit semicircle bulging to x = 612.
    $slot = 300.0 + $OffsetY
    $p.AddLine(412.0, [float]$slot, 563.5, [float]$slot)
    $p.AddArc((New-Object System.Drawing.RectangleF(515.0, [float]$slot, 97.0, 97.0)), -90.0, 180.0)
    $p.AddLine(563.5, [float]($slot + 97.0), 412.0, [float]($slot + 97.0))
    $p.CloseFigure()

    return $p
}

function New-TileBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Everything below draws in design units, not pixels.
    $g.ScaleTransform([float]($Size / $Design), [float]($Size / $Design))

    $body = New-RoundedRectPath -X 0 -Y 0 -W $Design -H $Design -R $TileRadius

    # The solid tile: one flat field at every size, exactly as app-icon-solid.svg.
    $flat = New-Object System.Drawing.SolidBrush($FlatTile)
    $g.FillPath($flat, $body)

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillPath($white, (New-PlatePath -OffsetY 0))

    $accent = New-Object System.Drawing.SolidBrush($Accent)
    $g.FillPath($accent, (New-PlatePath -OffsetY 327))

    $g.Dispose()

    return $bmp
}

# Encodes a bitmap as an icon DIB: a BITMAPINFOHEADER whose height covers the colour
# image plus the 1bpp AND mask, then the bottom-up BGRA rows, then the mask.
function ConvertTo-IcoDib {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height

    $data = $Bitmap.LockBits(
        (New-Object System.Drawing.Rectangle(0, 0, $w, $h)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $stride = $data.Stride
        $raw = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # BITMAPINFOHEADER. biHeight is doubled: ICO DIBs stack the XOR image and the mask.
    $bw.Write([int32]40)                    # biSize
    $bw.Write([int32]$w)                    # biWidth
    $bw.Write([int32]($h * 2))              # biHeight
    $bw.Write([int16]1)                     # biPlanes
    $bw.Write([int16]32)                    # biBitCount
    $bw.Write([int32]0)                     # biCompression = BI_RGB
    $bw.Write([int32]($w * $h * 4))         # biSizeImage
    $bw.Write([int32]0)                     # biXPelsPerMeter
    $bw.Write([int32]0)                     # biYPelsPerMeter
    $bw.Write([int32]0)                     # biClrUsed
    $bw.Write([int32]0)                     # biClrImportant

    # XOR image: DIBs are stored bottom-up.
    for ($y = $h - 1; $y -ge 0; $y--) {
        $bw.Write($raw, $y * $stride, $w * 4)
    }

    # AND mask: 1bpp, each row padded to a 4-byte boundary. All zero - the alpha channel
    # in the XOR image is what actually shapes the icon.
    $maskStride = [int]([math]::Ceiling($w / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskStride * $h)))

    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()

    # -NoEnumerate keeps the byte[] intact: emitting it normally would unroll it into
    # one output object per byte and the caller would receive 4000 loose bytes.
    Write-Output -NoEnumerate $bytes
}

# Windows picks the nearest entry per context.
$sizes = @(16, 24, 32, 48, 64, 128, 256)

# A typed list, not an array: assigning a byte[] into it must not enumerate the bytes.
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bmp = New-TileBitmap -Size $size

    # 256 is stored as PNG (a 256px DIB would be ~262 KB). Everything smaller stays an
    # uncompressed DIB because System.Drawing.Icon, WiX's <Icon> element and most
    # non-shell tooling cannot decode PNG-compressed icon entries.
    if ($size -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($ms.ToArray())
        $ms.Dispose()
    }
    else {
        $images.Add((ConvertTo-IcoDib -Bitmap $bmp))
    }

    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)

# ICONDIR: reserved, type = icon, image count.
$w.Write([int16]0)
$w.Write([int16]1)
$w.Write([int16]$images.Count)

# ICONDIRENTRY for each image, then the image data itself.
$offset = 6 + 16 * $images.Count
for ($i = 0; $i -lt $images.Count; $i++) {
    $size = $sizes[$i]
    $data = $images[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }   # 0 encodes 256 in the directory

    $w.Write([byte]$dim)                 # width
    $w.Write([byte]$dim)                 # height
    $w.Write([byte]0)                    # palette entries
    $w.Write([byte]0)                    # reserved
    $w.Write([int16]1)                   # colour planes
    $w.Write([int16]32)                  # bits per pixel
    $w.Write([int32]$data.Length)        # bytes in resource
    $w.Write([int32]$offset)             # offset to resource

    $offset += $data.Length
}

foreach ($data in $images) {
    $w.Write($data)
}

$w.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())

$w.Dispose()
$out.Dispose()

Write-Output ("Created {0} ({1} bytes, sizes: {2})" -f $icoPath, (Get-Item $icoPath).Length, ($sizes -join ', '))
