$ErrorActionPreference = 'Stop'

# Rebuilds the Act 03 atlas and maps from zero. The maps intentionally use one
# external tileset only: Act_03_Environment.tsx.
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$tiled = Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled'
$maps = Join-Path $tiled 'Maps'
$tilesets = Join-Path $tiled 'Tilesets'
$source = Join-Path $tilesets 'Act_03_Environment.jpg'
$normalized = Join-Path $PSScriptRoot 'act03_source_320x240.png'
$atlasPath = Join-Path $tilesets 'Act_03_Environment_16.png'
$tsxPath = Join-Path $tilesets 'Act_03_Environment.tsx'
$magick = 'magick.exe'
$width = 320
$height = 24
$tileSize = 16
$atlasColumns = 8
$atlasRows = 5
$tileCount = $atlasColumns * $atlasRows
$utf8 = [System.Text.UTF8Encoding]::new($false)

New-Item -ItemType Directory -Force -Path $maps, $tilesets | Out-Null

# The source is an illustrated panel sheet. ImageMagick first normalizes its
# black panel grid to a strict 16x16 sampling grid, then selected panels are
# curated into the compact Act 03 atlas below.
& $magick $source -filter point -resize '320x240!' -define png:color-type=6 $normalized

function Escape([string]$value) { [System.Security.SecurityElement]::Escape($value) }
function Gid([int]$localId) { $localId + 1 }
function New-Grid([int]$fill = 0) { $g = [int[]]::new($width * $height); if ($fill -ne 0) { for ($i=0; $i -lt $g.Length; $i++) { $g[$i] = $fill } }; return ,$g }
function Set-Cell([int[]]$grid, [int]$x, [int]$y, [int]$gid) { if ($x -ge 0 -and $x -lt $width -and $y -ge 0 -and $y -lt $height) { $grid[$y * $width + $x] = $gid } }
function Fill-Rect([int[]]$grid, [int]$x1, [int]$y1, [int]$x2, [int]$y2, [int]$gid) {
    for ($y = [Math]::Max(0,$y1); $y -le [Math]::Min($height-1,$y2); $y++) { for ($x = [Math]::Max(0,$x1); $x -le [Math]::Min($width-1,$x2); $x++) { Set-Cell $grid $x $y $gid } }
}
function Base64([int[]]$grid) {
    $bytes = [byte[]]::new($grid.Length * 4)
    for ($i = 0; $i -lt $grid.Length; $i++) { [Array]::Copy([BitConverter]::GetBytes([uint32]$grid[$i]),0,$bytes,$i*4,4) }
    [Convert]::ToBase64String($bytes)
}
function Add-PropXml([System.Collections.Generic.List[string]]$lines, [hashtable]$props, [int]$indent = 2) {
    foreach ($key in $props.Keys) {
        $value = [string]$props[$key]; $type = $null
        if ($value -match '^(true|false)$') { $type = 'bool' } elseif ($value -match '^-?\d+$') { $type = 'int' }
        $pad = ' ' * $indent
        if ($type) { $lines.Add(('{0}<property name="{1}" type="{2}" value="{3}"/>' -f $pad,(Escape $key),$type,(Escape $value))) }
        else { $lines.Add(('{0}<property name="{1}" value="{2}"/>' -f $pad,(Escape $key),(Escape $value))) }
    }
}

