using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ScoringResetWorkflowTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Reset_cancel_confirm_cold_reopen_and_new_action_clear_scoring_history()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Reset_cancel_confirm_cold_reopen_and_new_action_clear_scoring_history), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Strikeout");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.ExitAndResume(driver);
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);

            ExitToWorkspace(driver);
            AndroidElementWait.UntilText(driver, "Reset Game").Click();
            AndroidElementWait.UntilText(driver, "Reset Scoring?");
            AndroidElementWait.UntilText(driver, "Cancel").Click();
            AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);

            ExitToWorkspace(driver);
            AndroidElementWait.UntilText(driver, "Reset Game").Click();
            AndroidElementWait.UntilText(driver, "Continue").Click();
            AndroidElementWait.UntilText(driver, "Reset Game").Click();
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
            AssertNoVisibleText(driver, "Resume Game");

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            AndroidElementWait.UntilAutomationId(driver, "GameScheduleStatus1").Click();
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
            AssertNoVisibleText(driver, "Resume Game");
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            AssertFreshScoringState(driver);

            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
            Assert.True(IsEnabled(AndroidElementWait.UntilAutomationId(
                driver, "RedoScoringButton")));
            AndroidScoringWorkflow.SelectResult(driver, "OUT");
            Assert.False(IsEnabled(AndroidElementWait.UntilAutomationId(
                driver, "RedoScoringButton")));
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
        });
    }

    private static void ExitToWorkspace(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
    }

    private static void AssertFreshScoringState(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidScoringWorkflow.AssertBatterInningAndOuts(
            driver, AndroidAutomationConstants.FirstBatterName, "Top 1", "0 Outs");
        AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
        AndroidScoringWorkflow.AssertEmptyBase(driver, "SecondBaseRunnerButton");
        AndroidScoringWorkflow.AssertEmptyBase(driver, "ThirdBaseRunnerButton");
        AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "0");
        AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "0");
        Assert.False(IsEnabled(AndroidElementWait.UntilAutomationId(
            driver, "UndoScoringButton")));
        Assert.False(IsEnabled(AndroidElementWait.UntilAutomationId(
            driver, "RedoScoringButton")));
    }

    private static bool IsEnabled(IWebElement element) =>
        string.Equals(element.GetAttribute("enabled"), "true", StringComparison.Ordinal);

    private static void AssertNoVisibleText(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string text) =>
        Assert.DoesNotContain(
            driver.FindElements(By.XPath($"//*[@text='{text}']")), item => item.Displayed);
}
