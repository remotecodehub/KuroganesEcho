$ErrorActionPreference='Stop'

# This script deliberately writes only the three TMX files. The refactored
# Act_03_Environment.tsx and its PNG are read-only inputs here.
$root=Split-Path -Parent $PSScriptRoot
$tiled=Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled'
$mapRoot=Join-Path $tiled 'Maps'
$tsxPath=Join-Path $tiled 'Tilesets\Act_03_Environment.tsx'
$width=320; $height=24; $utf8=[System.Text.UTF8Encoding]::new($false)
$tileset=[xml](Get-Content -LiteralPath $tsxPath -Raw)
if([int]$tileset.tileset.tilecount -ne 300 -or $tileset.tileset.image.source -ne 'Act_03_Environment_16x16.png'){ throw 'Unexpected Act_03_Environment.tsx; map rebuild stopped without changing the tileset.' }
New-Item -ItemType Directory -Force $mapRoot | Out-Null

function Escape([string]$s){[System.Security.SecurityElement]::Escape($s)}
function New-Grid([int]$fill=0){$g=[int[]]::new($width*$height);if($fill -ne 0){for($i=0;$i -lt $g.Length;$i++){$g[$i]=$fill}};return ,$g}
function Set-Cell([int[]]$g,[int]$x,[int]$y,[int]$gid){if($x -ge 0 -and $x -lt $width -and $y -ge 0 -and $y -lt $height){$g[$y*$width+$x]=$gid}}
function Fill-Rect([int[]]$g,[int]$x1,[int]$y1,[int]$x2,[int]$y2,[int]$gid){for($y=[Math]::Max(0,$y1);$y -le [Math]::Min($height-1,$y2);$y++){for($x=[Math]::Max(0,$x1);$x -le [Math]::Min($width-1,$x2);$x++){Set-Cell $g $x $y $gid}}}
function Grid-Base64([int[]]$g){$b=[byte[]]::new($g.Length*4);for($i=0;$i -lt $g.Length;$i++){[Array]::Copy([BitConverter]::GetBytes([uint32]$g[$i]),0,$b,$i*4,4)};[Convert]::ToBase64String($b)}
function Add-Properties([System.Collections.Generic.List[string]]$lines,[hashtable]$props,[int]$indent=2){foreach($key in $props.Keys){$v=[string]$props[$key];$type=$null;if($v -match '^(true|false)$'){$type='bool'}elseif($v -match '^-?\d+$'){$type='int'};$pad=' '*$indent;if($type){$lines.Add(('{0}<property name="{1}" type="{2}" value="{3}"/>' -f $pad,(Escape $key),$type,(Escape $v)))}else{$lines.Add(('{0}<property name="{1}" value="{2}"/>' -f $pad,(Escape $key),(Escape $v)))}}}
function Object-Xml([int]$id,[string]$name,[string]$type,[int]$x,[int]$y,[int]$w,[int]$h,[hashtable]$props){$o=[System.Collections.Generic.List[string]]::new();$o.Add(('   <object id="{0}" name="{1}" type="{2}" x="{3}" y="{4}" width="{5}" height="{6}">' -f $id,(Escape $name),(Escape $type),$x,$y,$w,$h));if($props.Count -gt 0){$o.Add('    <properties>');Add-Properties $o $props 6;$o.Add('    </properties>')};$o.Add('   </object>');return ,$o}
function Layer-Xml([int]$id,[string]$name,[int[]]$g){' <layer id="'+$id+'" name="'+$name+'" width="'+$width+'" height="'+$height+'">'+[Environment]::NewLine+'  <data encoding="base64">'+(Grid-Base64 $g)+'</data>'+[Environment]::NewLine+' </layer>'}

