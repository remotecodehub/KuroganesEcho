$ErrorActionPreference='Stop'

# Map-only rebuild. It reads the three existing TSX files and writes only the
# TMX files; none of the tilesets or source images are modified.
$root=Split-Path -Parent $PSScriptRoot;$tiled=Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled';$mapRoot=Join-Path $tiled 'Maps';$utf8=[System.Text.UTF8Encoding]::new($false);$width=320;$height=24
$envTsx=Join-Path $tiled 'Tilesets\Act_03_Environment.tsx';$nightTsx=Join-Path $tiled 'Tilesets\Act_03.tsx';$castleTsx=Join-Path $tiled 'Tilesets\Act_03_Final.tsx'
foreach($p in @($envTsx,$nightTsx,$castleTsx)){if(!(Test-Path $p)){throw "Missing tileset: $p"}}
$envXml=[xml](Get-Content -Raw $envTsx);$nightXml=[xml](Get-Content -Raw $nightTsx);$castleXml=[xml](Get-Content -Raw $castleTsx)
if([int]$envXml.tileset.tilecount -ne 300 -or [int]$nightXml.tileset.tilecount -ne 300 -or [int]$castleXml.tileset.tilecount -ne 300){throw 'Expected 300 tiles in each refactored TSX.'}
New-Item -ItemType Directory -Force $mapRoot|Out-Null

function E([int]$local){1+$local}                 # Act_03_Environment.tsx
function N([int]$local){301+$local}              # Act_03.tsx, second tileset in A/B
function C([int]$local){1+$local}                # Act_03_Final.tsx in C
function Esc([string]$s){[Security.SecurityElement]::Escape($s)}
function Grid(){,[int[]]::new($width*$height)}
function Set-Cell2([int[]]$g,[int]$x,[int]$y,[int]$gid){if($x -ge 0 -and $x -lt $width -and $y -ge 0 -and $y -lt $height){$g[$y*$width+$x]=$gid}}
function Fill([int[]]$g,[int]$x1,[int]$y1,[int]$x2,[int]$y2,[int]$gid){for($y=[Math]::Max(0,$y1);$y -le [Math]::Min($height-1,$y2);$y++){for($x=[Math]::Max(0,$x1);$x -le [Math]::Min($width-1,$x2);$x++){Set-Cell2 $g $x $y $gid}}}
function B64([int[]]$g){$b=[byte[]]::new($g.Length*4);for($i=0;$i -lt $g.Length;$i++){[Array]::Copy([BitConverter]::GetBytes([uint32]$g[$i]),0,$b,$i*4,4)};[Convert]::ToBase64String($b)}
function Props([System.Collections.Generic.List[string]]$l,[hashtable]$p,[int]$n=2){foreach($k in $p.Keys){$v=[string]$p[$k];$t=$null;if($v -match '^(true|false)$'){$t='bool'}elseif($v -match '^-?\d+$'){$t='int'};$pad=' '*$n;if($t){$l.Add(('{0}<property name="{1}" type="{2}" value="{3}"/>'-f$pad,(Esc $k),$t,(Esc $v)))}else{$l.Add(('{0}<property name="{1}" value="{2}"/>'-f$pad,(Esc $k),(Esc $v)))}}}
function Obj([int]$id,[string]$name,[string]$type,[int]$x,[int]$y,[int]$w,[int]$h,[hashtable]$p){$o=[System.Collections.Generic.List[string]]::new();$o.Add(('   <object id="{0}" name="{1}" type="{2}" x="{3}" y="{4}" width="{5}" height="{6}">'-f$id,(Esc $name),(Esc $type),$x,$y,$w,$h));if($p.Count){$o.Add('    <properties>');Props $o $p 6;$o.Add('    </properties>')};$o.Add('   </object>');return ,$o}
function Layer([int]$id,[string]$name,[int[]]$g){' <layer id="'+$id+'" name="'+$name+'" width="'+$width+'" height="'+$height+'">'+[Environment]::NewLine+'  <data encoding="base64">'+(B64 $g)+'</data>'+[Environment]::NewLine+' </layer>'}

