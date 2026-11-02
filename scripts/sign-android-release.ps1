[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $UnsignedApkPath,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string] $AndroidVersionCode,

    [Parameter(Mandatory = $true)]
    [string] $SourceSha,

    [Parameter(Mandatory = $true)]
    [string] $KeystorePath,

    [Parameter(Mandatory = $true)]
    [string] $KeyAlias,

    [Parameter(Mandatory = $true)]
    [string] $StorePasswordFile,

    [Parameter(Mandatory = $true)]
    [string] $KeyPasswordFile,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $ZipAlignPath,

    [string] $ApkSignerPath,

    [string] $Aapt2Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Description was not found at '$resolved'."
    }

    return $resolved
}

function Resolve-AndroidTool {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [string] $RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-RequiredFile -Path $RequestedPath -Description "Android SDK $Name"
    }

    $command = Get-Command -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    $sdkRoot = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $env:ANDROID_HOME
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $env:ANDROID_SDK_ROOT
    }
    else {
        $null
    }
    if ([string]::IsNullOrWhiteSpace($sdkRoot)) {
        throw "Android SDK $Name was not found on PATH and neither ANDROID_HOME nor ANDROID_SDK_ROOT is configured."
    }

    $buildToolsRoot = Join-Path $sdkRoot "build-tools"
    $buildToolDirectories = @(
        Get-ChildItem -LiteralPath $buildToolsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match "^[0-9]+(\.[0-9]+){1,3}$" } |
            Sort-Object { [version] $_.Name } -Descending
    )
    $candidateNames = if ($env:OS -eq "Windows_NT") {
        @("$Name.exe", "$Name.bat", $Name)
    }
    else {
        @($Name)
    }

    foreach ($directory in $buildToolDirectories) {
        foreach ($candidateName in $candidateNames) {
            $candidate = Join-Path $directory.FullName $candidateName
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    throw "Android SDK $Name was not found in '$buildToolsRoot'."
}

function Invoke-AndroidTool {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Description,

        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [switch] $AllowFailure
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode. $($output -join [Environment]::NewLine)"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Assert-TemporarySigningFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $resolved = Resolve-RequiredFile -Path $Path -Description $Description
    if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        $temporaryRoot = [System.IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        ) + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Description must be reconstructed under RUNNER_TEMP."
        }
    }

    return $resolved
}

if ($ReleaseVersion -notmatch "^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$") {
    throw "Release version '$ReleaseVersion' must use strict major.minor.patch format."
}
if ($AndroidVersionCode -notmatch "^[1-9][0-9]*$") {
    throw "Android version code '$AndroidVersionCode' must be a positive integer."
}
if ($SourceSha -notmatch "^[0-9A-Fa-f]{40}$") {
    throw "Source SHA '$SourceSha' must be a full 40-character Git commit SHA."
}
if ([string]::IsNullOrWhiteSpace($KeyAlias)) {
    throw "A signing key alias is required."
}

$unsignedApk = Resolve-RequiredFile -Path $UnsignedApkPath -Description "Unsigned Release APK"
if ([System.IO.Path]::GetFileName($unsignedApk) -match "-Signed\.apk$") {
    throw "The MAUI-generated signed APK is not a valid production signing input. Supply the unsigned Release APK."
}
$keystore = Assert-TemporarySigningFile -Path $KeystorePath -Description "Temporary Android signing keystore"
$storePassword = Assert-TemporarySigningFile -Path $StorePasswordFile -Description "Temporary keystore password file"
$keyPassword = Assert-TemporarySigningFile -Path $KeyPasswordFile -Description "Temporary key password file"
$zipAlign = Resolve-AndroidTool -Name "zipalign" -RequestedPath $ZipAlignPath
$apkSigner = Resolve-AndroidTool -Name "apksigner" -RequestedPath $ApkSignerPath
$aapt2 = Resolve-AndroidTool -Name "aapt2" -RequestedPath $Aapt2Path

$unsignedVerification = Invoke-AndroidTool `
    -Description "Unsigned APK signature preflight" `
    -Executable $apkSigner `
    -Arguments @("verify", "--verbose", $unsignedApk) `
    -AllowFailure
if ($unsignedVerification.ExitCode -eq 0) {
    throw "The production signing input is already signed. Refusing to re-sign a signed or debug-signed APK."
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$shortSha = $SourceSha.Substring(0, 8).ToLowerInvariant()
$outputApk = Join-Path $outputRoot "SoftballScorekeeper-$ReleaseVersion-$shortSha.apk"
if (Test-Path -LiteralPath $outputApk) {
    throw "Definitive Android artifact already exists at '$outputApk'. Refusing to overwrite it."
}

$workingDirectory = Join-Path $outputRoot ".android-signing-$shortSha"
if (Test-Path -LiteralPath $workingDirectory) {
    throw "Android signing working directory already exists at '$workingDirectory'."
}
New-Item -ItemType Directory -Path $workingDirectory | Out-Null
$alignedApk = Join-Path $workingDirectory "aligned-unsigned.apk"
$succeeded = $false

try {
    Invoke-AndroidTool `
        -Description "APK alignment" `
        -Executable $zipAlign `
        -Arguments @("-f", "-p", "4", $unsignedApk, $alignedApk) | Out-Null

    Invoke-AndroidTool `
        -Description "APK production signing" `
        -Executable $apkSigner `
        -Arguments @(
            "sign",
            "--ks", $keystore,
            "--ks-key-alias", $KeyAlias,
            "--ks-pass", "file:$storePassword",
            "--key-pass", "file:$keyPassword",
            "--v1-signing-enabled", "true",
            "--v2-signing-enabled", "true",
            "--v3-signing-enabled", "true",
            "--v4-signing-enabled", "false",
            "--out", $outputApk,
            $alignedApk
        ) | Out-Null

    $verification = & (Join-Path $PSScriptRoot "verify-android-release.ps1") `
        -ApkPath $outputApk `
        -ReleaseVersion $ReleaseVersion `
        -AndroidVersionCode $AndroidVersionCode `
        -SourceSha $SourceSha `
        -ZipAlignPath $zipAlign `
        -ApkSignerPath $apkSigner `
        -Aapt2Path $aapt2
    $succeeded = $true
    Write-Host "Definitive Android APK: $outputApk"
    return $verification
}
finally {
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force
    }
    if (-not $succeeded -and (Test-Path -LiteralPath $outputApk)) {
        Remove-Item -LiteralPath $outputApk -Force
    }
}
