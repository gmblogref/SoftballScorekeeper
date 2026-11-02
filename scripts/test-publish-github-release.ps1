[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$publisher = Join-Path $PSScriptRoot 'publish-github-release.ps1'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$template = Join-Path $repositoryRoot 'release-notes/RELEASE_NOTES_TEMPLATE.md'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SoftballScorekeeper-publication-tests-" + [Guid]::NewGuid().ToString('N'))
$releaseVersion = '2.0.1'
$androidCode = '7'
$sourceSha = '0123456789abcdef0123456789abcdef01234567'
$passed = 0
$failed = 0

function Invoke-Case {
    param([string] $Name, [scriptblock] $Body)
    try {
        & $Body
        Write-Host "PASS: $Name"
        $script:passed++
    }
    catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)"
        $script:failed++
    }
}

function Assert-ThrowsLike {
    param([scriptblock] $Body, [string] $Pattern)
    try {
        & $Body
    }
    catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw }
        return
    }
    throw "Expected failure matching '$Pattern' was not raised."
}

function New-Fixture {
    param([string] $Name)
    $root = Join-Path $testRoot $Name
    $projectRoot = Join-Path $root 'repository'
    $artifactRoot = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Path (Join-Path $projectRoot 'SoftballScorekeeper'), $artifactRoot -Force | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $projectRoot 'SoftballScorekeeper/SoftballScorekeeper.csproj'),
        "<Project><PropertyGroup><ApplicationDisplayVersion>$releaseVersion</ApplicationDisplayVersion><ApplicationVersion>$androidCode</ApplicationVersion></PropertyGroup></Project>"
    )
    $apkName = "SoftballScorekeeper-$releaseVersion-$($sourceSha.Substring(0, 8)).apk"
    $zipName = "SoftballScorekeeper-$releaseVersion-windows-x64-portable.zip"
    $apk = Join-Path $artifactRoot $apkName
    $zip = Join-Path $artifactRoot $zipName
    [System.IO.File]::WriteAllText($apk, "android-$Name")
    [System.IO.File]::WriteAllText($zip, "windows-$Name")
    $apkHash = (Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant()
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllLines(
        (Join-Path $artifactRoot 'SHA256SUMS.txt'),
        @("$apkHash  $apkName", "$zipHash  $zipName"),
        [System.Text.UTF8Encoding]::new($false)
    )
    $notes = (Get-Content -LiteralPath $template -Raw).
        Replace('{{RELEASE_VERSION}}', $releaseVersion).
        Replace('{{RELEASE_HIGHLIGHTS}}', '- Human-authored validation fixture.').
        Replace('{{KNOWN_LIMITATIONS}}', '- Hosted acceptance remains pending in this fixture.')
    $notesPath = Join-Path $root "v$releaseVersion.md"
    [System.IO.File]::WriteAllText($notesPath, $notes, [System.Text.UTF8Encoding]::new($false))
    [pscustomobject]@{
        Root = $root; Repository = $projectRoot; Artifacts = $artifactRoot; Notes = $notesPath
        ApiState = (Join-Path $root 'api-state.json'); Apk = $apk; Zip = $zip
    }
}

