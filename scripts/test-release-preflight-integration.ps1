[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSEdition -ne "Core" -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw "Release preflight integration validation must run under PowerShell 7 or later."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$validatorPath = Join-Path $PSScriptRoot "validate-release-preflight.ps1"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-release-preflight-integration-" + [Guid]::NewGuid().ToString("N"))
$mockBinPath = Join-Path $temporaryRoot "bin"
$originalPath = $env:PATH
$passed = 0
$failed = 0

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string] $Repository,
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $output = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Integration fixture git command failed: git -C '$Repository' $($Arguments -join ' '). $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function New-RepositoryFixture {
    param([Parameter(Mandatory = $true)][string] $Name)

    $path = Join-Path $temporaryRoot $Name
    $cloneOutput = & git clone --quiet --local --no-hardlinks $repositoryRoot $path 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to clone the real repository history for integration validation. $($cloneOutput -join [Environment]::NewLine)"
    }

    Invoke-Git -Repository $path -Arguments @("checkout", "--quiet", "--detach", "refs/remotes/origin/main") | Out-Null
    Invoke-Git -Repository $path -Arguments @("config", "user.name", "Release Preflight Integration Tests") | Out-Null
    Invoke-Git -Repository $path -Arguments @("config", "user.email", "release-preflight-integration@example.invalid") | Out-Null
    Invoke-Git -Repository $path -Arguments @("update-ref", "refs/remotes/origin/main", "HEAD") | Out-Null
    return $path
}

function Set-ProjectVersions {
    param(
        [Parameter(Mandatory = $true)][string] $Repository,
        [Parameter(Mandatory = $true)][string] $DisplayVersion,
        [Parameter(Mandatory = $true)][string] $ApplicationVersion,
        [Parameter(Mandatory = $true)][string] $Message
    )

    $projectPath = Join-Path $Repository "SoftballScorekeeper/SoftballScorekeeper.csproj"
    [xml] $project = Get-Content -LiteralPath $projectPath -Raw
    $displayNodes = @($project.SelectNodes("/Project/PropertyGroup/ApplicationDisplayVersion"))
    $applicationNodes = @($project.SelectNodes("/Project/PropertyGroup/ApplicationVersion"))
    if ($displayNodes.Count -ne 1 -or $applicationNodes.Count -ne 1) {
        throw "Integration fixture expected exactly one current display version and application version."
    }
    $displayNodes[0].InnerText = $DisplayVersion
    $applicationNodes[0].InnerText = $ApplicationVersion
    $project.Save($projectPath)
    Invoke-Git -Repository $Repository -Arguments @("add", "SoftballScorekeeper/SoftballScorekeeper.csproj") | Out-Null
    Invoke-Git -Repository $Repository -Arguments @("commit", "--quiet", "-m", $Message) | Out-Null
    Invoke-Git -Repository $Repository -Arguments @("update-ref", "refs/remotes/origin/main", "HEAD") | Out-Null
}

function Set-HistoricalProject {
    param(
        [Parameter(Mandatory = $true)][string] $Repository,
        [Parameter(Mandatory = $true)][string] $TagName,
        [Parameter(Mandatory = $true)][string] $ProjectXml
    )

    $projectPath = Join-Path $Repository "SoftballScorekeeper/SoftballScorekeeper.csproj"
    [System.IO.File]::WriteAllText($projectPath, $ProjectXml, [System.Text.UTF8Encoding]::new($false))
    Invoke-Git -Repository $Repository -Arguments @("add", "SoftballScorekeeper/SoftballScorekeeper.csproj") | Out-Null
    Invoke-Git -Repository $Repository -Arguments @("commit", "--quiet", "-m", "Create $TagName metadata fixture") | Out-Null
    Invoke-Git -Repository $Repository -Arguments @("tag", $TagName) | Out-Null
}

function Invoke-Preflight {
    param(
        [Parameter(Mandatory = $true)][string] $Repository,
        [hashtable] $Overrides = @{}
    )

    $sourceSha = [string] (Invoke-Git -Repository $Repository -Arguments @("rev-parse", "HEAD") | Select-Object -First 1)
    $sourceSha = $sourceSha.Trim()
    $arguments = @{
        ReleaseVersion = "2.0.1"
        AndroidVersionCode = "7"
        EventName = "workflow_dispatch"
        ExpectedRef = "refs/heads/main"
        SourceSha = $sourceSha
        Repository = "example/SoftballScorekeeper"
        RepositoryRoot = $Repository
    }
    foreach ($key in $Overrides.Keys) {
        $arguments[$key] = $Overrides[$key]
    }

    return & $validatorPath @arguments 2>&1
}

