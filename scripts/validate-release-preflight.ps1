[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [string] $AndroidVersionCode,

    [Parameter(Mandatory = $true)]
    [string] $EventName,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedRef,

    [Parameter(Mandatory = $true)]
    [string] $SourceSha,

    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string] $ProjectPath = "SoftballScorekeeper/SoftballScorekeeper.csproj",

    [string] $GitHubOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [switch] $AllowFailure
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & git @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode. $($output -join [Environment]::NewLine)"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output)
    }
}

function Assert-GitHubResourceMissing {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ApiPath,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & gh api --method GET --header "Accept: application/vnd.github+json" $ApiPath 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $message = $output -join [Environment]::NewLine

    if ($exitCode -eq 0) {
        throw "$Description already exists. Existing release resources are never overwritten or moved."
    }

    if ($message -notmatch "HTTP 404") {
        throw "Unable to determine whether $Description exists. GitHub API lookup failed with exit code $exitCode. $message"
    }
}

function Get-ProjectVersions {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Application project was not found at '$Path'."
    }

    [xml] $project = Get-Content -LiteralPath $Path -Raw
    $displayVersions = @($project.SelectNodes("/Project/PropertyGroup/ApplicationDisplayVersion") | ForEach-Object { $_.InnerText })
    $applicationVersions = @($project.SelectNodes("/Project/PropertyGroup/ApplicationVersion") | ForEach-Object { $_.InnerText })

    if ($displayVersions.Count -ne 1 -or $applicationVersions.Count -ne 1) {
        throw "The application project must contain exactly one ApplicationDisplayVersion and one ApplicationVersion."
    }

    return [pscustomobject]@{
        DisplayVersion = [string] $displayVersions[0]
        ApplicationVersion = [string] $applicationVersions[0]
    }
}

function Assert-VersionCodeNotUsedByVersionTag {
    param(
        [Parameter(Mandatory = $true)]
        [long] $VersionCode,

        [Parameter(Mandatory = $true)]
        [string] $RelativeProjectPath
    )

    $tagsResult = Invoke-Git -Arguments @("tag", "--list", "v[0-9]*")
    foreach ($tag in $tagsResult.Output) {
        $tagName = [string] $tag
        if ([string]::IsNullOrWhiteSpace($tagName)) {
            continue
        }

        $projectResult = Invoke-Git -Arguments @("show", "${tagName}:$RelativeProjectPath") -AllowFailure
        if ($projectResult.ExitCode -ne 0) {
            continue
        }

        try {
            [xml] $taggedProject = $projectResult.Output -join [Environment]::NewLine
            $taggedVersionValues = @($taggedProject.SelectNodes("/Project/PropertyGroup/ApplicationVersion") | ForEach-Object { $_.InnerText })
            if ($taggedVersionValues.Count -ne 1) {
                throw "The application project at version tag $tagName must contain exactly one ApplicationVersion."
            }
            if ([long] $taggedVersionValues[0] -eq $VersionCode) {
                throw "Android version code $VersionCode was already used by version tag $tagName."
            }
        }
        catch [System.Management.Automation.RuntimeException] {
            throw
        }
        catch {
            throw "Unable to inspect ApplicationVersion at version tag $tagName. $($_.Exception.Message)"
        }
    }
}

Push-Location -LiteralPath $RepositoryRoot
try {
    if ($EventName -ne "workflow_dispatch") {
        throw "Manual release preflight can run only for workflow_dispatch. Received '$EventName'."
    }

    if ($ExpectedRef -ne "refs/heads/main") {
        throw "Manual releases must be dispatched from main. Received '$ExpectedRef'."
    }

    if ($ReleaseVersion -notmatch "^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$") {
        throw "Release version '$ReleaseVersion' must use strict major.minor.patch format without a leading v or prerelease suffix."
    }

    if ($AndroidVersionCode -notmatch "^[1-9][0-9]*$") {
        throw "Android version code '$AndroidVersionCode' must be a positive integer."
    }

    $parsedVersionCode = 0L
    if (-not [long]::TryParse($AndroidVersionCode, [ref] $parsedVersionCode) -or $parsedVersionCode -gt 2100000000L) {
        throw "Android version code '$AndroidVersionCode' must be between 1 and 2100000000."
    }

    if ($SourceSha -notmatch "^[0-9a-fA-F]{40}$") {
        throw "Source SHA '$SourceSha' is not a full 40-character Git commit SHA."
    }

    if ($Repository -notmatch "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") {
        throw "GitHub repository '$Repository' must use owner/name format."
    }

    $headResult = Invoke-Git -Arguments @("rev-parse", "HEAD")
    $headSha = ([string] $headResult.Output[0]).Trim()
    if ($headSha -ne $SourceSha.ToLowerInvariant()) {
        throw "Checked-out HEAD '$headSha' does not match dispatch source '$SourceSha'."
    }

    $commitResult = Invoke-Git -Arguments @("cat-file", "-e", "$SourceSha^{commit}") -AllowFailure
    if ($commitResult.ExitCode -ne 0) {
        throw "Dispatch source '$SourceSha' is not an available Git commit."
    }

    $mainRefResult = Invoke-Git -Arguments @("show-ref", "--verify", "refs/remotes/origin/main") -AllowFailure
    if ($mainRefResult.ExitCode -ne 0) {
        throw "Remote-tracking main was not fetched; source membership cannot be verified safely."
    }

    $ancestorResult = Invoke-Git -Arguments @("merge-base", "--is-ancestor", $SourceSha, "refs/remotes/origin/main") -AllowFailure
    if ($ancestorResult.ExitCode -ne 0) {
        throw "Dispatch source '$SourceSha' is not contained in merged origin/main history."
    }

    $projectVersions = Get-ProjectVersions -Path $ProjectPath
    if ($projectVersions.DisplayVersion -ne $ReleaseVersion) {
        throw "Release version '$ReleaseVersion' does not match committed ApplicationDisplayVersion '$($projectVersions.DisplayVersion)'."
    }

    if ($projectVersions.ApplicationVersion -ne $AndroidVersionCode) {
        throw "Android version code '$AndroidVersionCode' does not match committed ApplicationVersion '$($projectVersions.ApplicationVersion)'."
    }

    $releaseTag = "v$ReleaseVersion"
    Assert-GitHubResourceMissing -ApiPath "repos/$Repository/git/ref/tags/$releaseTag" -Description "Git tag $releaseTag"
    Assert-GitHubResourceMissing -ApiPath "repos/$Repository/releases/tags/$releaseTag" -Description "GitHub Release $releaseTag"
    Assert-VersionCodeNotUsedByVersionTag -VersionCode $parsedVersionCode -RelativeProjectPath $ProjectPath

    if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
        Add-Content -LiteralPath $GitHubOutputPath -Value "release_version=$ReleaseVersion"
        Add-Content -LiteralPath $GitHubOutputPath -Value "android_version_code=$AndroidVersionCode"
        Add-Content -LiteralPath $GitHubOutputPath -Value "release_tag=$releaseTag"
        Add-Content -LiteralPath $GitHubOutputPath -Value "source_sha=$headSha"
    }

    Write-Host "Release preflight passed for $releaseTag at source $headSha with Android version code $AndroidVersionCode."
}
finally {
    Pop-Location
}
