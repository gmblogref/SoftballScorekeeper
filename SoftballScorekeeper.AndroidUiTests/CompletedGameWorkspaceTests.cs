using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class CompletedGameWorkspaceTests
{
    private readonly AndroidAppiumFixture fixture;

    public CompletedGameWorkspaceTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Completed_game_plays_expose_canonical_chronological_results()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.CompletedStatisticsSortingTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AssertViewSelection(driver, statsVisible: true);
            AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
            AssertViewSelection(driver, statsVisible: false);
            AndroidElementWait.UntilText(driver, "Top 1 — Automation Team");
            AndroidElementWait.UntilText(driver, "Alex Automation — Single");
            AndroidElementWait.UntilText(
                driver, "SCORE — Automation Team: 0 | Automation Opponent: 0");
            AndroidElementWait.UntilAutomationId(driver, "GameStatsButton").Click();
            AssertViewSelection(driver, statsVisible: true);
            AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
            AssertViewSelection(driver, statsVisible: false);

            fixture.SetOrientation(ScreenOrientation.Portrait);
            AssertViewSelection(driver, statsVisible: false);
            AndroidElementWait.UntilAutomationId(driver, "GameStatsButton").Click();
            AssertViewSelection(driver, statsVisible: true);

            fixture.SetOrientation(ScreenOrientation.Landscape);
            AssertViewSelection(driver, statsVisible: true);
            AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
            AssertViewSelection(driver, statsVisible: false);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(nameof(
                Completed_game_plays_expose_canonical_chronological_results), exception);
            throw new Xunit.Sdk.XunitException(
                $"Android completed Game Plays workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
        }
    }

    [AndroidUiFact]
    public void Completed_statistics_sorting_remains_correct_across_landscape_and_portrait()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.CompletedStatisticsSortingTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedHomeName", "Automation Opponent");
            AssertNotDisplayed(driver, "ScoreGameButton");
            AssertNotDisplayed(driver, "ResumeGameButton");

            AssertHeaderAndOrder(driver, "CompletedStatsSortOnBasePercentage",
                "OBP ▼", 1, 3, 2);
            AssertLandscapeColumnAlignment(driver);

            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortPlayer",
                "Player ▲", 1, 2, 3);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortPlayer",
                "Player ▼", 3, 2, 1);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortPlateAppearances",
                "PA ▼", 2, 1, 3);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortPlateAppearances",
                "PA ▲", 3, 1, 2);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortHits",
                "H ▼", 1, 2, 3);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortHomeRuns",
                "HR ▼", 1, 2, 3);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortWalks",
                "BB ▼", 3, 1, 2);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortBattingAverage",
                "AVG ▼", 1, 3, 2);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortOnBasePercentage",
                "OBP ▼", 1, 3, 2);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortSluggingPercentage",
                "SLG ▼", 1, 3, 2);

            fixture.SetOrientation(ScreenOrientation.Portrait);
            AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
            ScrollStatisticsToRateColumns(driver);
            AndroidElementWait.UntilAutomationId(driver,
                "CompletedStatsSortBattingAverage");
            AndroidElementWait.UntilAutomationId(driver,
                "CompletedStatsSortOnBasePercentage");
            AndroidElementWait.UntilAutomationId(driver,
                "CompletedStatsSortSluggingPercentage");
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortOnBasePercentage",
                "OBP ▼", 1, 3, 2);
            ClickHeaderAndAssertOrder(driver, "CompletedStatsSortOnBasePercentage",
                "OBP ▲", 2, 3, 1);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(nameof(
                Completed_statistics_sorting_remains_correct_across_landscape_and_portrait),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android completed-statistics sorting workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
        }
    }

    [AndroidUiFact]
    public void Ready_in_progress_and_completed_states_expose_the_expected_workspace()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Ready_in_progress_and_completed_states_expose_the_expected_workspace), driver =>
        {
            AndroidScoringWorkflow.Step("verify the prepared Game is Ready", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
                AssertNotDisplayed(driver, "ResumeGameButton");
                AssertNotDisplayed(driver, "CompletedGameStatus");
            });

            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.ExitAndResume(driver);
            AndroidScoringWorkflow.Step("verify the Game can resume while In Progress", () =>
            {
                AndroidElementWait.UntilAutomationIdText(
                    driver, "CurrentBatter", AndroidAutomationConstants.FirstBatterName);
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
                AssertNotDisplayed(driver, "CompletedGameStatus");
            });

            AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
            AndroidScoringWorkflow.SelectResult(driver, "Home Run");
            AndroidScoringWorkflow.Step("complete the Game", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "GameOverButton").Click();
                AndroidElementWait.UntilText(driver, "Game Over").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "CompletedGameStatus", "FINAL");
            });

            AssertCompletedGame(driver);
            AndroidScoringWorkflow.Step("verify the completed presentation in portrait", () =>
            {
                fixture.SetOrientation(ScreenOrientation.Portrait);
                AssertCompletedGame(driver);
            });
            AndroidScoringWorkflow.Step("verify the completed presentation in landscape", () =>
            {
                fixture.SetOrientation(ScreenOrientation.Landscape);
                AssertCompletedGame(driver);
            });
        });
    }

    [AndroidUiFact]
    public void Load_failure_remains_visibly_represented_when_no_workspace_is_loaded()
    {
        fixture.ResetApplication(AndroidAutomationConstants.MissingGameTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "GameWorkspaceValidationMessage",
                "The selected Game could not be found for this Team.");
            AssertNotDisplayed(driver, "ScoreGameButton");
            AssertNotDisplayed(driver, "ResumeGameButton");
            AssertNotDisplayed(driver, "CompletedGameStatus");
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(nameof(
                Load_failure_remains_visibly_represented_when_no_workspace_is_loaded), exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Game Workspace workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
        }
    }

    private static void AssertCompletedGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedVisitorName", "Automation Team");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedHomeName", "Automation Opponent");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedHomeScore", "0");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingAtBatsPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHomeRunsPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingBattingAveragePosition1", "1.000");
        ScrollStatisticsToRateColumns(driver);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingOnBasePercentagePosition1", "1.000");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingSluggingPercentagePosition1", "4.000");
        AssertNotDisplayed(driver, "ResumeGameButton");
    }

    private static void ClickHeaderAndAssertOrder(
        AndroidDriver driver,
        string headerAutomationId,
        string expectedHeaderText,
        params int[] expectedBattingPositions)
    {
        AndroidElementWait.UntilAutomationId(driver, headerAutomationId).Click();
        AssertHeaderAndOrder(
            driver, headerAutomationId, expectedHeaderText, expectedBattingPositions);
    }

    private static void AssertHeaderAndOrder(
        AndroidDriver driver,
        string headerAutomationId,
        string expectedHeaderText,
        params int[] expectedBattingPositions)
    {
        AndroidElementWait.UntilAutomationIdText(
            driver, headerAutomationId, expectedHeaderText);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        Assert.True(wait.Until(_ =>
        {
            var rows = expectedBattingPositions
                .Select(position => FindDisplayed(
                    driver, $"CompletedBattingRowPosition{position}"))
                .ToArray();
            var total = FindDisplayed(driver, "CompletedBattingTeamTotal");
            return rows.All(row => row is not null)
                && total is not null
                && rows.Select(row => row!.Location.Y).SequenceEqual(
                    rows.Select(row => row!.Location.Y).OrderBy(y => y))
                && total.Location.Y > rows.Max(row => row!.Location.Y);
        }));
    }

    private static void AssertLandscapeColumnAlignment(AndroidDriver driver)
    {
        foreach (var (headerId, valueId) in new[]
        {
            ("CompletedStatsSortPlateAppearances",
                "CompletedBattingPlateAppearancesPosition1"),
            ("CompletedStatsSortHits", "CompletedBattingHitsPosition1"),
            ("CompletedStatsSortBattingAverage",
                "CompletedBattingBattingAveragePosition1"),
            ("CompletedStatsSortSluggingPercentage",
                "CompletedBattingSluggingPercentagePosition1"),
        })
        {
            var header = AndroidElementWait.UntilAutomationId(driver, headerId);
            var value = AndroidElementWait.UntilAutomationId(driver, valueId);
            Assert.InRange(Math.Abs(CenterX(header) - CenterX(value)), 0, 2);
        }
    }

    private static void ScrollStatisticsToRateColumns(AndroidDriver driver)
    {
        var statistics = AndroidElementWait.UntilAutomationId(driver, "CompletedBattingStats");
        var location = statistics.Location;
        var size = statistics.Size;
        driver.ExecuteScript(
            "mobile: swipeGesture",
            new Dictionary<string, object>
            {
                ["left"] = location.X + 10,
                ["top"] = location.Y + 10,
                ["width"] = size.Width - 20,
                ["height"] = Math.Min(size.Height - 20, 180),
                ["direction"] = "left",
                ["percent"] = 0.85,
            });
    }

    private static IWebElement? FindDisplayed(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        return driver.FindElements(By.Id(resourceId))
            .FirstOrDefault(element => element.Displayed);
    }

    private static void AssertViewSelection(AndroidDriver driver, bool statsVisible)
    {
        if (statsVisible)
        {
            AndroidElementWait.UntilAutomationId(driver, "CompletedBattingStats");
            PhaseAUiWorkflow.AssertTextAbsent(driver, "Top 1 — Automation Team");
            return;
        }

        AndroidElementWait.UntilText(driver, "Top 1 — Automation Team");
        AssertNotDisplayed(driver, "CompletedBattingStats");
    }

    private static int CenterX(IWebElement element) =>
        element.Location.X + (element.Size.Width / 2);

    private static void AssertNotDisplayed(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        Assert.DoesNotContain(
            driver.FindElements(By.Id(resourceId)),
            element => element.Displayed);
    }
}
