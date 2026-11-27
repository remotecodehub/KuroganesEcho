[CmdletBinding()]
param(
    [Alias("d")]
    [switch]$desktop,
    [Alias("a")]
    [switch]$android,
    [Alias("i")]
    [switch]$ios,
    [ValidateSet('Debug', 'Release')]
    [Alias("c")]
    [string]$Configuration = 'Debug',
    [switch]$SkipRun,
    [string[]]$RunArguments,
    [ValidateSet('Auto', 'Usb', 'Wifi')]
    [string]$AndroidTransport = 'Auto',
    [string]$AndroidDevice,
    [string]$AndroidWifiAddress,
    [int]$AndroidWifiPort = 5555,
    [string]$MacHost,
    [string]$MacUser,
    [string]$MacRemoteRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$androidPackageName = 'com.remotecodehub.kuroganesecho'
$iosBundleIdentifier = 'com.remotecodehub.kuroganesecho'

function Import-RepositoryDotEnv {
    $dotenvPath = Join-Path $repoRoot '.env'
    if (-not (Test-Path -LiteralPath $dotenvPath)) {
        return
    }

    foreach ($line in Get-Content -LiteralPath $dotenvPath) {
        if ($line -match '^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') {
            $name = $matches[1]
            $value = $matches[2].Trim()
            if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
            Set-Item -Path "Env:$name" -Value $value
        }
    }
}

Import-RepositoryDotEnv

function Get-ToolPath {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string[]]$Fallbacks = @()
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        return $command.Source
    }

    foreach ($fallback in $Fallbacks) {
        if ($fallback -and (Test-Path -LiteralPath $fallback)) {
            return $fallback
        }
    }

    return $null
}

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Description
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description falhou com exit code $LASTEXITCODE."
    }
}

function Find-VisualStudioCommunity {
    $knownPaths = @(
        'E:\Program Files\Microsoft Visual Studio\2026\Community\Common7\IDE\devenv.exe',
        'E:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe',
        'E:\Program Files\Microsoft Visual Studio\Community\Common7\IDE\devenv.exe',
        'E:\Program Files (x86)\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe'
    )

    foreach ($knownPath in $knownPaths) {
        if (Test-Path -LiteralPath $knownPath) {
            return $knownPath
        }
    }

    $visualStudioRoot = 'E:\Program Files\Microsoft Visual Studio'
    if (Test-Path -LiteralPath $visualStudioRoot) {
        $pathMatch = @(Get-ChildItem -Path "$visualStudioRoot\*\Community\Common7\IDE\devenv.exe" -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1)
        if ($pathMatch.Count -gt 0) {
            return $pathMatch[0].FullName
        }
    }

    $vswhere = 'E:\Program Files\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installationPath = (& $vswhere -products Microsoft.VisualStudio.Product.Community -latest -property installationPath 2>$null | Select-Object -First 1).Trim()
        if ($installationPath) {
            $devenv = Join-Path $installationPath 'Common7\IDE\devenv.exe'
            if (Test-Path -LiteralPath $devenv) {
                return $devenv
            }
        }
    }

    return $null
}

function Get-AdbPath {
    $fallbacks = @(
        (Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'),
        (Join-Path $env:ANDROID_SDK_ROOT 'platform-tools\adb.exe'),
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'),
        'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
    ) | Where-Object { $_ }

    $adbPath = Get-ToolPath -Name 'adb' -Fallbacks $fallbacks
    if (-not $adbPath) {
        throw 'ADB nao foi encontrado no PATH nem no Android SDK.'
    }

    return $adbPath
}

function Get-AdbDevices {
    param([Parameter(Mandatory)][string]$AdbPath)

    $raw = @(& $AdbPath devices -l)
    if ($LASTEXITCODE -ne 0) {
        throw 'Nao foi possivel consultar os dispositivos ADB.'
    }

    $devices = @()
    foreach ($line in $raw) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^List of devices attached' -or $line -match '^\* daemon') {
            continue
        }

        $parts = $line -split '\s+'
        if ($parts.Count -lt 2) {
            continue
        }

        $serial = $parts[0]
        $transport = if ($serial -match '^\d{1,3}(?:\.\d{1,3}){3}:\d+$') {
            'Wifi'
        } elseif ($serial -match '^emulator-') {
            'Emulator'
        } else {
            'Usb'
        }

        $model = 'unknown'
        if ($line -match 'model:([^\s]+)') {
            $model = $matches[1]
        }

        $devices += [PSCustomObject]@{
            Serial    = $serial
            State     = $parts[1]
            Transport = $transport
            Model     = $model
        }
    }

    return @($devices)
}

