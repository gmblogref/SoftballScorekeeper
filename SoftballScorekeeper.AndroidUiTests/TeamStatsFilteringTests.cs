using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamStatsFilteringTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Team_stats_presets_filter_completed_team_year_games_and_preserve_sorting()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamStatsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenTeamStats(driver);
            Assert.DoesNotContain("Last 2 Games", driver.PageSource, StringComparison.Ordinal);
            AssertFilterAndPlateAppearances(driver, "All Games", "10");

            FindFilterButton(driver, "TeamStatsLastGameNightFilter").Click();
            AssertFilterAndPlateAppearances(driver, "Last Game Night", "2");

            AndroidElementWait.UntilAutomationId(driver, "CompletedStatsSortPlayer").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedStatsSortPlayer", "Player ▲");

            FindFilterButton(driver, "TeamStatsCurrentLeagueSeasonFilter").Click();
            AssertFilterAndPlateAppearances(driver, "Current League Season", "6");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedStatsSortPlayer", "Player ▲");
            Assert.DoesNotContain("Team Stats could not be loaded.", driver.PageSource);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Team_stats_presets_filter_completed_team_year_games_and_preserve_sorting),
                exception);
        }
    }

    [AndroidUiFact]
    public void Custom_game_checklist_filters_across_league_seasons_clears_and_resets_on_reopen()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamStatsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenTeamStats(driver);
            FindFilterButton(driver, "TeamStatsCustomFilter").Click();
            AndroidElementWait.UntilText(driver, "Select Completed Games");

            AssertChecklistGame(driver, 1, "Apr 1, 2026", "Spring One", "6:30", "Spring");
            AssertChecklistGame(driver, 4, "Jul 8, 2026", "Summer Two", "6:30", "Summer 1");
            Assert.DoesNotContain("Future Summer", driver.PageSource);
            Assert.DoesNotContain("In Progress Fall", driver.PageSource);

            AndroidElementWait.UntilAutomationId(driver, "TeamStatsClearAll").Click();
            Assert.False(IsChecked(FindGameCheckbox(driver, 1)));
            Assert.False(IsChecked(FindGameCheckbox(driver, 4)));
            FindGameCheckbox(driver, 1).Click();
            FindGameCheckbox(driver, 4).Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamStatsApplyCustom").Click();
            AssertFilterAndPlateAppearances(driver, "Custom (2 Games)", "4");

            FindFilterButton(driver, "TeamStatsCustomFilter").Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamStatsClearAll").Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamStatsApplyCustom").Click();
            AssertFilterAndPlateAppearances(driver, "Custom (0 Games)", "0");
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamStatsNoGames",
                "No Completed Games match this filter.");
            Assert.Empty(driver.FindElements(By.Id(ResourceId("CompletedBattingRowPosition1"))));
            Assert.Empty(driver.FindElements(By.Id(ResourceId("CompletedBattingRowPosition2"))));

            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
            AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
            AssertFilterAndPlateAppearances(driver, "All Games", "10");
            Assert.DoesNotContain("Team Stats could not be loaded.", driver.PageSource);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Custom_game_checklist_filters_across_league_seasons_clears_and_resets_on_reopen),
                exception);
        }
    }

    private static void OpenTeamStats(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
        AndroidElementWait.UntilText(driver, "Team Stats");
        AndroidElementWait.UntilText(driver, "Automation Stats Team");
    }

    private static void AssertFilterAndPlateAppearances(
        AndroidDriver driver,
        string expectedFilter,
        string expectedPlateAppearances)
    {
        AndroidElementWait.UntilAutomationIdText(
            driver, "TeamStatsActiveFilter", expectedFilter);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        wait.Until(currentDriver =>
        {
            var total = currentDriver.FindElements(MobileBy.AccessibilityId(
                    "CompletedBattingPlateAppearancesTeamTotal"))
                .FirstOrDefault(element => element.Displayed);
            return total is not null
                && string.Equals(total.Text, expectedPlateAppearances, StringComparison.Ordinal);
        });
    }

    private static void AssertChecklistGame(
        AndroidDriver driver,
        int gameId,
        params string[] expectedParts)
    {
        var row = AndroidElementWait.UntilAutomationId(driver, $"TeamStatsGameDisplay-{gameId}");
        Assert.All(expectedParts, part => Assert.Contains(part, row.Text, StringComparison.Ordinal));
    }

    private static IWebElement FindFilterButton(AndroidDriver driver, string automationId)
    {
        var resourceId = ResourceId(automationId);
        var displayed = driver.FindElements(By.Id(resourceId))
            .FirstOrDefault(element => element.Displayed);
        return displayed ?? driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().scrollable(true)).setAsHorizontalList()"
            + $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
    }

    private static IWebElement FindGameCheckbox(AndroidDriver driver, int gameId) =>
        AndroidElementWait.UntilAutomationId(driver, $"TeamStatsGameSelection-{gameId}");

    private static bool IsChecked(IWebElement element) =>
        string.Equals(element.GetAttribute("checked"), "true", StringComparison.OrdinalIgnoreCase);

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Team Stats workflow failed. Diagnostics: {artifacts}"
            + $"{Environment.NewLine}{exception}");
    }

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
}
