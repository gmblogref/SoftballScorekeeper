using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

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
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingOnBasePercentagePosition1", "1.000");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingSluggingPercentagePosition1", "4.000");
        AssertNotDisplayed(driver, "ResumeGameButton");
    }

    private static void AssertNotDisplayed(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        Assert.DoesNotContain(
            driver.FindElements(By.Id(resourceId)),
            element => element.Displayed);
    }
}
