[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$signingScript = Join-Path $PSScriptRoot "sign-android-release.ps1"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-signing-tests-" + [Guid]::NewGuid().ToString("N"))
$toolsDirectory = Join-Path $temporaryRoot "tools"
$logsDirectory = Join-Path $temporaryRoot "logs"
$inputDirectory = Join-Path $temporaryRoot "inputs"
$keystorePath = Join-Path $temporaryRoot "production.keystore"
$storePasswordFile = Join-Path $temporaryRoot "store-password.txt"
$keyPasswordFile = Join-Path $temporaryRoot "key-password.txt"
$sourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
$originalRunnerTemp = $env:RUNNER_TEMP
$passed = 0
$failed = 0

function Invoke-SigningCase {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Mode,

        [Parameter(Mandatory = $true)]
        [bool] $ShouldPass,

        [string] $ExpectedFailure
    )

    $caseDirectory = Join-Path $temporaryRoot ($Name -replace "[^A-Za-z0-9.-]", "-")
    $outputDirectory = Join-Path $caseDirectory "output"
    $logPath = Join-Path $logsDirectory (($Name -replace "[^A-Za-z0-9.-]", "-") + ".log")
    New-Item -ItemType Directory -Path $caseDirectory, $outputDirectory -Force | Out-Null
    $env:SIGNING_TEST_MODE = $Mode
    $env:SIGNING_TEST_LOG = $logPath

    $arguments = @{
        UnsignedApkPath = Join-Path $inputDirectory "com.companyname.softballscorekeeper.apk"
        ReleaseVersion = "2.0.1"
        AndroidVersionCode = "7"
        SourceSha = $sourceSha
        KeystorePath = $keystorePath
        KeyAlias = "softballscorekeeper"
        StorePasswordFile = $storePasswordFile
        KeyPasswordFile = $keyPasswordFile
        OutputDirectory = $outputDirectory
        ZipAlignPath = Join-Path $toolsDirectory "zipalign.ps1"
        ApkSignerPath = Join-Path $toolsDirectory "apksigner.ps1"
        Aapt2Path = Join-Path $toolsDirectory "aapt2.ps1"
    }

    $didPass = $true
    $failureMessage = $null
    $result = $null
    try {
        $result = & $signingScript @arguments
    }
    catch {
        $didPass = $false
        $failureMessage = $_.Exception.Message
    }

    $workingDirectories = @(Get-ChildItem -LiteralPath $outputDirectory -Directory -Filter ".android-signing-*" -ErrorAction SilentlyContinue)
    if ($workingDirectories.Count -ne 0) {
        throw "Signing case '$Name' left an intermediate signing directory."
    }

    $expectedArtifact = Join-Path $outputDirectory "SoftballScorekeeper-2.0.1-aaaaaaaa.apk"
    if ($ShouldPass) {
        if (-not $didPass) {
            throw "Signing case '$Name' unexpectedly failed. $failureMessage"
        }
        if (-not (Test-Path -LiteralPath $expectedArtifact -PathType Leaf)) {
            throw "Signing case '$Name' did not create the expected definitive artifact name."
        }
        if ($result.FileName -cne "SoftballScorekeeper-2.0.1-aaaaaaaa.apk") {
            throw "Signing case '$Name' returned an unexpected artifact name '$($result.FileName)'."
        }
        if ($result.CertificateSha256 -cne "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05") {
            throw "Signing case '$Name' returned an unexpected certificate fingerprint."
        }
        if (($result.Abis -join ",") -cne "arm64-v8a,x86_64") {
            throw "Signing case '$Name' returned unexpected ABIs."
        }

        $toolLog = Get-Content -LiteralPath $logPath -Raw
        foreach ($required in @(
                "--v1-signing-enabled true",
                "--v2-signing-enabled true",
                "--v3-signing-enabled true",
                "--v4-signing-enabled false",
                "--ks-pass file:",
                "--key-pass file:",
                "zipalign -f -p 4",
                "zipalign -c -p 4"
            )) {
            if ($toolLog.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
                throw "Signing case '$Name' did not record required command structure '$required'."
            }
        }
        if ($toolLog.IndexOf("store-secret", [StringComparison]::Ordinal) -ge 0 -or
            $toolLog.IndexOf("key-secret", [StringComparison]::Ordinal) -ge 0) {
            throw "Signing case '$Name' exposed a password value to a tool command."
        }
    }
    else {
        if ($didPass) {
            throw "Signing case '$Name' unexpectedly passed."
        }
        if (-not [string]::IsNullOrWhiteSpace($ExpectedFailure) -and
            $failureMessage.IndexOf($ExpectedFailure, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Signing case '$Name' failed for an unexpected reason. $failureMessage"
        }
        if (Test-Path -LiteralPath $expectedArtifact) {
            throw "Signing case '$Name' retained a failed definitive artifact."
        }
    }

    $script:passed++
    Write-Host "PASS: $Name"
}

try {
    New-Item -ItemType Directory -Path $toolsDirectory, $logsDirectory, $inputDirectory -Force | Out-Null
    [System.IO.File]::WriteAllBytes(
        (Join-Path $inputDirectory "com.companyname.softballscorekeeper.apk"),
        [byte[]] (1, 2, 3, 4)
    )
    [System.IO.File]::WriteAllText($keystorePath, "test-keystore")
    [System.IO.File]::WriteAllText($storePasswordFile, "store-secret")
    [System.IO.File]::WriteAllText($keyPasswordFile, "key-secret")
    $env:RUNNER_TEMP = $temporaryRoot

    Set-Content -LiteralPath (Join-Path $toolsDirectory "zipalign.ps1") -Encoding utf8 -Value @'
Add-Content -LiteralPath $env:SIGNING_TEST_LOG -Value ("zipalign " + ($args -join " "))
if ($args[0] -eq "-f") {
    Copy-Item -LiteralPath $args[-2] -Destination $args[-1]
}
exit 0
'@

    Set-Content -LiteralPath (Join-Path $toolsDirectory "apksigner.ps1") -Encoding utf8 -Value @'
Add-Content -LiteralPath $env:SIGNING_TEST_LOG -Value ("apksigner " + ($args -join " "))
if ($args[0] -eq "sign") {
    $outputIndex = [Array]::IndexOf($args, "--out")
    Copy-Item -LiteralPath $args[-1] -Destination $args[$outputIndex + 1]
    if ($env:SIGNING_TEST_MODE -eq "sign-failure") {
        Write-Error "simulated signing failure" -ErrorAction Continue
        exit 1
    }
    exit 0
}

$apkPath = $args[-1]
if ([System.IO.Path]::GetFileName($apkPath) -eq "com.companyname.softballscorekeeper.apk") {
    if ($env:SIGNING_TEST_MODE -eq "already-signed") { exit 0 }
    Write-Error "DOES NOT VERIFY" -ErrorAction Continue
    exit 1
}

$v3 = if ($env:SIGNING_TEST_MODE -eq "v3-false") { "false" } else { "true" }
$v4 = if ($env:SIGNING_TEST_MODE -eq "v4-true") { "true" } else { "false" }
$certificate = if ($env:SIGNING_TEST_MODE -eq "certificate-mismatch") {
    "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
} else {
    "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05"
}
Write-Output "Verified using v1 scheme (JAR signing): true"
Write-Output "Verified using v2 scheme (APK Signature Scheme v2): true"
Write-Output "Verified using v3 scheme (APK Signature Scheme v3): $v3"
Write-Output "Verified using v4 scheme (APK Signature Scheme v4): $v4"
Write-Output "Signer #1 certificate SHA-256 digest: $certificate"
if ($env:SIGNING_TEST_MODE -eq "multiple-signers") {
    Write-Output "Signer #2 certificate SHA-256 digest: $certificate"
}
exit 0
'@

    Set-Content -LiteralPath (Join-Path $toolsDirectory "aapt2.ps1") -Encoding utf8 -Value @'
Add-Content -LiteralPath $env:SIGNING_TEST_LOG -Value ("aapt2 " + ($args -join " "))
$packageId = if ($env:SIGNING_TEST_MODE -eq "metadata-mismatch") { "com.example.wrong" } else { "com.companyname.softballscorekeeper" }
$abis = if ($env:SIGNING_TEST_MODE -eq "abi-mismatch") { "'arm64-v8a'" } else { "'arm64-v8a' 'x86_64'" }
Write-Output "package: name='$packageId' versionCode='7' versionName='2.0.1'"
Write-Output "native-code: $abis"
exit 0
'@

    Invoke-SigningCase -Name "valid production signing contract" -Mode "valid" -ShouldPass $true
    Invoke-SigningCase -Name "rejects already signed input" -Mode "already-signed" -ShouldPass $false -ExpectedFailure "already signed"
    Invoke-SigningCase -Name "rejects certificate mismatch" -Mode "certificate-mismatch" -ShouldPass $false -ExpectedFailure "certificate mismatch"
    Invoke-SigningCase -Name "rejects multiple signers" -Mode "multiple-signers" -ShouldPass $false -ExpectedFailure "exactly one signer"
    Invoke-SigningCase -Name "rejects missing v3 verification" -Mode "v3-false" -ShouldPass $false -ExpectedFailure "scheme v3"
    Invoke-SigningCase -Name "rejects v4 signature" -Mode "v4-true" -ShouldPass $false -ExpectedFailure "v4"
    Invoke-SigningCase -Name "rejects package mismatch" -Mode "metadata-mismatch" -ShouldPass $false -ExpectedFailure "package mismatch"
    Invoke-SigningCase -Name "rejects ABI mismatch" -Mode "abi-mismatch" -ShouldPass $false -ExpectedFailure "ABI mismatch"
    Invoke-SigningCase -Name "cleans partial artifact after signing failure" -Mode "sign-failure" -ShouldPass $false -ExpectedFailure "signing"
}
catch {
    $failed++
    Write-Error $_
}
finally {
    Remove-Item Env:SIGNING_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:SIGNING_TEST_LOG -ErrorAction SilentlyContinue
    $env:RUNNER_TEMP = $originalRunnerTemp
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Android signing validation: $passed passed, $failed failed."
if ($failed -ne 0) {
    throw "$failed Android signing validation case(s) failed."
}
