[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AndroidArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string] $WindowsArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string] $AndroidVersionCode,

    [Parameter(Mandatory = $true)]
    [string] $SourceSha,

    [string] $AndroidVerifierPath = (Join-Path $PSScriptRoot "verify-android-release.ps1"),

    [string] $WindowsVerifierPath = (Join-Path $PSScriptRoot "package-windows-portable.ps1")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-Artifact {
    param([string] $Directory, [string] $ExpectedName, [string] $Description)
    $root = [System.IO.Path]::GetFullPath($Directory)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "$Description artifact directory does not exist: $root"
    }
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
    if ($files.Count -ne 1) {
        throw "$Description artifact directory must contain exactly one file; found $($files.Count)."
    }
    if ($files[0].Name -cne $ExpectedName) {
        throw "$Description artifact must be named '$ExpectedName'; found '$($files[0].Name)'."
    }
    return $files[0].FullName
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

$shortSha = $SourceSha.Substring(0, 8).ToLowerInvariant()
$apkName = "SoftballScorekeeper-$ReleaseVersion-$shortSha.apk"
$zipName = "SoftballScorekeeper-$ReleaseVersion-windows-x64-portable.zip"
$apk = Resolve-Artifact -Directory $AndroidArtifactDirectory -ExpectedName $apkName -Description "Android"
$zip = Resolve-Artifact -Directory $WindowsArtifactDirectory -ExpectedName $zipName -Description "Windows"

& $AndroidVerifierPath -ApkPath $apk -ReleaseVersion $ReleaseVersion -AndroidVersionCode $AndroidVersionCode -SourceSha $SourceSha | Out-Null
& $WindowsVerifierPath -InputZip $zip -ReleaseVersion $ReleaseVersion | Out-Null

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) {
    $existing = @(Get-ChildItem -LiteralPath $outputRoot -Force)
    if ($existing.Count -ne 0) {
        throw "Final artifact directory must be empty: $outputRoot"
    }
} else {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
}

$outputApk = Join-Path $outputRoot $apkName
$outputZip = Join-Path $outputRoot $zipName
Copy-Item -LiteralPath $apk -Destination $outputApk
Copy-Item -LiteralPath $zip -Destination $outputZip

$checksumPath = Join-Path $outputRoot "SHA256SUMS.txt"
$checksumLines = @(
    "$((Get-FileHash -LiteralPath $outputApk -Algorithm SHA256).Hash.ToLowerInvariant())  $apkName",
    "$((Get-FileHash -LiteralPath $outputZip -Algorithm SHA256).Hash.ToLowerInvariant())  $zipName"
)
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))

$parsed = @(Get-Content -LiteralPath $checksumPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($parsed.Count -ne 2) {
    throw "SHA256SUMS.txt must contain exactly two checksum entries."
}
foreach ($line in $parsed) {
    if ($line -notmatch '^([0-9a-f]{64})  ([^/\\]+)$') {
        throw "SHA256SUMS.txt contains an invalid entry: $line"
    }
    $expectedHash = $Matches[1]
    $fileName = $Matches[2]
    if ($fileName -cnotin @($apkName, $zipName)) {
        throw "SHA256SUMS.txt contains an unexpected artifact '$fileName'."
    }
    $actualHash = (Get-FileHash -LiteralPath (Join-Path $outputRoot $fileName) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $expectedHash) {
        throw "SHA-256 revalidation failed for '$fileName'."
    }
}

$finalFiles = @(Get-ChildItem -LiteralPath $outputRoot -File -Force)
if ($finalFiles.Count -ne 3 -or
    @($finalFiles.Name | Where-Object { $_ -cnotin @($apkName, $zipName, 'SHA256SUMS.txt') }).Count -ne 0) {
    throw "Final artifact directory must contain only the APK, Windows ZIP, and SHA256SUMS.txt."
}

Write-Host "Prepared verified release artifact set: $outputRoot"
[pscustomobject]@{
    OutputDirectory = $outputRoot
    AndroidApk = $outputApk
    WindowsZip = $outputZip
    Checksums = $checksumPath
}