function Connect-AdbWifi {
    param([Parameter(Mandatory)][string]$AdbPath)

    if ([string]::IsNullOrWhiteSpace($AndroidWifiAddress)) {
        return
    }

    $endpoint = if ($AndroidWifiAddress -match ':\d+$') {
        $AndroidWifiAddress
    } else {
        "$AndroidWifiAddress`:$AndroidWifiPort"
    }

    Write-Host "Conectando ao Android por Wi-Fi em $endpoint..."
    $result = @(& $AdbPath connect $endpoint)
    if ($LASTEXITCODE -ne 0 -or ($result -join ' ') -notmatch '(?i)connected|already connected') {
        throw "ADB nao conseguiu conectar em $endpoint."
    }
}

function Resolve-AdbDevice {
    param([Parameter(Mandatory)][string]$AdbPath)

    if ($AndroidTransport -eq 'Wifi') {
        Connect-AdbWifi -AdbPath $AdbPath
    }

    $devices = @(Get-AdbDevices -AdbPath $AdbPath | Where-Object { $_.State -eq 'device' })
    if ($AndroidDevice) {
        $selected = $devices | Where-Object { $_.Serial -eq $AndroidDevice } | Select-Object -First 1
        if (-not $selected) {
            throw "O dispositivo ADB '$AndroidDevice' nao esta conectado ou autorizado."
        }
        return $selected
    }

    $candidates = switch ($AndroidTransport) {
        'Usb'  { @($devices | Where-Object { $_.Transport -eq 'Usb' }) }
        'Wifi' { @($devices | Where-Object { $_.Transport -eq 'Wifi' }) }
        default { @($devices | Where-Object { $_.Transport -in @('Usb', 'Wifi') }) }
    }

    if ($candidates.Count -eq 0) {
        throw "Nenhum dispositivo Android ativo para o transporte '$AndroidTransport'."
    }

    if ($candidates.Count -gt 1) {
        $available = ($candidates | ForEach-Object { "$($_.Serial) [$($_.Transport), $($_.Model)]" }) -join ', '
        throw "Mais de um dispositivo corresponde a '$AndroidTransport': $available. Informe -AndroidDevice."
    }

    return $candidates[0]
}

function Publish-Android {
    param([Parameter(Mandatory)][string]$ProjectPath)

    $publishArguments = @(
        $ProjectPath,
        '--configuration', $Configuration,
        '--framework', 'net11.0-android',
        '--disable-build-servers',
        '--nologo',
        '-p:AndroidPackageFormats=apk'
    )

    Write-Host "Publicando APK Android ($Configuration)..."
    & dotnet publish @publishArguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Publish Android falhou com exit code $LASTEXITCODE."
    }

    $apkRoot = Join-Path $repoRoot "src\KuroganesEcho.Android\bin\$Configuration\net11.0-android"
    $apkCandidates = @(Get-ChildItem -LiteralPath $apkRoot -Filter '*-Signed.apk' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
    if ($apkCandidates.Count -eq 0) {
        $apkCandidates = @(Get-ChildItem -LiteralPath $apkRoot -Filter '*.apk' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
    }

    if ($apkCandidates.Count -eq 0) {
        throw "O publish terminou, mas nenhum APK foi encontrado em $apkRoot."
    }

    return $apkCandidates[0]
}

function Install-And-RunAndroid {
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)]$Device,
        [Parameter(Mandatory)][System.IO.FileInfo]$Apk
    )

    Write-Host "Instalando $($Apk.Name) em $($Device.Serial) via $($Device.Transport)..."
    & $AdbPath -s $Device.Serial install -r -g $Apk.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Instalacao Android falhou no dispositivo $($Device.Serial)."
    }

    & $AdbPath -s $Device.Serial shell am force-stop $androidPackageName
    & $AdbPath -s $Device.Serial shell monkey -p $androidPackageName -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel iniciar $androidPackageName no dispositivo $($Device.Serial)."
    }
}

