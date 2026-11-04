[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ReleaseVersion,
    [Parameter(Mandatory = $true)] [string] $AndroidVersionCode,
    [Parameter(Mandatory = $true)] [string] $SourceSha,
    [Parameter(Mandatory = $true)] [string] $Repository,
    [Parameter(Mandatory = $true)] [string] $ArtifactDirectory,
    [Parameter(Mandatory = $true)] [string] $ReleaseNotesPath,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $ProjectPath = 'SoftballScorekeeper/SoftballScorekeeper.csproj',
    [string] $GitHubToken,
    [string] $ApiBaseUrl = 'https://api.github.com',
    [string] $ApiInvokerPath,
    [string] $GitCommand = 'git'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$certificateSha256 = 'D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05'
$packageId = 'com.companyname.softballscorekeeper'
$tagReferenceCreated = $false
$draftReleaseCreated = $false
$published = $false
$uploadedAssets = 0
$releaseId = $null
$tagName = "v$ReleaseVersion"

function Invoke-GitRead {
    param([string[]] $Arguments, [switch] $AllowFailure)
    if ($GitCommand.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase)) {
        $mockResult = & $GitCommand -Arguments $Arguments
        if ($null -eq $mockResult -or $null -eq $mockResult.ExitCode) {
            throw 'Mock git command returned an invalid result.'
        }
        if (-not $AllowFailure -and [int] $mockResult.ExitCode -ne 0) {
            throw "git $($Arguments -join ' ') failed with exit code $($mockResult.ExitCode). $($mockResult.Output -join [Environment]::NewLine)"
        }
        return [pscustomobject]@{ ExitCode = [int] $mockResult.ExitCode; Output = @($mockResult.Output) }
    }
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $GitCommand @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode. $($output -join [Environment]::NewLine)"
    }
    [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Invoke-GitHubApi {
    param(
        [string] $Method,
        [string] $Uri,
        [object] $Body,
        [string] $InputFile,
        [string] $ContentType = 'application/vnd.github+json'
    )

    $bodyJson = if ($null -eq $Body) { '' } else { ConvertTo-Json -InputObject $Body -Depth 12 -Compress }
    if (-not [string]::IsNullOrWhiteSpace($ApiInvokerPath)) {
        Write-Verbose "Invoking mocked GitHub API: $Method $Uri"
        $mockOutput = @(& $ApiInvokerPath -Method $Method -Uri $Uri -BodyJson $bodyJson -InputFile $InputFile -ContentType $ContentType 2>&1)
        Write-Verbose "Mocked GitHub API completed: $Method $Uri"
        try {
            return (($mockOutput -join [Environment]::NewLine) | ConvertFrom-Json)
        }
        catch {
            throw "Mock GitHub API returned an invalid response envelope. $($mockOutput -join [Environment]::NewLine)"
        }
    }

    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        throw 'GitHub token is required for live publication.'
    }
    $headers = @{
        Accept = 'application/vnd.github+json'
        Authorization = "Bearer $GitHubToken"
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'SoftballScorekeeper-Release-Automation'
    }
    try {
        $parameters = @{
            Uri = $Uri
            Method = $Method
            Headers = $headers
            UseBasicParsing = $true
        }
        if (-not [string]::IsNullOrWhiteSpace($InputFile)) {
            $parameters.InFile = $InputFile
            $parameters.ContentType = $ContentType
        }
        elseif (-not [string]::IsNullOrWhiteSpace($bodyJson)) {
            $parameters.Body = $bodyJson
            $parameters.ContentType = $ContentType
        }
        $response = Invoke-WebRequest @parameters
        $parsedBody = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json }
        return [pscustomobject]@{ status_code = [int] $response.StatusCode; body = $parsedBody }
    }
    catch {
        $statusCode = 0
        $responseBody = $null
        if ($null -ne $_.Exception.Response) {
            $statusCode = [int] $_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = [System.IO.StreamReader]::new($stream)
                $content = $reader.ReadToEnd()
                $reader.Dispose()
                if (-not [string]::IsNullOrWhiteSpace($content)) {
                    $responseBody = $content | ConvertFrom-Json
                }
            }
            catch {
                $responseBody = $null
            }
        }
        return [pscustomobject]@{ status_code = $statusCode; body = $responseBody; error = $_.Exception.Message }
    }
}

