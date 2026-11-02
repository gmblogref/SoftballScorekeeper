[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptUnderTest = Join-Path $PSScriptRoot "prepare-release-artifacts.ps1"
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SoftballScorekeeper-artifact-tests-" + [System.Guid]::NewGuid().ToString('N'))
$sourceSha = "0123456789abcdef0123456789abcdef01234567"
$releaseVersion = "2.0.1"
$apkName = "SoftballScorekeeper-$releaseVersion-01234567.apk"
$zipName = "SoftballScorekeeper-$releaseVersion-windows-x64-portable.zip"
$passed = 0
$failed = 0

function Invoke-Case {
    param([string] $Name, [scriptblock] $Body)
    try {
        & $Body
        Write-Host "PASS: $Name"
        $script:passed++
    } catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)"
        $script:failed++
    }
}

function New-CaseDirectories {
    param([string] $Name)
    $root = Join-Path $testRoot $Name
    $android = Join-Path $root "android"
    $windows = Join-Path $root "windows"
    $output = Join-Path $root "output"
    New-Item -ItemType Directory -Path $android, $windows | Out-Null
    return [pscustomobject]@{ Android = $android; Windows = $windows; Output = $output }
}

function Invoke-Preparation {
    param([psobject] $Directories)
    & $scriptUnderTest `
        -AndroidArtifactDirectory $Directories.Android `
        -WindowsArtifactDirectory $Directories.Windows `
        -OutputDirectory $Directories.Output `
        -ReleaseVersion $releaseVersion `
        -AndroidVersionCode "201" `
        -SourceSha $sourceSha `
        -AndroidVerifierPath $androidVerifier `
        -WindowsVerifierPath $windowsVerifier
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $androidVerifier = Join-Path $testRoot "mock-android-verifier.ps1"
    $windowsVerifier = Join-Path $testRoot "mock-windows-verifier.ps1"
    [System.IO.File]::WriteAllText($androidVerifier, 'param($ApkPath,$ReleaseVersion,$AndroidVersionCode,$SourceSha) if (-not (Test-Path -LiteralPath $ApkPath)) { throw "missing apk" }')
    [System.IO.File]::WriteAllText($windowsVerifier, 'param($InputZip,$ReleaseVersion) if (-not (Test-Path -LiteralPath $InputZip)) { throw "missing zip" }')

    Invoke-Case "prepares only the two packages and standard checksum file" {
        $directories = New-CaseDirectories -Name "valid"
        [System.IO.File]::WriteAllText((Join-Path $directories.Android $apkName), "apk-content")
        [System.IO.File]::WriteAllText((Join-Path $directories.Windows $zipName), "zip-content")
        Invoke-Preparation -Directories $directories | Out-Null
        $names = @(Get-ChildItem -LiteralPath $directories.Output -File | ForEach-Object Name | Sort-Object)
        if (($names -join ',') -cne ((@('SHA256SUMS.txt', $apkName, $zipName) | Sort-Object) -join ',')) {
            throw "Unexpected final inventory: $($names -join ', ')"
        }
        $lines = @(Get-Content -LiteralPath (Join-Path $directories.Output "SHA256SUMS.txt"))
        if ($lines.Count -ne 2 -or @($lines | Where-Object { $_ -notmatch '^[0-9a-f]{64}  [^/\\]+$' }).Count -ne 0) {
            throw "Checksum file is not in standard two-entry format."
        }
        foreach ($line in $lines) {
            $hash, $name = $line -split '  ', 2
            $actual = (Get-FileHash -LiteralPath (Join-Path $directories.Output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($hash -cne $actual) { throw "Checksum mismatch for $name" }
        }
    }

    Invoke-Case "rejects a missing Android package" {
        $directories = New-CaseDirectories -Name "missing"
        [System.IO.File]::WriteAllText((Join-Path $directories.Windows $zipName), "zip-content")
        try { Invoke-Preparation -Directories $directories | Out-Null; throw "Expected failure was not raised." }
        catch { if ($_.Exception.Message -notmatch 'exactly one file') { throw } }
    }

    Invoke-Case "rejects duplicate Android outputs" {
        $directories = New-CaseDirectories -Name "duplicate"
        [System.IO.File]::WriteAllText((Join-Path $directories.Android $apkName), "apk-content")
        [System.IO.File]::WriteAllText((Join-Path $directories.Android "unexpected.apk"), "extra")
        [System.IO.File]::WriteAllText((Join-Path $directories.Windows $zipName), "zip-content")
        try { Invoke-Preparation -Directories $directories | Out-Null; throw "Expected failure was not raised." }
        catch { if ($_.Exception.Message -notmatch 'exactly one file') { throw } }
    }

    Invoke-Case "rejects an artifact with the wrong exact name" {
        $directories = New-CaseDirectories -Name "wrong-name"
        [System.IO.File]::WriteAllText((Join-Path $directories.Android "wrong.apk"), "apk-content")
        [System.IO.File]::WriteAllText((Join-Path $directories.Windows $zipName), "zip-content")
        try { Invoke-Preparation -Directories $directories | Out-Null; throw "Expected failure was not raised." }
        catch { if ($_.Exception.Message -notmatch 'must be named') { throw } }
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Host "$passed passed, $failed failed"
if ($failed -ne 0) {
    exit 1
}
