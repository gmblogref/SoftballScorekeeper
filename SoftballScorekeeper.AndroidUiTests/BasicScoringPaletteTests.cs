using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class BasicScoringPaletteTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Home_scoring_palette_advances_runners_scores_and_inning_in_both_orientations()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Home_scoring_palette_advances_runners_scores_and_inning_in_both_orientations),
            driver =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
                AndroidElementWait.UntilText(driver, "Home").Click();
                AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "OpponentHalfInningDialog");
                AndroidElementWait.UntilAutomationId(
                    driver, "OpponentHalfInningSubmitButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.FirstBatterName, "Bottom 1", "0 Outs");

                AndroidScoringWorkflow.SelectResult(driver, "Double");
                AndroidScoringWorkflow.AssertRunner(
                    driver, "SecondBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
                AndroidScoringWorkflow.CompletePendingPlay(driver);

                fixture.SetOrientation(ScreenOrientation.Portrait);
                AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
                AndroidScoringWorkflow.SelectResult(driver, "Triple");
                AndroidScoringWorkflow.AssertRunner(
                    driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);
                AndroidScoringWorkflow.CompletePendingPlay(driver);
                AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "1");

                fixture.SetOrientation(ScreenOrientation.Landscape);
                AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "3");
                AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
                AndroidScoringWorkflow.AssertEmptyBase(driver, "SecondBaseRunnerButton");
                AndroidScoringWorkflow.AssertEmptyBase(driver, "ThirdBaseRunnerButton");

                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidElementWait.UntilText(driver, "OK").Click();
                var opponentEntries = AndroidScoringWorkflow.VisibleEditTexts(driver);
                Assert.Equal(2, opponentEntries.Length);
                opponentEntries[0].SendKeys("2");
                opponentEntries[1].SendKeys("0");
                AndroidElementWait.UntilAutomationId(
                    driver, "OpponentHalfInningSubmitButton").Click();

                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, "Gray Automation", "Bottom 2", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "3");
                AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "2");
            });
    }
}