function Add-DuskBackground([int[]]$g,[int]$variant){
    # Empty cells expose the map's dark blue dusk background colour. Existing
    # dark atlas tiles are used only as distant silhouettes and bat markers.
    for($x=8;$x -lt $width;$x+=13){$sy=14+(($x+$variant)%3);$sil=$(if($x%2 -eq 0){9}else{10});Set-Cell $g $x $sy $sil}
    $bats=@(@(28,4),@(61,7),@(107,3),@(154,6),@(201,4),@(249,8),@(292,5));foreach($bat in $bats){$x=$bat[0]+$variant*2;$y=$bat[1];Set-Cell $g $x $y $(if(($x+$y)%2 -eq 0){19}else{20});Set-Cell $g ($x+1) $y $(if(($x+$y)%2 -eq 0){20}else{19})}
}
function Add-ExteriorCastle([int[]]$castle,[int[]]$structures,[int[]]$details,[int]$start,[int]$span,[int]$variant,[bool]$withBanner){
    # GIDs selected from the original source: masonry wall family 161-168,
    # roof fragments 29-39, Japanese gate family 247-250, invader flags 158/159.
    for($x=$start+2;$x -le $start+$span-2;$x++){
        for($y=12;$y -le 18;$y++){Set-Cell $castle $x $y $(if(($x+$y+$variant)%5 -eq 0){162}else{161})}
        if(($x+$variant)%7 -ne 0){Set-Cell $castle $x 10 $(if($x%3 -eq 0){29}else{30});Set-Cell $castle $x 11 $(if($x%4 -eq 0){31}else{32})}
    }
    for($x=$start+1;$x -le $start+$span-1;$x+=2){Set-Cell $castle $x 9 $(if($x%4 -eq 0){33}else{34})}
    $gate=$start+[Math]::Floor($span/2);for($y=14;$y -le 18;$y++){Set-Cell $structures $gate $y $(if($y%2 -eq 0){247}else{248});Set-Cell $structures ($gate+1) $y 249}
    for($y=11;$y -le 18;$y+=2){Set-Cell $structures ($start+2) $y 165;Set-Cell $structures ($start+$span-2) $y 166}
    if($withBanner){Set-Cell $structures ($start+4) 18 $(if($variant%2 -eq 0){158}else{159});Set-Cell $structures ($start+$span-4) 18 $(if($variant%2 -eq 0){159}else{158})}
    Set-Cell $details ($start-1) 19 299;Set-Cell $details ($start+$span+1) 19 300;Set-Cell $details ($start+[Math]::Floor($span/3)) 19 297
}
function Add-InteriorKeep([int[]]$castle,[int[]]$structures,[int[]]$details){
    # Interior of a semi-ruined keep: masonry side walls, broken ceiling,
    # columns, statues, a rear gate and a throne/boss dais.
    Fill-Rect $castle 8 8 18 18 161;Fill-Rect $castle 301 8 312 18 161;Fill-Rect $castle 274 9 312 12 162
    # Heavy masonry ceiling/wall bands make C read as an interior even where
    # the roof has collapsed; the gaps expose the blue dusk above the keep.
    Fill-Rect $castle 14 3 306 4 161;Fill-Rect $castle 14 5 306 7 162
    for($x=10;$x -le 309;$x+=3){if(($x%8)-ne 0){Set-Cell $castle $x 3 $(if($x%4 -eq 0){29}else{30});Set-Cell $castle $x 6 $(if($x%5 -eq 0){31}else{32})}}
    for($x=27;$x -le 294;$x+=31){Set-Cell $castle $x 3 0;Set-Cell $castle ($x+1) 3 0;Set-Cell $castle $x 4 0}
    for($x=20;$x -le 300;$x+=18){Set-Cell $castle $x 8 $(if($x%3 -eq 0){163}else{164})}
    for($x=34;$x -le 254;$x+=44){for($y=13;$y -le 18;$y+=2){Set-Cell $structures $x $y $(if($x%3 -eq 0){236}else{237})}}
    foreach($x in @(58,146,238)){Set-Cell $structures $x 17 $(if($x%2 -eq 0){185}else{186});Set-Cell $structures ($x+1) 17 187}
    for($y=14;$y -le 18;$y++){Set-Cell $structures 286 $y $(if($y%2 -eq 0){247}else{248});Set-Cell $structures 287 $y 249}
    Set-Cell $structures 292 16 257;Set-Cell $structures 293 16 257;Set-Cell $details 270 19 299;Set-Cell $details 302 19 298
    foreach($x in @(22,88,174,260)){Set-Cell $details $x 18 190;Set-Cell $details ($x+1) 19 294}
}
function Add-Platform([int[]]$g,[int]$start,[int]$row,[int]$length,[int]$gid){for($x=$start;$x -lt $start+$length;$x++){Set-Cell $g $x $row $gid}}

