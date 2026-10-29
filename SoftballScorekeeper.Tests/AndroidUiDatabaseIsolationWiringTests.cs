namespace SoftballScorekeeper.Tests;

public sealed class AndroidUiDatabaseIsolationWiringTests
{
    [Fact]
    public void App_configuration_selects_and_resets_only_the_ui_test_database()
    {
        var settings = Read("SoftballScorekeeper", "Configuration", "ApplicationSettings.cs");
        var composition = Read("SoftballScorekeeper", "MauiProgram.cs");
        var initializer = Read("SoftballScorekeeper", "Testing", "AndroidUiTestDataInitializer.cs");

        Assert.Contains("SoftballScorekeeper.db3", settings);
        Assert.Contains("SoftballScorekeeper.AndroidUiTests.db3", settings);
        Assert.Contains("IsInitializationRequested", composition);
        Assert.Contains("new AppDatabasePaths", composition);
        Assert.Contains("IsUiTestModeRequested", composition);
        Assert.Contains("AppDatabaseFileIsolation.ResetAndroidUiTestDatabase", composition);
        Assert.Contains("RequestFileNames.Any", initializer);
        Assert.Contains("ModeFileName", initializer);
    }

    [Fact]
    public void Appium_fixture_requests_a_seed_without_clearing_application_data()
    {
        var fixture = Read("SoftballScorekeeper.AndroidUiTests", "AndroidAppiumFixture.cs");

        Assert.Contains("RequestDeterministicSeed", fixture);
        Assert.DoesNotContain("\"pm\", \"clear\"", fixture);
        Assert.Contains("TestSeedRequestFileNames", fixture);
        Assert.Contains("$\"files/{seedRequestFileName}\"", fixture);
        Assert.Contains("TestModeFileName", fixture);
    }

    [Fact]
    public void Android_ui_runner_defaults_to_a_dedicated_avd_and_reserves_the_development_avd()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");

        Assert.Contains("$defaultAndroidUiTestAvdName = 'softball_tablet_ui_tests_api_35'", runner);
        Assert.Contains("$reservedDevelopmentAvdName = 'softball_tablet_api_35'", runner);
        Assert.Contains(
            "$releaseAcceptanceAvdName = 'softball_tablet_release_acceptance_api_35'",
            runner
        );
        Assert.Contains("Assert-NotReservedDevelopmentAvd", runner);
        Assert.Contains("Get-EmulatorAvdName", runner);
        Assert.Contains("[string]$AvdName", runner);
        Assert.Contains("$env:SOFTBALL_ANDROID_AVD", runner);
        Assert.Contains(
            "if (-not [string]::Equals($selectedAvdName, $releaseAcceptanceAvdName, [StringComparison]::OrdinalIgnoreCase))",
            runner
        );
        Assert.Contains("$script:startedEmulator = $true", runner);
        Assert.Contains("if ($startedEmulator -and -not $KeepEmulatorRunning", runner);
    }

    [Fact]
    public void Android_ui_runner_allows_nonfatal_native_stderr_during_avd_discovery()
    {
        var discovery = ReadAvdDiscoveryBlock();

        Assert.Contains("$previousErrorActionPreference = $ErrorActionPreference", discovery);
        Assert.Contains("$ErrorActionPreference = 'Continue'", discovery);
        Assert.Contains("$avdOutput = & $EmulatorExecutable -list-avds", discovery);
        Assert.DoesNotContain("-list-avds 2>&1", discovery);
        Assert.Contains("$avdExitCode = $LASTEXITCODE", discovery);
        Assert.Contains("finally", discovery);
        Assert.Contains("$ErrorActionPreference = $previousErrorActionPreference", discovery);
    }

    [Fact]
    public void Android_ui_runner_accepts_valid_avd_stdout_with_diagnostic_stderr()
    {
        var result = RunAvdDiscovery(
            ["softball_tablet_release_acceptance_api_35"],
            "nonfatal emulator crash-reporter diagnostic",
            0
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DISCOVERY_SUCCEEDED", result.StandardOutput);
        Assert.Contains("nonfatal emulator crash-reporter diagnostic", result.StandardError);
    }

    [Fact]
    public void Android_ui_runner_rejects_nonzero_avd_discovery_exit_code()
    {
        var result = RunAvdDiscovery(
            ["softball_tablet_release_acceptance_api_35"],
            "fatal emulator diagnostic",
            7
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DISCOVERY_FAILED:Android AVD discovery failed with exit code 7.", result.StandardOutput);
    }

    [Fact]
    public void Android_ui_runner_does_not_treat_stderr_as_an_avd_name()
    {
        var result = RunAvdDiscovery(
            ["softball_tablet_ui_tests_api_35"],
            "softball_tablet_release_acceptance_api_35",
            0
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DISCOVERY_FAILED:Android AVD 'softball_tablet_release_acceptance_api_35' was not found.", result.StandardOutput);
    }

    [Fact]
    public void Android_ui_runner_rejects_failed_or_missing_avd_discovery()
    {
        var discovery = ReadAvdDiscoveryBlock();

        Assert.Contains("if ($avdExitCode -ne 0)", discovery);
        Assert.Contains(
            "throw \"Android AVD discovery failed with exit code $avdExitCode.\"",
            discovery
        );
        Assert.Contains("$availableAvds = @($avdOutput", discovery);
        Assert.Contains("$_ -match '^[A-Za-z0-9._-]+$'", discovery);
        Assert.Contains("if ($RequestedAvdName -notin $availableAvds)", discovery);
        Assert.Contains("throw \"Android AVD '$RequestedAvdName' was not found.", discovery);
    }

    private static PowerShellResult RunAvdDiscovery(
        string[] avdOutput,
        string diagnostic,
        int exitCode
    )
    {
        var discovery = ReadAvdDiscoveryBlock();
        var quotedAvds = string.Join(", ", avdOutput.Select(value => $"'{Escape(value)}'"));
        var harness = $$"""
            $ErrorActionPreference = 'Stop'
            $RequestedAvdName = 'softball_tablet_release_acceptance_api_35'
            $fakeAvdOutput = @({{quotedAvds}})
            $fakeDiagnostic = '{{Escape(diagnostic)}}'
            $fakeExitCode = {{exitCode}}
            $EmulatorExecutable = {
                $fakeAvdOutput | ForEach-Object { Write-Output $_ }
                Write-Error $fakeDiagnostic
                $global:LASTEXITCODE = $fakeExitCode
            }
            function Write-Stage { param([string]$Message) }
            try {
            {{discovery}}
                Write-Output 'DISCOVERY_SUCCEEDED'
            }
            catch {
                Write-Output "DISCOVERY_FAILED:$($_.Exception.Message)"
            }
            """;
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(harness);

        using var process = System.Diagnostics.Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new PowerShellResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult()
        );
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string ReadAvdDiscoveryBlock()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");
        var start = runner.IndexOf(
            "Write-Stage \"Checking configured Android test AVD:",
            StringComparison.Ordinal
        );
        var end = runner.IndexOf(
            "Write-Stage \"Starting Android test AVD:",
            start,
            StringComparison.Ordinal
        );

        Assert.True(start >= 0);
        Assert.True(end > start);
        return runner[start..end];
    }

    private static string Read(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError);
}
