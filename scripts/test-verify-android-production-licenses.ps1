[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$verifier = Join-Path $PSScriptRoot "verify-android-production-licenses.ps1"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-android-license-tests-" + [Guid]::NewGuid().ToString("N"))
$passed = 0
$failed = 0

function New-TestApk {
    param(
        [string] $Path,
        [string] $OmitAsset,
        [string] $TamperAsset,
        [switch] $AddUnexpected
    )
    $sources = @{
        "assets/licenses/application/LICENSE.txt" = Join-Path $repositoryRoot "LICENSE"
        "assets/licenses/THIRD-PARTY-NOTICES.txt" = Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.md"
        "assets/licenses/common/OpenSans-Apache-2.0.txt" = Join-Path $repositoryRoot "licenses/OpenSans-Apache-2.0.txt"
    }
    foreach ($platform in @("common", "android")) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "licenses/distribution/$platform") -File) {
            $sources["assets/licenses/$platform/$($file.Name)"] = $file.FullName
        }
    }
    $archive = [System.IO.Compression.ZipFile]::Open($Path, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($asset in $sources.Keys) {
            if ($asset -ceq $OmitAsset) { continue }
            if ($asset -ceq $TamperAsset) {
                $entry = $archive.CreateEntry($asset)
                $writer = [System.IO.StreamWriter]::new($entry.Open())
                try {
                    $writer.Write([System.IO.File]::ReadAllText($sources[$asset]))
                    $writer.Write("tampered")
                }
                finally { $writer.Dispose() }
            }
            else {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $sources[$asset],
                    $asset,
                    [System.IO.Compression.CompressionLevel]::Optimal
                ) | Out-Null
            }
        }
        if ($AddUnexpected) {
            $entry = $archive.CreateEntry("assets/licenses/windows/unexpected.txt")
            $writer = [System.IO.StreamWriter]::new($entry.Open())
            try { $writer.Write("unexpected") } finally { $writer.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Invoke-Case {
    param([string] $Name, [string] $ApkPath, [bool] $ShouldPass, [string] $ExpectedFailure)
    try {
        $didPass = $true
        $message = $null
        try { & $verifier -ApkPath $ApkPath -ReleaseVersion "2.0.1" -RepositoryRoot $repositoryRoot | Out-Null }
        catch { $didPass = $false; $message = $_.Exception.Message }
        if ($didPass -ne $ShouldPass) { throw "Unexpected pass state. $message" }
        if (-not $ShouldPass -and $message.IndexOf($ExpectedFailure, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Unexpected failure: $message"
        }
        $script:passed++
        Write-Host "PASS: $Name"
    }
    catch {
        $script:failed++
        Write-Host "FAIL: $Name -- $($_.Exception.Message)"
    }
}

try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $valid = Join-Path $temporaryRoot "valid.apk"
    $missing = Join-Path $temporaryRoot "missing.apk"
    $tampered = Join-Path $temporaryRoot "tampered.apk"
    $unexpected = Join-Path $temporaryRoot "unexpected.apk"
    New-TestApk -Path $valid
    New-TestApk -Path $missing -OmitAsset "assets/licenses/android/DotNet-Android-MIT-LICENSE.txt"
    New-TestApk -Path $tampered -TamperAsset "assets/licenses/common/DotNet-MIT-LICENSE.txt"
    New-TestApk -Path $unexpected -AddUnexpected
    Invoke-Case -Name "accepts exact reviewed Android license inventory" -ApkPath $valid -ShouldPass $true
    Invoke-Case -Name "rejects missing Android notice" -ApkPath $missing -ShouldPass $false -ExpectedFailure "Missing:"
    Invoke-Case -Name "rejects changed notice content" -ApkPath $tampered -ShouldPass $false -ExpectedFailure "does not match"
    Invoke-Case -Name "rejects unexpected platform notice" -ApkPath $unexpected -ShouldPass $false -ExpectedFailure "unexpected:"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}

Write-Host "Android production license validation: $passed passed, $failed failed."
if ($failed -ne 0) { exit 1 }