function Build-Map([string]$name,[string]$biome,[string]$next,[ValidateSet('Exterior','Interior')][string]$mode,[int]$variant,[array]$castleSpecs,[array]$platformSpecs,[array]$collectibleTiles,[array]$fireTiles,[bool]$boss){
    $background=New-Grid;$castle=New-Grid;$terrain=New-Grid;$platforms=New-Grid;$structures=New-Grid;$details=New-Grid;$collectibles=New-Grid
    Add-DuskBackground $background $variant
    if($mode -eq 'Exterior'){
        Fill-Rect $terrain 0 20 319 23 221;for($x=0;$x -lt $width;$x++){Set-Cell $terrain $x 20 $(if(($x+$variant)%9 -lt 3){222}elseif(($x+$variant)%13 -eq 0){224}else{221})}
        foreach($spec in $castleSpecs){Add-ExteriorCastle $castle $structures $details $spec[0] $spec[1] ($variant+$spec[2]) $true}
    }else{
        Fill-Rect $terrain 0 20 319 23 221;for($x=0;$x -lt $width;$x++){Set-Cell $terrain $x 20 $(if($x%8 -lt 2){222}else{221})};Add-InteriorKeep $castle $structures $details
    }
    foreach($p in $platformSpecs){Add-Platform $platforms $p[0] $p[1] $p[2] $p[3]}
    if($mode -eq 'Exterior'){
        foreach($x in $fireTiles){Set-Cell $details $x 19 $(if($x%2 -eq 0){279}else{280});Set-Cell $details ($x+1) 19 $(if($x%2 -eq 0){280}else{279})}
        for($x=15+$variant;$x -lt 315;$x+=31){Set-Cell $details $x 19 $(if($x%3 -eq 0){297}else{298})}
    }

    $objects=[System.Collections.Generic.List[string]]::new();$id=1
    $objects.AddRange((Object-Xml $id 'PlayerSpawn' 'PlayerSpawn' 32 288 16 32 @{SpawnPoint='Act03Start';Facing='Right'}));$id++
    $checkpoints=@(1024,2560,4096);for($i=0;$i -lt $checkpoints.Count;$i++){$objects.AddRange((Object-Xml $id ("Checkpoint_{0:D2}"-f($i+1)) 'Checkpoint' $checkpoints[$i] 272 16 48 @{CheckpointId=("$name`_CP$($i+1)");RespawnFacing='Right'}));$id++}
    $tutorials=@(@(128,256,256,64,'MovementAndJump'),@(2048,192,512,64,'Platforms'),@(3680,240,512,64,'EnemyTiming'));foreach($t in $tutorials){$objects.AddRange((Object-Xml $id "Tutorial_$($t[4])" 'TutorialArea' $t[0] $t[1] $t[2] $t[3] @{TutorialId=$t[4];PromptState='Unseen'}));$id++}
    foreach($x in $collectibleTiles){$objects.AddRange((Object-Xml $id ("Collectible_{0:D2}"-f$id) 'Collectible' ($x*16) 272 12 12 @{PickupKind='SpiritShard';CollectibleSource='GameplayObject';VisualTileset='Act_03_Environment';VisualTileGid='208'}));$id++}
    $enemyTiles=@(40,76,112,150,188,226,264);$enemyIndex=1;foreach($x in $enemyTiles){$objects.AddRange((Object-Xml $id ("Enemy_{0:D2}"-f$enemyIndex) 'EnemySpawn' ($x*16) 288 16 32 @{EnemyId=("$name`_Enemy$enemyIndex");EnemyArchetype=if($enemyIndex%3 -eq 0){'AshRonin'}elseif($enemyIndex%3 -eq 1){'WarHusk'}else{'CastleArcher'};Facing='Left'}));$id++;$enemyIndex++}
    if($mode -eq 'Exterior'){foreach($x in $castleSpecs){$bannerX=($x[0]+4)*16;$objects.AddRange((Object-Xml $id "InvaderBanner_$id" 'InvaderBanner' $bannerX 288 16 32 @{VisualTileGid=if($id%2 -eq 0){158}else{159};Faction='WesternInvaders';Planted='true';Damage='false'}));$id++};foreach($x in $fireTiles){$objects.AddRange((Object-Xml $id "Campfire_$id" 'FireHazard' ($x*16) 304 32 16 @{VisualTileGid=if($x%2 -eq 0){279}else{280};AnimationFrames='279,280,294,295';DamageType='Fire';Damage='1';Jumpable='true';Hazard='SmallCampfire'}));$id++}}
    $objects.AddRange((Object-Xml $id 'ExitToNextAct' 'Exit' 5056 256 64 64 @{TargetLevel=$next;Transition='CastleGate'}));$id++
    if($boss){$objects.AddRange((Object-Xml $id 'Act03BossSpawn' 'Boss' 4608 256 32 64 @{BossId='Act03CastleWarlord';Arena='InnerKeep';LockArena='true'}));$id++}
    $gameplay='   <objectgroup id="8" name="Gameplay">'+"`n"+($objects -join "`n")+"`n   </objectgroup>"
    $collision=[System.Collections.Generic.List[string]]::new();$cid=1;$collision.AddRange((Object-Xml $cid 'Ground' 'Solid' 0 320 5120 64 @{CollisionType='Solid';Surface='RuinedStone'}));$cid++
    foreach($p in $platformSpecs){$collision.AddRange((Object-Xml $cid "Platform_$cid" 'Solid' ($p[0]*16) ($p[1]*16) ($p[2]*16) 16 @{CollisionType='Platform';Surface=if($p[3] -eq 65){'Wood'}else{'Stone'};OneWay='true'}));$cid++}
    if($mode -eq 'Interior'){$collision.AddRange((Object-Xml $cid 'BossArenaFloor' 'Solid' 4000 288 1200 32 @{CollisionType='Solid';Surface='CastleStone'}));$cid++}
    $collisionXml='   <objectgroup id="9" name="Collision">'+"`n"+($collision -join "`n")+"`n   </objectgroup>"
    $xml=[System.Collections.Generic.List[string]]::new();$xml.Add('<?xml version="1.0" encoding="UTF-8"?>');$xml.Add(('<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="320" height="24" tilewidth="16" tileheight="16" infinite="0" backgroundcolor="#1c2b4b" nextlayerid="10" nextobjectid="{0}">'-f$id));$xml.Add(' <properties>');$xml.Add(('  <property name="Act" value="{0}"/>'-f$name));$xml.Add(('  <property name="Biome" value="{0}"/>'-f$biome));$xml.Add('  <property name="TileSize" type="int" value="16"/>');$xml.Add('  <property name="TilesetPolicy" value="Every map GID rebuilt from Act_03_Environment.tsx; no legacy GIDs"/>');$xml.Add('  <property name="BackgroundTheme" value="Dusk blue sky without sun, distant western-war silhouettes and flying bats"/>');$xml.Add('  <property name="ConstructionPalette" value="Original atlas masonry, broken roofs, Japanese gates, stone platforms, debris, banners and campfires"/>');$xml.Add('  <property name="FireHazard" value="Small jumpable campfires near invader banners; DamageArea metadata in Gameplay"/>');$xml.Add(('  <property name="NextLevelId" value="{0}"/>'-f$next));$xml.Add(' </properties>');$xml.Add(' <tileset firstgid="1" source="../Tilesets/Act_03_Environment.tsx"/>');$xml.Add((Layer-Xml 1 'Background' $background));$xml.Add((Layer-Xml 2 'RuinedCastle' $castle));$xml.Add((Layer-Xml 3 'Terrain' $terrain));$xml.Add((Layer-Xml 4 'Platforms' $platforms));$xml.Add((Layer-Xml 5 'Structures' $structures));$xml.Add((Layer-Xml 6 'Details' $details));$xml.Add((Layer-Xml 7 'Collectibles' $collectibles));$xml.Add($gameplay);$xml.Add($collisionXml);$xml.Add('</map>')
    [IO.File]::WriteAllLines((Join-Path $mapRoot "$name.tmx"),$xml,$utf8)
}

