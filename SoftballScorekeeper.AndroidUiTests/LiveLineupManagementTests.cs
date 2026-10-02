namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class LiveLineupManagementTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Add_in_middle_shows_named_positions_and_preserves_current_batter()
    {
        Run(
            nameof(Add_in_middle_shows_named_positions_and_preserves_current_batter),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "Where should Bailey Live bat?");
                AndroidElementWait.UntilText(driver, "1 — Before Alex Live");
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();

                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Blake Live");
            }
        );
    }

    [AndroidUiFact]
    public void Add_at_end_preserves_append_rotation()
    {
        Run(
            nameof(Add_at_end_preserves_append_rotation),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "4 — End of Lineup").Click();

                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Blake Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Casey Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Bailey Live");
            }
        );
    }

    [AndroidUiFact]
    public void Replace_participates_in_chronological_undo_redo_and_resume()
    {
        Run(
            nameof(Replace_participates_in_chronological_undo_redo_and_resume),
            driver =>
            {
                OpenScoring(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Replace Player").Click();
                AndroidElementWait.UntilText(driver, "2. Blake Live").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );

                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );

                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void Remove_and_readd_uses_selected_middle_position()
    {
        Run(
            nameof(Remove_and_readd_uses_selected_middle_position),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Remove Player").Click();
                AndroidElementWait.UntilText(driver, "1. Alex Live").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Alex Live").Click();
                AndroidElementWait.UntilText(driver, "2 — Before Casey Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Alex Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Alex Live",
                    "Top 1",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void New_player_uses_normal_roster_creation_then_can_join_live_lineup()
    {
        Run(
            nameof(New_player_uses_normal_roster_creation_then_can_join_live_lineup),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Create New Player").Click();
                var entries = AndroidScoringWorkflow.VisibleEditTexts(driver);
                Assert.Equal(2, entries.Length);
                entries[0].SendKeys("Taylor");
                entries[1].SendKeys("Live");
                AndroidElementWait.UntilText(driver, "Save").Click();
                AndroidElementWait.UntilText(driver, "Where should Taylor Live bat?");
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Taylor Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Taylor Live",
                    "Top 1",
                    "0 Outs"
                );

                ExitToWorkspace(driver);
                AndroidElementWait.UntilText(driver, "Reset Game").Click();
                AndroidElementWait.UntilText(driver, "Continue").Click();
                AndroidElementWait.UntilText(driver, "Reset Game").Click();
                AndroidElementWait.UntilText(driver, "Edit Lineup").Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();
                AndroidElementWait.UntilText(driver, "Taylor Live");
            }
        );
    }

    [AndroidUiFact]
    public void Middle_insertion_undo_redo_restores_exact_order()
    {
        Run(
            nameof(Middle_insertion_undo_redo_restores_exact_order),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");

                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Blake Live");
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");

                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Blake Live");
            }
        );
    }

    private void Run(string name, Action<OpenQA.Selenium.Appium.Android.AndroidDriver> scenario) =>
        PhaseAUiWorkflow.Run(
            fixture,
            name,
            scenario,
            AndroidAutomationConstants.LiveLineupTestSeedRequestFileName
        );

    private static void OpenScoring(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
        AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
    }

    private static void OpenManageLineup(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ManageLiveLineupButton").Click();
        AndroidElementWait.UntilText(driver, "Manage Live Lineup");
    }

    private static void ExitToWorkspace(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
    }
}
