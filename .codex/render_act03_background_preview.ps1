$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$mapRoot = Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Maps'
$envRoot = Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Tilesets\Env'
$scale = 4

function Get-TileBitmap([int] $gid) {
    if ($gid -le 300) {
        $local = $gid - 1
        $source = [System.Drawing.Bitmap]::new((Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Tilesets\Act_03_Environment.png'))
        $sourceRect = [System.Drawing.Rectangle]::new(($local % 20) * 16, [Math]::Floor($local / 20) * 16, 16, 16)
        return @($source, $sourceRect, $true)
    }
    $local = $gid - 301
    $row = [int][Math]::Floor($local / 20)
    $column = [int]($local % 20)
    $file = Join-Path $envRoot ("tile_r{0:D2}_c{1:D2}_{2:D3}.png" -f $row, $column, ([int]$local))
    return @([System.Drawing.Bitmap]::new($file), [System.Drawing.Rectangle]::new(0, 0, 16, 16), $false)
}

foreach ($mapPath in Get-ChildItem -LiteralPath $mapRoot -Filter 'Act_03_*.tmx' | Where-Object { $_.BaseName -in @('Act_03_A', 'Act_03_B') }) {
    $map = [xml](Get-Content -Raw -LiteralPath $mapPath.FullName)
    $width = [int]$map.map.width
    $height = [int]$map.map.height
    $layer = $map.map.layer | Where-Object { $_.id -eq '1' -and $_.name -eq 'Background' }
    $bytes = [Convert]::FromBase64String(($layer.data.'#text' -replace '\s', ''))
    $out = [System.Drawing.Bitmap]::new($width * $scale, 12 * $scale, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($out)
    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 21, 41, 74))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    for ($i = 0; $i -lt ($width * 12); $i++) {
        $gid = [BitConverter]::ToUInt32($bytes, $i * 4)
        if ($gid -eq 0) { continue }
        $pair = Get-TileBitmap $gid
        $bitmap = $pair[0]
        $sourceRect = $pair[1]
        $destination = [System.Drawing.Rectangle]::new(($i % $width) * $scale, [Math]::Floor($i / $width) * $scale, $scale, $scale)
        $graphics.DrawImage($bitmap, $destination, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
        if (-not $pair[2]) { $bitmap.Dispose() } else { $bitmap.Dispose() }
    }
    $graphics.Dispose()
    $outputPath = Join-Path $PSScriptRoot ($mapPath.BaseName + '_background_preview.png')
    $out.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    Write-Output $outputPath
}
