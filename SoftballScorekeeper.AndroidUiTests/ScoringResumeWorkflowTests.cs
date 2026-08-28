using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ScoringResumeWorkflowTests
{
    private readonly AndroidAppiumFixture fixture;

    public ScoringResumeWorkflowTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Single_next_batter_exit_and_resume_preserves_visible_scoring_state()
    {
        var driver = fixture.Driver;
        try
        {
            Step("wait for the prepared Game Workspace", () =>
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton"));
            Step("open first-start scoring setup", () =>
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click());
            Step("confirm the default Away side and Game Rules", () =>
            {
                Assert.Equal("true", AndroidElementWait
                    .UntilAutomationId(driver, "AwayTeamSideOption")
                    .GetAttribute("checked"));
                AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            });

            AssertScoringState(driver, AndroidAutomationConstants.FirstBatterName,
                expectedFirstBaseRunner: string.Empty);

            Step("select Single for the first batter", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "CurrentBatter").Click();
                AndroidElementWait.UntilText(driver, "Single").Click();
            });
            Step("verify the pending batter-runner on First", () =>
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName));

            Step("advance to the next batter", () =>
                AndroidElementWait.UntilAutomationId(driver, "NextBatterButton").Click());
            AssertScoringState(driver, AndroidAutomationConstants.SecondBatterName,
                AndroidAutomationConstants.FirstBatterName);

            Step("exit scoring through the scoring menu", () =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
            });
            Step("resume scoring", () =>
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click());

            AssertScoringState(driver, AndroidAutomationConstants.SecondBatterName,
                AndroidAutomationConstants.FirstBatterName);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Single_next_batter_exit_and_resume_preserves_visible_scoring_state),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android scoring workflow failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}");
        }
    }

    private static void AssertScoringState(
        AndroidDriver driver,
        string expectedBatter,
        string expectedFirstBaseRunner)
    {
        Step($"verify Current Batter is {expectedBatter}", () =>
            AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", expectedBatter));
        Step("verify First Base state", () =>
            AndroidElementWait.UntilAutomationIdText(
                driver, "FirstBaseRunnerButton", expectedFirstBaseRunner));
        Step("verify inning and out state", () =>
            AndroidElementWait.UntilAutomationIdText(
                driver, "InningOutStateLabel", "Top 1    0 Outs"));
        Step("verify the score remains tied at zero", () =>
        {
            AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "0");
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "0");
        });
    }

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
