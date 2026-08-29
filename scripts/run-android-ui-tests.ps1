[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$AvdName,
    [switch]$KeepEmulatorRunning,
    [string]$TestFilter,
    [string]$AppiumServerUrl,
    [string]$AdbPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$originalLocation = Get-Location
$originalDeviceId = $env:SOFTBALL_ANDROID_DEVICE_ID
$originalAppiumServerUrl = $env:SOFTBALL_APPIUM_SERVER_URL
$originalAdbPath = $env:SOFTBALL_ADB_PATH
$originalRunAndroidUiTests = $env:SOFTBALL_RUN_ANDROID_UI_TESTS
$exitCode = 0
$startedEmulator = $false
$selectedEmulatorId = $null
$startedEmulatorProcess = $null

function Write-Stage {
    param([string]$Message)

    Write-Host "[Android UI] $Message"
}

function Resolve-RequiredCommand {
    param(
        [string]$Name,
        [string]$RequestedCommand
    )

    $command = Get-Command -Name $RequestedCommand -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "Required CLI tool '$Name' was not found. Ensure it is available on PATH or supply its configured path."
    }

    return $command.Source
}

function Invoke-RequiredCommand {
    param(
        [string]$Description,
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        $commandExitCode = $LASTEXITCODE
        $exception = [InvalidOperationException]::new("$Description failed with exit code $commandExitCode.")
        $exception.Data['ExitCode'] = $commandExitCode
        throw $exception
    }
}

function Get-AdbDeviceStates {
    param([string]$AdbExecutable)

    $deviceOutput = & $AdbExecutable devices
    if ($LASTEXITCODE -ne 0) {
        throw "adb device detection failed with exit code $LASTEXITCODE."
    }

    $deviceStates = @{}
    $deviceOutput | ForEach-Object {
        if ($_ -match '^([^\s]+)\s+([^\s]+)(?:\s|$)') {
            $deviceStates[$Matches[1]] = $Matches[2]
        }
    }

    return $deviceStates
}

function Get-UsableDeviceIds {
    param([hashtable]$DeviceStates)

    return @($DeviceStates.Keys | Where-Object { $DeviceStates[$_] -eq 'device' } | Sort-Object)
}

function Get-EmulatorAvdName {
    param(
        [string]$AdbExecutable,
        [string]$EmulatorId
    )

    $avdOutput = & $AdbExecutable -s $EmulatorId emu avd name 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return $avdOutput |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -ne 'OK' } |
        Select-Object -First 1
}

