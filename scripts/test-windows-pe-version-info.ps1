[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WindowsZipPath,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'windows-pe-version-info.ps1')

$zipPath = [System.IO.Path]::GetFullPath($WindowsZipPath)
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Windows ZIP was not found at '$zipPath'."
}
if ($ReleaseVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw "Release version '$ReleaseVersion' must use strict major.minor.patch format."
}

$expectedVersion = "$ReleaseVersion.0"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-windows-version-test-" + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entryName in @('SoftballScorekeeper.exe', 'SoftballScorekeeper.dll')) {
            $matches = @($archive.Entries | Where-Object { $_.FullName -ceq $entryName })
            if ($matches.Count -ne 1) {
                throw "Windows ZIP must contain exactly one '$entryName' entry."
            }
            $destination = Join-Path $temporaryRoot $entryName
            $input = $matches[0].Open()
            try {
                $output = [System.IO.File]::Create($destination)
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $executablePath = Join-Path $temporaryRoot 'SoftballScorekeeper.exe'
    $assemblyPath = Join-Path $temporaryRoot 'SoftballScorekeeper.dll'
    $metadata = Get-WindowsPeVersionMetadata -Path $executablePath
    if ($metadata.FileVersion -cne $expectedVersion) {
        throw "Real Windows executable file version mismatch. Expected $expectedVersion; found '$($metadata.FileVersion)'."
    }
    if ($metadata.ProductVersion -cne $expectedVersion) {
        throw "Real Windows executable product version mismatch. Expected $expectedVersion; found '$($metadata.ProductVersion)'."
    }
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
    if ($assemblyVersion -cne $expectedVersion) {
        throw "Real Windows assembly version mismatch. Expected $expectedVersion; found '$assemblyVersion'."
    }

    $malformedPath = Join-Path $temporaryRoot 'malformed.exe'
    [System.IO.File]::WriteAllBytes($malformedPath, [byte[]](0x4D, 0x5A, 0x00, 0x00))
    $malformedRejected = $false
    try {
        Get-WindowsPeVersionMetadata -Path $malformedPath | Out-Null
    }
    catch {
        $malformedRejected = $true
    }
    if (-not $malformedRejected) {
        throw 'Malformed Windows executable version metadata was accepted.'
    }

    Write-Host "PASS: real Windows PE file version $($metadata.FileVersion), product version $($metadata.ProductVersion), and DLL assembly version $assemblyVersion."
    Write-Host 'PASS: malformed Windows PE metadata was rejected.'
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