function Build-RemoteIos {
    $visualStudioCommunity = Find-VisualStudioCommunity
    if ($visualStudioCommunity) {
        Write-Host "Visual Studio Community detectado: $visualStudioCommunity"
    } else {
        Write-Warning 'Visual Studio Community nao foi encontrado em E:\Program Files; o build remoto continuara via SSH.'
    }

    $sshPath = Get-ToolPath -Name 'ssh' -Fallbacks @('C:\Windows\System32\OpenSSH\ssh.exe')
    $scpPath = Get-ToolPath -Name 'scp' -Fallbacks @('C:\Windows\System32\OpenSSH\scp.exe')
    $plinkPath = Get-ToolPath -Name 'plink' -Fallbacks @('C:\Program Files\PuTTY\plink.exe')
    $pscpPath = Get-ToolPath -Name 'pscp' -Fallbacks @('C:\Program Files\PuTTY\pscp.exe')
    $tarPath = if (Test-Path -LiteralPath 'C:\Windows\System32\tar.exe') {
        'C:\Windows\System32\tar.exe'
    } else {
        Get-ToolPath -Name 'tar'
    }

    if (-not $sshPath -or -not $scpPath -or -not $tarPath) {
        throw 'O fluxo iOS remoto requer ssh.exe, scp.exe e tar.exe no Windows.'
    }

    if ([string]::IsNullOrWhiteSpace($MacHost)) { $MacHost = $env:MACOS_ADDR }
    if ([string]::IsNullOrWhiteSpace($MacUser)) { $MacUser = $env:MACOS_USER }
    $macPassword = $env:MACOS_PASS

    if ([string]::IsNullOrWhiteSpace($MacHost)) {
        $MacHost = Read-Host 'IP ou hostname do host macOS na rede local'
    }
    if ([string]::IsNullOrWhiteSpace($MacUser)) {
        $MacUser = Read-Host 'Usuario SSH do host macOS'
    }
    if ([string]::IsNullOrWhiteSpace($MacHost) -or [string]::IsNullOrWhiteSpace($MacUser) -or $MacHost -match '\s' -or $MacUser -match '\s') {
        throw 'IP/hostname e usuario SSH do macOS sao obrigatorios e nao podem conter espacos.'
    }

    $sshOptions = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'PreferredAuthentications=publickey,password,keyboard-interactive')
    $endpoint = "$MacUser@$MacHost"
    $puttyHostKey = $env:MACOS_HOSTKEY
    $usePutty = -not [string]::IsNullOrWhiteSpace($macPassword) -and
        -not [string]::IsNullOrWhiteSpace($puttyHostKey) -and
        $plinkPath -and $pscpPath

    if ($usePutty) {
        Write-Host 'Credenciais MACOS_ADDR/MACOS_USER/MACOS_PASS carregadas do .env; a senha nao sera exibida.'
    } elseif (-not [string]::IsNullOrWhiteSpace($macPassword)) {
        Write-Warning 'MACOS_PASS foi encontrado, mas MACOS_HOSTKEY nao. Por seguranca, a senha nao sera passada ao processo; o OpenSSH solicitara a senha interativamente.'
    } else {
        Write-Host 'MACOS_PASS nao esta disponivel com plink/pscp; o OpenSSH solicitara a senha interativamente.'
    }

    function Invoke-MacSsh {
        param([Parameter(Mandatory)][string]$Command)

        if ($usePutty) {
            & $plinkPath -ssh -batch -hostkey $puttyHostKey -pw $macPassword $endpoint $Command
        } else {
            & $sshPath @sshOptions $endpoint $Command
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Comando remoto no macOS falhou com exit code $LASTEXITCODE."
        }
    }

    function Copy-ToMac {
        param([Parameter(Mandatory)][string]$LocalPath, [Parameter(Mandatory)][string]$RemotePath)

        if ($usePutty) {
            & $pscpPath -batch -hostkey $puttyHostKey -pw $macPassword $LocalPath "${endpoint}:$RemotePath"
        } else {
            & $scpPath @sshOptions $LocalPath "${endpoint}:$RemotePath"
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Envio para o macOS falhou com exit code $LASTEXITCODE."
        }
    }

    function Copy-FromMac {
        param([Parameter(Mandatory)][string]$RemotePath, [Parameter(Mandatory)][string]$LocalPath)

        if ($usePutty) {
            & $pscpPath -batch -hostkey $puttyHostKey -pw $macPassword "${endpoint}:$RemotePath" $LocalPath
        } else {
            & $scpPath @sshOptions "${endpoint}:$RemotePath" $LocalPath
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Download do macOS falhou com exit code $LASTEXITCODE."
        }
    }

    $sessionId = [Guid]::NewGuid().ToString('N')
    if ([string]::IsNullOrWhiteSpace($MacRemoteRoot)) {
        $MacRemoteRoot = "/tmp/kuroganes-echo-$sessionId"
    }
    if ($MacRemoteRoot -notmatch '^/tmp/kuroganes-echo-[A-Za-z0-9_-]+$') {
        throw 'MacRemoteRoot deve ser um caminho temporario no formato /tmp/kuroganes-echo-<id>.'
    }

    $remoteArchive = "$MacRemoteRoot/source.tar.gz"
    $localArchive = Join-Path ([System.IO.Path]::GetTempPath()) "kuroganes-echo-$sessionId.tar.gz"

    try {
        $tarArguments = @(
            '-czf', $localArchive,
            '-C', $repoRoot,
            '--exclude=.git', '--exclude=.vs', '--exclude=.codex',
            '--exclude=bin', '--exclude=obj', '--exclude=artifacts',
            '--exclude=.tmp-array-fonts', '.'
        )
        Invoke-CheckedNative -FilePath $tarPath -Arguments $tarArguments -Description 'Empacotamento da fonte para o macOS'

        Invoke-MacSsh "mkdir -p '$MacRemoteRoot'"
        Copy-ToMac -LocalPath $localArchive -RemotePath $remoteArchive
        Invoke-MacSsh "tar -xzf '$remoteArchive' -C '$MacRemoteRoot'"

        $remoteArchitecture = ((Invoke-MacSsh 'uname -m') -join '').Trim()
        $iosRuntime = if ($remoteArchitecture -eq 'arm64') { 'iossimulator-arm64' } else { 'iossimulator-x64' }
        Write-Host "Host macOS detectado como $remoteArchitecture; usando $iosRuntime."

        $remoteProject = "$MacRemoteRoot/src/KuroganesEcho.iOS/KuroganesEcho.iOS.csproj"
        $buildCommand = @'
cd '__REMOTE_ROOT__'
DOTNET_BIN=$(command -v dotnet || true)
if [ -z "$DOTNET_BIN" ] && [ -x /usr/local/share/dotnet/dotnet ]; then DOTNET_BIN=/usr/local/share/dotnet/dotnet; fi
if [ -z "$DOTNET_BIN" ] && [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET_BIN="$HOME/.dotnet/dotnet"; fi
if [ -z "$DOTNET_BIN" ]; then echo "dotnet nao foi encontrado no PATH nem nos caminhos padrao"; exit 127; fi
export DOTNET_ROOT="$(dirname "$DOTNET_BIN")"
export PATH="$DOTNET_ROOT:$PATH"
"$DOTNET_BIN" restore '__REMOTE_PROJECT__' --runtime '__IOS_RUNTIME__' --disable-parallel
"$DOTNET_BIN" build '__REMOTE_PROJECT__' --configuration '__CONFIGURATION__' --framework net11.0-ios --runtime '__IOS_RUNTIME__' --disable-build-servers -p:EnableCodeSigning=false --nologo
'@
        $buildCommand = $buildCommand.Replace('__REMOTE_ROOT__', $MacRemoteRoot).Replace('__REMOTE_PROJECT__', $remoteProject).Replace('__IOS_RUNTIME__', $iosRuntime).Replace('__CONFIGURATION__', $Configuration)
        Invoke-MacSsh $buildCommand

        $remoteAppDirectory = "$MacRemoteRoot/src/KuroganesEcho.iOS/bin/$Configuration/net11.0-ios/$iosRuntime"
        $remoteOutputArchive = "$MacRemoteRoot/KuroganesEcho-$iosRuntime.tar.gz"
        Invoke-MacSsh "test -d '$remoteAppDirectory/KuroganesEcho.iOS.app' && tar -czf '$remoteOutputArchive' -C '$remoteAppDirectory' KuroganesEcho.iOS.app"

        $localOutputDirectory = Join-Path $repoRoot "artifacts\ios-remote\$iosRuntime"
        New-Item -ItemType Directory -Force -Path $localOutputDirectory | Out-Null
        $localOutputArchive = Join-Path $localOutputDirectory "KuroganesEcho-$iosRuntime.tar.gz"
        Copy-FromMac -RemotePath $remoteOutputArchive -LocalPath $localOutputArchive
        Invoke-CheckedNative -FilePath $tarPath -Arguments @('-xzf', $localOutputArchive, '-C', $localOutputDirectory) -Description 'Extracao do app iOS no Windows'

        $appPath = Get-ChildItem -LiteralPath $localOutputDirectory -Filter '*.app' -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $appPath) {
            throw "O build remoto terminou, mas nenhum .app foi encontrado em $localOutputDirectory."
        }

        if (-not $SkipRun) {
            $simulatorCommand = 'open -a Simulator 2>/dev/null || true; device=$(xcrun simctl list devices available | awk -F "[()]" ''/Booted/ {print $2; exit}''); if [ -z "$device" ]; then device=$(xcrun simctl list devices available | awk -F "[()]" ''/iPhone/ {print $2; exit}''); if [ -n "$device" ]; then xcrun simctl boot "$device" || true; fi; fi; [ -n "$device" ] || { echo "No available iOS simulator found"; exit 1; }; xcrun simctl bootstatus "$device" -b; xcrun simctl install "$device" "__APP_PATH__"; xcrun simctl launch "$device" "__BUNDLE_ID__"'
            $simulatorCommand = $simulatorCommand.Replace('__APP_PATH__', "$remoteAppDirectory/KuroganesEcho.iOS.app").Replace('__BUNDLE_ID__', $iosBundleIdentifier)
            Invoke-MacSsh $simulatorCommand
            Write-Host 'Aplicativo iOS instalado/iniciado no simulador remoto do macOS.' -ForegroundColor Green
        }

        Write-Host "App iOS disponivel no Windows: $($appPath.FullName)" -ForegroundColor Green
    }
    finally {
        Remove-Item -LiteralPath $localArchive -Force -ErrorAction SilentlyContinue
    }
}

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

