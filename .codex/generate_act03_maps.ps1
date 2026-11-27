$ErrorActionPreference = 'Stop'

$tiledRoot = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled'
$mapRoot = Join-Path $tiledRoot 'Maps'
$tilesetRoot = Join-Path $tiledRoot 'Tilesets'
$script:width = 320
$script:height = 24
$script:environmentColumns = 20
$script:sourceRows = 15
$script:environmentRows = 16
$script:environmentTileCount = $script:environmentColumns * $script:environmentRows
$script:backdropFirstGid = 1 + ($script:environmentColumns * $script:sourceRows)
$encoding = [System.Text.UTF8Encoding]::new($false)

function Tile([int] $column, [int] $row) {
    return 1 + $column + ($row * $script:environmentColumns)
}

function New-Grid([int] $fill = 0) {
    $grid = [int[]]::new($script:width * $script:height)
    if ($fill -ne 0) {
        for ($i = 0; $i -lt $grid.Length; $i++) { $grid[$i] = $fill }
    }
    return ,$grid
}

function Set-Cell([int[]] $grid, [int] $x, [int] $y, [int] $gid) {
    if ($x -ge 0 -and $x -lt $script:width -and $y -ge 0 -and $y -lt $script:height) {
        $grid.SetValue($gid, (($y * $script:width) + $x))
    }
}

function Fill-Rect([int[]] $grid, [int] $x1, [int] $y1, [int] $x2, [int] $y2, [int] $gid) {
    for ($y = [Math]::Max(0, $y1); $y -le [Math]::Min($script:height - 1, $y2); $y++) {
        for ($x = [Math]::Max(0, $x1); $x -le [Math]::Min($script:width - 1, $x2); $x++) {
            Set-Cell $grid $x $y $gid
        }
    }
}

function Grid-Csv([int[]] $grid) {
    $rows = [Collections.Generic.List[string]]::new()
    for ($y = 0; $y -lt $script:height; $y++) {
        $start = $y * $script:width
        $rows.Add((($grid[$start..($start + $script:width - 1)]) -join ','))
    }
    return $rows -join "`n"
}

function Grid-Base64([int[]] $grid) {
    $bytes = [byte[]]::new($grid.Length * 4)
    for ($i = 0; $i -lt $grid.Length; $i++) {
        $valueBytes = [BitConverter]::GetBytes([uint32]$grid[$i])
        [Array]::Copy($valueBytes, 0, $bytes, $i * 4, 4)
    }
    return [Convert]::ToBase64String($bytes)
}

function Xml-Escape([string] $value) {
    return [System.Security.SecurityElement]::Escape($value)
}

function Property-Xml([string] $name, [string] $value, [string] $type = $null) {
    $escapedName = Xml-Escape $name
    $escapedValue = Xml-Escape $value
    if ([string]::IsNullOrWhiteSpace($type)) {
        return "    <property name=`"$escapedName`" value=`"$escapedValue`"/>"
    }
    return "    <property name=`"$escapedName`" type=`"$type`" value=`"$escapedValue`"/>"
}

