$ErrorActionPreference = 'Stop'

$mapPath = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled\Maps\Act_01_A.tmx'
$width = 320
$height = 24
$tileWidth = 16
$tileHeight = 16
$environmentFirstGid = 1
$itemsFirstGid = 73

$gid = @{
    GroundTop = $environmentFirstGid + 1
    GroundFill = $environmentFirstGid + 2
    WoodPlatform = $environmentFirstGid + 4
    PlatformEnd = $environmentFirstGid + 5
    VillageWall = $environmentFirstGid + 6
    VillageWallCracked = $environmentFirstGid + 7
    Roof = $environmentFirstGid + 8
    RoofEdge = $environmentFirstGid + 9
    Beam = $environmentFirstGid + 10
    Window = $environmentFirstGid + 11
    Door = $environmentFirstGid + 12
    ExtinguishedLantern = $environmentFirstGid + 13
    LanternPost = $environmentFirstGid + 14
    LanternGlass = $environmentFirstGid + 15
    Weed = $environmentFirstGid + 16
    Rubble = $environmentFirstGid + 17
    HealthPickup = $environmentFirstGid + 18
    MissingSealAltar = $environmentFirstGid + 19
    ChainAnchor = $environmentFirstGid + 20
    ChainHook = $environmentFirstGid + 21
    CheckpointLantern = $environmentFirstGid + 22
    EnemyShadow = $environmentFirstGid + 23
    GuardianStatue = $environmentFirstGid + 24
    GuardianGate = $environmentFirstGid + 25
    Fog = $environmentFirstGid + 26
    DimSky = $environmentFirstGid + 27
    Cloud = $environmentFirstGid + 28
    DistantMountain = $environmentFirstGid + 29
    WarningSign = $environmentFirstGid + 30
    DarkMarker = $environmentFirstGid + 31
    HealthPickupTile = $itemsFirstGid
    StaminaPickupTile = $itemsFirstGid + 1
    IronTalismanThirdTile = $itemsFirstGid + 2
}

function New-Grid {
    param([int]$GridWidth, [int]$GridHeight, [int]$DefaultValue = 0)
    $grid = [Collections.Generic.List[object]]::new()
    for ($y = 0; $y -lt $GridHeight; $y++) {
        $row = [int[]]::new($GridWidth)
        if ($DefaultValue -ne 0) {
            for ($x = 0; $x -lt $GridWidth; $x++) { $row[$x] = $DefaultValue }
        }
        $grid.Add($row)
    }
    return $grid
}

function Set-Tile {
    param([object]$Grid, [int]$X, [int]$Y, [int]$Value)
    if ($X -ge 0 -and $X -lt $width -and $Y -ge 0 -and $Y -lt $height) {
        $Grid[$Y][$X] = $Value
    }
}

function Add-Rect {
    param([object]$Grid, [int]$X, [int]$Y, [int]$RectWidth, [int]$RectHeight, [int]$Value)
    for ($yy = $Y; $yy -lt ($Y + $RectHeight); $yy++) {
        for ($xx = $X; $xx -lt ($X + $RectWidth); $xx++) {
            Set-Tile $Grid $xx $yy $Value
        }
    }
}

function Grid-Base64 {
    param([object]$Grid)
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    foreach ($row in $Grid) {
        foreach ($value in $row) { $writer.Write([uint32]$value) }
    }
    $writer.Flush()
    $encoded = [Convert]::ToBase64String($stream.ToArray())
    $writer.Dispose()
    $stream.Dispose()
    return $encoded
}

function Layer-Xml {
    param([int]$Id, [string]$Name, [object]$Grid)
    return @(
        "  <layer id=`"$Id`" name=`"$Name`" x=`"0`" y=`"0`" width=`"$width`" height=`"$height`">",
        '    <data encoding="base64">',
        (Grid-Base64 $Grid),
        '    </data>',
        '  </layer>'
    )
}

