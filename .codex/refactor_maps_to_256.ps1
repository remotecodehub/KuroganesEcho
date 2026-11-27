param(
    [int]$TargetWidth = 256,
    [int]$ExpectedHeight = 24,
    [string]$MapRoot = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mapRoot = if ([string]::IsNullOrWhiteSpace($MapRoot)) {
    Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Maps'
} else {
    (Resolve-Path -LiteralPath $MapRoot).Path
}
$mapNames = @(
    'Act_01_A', 'Act_01_B', 'Act_01_C',
    'Act_02_A', 'Act_02_B', 'Act_02_C',
    'Act_03_A', 'Act_03_B', 'Act_03_C'
)
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Scale-Column([int]$column, [int]$sourceWidth, [int]$destinationWidth) {
    return [int][Math]::Floor(($column * $destinationWidth) / [double]$sourceWidth)
}

function Source-ColumnForTarget([int]$column, [int]$sourceWidth, [int]$destinationWidth) {
    return [int][Math]::Floor(($column * $sourceWidth) / [double]$destinationWidth)
}

function Read-TileValues([System.Xml.XmlElement]$data, [int]$count) {
    $encoding = [string]$data.GetAttribute('encoding')
    if ($encoding -ieq 'csv') {
        return @(
            (($data.InnerText -split '[,\s]+' | Where-Object { $_ -ne '' } | ForEach-Object {
                [uint32]::Parse($_, [Globalization.CultureInfo]::InvariantCulture)
            }) | Select-Object -First $count)
        )
    }

    $encoded = $data.InnerText -replace '\s', ''
    $bytes = [Convert]::FromBase64String($encoded)
    $values = [uint32[]]::new([Math]::Min($count, [int]($bytes.Length / 4)))
    for ($i = 0; $i -lt $values.Length; $i++) {
        $offset = $i * 4
        $values[$i] = [uint32]($bytes[$offset] -bor
            ($bytes[$offset + 1] -shl 8) -bor
            ($bytes[$offset + 2] -shl 16) -bor
            ($bytes[$offset + 3] -shl 24))
    }
    return ,$values
}

function Write-TileValues([uint32[]]$values) {
    $bytes = [byte[]]::new($values.Length * 4)
    for ($i = 0; $i -lt $values.Length; $i++) {
        [Array]::Copy([BitConverter]::GetBytes($values[$i]), 0, $bytes, $i * 4, 4)
    }
    return [Convert]::ToBase64String($bytes)
}

function Build-CollectibleValues([uint32[]]$sourceValues, [int]$sourceWidth, [int]$height, [int]$destinationWidth) {
    $targetValues = [uint32[]]::new($destinationWidth * $height)
    for ($y = 0; $y -lt $height; $y++) {
        for ($sourceX = 0; $sourceX -lt $sourceWidth; $sourceX++) {
            $gid = $sourceValues[($y * $sourceWidth) + $sourceX]
            if ($gid -eq 0) { continue }
            $destinationX = Scale-Column $sourceX $sourceWidth $destinationWidth
            $destinationIndex = ($y * $destinationWidth) + $destinationX
            if ($targetValues[$destinationIndex] -ne 0) {
                $found = $false
                for ($distance = 1; $distance -lt $destinationWidth -and -not $found; $distance++) {
                    foreach ($candidateX in @($destinationX - $distance, $destinationX + $distance)) {
                        if ($candidateX -lt 0 -or $candidateX -ge $destinationWidth) { continue }
                        $candidateIndex = ($y * $destinationWidth) + $candidateX
                        if ($targetValues[$candidateIndex] -eq 0) {
                            $destinationIndex = $candidateIndex
                            $found = $true
                            break
                        }
                    }
                }
                if (-not $found) { throw "Could not preserve collectible tile at source column $sourceX, row $y." }
            }
            $targetValues[$destinationIndex] = $gid
        }
    }
    return ,$targetValues
}

function Transform-CoordinateList([string]$value, [int]$sourceWidth, [int]$destinationWidth) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $value }
    $items = foreach ($coordinate in ($value -split ';')) {
        $parts = $coordinate -split ',', 2
        if ($parts.Count -ne 2) { $coordinate; continue }
        $x = 0; $y = 0
        if ([int]::TryParse($parts[0].Trim(), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$x) -and
            [int]::TryParse($parts[1].Trim(), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$y)) {
            '{0},{1}' -f (Scale-Column $x $sourceWidth $destinationWidth), $y
        } else {
            $coordinate
        }
    }
    return ($items -join ';')
}

