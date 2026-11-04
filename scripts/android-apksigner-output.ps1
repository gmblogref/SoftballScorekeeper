Set-StrictMode -Version Latest

function ConvertFrom-ApkSignerVerificationOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Output
    )

    $signerCountMatches = [regex]::Matches(
        $Output,
        "(?m)^Number of signers:\s*([0-9]+)\s*$"
    )
    if ($signerCountMatches.Count -ne 1) {
        throw "APK signer verification output must contain exactly one signer-count declaration."
    }

    $signerCount = 0
    if (-not [int]::TryParse($signerCountMatches[0].Groups[1].Value, [ref] $signerCount)) {
        throw "APK signer verification output contains an invalid signer count."
    }

    $certificateMatches = [regex]::Matches(
        $Output,
        "-----BEGIN CERTIFICATE-----\s*(?<Body>[A-Za-z0-9+/\r\n]+={0,2})\s*-----END CERTIFICATE-----"
    )
    if ($certificateMatches.Count -ne $signerCount) {
        throw "APK signer verification reported $signerCount signer(s) but emitted $($certificateMatches.Count) signing certificate(s)."
    }

    $certificateSha256 = @(
        foreach ($certificateMatch in $certificateMatches) {
            $encodedCertificate = $certificateMatch.Groups["Body"].Value -replace "\s", ""
            try {
                $certificateBytes = [Convert]::FromBase64String($encodedCertificate)
                $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificateBytes)
                try {
                    $sha256 = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        (([BitConverter]::ToString($sha256.ComputeHash($certificate.RawData))) -replace "-", "").ToUpperInvariant()
                    }
                    finally {
                        $sha256.Dispose()
                    }
                }
                finally {
                    $certificate.Dispose()
                }
            }
            catch {
                throw "APK signer verification emitted an invalid signing certificate. $($_.Exception.Message)"
            }
        }
    )

    return [pscustomobject]@{
        SignerCount = $signerCount
        CertificateSha256 = $certificateSha256
    }
}
