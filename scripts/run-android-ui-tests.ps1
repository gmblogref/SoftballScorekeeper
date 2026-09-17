[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$AvdName,
    [switch]$KeepEmulatorRunning,
    [string]$TestFilter,
    [string]$AppiumServerUrl,
    [string]$AppiumCommand = 'appium',
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
$startedAppium = $false
$startedAppiumProcess = $null
$appiumStandardOutputPath = $null
$appiumStandardErrorPath = $null
$defaultAndroidUiTestAvdName = 'softball_tablet_ui_tests_api_35'
$reservedDevelopmentAvdName = 'softball_tablet_api_35'

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

function Resolve-AppiumLaunch {
    param([string]$RequestedCommand)

    $command = Get-Command -Name $RequestedCommand -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "Appium was not found as '$RequestedCommand'. Install and configure Appium, ensure it is available on PATH, or supply -AppiumCommand."
    }

    if ($env:OS -eq 'Windows_NT' -and $command.Source.EndsWith('.cmd', [StringComparison]::OrdinalIgnoreCase)) {
        $appiumEntryPoint = Join-Path (Split-Path -Parent $command.Source) 'node_modules\appium\index.js'
        if (-not (Test-Path -LiteralPath $appiumEntryPoint -PathType Leaf)) {
            throw "The Appium npm wrapper '$($command.Source)' was found, but its Node entry point was not found at '$appiumEntryPoint'. Repair the existing Appium installation."
        }

        return [pscustomobject]@{
            FilePath = Resolve-RequiredCommand -Name 'node' -RequestedCommand 'node'
            PrefixArguments = @($appiumEntryPoint)
            Description = $command.Source
        }
    }

    return [pscustomobject]@{
        FilePath = $command.Source
        PrefixArguments = @()
        Description = $command.Source
    }
}

function Assert-UiAutomator2DriverInstalled {
    param([pscustomobject]$AppiumLaunch)

    $driverArguments = @($AppiumLaunch.PrefixArguments) + @('driver', 'list', '--installed', '--json')
    $driverOutput = & $AppiumLaunch.FilePath @driverArguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Appium was found at '$($AppiumLaunch.Description)', but installed-driver discovery failed. Ensure Appium and its extensions are usable. $($driverOutput -join ' ')"
    }

    try {
        $driverState = ($driverOutput -join "`n") | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "Appium returned an unreadable installed-driver response. Ensure the installed Appium CLI supports 'driver list --installed --json'. $($driverOutput -join ' ')"
    }

    $uiautomator2 = $driverState.PSObject.Properties['uiautomator2']
    if ($null -eq $uiautomator2 -or $uiautomator2.Value.installed -ne $true) {
        throw "Appium UiAutomator2 support is unavailable. Install the compatible driver with 'appium driver install uiautomator2' before running Android UI tests."
    }

    Write-Stage "Appium is available at '$($AppiumLaunch.Description)'; UiAutomator2 $($uiautomator2.Value.version) is installed."
}

function Get-AppiumStatus {
    param([string]$StatusUrl)

    try {
        $response = Invoke-RestMethod -Uri $StatusUrl -Method Get -TimeoutSec 2 -UseBasicParsing
    }
    catch {
        if ($null -ne $_.Exception.Response) {
            return [pscustomobject]@{ Kind = 'Conflict'; Detail = $_.Exception.Message }
        }
        return [pscustomobject]@{ Kind = 'Unavailable'; Detail = $_.Exception.Message }
    }

    $valueProperty = $response.PSObject.Properties['value']
    $readyProperty = if ($null -ne $valueProperty) {
        $valueProperty.Value.PSObject.Properties['ready']
    }
    else {
        $null
    }
    if ($null -ne $readyProperty -and $readyProperty.Value -is [bool] -and $readyProperty.Value) {
        return [pscustomobject]@{ Kind = 'Healthy'; Detail = $valueProperty.Value.message }
    }

    return [pscustomobject]@{
        Kind = 'Conflict'
        Detail = 'The endpoint responded, but it did not return a healthy Appium status payload.'
    }
}

function Get-AppiumLogTail {
    param([int]$LineCount = 40)

    $sections = @()
    if (-not [string]::IsNullOrWhiteSpace($script:appiumStandardOutputPath) -and
        (Test-Path -LiteralPath $script:appiumStandardOutputPath -PathType Leaf)) {
        $sections += "Appium stdout:`n$((Get-Content -LiteralPath $script:appiumStandardOutputPath -Tail $LineCount) -join "`n")"
    }
    if (-not [string]::IsNullOrWhiteSpace($script:appiumStandardErrorPath) -and
        (Test-Path -LiteralPath $script:appiumStandardErrorPath -PathType Leaf)) {
        $sections += "Appium stderr:`n$((Get-Content -LiteralPath $script:appiumStandardErrorPath -Tail $LineCount) -join "`n")"
    }
    return $sections -join "`n"
}

