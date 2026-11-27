$ErrorActionPreference = 'Stop'

$mapRoot = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled\Maps'
$environmentFirstGid = 1
$itemsFirstGid = 73

$dark = @{
    RoofLeft = 2; RoofMid = 3; RoofRight = 4; RoofBroken = 5; RoofRidge = 6
    WoodWall = 7; WoodWallBroken = 8; ShojiWall = 9; ShojiTorn = 10; DoorSliding = 11
    WoodPost = 12; WoodBeam = 13; ToriiKasagiLeft = 14; ToriiKasagiMid = 15; ToriiKasagiRight = 16
    ToriiNukiLeft = 17; ToriiNukiMid = 18; ToriiNukiRight = 19; ToriiPillarLeft = 20
    ToriiPillarRight = 21; ToriiPillarBroken = 22; ToriiKasagiStub = 23; StoneLanternTop = 24
    StoneLanternBody = 25; StoneLanternBase = 26; StoneStep = 27; SnowGroundTop = 28
    SnowGroundFill = 29; SmokeA = 30; SmokeB = 31; MountainFlat = 32; MountainRiseA = 33
    MountainRiseB = 34; DeadBranch = 35; BeamDebris = 36; AshRubble = 37; BrokenFence = 38
    TornBanner = 39; MossStone = 40; FogBand = 41; CraneSilhouette = 42; Shimenawa = 43
    ToriiBell = 44; BurntPole = 45; SkyA = 46; SkyB = 47; SkyC = 48; SkyDitherAB = 49
    SkyDitherBC = 50; WoodPlatformMid = 51; WoodPlatformLeft = 52; WoodPlatformRight = 53
    HealthPickup = 54; MissingSealAltar = 55; ChainAnchor = 56; ChainHook = 57
    CheckpointLantern = 58; EnemyShadow = 59; GuardianStatue = 60; GuardianGate = 61
    WarningSign = 62; DarkMarker = 63; MountainFill = 64; MountainFallA = 65; MountainFallB = 66
}

function New-Grid([int]$width, [int]$height, [int]$fill = 0) {
    $grid = [object[]]::new($height)
    for ($y = 0; $y -lt $height; $y++) {
        $row = [uint32[]]::new($width)
        if ($fill -ne 0) {
            for ($x = 0; $x -lt $width; $x++) { $row[$x] = [uint32]$fill }
        }
        $grid[$y] = $row
    }
    return ,$grid
}

function Set-Tile([object]$grid, [int]$x, [int]$y, [int]$value) {
    if ($x -ge 0 -and $x -lt 320 -and $y -ge 0 -and $y -lt 24) { $grid[$y][$x] = [uint32]$value }
}

function Add-Rect([object]$grid, [int]$x, [int]$y, [int]$width, [int]$height, [int]$value) {
    for ($yy = $y; $yy -lt $y + $height; $yy++) {
        for ($xx = $x; $xx -lt $x + $width; $xx++) { Set-Tile $grid $xx $yy $value }
    }
}

function Encode-Grid([object]$grid) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    foreach ($row in $grid) { foreach ($value in $row) { $writer.Write([uint32]$value) } }
    $writer.Flush()
    $encoded = [Convert]::ToBase64String($stream.ToArray())
    $writer.Dispose()
    $stream.Dispose()
    return $encoded
}

function Decode-Grid([System.Xml.XmlElement]$layer) {
    $data = $layer.SelectSingleNode('data')
    $bytes = [Convert]::FromBase64String(($data.InnerText -replace '\s', ''))
    $grid = [object[]]::new(24)
    for ($y = 0; $y -lt 24; $y++) { $grid[$y] = [uint32[]]::new(320) }
    for ($index = 0; $index -lt 7680; $index++) {
        $rowIndex = [int][Math]::Floor($index / 320)
        $columnIndex = $index % 320
        $row = $grid[$rowIndex]
        if ($null -eq $row) { throw "Decoded grid row is null: index=$index row=$rowIndex gridCount=$($grid.Count)" }
        $row[$columnIndex] = [BitConverter]::ToUInt32($bytes, $index * 4)
    }
    return ,$grid
}

function Set-LayerGrid([System.Xml.XmlElement]$map, [string]$name, [object]$grid) {
    $layer = $map.SelectSingleNode("layer[@name='$name']")
    if ($null -eq $layer) { throw "Layer '$name' not found." }
    $layer.SelectSingleNode('data').InnerText = Encode-Grid $grid
}

