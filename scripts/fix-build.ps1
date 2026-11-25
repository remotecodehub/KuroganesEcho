[CmdletBinding()]
param(
    [string]$Project,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipRun,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ApplicationArguments
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Project)) {
    $Project = Join-Path $PSScriptRoot '..\src\KuroganesEcho.DesktopGL\KuroganesEcho.DesktopGL.csproj'
}

$projectPath = (Resolve-Path -LiteralPath $Project).Path

Write-Host "Building $projectPath ($Configuration) with isolated build servers..."
& dotnet build $projectPath --configuration $Configuration --disable-build-servers --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if ($SkipRun) {
    exit 0
}

Write-Host 'Build succeeded. Starting DesktopGL without rebuilding...'
& dotnet run --project $projectPath --configuration $Configuration --no-build -- @ApplicationArguments
exit $LASTEXITCODE
