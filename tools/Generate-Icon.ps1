param([string]$OutputPath = (Join-Path $PSScriptRoot '..\src\ModernCalculator.WinForms\Resources\aster.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconDirectory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($iconDirectory) | Out-Null
$images = [System.Collections.Generic.List[byte[]]]::new()
$sizes = @(16, 32, 48, 64, 128, 256)
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $inset = [single]($size / 32)
    $edge = [single]($size - 2 * $inset)
    $radius = [single]($size * .22)
    $path.AddArc($inset, $inset, $radius, $radius, 180, 90)
    $path.AddArc(($size - $inset - $radius), $inset, $radius, $radius, 270, 90)
    $path.AddArc(($size - $inset - $radius), ($size - $inset - $radius), $radius, $radius, 0, 90)
    $path.AddArc($inset, ($size - $inset - $radius), $radius, $radius, 90, 90)
    $path.CloseFigure()
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(24, 25, 28))
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(159, 190, 255))
    $graphics.FillPath($background, $path)
    $scale = [single]($size / 32)
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(16 * $scale, 5 * $scale), [System.Drawing.PointF]::new(19 * $scale, 13 * $scale),
        [System.Drawing.PointF]::new(27 * $scale, 16 * $scale), [System.Drawing.PointF]::new(19 * $scale, 19 * $scale),
        [System.Drawing.PointF]::new(16 * $scale, 27 * $scale), [System.Drawing.PointF]::new(13 * $scale, 19 * $scale),
        [System.Drawing.PointF]::new(5 * $scale, 16 * $scale), [System.Drawing.PointF]::new(13 * $scale, 13 * $scale))
    $graphics.FillPolygon($accent, $points)
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose(); $accent.Dispose(); $background.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create([System.IO.Path]::GetFullPath($OutputPath))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($data in $images) { $writer.Write($data) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output "Generated original Aster icon: $OutputPath"
