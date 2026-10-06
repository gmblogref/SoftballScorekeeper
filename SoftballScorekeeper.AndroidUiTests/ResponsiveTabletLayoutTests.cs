using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ResponsiveTabletLayoutTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Root_team_workflow_and_add_players_actions_remain_reachable()
    {
        Run(
            nameof(Root_team_workflow_and_add_players_actions_remain_reachable),
            driver =>
            {
                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilAutomationId(driver, "TeamsFlyoutMenuItem").Click();
                AndroidElementWait.UntilText(driver, "Create Team");

                AssertInBothOrientations(
                    driver,
                    () =>
                    {
                        AssertWithinViewport(driver, AndroidElementWait.UntilText(driver, "Teams"));
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilText(driver, "Players")
                        );
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1")
                        );
                    }
                );

                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                var rosterSectionButton = AndroidElementWait.UntilAutomationId(
                    driver,
                    "TeamWorkspaceRosterSectionButton"
                );
                AssertNoVisibleExactText(driver, "Teams");
                AssertNoVisibleExactText(driver, "Players");
                rosterSectionButton.Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();

                AssertInBothOrientations(
                    driver,
                    () =>
                    {
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilAutomationId(
                                driver,
                                "AddPlayersSelectAllButton"
                            )
                        );
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilAutomationId(driver, "AddPlayersClearAllButton")
                        );
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilAutomationId(
                                driver,
                                "CreatePlayerFromTeamButton"
                            )
                        );
                        AssertWithinViewport(
                            driver,
                            AndroidElementWait.UntilAutomationId(
                                driver,
                                "AddSelectedPlayersToTeamButton"
                            )
                        );
                    }
                );

                driver.Navigate().Back();
                AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton");
                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilAutomationId(driver, "PlayersFlyoutMenuItem").Click();
                AndroidElementWait.UntilText(driver, "Add Player");
            }
        );
    }

    [AndroidUiFact]
    public void Scoring_controls_remain_reachable_in_compact_landscape_and_portrait()
    {
        Run(
            nameof(Scoring_controls_remain_reachable_in_compact_landscape_and_portrait),
            driver =>
            {
                AndroidScoringWorkflow.OpenPreparedScoring(driver);
                AssertInBothOrientations(
                    driver,
                    () =>
                    {
                        foreach (
                            var automationId in new[]
                            {
                                "ScoringMenuButton",
                                "HomeScoreValue",
                                "VisitorScoreValue",
                                "FirstBaseRunnerButton",
                                "SecondBaseRunnerButton",
                                "ThirdBaseRunnerButton",
                                "CurrentBatterButton",
                                "OnDeckBatter",
                                "OutsThisInningHeading",
                            }
                        )
                            AssertWithinViewport(
                                driver,
                                AndroidElementWait.UntilAutomationId(driver, automationId)
                            );

                        foreach (
                            var automationId in new[]
                            {
                                "ScoringMenuButton",
                                "FirstBaseRunnerButton",
                                "SecondBaseRunnerButton",
                                "ThirdBaseRunnerButton",
                                "CurrentBatterButton",
                            }
                        )
                            Assert.True(
                                AndroidElementWait
                                    .UntilAutomationId(driver, automationId)
                                    .Size.Height >= 44,
                                $"{automationId} must remain a practical touch target."
                            );
                    }
                );
            }
        );
    }

    private void Run(string testName, Action<AndroidDriver> scenario)
    {
        fixture.ResetApplication();
        try
        {
            scenario(fixture.Driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
            throw new Xunit.Sdk.XunitException(
                $"Responsive tablet workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}"
            );
        }
    }

    private void AssertInBothOrientations(AndroidDriver driver, Action assertion)
    {
        fixture.SetOrientation(ScreenOrientation.Landscape);
        assertion();
        fixture.SetOrientation(ScreenOrientation.Portrait);
        assertion();
        fixture.SetOrientation(ScreenOrientation.Landscape);
    }

    private static void AssertWithinViewport(AndroidDriver driver, IWebElement element)
    {
        var viewport = driver.Manage().Window.Size;
        Assert.True(element.Displayed);
        Assert.True(element.Size.Width > 0);
        Assert.True(element.Size.Height > 0);
        Assert.InRange(element.Location.X, 0, viewport.Width - 1);
        Assert.InRange(element.Location.Y, 0, viewport.Height - 1);
        Assert.InRange(element.Location.X + element.Size.Width, 1, viewport.Width);
        Assert.InRange(element.Location.Y + element.Size.Height, 1, viewport.Height);
    }

    private static void AssertNoVisibleExactText(AndroidDriver driver, string text) =>
        Assert.DoesNotContain(
            driver.FindElements(By.XPath($"//*[@text='{text}']")),
            element => element.Displayed
        );
}
