using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class BeerScoringIntegrationTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void In_game_stats_viewer_reflects_committed_scoring_and_returns_to_game()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            In_game_stats_viewer_reflects_committed_scoring_and_returns_to_game), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Home Run");
            AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "InGameStatsButton").Click();

            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingPlateAppearancesPosition1", "1");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingHitsPosition1", "1");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingHomeRunsPosition1", "1");
            AndroidElementWait.UntilAutomationId(driver, "CloseInGameStatsButton").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CurrentBatter", AndroidAutomationConstants.SecondBatterName);
        });
    }

    [AndroidUiFact]
    public void Solo_home_run_and_stacked_strikeout_two_outs_survive_undo_redo()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Solo_home_run_and_stacked_strikeout_two_outs_survive_undo_redo), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "OUT");
            AndroidScoringWorkflow.SelectResult(driver, "Home Run");
            for (var batter = 3; batter <= 10; batter++)
                AndroidScoringWorkflow.SelectResult(driver, "Walk");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.FirstBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.SelectResult(driver, "Strikeout");

            AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.FirstBatterName, "Top 1", "1 Out");
            AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "2 Outs");

            OpenBeerTotalsFromScoring(driver);
            AssertBeerCell(driver, "SoloHomeRun", 2, "1");
            AssertBeerCell(driver, "Owed", 2, "1");
            AssertBeerCell(driver, "Strikeout", 1, "1");
            AssertBeerCell(driver, "TwoOuts", 1, "1");
            AssertBeerCell(driver, "Owed", 1, "3");
            AssertUnrelatedRulesAreZero(driver, 1, "Dbo", "SoloHomeRun", "DoublePlay", "TriplePlay");
        });
    }

    [AndroidUiFact]
    public void Double_play_reaches_beer_totals_without_unrelated_consequences()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Double_play_reaches_beer_totals_without_unrelated_consequences), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Double Play");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "2 Outs");

            OpenBeerTotalsFromScoring(driver);
            AssertBeerCell(driver, "DoublePlay", 2, "1");
            AssertBeerCell(driver, "TwoOuts", 2, "0");
            AssertBeerCell(driver, "Owed", 2, "1");
            AssertUnrelatedRulesAreZero(
                driver, 2, "Dbo", "SoloHomeRun", "TriplePlay", "Strikeout");
        });
    }

    [AndroidUiFact]
    public void Triple_play_records_once_without_two_outs_consequence()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Triple_play_records_once_without_two_outs_consequence), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Double");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Walk");
            AndroidScoringWorkflow.SelectResult(driver, "Triple Play");
            AndroidElementWait.UntilText(driver, "OK").Click();
            AndroidElementWait.UntilAutomationId(
                driver, "OpponentHalfInningSubmitButton").Click();

            OpenBeerTotalsFromScoring(driver);
            AssertBeerCell(driver, "TriplePlay", 4, "1");
            AssertBeerCell(driver, "TwoOuts", 4, "0");
            AssertBeerCell(driver, "Owed", 4, "1");
            AssertUnrelatedRulesAreZero(driver, 4, "Dbo", "SoloHomeRun", "DoublePlay", "Strikeout");
        });
    }

    [AndroidUiFact]
    public void Disabled_dbo_stays_absent_and_reenabled_rule_applies_only_to_future_play()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Disabled_dbo_stays_absent_and_reenabled_rule_applies_only_to_future_play), driver =>
        {
            StartScoringWithHomeRunLimit(driver, "0");
            SetGameBeerRuleEnabled(driver, "Dbo", false);
            AndroidScoringWorkflow.SelectResult(driver, "Dead Ball Out");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "1 Out");

            SetGameBeerRuleEnabled(driver, "Dbo", true);
            AndroidScoringWorkflow.SelectResult(driver, "Dead Ball Out");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "2 Outs");

            OpenBeerTotalsFromScoring(driver);
            AssertBeerCell(driver, "Dbo", 1, "0");
            AssertBeerCell(driver, "Owed", 1, "0");
            AssertBeerCell(driver, "Dbo", 2, "1");
            AssertBeerCell(driver, "TwoOuts", 2, "0");
            AssertBeerCell(driver, "Owed", 2, "1");
            AssertUnrelatedRulesAreZero(
                driver, 2, "SoloHomeRun", "DoublePlay", "TriplePlay", "Strikeout");
        });
    }

    private static void StartScoringWithHomeRunLimit(AndroidDriver driver, string limit)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
        var entry = AndroidScoringWorkflow.VisibleEditTexts(driver).Single();
        entry.Clear();
        entry.SendKeys(limit);
        AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
        AndroidElementWait.UntilAutomationIdText(
            driver, "CurrentBatter", AndroidAutomationConstants.FirstBatterName);
    }

    private static void SetGameBeerRuleEnabled(
        AndroidDriver driver,
        string ruleName,
        bool enabled)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "GameBeerRulesButton").Click();
        var resourceId = ResourceId($"BeerRule{ruleName}Toggle");
        var toggle = driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().scrollable(true))"
            + $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
        var expected = enabled.ToString().ToLowerInvariant();
        if (!string.Equals(toggle.GetAttribute("checked"), expected, StringComparison.Ordinal))
        {
            toggle.Click();
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            wait.Until(_ => string.Equals(
                toggle.GetAttribute("checked"), expected, StringComparison.Ordinal));
        }
        AndroidElementWait.UntilAutomationId(driver, "SaveBeerRulesButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
    }

    private static void OpenBeerTotalsFromScoring(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
        driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
        AndroidElementWait.UntilText(driver, "Beer Totals");
    }

    private static void AssertBeerCell(
        AndroidDriver driver,
        string column,
        int playerId,
        string expected) =>
        AndroidElementWait.UntilAutomationIdText(
            driver, $"BeerTotals{column}-{playerId}", expected);

    private static void AssertUnrelatedRulesAreZero(
        AndroidDriver driver,
        int playerId,
        params string[] columns) =>
        Assert.All(columns, column => AssertBeerCell(driver, column, playerId, "0"));

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
}
