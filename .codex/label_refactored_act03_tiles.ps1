Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$image=Join-Path $root 'src\KuroganesEcho.Core\Content\Tiled\Tilesets\Act_03_Environment_16x16.png'
$src=[System.Drawing.Bitmap]::new($image)
$scale=4; $cols=20; $rows=15; $cell=16; $font=[System.Drawing.Font]::new('Consolas',7,[System.Drawing.FontStyle]::Bold)
$out=[System.Drawing.Bitmap]::new($cols*$cell*$scale,$rows*$cell*$scale,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[System.Drawing.Graphics]::FromImage($out); $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half; $g.DrawImage($src,0,0,$out.Width,$out.Height)
for($y=0;$y -lt $rows;$y++){for($x=0;$x -lt $cols;$x++){ $gid=1+$x+$y*$cols; $px=$x*$cell*$scale; $py=$y*$cell*$scale; $g.FillRectangle([System.Drawing.Brushes]::Black,$px,$py,28,12); $g.DrawString([string]$gid,$font,[System.Drawing.Brushes]::White,$px,$py-1); $g.DrawRectangle([System.Drawing.Pens]::Yellow,$px,$py,$cell*$scale-1,$cell*$scale-1) }}
$font.Dispose();$g.Dispose();$out.Save((Join-Path $PSScriptRoot 'refactored_act03_tiles_labeled.png'),[System.Drawing.Imaging.ImageFormat]::Png);$out.Dispose();$src.Dispose()