function Wait-ForEmulatorShutdown {
    param(
        [string]$AdbExecutable,
        [string]$EmulatorId,
        [System.Diagnostics.Process]$EmulatorProcess,
        [int]$TimeoutSeconds = 60
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $consecutiveAbsentChecks = 0
    Write-Stage "Waiting up to $TimeoutSeconds seconds for $EmulatorId to shut down completely..."

    while ([DateTime]::UtcNow -lt $deadline) {
        $deviceStates = Get-AdbDeviceStates -AdbExecutable $AdbExecutable
        if ($deviceStates.ContainsKey($EmulatorId)) {
            $consecutiveAbsentChecks = 0
        }
        else {
            $consecutiveAbsentChecks++
        }

        $emulatorProcessExited = $true
        if ($null -ne $EmulatorProcess) {
            $EmulatorProcess.Refresh()
            $emulatorProcessExited = $EmulatorProcess.HasExited
        }

        if ($consecutiveAbsentChecks -ge 2 -and $emulatorProcessExited) {
            Write-Stage "Emulator shutdown completed: $EmulatorId"
            return
        }

        Start-Sleep -Seconds 1
    }

    $finalDeviceStates = Get-AdbDeviceStates -AdbExecutable $AdbExecutable
    $adbState = if ($finalDeviceStates.ContainsKey($EmulatorId)) {
        $finalDeviceStates[$EmulatorId]
    }
    else {
        'absent'
    }
    $processState = if ($null -eq $EmulatorProcess -or $EmulatorProcess.HasExited) {
        'exited'
    }
    else {
        "still running (PID $($EmulatorProcess.Id))"
    }

    throw "Emulator '$EmulatorId' did not shut down completely within $TimeoutSeconds seconds (adb state: $adbState; runner-owned process: $processState). Do not delete AVD lock files while an emulator process is still running."
}

function Start-ConfiguredEmulator {
    param(
        [string]$AdbExecutable,
        [string]$EmulatorExecutable,
        [string]$RequestedAvdName,
        [string[]]$ExistingDeviceIds,
        [int]$TimeoutSeconds = 180
    )

    Write-Stage "Checking configured Android test AVD: $RequestedAvdName"
    $avdOutput = & $EmulatorExecutable -list-avds
    if ($LASTEXITCODE -ne 0) {
        throw "Android AVD discovery failed with exit code $LASTEXITCODE."
    }
    $availableAvds = @($avdOutput | ForEach-Object { $_.Trim() } | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and $_ -match '^[A-Za-z0-9._-]+$'
    })
    if ($RequestedAvdName -notin $availableAvds) {
        $available = if ($availableAvds.Count -eq 0) { 'none' } else { $availableAvds -join ', ' }
        throw "Android AVD '$RequestedAvdName' was not found. Available AVDs: $available. Use -AvdName or SOFTBALL_ANDROID_AVD to select one."
    }

    Write-Stage "Starting Android test AVD: $RequestedAvdName"
    $emulatorProcess = Start-Process -FilePath $EmulatorExecutable -ArgumentList @('-avd', $RequestedAvdName) -PassThru
    $script:startedEmulator = $true
    $script:startedEmulatorProcess = $emulatorProcess
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

    Write-Stage "Waiting up to $TimeoutSeconds seconds for the emulator to become an adb device..."
    while ([DateTime]::UtcNow -lt $deadline) {
        $deviceStates = Get-AdbDeviceStates -AdbExecutable $AdbExecutable
        $candidateIds = @(Get-UsableDeviceIds -DeviceStates $deviceStates | Where-Object {
            $_ -match '^emulator-\d+$' -and $_ -notin $ExistingDeviceIds
        })
        $matchingIds = @($candidateIds | Where-Object {
            (Get-EmulatorAvdName -AdbExecutable $AdbExecutable -EmulatorId $_) -eq $RequestedAvdName
        })

        if ($matchingIds.Count -eq 1) {
            $emulatorId = $matchingIds[0]
            $script:selectedEmulatorId = $emulatorId
            Write-Stage "Emulator is connected as $emulatorId. Waiting for Android boot completion..."
            while ([DateTime]::UtcNow -lt $deadline) {
                $bootCompleted = & $AdbExecutable -s $emulatorId shell getprop sys.boot_completed 2>$null
                if ($LASTEXITCODE -eq 0 -and ($bootCompleted -join '').Trim() -eq '1') {
                    Write-Stage "Android boot completed on $emulatorId. Waiting for package manager readiness..."
                    while ([DateTime]::UtcNow -lt $deadline) {
                        $packageManagerOutput = & $AdbExecutable -s $emulatorId shell pm path android 2>$null
                        if ($LASTEXITCODE -eq 0 -and ($packageManagerOutput -join "`n") -match '(?m)^package:') {
                            Write-Stage "Android package manager is ready on $emulatorId."
                            return $emulatorId
                        }
                        if ($emulatorProcess.HasExited) {
                            throw "Android emulator '$RequestedAvdName' exited while waiting for package manager readiness (exit code $($emulatorProcess.ExitCode))."
                        }

                        Start-Sleep -Seconds 2
                    }

                    throw "Android emulator '$RequestedAvdName' completed boot as '$emulatorId', but its package manager did not become ready within $TimeoutSeconds seconds."
                }

                Start-Sleep -Seconds 2
            }

            throw "Android emulator '$RequestedAvdName' connected as '$emulatorId' but did not complete boot within $TimeoutSeconds seconds."
        }
        if ($matchingIds.Count -gt 1) {
            throw "Multiple newly connected emulators report AVD '$RequestedAvdName': $($matchingIds -join ', ')."
        }
        if ($emulatorProcess.HasExited) {
            throw "Android emulator '$RequestedAvdName' exited before becoming usable (exit code $($emulatorProcess.ExitCode))."
        }

        Start-Sleep -Seconds 2
    }

    throw "Android emulator '$RequestedAvdName' did not become a usable adb device within $TimeoutSeconds seconds."
}

