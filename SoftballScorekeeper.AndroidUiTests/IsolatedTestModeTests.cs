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