function Assert-Status {
    param([object] $Response, [int[]] $Expected, [string] $Operation)
    if ([int] $Response.status_code -notin $Expected) {
        $detail = if ($null -ne $Response.body) { ConvertTo-Json -InputObject $Response.body -Depth 8 -Compress } else { [string] $Response.error }
        throw "$Operation failed with HTTP status $($Response.status_code). $detail"
    }
}

function Get-ExpectedArtifacts {
    $root = [System.IO.Path]::GetFullPath($ArtifactDirectory)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "Final release artifact directory does not exist: $root"
    }
    $shortSha = $SourceSha.Substring(0, 8).ToLowerInvariant()
    $apkName = "SoftballScorekeeper-$ReleaseVersion-$shortSha.apk"
    $zipName = "SoftballScorekeeper-$ReleaseVersion-windows-x64-portable.zip"
    $expectedNames = @($apkName, $zipName, 'SHA256SUMS.txt')
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
    if ($files.Count -ne 3 -or @($files.Name | Where-Object { $_ -cnotin $expectedNames }).Count -ne 0) {
        throw "Final release artifact directory must contain exactly '$apkName', '$zipName', and 'SHA256SUMS.txt'."
    }
    foreach ($name in $expectedNames) {
        if (@($files | Where-Object Name -CEQ $name).Count -ne 1) {
            throw "Final release artifact directory must contain exactly one '$name'."
        }
    }

    $checksums = @{}
    foreach ($line in Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt')) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  ([^/\\]+)$') {
            throw "SHA256SUMS.txt contains an invalid entry: $line"
        }
        $name = $Matches[2]
        if ($checksums.ContainsKey($name)) {
            throw "SHA256SUMS.txt contains duplicate entry '$name'."
        }
        $checksums[$name] = $Matches[1].ToLowerInvariant()
    }
    if ($checksums.Count -ne 2 -or -not $checksums.ContainsKey($apkName) -or -not $checksums.ContainsKey($zipName)) {
        throw 'SHA256SUMS.txt must contain exactly the definitive Android and Windows packages.'
    }
    foreach ($name in @($apkName, $zipName)) {
        $actual = (Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $checksums[$name]) {
            throw "SHA-256 mismatch for '$name'."
        }
    }
    [pscustomobject]@{
        Root = $root
        ApkName = $apkName
        ZipName = $zipName
        ApkSha256 = $checksums[$apkName]
        ZipSha256 = $checksums[$zipName]
        Files = @($expectedNames | ForEach-Object { Join-Path $root $_ })
    }
}

