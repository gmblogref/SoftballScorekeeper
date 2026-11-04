[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$validatorPath = Join-Path $PSScriptRoot "validate-release-preflight.ps1"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("softball-release-preflight-" + [Guid]::NewGuid().ToString("N"))
$repositoryPath = Join-Path $temporaryRoot "repository"
$mockBinPath = Join-Path $temporaryRoot "bin"
$originalPath = $env:PATH
$passed = 0
$failed = 0

function Invoke-TestGit {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        & git @Arguments *> $null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "Test fixture git command failed with exit code ${exitCode}: git $($Arguments -join ' ')"
    }
}

function Set-TestProjectVersion {
    param(
        [Parameter(Mandatory = $true)][string] $DisplayVersion,
        [Parameter(Mandatory = $true)][string] $ApplicationVersion,
        [Parameter(Mandatory = $true)][string] $Message
    )

    $projectDirectory = Join-Path $repositoryPath "SoftballScorekeeper"
    New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
    $projectPath = Join-Path $projectDirectory "SoftballScorekeeper.csproj"
    Set-Content -LiteralPath $projectPath -Encoding utf8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-windows10.0.19041.0</TargetFrameworks>
    <ApplicationTitle>SoftballScorekeeper</ApplicationTitle>
    <ApplicationDisplayVersion>$DisplayVersion</ApplicationDisplayVersion>
    <ApplicationVersion>$ApplicationVersion</ApplicationVersion>
  </PropertyGroup>
  <PropertyGroup Condition="'`$(Configuration)' == 'Debug'">
    <ApplicationId>com.companyname.softballscorekeeper.debug</ApplicationId>
  </PropertyGroup>
</Project>
"@
    Invoke-TestGit -Arguments @("add", "SoftballScorekeeper/SoftballScorekeeper.csproj")
    Invoke-TestGit -Arguments @("commit", "-m", $Message)
    Invoke-TestGit -Arguments @("update-ref", "refs/remotes/origin/main", "HEAD")
}

function Set-TestProjectXml {
    param(
        [Parameter(Mandatory = $true)][string] $Xml,
        [Parameter(Mandatory = $true)][string] $Message
    )

    $projectDirectory = Join-Path $repositoryPath "SoftballScorekeeper"
    New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $projectDirectory "SoftballScorekeeper.csproj") -Encoding utf8 -Value $Xml
    Invoke-TestGit -Arguments @("add", "SoftballScorekeeper/SoftballScorekeeper.csproj")
    Invoke-TestGit -Arguments @("commit", "-m", $Message)
    Invoke-TestGit -Arguments @("update-ref", "refs/remotes/origin/main", "HEAD")
}

function Invoke-ValidationCase {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][bool] $ShouldPass,
        [Parameter(Mandatory = $true)][hashtable] $Overrides
    )

    $sourceSha = (& git rev-parse HEAD).Trim()
    $arguments = @{
        ReleaseVersion = "2.0.1"
        AndroidVersionCode = "7"
        EventName = "workflow_dispatch"
        ExpectedRef = "refs/heads/main"
        SourceSha = $sourceSha
        Repository = "example/SoftballScorekeeper"
        RepositoryRoot = $repositoryPath
    }
    foreach ($key in $Overrides.Keys) {
        $arguments[$key] = $Overrides[$key]
    }

    $didPass = $true
    $failureMessage = $null
    try {
        & $validatorPath @arguments *> $null
    }
    catch {
        $didPass = $false
        $failureMessage = $_.Exception.Message
    }

    if ($didPass -eq $ShouldPass) {
        $script:passed++
        Write-Host "PASS: $Name"
    }
    else {
        $script:failed++
        Write-Error "FAIL: $Name expected pass=$ShouldPass but observed pass=$didPass. $failureMessage"
    }
}

try {
    New-Item -ItemType Directory -Path $repositoryPath, $mockBinPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $mockBinPath "gh.ps1") -Encoding utf8 -Value @'
