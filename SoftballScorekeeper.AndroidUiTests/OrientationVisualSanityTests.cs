using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class OrientationVisualSanityTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Core_navigation_roster_schedule_and_data_management_remain_usable_in_both_orientations()
    {
        RunWithDiagnostics(nameof(
            Core_navigation_roster_schedule_and_data_management_remain_usable_in_both_orientations),
            driver =>
            {
                AndroidElementWait.UntilText(driver, "Game Details");
                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilText(driver, "Teams").Click();
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1");
                AssertInBothOrientations(driver, () =>
                {
                    AssertTextWithinViewport(driver, "Teams");
                    AssertAutomationIdWithinViewport(driver, "TeamCard-1");
                });

                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AssertInBothOrientations(driver, () =>
                {
                    AssertAutomationIdWithinViewport(
                        driver,
                        "TeamWorkspaceGamesSectionButton");
                    AssertAutomationIdWithinViewport(
                        driver,
                        "TeamWorkspaceRosterSectionButton");
                    AssertAccessibilityIdWithinViewport(driver, "TeamWorkspaceMenuButton");
                });

                AndroidElementWait.UntilAutomationId(
                    driver,
                    "TeamWorkspaceRosterSectionButton").Click();
                AssertInBothOrientations(driver, () =>
                {
                    AssertReachableTextWithinViewport(driver, "Add Players");
                    AssertReachableTextWithinViewport(driver, "Remove Players");
                });

                AndroidElementWait.UntilAutomationId(
                    driver,
                    "TeamWorkspaceGamesSectionButton").Click();
                AndroidElementWait.UntilText(driver, "Add Game").Click();
                AssertInBothOrientations(driver, () =>
                {
                    AssertTextWithinViewport(driver, "Schedule Game");
                    AssertReachableTextWithinViewport(driver, "Save");
                    AssertReachableTextWithinViewport(driver, "Cancel");
                });
                AndroidElementWait.UntilText(driver, "Cancel").Click();

                PhaseAUiWorkflow.NavigateUp(driver);
                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilText(driver, "Data Management").Click();
                AssertInBothOrientations(driver, () =>
                {
                    AssertAutomationIdWithinViewport(driver, "BackupDataButton");
                    AssertAutomationIdWithinViewport(driver, "RestoreDataButton");
                });
            });
    }

    [AndroidUiFact]
    public void Set_lineup_actions_remain_reachable_in_both_orientations()
    {
        RunWithDiagnostics(
            nameof(Set_lineup_actions_remain_reachable_in_both_orientations),
            driver =>
            {
                AndroidElementWait.UntilText(driver, "Set Lineup").Click();
                AndroidElementWait.UntilText(driver, "Lineup Manager");

                AssertInBothOrientations(driver, () =>
                {
                    AssertReachableTextWithinViewport(driver, "Use Previous Lineup");
                    AssertReachableTextWithinViewport(driver, "Add Players");
                    AssertReachableTextWithinViewport(driver, "Done");
                });
            },
            AndroidAutomationConstants.PhaseALineupTestSeedRequestFileName);
    }

    [AndroidUiFact]
    public void Team_stats_filters_and_table_remain_reachable_in_both_orientations()
    {
        RunWithDiagnostics(
            nameof(Team_stats_filters_and_table_remain_reachable_in_both_orientations),
            driver =>
            {
                PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
                AndroidElementWait.UntilText(driver, "Automation Stats Team");

                AssertInBothOrientations(driver, () =>
                {
                    AssertAutomationIdWithinViewport(driver, "TeamStatsActiveFilter");
                    AssertAutomationIdWithinViewport(driver, "TeamStatsAllGamesFilter");
                    AssertAccessibilityIdWithinViewport(
                        driver,
                        "CompletedBattingPlateAppearancesTeamTotal");
                });
            },
            AndroidAutomationConstants.TeamStatsTestSeedRequestFileName);
    }

    private void RunWithDiagnostics(
        string testName,
        Action<AndroidDriver> action,
        string seedRequestFileName = AndroidAutomationConstants.TestSeedRequestFileName)
    {
        fixture.ResetApplication(seedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            action(driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
            throw new Xunit.Sdk.XunitException(
                $"Android orientation sanity workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
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

    private static void AssertAutomationIdWithinViewport(
        AndroidDriver driver,
        string automationId) =>
        AssertWithinViewport(
            driver,
            () => AndroidElementWait.UntilAutomationId(driver, automationId));

    private static void AssertTextWithinViewport(AndroidDriver driver, string text) =>
        AssertWithinViewport(driver, () => AndroidElementWait.UntilText(driver, text));

    private static void AssertAccessibilityIdWithinViewport(
        AndroidDriver driver,
        string accessibilityId) =>
        AssertWithinViewport(
            driver,
            () => PhaseAUiWorkflow.Wait(driver).Until(current =>
                current.FindElements(MobileBy.AccessibilityId(accessibilityId))
                    .FirstOrDefault(element => element.Displayed)
                ?? throw new NoSuchElementException())!);

    private static void AssertReachableTextWithinViewport(AndroidDriver driver, string text) =>
        AssertWithinViewport(driver, () => PhaseAUiWorkflow.ScrollToText(driver, text));

    private static void AssertWithinViewport(
        AndroidDriver driver,
        Func<IWebElement> findElement)
    {
        PhaseAUiWorkflow.Wait(driver).Until(_ =>
        {
            try
            {
                var element = findElement();
                var viewport = driver.Manage().Window.Size;
                var location = element.Location;
                var size = element.Size;
                return element.Displayed
                    && size.Width > 0
                    && size.Height > 0
                    && location.X >= 0
                    && location.Y >= 0
                    && location.X < viewport.Width
                    && location.Y < viewport.Height
                    && location.X + size.Width <= viewport.Width
                    && location.Y + size.Height <= viewport.Height;
            }
            catch (StaleElementReferenceException)
            {
                return false;
            }
        });
    }
}
