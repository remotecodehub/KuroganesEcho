$ErrorActionPreference = 'Stop'

$tiledRoot = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled'
$sourceRoot = Join-Path $tiledRoot 'Chars\craftpix-net-407836-free-ninja-sprite-sheets-pixel-art'
$frameRoot = Join-Path $tiledRoot 'Chars\GeneratedFrames'
$tilesetRoot = Join-Path $tiledRoot 'Tilesets'

$characters = @(
    @{ Name = 'Kunoichi'; FrameSize = 128 },
    @{ Name = 'Ninja_Monk'; FrameSize = 96 },
    @{ Name = 'Ninja_Peasant'; FrameSize = 96 }
)

function Get-PngDimensions([string] $path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) {
        throw "PNG inválido: $path"
    }
    $width = ([int]$bytes[16] * 16777216) + ([int]$bytes[17] * 65536) + ([int]$bytes[18] * 256) + [int]$bytes[19]
    $height = ([int]$bytes[20] * 16777216) + ([int]$bytes[21] * 65536) + ([int]$bytes[22] * 256) + [int]$bytes[23]
    return @{ Width = $width; Height = $height }
}

function XmlEscape([string] $value) {
    return [System.Security.SecurityElement]::Escape($value)
}

function Get-FrameDuration([string] $state) {
    switch -Regex ($state) {
        '^Idle$' { return 120 }
        '^Walk$' { return 100 }
        '^Run$' { return 80 }
        '^Jump$' { return 110 }
        '^Hurt$' { return 100 }
        '^Dead$' { return 140 }
        '^Eating$' { return 130 }
        '^Cast$' { return 110 }
        '^Attack' { return 90 }
        '^Blade$' { return 90 }
        '^Disguise$' { return 120 }
        '^Shot$' { return 100 }
        default { return 100 }
    }
}

foreach ($character in $characters) {
    $name = $character.Name
    $frameSize = [int]$character.FrameSize
    $sourceDir = Join-Path $sourceRoot $name
    $outputDir = Join-Path $frameRoot $name
    if (Test-Path $outputDir) {
        Get-ChildItem -LiteralPath $outputDir -Force | Remove-Item -Force -Recurse
    }
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    $tiles = [Collections.Generic.List[string]]::new()
    $tileId = 0

    foreach ($source in (Get-ChildItem -LiteralPath $sourceDir -Filter '*.png' -File | Sort-Object Name)) {
        $dimensions = Get-PngDimensions $source.FullName
        $state = $source.BaseName
        $isSheet = $dimensions.Height -eq $frameSize -and $dimensions.Width -ge $frameSize -and (($dimensions.Width % $frameSize) -eq 0)

        if ($isSheet) {
            $frameCount = [int]($dimensions.Width / $frameSize)
            $frameIds = @()
            for ($frame = 0; $frame -lt $frameCount; $frame++) {
                $frameName = '{0}_{1:00}.png' -f $state, $frame
                $framePath = Join-Path $outputDir $frameName
                $offset = $frame * $frameSize
                & magick $source.FullName -crop "${frameSize}x${frameSize}+${offset}+0" +repage $framePath
                if ($LASTEXITCODE -ne 0) { throw "Falha ao recortar $($source.FullName)" }
                $frameIds += $tileId
                $tileId++
            }

            $duration = Get-FrameDuration $state
            $loop = if ($state -in @('Dead', 'Hurt')) { 'false' } else { 'true' }
            $tiles.Add("    <tile id=`"$($frameIds[0])`">`n      <properties>`n        <property name=`"Character`" value=`"$(XmlEscape $name)`"/>`n        <property name=`"State`" value=`"$(XmlEscape $state)`"/>`n        <property name=`"RenderScale`" type=`"float`" value=`"0.5`"/>`n        <property name=`"Loop`" type=`"bool`" value=`"$loop`"/>`n      </properties>`n      <animation>")
            foreach ($frameId in $frameIds) {
                $tiles.Add("        <frame tileid=`"$frameId`" duration=`"$duration`"/>")
            }
            $tiles.Add("      </animation>`n      <image width=`"$frameSize`" height=`"$frameSize`" source=`"../Chars/GeneratedFrames/$name/$($state)_00.png`"/>`n    </tile>")

            for ($frame = 1; $frame -lt $frameCount; $frame++) {
                $id = $frameIds[$frame]
                $frameName = '{0}_{1:00}.png' -f $state, $frame
                $tiles.Add("    <tile id=`"$id`">`n      <image width=`"$frameSize`" height=`"$frameSize`" source=`"../Chars/GeneratedFrames/$name/$frameName`"/>`n    </tile>")
            }
        }
        else {
            $specialName = '{0}.png' -f $state
            $specialPath = Join-Path $outputDir $specialName
            Copy-Item -LiteralPath $source.FullName -Destination $specialPath -Force
            $kind = if ($dimensions.Width -lt $frameSize -or $dimensions.Height -lt $frameSize) { 'ProjectileOrEffect' } else { 'Special' }
            $tiles.Add("    <tile id=`"$tileId`">`n      <properties>`n        <property name=`"Character`" value=`"$(XmlEscape $name)`"/>`n        <property name=`"State`" value=`"$(XmlEscape $state)`"/>`n        <property name=`"AssetKind`" value=`"$kind`"/>`n      </properties>`n      <image width=`"$($dimensions.Width)`" height=`"$($dimensions.Height)`" source=`"../Chars/GeneratedFrames/$name/$specialName`"/>`n    </tile>")
            $tileId++
        }
    }

    $xml = @('<?xml version="1.0" encoding="UTF-8"?>', "<tileset version=`"1.10`" tiledversion=`"1.11.0`" name=`"$name`" tilewidth=`"$frameSize`" tileheight=`"$frameSize`" tilecount=`"$tileId`" columns=`"0`" objectalignment=`"bottom`">", "  <grid orientation=`"orthogonal`" width=`"$frameSize`" height=`"$frameSize`"/>")
    $xml += $tiles
    $xml += '</tileset>'
    $tilesetPath = Join-Path $tilesetRoot "$name.tsx"
    [IO.File]::WriteAllText($tilesetPath, ($xml -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "${name}: $tileId tiles geradas"
}
