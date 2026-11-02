[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/release.yml') -Raw
$runner = Get-Content -LiteralPath (Join-Path $repositoryRoot 'scripts/run-android-ui-tests.ps1') -Raw
$passed = 0
$failed = 0

function Assert-Matches {
    param([string]$Name, [string]$Text, [string]$Pattern)
    try {
        if ($Text -notmatch $Pattern) {
            throw "Expected pattern was not found: $Pattern"
        }
        Write-Host "PASS: $Name"
        $script:passed++
    }
    catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)"
        $script:failed++
    }
}

function Assert-DoesNotMatch {
    param([string]$Name, [string]$Text, [string]$Pattern)
    try {
        if ($Text -match $Pattern) {
            throw "Forbidden pattern was found: $Pattern"
        }
        Write-Host "PASS: $Name"
        $script:passed++
    }
    catch {
        Write-Host "FAIL: $Name -- $($_.Exception.Message)"
        $script:failed++
    }
}

Assert-Matches `
    -Name 'none mode skips the Android UI job' `
    -Text $workflow `
    -Pattern "(?ms)^  android-ui-validation:\s+name: Android UI validation\s+if: \$\{\{ inputs\.android_ui_validation != 'none' \}\}"
Assert-Matches `
    -Name 'hosted UI job uses Ubuntu 24.04 and exact validated source' `
    -Text $workflow `
    -Pattern "(?ms)^  android-ui-validation:.*?runs-on: ubuntu-24\.04.*?ref: \$\{\{ needs\.preflight\.outputs\.source_sha \}\}.*?git rev-parse HEAD"
Assert-Matches `
    -Name 'hosted setup pins Appium and UiAutomator2' `
    -Text $workflow `
    -Pattern '(?ms)npm install --global appium@3\.7\.0.*?driver install "uiautomator2@8\.5\.0"'
Assert-Matches `
    -Name 'hosted setup requires acceleration without fallback' `
    -Text $workflow `
    -Pattern '(?ms)/dev/kvm.*?refusing an unaccelerated Android emulator run.*?emulator -accel-check'
Assert-Matches `
    -Name 'smoke mode uses the exact requested test filter' `
    -Text $workflow `
    -Pattern 'FullyQualifiedName=SoftballScorekeeper\.AndroidUiTests\.AndroidSessionSmokeTests\.Installed_application_creates_an_Appium_session'
Assert-Matches `
    -Name 'full mode invokes the unfiltered existing runner' `
    -Text $workflow `
    -Pattern '(?ms)elseif \(\$env:ANDROID_UI_VALIDATION -ceq "full"\) \{\s+\./scripts/run-android-ui-tests\.ps1 -AvdName \$avdName -HeadlessEmulator\s+\}'
Assert-Matches `
    -Name 'only the dedicated UI-test AVD is configured' `
    -Text $workflow `
    -Pattern 'softball_tablet_ui_tests_api_35'
Assert-DoesNotMatch `
    -Name 'workflow does not select the development or release-acceptance AVD' `
    -Text $workflow `
    -Pattern 'softball_tablet_(api_35|release_acceptance_api_35)'
Assert-Matches `
    -Name 'final artifacts accept only skipped none mode or successful selected mode' `
    -Text $workflow `
    -Pattern "(?ms)inputs\.android_ui_validation == 'none' && needs\['android-ui-validation'\]\.result == 'skipped'.*?inputs\.android_ui_validation != 'none' && needs\['android-ui-validation'\]\.result == 'success'"
Assert-Matches `
    -Name 'final artifact verification explicitly needs Android UI validation' `
    -Text $workflow `
    -Pattern '(?ms)^  release-artifact-verification:.*?needs:.*?- android-ui-validation'
Assert-Matches `
    -Name 'repository runner supports explicit headless emulator startup' `
    -Text $runner `
    -Pattern '(?ms)\[switch\]\$HeadlessEmulator.*?-no-window.*?-no-audio.*?-no-boot-anim'
Assert-Matches `
    -Name 'repository runner retains the protected development AVD rejection' `
    -Text $runner `
    -Pattern "reservedDevelopmentAvdName = 'softball_tablet_api_35'"

Write-Host "Release UI workflow validation: $passed passed, $failed failed."
if ($failed -ne 0) {
    exit 1
}