$path = $args[-1]
switch ($env:PREFLIGHT_TEST_GH_MODE) {
    "tag-exists" {
        if ($path -match "/git/ref/tags/") { Write-Output "{}"; exit 0 }
    }
    "release-exists" {
        if ($path -match "/releases/tags/") { Write-Output "{}"; exit 0 }
    }
    "lookup-failure" {
        Write-Error "gh: server error (HTTP 500)" -ErrorAction Continue
        exit 1
    }
}
Write-Error "gh: Not Found (HTTP 404)" -ErrorAction Continue
exit 1
'@
    $env:PATH = "$mockBinPath$([System.IO.Path]::PathSeparator)$originalPath"
    $env:PREFLIGHT_TEST_GH_MODE = "missing"

    Push-Location -LiteralPath $repositoryPath
    Invoke-TestGit -Arguments @("init", "--initial-branch=main")
    Invoke-TestGit -Arguments @("config", "user.name", "Release Preflight Tests")
    Invoke-TestGit -Arguments @("config", "user.email", "release-preflight@example.invalid")

    foreach ($version in @("2.0.1", "2.1.0", "3.0.0")) {
        Set-TestProjectVersion -DisplayVersion $version -ApplicationVersion "7" -Message "Set version $version"
        Invoke-ValidationCase -Name "accepts semantic version $version from multiple PropertyGroups" -ShouldPass $true -Overrides @{ ReleaseVersion = $version }
    }

    Set-TestProjectVersion -DisplayVersion "2.0.1" -ApplicationVersion "7" -Message "Reset positive fixture"
    foreach ($version in @("v2.0.1", "2.0", "2", "2.0.1-beta", "2.0.1.0", "02.0.1")) {
        Invoke-ValidationCase -Name "rejects semantic version $version" -ShouldPass $false -Overrides @{ ReleaseVersion = $version }
    }

    foreach ($versionCode in @("0", "-1", "+7", "7.0", "abc", "2100000001")) {
        Invoke-ValidationCase -Name "rejects Android version code $versionCode" -ShouldPass $false -Overrides @{ AndroidVersionCode = $versionCode }
    }

    Invoke-ValidationCase -Name "rejects display-version mismatch" -ShouldPass $false -Overrides @{ ReleaseVersion = "2.0.2" }
    Invoke-ValidationCase -Name "rejects Android-version mismatch" -ShouldPass $false -Overrides @{ AndroidVersionCode = "8" }
    Invoke-ValidationCase -Name "rejects task-branch dispatch" -ShouldPass $false -Overrides @{ ExpectedRef = "refs/heads/task/3.1-release-pipeline" }
    Invoke-ValidationCase -Name "rejects non-dispatch event" -ShouldPass $false -Overrides @{ EventName = "push" }

    Set-TestProjectXml -Message "Remove display version" -Xml '<Project><PropertyGroup><ApplicationVersion>7</ApplicationVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationId>example</ApplicationId></PropertyGroup></Project>'
    Invoke-ValidationCase -Name "rejects missing display-version property" -ShouldPass $false -Overrides @{}
    Set-TestProjectXml -Message "Remove Android version" -Xml '<Project><PropertyGroup><ApplicationDisplayVersion>2.0.1</ApplicationDisplayVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationId>example</ApplicationId></PropertyGroup></Project>'
    Invoke-ValidationCase -Name "rejects missing Android-version property" -ShouldPass $false -Overrides @{}
    Set-TestProjectXml -Message "Duplicate display version" -Xml '<Project><PropertyGroup><ApplicationDisplayVersion>2.0.1</ApplicationDisplayVersion><ApplicationVersion>7</ApplicationVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationDisplayVersion>2.0.1</ApplicationDisplayVersion></PropertyGroup></Project>'
    Invoke-ValidationCase -Name "rejects duplicate display-version properties" -ShouldPass $false -Overrides @{}
    Set-TestProjectXml -Message "Duplicate Android version" -Xml '<Project><PropertyGroup><ApplicationDisplayVersion>2.0.1</ApplicationDisplayVersion><ApplicationVersion>7</ApplicationVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationVersion>7</ApplicationVersion></PropertyGroup></Project>'
    Invoke-ValidationCase -Name "rejects duplicate Android-version properties" -ShouldPass $false -Overrides @{}
    Set-TestProjectVersion -DisplayVersion "2.0.1" -ApplicationVersion "7" -Message "Restore valid multi-group fixture"

    Set-TestProjectVersion -DisplayVersion "2.0.0" -ApplicationVersion "6" -Message "Create existing v2 fixture"
    $env:PREFLIGHT_TEST_GH_MODE = "tag-exists"
    Invoke-ValidationCase -Name "rejects existing v2.0.0 Git tag" -ShouldPass $false -Overrides @{ ReleaseVersion = "2.0.0"; AndroidVersionCode = "6" }
    $env:PREFLIGHT_TEST_GH_MODE = "release-exists"
    Invoke-ValidationCase -Name "rejects existing v2.0.0 GitHub Release" -ShouldPass $false -Overrides @{ ReleaseVersion = "2.0.0"; AndroidVersionCode = "6" }
    Set-TestProjectVersion -DisplayVersion "2.0.1" -ApplicationVersion "7" -Message "Restore future release fixture"
    $env:PREFLIGHT_TEST_GH_MODE = "lookup-failure"
    Invoke-ValidationCase -Name "fails closed on GitHub lookup error" -ShouldPass $false -Overrides @{}
    $env:PREFLIGHT_TEST_GH_MODE = "missing"

    Invoke-TestGit -Arguments @("checkout", "-b", "unmerged")
    Invoke-TestGit -Arguments @("commit", "--allow-empty", "-m", "Create unmerged source")
    $unmergedSha = (& git rev-parse HEAD).Trim()
    Invoke-TestGit -Arguments @("update-ref", "refs/remotes/origin/main", "HEAD^")
    Invoke-ValidationCase -Name "rejects source outside merged main" -ShouldPass $false -Overrides @{ SourceSha = $unmergedSha }
    Invoke-TestGit -Arguments @("checkout", "main")

    Set-TestProjectVersion -DisplayVersion "1.0.0" -ApplicationVersion "7" -Message "Create historical version code"
    Invoke-TestGit -Arguments @("tag", "v1.0.0")
    Set-TestProjectVersion -DisplayVersion "2.0.1" -ApplicationVersion "7" -Message "Reuse historical version code"
    Invoke-ValidationCase -Name "rejects Android version code used by a tagged multi-PropertyGroup project" -ShouldPass $false -Overrides @{}

    Set-TestProjectVersion -DisplayVersion "9.9.9" -ApplicationVersion "8" -Message "Create future tag fixture"
    Set-TestProjectXml -Message "Remove tagged Android version" -Xml '<Project><PropertyGroup><ApplicationDisplayVersion>9.9.9</ApplicationDisplayVersion></PropertyGroup><PropertyGroup Condition="true"><ApplicationId>example</ApplicationId></PropertyGroup></Project>'
    Invoke-TestGit -Arguments @("tag", "v9.9.9")
    Set-TestProjectVersion -DisplayVersion "2.0.1" -ApplicationVersion "8" -Message "Inspect malformed historical project"
    Invoke-ValidationCase -Name "fails closed when a tagged project lacks exactly one Android version" -ShouldPass $false -Overrides @{ AndroidVersionCode = "8" }
}
finally {
    if ((Get-Location).Path -eq $repositoryPath) {
        Pop-Location
    }
    $env:PATH = $originalPath
    Remove-Item Env:PREFLIGHT_TEST_GH_MODE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Release preflight validation: $passed passed, $failed failed."
if ($failed -ne 0) {
    throw "$failed release preflight validation case(s) failed."
}
