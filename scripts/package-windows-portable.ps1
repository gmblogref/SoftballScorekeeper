[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputZip,

    [ValidateNotNullOrEmpty()]
    [string]$StagingDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

function Get-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Get-NormalizedRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $rootWithSeparator = $RootPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $FullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$FullPath' is outside '$RootPath'."
    }

    return $FullPath.Substring($rootWithSeparator.Length).Replace('\', '/')
}

function Assert-SafeRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $normalized = $Path.Replace('\', '/')
    if ([System.IO.Path]::IsPathRooted($Path) -or
        $normalized.StartsWith('/', [System.StringComparison]::Ordinal) -or
        $normalized.Split('/') -contains '..') {
        throw "$Description '$Path' must be a repository-relative path without parent traversal."
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-DistributionChecksums {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $distributionRoot = Join-Path $RepositoryRoot 'licenses/distribution'
    $checksumPath = Join-Path $distributionRoot 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Distribution checksum inventory is missing: $checksumPath"
    }

    $validatedCounts = @{
        common = 0
        windows = 0
    }

    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -notmatch '^([A-Fa-f0-9]{64})  (.+)$') {
            throw "Distribution checksum inventory contains an invalid line: $line"
        }

        $expectedHash = $Matches[1].ToUpperInvariant()
        $relativePath = $Matches[2].Replace('\', '/')
        $platformDirectory = $relativePath.Split('/')[0]
        if ($platformDirectory -notin @('common', 'windows')) {
            continue
        }

        Assert-SafeRelativePath -Path $relativePath -Description 'Distribution checksum path'
        $sourcePath = Get-FullPath -Path $relativePath -BasePath $distributionRoot
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Checksummed $platformDirectory license source is missing: $relativePath"
        }

        $actualHash = Get-Sha256 -Path $sourcePath
        if ($actualHash -cne $expectedHash) {
            throw "Distribution checksum mismatch for '$relativePath'. Expected $expectedHash; found $actualHash."
        }

        $validatedCounts[$platformDirectory]++
    }

    if ($validatedCounts.common -eq 0 -or $validatedCounts.windows -eq 0) {
        throw 'Distribution checksum inventory must contain both common and Windows license files.'
    }
}

