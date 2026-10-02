param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'obj'), [string]$IconName = 'AppIcon', [switch]$Transparent)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
[xml]$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'assets\through.svg') -Raw
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'ThroughPath.txt'), (($source.svg.path | ForEach-Object d) -join ' '), [Text.UTF8Encoding]::new($false))
function Convert-IconPath([string]$data) {
    $tokens = [regex]::Matches($data,'[MLC]|-?\d+(?:\.\d+)?') | ForEach-Object Value
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $lastX = [single]0; $lastY = [single]0
    for ($at = 0; $at -lt $tokens.Count;) {
        $command = $tokens[$at++]
        switch ($command) {
            M { $lastX = [single]::Parse($tokens[$at++],[Globalization.CultureInfo]::InvariantCulture); $lastY = [single]::Parse($tokens[$at++],[Globalization.CultureInfo]::InvariantCulture); $path.StartFigure() }
            L { $x = [single]::Parse($tokens[$at++],[Globalization.CultureInfo]::InvariantCulture); $y = [single]::Parse($tokens[$at++],[Globalization.CultureInfo]::InvariantCulture); $path.AddLine($lastX,$lastY,$x,$y); $lastX = $x; $lastY = $y }
            C {
                $values = @(); for ($i = 0; $i -lt 6; $i++) { $values += [single]::Parse($tokens[$at++],[Globalization.CultureInfo]::InvariantCulture) }
                $path.AddBezier($lastX,$lastY,$values[0],$values[1],$values[2],$values[3],$values[4],$values[5]); $lastX = $values[4]; $lastY = $values[5]
            }
            default { throw "Unsupported icon path command: $command" }
        }
    }
    return ,$path
}
$images = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    # Render every size from the vector. Small icons receive a slightly stronger stroke.
    $scale = $size * 4 / 64.0
    $large = New-Object Drawing.Bitmap ($size * 4),($size * 4)
    $graphics = [Drawing.Graphics]::FromImage($large)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform($scale,$scale)
    $tile = New-Object Drawing.Drawing2D.GraphicsPath
    $tile.AddArc(0,0,32,32,180,90); $tile.AddArc(32,0,32,32,270,90)
    $tile.AddArc(32,32,32,32,0,90); $tile.AddArc(0,32,32,32,90,90); $tile.CloseFigure()
    $brush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(243,148,133))
    if (-not $Transparent) { $graphics.FillPath($brush,$tile) }
    foreach ($shape in $source.svg.path) {
        $path = Convert-IconPath $shape.d
        $stroke = if ($size -le 24) { 6 } else { [single]$shape.'stroke-width' }
        $markColor = if ($Transparent) { [Drawing.Color]::FromArgb(132,136,144) } else { [Drawing.Color]::FromArgb(36,27,27) }
        $pen = New-Object Drawing.Pen $markColor,$stroke
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
        $graphics.DrawPath($pen,$path); $pen.Dispose(); $path.Dispose()
    }
    $graphics.Dispose(); $brush.Dispose(); $tile.Dispose()
    $bitmap = New-Object Drawing.Bitmap $size,$size
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = 'HighQualityBicubic'; $graphics.PixelOffsetMode = 'HighQuality'
    $graphics.DrawImage($large,0,0,$size,$size)
    $memory = New-Object IO.MemoryStream
    $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
    $images += @{ Size = $size; Bytes = $memory.ToArray() }
    if ($size -eq 256) { $bitmap.Save((Join-Path $OutputDirectory ($IconName + '.png')),[Drawing.Imaging.ImageFormat]::Png) }
    $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $large.Dispose()
}
$writer = New-Object IO.BinaryWriter ([IO.File]::Create((Join-Path $OutputDirectory ($IconName + '.ico'))))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
} finally { $writer.Dispose() }