function Object-Xml(
    [int] $id,
    [string] $name,
    [string] $type,
    [int] $x,
    [int] $y,
    [int] $w = 0,
    [int] $h = 0,
    [hashtable] $properties = @{}
) {
    $attrs = "id=`"$id`" name=`"$(Xml-Escape $name)`" type=`"$(Xml-Escape $type)`" x=`"$x`" y=`"$y`""
    if ($w -gt 0) { $attrs += " width=`"$w`"" }
    if ($h -gt 0) { $attrs += " height=`"$h`"" }
    $lines = [Collections.Generic.List[string]]::new()
    if ($properties.Count -eq 0) {
        $lines.Add("  <object $attrs/>")
        return ($lines -join "`n")
    }
    $lines.Add("  <object $attrs>")
    $lines.Add('   <properties>')
    foreach ($key in $properties.Keys) {
        $entry = $properties[$key]
        if ($entry -is [hashtable]) {
            $lines.Add((Property-Xml $key ([string]$entry.Value) ([string]$entry.Type)))
        } else {
            $lines.Add((Property-Xml $key ([string]$entry)))
        }
    }
    $lines.Add('   </properties>')
    $lines.Add('  </object>')
    return ($lines -join "`n")
}

function Layer-Xml([int] $id, [string] $name, [int[]] $grid) {
    return @(
        " <layer id=`"$id`" name=`"$name`" width=`"$script:width`" height=`"$script:height`">",
        '  <data encoding="base64">',
        (Grid-Base64 $grid),
        '  </data>',
        ' </layer>'
    ) -join "`n"
}

function Object-Group-Xml([int] $id, [string] $name, [Collections.Generic.List[string]] $objects) {
    return @(
        " <objectgroup id=`"$id`" name=`"$name`">",
        ($objects -join "`n"),
        ' </objectgroup>'
    ) -join "`n"
}

function Add-CommonCollision([Collections.Generic.List[string]] $objects, [int] $mapIndex) {
    $id = 1
    $objects.Add((Object-Xml $id 'Ground' 'Collision' 0 352 5120 32)); $id++
    $platforms = switch ($mapIndex) {
        0 { @(@(448,304,256), @(960,272,192), @(1472,320,256), @(2016,256,192), @(2496,288,320), @(3072,240,256), @(3616,272,256), @(4160,224,320), @(4704,288,256)) }
        1 { @(@(384,288,256), @(896,240,256), @(1408,304,192), @(1888,256,256), @(2368,208,192), @(2816,272,320), @(3456,224,256), @(4032,288,320), @(4608,240,256)) }
        default { @(@(352,304,256), @(832,256,192), @(1280,224,256), @(1760,288,256), @(2240,240,320), @(2816,192,192), @(3264,256,256), @(3776,208,320), @(4384,272,256), @(4800,224,192)) }
    }
    $index = 1
    foreach ($platform in $platforms) {
        $objects.Add((Object-Xml $id ("Platform_{0:d2}" -f $index) 'Collision' $platform[0] $platform[1] $platform[2] 16)); $id++; $index++
    }
    $roofs = switch ($mapIndex) {
        0 { @(@(128,224,192), @(720,208,160), @(1248,224,192), @(2240,192,256), @(4128,208,192)) }
        1 { @(@(224,208,224), @(800,192,192), @(1552,208,224), @(2688,176,256), @(3904,208,192)) }
        default { @(@(160,208,192), @(704,176,224), @(1328,192,256), @(2080,176,256), @(3488,192,224), @(4320,176,256)) }
    }
    $index = 1
    foreach ($roof in $roofs) {
        $objects.Add((Object-Xml $id ("CastleRoof_{0:d2}" -f $index) 'Collision' $roof[0] $roof[1] $roof[2] 16)); $id++; $index++
    }
}

function Add-Gameplay(
    [Collections.Generic.List[string]] $objects,
    [int] $mapIndex,
    [string] $act,
    [string] $nextAct,
    [string[]] $enemyTypes,
    [int[]] $enemyXs,
    [int[]] $checkpointXs,
    [int[]] $tutorialXs,
    [int[]] $pickupXs
) {
    $id = 100
    $objects.Add((Object-Xml $id 'PlayerSpawn' 'Spawn' 48 336 0 0 @{ Island = $act; SpawnKind = 'Player' })); $id++
    $tutorialIndex = 1
    foreach ($x in $tutorialXs) {
        $props = @{ TutorialId = "Act03_$mapIndex`_$tutorialIndex"; Trigger = 'OnEnter'; MessageKey = "Act03Tutorial_$mapIndex`_$tutorialIndex"; AreaKind = 'MovementAndPlatforming' }
        $objects.Add((Object-Xml $id ("TutorialArea_{0:d2}" -f $tutorialIndex) 'TutorialArea' $x 256 192 96 $props)); $id++; $tutorialIndex++
    }
    $checkpointIndex = 1
    foreach ($x in $checkpointXs) {
        $props = @{ CheckpointId = @{ Value = [string]$checkpointIndex; Type = 'int' }; RespawnX = @{ Value = [string]$x; Type = 'int' }; RespawnY = @{ Value = '320'; Type = 'int' }; SaveKind = 'Act03Checkpoint' }
        $objects.Add((Object-Xml $id ("Checkpoint_{0:d2}" -f $checkpointIndex) 'Checkpoint' $x 320 16 32 $props)); $id++; $checkpointIndex++
    }
    $enemyIndex = 1
    for ($i = 0; $i -lt $enemyXs.Count; $i++) {
        $enemyType = $enemyTypes[$i % $enemyTypes.Count]
        $props = @{ EnemyType = $enemyType; SpawnMode = 'Single'; SpawnOnly = @{ Value = 'true'; Type = 'bool' }; SpawnOrigin = 'RuinPatrol'; Act = $act }
        $objects.Add((Object-Xml $id ("EnemySpawn_{0:d2}" -f $enemyIndex) 'EnemySpawn' $enemyXs[$i] 320 16 32 $props)); $id++; $enemyIndex++
    }
    $pickupIndex = 1
    foreach ($x in $pickupXs) {
        $kind = switch (($pickupIndex + $mapIndex) % 3) { 0 { 'Health' } 1 { 'Stamina' } default { 'PowerFruit' } }
        $props = @{
            CollectibleId = @{ Value = [string]$pickupIndex; Type = 'int' }
            PickupKind = $kind
            CollectibleSource = 'GameplayObject'
            VisualTileset = 'Act_03_Environment'
            VisualTileGid = @{ Value = [string](Tile 14 4); Type = 'int' }
            Respawn = 'PerLevel'
        }
        $objects.Add((Object-Xml $id ("Collectible_{0:d2}" -f $pickupIndex) 'Collectible' $x 304 16 32 $props)); $id++; $pickupIndex++
    }
    $exitProps = @{ NextLevelId = $nextAct; ExitKind = 'ActTransition'; Trigger = 'OnTouch' }
    if ([string]::IsNullOrWhiteSpace($nextAct)) { $exitProps = @{ ExitKind = 'EndOfAct'; Trigger = 'OnTouch' } }
    $objects.Add((Object-Xml $id ("{0}Exit" -f $act) 'LevelExit' 5088 288 32 64 $exitProps)); $id++
    if ($mapIndex -eq 2) {
        $bossProps = @{ BossType = 'Minotaur'; BossVariant = 'Minotaur_3'; Reward = 'SealRubyPart03'; SealPartIndex = @{ Value = '3'; Type = 'int' }; Trigger = 'OnEnter'; SpawnOnly = @{ Value = 'true'; Type = 'bool' } }
        $objects.Add((Object-Xml $id 'FinalBoss_Minotaur_3' 'BossSpawn' 4960 320 16 32 $bossProps)); $id++
    }
}

function Stamp-Atlas([int[]] $grid, [int] $destX, [int] $destY, [int] $sourceX, [int] $sourceY, [int] $tileWidth, [int] $tileHeight) {
    for ($y = 0; $y -lt $tileHeight; $y++) {
        for ($x = 0; $x -lt $tileWidth; $x++) {
            Set-Cell $grid ($destX + $x) ($destY + $y) (Tile ($sourceX + $x) ($sourceY + $y))
        }
    }
}

function New-VisualLayers([int] $mapIndex) {
    # The original reference is a 20x15 atlas of 120px panels. The generator
    # resizes it to 320x240, making each black-bounded panel one 16x16 tile.
    $sky = $script:backdropFirstGid
    $skyAlt = $script:backdropFirstGid + 1
    $backdropWall = $script:backdropFirstGid + 2
    $backdropGround = $script:backdropFirstGid + 3
    $stoneFloor = Tile 0 8
    $stoneFloorAlt = Tile 2 8
    $ashFloor = Tile 6 10
    $castleWall = Tile 1 4
    $wallAlt = Tile 3 6
    $woodPlatform = Tile 11 12
    $stonePlatform = Tile 2 4
    $rubble = Tile 6 7
    $fire = Tile 6 4
    $fireAlt = Tile 4 5
    $flameThird = Tile 14 4
    $column = Tile 12 12
    $banner = Tile 16 7

    $background = New-Grid $sky
    for ($y = 0; $y -lt 10; $y++) {
        for ($x = 0; $x -lt $script:width; $x++) {
            if ((($x + ($y * 2) + $mapIndex) % 13) -eq 0) { Set-Cell $background $x $y $skyAlt }
        }
    }
    Fill-Rect $background 0 8 319 13 $backdropWall
    Fill-Rect $background 0 14 319 19 $skyAlt
    for ($x = 0; $x -lt $script:width; $x += 18) {
        Set-Cell $background $x 12 $backdropGround
        Set-Cell $background ($x + 7) 15 $backdropWall
    }

    $ruins = New-Grid
    # Castle silhouettes: roof/building panels, wall sections, gates and
    # guardian/statue panels are placed as distinct horizon landmarks.
    $roofDestinations = switch ($mapIndex) {
        0 { @(24, 104, 184, 264) }
        1 { @(8, 88, 168, 248) }
        default { @(32, 112, 192, 272) }
    }
    foreach ($x in $roofDestinations) {
        Stamp-Atlas $ruins $x 1 8 1 12 3
        Stamp-Atlas $ruins ($x + 4) 5 10 8 6 4
    }
    foreach ($x in @(0, 80, 160, 240)) {
        Stamp-Atlas $ruins $x 8 0 4 10 3
        Stamp-Atlas $ruins ($x + 8) 11 8 8 8 6 4
    }
    foreach ($x in @(48, 144, 240)) {
        Stamp-Atlas $ruins $x 15 13 4 7 5
        Stamp-Atlas $ruins ($x + 8) 16 15 4 5 4
    }

    $terrain = New-Grid
    Fill-Rect $terrain 0 20 319 23 $stoneFloor
    Fill-Rect $terrain 0 19 319 20 $stoneFloorAlt
    for ($x = 0; $x -lt $script:width; $x += 20) {
        Stamp-Atlas $terrain $x 21 (($x / 20 + $mapIndex) % 8) 8 8 3
    }
    for ($x = 4; $x -lt $script:width; $x += 29) { Set-Cell $terrain $x 19 $ashFloor }

    $platforms = New-Grid
    $platformRanges = switch ($mapIndex) {
        0 { @(@(28,18,44), @(66,16,82), @(104,14,120), @(142,18,158), @(180,13,198), @(220,16,238), @(258,12,276), @(296,17,316)) }
        1 { @(@(24,17,42), @(58,14,76), @(96,18,114), @(132,12,150), @(168,16,188), @(208,13,226), @(246,17,266), @(286,12,310)) }
        default { @(@(20,18,38), @(54,15,72), @(88,12,108), @(124,17,144), @(160,13,180), @(198,10,218), @(236,16,256), @(274,12,294), @(306,15,318)) }
    }
    $platformTile = @($woodPlatform, $stonePlatform)
    $platformIndex = 0
    foreach ($range in $platformRanges) {
        Fill-Rect $platforms $range[0] $range[1] $range[2] $range[1] $platformTile[$platformIndex % 2]
        $platformIndex++
    }

    $structures = New-Grid
    for ($x = 16; $x -lt $script:width; $x += 32) {
        Set-Cell $structures $x 18 $column
        if ((($x / 32 + $mapIndex) % 3) -eq 0) { Set-Cell $structures ($x + 1) 17 $banner }
    }
    for ($x = 16; $x -lt $script:width; $x += 48) { Set-Cell $structures $x 19 $wallAlt }

    $details = New-Grid
    for ($x = 0; $x -lt $script:width; $x += 22) { Set-Cell $details $x 19 $rubble }
    $flameXs = switch ($mapIndex) { 0 { @(74, 176, 286) } 1 { @(48, 132, 224, 304) } default { @(64, 152, 240, 300) } }
    foreach ($x in $flameXs) {
        Set-Cell $details $x 18 $fire
        if ($x + 1 -lt $script:width) { Set-Cell $details ($x + 1) 18 $fireAlt }
        if ($x + 2 -lt $script:width) { Set-Cell $details ($x + 2) 18 $flameThird }
    }

    $collectibles = New-Grid
    $pickupXs = switch ($mapIndex) { 0 { @(544, 1120, 1744, 2528, 3472, 4320) } 1 { @(480, 1008, 1616, 2288, 3168, 4096, 4720) } default { @(448, 944, 1504, 2128, 2784, 3408, 4048, 4688) } }

    return [pscustomobject]@{
        Background = $background
        Ruins = $ruins
        Terrain = $terrain
        Platforms = $platforms
        Structures = $structures
        Details = $details
        Collectibles = $collectibles
        PickupXs = $pickupXs
    }
}

function Write-EnvironmentTileset {
    $platformIds = @((Tile 11 12), (Tile 2 4), (Tile 3 6))
    $flameIds = @((Tile 6 4), (Tile 4 5), (Tile 14 4), (Tile 6 12))
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('<?xml version="1.0" encoding="UTF-8"?>')
    $lines.Add('<tileset version="1.10" tiledversion="1.12.2" name="Act_03_Environment" tilewidth="16" tileheight="16" tilecount="320" columns="20" objectalignment="bottom">')
    $lines.Add(' <properties>')
    $lines.Add((Property-Xml 'Act' 'Act_03'))
    $lines.Add((Property-Xml 'TileSize' '16' 'int'))
    $lines.Add((Property-Xml 'PixelArt' 'true' 'bool'))
    $lines.Add((Property-Xml 'SourceImageGrid' '20x15 source tiles plus 4 generated backdrop tiles; all 16x16 px; black divisions normalized from 120x120 px'))
    $lines.Add((Property-Xml 'PlatformTileIds' (($platformIds -join ','))))
    $lines.Add((Property-Xml 'FlameAnimationTileIds' (($flameIds -join ','))))
    $lines.Add((Property-Xml 'AnimationFrameDurationMs' '160' 'int'))
    $lines.Add(' </properties>')
    $lines.Add(' <image source="Act_03_Environment_16.png" width="320" height="256"/>')
    $lines.Add(' <grid orientation="orthogonal" width="16" height="16"/>')
    for ($localId = $script:backdropFirstGid - 1; $localId -lt $script:backdropFirstGid + 3; $localId++) {
        $lines.Add(" <tile id=`"$localId`" type=`"Backdrop`">")
        $lines.Add('  <properties>')
        $lines.Add((Property-Xml 'Backdrop' 'true' 'bool'))
        $lines.Add('  </properties>')
        $lines.Add(' </tile>')
    }
    foreach ($gid in $platformIds) {
        $localId = $gid - 1
        $lines.Add(" <tile id=`"$localId`" type=`"Platform`">")
        $lines.Add('  <properties>')
        $lines.Add((Property-Xml 'Platform' 'true' 'bool'))
        $lines.Add((Property-Xml 'Solid' 'true' 'bool'))
        $lines.Add((Property-Xml 'FootstepSurface' 'Wood'))
        $lines.Add('  </properties>')
        $lines.Add(' </tile>')
    }
    $first = $flameIds[0] - 1
    $lines.Add(" <tile id=`"$first`" type=`"AnimatedFlame`">")
    $lines.Add('  <properties>')
    $lines.Add((Property-Xml 'Animation' 'Flame'))
    $lines.Add((Property-Xml 'Loop' 'true' 'bool'))
    $lines.Add('  </properties>')
    $lines.Add('  <animation>')
    foreach ($gid in $flameIds) { $lines.Add("   <frame tileid=`"$($gid - 1)`" duration=`"160`"/>") }
    $lines.Add('  </animation>')
    $lines.Add(' </tile>')
    $lines.Add('</tileset>')
    [IO.File]::WriteAllText((Join-Path $tilesetRoot 'Act_03_Environment.tsx'), ($lines -join "`n") + "`n", $encoding)
}

function Write-ResizedEnvironmentImage {
    $source = Join-Path $tilesetRoot 'Act_03_Environment.jpg'
    $destination = Join-Path $tilesetRoot 'Act_03_Environment_16.png'
    $normalized = Join-Path $env:TEMP 'Act_03_Environment_16_source.png'
    $backdrop = Join-Path $tilesetRoot 'Act_03_Backdrop.png'
    & magick $source -filter point -resize 320x240! $normalized
    if ($LASTEXITCODE -ne 0) { throw "ImageMagick failed to normalize the Act 03 environment image." }
    & magick $normalized $backdrop -append $destination
    if ($LASTEXITCODE -ne 0) { throw "ImageMagick failed to resize the Act 03 environment image." }
}

function Write-Map([int] $mapIndex, [string] $act, [string] $title, [string] $biome, [string] $nextAct, [string[]] $enemyTypes, [int[]] $enemyXs, [int[]] $checkpointXs, [int[]] $tutorialXs) {
    $layers = New-VisualLayers $mapIndex
    $background = $layers.Background; $ruins = $layers.Ruins; $terrain = $layers.Terrain; $platforms = $layers.Platforms; $structures = $layers.Structures; $details = $layers.Details; $collectibles = $layers.Collectibles; $pickupXs = $layers.PickupXs
    $collision = [Collections.Generic.List[string]]::new()
    Add-CommonCollision $collision $mapIndex
    $gameplay = [Collections.Generic.List[string]]::new()
    Add-Gameplay $gameplay $mapIndex $act $nextAct $enemyTypes $enemyXs $checkpointXs $tutorialXs $pickupXs

    $properties = [Collections.Generic.List[string]]::new()
    $properties.Add((Property-Xml 'Act' $act))
    $properties.Add((Property-Xml 'Biome' $biome))
    $properties.Add((Property-Xml 'Palette' 'charcoal stone, burnt umber, moss green, ember orange'))
    $properties.Add((Property-Xml 'TileSize' '16' 'int'))
    $properties.Add((Property-Xml 'VisualStyle' '16-bit pixel-art ruined castle kingdom with broken walls, scorched courtyards and suspended platforms'))
    $properties.Add((Property-Xml 'Weather' 'ashfall and ember haze'))
    $properties.Add((Property-Xml 'NextLevelId' $nextAct))
    $properties.Add((Property-Xml 'PlatformTiles' 'Collision object rectangles backed by Act_03_Environment platform tiles'))
    $properties.Add((Property-Xml 'FlameAnimation' 'Act_03_Environment AnimatedFlame tile'))
    $properties.Add((Property-Xml 'Collectibles' 'Gameplay object markers using Act_03_Environment visual references'))
    $actEnding = if ($mapIndex -eq 2) { 'The dead throne remains beyond the Act 03 ruins.' } else { "The trail continues into $nextAct." }
    $properties.Add((Property-Xml 'ActEnding' $actEnding))

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('<?xml version="1.0" encoding="UTF-8"?>')
    $lines.Add("<map version=`"1.10`" tiledversion=`"1.12.2`" orientation=`"orthogonal`" renderorder=`"right-down`" width=`"$script:width`" height=`"$script:height`" tilewidth=`"16`" tileheight=`"16`" infinite=`"0`" nextlayerid=`"10`" nextobjectid=`"250`">")
    $lines.Add(' <properties>')
    foreach ($property in $properties) { $lines.Add($property) }
    $lines.Add(' </properties>')
    $lines.Add(' <tileset firstgid="1" source="../Tilesets/Act_03_Environment.tsx"/>')
    $lines.Add((Layer-Xml 1 'Background' $background))
    $lines.Add((Layer-Xml 2 'RuinedCastle' $ruins))
    $lines.Add((Layer-Xml 3 'Terrain' $terrain))
    $lines.Add((Layer-Xml 4 'Platforms' $platforms))
    $lines.Add((Layer-Xml 5 'Structures' $structures))
    $lines.Add((Layer-Xml 6 'Details' $details))
    $lines.Add((Layer-Xml 7 'Collectibles' $collectibles))
    $lines.Add((Object-Group-Xml 8 'Collision' $collision))
    $lines.Add((Object-Group-Xml 9 'Gameplay' $gameplay))
    $lines.Add('</map>')
    [IO.File]::WriteAllText((Join-Path $mapRoot "$act.tmx"), ($lines -join "`n") + "`n", $encoding)
}

Write-ResizedEnvironmentImage
Write-EnvironmentTileset
Write-Map 0 'Act_03_A' 'A Muralha Quebrada' 'outer ruined castle wall and burned village approach' 'Act_03_B' @('Skeleton_Warrior','Skeleton_Archer','White_Werewolf','Skeleton_Spearman','Karasu_tengu','Black_Werewolf','Gorgon_1') @(640, 1040, 1536, 1856, 2240, 2784, 3328, 3872, 4416) @(1280, 2560, 3840) @(192, 1184, 2704) 
Write-Map 1 'Act_03_B' 'Pátio das Cinzas' 'ash-covered castle courtyard with broken towers and chapel ruins' 'Act_03_C' @('Yamabushi_tengu','Gorgon_3','Red_Werewolf','Kitsune','Gorgon_2','White_Werewolf','Skeleton_Spearman') @(576, 1120, 1632, 2080, 2592, 3136, 3680, 4224, 4768) @(1120, 2400, 3840) @(160, 1440, 3008)
Write-Map 2 'Act_03_C' 'Salão do Trono Morto' 'inner keep, ruined throne hall and final ash-filled battlements' '' @('Gorgon_3','Yamabushi_tengu','White_Werewolf','Kitsune','Skeleton_Spearman','Skeleton_Archer','Gorgon_3') @(512, 960, 1440, 1920, 2464, 3008, 3552, 4096, 4576) @(1024, 2304, 3712) @(176, 1328, 2816)

Write-Host 'Act 03 tileset and maps generated.'