function Scale-NumericXProperty([System.Xml.XmlElement]$property, [int]$sourceWidth, [int]$destinationWidth) {
    $name = [string]$property.GetAttribute('name')
    if ($name -notmatch '(?i)(^|_)(x|positionx|respawnx|spawnx)$') { return }
    $value = [string]$property.GetAttribute('value')
    $number = 0
    if ([int]::TryParse($value, [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        $property.SetAttribute('value', (Scale-Column $number $sourceWidth $destinationWidth).ToString([Globalization.CultureInfo]::InvariantCulture))
    }
}

function Scale-Object([System.Xml.XmlElement]$object, [int]$sourceWidth, [int]$destinationWidth) {
    if ($object.HasAttribute('x')) {
        $x = 0.0
        if ([double]::TryParse($object.GetAttribute('x'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$x)) {
            $scaledX = [Math]::Round($x * $destinationWidth / $sourceWidth, 0, [MidpointRounding]::AwayFromZero)
            $object.SetAttribute('x', ([int]$scaledX).ToString([Globalization.CultureInfo]::InvariantCulture))
        }
    }
    if ($object.HasAttribute('width')) {
        $width = 0.0
        if ([double]::TryParse($object.GetAttribute('width'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$width)) {
            $scaledWidth = [Math]::Max(1, [Math]::Round($width * $destinationWidth / $sourceWidth, 0, [MidpointRounding]::AwayFromZero))
            $scaledX = 0
            [void][int]::TryParse($object.GetAttribute('x'), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$scaledX)
            $maxWidth = ($destinationWidth * 16) - $scaledX
            $scaledWidth = [Math]::Min($scaledWidth, [Math]::Max(1, $maxWidth))
            $object.SetAttribute('width', ([int]$scaledWidth).ToString([Globalization.CultureInfo]::InvariantCulture))
        }
    }

    foreach ($property in @($object.SelectNodes('.//property'))) {
        Scale-NumericXProperty $property $sourceWidth $destinationWidth
    }
}

foreach ($mapName in $mapNames) {
    $path = Join-Path $mapRoot "$mapName.tmx"
    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.Load($path)
    $rootElement = $document.DocumentElement
    $sourceWidth = [int]$rootElement.GetAttribute('width')
    $height = [int]$rootElement.GetAttribute('height')
    if ($sourceWidth -ne 320 -or $height -ne $ExpectedHeight) {
        throw "$mapName has unexpected dimensions ${sourceWidth}x${height}; expected 320x$ExpectedHeight."
    }

    $rootElement.SetAttribute('width', $TargetWidth.ToString([Globalization.CultureInfo]::InvariantCulture))

    foreach ($property in @($rootElement.SelectNodes('./properties/property'))) {
        $propertyName = [string]$property.GetAttribute('name')
        if ($propertyName -match '(?i)(Tile|Tiles)$' -and [string]$property.GetAttribute('value') -match ',') {
            $property.SetAttribute('value', (Transform-CoordinateList $property.GetAttribute('value') $sourceWidth $TargetWidth))
        }
    }

    foreach ($layer in @($rootElement.SelectNodes('./layer'))) {
        $layerWidth = [int]$layer.GetAttribute('width')
        $layerHeight = [int]$layer.GetAttribute('height')
        if ($layerWidth -ne $sourceWidth -or $layerHeight -ne $height) {
            throw "$mapName layer '$($layer.GetAttribute('name'))' has unexpected dimensions ${layerWidth}x${layerHeight}."
        }
        $data = [System.Xml.XmlElement]$layer.SelectSingleNode('./data')
        $sourceValues = Read-TileValues $data ($sourceWidth * $height)
        if ($sourceValues.Length -ne ($sourceWidth * $height)) {
            throw "$mapName layer '$($layer.GetAttribute('name'))' has incomplete tile data."
        }
        if ($layer.GetAttribute('name') -ieq 'Collectibles') {
            $targetValues = Build-CollectibleValues $sourceValues $sourceWidth $height $TargetWidth
        } else {
            $targetValues = [uint32[]]::new($TargetWidth * $height)
            for ($y = 0; $y -lt $height; $y++) {
                for ($x = 0; $x -lt $TargetWidth; $x++) {
                    $sourceX = Source-ColumnForTarget $x $sourceWidth $TargetWidth
                    $targetValues[($y * $TargetWidth) + $x] = $sourceValues[($y * $sourceWidth) + $sourceX]
                }
            }
        }
        $layer.SetAttribute('width', $TargetWidth.ToString([Globalization.CultureInfo]::InvariantCulture))
        $data.SetAttribute('encoding', 'base64')
        $data.InnerText = Write-TileValues $targetValues
    }

    foreach ($object in @($rootElement.SelectNodes('./objectgroup/object'))) {
        Scale-Object $object $sourceWidth $TargetWidth
    }

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = $utf8NoBom
    $settings.Indent = $false
    $settings.NewLineChars = "`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::None
    $writer = [System.Xml.XmlWriter]::Create($path, $settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
    Write-Host "Refactored $mapName from ${sourceWidth}x${height} to ${TargetWidth}x${height}."
}