function Add-LicenseMapping {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Mappings,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$SourceRelativePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRelativePath
    )

    Assert-SafeRelativePath -Path $SourceRelativePath -Description 'License source'
    Assert-SafeRelativePath -Path $DestinationRelativePath -Description 'License destination'

    $normalizedDestination = $DestinationRelativePath.Replace('\', '/')
    $isAllowedDestination =
        $normalizedDestination -ceq 'LICENSE.txt' -or
        $normalizedDestination -ceq 'LICENSE-INFO.txt' -or
        $normalizedDestination -ceq 'THIRD-PARTY-NOTICES.txt' -or
        $normalizedDestination.StartsWith('licenses/common/', [System.StringComparison]::Ordinal) -or
        $normalizedDestination.StartsWith('licenses/windows/', [System.StringComparison]::Ordinal)

    if (-not $isAllowedDestination -or
        $normalizedDestination.StartsWith('licenses/android/', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest destination '$DestinationRelativePath' is not valid for the Windows portable distribution."
    }

    $sourcePath = Get-FullPath -Path $SourceRelativePath -BasePath $RepositoryRoot
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required license source is missing: $SourceRelativePath"
    }

    $sourceHash = Get-Sha256 -Path $sourcePath
    if ($Mappings.Contains($normalizedDestination)) {
        $existing = $Mappings[$normalizedDestination]
        if ($existing.SourceHash -cne $sourceHash) {
            throw "Manifest maps different content to '$normalizedDestination': '$($existing.SourceRelativePath)' and '$SourceRelativePath'."
        }

        return
    }

    $Mappings[$normalizedDestination] = [pscustomobject]@{
        SourceRelativePath = $SourceRelativePath.Replace('\', '/')
        SourcePath = $sourcePath
        SourceHash = $sourceHash
        DestinationRelativePath = $normalizedDestination
    }
}

function Get-WindowsLicenseMappings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [psobject]$Manifest
    )

    if ($Manifest.schema_version -ne 1) {
        throw "Unsupported production license manifest schema '$($Manifest.schema_version)'. Expected schema 1."
    }

    if ($Manifest.release_target -cne 'v2.0.0') {
        throw "Production license manifest targets '$($Manifest.release_target)', not v2.0.0."
    }

    if ([string]::IsNullOrWhiteSpace([string]$Manifest.coverage_status)) {
        throw 'Production license manifest does not record a coverage status.'
    }

    $mappings = New-Object 'System.Collections.Specialized.OrderedDictionary' ([System.StringComparer]::OrdinalIgnoreCase)
    Add-LicenseMapping `
        -Mappings $mappings `
        -RepositoryRoot $RepositoryRoot `
        -SourceRelativePath ([string]$Manifest.application_license.source) `
        -DestinationRelativePath ([string]$Manifest.application_license.packaged_destination)

    $noticeSource = [string]$Manifest.windows_distribution_notice.source
    $noticeDestination = [string]$Manifest.windows_distribution_notice.packaged_destination
    $noticeExpectedHash = [string]$Manifest.windows_distribution_notice.sha256
    if ($noticeExpectedHash -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Production license manifest does not record a valid Windows distribution notice SHA-256.'
    }

    Add-LicenseMapping `
        -Mappings $mappings `
        -RepositoryRoot $RepositoryRoot `
        -SourceRelativePath $noticeSource `
        -DestinationRelativePath $noticeDestination

    $normalizedNoticeDestination = $noticeDestination.Replace('\', '/')
    if ($mappings[$normalizedNoticeDestination].SourceHash -cne $noticeExpectedHash.ToUpperInvariant()) {
        throw "Windows distribution notice SHA-256 does not match '$noticeSource'."
    }

    $windowsComponents = @($Manifest.components | Where-Object { @($_.platform) -contains 'windows' })
    if ($windowsComponents.Count -eq 0) {
        throw 'Production license manifest contains no Windows runtime components.'
    }

    foreach ($component in $windowsComponents) {
        $sources = @($component.required_notice_files)
        $destinations = @($component.packaged_destination_paths)

        if ($sources.Count -eq 0 -or $destinations.Count -eq 0) {
            throw "Component '$($component.component)' has no required notice mapping."
        }

        $componentHashProperty = $component.PSObject.Properties['sha256']
        if ($null -ne $componentHashProperty -and
            -not [string]::IsNullOrWhiteSpace([string]$componentHashProperty.Value)) {
            if ($sources.Count -ne 1) {
                throw "Component '$($component.component)' records one SHA-256 but maps multiple notice files."
            }

            $versionedSourcePath = Get-FullPath -Path ([string]$sources[0]) -BasePath $RepositoryRoot
            if (-not (Test-Path -LiteralPath $versionedSourcePath -PathType Leaf)) {
                throw "Version-specific license source is missing: $($sources[0])"
            }

            $versionedSourceHash = Get-Sha256 -Path $versionedSourcePath
            if ($versionedSourceHash -cne ([string]$componentHashProperty.Value).ToUpperInvariant()) {
                throw "Manifest SHA-256 does not match '$($sources[0])'."
            }
        }

        if ($sources.Count -eq $destinations.Count) {
            for ($index = 0; $index -lt $sources.Count; $index++) {
                Add-LicenseMapping `
                    -Mappings $mappings `
                    -RepositoryRoot $RepositoryRoot `
                    -SourceRelativePath ([string]$sources[$index]) `
                    -DestinationRelativePath ([string]$destinations[$index])
            }

            continue
        }

        if ($destinations.Count -eq 1 -and ([string]$destinations[0]).Replace('\', '/').EndsWith('/')) {
            foreach ($source in $sources) {
                $destination = ([string]$destinations[0]).Replace('\', '/') + [System.IO.Path]::GetFileName([string]$source)
                Add-LicenseMapping `
                    -Mappings $mappings `
                    -RepositoryRoot $RepositoryRoot `
                    -SourceRelativePath ([string]$source) `
                    -DestinationRelativePath $destination
            }

            continue
        }

        throw "Component '$($component.component)' has ambiguous required notice and destination mappings."
    }

    $requiredDestinations = @(
        'LICENSE.txt',
        'LICENSE-INFO.txt',
        'THIRD-PARTY-NOTICES.txt',
        'licenses/common/Apache-2.0-LICENSE.txt',
        'licenses/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md',
        'licenses/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt',
        'licenses/common/DotNet-MIT-LICENSE.txt',
        'licenses/common/Microsoft.Maui-10.0.110-THIRD-PARTY-NOTICES.txt',
        'licenses/common/sqlite-net-pcl-1.11.285-LICENSE.txt',
        'licenses/common/SQLite-PUBLIC-DOMAIN.txt',
        'licenses/windows/Microsoft.Graphics.Win2D-1.4.0-EULA.html',
        'licenses/windows/Microsoft.IO.RecyclableMemoryStream-3.0.1-LICENSE.txt',
        'licenses/windows/Microsoft.NETCore.App.Runtime.win-x64-10.0.13-THIRD-PARTY-NOTICES.txt',
        'licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt',
        'licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt',
        'licenses/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf',
        'licenses/windows/Microsoft.WindowsAppSDK-1.8.260529003-LICENSE.txt',
        'licenses/windows/Microsoft.WindowsAppSDK-1.8.260529003-NOTICE.txt',
        'licenses/windows/Microsoft.WindowsAppSDK.DWrite-1.8.25122902-LICENSE.txt',
        'licenses/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-LICENSE.txt',
        'licenses/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-THIRD-PARTY-NOTICES.txt'
    )

    foreach ($requiredDestination in $requiredDestinations) {
        if (-not $mappings.Contains($requiredDestination)) {
            throw "Production license manifest does not map required Windows distribution file '$requiredDestination'."
        }
    }

    return @($mappings.Values | Sort-Object DestinationRelativePath)
}

function Assert-PublishDirectorySafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishRoot
    )

    $reservedRootNames = @('LICENSE.txt', 'LICENSE-INFO.txt', 'THIRD-PARTY-NOTICES.txt', 'licenses')
    foreach ($reservedName in $reservedRootNames) {
        if (Test-Path -LiteralPath (Join-Path $PublishRoot $reservedName)) {
            throw "Publish directory already contains reserved packaging path '$reservedName'. Use an unmodified publish output."
        }
    }

    $forbiddenExtensions = @('.appx', '.appxbundle', '.cer', '.jks', '.key', '.keystore', '.msix', '.msixbundle', '.p12', '.p8', '.pem', '.pfx', '.snk')
    $forbiddenDirectories = @('.git', '.vs', 'node_modules', 'obj', 'TestResults')
    $publishFiles = @(Get-ChildItem -LiteralPath $PublishRoot -File -Recurse -Force)
    if ($publishFiles.Count -eq 0) {
        throw "Publish directory is empty: $PublishRoot"
    }

    foreach ($item in Get-ChildItem -LiteralPath $PublishRoot -Recurse -Force) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Publish directory contains a symbolic link or reparse point: $($item.FullName)"
        }

        if ($item.PSIsContainer -and $forbiddenDirectories -contains $item.Name) {
            throw "Publish directory contains build cache or repository directory '$($item.Name)'."
        }

        if (-not $item.PSIsContainer -and $forbiddenExtensions -contains $item.Extension.ToLowerInvariant()) {
            throw "Publish directory contains a forbidden package or sensitive file: $($item.FullName)"
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $PublishRoot 'SoftballScorekeeper.exe') -PathType Leaf)) {
        throw "Publish directory does not contain SoftballScorekeeper.exe: $PublishRoot"
    }
}

