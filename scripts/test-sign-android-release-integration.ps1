[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $UnsignedApkPath,

    [Parameter(Mandatory = $true)]
    [string] $BuildToolsDirectory,

    [Parameter(Mandatory = $true)]
    [string] $KeyToolPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "android-apksigner-output.ps1")

function Resolve-RequiredFile {
    param([string] $Path, [string] $Description)

    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Description was not found at '$resolved'."
    }
    return $resolved
}

function Invoke-ExternalTool {
    param(
        [string] $Description,
        [string] $Executable,
        [string[]] $Arguments,
        [switch] $AllowFailure
    )

    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode. $($output -join [Environment]::NewLine)"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

$unsignedApk = Resolve-RequiredFile -Path $UnsignedApkPath -Description "Unsigned integration-test APK"
$keyTool = Resolve-RequiredFile -Path $KeyToolPath -Description "Java keytool"
$buildTools = [System.IO.Path]::GetFullPath($BuildToolsDirectory)
$toolSuffix = if ($env:OS -eq "Windows_NT") { ".exe" } else { "" }
$apkSignerName = if ($env:OS -eq "Windows_NT") { "apksigner.bat" } else { "apksigner" }
$zipAlign = Resolve-RequiredFile -Path (Join-Path $buildTools "zipalign$toolSuffix") -Description "Android SDK zipalign"
$apkSigner = Resolve-RequiredFile -Path (Join-Path $buildTools $apkSignerName) -Description "Android SDK apksigner"
$aapt2 = Resolve-RequiredFile -Path (Join-Path $buildTools "aapt2$toolSuffix") -Description "Android SDK aapt2"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-real-signing-tests-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $keystore = Join-Path $temporaryRoot "disposable.p12"
    $storePasswordFile = Join-Path $temporaryRoot "store-password.txt"
    $keyPasswordFile = Join-Path $temporaryRoot "key-password.txt"
    $alignedApk = Join-Path $temporaryRoot "aligned-unsigned.apk"
    $signedApk = Join-Path $temporaryRoot "signed.apk"
    $tamperedApk = Join-Path $temporaryRoot "tampered.apk"
    $password = "disposable-" + [Guid]::NewGuid().ToString("N")
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($storePasswordFile, "$password`n", $utf8WithoutBom)
    [System.IO.File]::WriteAllText($keyPasswordFile, "$password`n", $utf8WithoutBom)

    Invoke-ExternalTool `
        -Description "Disposable key generation" `
        -Executable $keyTool `
        -Arguments @(
            "-genkeypair",
            "-alias", "disposable",
            "-keyalg", "RSA",
            "-keysize", "2048",
            "-validity", "1",
            "-dname", "CN=Disposable Android Signing Integration",
            "-keystore", $keystore,
            "-storetype", "PKCS12",
            "-storepass:file", $storePasswordFile,
            "-keypass:file", $keyPasswordFile,
            "-noprompt"
        ) | Out-Null

    $unsignedVerification = Invoke-ExternalTool `
        -Description "Unsigned APK rejection" `
        -Executable $apkSigner `
        -Arguments @("verify", "--verbose", $unsignedApk) `
        -AllowFailure
    if ($unsignedVerification.ExitCode -eq 0) {
        throw "The real-tool integration fixture must be unsigned before signing."
    }

    Invoke-ExternalTool `
        -Description "Real APK alignment" `
        -Executable $zipAlign `
        -Arguments @("-f", "-p", "4", $unsignedApk, $alignedApk) | Out-Null
    Invoke-ExternalTool `
        -Description "Real APK signing" `
        -Executable $apkSigner `
        -Arguments @(
            "sign",
            "--ks", $keystore,
            "--ks-key-alias", "disposable",
            "--ks-pass", "file:$storePasswordFile",
            "--key-pass", "file:$keyPasswordFile",
            "--v1-signing-enabled", "true",
            "--v2-signing-enabled", "true",
            "--v3-signing-enabled", "true",
            "--v4-signing-enabled", "false",
            "--out", $signedApk,
            $alignedApk
        ) | Out-Null

    $verification = Invoke-ExternalTool `
        -Description "Real APK signature verification" `
        -Executable $apkSigner `
        -Arguments @("verify", "--verbose", "--print-certs-pem", $signedApk)
    $verificationText = $verification.Output -join [Environment]::NewLine
    foreach ($scheme in @("v1", "v2", "v3")) {
        if ($verificationText -notmatch "Verified using $scheme scheme .*:\s*true") {
            throw "Real-tool integration APK did not verify under signature scheme $scheme."
        }
    }
    if ($verificationText -notmatch "Verified using v4 scheme .*:\s*false") {
        throw "Real-tool integration APK unexpectedly contained a v4 sidecar signature."
    }

    $signerInfo = ConvertFrom-ApkSignerVerificationOutput -Output $verificationText
    if ($signerInfo.SignerCount -ne 1 -or $signerInfo.CertificateSha256.Count -ne 1) {
        throw "Real-tool integration APK did not produce exactly one verified signing certificate."
    }
    if ($signerInfo.CertificateSha256[0] -ceq "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05") {
        throw "Disposable integration signing unexpectedly used the production certificate."
    }

    $productionVerifierApk = Join-Path $temporaryRoot "SoftballScorekeeper-2.0.1-aaaaaaaa.apk"
    Copy-Item -LiteralPath $signedApk -Destination $productionVerifierApk
    $productionVerifierPassed = $true
    $productionVerifierFailure = $null
    try {
        & (Join-Path $PSScriptRoot "verify-android-release.ps1") `
            -ApkPath $productionVerifierApk `
            -ReleaseVersion "2.0.1" `
            -AndroidVersionCode "7" `
            -SourceSha "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" `
            -ZipAlignPath $zipAlign `
            -ApkSignerPath $apkSigner `
            -Aapt2Path $aapt2 | Out-Null
    }
    catch {
        $productionVerifierPassed = $false
        $productionVerifierFailure = $_.Exception.Message
    }
    if ($productionVerifierPassed -or
        $productionVerifierFailure.IndexOf("certificate mismatch", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "The production verifier did not reject the disposable signer specifically as a certificate mismatch. $productionVerifierFailure"
    }

    Copy-Item -LiteralPath $signedApk -Destination $tamperedApk
    $tamperedBytes = [System.IO.File]::ReadAllBytes($tamperedApk)
    $tamperOffset = [Math]::Floor($tamperedBytes.Length / 2)
    $tamperedBytes[$tamperOffset] = $tamperedBytes[$tamperOffset] -bxor 1
    [System.IO.File]::WriteAllBytes($tamperedApk, $tamperedBytes)
    $tamperedVerification = Invoke-ExternalTool `
        -Description "Tampered APK rejection" `
        -Executable $apkSigner `
        -Arguments @("verify", "--verbose", "--print-certs-pem", $tamperedApk) `
        -AllowFailure
    if ($tamperedVerification.ExitCode -eq 0) {
        throw "Real apksigner accepted an APK modified after signing."
    }

    Write-Host "PASS: real apksigner signed and verified one disposable signer with v1/v2/v3 and no v4."
    Write-Host "PASS: production verification recognized the signer and rejected its non-production certificate."
    Write-Host "PASS: real apksigner rejected unsigned and post-signing-tampered APKs."
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