$background = New-Grid $width $height $gid.DimSky
$backdrop = New-Grid $width $height
$terrain = New-Grid $width $height
$props = New-Grid $width $height
$collectibles = New-Grid $width $height

# Distant silhouettes and clouds create the desaturated, extinguished-village mood.
foreach ($x in @(12, 43, 82, 131, 181, 235, 286)) { Set-Tile $background $x 5 $gid.Cloud }
foreach ($x in @(0, 22, 47, 74, 108, 142, 178, 214, 249, 281, 306)) { Set-Tile $background $x 13 $gid.DistantMountain }
foreach ($x in @(18, 55, 91, 129, 176, 220, 260, 298)) { Set-Tile $background $x 12 $gid.Fog }

# Abandoned village buildings in the first half of the act.
$buildings = @(
    @{ X = 6; Width = 18 }, @{ X = 35; Width = 19 }, @{ X = 70; Width = 21 },
    @{ X = 112; Width = 23 }, @{ X = 154; Width = 20 }, @{ X = 196; Width = 22 },
    @{ X = 238; Width = 21 }, @{ X = 275; Width = 19 }
)
foreach ($building in $buildings) {
    $x0 = [int]$building.X
    $buildingWidth = [int]$building.Width
    Add-Rect $backdrop $x0 16 $buildingWidth 6 $gid.VillageWall
    Set-Tile $backdrop ($x0 + 1) 16 $gid.VillageWallCracked
    Set-Tile $backdrop ($x0 + $buildingWidth - 2) 18 $gid.VillageWallCracked
    for ($x = $x0; $x -lt ($x0 + $buildingWidth); $x++) { Set-Tile $backdrop $x 14 $gid.Roof; Set-Tile $backdrop $x 15 $gid.RoofEdge }
    Set-Tile $backdrop ($x0 + 3) 18 $gid.Window
    Set-Tile $backdrop ($x0 + $buildingWidth - 5) 18 $gid.Window
    Set-Tile $backdrop ($x0 + [int]($buildingWidth / 2)) 20 $gid.Door
    Set-Tile $backdrop $x0 16 $gid.Beam
    Set-Tile $backdrop ($x0 + $buildingWidth - 1) 16 $gid.Beam
}

# Floor and the platform route used by the basic-jump and chain tutorials.
Add-Rect $terrain 0 22 $width 1 $gid.GroundTop
Add-Rect $terrain 0 23 $width 1 $gid.GroundFill
$platforms = @(
    @{ X = 28; Y = 19; W = 16 }, @{ X = 60; Y = 17; W = 12 }, @{ X = 90; Y = 20; W = 20 },
    @{ X = 125; Y = 16; W = 12 }, @{ X = 152; Y = 19; W = 16 }, @{ X = 188; Y = 15; W = 12 },
    @{ X = 222; Y = 18; W = 16 }, @{ X = 256; Y = 16; W = 20 }, @{ X = 292; Y = 19; W = 16 }
)
foreach ($platform in $platforms) {
    for ($x = [int]$platform.X; $x -lt ([int]$platform.X + [int]$platform.W); $x++) { Set-Tile $terrain $x ([int]$platform.Y) $gid.WoodPlatform }
    Set-Tile $terrain ([int]$platform.X) ([int]$platform.Y) $gid.PlatformEnd
    Set-Tile $terrain ([int]$platform.X + [int]$platform.W - 1) ([int]$platform.Y) $gid.PlatformEnd
}