Build-Map 'Act_03_A' 'exterior west approach: dusk, burned village edge and breached outer wall' 'Act_03_B' 'Exterior' 0 @(@(18,28,0),@(86,24,1),@(154,32,2),@(244,30,3)) @(@(32,17,9,65),@(72,14,8,65),@(128,16,11,121),@(198,12,9,65),@(276,15,10,121)) @(22,64,118,178,236,292) @(28,82,142,214,276) $false
Build-Map 'Act_03_B' 'exterior inner bailey: shattered concrete defenses and occupied courtyard' 'Act_03_C' 'Exterior' 1 @(@(12,34,0),@(70,30,1),@(138,36,2),@(224,40,3),@(282,24,4)) @(@(26,16,10,65),@(64,12,8,121),@(112,15,10,65),@(170,11,12,121),@(232,14,10,65),(294,10,8,121)) @(18,58,104,166,222,278,306) @(24,96,158,226,296) $false
Build-Map 'Act_03_C' 'interior of a semi-ruined castle keep: nave, columns, broken ceiling and boss hall' 'Act_04' 'Interior' 2 @() @(@(30,15,10,121),@(92,13,8,121),@(154,16,12,65),@(226,12,10,121),@(276,15,9,65)) @(44,108,176,244,286) @() $true
Write-Output 'Rebuilt Act_03_A/B/C TMX only from the existing Act_03_Environment.tsx.'
