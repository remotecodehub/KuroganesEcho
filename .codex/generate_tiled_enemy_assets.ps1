$ErrorActionPreference = 'Stop'

$tiledRoot = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled'
$sourceRoot = Join-Path $tiledRoot 'Enemies'
$frameRoot = Join-Path $sourceRoot 'GeneratedFrames'
$tilesetRoot = Join-Path $tiledRoot 'Tilesets\Enemies'

function Get-PngDimensions([string] $path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) {
        throw "PNG inválido: $path"
    }

    return @{
        Width = ([int]$bytes[16] * 16777216) + ([int]$bytes[17] * 65536) + ([int]$bytes[18] * 256) + [int]$bytes[19]
        Height = ([int]$bytes[20] * 16777216) + ([int]$bytes[21] * 65536) + ([int]$bytes[22] * 256) + [int]$bytes[23]
    }
}

function XmlEscape([string] $value) {
    return [System.Security.SecurityElement]::Escape($value)
}

function Get-StateName([string] $name) {
    if ($name -eq 'Attack') { return 'Attack1' }
    if ($name -eq 'walk') { return 'Walk' }
    return $name -replace '[^A-Za-z0-9_]+', '_'
}

function Get-FrameDuration([string] $state) {
    switch -Regex ($state) {
        '^Idle' { return 120 }
        '^Walk$' { return 100 }
        '^Run$' { return 80 }
        '^Jump$' { return 110 }
        '^Hurt$' { return 100 }
        '^Dead$' { return 140 }
        '^Attack' { return 90 }
        default { return 100 }
    }
}

New-Item -ItemType Directory -Force -Path $frameRoot, $tilesetRoot | Out-Null

$variantDirectories = Get-ChildItem -LiteralPath $sourceRoot -Directory |
    Where-Object { $_.Name -notin @('GeneratedFrames', '__MACOSX', 'PSD') } |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Directory |
            Where-Object { $_.Name -notin @('__MACOSX', 'PSD') }
    }

foreach ($variantDirectory in $variantDirectories) {
    $variant = $variantDirectory.Name
    $frameDirectory = Join-Path $frameRoot $variant
    if (Test-Path -LiteralPath $frameDirectory) {
        Get-ChildItem -LiteralPath $frameDirectory -Force | Remove-Item -Force -Recurse
    }
    New-Item -ItemType Directory -Force -Path $frameDirectory | Out-Null

    $tiles = [Collections.Generic.List[string]]::new()
    $tileId = 0
    $tileWidth = 128
    $tileHeight = 128

    foreach ($source in (Get-ChildItem -LiteralPath $variantDirectory.FullName -Filter '*.png' -File | Sort-Object Name)) {
        if ($source.Name.StartsWith('._')) { continue }

        $dimensions = Get-PngDimensions $source.FullName
        $state = Get-StateName $source.BaseName
        $isSheet = $dimensions.Height -eq $tileHeight -and $dimensions.Width -ge $tileWidth -and (($dimensions.Width % $tileWidth) -eq 0)

        if ($isSheet) {
            $frameCount = [int]($dimensions.Width / $tileWidth)
            $frameIds = @()
            for ($frame = 0; $frame -lt $frameCount; $frame++) {
                $frameName = '{0}_{1:00}.png' -f $state, $frame
                $framePath = Join-Path $frameDirectory $frameName
                $offset = $frame * $tileWidth
                & magick $source.FullName -crop "${tileWidth}x${tileHeight}+${offset}+0" +repage $framePath
                if ($LASTEXITCODE -ne 0) { throw "Falha ao recortar $($source.FullName)" }
                $frameIds += $tileId
                $tileId++
            }

            $duration = Get-FrameDuration $state
            $loop = if ($state -in @('Dead', 'Hurt')) { 'false' } else { 'true' }
            $tiles.Add("    <tile id=`"$($frameIds[0])`">`n      <properties>`n        <property name=`"Character`" value=`"$(XmlEscape $variant)`"/>`n        <property name=`"State`" value=`"$(XmlEscape $state)`"/>`n        <property name=`"RenderScale`" type=`"float`" value=`"0.75`"/>`n        <property name=`"Loop`" type=`"bool`" value=`"$loop`"/>`n      </properties>`n      <animation>")
            foreach ($frameId in $frameIds) {
                $tiles.Add("        <frame tileid=`"$frameId`" duration=`"$duration`"/>")
            }
            $tiles.Add("      </animation>`n      <image width=`"$tileWidth`" height=`"$tileHeight`" source=`"../../Enemies/GeneratedFrames/$variant/$($state)_00.png`"/>`n    </tile>")

            for ($frame = 1; $frame -lt $frameCount; $frame++) {
                $id = $frameIds[$frame]
                $frameName = '{0}_{1:00}.png' -f $state, $frame
                $tiles.Add("    <tile id=`"$id`">`n      <image width=`"$tileWidth`" height=`"$tileHeight`" source=`"../../Enemies/GeneratedFrames/$variant/$frameName`"/>`n    </tile>")
            }
        }
        else {
            $specialName = '{0}.png' -f $state
            $specialPath = Join-Path $frameDirectory $specialName
            Copy-Item -LiteralPath $source.FullName -Destination $specialPath -Force
            $tiles.Add("    <tile id=`"$tileId`">`n      <properties>`n        <property name=`"Character`" value=`"$(XmlEscape $variant)`"/>`n        <property name=`"State`" value=`"$(XmlEscape $state)`"/>`n        <property name=`"AssetKind`" value=`"Special`"/>`n      </properties>`n      <image width=`"$($dimensions.Width)`" height=`"$($dimensions.Height)`" source=`"../../Enemies/GeneratedFrames/$variant/$specialName`"/>`n    </tile>")
            $tileId++
        }
    }

    $xml = @(
        '<?xml version="1.0" encoding="UTF-8"?>'
        "<tileset version=`"1.10`" tiledversion=`"1.11.0`" name=`"$variant`" tilewidth=`"$tileWidth`" tileheight=`"$tileHeight`" tilecount=`"$tileId`" columns=`"0`" objectalignment=`"bottom`">"
        "  <grid orientation=`"orthogonal`" width=`"$tileWidth`" height=`"$tileHeight`"/>"
    )
    $xml += $tiles
    $xml += '</tileset>'
    $tilesetPath = Join-Path $tilesetRoot "$variant.tsx"
    [IO.File]::WriteAllText($tilesetPath, ($xml -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "${variant}: $tileId tiles geradas"
}