# Props tell the story without requiring dialogue: all lanterns are out and the seal altar is empty.
foreach ($x in @(24, 54, 89, 130, 180, 223, 265, 288)) { Set-Tile $props $x 21 $gid.ExtinguishedLantern }
Set-Tile $props 38 21 $gid.MissingSealAltar
foreach ($x in @(18, 78, 143, 205, 278)) { Set-Tile $props $x 21 $gid.Weed }
foreach ($x in @(66, 180, 260)) { Set-Tile $props $x 21 $gid.HealthPickup }
foreach ($x in @(91, 188, 256)) { Set-Tile $props $x 14 $gid.ChainAnchor }
foreach ($x in @(96, 193, 261)) { Set-Tile $props $x 18 $gid.ChainHook }
foreach ($x in @(80, 160, 240)) { Set-Tile $props $x 21 $gid.CheckpointLantern }
foreach ($x in @(108, 145, 208, 278)) { Set-Tile $props $x 21 $gid.EnemyShadow }
Set-Tile $props 300 17 $gid.GuardianStatue
Set-Tile $props 306 14 $gid.GuardianGate
Set-Tile $props 286 21 $gid.WarningSign
Set-Tile $props 310 21 $gid.DarkMarker

# Collectibles are authored as tiles so their visual state and gameplay state
# share the same map cell. The final talisman fragment becomes collectible
# only after the boss is defeated by the runtime.
foreach ($x in @(66, 180, 260)) { Set-Tile $collectibles $x 20 $gid.HealthPickupTile }
foreach ($x in @(35, 128, 222)) { Set-Tile $collectibles $x 18 $gid.StaminaPickupTile }