if ($target -eq 'android') {
    $apk = Publish-Android -ProjectPath $projectPath
    if ($SkipRun) {
        Write-Host "APK gerado: $($apk.FullName)" -ForegroundColor Green
        exit 0
    }

    $adbPath = Get-AdbPath
    $device = Resolve-AdbDevice -AdbPath $adbPath
    Install-And-RunAndroid -AdbPath $adbPath -Device $device -Apk $apk
    exit 0
}

if ($target -eq 'ios') {
    Build-RemoteIos
    exit 0
}

$buildArguments = @(
    $projectPath,
    '--configuration', $Configuration,
    '--framework', $targetInfo.Framework,
    '--disable-build-servers',
    '--nologo'
)

Write-Host "Building target '$target' ($($targetInfo.Framework))..."
& dotnet build @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Build do target '$target' falhou com exit code $LASTEXITCODE."
}

if ($SkipRun) {
    exit 0
}

$runArguments = @(
    '--project', $projectPath,
    '--configuration', $Configuration,
    '--framework', $targetInfo.Framework,
    '--no-build'
)
if ($RunArguments) {
    $runArguments += '--'
    $runArguments += $RunArguments
}

Write-Host "Build concluido. Executando target '$target' sem rebuild..."
& dotnet run @runArguments
exit $LASTEXITCODE
