namespace SoftballScorekeeper.Tests;

public sealed class ReleaseUpgradeAcceptanceWiringTests
{
    private const string AuthoritativeFilter =
        "FullyQualifiedName=SoftballScorekeeper.AndroidUiTests.ReleaseUpgradeAcceptanceTests.Signed_release_upgrade_preserves_data_and_licenses_work_offline";

    [Fact]
    public void Release_upgrade_uses_scroll_aware_post_save_team_and_player_lookup()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseUpgradeAcceptanceTests.cs"
        );
        var createTeam = MethodBlock(acceptance, "private static void CreateTeam", "private static void CreatePlayer");
        var createPlayer = MethodBlock(acceptance, "private static void CreatePlayer", "private static void AssertPersistedData");

        AssertOrdered(
            createTeam,
            "UntilText(driver, \"Save\").Click()",
            "UntilTextAbsent(driver, \"Save\")",
            "ScrollToText(driver, TeamName)"
        );
        AssertOrdered(
            createPlayer,
            "UntilText(driver, \"Save\").Click()",
            "UntilTextAbsent(driver, \"Save\")",
            "ScrollToText(driver, PlayerName)"
        );
        Assert.DoesNotContain("UntilText(driver, TeamName)", createTeam);
        Assert.DoesNotContain("UntilText(driver, PlayerName)", createPlayer);
    }

    [Fact]
    public void Release_upgrade_persistence_checks_use_stable_section_landings_and_scroll_lookup()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseUpgradeAcceptanceTests.cs"
        );
        var persisted = MethodBlock(acceptance, "private static void AssertPersistedData", "private static void NavigateToPersistedTeam");
        var teamNavigation = MethodBlock(acceptance, "private static void NavigateToPersistedTeam", "private static void OpenLicenses");

        Assert.Contains("NavigateToPersistedTeam(driver)", persisted);
        Assert.Contains("NavigateSection(driver, \"Players\", \"Add Player\")", persisted);
        Assert.Contains("ScrollToText(driver, PlayerName)", persisted);
        Assert.Contains("NavigateSection(driver, \"Teams\", \"Create Team\")", teamNavigation);
        Assert.Contains("ScrollToText(driver, TeamName)", teamNavigation);
    }

    [Fact]
    public void Release_upgrade_is_the_single_authoritative_order_independent_test()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseUpgradeAcceptanceTests.cs"
        );

        Assert.Equal(1, CountOccurrences(acceptance, "[AndroidUiFact]"));
        Assert.Contains("public void Signed_release_upgrade_preserves_data_and_licenses_work_offline()", acceptance);
        Assert.Contains("Install(sourceApk", acceptance);
        Assert.Contains("Install(destinationApk, replace: true)", acceptance);
        Assert.Contains("AssertVersion(\"5\", \"1.0.1\")", acceptance);
        Assert.Contains("AssertVersion(\"6\", \"2.0.0\")", acceptance);
        Assert.True(CountOccurrences(acceptance, "AssertPersistedData(driver)") >= 2);
        Assert.Contains("airplane-mode\", \"enable", acceptance);
        Assert.Contains("ScreenOrientation.Portrait", acceptance);
        Assert.Contains("ScreenOrientation.Landscape", acceptance);
    }

    [Fact]
    public void Release_runner_and_readme_require_the_exact_authoritative_method_filter()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");
        var readme = Read("SoftballScorekeeper.AndroidUiTests", "README.md");

        Assert.Contains($"$releaseAcceptanceTestFilter = '{AuthoritativeFilter}'", runner);
        Assert.Contains("[string]::Equals($TestFilter, $releaseAcceptanceTestFilter, [StringComparison]::Ordinal)", runner);
        Assert.Contains($"-TestFilter '{AuthoritativeFilter}'", readme);
        Assert.DoesNotContain("FullyQualifiedName~ReleaseUpgradeAcceptanceTests", readme);
        Assert.Contains("$releaseAcceptanceAvdName = 'softball_tablet_release_acceptance_api_35'", runner);
        Assert.Contains("Assert-NotReservedDevelopmentAvd", runner);
        Assert.Contains("Get-EmulatorAvdName", runner);
        Assert.Contains("Release acceptance is restricted to Android AVD", runner);
        Assert.Contains("if ($startedAppium -and $null -ne $startedAppiumProcess)", runner);
        Assert.Contains("if ($startedEmulator -and -not $KeepEmulatorRunning", runner);
    }

    [Fact]
    public void Release_runner_retains_timestamped_trx_failure_diagnostics()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");

        Assert.Contains("SoftballScorekeeper.AndroidUiTests\\TestResults\\ReleaseAcceptance", runner);
        Assert.Contains("release-upgrade-acceptance-", runner);
        Assert.Contains("yyyyMMdd-HHmmssfff", runner);
        Assert.Contains("'--logger', \"trx;LogFileName=$releaseTrxName\"", runner);
        Assert.Contains("'--results-directory', $releaseResultsDirectory", runner);
    }

    [Fact]
    public void Text_absence_wait_ignores_departing_and_stale_elements()
    {
        var waits = Read("SoftballScorekeeper.AndroidUiTests", "AndroidElementWait.cs");
        var absent = MethodBlock(waits, "public static void UntilTextAbsent", "public static IWebElement UntilTextContaining");

        Assert.Contains("CreateWait(driver, timeout)", absent);
        Assert.Contains(".All(element => !element.Displayed)", absent);
        Assert.Contains("StaleElementReferenceException", waits);
    }

    private static void AssertOrdered(string source, params string[] values)
    {
        var prior = -1;
        foreach (var value in values)
        {
            var current = source.IndexOf(value, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{value}' after index {prior}.");
            prior = current;
        }
    }

    private static string MethodBlock(string source, string startText, string endText)
    {
        var start = source.IndexOf(startText, StringComparison.Ordinal);
        var end = source.IndexOf(endText, start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method start not found: {startText}");
        Assert.True(end > start, $"Method end not found: {endText}");
        return source[start..end];
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