function Invoke-Publisher {
    param([psobject] $Fixture, [string] $Scenario = 'success')
    $env:MOCK_API_STATE = $Fixture.ApiState
    $env:MOCK_API_SCENARIO = $Scenario
    $env:MOCK_HEAD_SHA = $sourceSha
    & $publisher `
        -ReleaseVersion $releaseVersion `
        -AndroidVersionCode $androidCode `
        -SourceSha $sourceSha `
        -Repository 'example/SoftballScorekeeper' `
        -ArtifactDirectory $Fixture.Artifacts `
        -ReleaseNotesPath $Fixture.Notes `
        -RepositoryRoot $Fixture.Repository `
        -ApiBaseUrl 'https://api.example.test' `
        -ApiInvokerPath $mockApi `
        -GitCommand $mockGit
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $mockGit = Join-Path $testRoot 'mock-git.ps1'
    $mockApi = Join-Path $testRoot 'mock-api.ps1'
    [System.IO.File]::WriteAllText($mockGit, @'
param([string[]] $Arguments)
$joined = $Arguments -join ' '
if ($joined -eq 'rev-parse HEAD') {
    return [pscustomobject]@{ ExitCode = 0; Output = @($env:MOCK_HEAD_SHA) }
}
if ($joined -eq 'show-ref --verify refs/remotes/origin/main') {
    return [pscustomobject]@{ ExitCode = 0; Output = @('origin/main') }
}
if ($joined -match '^merge-base --is-ancestor ') {
    return [pscustomobject]@{ ExitCode = 0; Output = @() }
}
return [pscustomobject]@{ ExitCode = 1; Output = @("Unexpected git command: $joined") }
'@)
    [System.IO.File]::WriteAllText($mockApi, @'
param([string] $Method, [string] $Uri, [string] $BodyJson, [string] $InputFile, [string] $ContentType)
$state = if (Test-Path -LiteralPath $env:MOCK_API_STATE) {
    Get-Content -LiteralPath $env:MOCK_API_STATE -Raw | ConvertFrom-Json
} else {
    [pscustomobject]@{ tag_reference_created = $false; draft_created = $false; draft = $false; published = $false; assets = @(); calls = @() }
}
$body = if ([string]::IsNullOrWhiteSpace($BodyJson)) { $null } else { $BodyJson | ConvertFrom-Json }
$state.calls = @($state.calls) + @([pscustomobject]@{ method = $Method; uri = $Uri; body = $body; input = $InputFile })
$status = 500
$responseBody = [pscustomobject]@{ message = 'Unhandled mock route' }
$scenario = $env:MOCK_API_SCENARIO
if ($scenario -eq 'api_failure' -and $Uri -match '/git/ref/tags/') {
    $status = 503; $responseBody = [pscustomobject]@{ message = 'uncertain' }
} elseif ($Method -eq 'GET' -and $Uri -match '/git/ref/tags/') {
    if ($scenario -eq 'existing_tag') { $status = 200; $responseBody = [pscustomobject]@{ ref = 'refs/tags/v2.0.1' } }
    else { $status = 404; $responseBody = [pscustomobject]@{ message = 'Not Found' } }
} elseif ($Method -eq 'GET' -and $Uri -match '/releases/tags/') {
    if ($scenario -eq 'existing_release') { $status = 200; $responseBody = [pscustomobject]@{ id = 41 } }
    else { $status = 404; $responseBody = [pscustomobject]@{ message = 'Not Found' } }
} elseif ($Method -eq 'POST' -and $Uri -match '/git/tags$') {
    $status = 201; $responseBody = [pscustomobject]@{ sha = ('a' * 40) }
} elseif ($Method -eq 'POST' -and $Uri -match '/git/refs$') {
    $state.tag_reference_created = $true; $status = 201; $responseBody = [pscustomobject]@{ ref = $body.ref; object = @{ sha = $body.sha } }
} elseif ($Method -eq 'POST' -and $Uri -match '/releases$') {
    $state.draft_created = $true; $state.draft = $true
    $status = 201; $responseBody = [pscustomobject]@{ id = 42; draft = $true; upload_url = 'https://uploads.example.test/repos/example/SoftballScorekeeper/releases/42/assets{?name,label}' }
} elseif ($Method -eq 'POST' -and $Uri -match 'uploads\.example\.test') {
    $name = [Uri]::UnescapeDataString(([Uri] $Uri).Query.Substring(6))
    $asset = [pscustomobject]@{
        name = $name; state = 'uploaded'; size = (Get-Item -LiteralPath $InputFile).Length
        digest = "sha256:$((Get-FileHash -LiteralPath $InputFile -Algorithm SHA256).Hash.ToLowerInvariant())"
    }
    $state.assets = @($state.assets) + @($asset)
    $status = 201; $responseBody = $asset
} elseif ($Method -eq 'GET' -and $Uri -match '/releases/42$') {
    $assets = @($state.assets)
    if ($scenario -eq 'asset_verification_failure' -and $assets.Count -gt 0) { $assets[0].digest = 'sha256:' + ('0' * 64) }
    $status = 200
    $responseBody = [pscustomobject]@{ id = 42; tag_name = 'v2.0.1'; draft = [bool] $state.draft; prerelease = $false; assets = $assets }
} elseif ($Method -eq 'PATCH' -and $Uri -match '/releases/42$') {
    if ($scenario -eq 'publication_failure') {
        $status = 500; $responseBody = [pscustomobject]@{ message = 'publish failed' }
    } else {
        $state.draft = $false; $state.published = $true
        $status = 200; $responseBody = [pscustomobject]@{ id = 42; draft = $false; prerelease = $false }
    }
}
ConvertTo-Json -InputObject $state -Depth 20 | Set-Content -LiteralPath $env:MOCK_API_STATE
ConvertTo-Json -InputObject ([pscustomobject]@{ status_code = $status; body = $responseBody }) -Depth 20 -Compress
'@)

    Invoke-Case 'publishes through annotated tag, verified draft assets, and final gate' {
        $fixture = New-Fixture 'success'
        Invoke-Publisher $fixture | Out-Null
        $state = Get-Content -LiteralPath $fixture.ApiState -Raw | ConvertFrom-Json
        if (-not $state.tag_reference_created -or -not $state.draft_created -or -not $state.published -or @($state.assets).Count -ne 3) {
            throw 'Successful publication state was incomplete.'
        }
        $tagCall = @($state.calls | Where-Object { $_.uri -match '/git/tags$' })
        if ($tagCall.Count -ne 1 -or $tagCall[0].body.object -cne $sourceSha -or $tagCall[0].body.type -cne 'commit') {
            throw 'Annotated tag did not target the exact source commit.'
        }
        $referenceCall = @($state.calls | Where-Object { $_.uri -match '/git/refs$' })
        $draftCall = @($state.calls | Where-Object { $_.method -eq 'POST' -and $_.uri -match '/releases$' })
        $publishCall = @($state.calls | Where-Object { $_.method -eq 'PATCH' -and $_.uri -match '/releases/42$' })
        if ($referenceCall.Count -ne 1 -or $referenceCall[0].body.ref -cne 'refs/tags/v2.0.1' -or
            $draftCall.Count -ne 1 -or $draftCall[0].body.draft -ne $true -or $draftCall[0].body.prerelease -ne $false -or
            $publishCall.Count -ne 1 -or $publishCall[0].body.draft -ne $false) {
            throw 'Annotated-reference, draft, or final publication sequence was incorrect.'
        }
        foreach ($metadata in @($sourceSha, 'SoftballScorekeeper-2.0.1-01234567.apk', 'SoftballScorekeeper-2.0.1-windows-x64-portable.zip', 'com.companyname.softballscorekeeper', $androidCode, 'D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05')) {
            if ($draftCall[0].body.body.IndexOf($metadata, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw "Generated release metadata '$metadata' was missing from the draft body."
            }
        }
    }
    Invoke-Case 'rejects an existing tag' {
        $fixture = New-Fixture 'existing-tag'
        Assert-ThrowsLike { Invoke-Publisher $fixture 'existing_tag' } 'already exists'
    }
    Invoke-Case 'rejects an existing release' {
        $fixture = New-Fixture 'existing-release'
        Assert-ThrowsLike { Invoke-Publisher $fixture 'existing_release' } 'already exists'
    }
    Invoke-Case 'fails closed on API uncertainty' {
        $fixture = New-Fixture 'api-failure'
        Assert-ThrowsLike { Invoke-Publisher $fixture 'api_failure' } 'HTTP status 503'
    }
    Invoke-Case 'rejects wrong checked-out source SHA' {
        $fixture = New-Fixture 'wrong-source'
        $env:MOCK_HEAD_SHA = 'ffffffffffffffffffffffffffffffffffffffff'
        Assert-ThrowsLike {
            & $publisher -ReleaseVersion $releaseVersion -AndroidVersionCode $androidCode -SourceSha $sourceSha -Repository 'example/SoftballScorekeeper' -ArtifactDirectory $fixture.Artifacts -ReleaseNotesPath $fixture.Notes -RepositoryRoot $fixture.Repository -ApiInvokerPath $mockApi -GitCommand $mockGit
        } 'does not match validated source'
    }
    Invoke-Case 'rejects a missing definitive artifact' {
        $fixture = New-Fixture 'missing-artifact'
        Remove-Item -LiteralPath $fixture.Apk
        Assert-ThrowsLike { Invoke-Publisher $fixture } 'must contain exactly'
    }
    Invoke-Case 'rejects invalid checksums' {
        $fixture = New-Fixture 'bad-checksum'
        Add-Content -LiteralPath $fixture.Apk -Value 'changed'
        Assert-ThrowsLike { Invoke-Publisher $fixture } 'SHA-256 mismatch'
    }
    Invoke-Case 'rejects missing release notes' {
        $fixture = New-Fixture 'missing-notes'
        Remove-Item -LiteralPath $fixture.Notes
        Assert-ThrowsLike { Invoke-Publisher $fixture } 'release notes are missing'
    }
    Invoke-Case 'rejects unresolved release-note placeholders' {
        $fixture = New-Fixture 'placeholder-notes'
        Add-Content -LiteralPath $fixture.Notes -Value '{{UNRESOLVED_VALUE}}'
        Assert-ThrowsLike { Invoke-Publisher $fixture } 'unresolved template placeholders'
    }
    Invoke-Case 'rejects release notes for a different version' {
        $fixture = New-Fixture 'wrong-version-notes'
        (Get-Content -LiteralPath $fixture.Notes -Raw).Replace($releaseVersion, '9.9.9') |
            Set-Content -LiteralPath $fixture.Notes -NoNewline
        Assert-ThrowsLike { Invoke-Publisher $fixture } 'do not identify release version'
    }
    Invoke-Case 'rejects remote asset digest mismatch before publication' {
        $fixture = New-Fixture 'asset-mismatch'
        Assert-ThrowsLike { Invoke-Publisher $fixture 'asset_verification_failure' } 'did not verify'
        $state = Get-Content -LiteralPath $fixture.ApiState -Raw | ConvertFrom-Json
        if (-not $state.draft -or $state.published) { throw 'Failed asset verification did not preserve the draft.' }
    }
    Invoke-Case 'preserves tag and draft on final publication failure' {
        $fixture = New-Fixture 'partial-publication'
        Assert-ThrowsLike { Invoke-Publisher $fixture 'publication_failure' } 'HTTP status 500'
        $state = Get-Content -LiteralPath $fixture.ApiState -Raw | ConvertFrom-Json
        if (-not $state.tag_reference_created -or -not $state.draft_created -or -not $state.draft -or $state.published -or @($state.assets).Count -ne 3) {
            throw 'Partial-publication state was not preserved for manual recovery.'
        }
    }

    $workflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/release.yml') -Raw
    Invoke-Case 'false publication flag skips the publication job' {
        if ($workflow -notmatch "inputs\.create_github_release == true") { throw 'Explicit true-only publication gate is missing.' }
    }
    Invoke-Case 'required failures and UI selection explicitly gate publication' {
        foreach ($required in @('preflight', 'automated-tests', 'appium-project', 'android-application', 'windows-application', 'android-production-package', 'windows-production-package', 'release-artifact-verification')) {
            if ($workflow -notmatch "needs(?:\.|\[')$([regex]::Escape($required))") { throw "Missing publication dependency condition for $required." }
        }
        if ($workflow -notmatch "android_ui_validation == 'none'.*?'skipped'" -or $workflow -notmatch "android_ui_validation != 'none'.*?'success'") {
            throw 'Optional UI result gate is incomplete.'
        }
    }
    Invoke-Case 'write permission is scoped only to publication job' {
        if (($workflow | Select-String -Pattern 'contents: write' -AllMatches).Matches.Count -ne 1 -or $workflow -notmatch '(?ms)^  github-release-publication:.*?permissions:\s+contents: write') {
            throw 'Publication write permission is missing or not uniquely scoped.'
        }
    }
}
finally {
    Remove-Item Env:MOCK_API_STATE, Env:MOCK_API_SCENARIO, Env:MOCK_HEAD_SHA -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

Write-Host "GitHub publication validation: $passed passed, $failed failed."
if ($failed -ne 0) { exit 1 }
