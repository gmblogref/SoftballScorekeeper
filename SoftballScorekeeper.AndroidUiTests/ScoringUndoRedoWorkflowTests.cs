using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ScoringUndoRedoWorkflowTests
{
    private readonly AndroidAppiumFixture fixture;

    public ScoringUndoRedoWorkflowTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Committed_single_can_be_undone_and_redone()
    {
        AndroidScoringWorkflow.Run(
            fixture,
            nameof(Committed_single_can_be_undone_and_redone),
            driver =>
            {
                AndroidScoringWorkflow.OpenPreparedScoring(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Single");
                AndroidScoringWorkflow.CompletePendingPlay(driver);
                AndroidScoringWorkflow.AssertRunner(
                    driver,
                    "FirstBaseRunnerButton",
                    AndroidAutomationConstants.FirstBatterName
                );
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.SecondBatterName,
                    "Top 1",
                    "0 Outs"
                );

                SelectHistoryAction(driver, "UndoScoringButton");
                AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.FirstBatterName,
                    "Top 1",
                    "0 Outs"
                );

                SelectHistoryAction(driver, "RedoScoringButton");
                AndroidScoringWorkflow.AssertRunner(
                    driver,
                    "FirstBaseRunnerButton",
                    AndroidAutomationConstants.FirstBatterName
                );
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.SecondBatterName,
                    "Top 1",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void Visible_controls_undo_and_redo_pending_single_in_both_orientations()
    {
        AndroidScoringWorkflow.Run(
            fixture,
            nameof(Visible_controls_undo_and_redo_pending_single_in_both_orientations),
            driver =>
            {
                AndroidScoringWorkflow.OpenPreparedScoring(driver);
                AssertHistoryControlsBelowPanels(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Single");
                AndroidScoringWorkflow.AssertRunner(
                    driver, "FirstBaseRunnerButton",
                    AndroidAutomationConstants.FirstBatterName);

                SelectHistoryAction(driver, "UndoScoringButton");
                AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.FirstBatterName, "Top 1", "0 Outs");
                SelectHistoryAction(driver, "RedoScoringButton");
                AndroidScoringWorkflow.AssertRunner(
                    driver, "FirstBaseRunnerButton",
                    AndroidAutomationConstants.FirstBatterName);

                AndroidScoringWorkflow.Step("rotate to portrait and verify visible placement", () =>
                {
                    fixture.SetOrientation(ScreenOrientation.Portrait);
                    AndroidElementWait.UntilInningAndOutState(driver, "Top 1", "0 Outs");
                    AssertHistoryControlsBelowPanels(driver);
                });
            }
        );
    }

    [AndroidUiFact]
    public void Third_out_transition_can_be_undone_and_redone_exactly_once()
    {
        AndroidScoringWorkflow.Run(
            fixture,
            nameof(Third_out_transition_can_be_undone_and_redone_exactly_once),
            driver =>
            {
                AndroidScoringWorkflow.OpenPreparedScoring(driver);
                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidScoringWorkflow.SelectResult(driver, "OUT");
                AndroidScoringWorkflow.Step(
                    "complete the half-inning transition",
                    () =>
                    {
                        AndroidElementWait.UntilText(driver, "OK").Click();
                        AndroidElementWait.UntilAutomationId(driver, "OpponentHalfInningDialog");
                        AndroidElementWait
                            .UntilAutomationId(driver, "OpponentHalfInningSubmitButton")
                            .Click();
                    }
                );
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.FourthBatterName,
                    "Top 2",
                    "0 Outs"
                );

                SelectHistoryAction(driver, "UndoScoringButton");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.ThirdBatterName,
                    "Top 1",
                    "2 Outs"
                );

                SelectHistoryAction(driver, "RedoScoringButton");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    AndroidAutomationConstants.FourthBatterName,
                    "Top 2",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void Home_opening_opponent_entry_undo_can_redo_reenter_or_change_to_away()
    {
        AndroidScoringWorkflow.Run(
            fixture,
            nameof(Home_opening_opponent_entry_undo_can_redo_reenter_or_change_to_away),
            driver =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
                AndroidElementWait.UntilText(driver, "Home").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "ConfirmGameRulesButton").Click();
                SubmitOpponentEntry(driver, "2", "1");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.FirstBatterName,
                    "Bottom 1", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "VisitorScoreValue", "2");

                SelectHistoryAction(driver, "UndoScoringButton");
                var startupRedo = AndroidElementWait.UntilAutomationId(
                    driver, "StartupRedoScoringButton");
                Assert.True(startupRedo.Enabled);
                AndroidElementWait.UntilAutomationId(
                    driver, "EnterOpponentHalfInningButton");

                startupRedo.Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.FirstBatterName,
                    "Bottom 1", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "VisitorScoreValue", "2");

                SelectHistoryAction(driver, "UndoScoringButton");
                AndroidElementWait.UntilAutomationId(
                    driver, "EnterOpponentHalfInningButton").Click();
                SubmitOpponentEntry(driver, "3", "0");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "VisitorScoreValue", "3");
                Assert.False(AndroidElementWait.UntilAutomationId(
                    driver, "RedoScoringButton").Enabled);

                SelectHistoryAction(driver, "UndoScoringButton");
                AndroidElementWait.UntilAutomationId(
                    driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilText(driver, "Game Rules").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "AwayTeamSideOption").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "ConfirmGameRulesButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.FirstBatterName,
                    "Top 1", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "VisitorScoreValue", "0");
            });
    }

    private static void SubmitOpponentEntry(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string runs,
        string homeRuns)
    {
        AndroidElementWait.UntilAutomationId(driver, "OpponentHalfInningDialog");
        var entries = AndroidScoringWorkflow.VisibleEditTexts(driver);
        Assert.Equal(2, entries.Length);
        entries[0].SendKeys(runs);
        entries[1].SendKeys(homeRuns);
        AndroidElementWait.UntilAutomationId(
            driver, "OpponentHalfInningSubmitButton").Click();
    }

    private static void SelectHistoryAction(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string automationId
    ) =>
        AndroidScoringWorkflow.Step(
            $"select {automationId}",
            () => AndroidElementWait.UntilAutomationId(driver, automationId).Click()
        );

    private static void AssertHistoryControlsBelowPanels(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        var upcoming = AndroidElementWait.UntilText(driver, "UPCOMING");
        var outs = AndroidElementWait.UntilText(driver, "OUTS THIS INNING");
        var undo = AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton");
        var redo = AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton");
        Assert.True(undo.Displayed && redo.Displayed);
        Assert.True(undo.Location.Y > upcoming.Location.Y);
        Assert.True(redo.Location.Y > outs.Location.Y);
    }
}