function Get-LayerGrid([System.Xml.XmlElement]$map, [string]$name) {
    return ,(Decode-Grid ($map.SelectSingleNode("layer[@name='$name']")))
}

function Add-PowerTiles([System.Xml.XmlElement]$map, [string[]]$coordinates, [string]$ironTalismanCoordinate = '') {
    $collectibles = Get-LayerGrid $map 'Collectibles'
    foreach ($coordinate in $coordinates) {
        $parts = $coordinate -split ','
        Set-Tile $collectibles ([int]$parts[0]) ([int]$parts[1]) ($itemsFirstGid + 2)
    }
    Set-LayerGrid $map 'Collectibles' $collectibles
    Set-MapProperty $map 'PowerFruitTile' ''
    Set-MapProperty $map 'PowerFruitTiles' ($coordinates -join ';')
    Set-MapProperty $map 'IronTalismanTile' $ironTalismanCoordinate
}

function Set-MapProperty([System.Xml.XmlElement]$map, [string]$name, [string]$value) {
    $property = $map.SelectSingleNode("properties/property[@name='$name']")
    if ($null -eq $property) {
        $properties = $map.SelectSingleNode('properties')
        if ($null -eq $properties) {
            $properties = $map.OwnerDocument.CreateElement('properties')
            [void]$map.PrependChild($properties)
        }
        $property = $map.OwnerDocument.CreateElement('property')
        $property.SetAttribute('name', $name)
        [void]$properties.AppendChild($property)
    }
    $property.SetAttribute('value', $value)
}

function Set-ObjectProperty([System.Xml.XmlElement]$object, [string]$name, [string]$value) {
    $property = $object.SelectSingleNode("properties/property[@name='$name']")
    if ($null -eq $property) {
        $properties = $object.SelectSingleNode('properties')
        if ($null -eq $properties) {
            $properties = $object.OwnerDocument.CreateElement('properties')
            [void]$object.AppendChild($properties)
        }
        $property = $object.OwnerDocument.CreateElement('property')
        $property.SetAttribute('name', $name)
        [void]$properties.AppendChild($property)
    }
    $property.SetAttribute('value', $value)
}

function Get-Gameplay([System.Xml.XmlElement]$map) {
    $gameplay = $map.SelectSingleNode("objectgroup[@name='Gameplay']")
    if ($null -eq $gameplay) { throw 'Gameplay object layer not found.' }
    return $gameplay
}

function Remove-GameplayObjects([System.Xml.XmlElement]$gameplay, [string[]]$types) {
    foreach ($object in @($gameplay.SelectNodes('object'))) {
        if ($types -contains [string]$object.type) { [void]$gameplay.RemoveChild($object) }
    }
}

function Add-Property([System.Xml.XmlElement]$object, [string]$name, [string]$value) {
    Set-ObjectProperty $object $name $value
}

function Add-Enemy([System.Xml.XmlDocument]$document, [System.Xml.XmlElement]$gameplay, [int]$id, [int]$x, [string]$type) {
    $object = $document.CreateElement('object')
    $object.SetAttribute('id', [string]$id)
    $object.SetAttribute('name', "EnemySpawn_$id")
    $object.SetAttribute('type', 'EnemySpawn')
    $object.SetAttribute('x', [string]$x)
    $object.SetAttribute('y', '320')
    $object.SetAttribute('width', '16')
    $object.SetAttribute('height', '32')
    Add-Property $object 'EnemyType' $type
    [void]$gameplay.AppendChild($object)
}

function Add-LevelExit([System.Xml.XmlDocument]$document, [System.Xml.XmlElement]$gameplay, [int]$id, [string]$name, [string]$nextLevel) {
    $object = $document.CreateElement('object')
    $object.SetAttribute('id', [string]$id)
    $object.SetAttribute('name', $name)
    $object.SetAttribute('type', 'LevelExit')
    $object.SetAttribute('x', '4992')
    $object.SetAttribute('y', '304')
    $object.SetAttribute('width', '32')
    $object.SetAttribute('height', '48')
    Add-Property $object 'NextLevelId' $nextLevel
    Add-Property $object 'Trigger' 'OnEnter'
    [void]$gameplay.AppendChild($object)
}