function Invoke-Case {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][scriptblock] $Body
    )

    try {
        & $Body
        $script:passed++
        Write-Host "PASS: $Name"
    }
    catch {
        $script:failed++
        Write-Error "FAIL: $Name -- $($_.Exception.Message)"
    }
}

function Assert-FailsLike {
    param(
        [Parameter(Mandatory = $true)][scriptblock] $Body,
        [Parameter(Mandatory = $true)][string] $Pattern
    )

    try {
        & $Body | Out-Null
    }
    catch {
        if ($_.Exception.Message -like "*$Pattern*") {
            return
        }
        throw "Expected failure '*$Pattern*' but received '$($_.Exception.Message)'."
    }
    throw "Expected failure '*$Pattern*' was not raised."
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot, $mockBinPath -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $mockBinPath "gh.ps1"), @'
$path = $args[-1]
switch ($env:PREFLIGHT_INTEGRATION_GH_MODE) {
    "tag-exists" {
        if ($path -match "/git/ref/tags/") { Write-Output "{}"; exit 0 }
    }
    "release-exists" {
        if ($path -match "/releases/tags/") { Write-Output "{}"; exit 0 }
    }
}
Write-Error "gh: Not Found (HTTP 404)" -ErrorAction Continue
exit 1
'@, [System.Text.UTF8Encoding]::new($false))
    $env:PATH = "$mockBinPath$([System.IO.Path]::PathSeparator)$originalPath"
    $env:PREFLIGHT_INTEGRATION_GH_MODE = "missing"

    $baseRepository = New-RepositoryFixture -Name "actual-history"
    Invoke-Case "extracts current and actual historical release metadata" {
        [xml] $currentProject = Get-Content -LiteralPath (Join-Path $baseRepository "SoftballScorekeeper/SoftballScorekeeper.csproj") -Raw
        $currentDisplay = @($currentProject.SelectNodes("/Project/PropertyGroup/ApplicationDisplayVersion") | ForEach-Object { $_.InnerText })
        $currentCode = @($currentProject.SelectNodes("/Project/PropertyGroup/ApplicationVersion") | ForEach-Object { $_.InnerText })
        if ($currentDisplay.Count -ne 1 -or $currentDisplay[0] -cne "2.0.1" -or $currentCode.Count -ne 1 -or $currentCode[0] -cne "7") {
            throw "Current metadata was not exactly display version 2.0.1 and code 7."
        }

        $expectedTags = [ordered]@{ "v1.0.0" = "4"; "v1.0.1" = "5"; "v2.0.0" = "6" }
        foreach ($entry in $expectedTags.GetEnumerator()) {
            $taggedProjectText = (Invoke-Git -Repository $baseRepository -Arguments @("show", "$($entry.Key):SoftballScorekeeper/SoftballScorekeeper.csproj")) -join [Environment]::NewLine
            if ($taggedProjectText.Length -gt 0 -and $taggedProjectText[0] -eq [char] 0xFEFF) {
                $taggedProjectText = $taggedProjectText.Substring(1)
            }
            [xml] $taggedProject = $taggedProjectText
            $taggedCodes = @($taggedProject.SelectNodes("/Project/PropertyGroup/ApplicationVersion") | ForEach-Object { $_.InnerText })
            if ($taggedCodes.Count -ne 1 -or $taggedCodes[0] -cne $entry.Value) {
                throw "Actual tag $($entry.Key) did not contain expected Android version code $($entry.Value)."
            }
        }
    }

    Invoke-Case "completes preflight against actual history with simulated missing GitHub resources" {
        $outputPath = Join-Path $temporaryRoot "github-output.txt"
        Invoke-Preflight -Repository $baseRepository -Overrides @{ GitHubOutputPath = $outputPath } | Out-Null
        $outputs = @(Get-Content -LiteralPath $outputPath)
        foreach ($expected in @("release_version=2.0.1", "android_version_code=7", "release_tag=v2.0.1")) {
            if ($expected -notin $outputs) { throw "Preflight output '$expected' was missing." }
        }
    }

    Invoke-Case "rejects a duplicate GitHub tag" {
        $env:PREFLIGHT_INTEGRATION_GH_MODE = "tag-exists"
        Assert-FailsLike { Invoke-Preflight -Repository $baseRepository } "Git tag v2.0.1 already exists"
        $env:PREFLIGHT_INTEGRATION_GH_MODE = "missing"
    }

    Invoke-Case "rejects an existing GitHub Release" {
        $env:PREFLIGHT_INTEGRATION_GH_MODE = "release-exists"
        Assert-FailsLike { Invoke-Preflight -Repository $baseRepository } "GitHub Release v2.0.1 already exists"
        $env:PREFLIGHT_INTEGRATION_GH_MODE = "missing"
    }

    Invoke-Case "rejects an incorrect source SHA" {
        Assert-FailsLike { Invoke-Preflight -Repository $baseRepository -Overrides @{ SourceSha = ("f" * 40) } } "does not match dispatch source"
    }

    Invoke-Case "rejects a non-main dispatch" {
        Assert-FailsLike { Invoke-Preflight -Repository $baseRepository -Overrides @{ ExpectedRef = "refs/heads/task/3.2-preflight-reliability" } } "must be dispatched from main"
    }

    $reuseRepository = New-RepositoryFixture -Name "reused-code"
    Set-ProjectVersions -Repository $reuseRepository -DisplayVersion "2.0.1" -ApplicationVersion "6" -Message "Reuse historical Android version code"
    Invoke-Case "rejects a version code from actual v2.0.0 history" {
        Assert-FailsLike { Invoke-Preflight -Repository $reuseRepository -Overrides @{ AndroidVersionCode = "6" } } "Android version code 6 was already used by version tag v2.0.0"
    }

    $missingRepository = New-RepositoryFixture -Name "missing-historical-code"
    Set-HistoricalProject -Repository $missingRepository -TagName "v9.9.8" -ProjectXml '<Project><PropertyGroup><ApplicationDisplayVersion>9.9.8</ApplicationDisplayVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationId>example</ApplicationId></PropertyGroup></Project>'
    Invoke-Git -Repository $missingRepository -Arguments @("checkout", "refs/remotes/origin/main", "--", "SoftballScorekeeper/SoftballScorekeeper.csproj") | Out-Null
    Set-ProjectVersions -Repository $missingRepository -DisplayVersion "2.0.1" -ApplicationVersion "8" -Message "Restore current metadata after missing historical code"
    Invoke-Case "fails closed on missing historical version metadata" {
        Assert-FailsLike { Invoke-Preflight -Repository $missingRepository -Overrides @{ AndroidVersionCode = "8" } } "version tag v9.9.8 must contain exactly one ApplicationVersion"
    }

    $malformedRepository = New-RepositoryFixture -Name "malformed-historical-code"
    Set-HistoricalProject -Repository $malformedRepository -TagName "v9.9.9" -ProjectXml '<Project><PropertyGroup><ApplicationDisplayVersion>9.9.9</ApplicationDisplayVersion><ApplicationVersion>not-a-number</ApplicationVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationId>example</ApplicationId></PropertyGroup></Project>'
    Invoke-Git -Repository $malformedRepository -Arguments @("checkout", "refs/remotes/origin/main", "--", "SoftballScorekeeper/SoftballScorekeeper.csproj") | Out-Null
    Set-ProjectVersions -Repository $malformedRepository -DisplayVersion "2.0.1" -ApplicationVersion "8" -Message "Restore current metadata after malformed historical code"
    Invoke-Case "fails closed on malformed historical version metadata" {
        Assert-FailsLike { Invoke-Preflight -Repository $malformedRepository -Overrides @{ AndroidVersionCode = "8" } } "ApplicationVersion at version tag v9.9.9 must be an integer between 1 and 2100000000"
    }
}
finally {
    $env:PATH = $originalPath
    Remove-Item Env:PREFLIGHT_INTEGRATION_GH_MODE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Release preflight integration validation: $passed passed, $failed failed under PowerShell $($PSVersionTable.PSVersion)."
if ($failed -ne 0) {
    throw "$failed release preflight integration case(s) failed."
}