try {
    if ($ReleaseVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "Release version '$ReleaseVersion' must use strict major.minor.patch format."
    }
    if ($AndroidVersionCode -notmatch '^[1-9][0-9]*$' -or $SourceSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Android version code or source SHA is invalid.'
    }
    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        throw "GitHub repository '$Repository' must use owner/name format."
    }

    Push-Location -LiteralPath $RepositoryRoot
    try {
        $head = (Invoke-GitRead -Arguments @('rev-parse', 'HEAD')).Output[0].Trim()
        if ($head -cne $SourceSha.ToLowerInvariant()) {
            throw "Checked-out source '$head' does not match validated source '$SourceSha'."
        }
        if ((Invoke-GitRead -Arguments @('show-ref', '--verify', 'refs/remotes/origin/main') -AllowFailure).ExitCode -ne 0 -or
            (Invoke-GitRead -Arguments @('merge-base', '--is-ancestor', $SourceSha, 'refs/remotes/origin/main') -AllowFailure).ExitCode -ne 0) {
            throw "Validated source '$SourceSha' is not confirmed in merged origin/main history."
        }
        $projectFile = Join-Path $RepositoryRoot $ProjectPath
        [xml] $project = Get-Content -LiteralPath $projectFile -Raw
        $display = @($project.SelectNodes('/Project/PropertyGroup/ApplicationDisplayVersion') | ForEach-Object { $_.InnerText })
        $code = @($project.SelectNodes('/Project/PropertyGroup/ApplicationVersion') | ForEach-Object { $_.InnerText })
        if ($display.Count -ne 1 -or [string] $display[0] -cne $ReleaseVersion -or
            $code.Count -ne 1 -or [string] $code[0] -cne $AndroidVersionCode) {
            throw 'Release version or Android version code does not match the checked-out application project.'
        }
    }
    finally {
        Pop-Location
    }

    $artifacts = Get-ExpectedArtifacts
    if (-not (Test-Path -LiteralPath $ReleaseNotesPath -PathType Leaf)) {
        throw "Tracked release notes are missing: $ReleaseNotesPath"
    }
    $notes = Get-Content -LiteralPath $ReleaseNotesPath -Raw
    if ([string]::IsNullOrWhiteSpace($notes) -or $notes -match '\{\{[A-Z0-9_]+\}\}') {
        throw 'Release notes are empty or contain unresolved template placeholders.'
    }
    if ($notes.IndexOf($ReleaseVersion, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Release notes do not identify release version '$ReleaseVersion'."
    }
    $publishedNotes = @"
$($notes.TrimEnd())

## Verified release metadata

- Source commit: ``$($SourceSha.ToLowerInvariant())``
- Android: ``$($artifacts.ApkName)``
  - SHA-256: ``$($artifacts.ApkSha256)``
  - Package ID: ``$packageId``
  - Android version code: ``$AndroidVersionCode``
  - Production signing certificate SHA-256: ``$certificateSha256``
- Windows x64 portable: ``$($artifacts.ZipName)``
  - SHA-256: ``$($artifacts.ZipSha256)``
- Checksums: ``SHA256SUMS.txt``
"@

    $apiRoot = "$($ApiBaseUrl.TrimEnd('/'))/repos/$Repository"
    $tagLookup = Invoke-GitHubApi -Method GET -Uri "$apiRoot/git/ref/tags/$tagName"
    if ($tagLookup.status_code -eq 200) { throw "Git tag $tagName already exists. Existing tags are never overwritten or moved." }
    Assert-Status -Response $tagLookup -Expected @(404) -Operation "Git tag $tagName lookup"
    $releaseLookup = Invoke-GitHubApi -Method GET -Uri "$apiRoot/releases/tags/$tagName"
    if ($releaseLookup.status_code -eq 200) { throw "GitHub Release $tagName already exists. Existing releases are never overwritten." }
    Assert-Status -Response $releaseLookup -Expected @(404) -Operation "GitHub Release $tagName lookup"

    $tagMessage = @"
Softball Scorekeeper $ReleaseVersion

Source SHA: $($SourceSha.ToLowerInvariant())
Android: $($artifacts.ApkName)  SHA-256 $($artifacts.ApkSha256)
Windows: $($artifacts.ZipName)  SHA-256 $($artifacts.ZipSha256)
"@
    Write-Host "Creating annotated tag object for $tagName at $SourceSha..."
    $tagObject = Invoke-GitHubApi -Method POST -Uri "$apiRoot/git/tags" -Body @{
        tag = $tagName; message = $tagMessage.Trim(); object = $SourceSha.ToLowerInvariant(); type = 'commit'
        tagger = @{ name = 'github-actions[bot]'; email = '41898282+github-actions[bot]@users.noreply.github.com'; date = [DateTime]::UtcNow.ToString('o') }
    }
    Assert-Status -Response $tagObject -Expected @(201) -Operation "Annotated tag object creation for $tagName"
    if ([string] $tagObject.body.sha -notmatch '^[0-9a-fA-F]{40}$') { throw 'GitHub did not return an annotated tag-object SHA.' }

    Write-Host "Creating protected tag reference refs/tags/$tagName..."
    $tagReference = Invoke-GitHubApi -Method POST -Uri "$apiRoot/git/refs" -Body @{ ref = "refs/tags/$tagName"; sha = [string] $tagObject.body.sha }
    Assert-Status -Response $tagReference -Expected @(201) -Operation "Annotated tag reference creation for $tagName"
    $tagReferenceCreated = $true

    Write-Host "Creating draft GitHub Release for $tagName..."
    $draftRequest = @{}
    $draftRequest['tag_name'] = $tagName
    $draftRequest['target_commitish'] = $SourceSha.ToLowerInvariant()
    $draftRequest['name'] = "Softball Scorekeeper $tagName"
    $draftRequest['body'] = $publishedNotes.Trim()
    $draftRequest['draft'] = $true
    $draftRequest['prerelease'] = $false
    $draftRequest['generate_release_notes'] = $false
    $draft = Invoke-GitHubApi -Method POST -Uri "$apiRoot/releases" -Body $draftRequest
    Assert-Status -Response $draft -Expected @(201) -Operation "Draft GitHub Release creation for $tagName"
    $releaseId = [long] $draft.body.id
    if ($releaseId -le 0 -or $draft.body.draft -ne $true) { throw 'GitHub did not return a valid draft release.' }
    $draftReleaseCreated = $true
    $uploadTemplate = [string] $draft.body.upload_url
    if ([string]::IsNullOrWhiteSpace($uploadTemplate)) { throw 'Draft release did not provide an asset upload URL.' }
    $uploadBase = $uploadTemplate.Split('{')[0]

    foreach ($file in $artifacts.Files) {
        $name = [System.IO.Path]::GetFileName($file)
        Write-Host "Uploading verified draft asset $name..."
        $upload = Invoke-GitHubApi -Method POST -Uri "${uploadBase}?name=$([Uri]::EscapeDataString($name))" -InputFile $file -ContentType 'application/octet-stream'
        Assert-Status -Response $upload -Expected @(201) -Operation "Release asset upload for $name"
        $uploadedAssets++
    }

    Write-Host 'Verifying draft metadata and remote asset digests...'
    $draftVerification = Invoke-GitHubApi -Method GET -Uri "$apiRoot/releases/$releaseId"
    Assert-Status -Response $draftVerification -Expected @(200) -Operation 'Draft release verification'
    if ($draftVerification.body.draft -ne $true -or $draftVerification.body.prerelease -eq $true -or [string] $draftVerification.body.tag_name -cne $tagName) {
        throw 'Draft release metadata is inconsistent with the requested stable release.'
    }
    $remoteAssets = @($draftVerification.body.assets)
    if ($remoteAssets.Count -ne 3) { throw "Draft release contains $($remoteAssets.Count) assets; expected exactly 3." }
    foreach ($file in $artifacts.Files) {
        $name = [System.IO.Path]::GetFileName($file)
        $remote = @($remoteAssets | Where-Object { $_.name -ceq $name })
        $expectedDigest = "sha256:$((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant())"
        if ($remote.Count -ne 1 -or $remote[0].state -cne 'uploaded' -or [long] $remote[0].size -ne (Get-Item -LiteralPath $file).Length -or [string] $remote[0].digest -cne $expectedDigest) {
            throw "Uploaded release asset '$name' did not verify by exact name, state, size, and SHA-256 digest."
        }
    }

    Write-Host "Publishing verified draft release $tagName..."
    $publication = Invoke-GitHubApi -Method PATCH -Uri "$apiRoot/releases/$releaseId" -Body @{ draft = $false; prerelease = $false }
    Assert-Status -Response $publication -Expected @(200) -Operation "Final publication for $tagName"
    if ($publication.body.draft -ne $false -or $publication.body.prerelease -ne $false) {
        throw 'GitHub did not confirm stable release publication.'
    }
    $published = $true
    Write-Host "Published stable GitHub Release $tagName from source $SourceSha with three verified assets."
}
catch {
    Write-Error "Release publication failed. tag_reference_created=$tagReferenceCreated; draft_release_created=$draftReleaseCreated; release_id=$releaseId; uploaded_assets=$uploadedAssets; published=$published. No tag, draft, release, or asset was deleted. $($_.Exception.Message)" -ErrorAction Continue
    throw
}