$xml = [Collections.Generic.List[string]]::new()
$xml.Add('<?xml version="1.0" encoding="UTF-8"?>')
$xml.Add('<map version="1.10" tiledversion="1.11.0" orientation="orthogonal" renderorder="right-down" width="320" height="24" tilewidth="16" tileheight="16" infinite="0" nextlayerid="10" nextobjectid="28">')
$xml.Add('  <properties>')
$xml.Add('    <property name="Act" value="Act_01_A"/>')
$xml.Add('    <property name="Title" value="A Trilha das Lanternas Apagadas"/>')
$xml.Add('    <property name="VisualStyle" value="16-bit pixel art, desaturated village"/>')
$xml.Add('    <property name="TileSize" type="int" value="16"/>')
$xml.Add('    <property name="CharacterRenderScale" type="float" value="0.5"/>')
$xml.Add('    <property name="CheckpointCount" type="int" value="3"/>')
$xml.Add('    <property name="ActEnding" value="The trail continues into Act_01_B"/>')
$xml.Add('  </properties>')
$xml.Add('  <tileset firstgid="1" source="../Tilesets/Act_01_Environment.tsx"/>')
$xml.Add('  <tileset firstgid="73" source="../Tilesets/Act_01_Items.tsx"/>')
foreach ($line in (Layer-Xml 1 'Background' $background)) { $xml.Add([string]$line) }
foreach ($line in (Layer-Xml 2 'VillageBackdrop' $backdrop)) { $xml.Add([string]$line) }
foreach ($line in (Layer-Xml 3 'Terrain' $terrain)) { $xml.Add([string]$line) }
foreach ($line in (Layer-Xml 4 'Act01Props' $props)) { $xml.Add([string]$line) }
foreach ($line in (Layer-Xml 9 'Collectibles' $collectibles)) { $xml.Add([string]$line) }
$xml.Add('  <objectgroup id="5" name="Collision">')
$xml.Add('    <object id="1" name="Ground" type="Collision" x="0" y="352" width="5120" height="32"/>')
$xml.Add('    <object id="2" name="Platform_01" type="Collision" x="448" y="304" width="256" height="16"/>')
$xml.Add('    <object id="3" name="Platform_02" type="Collision" x="960" y="272" width="192" height="16"/>')
$xml.Add('    <object id="4" name="Platform_03" type="Collision" x="1440" y="320" width="320" height="16"/>')
$xml.Add('    <object id="5" name="Platform_04" type="Collision" x="2000" y="256" width="192" height="16"/>')
$xml.Add('    <object id="6" name="Platform_05" type="Collision" x="2432" y="304" width="256" height="16"/>')
$xml.Add('    <object id="7" name="Platform_06" type="Collision" x="3008" y="240" width="256" height="16"/>')
$xml.Add('    <object id="8" name="Platform_07" type="Collision" x="3552" y="288" width="256" height="16"/>')
$xml.Add('    <object id="9" name="Platform_08" type="Collision" x="4096" y="256" width="320" height="16"/>')
$xml.Add('    <object id="10" name="Platform_09" type="Collision" x="4672" y="304" width="256" height="16"/>')
$xml.Add('  </objectgroup>')
$xml.Add('  <objectgroup id="6" name="Gameplay">')
$xml.Add('    <object id="11" name="PlayerSpawn" type="Spawn" x="48" y="336"><properties><property name="Character" value="Kunoichi"/><property name="SpawnId" type="int" value="0"/></properties><point/></object>')
$xml.Add('    <object id="12" name="MissingSealIntro" type="StoryBeat" x="560" y="288" width="96" height="64"><properties><property name="Beat" value="The village has lost its color and the seal is missing"/><property name="Trigger" value="OnEnter"/></properties></object>')
$xml.Add('    <object id="13" name="ChainTutorial" type="Tutorial" x="1424" y="240" width="192" height="96"><properties><property name="Mechanic" value="Chain attack and platform traversal"/><property name="Trigger" value="OnEnter"/></properties></object>')
$xml.Add('    <object id="14" name="Checkpoint_01" type="Checkpoint" x="1280" y="320" width="16" height="32"><properties><property name="CheckpointId" type="int" value="1"/><property name="RespawnX" type="int" value="1280"/><property name="RespawnY" type="int" value="320"/></properties></object>')
$xml.Add('    <object id="15" name="Checkpoint_02" type="Checkpoint" x="2560" y="320" width="16" height="32"><properties><property name="CheckpointId" type="int" value="2"/><property name="RespawnX" type="int" value="2560"/><property name="RespawnY" type="int" value="320"/></properties></object>')
$xml.Add('    <object id="16" name="Checkpoint_03" type="Checkpoint" x="3840" y="320" width="16" height="32"><properties><property name="CheckpointId" type="int" value="3"/><property name="RespawnX" type="int" value="3840"/><property name="RespawnY" type="int" value="320"/></properties></object>')
$xml.Add('    <object id="20" name="EnemySpawn_01" type="EnemySpawn" x="1728" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="21" name="EnemySpawn_02" type="EnemySpawn" x="2320" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="22" name="EnemySpawn_03" type="EnemySpawn" x="3328" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="23" name="EnemySpawn_04" type="EnemySpawn" x="4448" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="26" name="EnemySpawn_05" type="EnemySpawn" x="1808" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="27" name="EnemySpawn_06" type="EnemySpawn" x="1888" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="36" name="EnemySpawn_07" type="EnemySpawn" x="2400" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="37" name="EnemySpawn_08" type="EnemySpawn" x="2480" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="38" name="EnemySpawn_09" type="EnemySpawn" x="3408" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="39" name="EnemySpawn_10" type="EnemySpawn" x="3488" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="40" name="EnemySpawn_11" type="EnemySpawn" x="4528" y="320" width="16" height="32"><properties><property name="EnemyType" value="Black_Werewolf"/></properties></object>')
$xml.Add('    <object id="41" name="EnemySpawn_12" type="EnemySpawn" x="4608" y="320" width="16" height="32"><properties><property name="EnemyType" value="Skeleton_Warrior"/></properties></object>')
$xml.Add('    <object id="24" name="Act01BExit" type="LevelExit" x="4992" y="304" width="32" height="48"><properties><property name="NextLevelId" value="Act_01_B"/><property name="Trigger" value="OnEnter"/></properties></object>')
$xml.Add('  </objectgroup>')
$xml.Add('</map>')

[IO.File]::WriteAllText($mapPath, ($xml -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Act_01 gerado em $mapPath"
