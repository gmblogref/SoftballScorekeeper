using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class AdvancedScoringWorkflowTests
{
    private readonly AndroidAppiumFixture fixture;

    public AdvancedScoringWorkflowTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Fielder_choice_retires_forced_runner_and_displaces_batter_to_first()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Fielder_choice_retires_forced_runner_and_displaces_batter_to_first), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.SelectResult(driver, "Fielder's Choice");
            AndroidScoringWorkflow.Step("retire the runner forced from First", () =>
                AndroidElementWait.UntilAutomationId(
                    driver, "RetireRunnerFromFirstButton").Click());
            AndroidScoringWorkflow.AssertRunner(
                driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.ThirdBatterName);
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.FourthBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.ThirdBatterName);
        });
    }

    [AndroidUiFact]
    public void Fielder_choice_batter_retirement_reconciles_to_out_and_survives_resume()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Fielder_choice_batter_retirement_reconciles_to_out_and_survives_resume), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.SelectResult(driver, "Fielder's Choice");
            AndroidScoringWorkflow.Step("retire the batter", () =>
                AndroidElementWait.UntilAutomationId(driver, "RetireBatterButton").Click());
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.ExitAndResume(driver);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AssertReconciledOutState(driver);
        });
    }

    [AndroidUiFact]
    public void Sacrifice_fly_runner_correction_reconciles_to_out()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Sacrifice_fly_runner_correction_reconciles_to_out), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Triple");
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.SelectResult(driver, "Sac Fly");
            AndroidScoringWorkflow.Step("hold the runner from Third", () =>
            {
                AndroidElementWait.UntilAutomationId(
                    driver, "ScoredRunnerFromThirdButton").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "RunnerDestinationThirdButton").Click();
            });
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "0");
        });
    }

    [AndroidUiFact]
    public void Runner_out_attempting_third_records_out_and_preserves_trailing_runner()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Runner_out_attempting_third_records_out_and_preserves_trailing_runner), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");

            AndroidScoringWorkflow.Step("record the lead runner out attempting Third", () =>
            {
                AndroidElementWait.UntilAutomationId(
                    driver, "SecondBaseRunnerButton").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "RunnerOutAttemptingThirdButton").Click();
            });
            AndroidScoringWorkflow.CompletePendingPlay(driver);

            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);
            AndroidScoringWorkflow.AssertEmptyBase(driver, "SecondBaseRunnerButton");
        });
    }

    [AndroidUiFact]
    public void Double_play_retires_runner_and_batter_through_the_scoring_ui()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Double_play_retires_runner_and_batter_through_the_scoring_ui), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Double Play");

            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "2 Outs");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "SecondBaseRunnerButton");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "ThirdBaseRunnerButton");
        });
    }

    [AndroidUiFact]
    public void Third_out_acknowledgement_and_opponent_entry_advance_to_next_inning()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Third_out_acknowledgement_and_opponent_entry_advance_to_next_inning), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "OUT");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.SelectResult(driver, "OUT");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "2 Outs");
            AndroidScoringWorkflow.SelectResult(driver, "OUT");

            AndroidScoringWorkflow.Step("acknowledge the completed half-inning", () =>
                AndroidElementWait.UntilText(driver, "OK").Click());
            AndroidScoringWorkflow.Step("submit the unchanged opponent half-inning totals", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "OpponentHalfInningDialog");
                AndroidElementWait.UntilAutomationId(
                    driver, "OpponentHalfInningSubmitButton").Click();
            });

            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.FourthBatterName, "Top 2", "0 Outs");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "FirstBaseRunnerButton");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "SecondBaseRunnerButton");
            AndroidScoringWorkflow.AssertEmptyBase(driver, "ThirdBaseRunnerButton");
        });
    }

    [AndroidUiFact]
    public void Scoring_state_remains_usable_across_portrait_and_landscape_rotation()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Scoring_state_remains_usable_across_portrait_and_landscape_rotation), driver =>
        {
            AndroidScoringWorkflow.OpenPreparedScoring(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResult(driver, "Single");

            AndroidScoringWorkflow.Step("rotate scoring to portrait", () =>
            {
                fixture.SetOrientation(ScreenOrientation.Portrait);
                AndroidElementWait.UntilInningAndOutState(driver, "Top 1", "0 Outs");
            });
            AndroidScoringWorkflow.Step("correct the lead runner while in portrait", () =>
            {
                AndroidElementWait.UntilAutomationId(
                    driver, "SecondBaseRunnerButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "RunnerCurrentOutcomeLabel", "Current: Second");
                AndroidElementWait.UntilAutomationId(
                    driver, "RunnerDestinationThirdButton").Click();
            });
            AndroidScoringWorkflow.AssertRunner(
                driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);

            AndroidScoringWorkflow.Step("rotate scoring back to landscape", () =>
            {
                fixture.SetOrientation(ScreenOrientation.Landscape);
                AndroidElementWait.UntilAutomationIdText(driver, "InningStateLabel", "Top 1");
                AndroidElementWait.UntilAutomationIdText(driver, "OutsStateLabel", "0 Outs");
            });
            AndroidScoringWorkflow.AssertRunner(
                driver, "ThirdBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "0 Outs");
        });
    }

    private static void AssertReconciledOutState(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidScoringWorkflow.AssertBatterInningAndOuts(
            driver, AndroidAutomationConstants.ThirdBatterName, "Top 1", "1 Out");
        AndroidScoringWorkflow.AssertRunner(
            driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
    }
}