function Add-Boss([System.Xml.XmlDocument]$document, [System.Xml.XmlElement]$gameplay, [string]$variant) {
    $object = $document.CreateElement('object')
    $object.SetAttribute('id', '120')
    $object.SetAttribute('name', "FinalBoss_$variant")
    $object.SetAttribute('type', 'BossSpawn')
    $object.SetAttribute('x', '4800')
    $object.SetAttribute('y', '256')
    $object.SetAttribute('width', '256')
    $object.SetAttribute('height', '96')
    Add-Property $object 'BossType' 'Minotaur'
    Add-Property $object 'BossVariant' $variant
    Add-Property $object 'Trigger' 'OnEnter'
    [void]$gameplay.AppendChild($object)
}

function Save-Map([System.Xml.XmlDocument]$document, [string]$path) {
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $writer = [System.Xml.XmlWriter]::Create($path, $settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
}

function New-DarkLayers([bool]$finalAct) {
    $background = New-Grid 320 24 $dark.SkyA
    $backdrop = New-Grid 320 24
    $terrain = New-Grid 320 24
    $props = New-Grid 320 24

    for ($y = 0; $y -lt 12; $y++) {
        $sky = if ($y -lt 4) { $dark.SkyA } elseif ($y -lt 8) { $dark.SkyB } else { $dark.SkyC }
        Add-Rect $background 0 $y 320 1 $sky
    }
    foreach ($x in @(8, 28, 52, 78, 108, 142, 178, 214, 250, 286, 312)) {
        Set-Tile $background $x 11 $dark.MountainFlat
        Set-Tile $background ($x + 1) 10 $dark.MountainRiseA
        Set-Tile $background ($x + 2) 11 $dark.MountainRiseB
    }
    Add-Rect $background 0 12 320 3 $dark.MountainFill
    foreach ($x in @(22, 66, 118, 168, 226, 274, 306)) { Set-Tile $background $x 7 $dark.SmokeA }
    foreach ($x in @(38, 94, 146, 202, 258, 296)) { Set-Tile $background $x 9 $dark.FogBand }

    $buildings = if ($finalAct) {
        @(@{X=8;W=17}, @{X=48;W=14}, @{X=94;W=19}, @{X=150;W=13}, @{X=218;W=17}, @{X=268;W=14})
    } else {
        @(@{X=6;W=18}, @{X=36;W=16}, @{X=72;W=21}, @{X=118;W=17}, @{X=162;W=20}, @{X=208;W=16}, @{X=246;W=22}, @{X=284;W=16})
    }
    foreach ($building in $buildings) {
        $x0 = [int]$building.X; $w = [int]$building.W
        Add-Rect $backdrop $x0 17 $w 4 $dark.WoodWall
        Set-Tile $backdrop ($x0 + 1) 17 $dark.WoodWallBroken
        Set-Tile $backdrop ($x0 + $w - 2) 18 $dark.ShojiTorn
        for ($x = $x0; $x -lt $x0 + $w; $x++) { Set-Tile $backdrop $x 15 $dark.RoofMid }
        Set-Tile $backdrop $x0 15 $dark.RoofLeft
        Set-Tile $backdrop ($x0 + $w - 1) 15 $dark.RoofRight
        Set-Tile $backdrop ($x0 + [int]($w / 2)) 19 $dark.DoorSliding
        Set-Tile $backdrop $x0 16 $dark.WoodPost
        Set-Tile $backdrop ($x0 + $w - 1) 16 $dark.WoodPost
    }
    foreach ($x in @(16, 58, 106, 176, 232, 302)) { Set-Tile $backdrop $x 18 $dark.DeadBranch }
    foreach ($x in @(30, 84, 136, 194, 252, 316)) { Set-Tile $backdrop $x 20 $dark.BeamDebris }

    Add-Rect $terrain 0 22 320 1 $dark.SnowGroundTop
    Add-Rect $terrain 0 23 320 1 $dark.SnowGroundFill
    $platforms = if ($finalAct) {
        @(@{X=24;Y=19;W=13}, @{X=54;Y=16;W=12}, @{X=88;Y=20;W=16}, @{X=128;Y=15;W=11}, @{X=166;Y=18;W=14}, @{X=208;Y=14;W=12}, @{X=246;Y=18;W=15}, @{X=286;Y=15;W=14})
    } else {
        @(@{X=26;Y=19;W=15}, @{X=60;Y=17;W=12}, @{X=96;Y=20;W=18}, @{X=132;Y=16;W=11}, @{X=164;Y=19;W=15}, @{X=198;Y=15;W=12}, @{X=230;Y=18;W=17}, @{X=266;Y=16;W=13}, @{X=298;Y=19;W=12})
    }
    foreach ($platform in $platforms) {
        for ($x = $platform.X; $x -lt $platform.X + $platform.W; $x++) { Set-Tile $terrain $x $platform.Y $dark.WoodPlatformMid }
        Set-Tile $terrain $platform.X $platform.Y $dark.WoodPlatformLeft
        Set-Tile $terrain ($platform.X + $platform.W - 1) $platform.Y $dark.WoodPlatformRight
    }

    foreach ($x in @(12, 42, 74, 112, 154, 188, 224, 262, 296, 314)) { Set-Tile $props $x 21 $dark.AshRubble }
    foreach ($x in @(20, 62, 104, 144, 184, 226, 270, 306)) { Set-Tile $props $x 20 $dark.BrokenFence }
    foreach ($x in @(34, 82, 126, 174, 218, 258, 300)) { Set-Tile $props $x 19 $dark.DarkMarker }
    foreach ($x in @(38, 86, 130, 178, 222, 262, 304)) { Set-Tile $props $x 20 $dark.DarkMarker }
    foreach ($x in @(46, 94, 138, 186, 230, 270, 312)) { Set-Tile $props $x 21 $dark.DarkMarker }
    foreach ($x in @(28, 100, 172, 244, 292)) { Set-Tile $props $x 18 $dark.BurntPole }
    foreach ($x in @(52, 148, 236, 280)) { Set-Tile $props $x 17 $dark.TornBanner }
    foreach ($x in @(72, 160, 248)) { Set-Tile $props $x 21 $dark.Shimenawa }

    # A broken torii gate marks the transition at the far end of the act.
    Set-Tile $props 308 14 $dark.ToriiKasagiLeft
    Set-Tile $props 309 14 $dark.ToriiKasagiMid
    Set-Tile $props 310 14 $dark.ToriiKasagiRight
    Set-Tile $props 308 15 $dark.ToriiNukiLeft
    Set-Tile $props 309 15 $dark.ToriiNukiMid
    Set-Tile $props 310 15 $dark.ToriiNukiRight
    Set-Tile $props 308 16 $dark.ToriiPillarBroken
    Set-Tile $props 310 16 $dark.ToriiPillarRight
    Set-Tile $props 308 17 $dark.ToriiPillarBroken
    Set-Tile $props 310 17 $dark.ToriiPillarRight
    Set-Tile $props 309 13 $dark.ToriiBell
    if ($finalAct) {
        Set-Tile $props 304 20 $dark.GuardianStatue
        Set-Tile $props 310 20 $dark.MissingSealAltar
        Set-Tile $props 306 18 $dark.SmokeB
        Set-Tile $props 316 18 $dark.SmokeB
    }

    return ,@($background, $backdrop, $terrain, $props)
}

function Set-EnemyRoster([System.Xml.XmlElement]$gameplay, [string[]]$roster) {
    $spawns = @($gameplay.SelectNodes("object[@type='EnemySpawn']"))
    for ($index = 0; $index -lt $spawns.Count; $index++) { Set-ObjectProperty $spawns[$index] 'EnemyType' $roster[$index % $roster.Count] }
}

$aPath = Join-Path $mapRoot 'Act_01_A.tmx'
$bPath = Join-Path $mapRoot 'Act_01_B.tmx'
$cPath = Join-Path $mapRoot 'Act_01_C.tmx'

[xml]$a = Get-Content -Raw $aPath
$aMap = $a.map
Set-MapProperty $aMap 'ActEnding' 'The trail passes through the ruined gate into Act_01_B'
$aProps = Get-LayerGrid $aMap 'Act01Props'
Set-Tile $aProps 309 14 ($environmentFirstGid + 25)
Set-Tile $aProps 310 14 ($environmentFirstGid + 25)
Set-LayerGrid $aMap 'Act01Props' $aProps
$aGameplay = Get-Gameplay $aMap
Set-EnemyRoster $aGameplay @('Black_Werewolf','Karasu_tengu','Skeleton_Warrior','Gorgon_1','Black_Werewolf','Karasu_tengu','Skeleton_Warrior','Gorgon_1')
Set-MapProperty $aMap 'NextLevelId' 'Act_01_B'
Add-PowerTiles $aMap @('32,20','80,20','128,20','176,20','224,20','272,20','304,20')
Save-Map $a $aPath

[xml]$b = Get-Content -Raw $bPath
$bMap = $b.map
Set-MapProperty $bMap 'Act' 'Act_01_B'
Set-MapProperty $bMap 'Title' 'A Vila Devastada e as Montanhas de Cinzas'
Set-MapProperty $bMap 'VisualStyle' '16-bit pixel art, devastated feudal ninja village, mountains, runes and spirit fog'
Set-MapProperty $bMap 'ActEnding' 'The broken torii opens the way to Act_01_C'
Set-MapProperty $bMap 'NextLevelId' 'Act_01_C'
$bMap.tileset[0].SetAttribute('source', '../Tilesets/Act_01_Feudal_Dark.tsx')
$bLayers = New-DarkLayers $false
Set-LayerGrid $bMap 'Background' $bLayers[0]
Set-LayerGrid $bMap 'VillageBackdrop' $bLayers[1]
Set-LayerGrid $bMap 'Terrain' $bLayers[2]
Set-LayerGrid $bMap 'Act01Props' $bLayers[3]
$bGameplay = Get-Gameplay $bMap
Remove-GameplayObjects $bGameplay @('EnemySpawn','BossSpawn','LevelExit')
$bRoster = @('Black_Werewolf','Karasu_tengu','Skeleton_Warrior','Gorgon_1','Karasu_tengu','Black_Werewolf','Gorgon_1','Skeleton_Warrior','Black_Werewolf','Karasu_tengu','Skeleton_Warrior','Gorgon_1','Black_Werewolf','Karasu_tengu','Gorgon_1','Skeleton_Warrior')
$bPositions = @(720, 960, 1200, 1440, 1760, 2080, 2400, 2720, 3040, 3360, 3680, 4000, 4240, 4480, 4640, 4800)
for ($index = 0; $index -lt $bRoster.Count; $index++) { Add-Enemy $b $bGameplay (50 + $index) $bPositions[$index] $bRoster[$index] }
Add-LevelExit $b $bGameplay 100 'Act01CExit' 'Act_01_C'
Add-PowerTiles $bMap @('32,20','80,20','128,20','176,20','224,20','272,20','304,20')
$bMap.SetAttribute('nextobjectid', '121')
Save-Map $b $bPath

[xml]$c = Get-Content -Raw $bPath
$cMap = $c.map
Set-MapProperty $cMap 'Act' 'Act_01_C'
Set-MapProperty $cMap 'Title' 'O Santuário em Ruínas'
Set-MapProperty $cMap 'VisualStyle' '16-bit pixel art, blackened mountain shrine, magical runes, smoke and devastation'
Set-MapProperty $cMap 'ActEnding' 'The Minotaur falls and the iron talisman is reclaimed'
Set-MapProperty $cMap 'NextLevelId' ''
$cGameplay = Get-Gameplay $cMap
Remove-GameplayObjects $cGameplay @('EnemySpawn','BossSpawn','LevelExit')
$cRoster = @('Black_Werewolf','Karasu_tengu','Gorgon_1','Skeleton_Warrior','Karasu_tengu','Black_Werewolf','Skeleton_Warrior','Gorgon_1','Black_Werewolf','Karasu_tengu','Gorgon_1','Skeleton_Warrior','Black_Werewolf','Gorgon_1','Karasu_tengu','Skeleton_Warrior')
$cPositions = @(640, 880, 1120, 1360, 1680, 2000, 2320, 2640, 2960, 3280, 3600, 3920, 4160, 4400, 4560, 4720)
for ($index = 0; $index -lt $cRoster.Count; $index++) { Add-Enemy $c $cGameplay (130 + $index) $cPositions[$index] $cRoster[$index] }
Add-Boss $c $cGameplay 'Minotaur_1'
$cMap.SetAttribute('nextobjectid', '160')
$cLayers = New-DarkLayers $true
Set-LayerGrid $cMap 'Background' $cLayers[0]
Set-LayerGrid $cMap 'VillageBackdrop' $cLayers[1]
Set-LayerGrid $cMap 'Terrain' $cLayers[2]
Set-LayerGrid $cMap 'Act01Props' $cLayers[3]
$cCollectibles = Get-LayerGrid $cMap 'Collectibles'
for ($y = 0; $y -lt 24; $y++) { for ($x = 0; $x -lt 320; $x++) { if ($cCollectibles[$y][$x] -eq ($itemsFirstGid + 2)) { $cCollectibles[$y][$x] = 0 } } }
Set-Tile $cCollectibles 300 20 ($itemsFirstGid + 2)
Set-LayerGrid $cMap 'Collectibles' $cCollectibles
Add-PowerTiles $cMap @('32,20','80,20','128,20','176,20','224,20','272,20') '300,20'
Save-Map $c $cPath

Write-Host "Act_01 A/B/C generated in $mapRoot"
