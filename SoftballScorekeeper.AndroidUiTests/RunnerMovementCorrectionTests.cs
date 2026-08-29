using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class RunnerMovementCorrectionTests
{
    private readonly AndroidAppiumFixture fixture;

    public RunnerMovementCorrectionTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Pending_runner_can_be_moved_directly_and_restores_after_resume()
    {
        fixture.ResetApplication();
        var driver = fixture.Driver;
        try
        {
            OpenPreparedScoring(driver);
            SelectSingle(driver);
            AndroidElementWait.UntilAutomationId(driver, "NextBatterButton").Click();

            SelectSingle(driver);
            AssertRunner(driver, "SecondBaseRunnerButton",
                AndroidAutomationConstants.FirstBatterName);
            AssertRunner(driver, "FirstBaseRunnerButton",
                AndroidAutomationConstants.SecondBatterName);

            Step("open Alex's runner picker and cancel without changing Second", () =>
            {
                AndroidElementWait.UntilAutomationId(
                    driver, "SecondBaseRunnerButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "RunnerCurrentOutcomeLabel", "Current: Second");
                AndroidElementWait.UntilAutomationId(
                    driver, "RunnerDestinationCancelButton").Click();
                AssertRunner(driver, "SecondBaseRunnerButton",
                    AndroidAutomationConstants.FirstBatterName);
            });

            Step("move Alex from the pending Second outcome to Third", () =>
            {
                AndroidElementWait.UntilAutomationId(
                    driver, "SecondBaseRunnerButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "RunnerCurrentOutcomeLabel", "Current: Second");
                AndroidElementWait.UntilAutomationId(
                    driver, "RunnerDestinationThirdButton").Click();
            });
            AssertRunner(driver, "ThirdBaseRunnerButton",
                AndroidAutomationConstants.FirstBatterName);
            AssertRunner(driver, "FirstBaseRunnerButton",
                AndroidAutomationConstants.SecondBatterName);

            Step("commit the corrected play and advance to Casey", () =>
                AndroidElementWait.UntilAutomationId(driver, "NextBatterButton").Click());
            AssertScoringState(driver);

            Step("exit and resume the corrected scoring state", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
            });
            AssertScoringState(driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Pending_runner_can_be_moved_directly_and_restores_after_resume),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android runner correction workflow failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}");
        }
    }

    private static void OpenPreparedScoring(AndroidDriver driver)
    {
        Step("open the prepared Game Workspace", () =>
        {
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CurrentBatter", AndroidAutomationConstants.FirstBatterName);
        });
    }

    private static void SelectSingle(AndroidDriver driver) =>
        Step("select Single for the current batter", () =>
        {
            AndroidElementWait.UntilAutomationId(driver, "CurrentBatter").Click();
            AndroidElementWait.UntilText(driver, "Single").Click();
        });

    private static void AssertScoringState(AndroidDriver driver)
    {
        AssertRunner(driver, "ThirdBaseRunnerButton",
            AndroidAutomationConstants.FirstBatterName);
        AssertRunner(driver, "FirstBaseRunnerButton",
            AndroidAutomationConstants.SecondBatterName);
        Step("verify Casey, inning, outs, and score", () =>
        {
            AndroidElementWait.UntilAutomationIdText(
                driver, "CurrentBatter", AndroidAutomationConstants.ThirdBatterName);
            AndroidElementWait.UntilInningAndOutState(driver, "Top 1", "0 Outs");
            AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "0");
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "0");
        });
    }

    private static void AssertRunner(
        AndroidDriver driver,
        string automationId,
        string playerName) =>
        Step($"verify {playerName} at {automationId}", () =>
            AndroidElementWait.UntilAutomationIdText(driver, automationId, playerName));

    private static void Step(string description, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to {description}.", exception);
        }
    }
}