function Start-OwnedAppium {
    param(
        [pscustomobject]$AppiumLaunch,
        [Uri]$ServerUri,
        [string]$StatusUrl,
        [string]$RepositoryRoot,
        [int]$TimeoutSeconds = 30
    )

    if ($ServerUri.Scheme -ne 'http' -or
        $ServerUri.Host -notin @('127.0.0.1', 'localhost', '::1')) {
        throw "No healthy Appium server is available at '$($ServerUri.AbsoluteUri)', and the runner can only start and own a local HTTP Appium endpoint (127.0.0.1, localhost, or ::1)."
    }

    $logDirectory = Join-Path $RepositoryRoot 'SoftballScorekeeper.AndroidUiTests\TestResults\Appium'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $script:appiumStandardOutputPath = Join-Path $logDirectory "appium-$timestamp.stdout.log"
    $script:appiumStandardErrorPath = Join-Path $logDirectory "appium-$timestamp.stderr.log"

    $arguments = @($AppiumLaunch.PrefixArguments) + @(
        '--address', $ServerUri.Host,
        '--port', $ServerUri.Port.ToString()
    )
    $basePath = $ServerUri.AbsolutePath.TrimEnd('/')
    if (-not [string]::IsNullOrWhiteSpace($basePath)) {
        $arguments += @('--base-path', $basePath)
    }

    $startArguments = @($arguments | ForEach-Object {
        if ($_ -match '\s') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    })
    Write-Stage "Starting Appium at $($ServerUri.AbsoluteUri)..."
    $script:startedAppiumProcess = Start-Process `
        -FilePath $AppiumLaunch.FilePath `
        -ArgumentList $startArguments `
        -RedirectStandardOutput $script:appiumStandardOutputPath `
        -RedirectStandardError $script:appiumStandardErrorPath `
        -PassThru `
        -WindowStyle Hidden
    $script:startedAppium = $true
    Write-Stage "Waiting up to $TimeoutSeconds seconds for runner-owned Appium PID $($script:startedAppiumProcess.Id) to become ready..."

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $script:startedAppiumProcess.Refresh()
        if ($script:startedAppiumProcess.HasExited) {
            throw "Runner-owned Appium PID $($script:startedAppiumProcess.Id) exited before readiness with code $($script:startedAppiumProcess.ExitCode).`n$(Get-AppiumLogTail)"
        }

        $status = Get-AppiumStatus -StatusUrl $StatusUrl
        if ($status.Kind -eq 'Healthy') {
            Write-Stage "Runner-owned Appium is ready (PID $($script:startedAppiumProcess.Id))."
            return
        }
        if ($status.Kind -eq 'Conflict') {
            throw "Appium startup encountered a non-Appium or unhealthy service at '$StatusUrl'. The runner will not terminate the unknown service. $($status.Detail)`n$(Get-AppiumLogTail)"
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Runner-owned Appium PID $($script:startedAppiumProcess.Id) did not become ready within $TimeoutSeconds seconds.`n$(Get-AppiumLogTail)"
}

function Stop-OwnedAppium {
    param(
        [System.Diagnostics.Process]$AppiumProcess,
        [int]$TimeoutSeconds = 15
    )

    $AppiumProcess.Refresh()
    if ($AppiumProcess.HasExited) {
        Write-Stage "Runner-owned Appium PID $($AppiumProcess.Id) is already stopped."
        return
    }

    Write-Stage "Stopping runner-owned Appium PID $($AppiumProcess.Id)..."
    Stop-Process -Id $AppiumProcess.Id -ErrorAction Stop
    if (-not $AppiumProcess.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Runner-owned Appium PID $($AppiumProcess.Id) did not stop within $TimeoutSeconds seconds. No unrelated Node processes were terminated."
    }
    Write-Stage "Runner-owned Appium stopped: PID $($AppiumProcess.Id)"
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

function Assert-NotReservedDevelopmentAvd {
    param(
        [string]$RequestedAvdName,
        [string]$SelectionDescription
    )

    if ($RequestedAvdName.Equals($script:reservedDevelopmentAvdName, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Android AVD '$RequestedAvdName' is reserved for Visual Studio and manual development. $SelectionDescription must use a dedicated Android UI-test AVD, such as '$($script:defaultAndroidUiTestAvdName)'."
    }
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
    $appiumStatus = Get-AppiumStatus -StatusUrl $appiumStatusUrl
    if ($appiumStatus.Kind -eq 'Healthy') {
        Write-Stage 'Reusing healthy pre-existing Appium; this runner will not stop it.'
    }
    elseif ($appiumStatus.Kind -eq 'Conflict') {
        throw "The configured endpoint '$appiumStatusUrl' is occupied by a non-Appium or unhealthy service. The runner will not start Appium on that port or terminate the unknown service. $($appiumStatus.Detail)"
    }
    else {
        if ([string]::IsNullOrWhiteSpace($AppiumCommand)) {
            $AppiumCommand = 'appium'
        }
        $appiumLaunch = Resolve-AppiumLaunch -RequestedCommand $AppiumCommand.Trim()
        Assert-UiAutomator2DriverInstalled -AppiumLaunch $appiumLaunch
        Start-OwnedAppium `
            -AppiumLaunch $appiumLaunch `
            -ServerUri $parsedAppiumUri `
            -StatusUrl $appiumStatusUrl `
            -RepositoryRoot $repositoryRoot
    }

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
    elseif (-not [string]::IsNullOrWhiteSpace($AvdName)) {
        $AvdName = $AvdName.Trim()
        Assert-NotReservedDevelopmentAvd `
            -RequestedAvdName $AvdName `
            -SelectionDescription 'The Android UI runner'

        $matchingDeviceIds = @($usableDeviceIds | Where-Object {
            $_ -match '^emulator-\d+$' -and
            (Get-EmulatorAvdName -AdbExecutable $adbExecutable -EmulatorId $_) -eq $AvdName
        })
        if ($matchingDeviceIds.Count -gt 1) {
            throw "Multiple connected emulators report requested AVD '$AvdName': $($matchingDeviceIds -join ', ')."
        }
        if ($matchingDeviceIds.Count -eq 1) {
            $DeviceId = $matchingDeviceIds[0]
        }
        else {
            $emulatorExecutable = Resolve-RequiredCommand -Name 'emulator' -RequestedCommand 'emulator'
            $DeviceId = Start-ConfiguredEmulator `
                -AdbExecutable $adbExecutable `
                -EmulatorExecutable $emulatorExecutable `
                -RequestedAvdName $AvdName `
                -ExistingDeviceIds @($initialDeviceStates.Keys)
        }
    }
    elseif ($usableDeviceIds.Count -eq 1) {
        $DeviceId = $usableDeviceIds[0]
    }
    elseif ($usableDeviceIds.Count -eq 0) {
        if ([string]::IsNullOrWhiteSpace($AvdName)) {
            $AvdName = if ([string]::IsNullOrWhiteSpace($env:SOFTBALL_ANDROID_AVD)) {
                $defaultAndroidUiTestAvdName
            }
            else {
                $env:SOFTBALL_ANDROID_AVD.Trim()
            }
        }
        else {
            $AvdName = $AvdName.Trim()
        }
        Assert-NotReservedDevelopmentAvd `
            -RequestedAvdName $AvdName `
            -SelectionDescription 'The Android UI runner'

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

    if ($DeviceId -match '^emulator-\d+$') {
        $selectedAvdName = Get-EmulatorAvdName -AdbExecutable $adbExecutable -EmulatorId $DeviceId
        if ([string]::IsNullOrWhiteSpace($selectedAvdName)) {
            throw "Unable to identify the AVD backing emulator '$DeviceId'; the runner cannot verify Android UI-test isolation."
        }
        Assert-NotReservedDevelopmentAvd `
            -RequestedAvdName $selectedAvdName `
            -SelectionDescription "Emulator '$DeviceId'"
        Write-Stage "Using Android UI-test AVD: $selectedAvdName"
    }

    Write-Stage "Using device: $DeviceId"

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
    $testArguments = @('test', $testProject, '-m:1')
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $testArguments += @('--filter', $TestFilter)
        Write-Stage "Running Android UI tests with filter: $TestFilter"
    }
    else {
        Write-Stage 'Running the complete Android UI test suite...'
    }
    Invoke-RequiredCommand -Description 'Android UI tests' -FilePath $dotnetExecutable -Arguments $testArguments

    if ($startedAppium) {
        $startedAppiumProcess.Refresh()
        if ($startedAppiumProcess.HasExited) {
            throw "Runner-owned Appium PID $($startedAppiumProcess.Id) exited unexpectedly with code $($startedAppiumProcess.ExitCode).`n$(Get-AppiumLogTail)"
        }
    }

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
    if ($startedAppium -and $null -ne $startedAppiumProcess) {
        $startedAppiumProcess.Refresh()
        if ($startedAppiumProcess.HasExited) {
            Write-Warning "[Android UI] Runner-owned Appium PID $($startedAppiumProcess.Id) exited unexpectedly with code $($startedAppiumProcess.ExitCode).`n$(Get-AppiumLogTail)"
        }
    }
}
finally {
    if ($startedAppium -and $null -ne $startedAppiumProcess) {
        try {
            Stop-OwnedAppium -AppiumProcess $startedAppiumProcess
        }
        catch {
            Write-Warning "[Android UI] Appium cleanup failed: $($_.Exception.Message)"
            if ($exitCode -eq 0) {
                $exitCode = 1
            }
        }
    }

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