function Copy-PublishFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishRoot,

        [Parameter(Mandatory = $true)]
        [string]$StageRoot
    )

    $copiedCount = 0
    foreach ($sourceFile in Get-ChildItem -LiteralPath $PublishRoot -File -Recurse -Force | Sort-Object FullName) {
        if ($sourceFile.Extension -ieq '.pdb') {
            continue
        }

        $relativePath = Get-NormalizedRelativePath -RootPath $PublishRoot -FullPath $sourceFile.FullName
        $destinationPath = Join-Path $StageRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $destinationDirectory = Split-Path -Parent $destinationPath
        if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }

        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationPath
        $copiedCount++
    }

    return $copiedCount
}

function Copy-LicenseFiles {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Mappings,

        [Parameter(Mandatory = $true)]
        [string]$StageRoot
    )

    foreach ($mapping in $Mappings) {
        $destinationPath = Join-Path $StageRoot ($mapping.DestinationRelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $destinationDirectory = Split-Path -Parent $destinationPath
        if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }

        Copy-Item -LiteralPath $mapping.SourcePath -Destination $destinationPath
        $destinationHash = Get-Sha256 -Path $destinationPath
        if ($destinationHash -cne $mapping.SourceHash) {
            throw "Staged license hash does not match source for '$($mapping.DestinationRelativePath)'."
        }
    }
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StageRoot,

        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    Add-Type -AssemblyName System.IO.Compression
    $fileStream = [System.IO.File]::Open($ArchivePath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $archive = New-Object System.IO.Compression.ZipArchive(
            $fileStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            $fixedTimestamp = New-Object System.DateTimeOffset(1980, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
            foreach ($sourceFile in Get-ChildItem -LiteralPath $StageRoot -File -Recurse -Force | Sort-Object FullName) {
                $relativePath = Get-NormalizedRelativePath -RootPath $StageRoot -FullPath $sourceFile.FullName
                $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTimestamp
                $entry.ExternalAttributes = 0

                $sourceStream = [System.IO.File]::OpenRead($sourceFile.FullName)
                try {
                    $entryStream = $entry.Open()
                    try {
                        $sourceStream.CopyTo($entryStream)
                    }
                    finally {
                        $entryStream.Dispose()
                    }
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}

function Test-ZipContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [object[]]$Mappings
    )

    Add-Type -AssemblyName System.IO.Compression
    $fileStream = [System.IO.File]::OpenRead($ArchivePath)
    try {
        $archive = New-Object System.IO.Compression.ZipArchive(
            $fileStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            $entries = @($archive.Entries)
            $entryNames = @($entries | ForEach-Object { $_.FullName })
            if ($entryNames -notcontains 'SoftballScorekeeper.exe') {
                throw 'Portable ZIP does not contain SoftballScorekeeper.exe at its root.'
            }

            if (@($entryNames | Where-Object { $_.StartsWith('licenses/android/', [System.StringComparison]::OrdinalIgnoreCase) }).Count -ne 0) {
                throw 'Portable ZIP contains Android-only license files.'
            }

            $licenseEntryNames = @($entryNames | Where-Object {
                $_ -ceq 'LICENSE.txt' -or
                $_ -ceq 'LICENSE-INFO.txt' -or
                $_ -ceq 'THIRD-PARTY-NOTICES.txt' -or
                $_.StartsWith('licenses/', [System.StringComparison]::Ordinal)
            })

            if ($licenseEntryNames.Count -ne $Mappings.Count) {
                throw "Portable ZIP contains $($licenseEntryNames.Count) license files; expected $($Mappings.Count)."
            }

            foreach ($mapping in $Mappings) {
                $matchingEntries = @($entries | Where-Object { $_.FullName -ceq $mapping.DestinationRelativePath })
                if ($matchingEntries.Count -ne 1) {
                    throw "Portable ZIP must contain exactly one '$($mapping.DestinationRelativePath)' entry."
                }

                $entryStream = $matchingEntries[0].Open()
                try {
                    $sha256 = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $zipHash = ([System.BitConverter]::ToString($sha256.ComputeHash($entryStream))).Replace('-', '')
                    }
                    finally {
                        $sha256.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }

                if ($zipHash -cne $mapping.SourceHash) {
                    throw "Portable ZIP license hash does not match source for '$($mapping.DestinationRelativePath)'."
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}

$scriptRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$publishRoot = Get-FullPath -Path $PublishDirectory -BasePath (Get-Location).Path
$outputPath = Get-FullPath -Path $OutputZip -BasePath (Get-Location).Path
$outputParent = Split-Path -Parent $outputPath

if (-not (Test-Path -LiteralPath $publishRoot -PathType Container)) {
    throw "Windows publish directory does not exist: $publishRoot"
}

if ([System.IO.Path]::GetExtension($outputPath) -ine '.zip') {
    throw "Output path must use the .zip extension: $outputPath"
}

if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) {
    throw "Output directory does not exist: $outputParent"
}

if (Test-Path -LiteralPath $outputPath) {
    throw "Output ZIP already exists and will not be overwritten: $outputPath"
}

$publishPrefix = $publishRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($outputPath.StartsWith($publishPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output ZIP must be outside the publish directory.'
}

$manifestPath = Join-Path $repositoryRoot 'licenses/distribution/production-license-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Production license manifest is missing: $manifestPath"
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}
catch {
    throw "Production license manifest is not valid JSON: $($_.Exception.Message)"
}

$licenseMappings = @(Get-WindowsLicenseMappings -RepositoryRoot $repositoryRoot -Manifest $manifest)
Assert-DistributionChecksums -RepositoryRoot $repositoryRoot
Assert-PublishDirectorySafe -PublishRoot $publishRoot

if ([string]::IsNullOrWhiteSpace($StagingDirectory)) {
    $stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SoftballScorekeeper-windows-package-" + [System.Guid]::NewGuid().ToString('N'))
}
else {
    $stageRoot = Get-FullPath -Path $StagingDirectory -BasePath (Get-Location).Path
}

if (Test-Path -LiteralPath $stageRoot) {
    throw "Staging directory already exists and will not be overwritten: $stageRoot"
}

if ($stageRoot.StartsWith($publishPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Staging directory must be outside the publish directory.'
}

$temporaryArchive = Join-Path $outputParent (([System.IO.Path]::GetFileName($outputPath)) + '.' + [System.Guid]::NewGuid().ToString('N') + '.tmp')
$stageCreated = $false
try {
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    $stageCreated = $true

    $publishedFileCount = Copy-PublishFiles -PublishRoot $publishRoot -StageRoot $stageRoot
    Copy-LicenseFiles -Mappings $licenseMappings -StageRoot $stageRoot
    New-DeterministicZip -StageRoot $stageRoot -ArchivePath $temporaryArchive
    Test-ZipContents -ArchivePath $temporaryArchive -Mappings $licenseMappings

    Move-Item -LiteralPath $temporaryArchive -Destination $outputPath
    $artifact = Get-Item -LiteralPath $outputPath
    [pscustomobject]@{
        OutputZip = $artifact.FullName
        SizeBytes = $artifact.Length
        Sha256 = Get-Sha256 -Path $artifact.FullName
        PublishedFileCount = $publishedFileCount
        LicenseFileCount = $licenseMappings.Count
        Manifest = $manifestPath
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryArchive) {
        Remove-Item -LiteralPath $temporaryArchive -Force
    }

    if ($stageCreated -and (Test-Path -LiteralPath $stageRoot -PathType Container)) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}
