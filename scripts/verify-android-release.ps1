[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ApkPath,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string] $AndroidVersionCode,

    [Parameter(Mandatory = $true)]
    [string] $SourceSha,

    [string] $ZipAlignPath,

    [string] $ApkSignerPath,

    [string] $Aapt2Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "android-apksigner-output.ps1")

$expectedCertificateSha256 = "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05"
$expectedPackageId = "com.companyname.softballscorekeeper"
$expectedAbis = @("arm64-v8a", "x86_64")

function Resolve-RequiredFile {
    param([string] $Path, [string] $Description)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Description was not found at '$resolved'."
    }
    return $resolved
}

function Resolve-AndroidTool {
    param([string] $Name, [string] $RequestedPath)
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-RequiredFile -Path $RequestedPath -Description "Android SDK $Name"
    }
    $command = Get-Command -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }
    $sdkRoot = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $env:ANDROID_HOME
    } elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $env:ANDROID_SDK_ROOT
    } else {
        $null
    }
    if ([string]::IsNullOrWhiteSpace($sdkRoot)) {
        throw "Android SDK $Name was not found on PATH and neither ANDROID_HOME nor ANDROID_SDK_ROOT is configured."
    }
    $candidateNames = if ($env:OS -eq "Windows_NT") { @("$Name.exe", "$Name.bat", $Name) } else { @($Name) }
    foreach ($directory in @(Get-ChildItem -LiteralPath (Join-Path $sdkRoot "build-tools") -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "^[0-9]+(\.[0-9]+){1,3}$" } | Sort-Object { [version] $_.Name } -Descending)) {
        foreach ($candidateName in $candidateNames) {
            $candidate = Join-Path $directory.FullName $candidateName
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }
    throw "Android SDK $Name was not found in '$sdkRoot/build-tools'."
}

function Invoke-AndroidTool {
    param([string] $Description, [string] $Executable, [string[]] $Arguments)
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode. $($output -join [Environment]::NewLine)"
    }
    return $output
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

$apk = Resolve-RequiredFile -Path $ApkPath -Description "Definitive Android APK"
$expectedName = "SoftballScorekeeper-$ReleaseVersion-$($SourceSha.Substring(0, 8).ToLowerInvariant()).apk"
if ([System.IO.Path]::GetFileName($apk) -cne $expectedName) {
    throw "Definitive Android APK must be named '$expectedName'."
}
$zipAlign = Resolve-AndroidTool -Name "zipalign" -RequestedPath $ZipAlignPath
$apkSigner = Resolve-AndroidTool -Name "apksigner" -RequestedPath $ApkSignerPath
$aapt2 = Resolve-AndroidTool -Name "aapt2" -RequestedPath $Aapt2Path

Invoke-AndroidTool -Description "Definitive APK alignment verification" -Executable $zipAlign -Arguments @("-c", "-p", "4", $apk) | Out-Null
$signatureText = (Invoke-AndroidTool -Description "Definitive APK signature verification" -Executable $apkSigner -Arguments @("verify", "--verbose", "--print-certs-pem", $apk)) -join [Environment]::NewLine
foreach ($scheme in @("v1", "v2", "v3")) {
    if ($signatureText -notmatch "Verified using $scheme scheme .*:\s*true") {
        throw "Definitive APK did not verify under APK signature scheme $scheme."
    }
}
if ($signatureText -notmatch "Verified using v4 scheme .*:\s*false") {
    throw "Definitive APK must not contain an APK signature scheme v4 sidecar signature."
}
$signerInfo = ConvertFrom-ApkSignerVerificationOutput -Output $signatureText
if ($signerInfo.SignerCount -ne 1) {
    throw "Definitive APK must contain exactly one signer; found $($signerInfo.SignerCount)."
}
$actualCertificate = $signerInfo.CertificateSha256[0]
if ($actualCertificate -cne $expectedCertificateSha256) {
    throw "Definitive APK certificate mismatch. Expected $expectedCertificateSha256; found $actualCertificate."
}

$badgingText = (Invoke-AndroidTool -Description "Definitive APK metadata verification" -Executable $aapt2 -Arguments @("dump", "badging", $apk)) -join [Environment]::NewLine
$packageMatch = [regex]::Match($badgingText, "(?m)^package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'")
if (-not $packageMatch.Success) {
    throw "Definitive APK package metadata was not recognized."
}
if ($packageMatch.Groups[1].Value -cne $expectedPackageId) {
    throw "Definitive APK package mismatch. Expected $expectedPackageId; found $($packageMatch.Groups[1].Value)."
}
if ($packageMatch.Groups[2].Value -cne $AndroidVersionCode) {
    throw "Definitive APK version code mismatch. Expected $AndroidVersionCode; found $($packageMatch.Groups[2].Value)."
}
if ($packageMatch.Groups[3].Value -cne $ReleaseVersion) {
    throw "Definitive APK version name mismatch. Expected $ReleaseVersion; found $($packageMatch.Groups[3].Value)."
}
$nativeCodeMatch = [regex]::Match($badgingText, "(?m)^native-code:\s*(.+)$")
if (-not $nativeCodeMatch.Success) {
    throw "Definitive APK native ABI metadata was not found."
}
$actualAbis = @([regex]::Matches($nativeCodeMatch.Groups[1].Value, "'([^']+)'") | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
if (($actualAbis -join ",") -cne (($expectedAbis | Sort-Object) -join ",")) {
    throw "Definitive APK ABI mismatch. Expected $(($expectedAbis | Sort-Object) -join ', '); found $($actualAbis -join ', ')."
}

$hash = (Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToUpperInvariant()
Write-Host "Verified definitive Android APK: $apk"
Write-Host "Definitive Android APK SHA-256: $hash"
[pscustomobject]@{
    Path = $apk
    FileName = [System.IO.Path]::GetFileName($apk)
    Sha256 = $hash
    CertificateSha256 = $actualCertificate
    PackageId = $expectedPackageId
    VersionName = $ReleaseVersion
    VersionCode = $AndroidVersionCode
    Abis = $actualAbis
}
