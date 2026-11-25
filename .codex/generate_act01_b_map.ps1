$ErrorActionPreference = 'Stop'

$rootPath = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Core\Content\Tiled'
$sourcePath = Join-Path $rootPath 'Maps\Act_01_A.tmx'
$destinationPath = Join-Path $rootPath 'Maps\Act_01_B.tmx'

[xml]$document = Get-Content -Raw $sourcePath
$map = $document.map
$map.nextobjectid = '80'

foreach ($property in $map.properties.property) {
    switch ([string]$property.name) {
        'Act' { $property.value = 'Act_01_B' }
        'Title' { $property.value = 'A Fortaleza do Templo Sem Lua' }
        'VisualStyle' { $property.value = '16-bit pixel art, dark feudal Japanese temple' }
        'ActEnding' { $property.value = 'The selected guardian is defeated' }
    }
}

$map.tileset.source = '../Tilesets/Act_01_Feudal_Dark.tsx'
$gameplay = @($map.objectgroup | Where-Object { $_.name -eq 'Gameplay' })[0]
if ($null -eq $gameplay) { throw 'Gameplay object layer not found.' }

foreach ($object in @($gameplay.object | Where-Object { $_.type -eq 'LevelExit' })) {
    [void]$gameplay.RemoveChild($object)
}

function Add-Property {
    param([System.Xml.XmlElement]$Object, [string]$Name, [string]$Value)
    $properties = $Object.SelectSingleNode('properties')
    if ($null -eq $properties) {
        $properties = $Object.OwnerDocument.CreateElement('properties')
        [void]$Object.AppendChild($properties)
    }
    $property = $Object.OwnerDocument.CreateElement('property')
    $property.SetAttribute('name', $Name)
    $property.SetAttribute('value', $Value)
    [void]$properties.AppendChild($property)
}

function Add-Enemy {
    param([int]$Id, [int]$X, [string]$EnemyType)
    $object = $document.CreateElement('object')
    $object.SetAttribute('id', [string]$Id)
    $object.SetAttribute('name', "EnemySpawn_B_$Id")
    $object.SetAttribute('type', 'EnemySpawn')
    $object.SetAttribute('x', [string]$X)
    $object.SetAttribute('y', '320')
    $object.SetAttribute('width', '16')
    $object.SetAttribute('height', '32')
    Add-Property $object 'EnemyType' $EnemyType
    [void]$gameplay.AppendChild($object)
}

# Act_01_B deliberately reuses only the two regular enemy types already used by Act_01_A.
Add-Enemy 50 720 'Black_Werewolf'
Add-Enemy 51 1040 'Skeleton_Warrior'
Add-Enemy 52 1360 'Black_Werewolf'
Add-Enemy 53 1760 'Skeleton_Warrior'
Add-Enemy 54 2240 'Black_Werewolf'
Add-Enemy 55 2720 'Skeleton_Warrior'
Add-Enemy 56 3360 'Black_Werewolf'
Add-Enemy 57 3920 'Skeleton_Warrior'
Add-Enemy 58 4480 'Black_Werewolf'

$boss = $document.CreateElement('object')
$boss.SetAttribute('id', '60')
$boss.SetAttribute('name', 'FinalBoss_Minotaur')
$boss.SetAttribute('type', 'BossSpawn')
$boss.SetAttribute('x', '4800')
$boss.SetAttribute('y', '256')
$boss.SetAttribute('width', '256')
$boss.SetAttribute('height', '96')
Add-Property $boss 'BossType' 'Minotaur'
Add-Property $boss 'BossVariant' 'Minotaur_2'
Add-Property $boss 'Trigger' 'OnEnter'
[void]$gameplay.AppendChild($boss)

$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create($destinationPath, $settings)
try {
    $document.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "Act_01_B gerado em $destinationPath"
