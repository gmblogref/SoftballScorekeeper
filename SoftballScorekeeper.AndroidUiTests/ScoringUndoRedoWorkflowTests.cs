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
