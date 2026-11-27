$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mapRoot = Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Maps'
$encoding = [System.Text.UTF8Encoding]::new($false)
$firstGidAct03 = 301
$width = 256
$height = 24

function Set-Cell([uint32[]] $grid, [int] $x, [int] $y, [uint32] $gid) {
    if ($x -ge 0 -and $x -lt $width -and $y -ge 0 -and $y -lt $height) {
        $grid[($y * $width) + $x] = $gid
    }
}

function Tile([int] $localId) {
    return [uint32]($firstGidAct03 + $localId)
}

function Stamp-SourceRect(
    [uint32[]] $grid,
    [int] $destX,
    [int] $destY,
    [int] $sourceX,
    [int] $sourceY,
    [int] $tileWidth,
    [int] $tileHeight
) {
    for ($y = 0; $y -lt $tileHeight; $y++) {
        for ($x = 0; $x -lt $tileWidth; $x++) {
            $localId = (($sourceY + $y) * 20) + $sourceX + $x
            Set-Cell $grid ($destX + $x) ($destY + $y) (Tile $localId)
        }
    }
}

function Encode-Grid([uint32[]] $grid) {
    $bytes = [byte[]]::new($grid.Length * 4)
    for ($i = 0; $i -lt $grid.Length; $i++) {
        [Array]::Copy([BitConverter]::GetBytes($grid[$i]), 0, $bytes, $i * 4, 4)
    }
    return [Convert]::ToBase64String($bytes)
}

function Decode-Grid([string] $base64) {
    $bytes = [Convert]::FromBase64String(($base64 -replace '\s', ''))
    if (($bytes.Length % 4) -ne 0) { throw 'Background data is not 32-bit tile data.' }
    $grid = [uint32[]]::new($bytes.Length / 4)
    for ($i = 0; $i -lt $grid.Length; $i++) {
        $grid[$i] = [BitConverter]::ToUInt32($bytes, $i * 4)
    }
    return ,$grid
}

$placements = @{
    'Act_03_A' = @{
        Moon = @(126, 1)
        Clouds = @(@(10, 4), @(62, 8), @(204, 3))
    }
    'Act_03_B' = @{
        Moon = @(164, 1)
        Clouds = @(@(4, 7), @(58, 3), @(214, 6))
    }
}

foreach ($mapName in $placements.Keys) {
    $path = Join-Path $mapRoot ($mapName + '.tmx')
    if (-not (Test-Path -LiteralPath $path)) { throw "Map not found: $path" }

    $text = [IO.File]::ReadAllText($path, $encoding)
    $xml = [xml]$text
    if ([int]$xml.map.width -ne $width -or [int]$xml.map.height -ne $height) {
        throw "$mapName has an unexpected map size. Expected ${width}x${height}."
    }
    $background = $xml.map.layer | Where-Object { $_.id -eq '1' -and $_.name -eq 'Background' }
    if ($null -eq $background) { throw "$mapName does not contain layer id=1 Background." }

    $grid = Decode-Grid ([string]$background.data.'#text')
    if ($grid.Length -ne ($width * $height)) { throw "$mapName Background has an unexpected cell count." }

    # Full moon from Env_Tileset.jpg: source rows 11-12, columns 3-5.
    $moon = $placements[$mapName].Moon
    Stamp-SourceRect $grid $moon[0] $moon[1] 3 11 3 2

    # Cloud silhouette from Env_Tileset.jpg: source rows 11-14, columns 0-2.
    foreach ($cloud in $placements[$mapName].Clouds) {
        Stamp-SourceRect $grid $cloud[0] $cloud[1] 0 11 3 4
    }

    $newBase64 = Encode-Grid $grid
    $pattern = '(?s)(<layer\s+id="1"\s+name="Background".*?<data\s+encoding="base64">)\s*[^<]+?(\s*</data>)'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) { throw "Could not locate Background data in $mapName." }
    $replacement = $match.Groups[1].Value + "`n" + $newBase64 + $match.Groups[2].Value
    $updated = $text.Substring(0, $match.Index) + $replacement + $text.Substring($match.Index + $match.Length)
    $updated = $updated.Replace(
        'Act_03_Environment.png structures; Env_Tileset.jpg moon and bats',
        'Act_03_Environment.png structures; Env_Tileset.jpg full moon, clouds and bats'
    )
    [IO.File]::WriteAllText($path, $updated, $encoding)
    Write-Output "Updated $mapName Background with full moon and 3 cloud formations."
}
