[CmdletBinding()]
param(
    [switch]$desktop,
    [switch]$android,
    [switch]$ios,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipRun,
    [string[]]$RunArguments
)

$ErrorActionPreference = 'Stop'

$selectedTargets = @(
    if ($desktop) { 'desktop' }
    if ($android) { 'android' }
    if ($ios) { 'ios' }
)

if ($selectedTargets.Count -gt 1) {
    throw 'Escolha apenas um target: -desktop, -android ou -ios.'
}

$target = if ($selectedTargets.Count -eq 1) { $selectedTargets[0] } else { 'desktop' }
$targetInfo = switch ($target) {
    'desktop' {
        @{
            Project = Join-Path $PSScriptRoot '..\src\KuroganesEcho.DesktopGL\KuroganesEcho.DesktopGL.csproj'
            Framework = 'net11.0'
        }
    }
    'android' {
        @{
            Project = Join-Path $PSScriptRoot '..\src\KuroganesEcho.Android\KuroganesEcho.Android.csproj'
            Framework = 'net11.0-android'
        }
    }
    'ios' {
        @{
            Project = Join-Path $PSScriptRoot '..\src\KuroganesEcho.iOS\KuroganesEcho.iOS.csproj'
            Framework = 'net11.0-ios'
        }
    }
}

$projectPath = (Resolve-Path -LiteralPath $targetInfo.Project).Path

Write-Host "Building target '$target' ($($targetInfo.Framework))..."
& dotnet build $projectPath `
    --configuration $Configuration `
    --framework $targetInfo.Framework `
    --disable-build-servers `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Build do target '$target' falhou com exit code $LASTEXITCODE."
}

if ($SkipRun) {
    exit 0
}

Write-Host "Build concluído. Executando target '$target' sem rebuild..."
& dotnet run --project $projectPath `
    --configuration $Configuration `
    --framework $targetInfo.Framework `
    --no-build `
    -- @RunArguments

exit $LASTEXITCODE
