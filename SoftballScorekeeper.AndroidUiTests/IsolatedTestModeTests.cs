using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class IsolatedTestModeTests(AndroidAppiumFixture fixture)
{
    private const string NormalTeam = "Automation Team";

    [AndroidUiFact]
    public void Test_mode_isolates_data_and_resets_navigation()
    {
        Run(
            nameof(Test_mode_isolates_data_and_resets_navigation),
            driver =>
            {
                AndroidElementWait.UntilText(driver, NormalTeam);
                EnterTestMode(driver);
                AssertTestModeRoot(driver, NormalTeam);

                const string testTeam = "TEST MODE KEEP TEAM";
                CreateTeam(driver, testTeam);
                ExitTestMode(driver, "Keep Test Data");
                AndroidElementWait.UntilText(driver, NormalTeam);
                AssertTextAbsent(driver, testTeam);

                EnterTestMode(driver);
                AndroidElementWait.UntilText(driver, testTeam);
                AssertTestModeIndicator(driver);
                ExitTestMode(driver, "Keep Test Data");
            }
        );
    }

    [AndroidUiFact]
    public void Delete_test_data_never_deletes_production()
    {
        Run(
            nameof(Delete_test_data_never_deletes_production),
            driver =>
            {
                AndroidElementWait.UntilText(driver, NormalTeam);
                EnterTestMode(driver);

                const string testTeam = "DELETE ONLY THIS TEST TEAM";
                CreateTeam(driver, testTeam);
                ExitTestMode(driver, "Delete Test Data");
                AndroidElementWait.UntilText(driver, NormalTeam);
                AssertTextAbsent(driver, testTeam);

                EnterTestMode(driver);
                AndroidElementWait.UntilText(driver, "Create Team");
                AssertTextAbsent(driver, testTeam);
                AssertTestModeIndicator(driver);
                ExitTestMode(driver, "Keep Test Data");
            }
        );
    }

    [AndroidUiFact]
    public void Test_mode_persists_and_disables_backup_restore_with_global_indicator()
    {
        Run(
            nameof(Test_mode_persists_and_disables_backup_restore_with_global_indicator),
            driver =>
            {
                AndroidElementWait.UntilText(driver, NormalTeam);
                EnterTestMode(driver);

                const string testTeam = "PERSISTED TEST MODE TEAM";
                CreateTeam(driver, testTeam);
                fixture.RelaunchApplicationWithoutReset();
                driver = fixture.Driver;

                AndroidElementWait.UntilText(driver, testTeam);
                AssertTestModeIndicator(driver);
                OpenShellFlyout(driver);
                AndroidElementWait.UntilText(driver, "Data Management").Click();
                Assert.False(
                    AndroidElementWait.UntilAutomationId(driver, "BackupDataButton").Enabled
                );
                Assert.False(
                    AndroidElementWait.UntilAutomationId(driver, "RestoreDataButton").Enabled
                );
                AndroidElementWait.UntilAutomationIdText(
                    driver,
                    "PortableDataProductionOnlyMessage",
                    "Backup and Restore are available only in Production Mode."
                );
                AssertTestModeIndicator(driver);

                OpenShellFlyout(driver);
                AndroidElementWait.UntilText(driver, "Teams").Click();
                AndroidElementWait.UntilText(driver, testTeam).Click();
                AndroidElementWait.UntilText(driver, testTeam);
                AssertTestModeIndicator(driver);
                AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
                AndroidElementWait.UntilText(driver, "Beer Rules").Click();
                AndroidElementWait.UntilText(driver, "Team Beer Rule Defaults");
                AssertTestModeIndicator(driver);

                fixture.SetOrientation(ScreenOrientation.Portrait);
                AssertTestModeIndicator(driver);
                fixture.SetOrientation(ScreenOrientation.Landscape);
                AssertTestModeIndicator(driver);

                AndroidElementWait.UntilText(driver, "Cancel").Click();
                ReturnToRootAndExitTestMode(driver);
            }
        );
    }

    [AndroidUiFact]
    public void Test_mode_scoring_persists_without_changing_production_scoring()
    {
        Run(
            nameof(Test_mode_scoring_persists_without_changing_production_scoring),
            driver =>
            {
                StartScoring(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Single");
                AndroidScoringWorkflow.CompletePendingPlay(driver);
                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
                ExitScoring(driver);

                EnterTestMode(driver);
                AssertTestModeRoot(driver, NormalTeam);
                CreateTeam(driver, "TEST MODE SCORING TEAM");
                CreateTestPlayerAndGame(driver);

                AndroidElementWait.UntilText(driver, "TEST SCORING OPPONENT").Click();
                AndroidElementWait.UntilText(driver, "Set Lineup").Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();
                var player = AndroidElementWait.UntilText(driver, "Test Scorer");
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, player).Click();
                AndroidElementWait.UntilText(driver, "Add Selected").Click();
                AndroidElementWait.UntilText(driver, "1. Test Scorer");
                AndroidElementWait.UntilText(driver, "Done").Click();

                StartScoring(driver);
                AssertTestModeIndicator(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Single");
                AndroidScoringWorkflow.CompletePendingPlay(driver);
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", "Test Scorer");
                fixture.SetOrientation(ScreenOrientation.Portrait);
                AssertTestModeIndicator(driver);
                fixture.SetOrientation(ScreenOrientation.Landscape);
                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", "Test Scorer");
                AssertTestModeIndicator(driver);
                ExitScoring(driver);

                fixture.RelaunchApplicationWithoutReset();
                driver = fixture.Driver;
                AndroidElementWait.UntilText(driver, "TEST MODE SCORING TEAM");
                AssertTestModeIndicator(driver);
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "GameScheduleStatus1", "In Progress").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", "Test Scorer");
                AssertTestModeIndicator(driver);
                ExitScoring(driver);

                ReturnToRootAndExitTestMode(driver);
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "GameScheduleStatus1", "In Progress").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver, AndroidAutomationConstants.SecondBatterName, "Top 1", "0 Outs");
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", AndroidAutomationConstants.FirstBatterName);
                AssertIndicatorAbsent(driver);
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
                $"Android isolated Test Mode workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}"
            );
        }
    }

    private static void EnterTestMode(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AssertTextAbsent(driver, "Exit Test Mode");
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Enter Test Mode?");
        Assert.Contains("separate, disposable data", driver.PageSource, StringComparison.Ordinal);
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertTestModeIndicator(driver);
    }

    private static void ExitTestMode(AndroidDriver driver, string choice)
    {
        OpenShellFlyout(driver);
        AssertTextAbsent(driver, "Enter Test Mode");
        AndroidElementWait.UntilText(driver, "Exit Test Mode").Click();
        AndroidElementWait.UntilText(driver, choice).Click();
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertIndicatorAbsent(driver);
    }

    private static void ReturnToRootAndExitTestMode(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Exit Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Keep Test Data").Click();
        AndroidElementWait.UntilText(driver, NormalTeam);
        AssertIndicatorAbsent(driver);
    }

    private static void CreateTeam(AndroidDriver driver, string teamName)
    {
        AndroidElementWait.UntilText(driver, "Create Team").Click();
        var entries = Wait(driver)
            .Until(current =>
            {
                var visible = current
                    .FindElements(By.XPath("//android.widget.EditText"))
                    .Where(element => element.Displayed)
                    .ToArray();
                return visible.Length >= 2 ? visible : null;
            })!;
        entries[0].SendKeys(teamName);
        entries[1].Clear();
        entries[1].SendKeys("2026");
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilText(driver, teamName);
    }

    private static void CreateTestPlayerAndGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidElementWait.UntilAutomationId(
            driver, "TeamWorkspaceRosterSectionButton").Click();
        AndroidElementWait.UntilText(driver, "Add Players").Click();
        AndroidElementWait.UntilText(driver, "Create New Player").Click();
        var entries = PhaseAUiWorkflow.VisibleEditTexts(driver);
        Assert.Equal(2, entries.Length);
        entries[0].SendKeys("Test");
        entries[1].SendKeys("Scorer");
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilText(driver, "Test Scorer");

        AndroidElementWait.UntilAutomationId(
            driver, "TeamWorkspaceGamesSectionButton").Click();
        AndroidElementWait.UntilText(driver, "Add Game").Click();
        AndroidElementWait.UntilText(driver, "Schedule Game");
        EnableCreateNewSeason(driver);
        AndroidElementWait.UntilText(driver, "Enter Season name")
            .SendKeys("Test Season");
        AndroidElementWait.UntilText(driver, "Optional opponent")
            .SendKeys("TEST SCORING OPPONENT");
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilText(driver, "TEST SCORING OPPONENT");
    }

    private static void StartScoring(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatter");
    }

    private static void ExitScoring(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
    }

    private static void EnableCreateNewSeason(AndroidDriver driver)
    {
        var nextTapUtc = DateTime.MinValue;
        PhaseAUiWorkflow.Wait(driver).Until(current =>
        {
            if (current.FindElements(By.XPath("//*[@text='Enter Season name']"))
                .Any(item => item.Displayed))
                return true;
            if (DateTime.UtcNow < nextTapUtc)
                return false;

            var label = current.FindElements(By.XPath("//*[@text='Create New Season']"))
                .FirstOrDefault(item => item.Displayed);
            var toggle = current.FindElements(By.XPath("//android.widget.Switch"))
                .Where(item => item.Displayed)
                .MinBy(item => label is null ? int.MaxValue : Math.Abs(
                    item.Location.Y + (item.Size.Height / 2)
                    - label.Location.Y - (label.Size.Height / 2)));
            if (toggle is not null)
                PhaseAUiWorkflow.TapCenter(driver, toggle);
            nextTapUtc = DateTime.UtcNow.AddSeconds(1);
            return false;
        });
    }

    private static void AssertTestModeRoot(AndroidDriver driver, string absentTeam)
    {
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertTestModeIndicator(driver);
        AssertTextAbsent(driver, absentTeam);
    }

    private static void AssertTestModeIndicator(AndroidDriver driver)
    {
        var indicator = AndroidElementWait.UntilAutomationIdText(
            driver,
            "TestModeIndicator",
            "TEST MODE"
        );
        Assert.True(indicator.Displayed);
        Assert.True(indicator.Location.Y >= 0);
        Assert.True(indicator.Location.Y < driver.Manage().Window.Size.Height / 3);
    }

    private static void AssertIndicatorAbsent(AndroidDriver driver) =>
        Assert.DoesNotContain(
            driver.FindElements(By.Id(ResourceId("TestModeIndicator"))),
            element => element.Displayed
        );

    private static void AssertTextAbsent(AndroidDriver driver, string text) =>
        Assert.DoesNotContain(
            driver.FindElements(By.XPath($"//*[@text={XPathLiteral(text)}]")),
            element => element.Displayed
        );

    private static void OpenShellFlyout(AndroidDriver driver)
    {
        var wait = Wait(driver);
        wait.Until(current =>
        {
            if (
                current
                    .FindElements(By.XPath("//*[@text='Manage Inactive Teams']"))
                    .Any(element => element.Displayed)
            )
                return true;

            var drawer = current
                .FindElements(MobileBy.AccessibilityId("Open navigation drawer"))
                .FirstOrDefault(element => element.Displayed);
            if (drawer is not null)
            {
                drawer.Click();
                return false;
            }

            current
                .FindElements(MobileBy.AccessibilityId("Navigate up"))
                .FirstOrDefault(element => element.Displayed)
                ?.Click();
            return false;
        });
    }

    private static WebDriverWait Wait(AndroidDriver driver) =>
        new(driver, TimeSpan.FromSeconds(20)) { PollingInterval = TimeSpan.FromMilliseconds(250) };

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private static string XPathLiteral(string value) =>
        !value.Contains('\'') ? $"'{value}'" : $"concat('{value.Replace("'", "',\"'\",'")}')";
}
