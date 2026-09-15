Add-Type -AssemblyName System.Drawing

$icoPath = "c:\Users\kenny\Bitbucket\FileBackup\src\klst-backup\Assets\app.ico"

# Create 256x256 bitmap matching AppIcon.cs design
$bmp = New-Object System.Drawing.Bitmap(256, 256)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

# Blue gradient circle
$rect  = New-Object System.Drawing.Rectangle(0, 0, 256, 256)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(0, 120, 215),
    [System.Drawing.Color]::FromArgb(0, 70, 150),
    90)
$g.FillEllipse($brush, 8, 8, 240, 240)

# White "B"
$font  = New-Object System.Drawing.Font('Segoe UI', 120, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$tbrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sz = $g.MeasureString('B', $font)
$g.DrawString('B', $font, $tbrush, (256 - $sz.Width) / 2, (256 - $sz.Height) / 2 + 2)

$g.Dispose()
$font.Dispose()
$tbrush.Dispose()
$brush.Dispose()

# Save as PNG in memory, then wrap in ICO container
$ms  = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$bmp.Dispose()

$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($ico)

# ICO header: reserved=0, type=1(icon), count=1
$bw.Write([int16]0)
$bw.Write([int16]1)
$bw.Write([int16]1)

# ICO directory entry
$bw.Write([byte]0)          # width  (0 = 256)
$bw.Write([byte]0)          # height (0 = 256)
$bw.Write([byte]0)          # color palette
$bw.Write([byte]0)          # reserved
$bw.Write([int16]1)         # color planes
$bw.Write([int16]32)        # bits per pixel
$bw.Write([int32]$png.Length)  # image data size
$bw.Write([int32]22)           # offset to image data (6+16=22)

# PNG image data
$bw.Write($png)
$bw.Flush()

[System.IO.File]::WriteAllBytes($icoPath, $ico.ToArray())
$bw.Dispose()
$ico.Dispose()
$ms.Dispose()

Write-Output "Icon created at: $icoPath ($((Get-Item $icoPath).Length) bytes)"