function Add-NightSky([int[]]$g,[int]$variant){
    # Env_Tileset.jpg rows 10-14 are the authored star field, moon and bats.
    for($y=0;$y -lt 20;$y++){for($x=0;$x -lt $width;$x++){Set-Cell2 $g $x $y (N(200+(($x+$y+$variant)%8)))}}
    # Preserve the moon's original 6x5 source formation from Env_Tileset.jpg.
    $targetX=142;$targetY=1;for($sy=10;$sy -le 14;$sy++){for($sx=2;$sx -le 7;$sx++){Set-Cell2 $g ($targetX+$sx-2) ($targetY+$sy-10) (N($sy*20+$sx))}}
    # Original bat rows in Env_Tileset.jpg: local IDs 270-279 and 290-296.
    $batLocations=@(@(34,4,270),@(68,7,271),@(101,3,272),@(187,6,273),@(224,4,274),@(264,8,275),@(304,5,290),@(118,9,291));foreach($bat in $batLocations){Set-Cell2 $g $bat[0] $bat[1] (N $bat[2])}
}
function Add-ExteriorRuins([int[]]$ruins,[int[]]$structures,[int[]]$details,[int]$start,[int]$span,[int]$variant){
    # Act_03_Environment.png: stone wall family 161-168, roof family 29-34,
    # Japanese gate family 247-250, banners 158-159, rubble 297-300.
    for($x=$start+2;$x -le $start+$span-2;$x++){for($y=12;$y -le 18;$y++){Set-Cell2 $ruins $x $y (E($(if((($x+$y+$variant)%6)-eq 0){162}else{161})))};if((($x+$variant)%7)-ne 0){Set-Cell2 $ruins $x 10 (E($(if($x%3 -eq 0){29}else{30})));Set-Cell2 $ruins $x 11 (E($(if($x%4 -eq 0){31}else{32})))}}
    for($x=$start+1;$x -le $start+$span-1;$x+=2){Set-Cell2 $ruins $x 9 (E($(if($x%4 -eq 0){33}else{34})))}
    $gate=$start+[Math]::Floor($span/2);for($y=14;$y -le 18;$y++){Set-Cell2 $structures $gate $y (E($(if($y%2 -eq 0){247}else{248})));Set-Cell2 $structures ($gate+1) $y (E 249)}
    for($y=12;$y -le 18;$y+=2){Set-Cell2 $structures ($start+2) $y (E 165);Set-Cell2 $structures ($start+$span-2) $y (E 166)}
    Set-Cell2 $structures ($start+4) 18 (E($(if($variant%2 -eq 0){158}else{159})));Set-Cell2 $structures ($start+$span-4) 18 (E($(if($variant%2 -eq 0){159}else{158})))
    Set-Cell2 $details ($start-1) 19 (E 299);Set-Cell2 $details ($start+$span+1) 19 (E 300);Set-Cell2 $details ($start+[Math]::Floor($span/3)) 19 (E 297)
}
function Add-Platform([int[]]$g,[int]$start,[int]$row,[int]$length,[int]$gid){for($x=$start;$x -lt $start+$length;$x++){Set-Cell2 $g $x $row $gid}}
function Add-CastleInterior([int[]]$ruins,[int[]]$terrain,[int[]]$platforms,[int[]]$structures,[int[]]$details){
    # Castle_Tileset.jpg: stone/arch rows 0-6, carpet floor rows 9-10,
    # columns/statues/throne and damaged wood from the lower source rows.
    # Repeating the original 20-column source bands keeps the wall/arch
    # construction coherent instead of treating decorative debris as floor.
    for($x=0;$x -lt $width;$x++){Set-Cell2 $ruins $x 3 (C($x%20));Set-Cell2 $ruins $x 4 (C(20+($x%20)));Set-Cell2 $ruins $x 5 (C(40+($x%20)));for($y=7;$y -le 14;$y++){Set-Cell2 $ruins $x $y (C((($y-7)*20)+($x%20)))}}
    for($x=0;$x -lt 12;$x++){for($y=6;$y -le 18;$y++){Set-Cell2 $ruins $x $y (C(60+($x%10)))}};for($x=308;$x -lt $width;$x++){for($y=6;$y -le 18;$y++){Set-Cell2 $ruins $x $y (C(60+($x%10)))}}
    for($x=250;$x -lt $width;$x++){for($y=8;$y -le 15;$y++){Set-Cell2 $ruins $x $y (C(80+(($x+$y)%20)))}}
    for($x=0;$x -lt $width;$x++){Set-Cell2 $terrain $x 19 (C(200+($x%20)));for($y=20;$y -le 23;$y++){Set-Cell2 $terrain $x $y (C(180+($x%20)))}}
    foreach($x in @(30,72,116,160,204,248)){for($y=13;$y -le 18;$y+=2){Set-Cell2 $structures $x $y (C($(if($x%3 -eq 0){4}else{5})))}}
    foreach($x in @(54,144,236)){Set-Cell2 $structures $x 17 (C($(if($x%2 -eq 0){170}else{172})));Set-Cell2 $structures ($x+1) 17 (C 173)}
    for($y=13;$y -le 18;$y++){Set-Cell2 $structures 282 $y (C($(if($y%2 -eq 0){160}else{161})));Set-Cell2 $structures 283 $y (C 162)}
    Set-Cell2 $structures 289 16 (C 191);Set-Cell2 $structures 290 16 (C 192);Set-Cell2 $structures 291 16 (C 193)
    foreach($x in @(24,92,176,264)){Set-Cell2 $details $x 18 (C 71);Set-Cell2 $details ($x+1) 18 (C 72);Set-Cell2 $details ($x+2) 19 (C 247)}
    foreach($x in @(18,88,156,224,292)){Set-Cell2 $details $x 19 (C($(if($x%2 -eq 0){246}else{255})))}
    Add-Platform $platforms 34 15 10 (C 121);Add-Platform $platforms 96 13 9 (C 122);Add-Platform $platforms 168 16 12 (C 123);Add-Platform $platforms 232 12 11 (C 124);Add-Platform $platforms 282 15 9 (C 125)
}

