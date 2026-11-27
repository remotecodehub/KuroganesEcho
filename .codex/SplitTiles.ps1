param(
    [Parameter(Mandatory = $true)]
    [string]$InputImage,

    [string]$OutputDir = ".\tiles_16x16",

    [int]$Columns = 20,

    [int]$Rows = 15,

    [int]$TargetSize = 16,

    # Remove 1px de cada lado do recorte para evitar linhas pretas da grade. Use 0 para desativar.
    [int]$TrimBorder = 1
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    Write-Error "ImageMagick ('magick') nao encontrado no PATH."
    exit 1
}

if (-not (Test-Path -LiteralPath $InputImage)) {
    Write-Error "Arquivo nao encontrado: $InputImage"
    exit 1
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Obtem largura e altura reais da imagem
$dims = (& magick identify -format "%w %h" $InputImage).Trim().Split(" ")
$imgWidth  = [int]$dims[0]
$imgHeight = [int]$dims[1]

$cellW = $imgWidth / $Columns
$cellH = $imgHeight / $Rows

$total = $Columns * $Rows
$index = 0

Write-Host "Imagem: ${imgWidth}x${imgHeight} | Grade: ${Columns}x${Rows} | Celula aprox: $([Math]::Round($cellW, 2))x$([Math]::Round($cellH, 2))"

for ($r = 0; $r -lt $Rows; $r++) {
    $y1 = [int][Math]::Round($r * $cellH) + $TrimBorder
    $y2 = [int][Math]::Round(($r + 1) * $cellH) - $TrimBorder
    $cropH = [Math]::Max(1, $y2 - $y1)

    for ($c = 0; $c -lt $Columns; $c++) {
        $x1 = [int][Math]::Round($c * $cellW) + $TrimBorder
        $x2 = [int][Math]::Round(($c + 1) * $cellW) - $TrimBorder
        $cropW = [Math]::Max(1, $x2 - $x1)

        $fileName = "tile_r{0:D2}_c{1:D2}_{2:D3}.png" -f $r, $c, $index
        $outPath  = Join-Path $OutputDir $fileName

        # Recorta a celula (mesmo nao sendo 16x16) e força a saida exata em 16x16 sem blur
        & magick $InputImage `
            -crop "${cropW}x${cropH}+${x1}+${y1}" `
            +repage `
            -filter Point `
            -resize "${TargetSize}x${TargetSize}!" `
            $outPath

        $index++
        if (($index % 20) -eq 0 -or $index -eq $total) {
            Write-Progress -Activity "Exportando tiles 16x16" -Status "$index / $total" -PercentComplete (($index / $total) * 100)
        }
    }
}

Write-Progress -Activity "Exportando tiles 16x16" -Completed
Write-Host "Concluido! $total tiles exportados para: $(Resolve-Path $OutputDir)"