try {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    Set-Location -LiteralPath $repositoryRoot

    Write-Stage "Repository: $repositoryRoot"
    $dotnetExecutable = Resolve-RequiredCommand -Name 'dotnet' -RequestedCommand 'dotnet'

    if ([string]::IsNullOrWhiteSpace($AdbPath)) {
        $AdbPath = if ([string]::IsNullOrWhiteSpace($env:SOFTBALL_ADB_PATH)) {
            'adb'
        }
        else {
            $env:SOFTBALL_ADB_PATH.Trim()
        }
    }
    $adbExecutable = Resolve-RequiredCommand -Name 'adb' -RequestedCommand $AdbPath

    Write-Stage 'Detecting connected Android devices...'
    $initialDeviceStates = Get-AdbDeviceStates -AdbExecutable $adbExecutable
    $usableDeviceIds = @(Get-UsableDeviceIds -DeviceStates $initialDeviceStates)

    if ([string]::IsNullOrWhiteSpace($DeviceId)) {
        $DeviceId = $env:SOFTBALL_ANDROID_DEVICE_ID
    }
    if (-not [string]::IsNullOrWhiteSpace($DeviceId)) {
        $DeviceId = $DeviceId.Trim()
        if ($DeviceId -notin $usableDeviceIds) {
            $available = if ($usableDeviceIds.Count -eq 0) { 'none' } else { $usableDeviceIds -join ', ' }
            throw "Android device '$DeviceId' is not connected and usable. Available devices: $available."
        }
    }
    elseif ($usableDeviceIds.Count -eq 1) {
        $DeviceId = $usableDeviceIds[0]
    }
    elseif ($usableDeviceIds.Count -eq 0) {
        if ([string]::IsNullOrWhiteSpace($AvdName)) {
            $AvdName = if ([string]::IsNullOrWhiteSpace($env:SOFTBALL_ANDROID_AVD)) {
                'softball_tablet_api_35'
            }
            else {
                $env:SOFTBALL_ANDROID_AVD.Trim()
            }
        }
        else {
            $AvdName = $AvdName.Trim()
        }

        $emulatorExecutable = Resolve-RequiredCommand -Name 'emulator' -RequestedCommand 'emulator'
        $DeviceId = Start-ConfiguredEmulator `
            -AdbExecutable $adbExecutable `
            -EmulatorExecutable $emulatorExecutable `
            -RequestedAvdName $AvdName `
            -ExistingDeviceIds @($initialDeviceStates.Keys)
    }
    else {
        throw "Multiple usable Android devices are connected: $($usableDeviceIds -join ', '). Specify one with -DeviceId or SOFTBALL_ANDROID_DEVICE_ID."
    }

    Write-Stage "Using device: $DeviceId"

    if ([string]::IsNullOrWhiteSpace($AppiumServerUrl)) {
        $AppiumServerUrl = if ([string]::IsNullOrWhiteSpace($env:SOFTBALL_APPIUM_SERVER_URL)) {
            'http://127.0.0.1:4723/'
        }
        else {
            $env:SOFTBALL_APPIUM_SERVER_URL.Trim()
        }
    }
    $parsedAppiumUri = $null
    if (-not [Uri]::TryCreate($AppiumServerUrl, [UriKind]::Absolute, [ref]$parsedAppiumUri)) {
        throw "Appium server URL '$AppiumServerUrl' is not an absolute URL."
    }

    $appiumStatusUrl = "$($parsedAppiumUri.AbsoluteUri.TrimEnd('/'))/status"
    Write-Stage "Checking Appium: $appiumStatusUrl"
    try {
        Invoke-RestMethod -Uri $appiumStatusUrl -Method Get -TimeoutSec 5 -UseBasicParsing | Out-Null
    }
    catch {
        throw "Appium is not reachable at '$AppiumServerUrl'. Start it in a separate terminal with 'appium'. $($_.Exception.Message)"
    }

    $appProject = Join-Path $repositoryRoot 'SoftballScorekeeper\SoftballScorekeeper.csproj'
    Write-Stage 'Building embedded-assembly Android APK...'
    Invoke-RequiredCommand -Description 'Android build' -FilePath $dotnetExecutable -Arguments @(
        'build',
        $appProject,
        '--framework', 'net9.0-android',
        '--no-restore',
        '--no-incremental',
        '--property:EmbedAssembliesIntoApk=true',
        '--maxcpucount:1'
    )

    $apkPath = Join-Path $repositoryRoot 'SoftballScorekeeper\bin\Debug\net9.0-android\com.companyname.softballscorekeeper-Signed.apk'
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) {
        throw "Android build succeeded but the expected APK was not found at '$apkPath'."
    }

    Write-Stage "Installing APK on $DeviceId..."
    $installOutput = & $adbExecutable -s $DeviceId install -r $apkPath
    $installExitCode = $LASTEXITCODE
    $installOutput | ForEach-Object { Write-Host $_ }
    if ($installExitCode -ne 0 -or ($installOutput -join "`n") -notmatch '(?m)^Success\s*$') {
        $exception = [InvalidOperationException]::new("APK installation failed with exit code $installExitCode.")
        $exception.Data['ExitCode'] = if ($installExitCode -eq 0) { 1 } else { $installExitCode }
        throw $exception
    }

    $env:SOFTBALL_ANDROID_DEVICE_ID = $DeviceId
    $env:SOFTBALL_APPIUM_SERVER_URL = $parsedAppiumUri.AbsoluteUri
    $env:SOFTBALL_ADB_PATH = $adbExecutable
    $env:SOFTBALL_RUN_ANDROID_UI_TESTS = '1'

    $testProject = Join-Path $repositoryRoot 'SoftballScorekeeper.AndroidUiTests\SoftballScorekeeper.AndroidUiTests.csproj'
    $testArguments = @('test', $testProject)
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $testArguments += @('--filter', $TestFilter)
        Write-Stage "Running Android UI tests with filter: $TestFilter"
    }
    else {
        Write-Stage 'Running the complete Android UI test suite...'
    }
    Invoke-RequiredCommand -Description 'Android UI tests' -FilePath $dotnetExecutable -Arguments $testArguments

    Write-Stage 'Build, install, and Android UI tests succeeded.'
}
catch {
    $caughtException = $_.Exception
    $exitCode = if ($caughtException.Data.Contains('ExitCode')) {
        [int]$caughtException.Data['ExitCode']
    }
    else {
        1
    }
    Write-Error "[Android UI] $($caughtException.Message)" -ErrorAction Continue
}
finally {
    if ($startedEmulator -and -not $KeepEmulatorRunning -and -not [string]::IsNullOrWhiteSpace($selectedEmulatorId)) {
        try {
            $cleanupDeviceStates = Get-AdbDeviceStates -AdbExecutable $adbExecutable
            if ($cleanupDeviceStates.ContainsKey($selectedEmulatorId)) {
                Write-Stage "Shutting down emulator started by this script: $selectedEmulatorId"
                & $adbExecutable -s $selectedEmulatorId emu kill | ForEach-Object { Write-Host $_ }
                if ($LASTEXITCODE -ne 0) {
                    throw "adb emulator shutdown failed with exit code $LASTEXITCODE."
                }
            }
            else {
                Write-Stage "Runner-owned emulator is already absent from adb: $selectedEmulatorId"
            }

            Wait-ForEmulatorShutdown `
                -AdbExecutable $adbExecutable `
                -EmulatorId $selectedEmulatorId `
                -EmulatorProcess $startedEmulatorProcess
        }
        catch {
            Write-Warning "[Android UI] Emulator cleanup failed: $($_.Exception.Message)"
            if ($exitCode -eq 0) {
                $exitCode = 1
            }
        }
    }
    elseif ($startedEmulator -and -not $KeepEmulatorRunning -and $null -ne $startedEmulatorProcess -and -not $startedEmulatorProcess.HasExited) {
        try {
            Write-Stage 'Stopping the emulator process because no adb serial became available.'
            if ($env:OS -eq 'Windows_NT') {
                & taskkill.exe /PID $startedEmulatorProcess.Id /T /F | ForEach-Object { Write-Host $_ }
                if ($LASTEXITCODE -ne 0) {
                    throw "Emulator process-tree cleanup failed with exit code $LASTEXITCODE."
                }
            }
            else {
                Stop-Process -Id $startedEmulatorProcess.Id -Force -ErrorAction Stop
            }
        }
        catch {
            Write-Warning "[Android UI] Emulator process cleanup failed: $($_.Exception.Message)"
            if ($exitCode -eq 0) {
                $exitCode = 1
            }
        }
    }
    elseif ($startedEmulator -and $KeepEmulatorRunning) {
        Write-Stage "Leaving emulator started by this script running: $selectedEmulatorId"
    }

    $env:SOFTBALL_ANDROID_DEVICE_ID = $originalDeviceId
    $env:SOFTBALL_APPIUM_SERVER_URL = $originalAppiumServerUrl
    $env:SOFTBALL_ADB_PATH = $originalAdbPath
    $env:SOFTBALL_RUN_ANDROID_UI_TESTS = $originalRunAndroidUiTests
    Set-Location -LiteralPath $originalLocation
}

exit $exitCode
