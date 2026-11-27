$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$mapRoot=Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Maps'
$atlas=[System.Drawing.Bitmap]::new((Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Tilesets\Act_03_Environment_16.png'))
$scale=4; $w=320; $h=24
foreach($mapPath in Get-ChildItem $mapRoot -Filter 'Act_03_*.tmx') {
    $m=[xml](Get-Content -Raw $mapPath.FullName); $out=[System.Drawing.Bitmap]::new($w*$scale,$h*$scale,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb); $g=[System.Drawing.Graphics]::FromImage($out); $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
    foreach($layer in @($m.map.layer)) { $bytes=[Convert]::FromBase64String(($layer.data.'#text' -replace '\s','')); for($i=0;$i -lt $w*$h;$i++){ $gid=[BitConverter]::ToUInt32($bytes,$i*4); if($gid -eq 0){continue}; $local=$gid-1; $src=[System.Drawing.Rectangle]::new(($local%8)*16,[Math]::Floor($local/8)*16,16,16); $dst=[System.Drawing.Rectangle]::new(($i%$w)*$scale,[Math]::Floor($i/$w)*$scale,$scale,$scale); $g.DrawImage($atlas,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel) } }
    $g.Dispose(); $out.Save((Join-Path $PSScriptRoot ($mapPath.BaseName+'_preview.png')),[System.Drawing.Imaging.ImageFormat]::Png); $out.Dispose()
}
$atlas.Dispose()