function Build-Map([string]$name,[string]$next,[ValidateSet('Exterior','Interior')][string]$mode,[int]$variant,[array]$castleSpecs,[array]$platformSpecs,[array]$collectibles,[array]$fires,[bool]$boss){
    $background=Grid;$ruins=Grid;$terrain=Grid;$platforms=Grid;$structures=Grid;$details=Grid;$collectibleLayer=Grid
    if($mode -eq 'Exterior'){
        Add-NightSky $background $variant;for($x=0;$x -lt $width;$x++){Set-Cell2 $terrain $x 20 (E($(if(($x+$variant)%9 -lt 3){2}elseif(($x+$variant)%13 -eq 0){4}else{1})));for($y=21;$y -le 23;$y++){Set-Cell2 $terrain $x $y (E($(if(($x+$y)%7 -eq 0){3}else{1})))}}
        foreach($s in $castleSpecs){Add-ExteriorRuins $ruins $structures $details $s[0] $s[1] ($variant+$s[2])}
        foreach($p in $platformSpecs){Add-Platform $platforms $p[0] $p[1] $p[2] (E $p[3])}
        foreach($x in $fires){Set-Cell2 $details $x 19 (E($(if($x%2 -eq 0){279}else{280})));Set-Cell2 $details ($x+1) 19 (E($(if($x%2 -eq 0){280}else{279})))}
    }else{Add-CastleInterior $ruins $terrain $platforms $structures $details}
    $objects=[System.Collections.Generic.List[string]]::new();$id=1;$objects.AddRange((Obj $id 'PlayerSpawn' 'PlayerSpawn' 32 288 16 32 @{SpawnPoint='Act03Start';Facing='Right'}));$id++
    foreach($x in @(1024,2560,4096)){$objects.AddRange((Obj $id "Checkpoint_$id" 'Checkpoint' $x 272 16 48 @{CheckpointId=("$name`_CP$id");RespawnFacing='Right'}));$id++}
    foreach($t in @(@(128,256,256,64,'MovementAndJump'),@(2048,192,512,64,'Platforms'),@(3680,240,512,64,'EnemyTiming'))){$objects.AddRange((Obj $id "Tutorial_$($t[4])" 'TutorialArea' $t[0] $t[1] $t[2] $t[3] @{TutorialId=$t[4];PromptState='Unseen'}));$id++}
    foreach($x in $collectibles){$visualTileset=$(if($mode -eq 'Exterior'){'Act_03_Environment'}else{'Act_03_Final'});$visualGid=$(if($mode -eq 'Exterior'){208}else{291});$objects.AddRange((Obj $id "Collectible_$id" 'Collectible' ($x*16) 272 12 12 @{PickupKind='SpiritShard';CollectibleSource='GameplayObject';VisualTileset=$visualTileset;VisualTileGid=$visualGid}));$id++}
    foreach($x in @(40,76,112,150,188,226,264)){$enemyArchetype=$(if($id%3 -eq 0){'AshRonin'}elseif($id%3 -eq 1){'WarHusk'}else{'CastleArcher'});$objects.AddRange((Obj $id "Enemy_$id" 'EnemySpawn' ($x*16) 288 16 32 @{EnemyId=("$name`_Enemy$id");EnemyArchetype=$enemyArchetype;Facing='Left'}));$id++}
    if($mode -eq 'Exterior'){foreach($s in $castleSpecs){$bx=($s[0]+4)*16;$bannerGid=$(if($id%2 -eq 0){159}else{158});$objects.AddRange((Obj $id "InvaderBanner_$id" 'InvaderBanner' $bx 288 16 32 @{VisualTileset='Act_03_Environment';VisualTileGid=$bannerGid;Faction='WesternInvaders';Planted='true'}));$id++};foreach($x in $fires){$fireGid=$(if($x%2 -eq 0){280}else{279});$objects.AddRange((Obj $id "Campfire_$id" 'FireHazard' ($x*16) 304 32 16 @{VisualTileset='Act_03_Environment';VisualTileGid=$fireGid;AnimationFrames='279,280,294,295';DamageType='Fire';Damage='1';Jumpable='true';Hazard='SmallCampfire'}));$id++}}
    $objects.AddRange((Obj $id 'ExitToNextAct' 'Exit' 5056 256 64 64 @{TargetLevel=$next;Transition='CastleGate'}));$id++;if($boss){$objects.AddRange((Obj $id 'Act03BossSpawn' 'Boss' 4608 256 32 64 @{BossId='Act03CastleWarlord';Arena='InnerKeep';LockArena='true'}));$id++}
    $gameplay='   <objectgroup id="8" name="Gameplay">'+"`n"+($objects -join "`n")+"`n   </objectgroup>"; $collision=[System.Collections.Generic.List[string]]::new();$cid=1;$groundSurface=$(if($mode -eq 'Exterior'){'RuinedGround'}else{'CastleFloor'});$collision.AddRange((Obj $cid 'Ground' 'Solid' 0 320 5120 64 @{CollisionType='Solid';Surface=$groundSurface}));$cid++
    foreach($p in $platformSpecs){$platformSurface=$(if($mode -eq 'Exterior'){'StoneOrWood'}else{'CastleStone'});$collision.AddRange((Obj $cid "Platform_$cid" 'Solid' ($p[0]*16) ($p[1]*16) ($p[2]*16) 16 @{CollisionType='Platform';Surface=$platformSurface;OneWay='true'}));$cid++};$collisionXml='   <objectgroup id="9" name="Collision">'+"`n"+($collision -join "`n")+"`n   </objectgroup>"
    $backgroundColor=$(if($mode -eq 'Exterior'){'#15294a'}else{'#171a26'});$tilesetPolicy=$(if($mode -eq 'Exterior'){'Act_03_Environment.tsx + Act_03.tsx only'}else{'Act_03_Final.tsx only'});$sourceConstruction=$(if($mode -eq 'Exterior'){'Act_03_Environment.png structures; Env_Tileset.jpg moon and bats'}else{'Castle_Tileset.jpg castle interior'});$xml=[System.Collections.Generic.List[string]]::new();$xml.Add('<?xml version="1.0" encoding="UTF-8"?>');$xml.Add(('<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="320" height="24" tilewidth="16" tileheight="16" infinite="0" backgroundcolor="{0}" nextlayerid="10" nextobjectid="{1}">'-f $backgroundColor,$id));$xml.Add(' <properties>');$xml.Add(('  <property name="Act" value="{0}"/>'-f$name));$xml.Add(('  <property name="TilesetPolicy" value="{0}"/>'-f$tilesetPolicy));$xml.Add(('  <property name="SourceConstruction" value="{0}"/>'-f$sourceConstruction));$xml.Add('  <property name="TileSize" type="int" value="16"/>');$xml.Add(('  <property name="NextLevelId" value="{0}"/>'-f$next));$xml.Add(' </properties>');if($mode -eq 'Exterior'){$xml.Add(' <tileset firstgid="1" source="../Tilesets/Act_03_Environment.tsx"/>');$xml.Add(' <tileset firstgid="301" source="../Tilesets/Act_03.tsx"/>')}else{$xml.Add(' <tileset firstgid="1" source="../Tilesets/Act_03_Final.tsx"/>')};$xml.Add((Layer 1 'Background' $background));$xml.Add((Layer 2 'RuinedCastle' $ruins));$xml.Add((Layer 3 'Terrain' $terrain));$xml.Add((Layer 4 'Platforms' $platforms));$xml.Add((Layer 5 'Structures' $structures));$xml.Add((Layer 6 'Details' $details));$xml.Add((Layer 7 'Collectibles' $collectibleLayer));$xml.Add($gameplay);$xml.Add($collisionXml);$xml.Add('</map>');[IO.File]::WriteAllLines((Join-Path $mapRoot "$name.tmx"),$xml,$utf8)
}

Build-Map 'Act_03_A' 'Act_03_B' 'Exterior' 0 @(@(18,28,0),@(86,24,1),@(154,32,2),@(244,30,3)) @(@(32,17,9,121),@(72,14,8,122),@(128,16,11,123),@(198,12,9,121),@(276,15,10,122)) @(22,64,118,178,236,292) @(28,82,142,214,276) $false
Build-Map 'Act_03_B' 'Act_03_C' 'Exterior' 1 @(@(12,34,0),@(70,30,1),@(138,36,2),@(224,40,3),@(282,24,4)) @(@(26,16,10,121),@(64,12,8,122),@(112,15,10,123),@(170,11,12,121),@(232,14,10,122),(294,10,8,123)) @(18,58,104,166,222,278,306) @(24,96,158,226,296) $false
Build-Map 'Act_03_C' 'Act_04' 'Interior' 2 @() @() @(44,108,176,244,286) @() $true
Write-Output 'Rebuilt Act_03_A/B with Act_03_Environment.tsx + Act_03.tsx and Act_03_C with Act_03_Final.tsx; no tileset was modified.'
