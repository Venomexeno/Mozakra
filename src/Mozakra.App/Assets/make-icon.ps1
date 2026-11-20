# Regenerates Assets/mozakra.ico (a yellow sticky note with a folded corner and ink lines).
# Run from this folder: powershell -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 32.0
    $m = [Math]::Max(1, [Math]::Round(2 * $k))          # margin
    $fold = [Math]::Round(8 * $k)                          # folded corner size
    $x0 = $m; $y0 = $m; $x1 = $s - $m; $y1 = $s - $m

    # note body (without the bottom-right corner)
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddPolygon(@(
        (New-Object System.Drawing.PointF $x0, $y0),
        (New-Object System.Drawing.PointF $x1, $y0),
        (New-Object System.Drawing.PointF $x1, ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), $y1),
        (New-Object System.Drawing.PointF $x0, $y1)))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 243, 163))), $body)
    $g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(176, 140, 40)), ([Math]::Max(1, $k))), $body)

    # folded corner
    $foldPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $foldPath.AddPolygon(@(
        (New-Object System.Drawing.PointF $x1, ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), $y1)))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(232, 205, 110))), $foldPath)

    # accent pin strip + ink lines
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(232, 93, 63))), $x0, $y0, ($x1 - $x0), [Math]::Max(2, [Math]::Round(4 * $k)))
    $ink = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(43, 43, 43)), ([Math]::Max(1, 2 * $k))
    foreach ($ly in 12, 17, 22) {
        $len = if ($ly -eq 22) { 12 } else { 16 }
        $g.DrawLine($ink, [float](7 * $k), [float]($ly * $k), [float]((7 + $len) * $k), [float]($ly * $k))
    }
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    if ($s -ge 256) {
        # large entry: PNG-compressed (standard for 256px)
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # small entries: classic 32-bit DIB (BITMAPINFOHEADER + BGRA bottom-up + AND mask),
        # the most compatible format for tray/taskbar icons
        $bw = New-Object System.IO.BinaryWriter $ms
        $maskStride = [int]([Math]::Ceiling($s / 32.0) * 4)
        $bw.Write([UInt32]40); $bw.Write([Int32]$s); $bw.Write([Int32]($s * 2))
        $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]0)
        $bw.Write([UInt32]($s * $s * 4 + $maskStride * $s))
        $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $bw.Write((New-Object byte[] ($maskStride * $s)))   # all-zero mask: alpha channel decides
        $bw.Flush()
    }
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}

# ICO container
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'mozakra.ico'), $out.ToArray())
Write-Host "Wrote mozakra.ico"
