[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ApkPath,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseVersion,

    [string] $RepositoryRoot = (Join-Path $PSScriptRoot "..")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RequiredFile {
    param([string] $Path, [string] $Description)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Description was not found at '$resolved'."
    }
    return $resolved
}

function Add-ExpectedAsset {
    param(
        [hashtable] $Mappings,
        [string] $AssetPath,
        [string] $SourcePath
    )
    if ($Mappings.ContainsKey($AssetPath)) {
        throw "Duplicate expected Android license asset '$AssetPath'."
    }
    $Mappings[$AssetPath] = Resolve-RequiredFile -Path $SourcePath -Description "Android license source for $AssetPath"
}

if ($ReleaseVersion -notmatch "^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$") {
    throw "Release version '$ReleaseVersion' must use strict major.minor.patch format."
}

$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
$apk = Resolve-RequiredFile -Path $ApkPath -Description "Android APK"
$distributionRoot = Join-Path $root "licenses/distribution"
$manifestPath = Resolve-RequiredFile -Path (Join-Path $distributionRoot "production-license-manifest.json") -Description "Production license manifest"
$checksumPath = Resolve-RequiredFile -Path (Join-Path $distributionRoot "SHA256SUMS.txt") -Description "Production license checksum inventory"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($manifest.schema_version -ne 1) {
    throw "Unsupported production license manifest schema '$($manifest.schema_version)'."
}
$expectedReleaseTarget = "v$ReleaseVersion"
if ($manifest.release_target -cne $expectedReleaseTarget) {
    throw "Production license manifest targets '$($manifest.release_target)', not requested release '$expectedReleaseTarget'."
}

$checksumEntries = @{}
foreach ($line in Get-Content -LiteralPath $checksumPath) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^([0-9A-F]{64})  (android|common|windows)/([^/]+)$') {
        throw "Invalid production license checksum entry '$line'."
    }
    $relativePath = "$($Matches[2])/$($Matches[3])"
    if ($checksumEntries.ContainsKey($relativePath)) {
        throw "Duplicate production license checksum entry '$relativePath'."
    }
    $sourcePath = Resolve-RequiredFile -Path (Join-Path $distributionRoot $relativePath) -Description "Checksummed production license"
    $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -cne $Matches[1]) {
        throw "Production license checksum mismatch for '$relativePath'. Expected $($Matches[1]); found $actualHash."
    }
    $checksumEntries[$relativePath] = $actualHash
}

$expectedChecksumPaths = @(
    Get-ChildItem -LiteralPath (Join-Path $distributionRoot "android") -File
    Get-ChildItem -LiteralPath (Join-Path $distributionRoot "common") -File
) | ForEach-Object {
    $platform = Split-Path -Leaf $_.DirectoryName
    "$platform/$($_.Name)"
} | Sort-Object
$actualAndroidChecksumPaths = @($checksumEntries.Keys | Where-Object { $_ -match '^(android|common)/' } | Sort-Object)
if (($expectedChecksumPaths -join "`n") -cne ($actualAndroidChecksumPaths -join "`n")) {
    throw "Production license checksum inventory does not exactly cover the Android and common distribution files."
}

$mappings = @{}
Add-ExpectedAsset -Mappings $mappings -AssetPath "assets/licenses/application/LICENSE.txt" -SourcePath (Join-Path $root "LICENSE")
Add-ExpectedAsset -Mappings $mappings -AssetPath "assets/licenses/THIRD-PARTY-NOTICES.txt" -SourcePath (Join-Path $root "THIRD-PARTY-NOTICES.md")
Add-ExpectedAsset -Mappings $mappings -AssetPath "assets/licenses/common/OpenSans-Apache-2.0.txt" -SourcePath (Join-Path $root "licenses/OpenSans-Apache-2.0.txt")
foreach ($platform in @("common", "android")) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $distributionRoot $platform) -File | Sort-Object Name) {
        Add-ExpectedAsset -Mappings $mappings -AssetPath "assets/licenses/$platform/$($file.Name)" -SourcePath $file.FullName
    }
}

foreach ($component in @($manifest.components)) {
    if (@($component.platform) -notcontains "android") { continue }
    foreach ($noticePath in @($component.required_notice_files)) {
        $resolvedNotice = Resolve-RequiredFile -Path (Join-Path $root $noticePath) -Description "Required Android component notice"
        if ($resolvedNotice.StartsWith((Join-Path $distributionRoot "windows"), [StringComparison]::OrdinalIgnoreCase)) {
            throw "Android component '$($component.component)' requires a Windows-only notice '$noticePath'."
        }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($apk)
try {
    $licenseEntries = @($archive.Entries | Where-Object { $_.FullName.StartsWith("assets/licenses/", [StringComparison]::Ordinal) })
    $entryNames = @($licenseEntries | ForEach-Object FullName)
    if ($entryNames.Count -ne @($entryNames | Select-Object -Unique).Count) {
        throw "Android APK contains duplicate production license asset names."
    }
    $expectedNames = @($mappings.Keys | Sort-Object)
    $actualNames = @($entryNames | Sort-Object)
    if (($expectedNames -join "`n") -cne ($actualNames -join "`n")) {
        $missing = @($expectedNames | Where-Object { $_ -notin $actualNames })
        $unexpected = @($actualNames | Where-Object { $_ -notin $expectedNames })
        throw "Android APK license inventory mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }

    foreach ($entry in $licenseEntries) {
        $stream = $entry.Open()
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $actualHash = ([BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '') }
            finally { $sha.Dispose() }
        }
        finally { $stream.Dispose() }
        $expectedHash = (Get-FileHash -LiteralPath $mappings[$entry.FullName] -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -cne $expectedHash) {
            throw "Android APK license asset '$($entry.FullName)' does not match its reviewed source."
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Verified Android production license inventory: $($mappings.Count) packaged files for $expectedReleaseTarget."
[pscustomobject]@{
    ApkPath = $apk
    ReleaseTarget = $expectedReleaseTarget
    LicenseFileCount = $mappings.Count
}