# ----------------------- Compact 16x16 atlas ------------------------------
$sourceBitmap = [System.Drawing.Bitmap]::new($normalized)
$atlas = [System.Drawing.Bitmap]::new($atlasColumns*$tileSize, $atlasRows*$tileSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($atlas)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

function Atlas-Rect([int]$index) { [System.Drawing.Rectangle]::new(($index % $atlasColumns)*16, [Math]::Floor($index/$atlasColumns)*16, 16, 16) }
function Draw-Backdrop([int]$index, [System.Drawing.Color]$base, [System.Drawing.Color]$accent, [int]$mode) {
    $r = Atlas-Rect $index; $b = [System.Drawing.SolidBrush]::new($base); $graphics.FillRectangle($b,$r); $b.Dispose()
    $p = [System.Drawing.Pen]::new($accent,1)
    if ($mode -eq 0) { $graphics.DrawLine($p,$r.X,$r.Y+12,$r.Right,$r.Y+12); $graphics.DrawLine($p,$r.X+3,$r.Y+4,$r.X+6,$r.Y+4); $graphics.DrawLine($p,$r.X+11,$r.Y+7,$r.X+14,$r.Y+7) }
    elseif ($mode -eq 1) { $graphics.DrawLine($p,$r.X,$r.Y+5,$r.Right,$r.Y+5); $graphics.DrawLine($p,$r.X+2,$r.Y+11,$r.X+5,$r.Y+11); $graphics.DrawLine($p,$r.X+9,$r.Y+8,$r.X+13,$r.Y+8) }
    elseif ($mode -eq 2) { $graphics.FillRectangle([System.Drawing.Brushes]::MidnightBlue,$r.X+2,$r.Y+7,4,9); $graphics.FillRectangle([System.Drawing.Brushes]::MidnightBlue,$r.X+8,$r.Y+4,3,12); $graphics.FillRectangle([System.Drawing.Brushes]::MidnightBlue,$r.X+12,$r.Y+9,3,7) }
    else { $graphics.DrawLine($p,$r.X,$r.Y+2,$r.Right,$r.Y+2); $graphics.DrawLine($p,$r.X+1,$r.Y+8,$r.X+5,$r.Y+8); $graphics.DrawLine($p,$r.X+9,$r.Y+12,$r.X+14,$r.Y+12) }
    $p.Dispose()
}

Draw-Backdrop 0 ([System.Drawing.Color]::FromArgb(255,18,24,46)) ([System.Drawing.Color]::FromArgb(255,56,47,75)) 0
Draw-Backdrop 1 ([System.Drawing.Color]::FromArgb(255,43,40,58)) ([System.Drawing.Color]::FromArgb(255,104,73,72)) 1
Draw-Backdrop 2 ([System.Drawing.Color]::FromArgb(255,30,31,48)) ([System.Drawing.Color]::FromArgb(255,72,55,63)) 2
Draw-Backdrop 3 ([System.Drawing.Color]::FromArgb(255,61,48,45)) ([System.Drawing.Color]::FromArgb(255,112,73,48)) 3

# Source panels are copied as whole 16x16 units. Coordinates refer to the
# normalized 20x15 source grid and were chosen for castle, roof, gate, torii,
# stone, wood, banner, rubble and flame motifs.
$sourceCoords = @(
    @(1,4), @(3,6), @(8,1), @(9,1), @(10,8), @(11,8), @(10,5), @(12,12),
    @(15,5), @(16,7), @(6,7), @(4,8), @(11,12), @(3,4), @(6,4), @(4,5),
    @(14,4), @(2,12), @(18,13), @(2,10), @(5,10), @(14,8), @(16,5), @(17,7),
    @(7,6), @(13,8), @(18,12), @(0,10), @(4,6), @(12,7), @(8,8), @(19,13)
)
for ($i = 0; $i -lt $sourceCoords.Count; $i++) {
    $idx = 4 + $i; $c = $sourceCoords[$i]; $dest = Atlas-Rect $idx
    $src = [System.Drawing.Rectangle]::new($c[0]*16,$c[1]*16,16,16)
    $graphics.DrawImage($sourceBitmap,$dest,$src,[System.Drawing.GraphicsUnit]::Pixel)
}
$sourceBitmap.Dispose(); $graphics.Dispose(); $atlas.Save($atlasPath,[System.Drawing.Imaging.ImageFormat]::Png); $atlas.Dispose()

$tsx = [System.Collections.Generic.List[string]]::new()
$tsx.Add('<?xml version="1.0" encoding="UTF-8"?>')
$tsx.Add('<tileset version="1.10" tiledversion="1.12.2" name="Act_03_Environment" tilewidth="16" tileheight="16" tilecount="40" columns="8" objectalignment="bottom">')
$tsx.Add(' <properties>')
$tsx.Add('  <property name="Act" value="Act_03"/>')
$tsx.Add('  <property name="TileSize" type="int" value="16"/>')
$tsx.Add('  <property name="PixelArt" type="bool" value="true"/>')
$tsx.Add('  <property name="SourceGrid" value="Act_03_Environment.jpg normalized by ImageMagick to 20x15 16px panels; compact atlas curated for Act 03"/>')
$tsx.Add('  <property name="MapTilesetPolicy" value="Act_03 maps reference this TSX only; no legacy GIDs"/>')
$tsx.Add(' </properties>')
$tsx.Add(' <image source="Act_03_Environment_16.png" width="128" height="80"/>')
$tsx.Add(' <grid orientation="orthogonal" width="16" height="16"/>')
$tileTypes = @('Backdrop','Backdrop','Backdrop','Terrain','CastleWall','CastleWall','Roof','Roof','Gate','Gate','Torii','Column','Statue','Banner','Rubble','ScorchedGround','Platform','Platform','AnimatedFlame','AnimatedFlameFrame','AnimatedFlameFrame','AnimatedFlameFrame','Debris','Shrine','Tree','BloodTrail','WallCap','RoofEdge','GateDetail','Statue','Banner','Ash','Chain','SmallStone','HangingWood','Window','PlatformEdge','PlatformEdge','FireCoal','Detail')
$tileProps = @{
    3=@{Solid='true'; Surface='Ground'}; 4=@{Solid='true'; Surface='CastleStone'}; 5=@{Solid='true'; Surface='CastleStone'}; 6=@{Solid='true'; Surface='Roof'}; 7=@{Solid='true'; Surface='Roof'}
    8=@{Solid='true'; Structure='Gate'}; 9=@{Solid='true'; Structure='Gate'}; 10=@{Structure='Torii'}; 11=@{Structure='Column'}; 12=@{Structure='Statue'}; 13=@{Structure='Banner'}
    14=@{Detail='Rubble'}; 15=@{Solid='true'; Surface='ScorchedGround'}; 16=@{Platform='true'; Solid='true'; FootstepSurface='Wood'}; 17=@{Platform='true'; Solid='true'; FootstepSurface='Stone'}
    18=@{Animation='Flame'; Loop='true'}
}
for ($id = 0; $id -lt $tileCount; $id++) {
    $type = $tileTypes[$id]; $tsx.Add((' <tile id="{0}" type="{1}">' -f $id,$type))
    if ($tileProps.ContainsKey($id)) { $tsx.Add('  <properties>'); Add-PropXml $tsx $tileProps[$id] 4; $tsx.Add('  </properties>') }
    if ($id -eq 18) {
        $tsx.Add('  <animation>'); foreach ($frame in 18,19,20,21) { $tsx.Add(('   <frame tileid="{0}" duration="150"/>' -f $frame)) }; $tsx.Add('  </animation>')
    }
    $tsx.Add(' </tile>')
}
$tsx.Add('</tileset>')
[IO.File]::WriteAllLines($tsxPath,$tsx,$utf8)

# ---------------------------- Map construction ----------------------------
function Add-Castle([int[]]$castle, [int[]]$structures, [int[]]$details, [int]$start, [int]$span, [int]$variant) {
    Fill-Rect $castle $start 10 ($start+$span) 18 5
    for ($x=$start; $x -le $start+$span; $x++) { if (($x+$variant) % 5 -eq 0) { Set-Cell $castle $x 12 6 }; if (($x+$variant) % 7 -eq 0) { Set-Cell $castle $x 16 6 } }
    for ($x=$start+2; $x -le $start+$span-2; $x++) { if (($x+$variant) % 6 -ne 0) { Set-Cell $castle $x 8 7; if (($x+$variant) % 4 -ne 0) { Set-Cell $castle $x 7 8 } } }
    for ($y=6; $y -le 12; $y++) { Set-Cell $structures ($start+1) $y 12; Set-Cell $structures ($start+$span-1) $y 12 }
    $gateX = $start + [Math]::Floor($span/2) - 1
    for ($y=13; $y -le 17; $y++) { Set-Cell $structures $gateX $y 9; Set-Cell $structures ($gateX+1) $y 10 }
    Set-Cell $structures ($start+2) 7 14; Set-Cell $structures ($start+$span-2) 7 30
    Set-Cell $details ($start-1) 19 15; Set-Cell $details ($start+$span+1) 19 14
}
function Add-Torii([int[]]$structures,[int]$x) { Set-Cell $structures $x 14 11; Set-Cell $structures ($x+1) 14 11; Set-Cell $structures ($x-1) 13 11; Set-Cell $structures ($x+2) 13 11; Set-Cell $structures ($x-1) 12 11; Set-Cell $structures $x 12 11; Set-Cell $structures ($x+1) 12 11; Set-Cell $structures ($x+2) 12 11 }
function Add-Flame([int[]]$details,[int]$x,[int]$y) { Set-Cell $details $x $y 19; Set-Cell $details ($x+1) $y 39; Set-Cell $details ($x-1) ($y+1) 15 }

function Object-Xml([int]$id,[string]$name,[string]$type,[int]$x,[int]$y,[int]$w,[int]$h,[hashtable]$props) {
    $o=[System.Collections.Generic.List[string]]::new(); $o.Add(('   <object id="{0}" name="{1}" type="{2}" x="{3}" y="{4}" width="{5}" height="{6}">' -f $id,(Escape $name),(Escape $type),$x,$y,$w,$h))
    if ($props -and $props.Count -gt 0) { $o.Add('    <properties>'); Add-PropXml $o $props 6; $o.Add('    </properties>') }; $o.Add('   </object>'); return ,$o
}

function Build-Map([string]$name,[string]$biome,[string]$next,[int[]]$castleStarts,[int[]]$castleSpans,[int[]]$platformRows,[int[]]$collectibleXs,[int[]]$flameXs,[int]$variant,[bool]$boss) {
    $background=New-Grid; $castle=New-Grid; $terrain=New-Grid; $platforms=New-Grid; $structures=New-Grid; $details=New-Grid; $collectibles=New-Grid
    Fill-Rect $background 0 0 319 8 1; Fill-Rect $background 0 9 319 14 2; Fill-Rect $background 0 15 319 19 3
    for ($x=0; $x -lt $width; $x+=16) { if (($x/16+$variant) % 3 -eq 0) { Set-Cell $background ($x/16) 17 2 } }
    Fill-Rect $terrain 0 20 319 23 4
    for ($x=0; $x -lt $width; $x++) { if (($x+$variant) % 11 -lt 3) { Set-Cell $terrain $x 20 16 } }
    for ($i=0; $i -lt $castleStarts.Count; $i++) { Add-Castle $castle $structures $details $castleStarts[$i] $castleSpans[$i] ($variant+$i) }
    for ($i=0; $i -lt $platformRows.Count; $i++) { $row=$platformRows[$i]; $start=22+$i*48+$variant*3; $len=7+($i%3); $tile= if ($i%2 -eq 0) { 17 } else { 18 }; for ($x=$start; $x -lt $start+$len; $x++) { Set-Cell $platforms $x $row $tile } }
    Add-Torii $structures (44+$variant); Add-Torii $structures (196-$variant)
    foreach ($x in $flameXs) { Add-Flame $details $x 18 }
    for ($x=12+$variant; $x -lt $width; $x+=37) { Set-Cell $details $x 19 23; Set-Cell $details ($x+1) 19 33; Set-Cell $details ($x+2) 19 15 }

    $objects=[System.Collections.Generic.List[string]]::new(); $nextId=1
    $objects.AddRange((Object-Xml $nextId 'PlayerSpawn' 'PlayerSpawn' 32 288 16 32 @{SpawnPoint='Act03Start'; Facing='Right'})); $nextId++
    $checkpointXs=@(1024,2560,4096); for ($i=0; $i -lt $checkpointXs.Count; $i++) { $objects.AddRange((Object-Xml $nextId ("Checkpoint_{0:D2}" -f ($i+1)) 'Checkpoint' $checkpointXs[$i] 272 16 48 @{CheckpointId=("Act03_{0}_CP{1}" -f $name.Substring($name.Length-1),($i+1)); RespawnFacing='Right'})); $nextId++ }
    $tutorials=@(@(128,256,256,64,'MovementAndJump'),@(2048,192,512,64,'Platforms'),@(3680,240,512,64,'EnemyTiming')); foreach ($t in $tutorials) { $objects.AddRange((Object-Xml $nextId "Tutorial_$($t[4])" 'TutorialArea' $t[0] $t[1] $t[2] $t[3] @{TutorialId=$t[4]; PromptState='Unseen'})); $nextId++ }
    foreach ($x in $collectibleXs) { $objects.AddRange((Object-Xml $nextId ("Collectible_{0:D2}" -f $nextId) 'Collectible' ($x*16) 272 12 12 @{PickupKind='SpiritShard'; CollectibleSource='GameplayObject'; VisualTileset='Act_03_Environment'; VisualTileGid='34'})); $nextId++ }
    $enemyXs=@(18,44,72,104,136,168,204,244,286); $enemyIndex=1; foreach ($x in $enemyXs) { $objects.AddRange((Object-Xml $nextId ("Enemy_{0:D2}" -f $enemyIndex) 'EnemySpawn' ($x*16) 288 16 32 @{EnemyId=("Act03_$name`_Enemy$enemyIndex"); EnemyArchetype= if ($enemyIndex%3 -eq 0) {'AshRonin'} elseif ($enemyIndex%3 -eq 1) {'WarHusk'} else {'CastleArcher'}; Facing='Left'})); $nextId++; $enemyIndex++ }
    $objects.AddRange((Object-Xml $nextId 'ExitToNextAct' 'Exit' 5056 256 64 64 @{TargetLevel=$next; Transition='CastleGate'})); $nextId++
    if ($boss) { $objects.AddRange((Object-Xml $nextId 'Act03BossSpawn' 'Boss' 4560 256 32 64 @{BossId='Act03CastleWarlord'; Arena='InnerKeep'; LockArena='true'})); $nextId++ }
    $gameplay = '   <objectgroup id="8" name="Gameplay">' + "`n" + ($objects -join "`n") + "`n   </objectgroup>"
    $collision=[System.Collections.Generic.List[string]]::new(); $cid=1
    $collision.AddRange((Object-Xml $cid 'Ground' 'Solid' 0 320 5120 64 @{CollisionType='Solid'; Surface='ScorchedStone'})); $cid++
    for ($i=0; $i -lt $platformRows.Count; $i++) { $row=$platformRows[$i]; $start=22+$i*48+$variant*3; $len=7+($i%3); $collision.AddRange((Object-Xml $cid ("Platform_{0:D2}" -f ($i+1)) 'Solid' ($start*16) ($row*16) ($len*16) 16 @{CollisionType='Platform'; Surface=if ($i%2 -eq 0){'Wood'}else{'Stone'}; OneWay='true'})); $cid++ }
    $collisionXml = '   <objectgroup id="9" name="Collision">' + "`n" + ($collision -join "`n") + "`n   </objectgroup>"
    function Layer([int]$id,[string]$layerName,[int[]]$grid) { ' <layer id="' + $id + '" name="' + $layerName + '" width="' + $width + '" height="' + $height + '">' + [Environment]::NewLine + '  <data encoding="base64">' + (Base64 $grid) + '</data>' + [Environment]::NewLine + ' </layer>' }
    $xml=[System.Collections.Generic.List[string]]::new(); $xml.Add('<?xml version="1.0" encoding="UTF-8"?>'); $xml.Add(('<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="{0}" height="{1}" tilewidth="16" tileheight="16" infinite="0" nextlayerid="10" nextobjectid="{2}">' -f $width,$height,$nextId))
    $xml.Add(' <properties>'); $xml.Add(('  <property name="Act" value="{0}"/>' -f (Escape $name))); $xml.Add(('  <property name="Biome" value="{0}"/>' -f (Escape $biome))); $xml.Add('  <property name="TileSize" type="int" value="16"/>'); $xml.Add('  <property name="TilesetPolicy" value="Only Act_03_Environment.tsx; all layer GIDs regenerated"/>'); $xml.Add('  <property name="VisualStructure" value="Feudal Japanese ruined castle route: distant ash sky, masonry keep silhouettes, gates, torii, scorched terrain and aligned platforms"/>'); $xml.Add('  <property name="AnimationPolicy" value="Flame frames 19-22 all belong to Act_03_Environment.tsx"/>'); $xml.Add(('  <property name="NextLevelId" value="{0}"/>' -f (Escape $next))); $xml.Add(' </properties>'); $xml.Add(' <tileset firstgid="1" source="../Tilesets/Act_03_Environment.tsx"/>')
    $xml.Add((Layer 1 'Background' $background)); $xml.Add((Layer 2 'RuinedCastle' $castle)); $xml.Add((Layer 3 'Terrain' $terrain)); $xml.Add((Layer 4 'Platforms' $platforms)); $xml.Add((Layer 5 'Structures' $structures)); $xml.Add((Layer 6 'Details' $details)); $xml.Add((Layer 7 'Collectibles' $collectibles)); $xml.Add($gameplay); $xml.Add($collisionXml); $xml.Add('</map>')
    [IO.File]::WriteAllLines((Join-Path $maps "$name.tmx"),$xml,$utf8)
}

Build-Map 'Act_03_A' 'outer castle approach and burned village gate' 'Act_03_B' @(16,76,144,212,270) @(28,25,28,24,26) @(16,13,17,11,15) @(12,46,82,126,182,248) @(34,108,176,238,292) 0 $false
Build-Map 'Act_03_B' 'war-ravaged castle courtyard and broken watchtowers' 'Act_03_C' @(10,68,132,202,268) @(30,31,34,30,30) @(15,12,16,10,14) @(10,42,84,126,184,230,286) @(28,96,170,244,302) 1 $false
Build-Map 'Act_03_C' 'inner keep, funeral torii road and warlord arena' 'Act_04' @(22,92,168,244) @(34,38,42,48) @(14,10,15,9,13) @(14,56,112,164,218,280) @(52,142,218,286) 2 $true

Remove-Item -LiteralPath $normalized -Force -ErrorAction SilentlyContinue
Write-Output "Rebuilt Act 03 atlas and maps with one tileset and $tileCount fresh local tiles."
