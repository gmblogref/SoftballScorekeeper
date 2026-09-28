using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class SeasonBackupReminderTests(AndroidAppiumFixture fixture)
{
    private const string ReminderMessage =
        "One or more League Seasons have finished. Back up your Softball Scorekeeper data?";

    [AndroidUiFact]
    public void Season_completion_reminder_launches_backup_and_clears_only_after_success()
    {
        var remoteDirectoryName = $"SeasonBackupReminder-{Guid.NewGuid():N}";
        var remoteDirectory = $"/sdcard/Download/{remoteDirectoryName}";
        fixture.ExecuteAdb("shell", "mkdir", "-p", remoteDirectory);

        try
        {
            fixture.ResetApplication(
                AndroidAutomationConstants.SeasonBackupReminderTestSeedRequestFileName);
            var driver = fixture.Driver;

            AssertSingleReminder(driver);
            AndroidElementWait.UntilText(driver, "OK").Click();
            UntilPackage(driver, "com.google.android.documentsui");
            Assert.DoesNotContain("Data Management", driver.PageSource, StringComparison.Ordinal);

            CancelPickerAndReturnToApplication(driver);
            AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AssertSingleReminder(driver);
            AndroidElementWait.UntilText(driver, "OK").Click();
            UntilPackage(driver, "com.google.android.documentsui");
            SaveInDownloadsDirectory(driver, remoteDirectoryName);

            UntilPackage(driver, AndroidAutomationConstants.AppPackage);
            AndroidElementWait.UntilText(
                driver,
                "Backup saved successfully.",
                TimeSpan.FromSeconds(30));
            AndroidElementWait.UntilText(driver, "OK").Click();

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");
            AssertReminderRemainsAbsent(driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Season_completion_reminder_launches_backup_and_clears_only_after_success),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android season backup-reminder workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
        finally
        {
            fixture.ExecuteAdb("shell", "rm", "-rf", remoteDirectory);
        }
    }

    [AndroidUiFact]
    public void Remind_later_returns_next_startup_while_dismiss_and_test_mode_suppress_prompt()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.SeasonBackupReminderTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AssertSingleReminder(driver);
            AndroidElementWait.UntilText(driver, "Remind Later").Click();
            AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");

            EnterTestMode(driver);
            AndroidElementWait.UntilAutomationIdText(driver, "TestModeIndicator", "TEST MODE");
            AssertReminderRemainsAbsent(driver);
            ExitTestMode(driver);

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AssertSingleReminder(driver);
            AndroidElementWait.UntilText(driver, "Dismiss").Click();
            AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");
            AssertReminderRemainsAbsent(driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Remind_later_returns_next_startup_while_dismiss_and_test_mode_suppress_prompt),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android season backup-reminder branches failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    private static void AssertSingleReminder(AndroidDriver driver)
    {
        AndroidElementWait.UntilText(driver, ReminderMessage, TimeSpan.FromSeconds(30));
        Assert.Single(
            driver.FindElements(By.XPath($"//*[@text={XPathLiteral(ReminderMessage)}]"))
                .Where(element => element.Displayed));
    }

    private static void SaveInDownloadsDirectory(
        AndroidDriver driver,
        string remoteDirectoryName)
    {
        UntilAny(
            driver,
            MobileBy.AccessibilityId("Show roots"),
            By.Id("com.google.android.documentsui:id/roots_toolbar")).Click();
        UntilAny(
            driver,
            By.XPath(
                "//*[@resource-id='com.google.android.documentsui:id/roots_list']//*[@text='Downloads']"))
            .Click();
        AndroidElementWait.UntilText(driver, remoteDirectoryName).Click();
        UntilAny(
            driver,
            By.XPath(
                $"//*[@resource-id='com.google.android.documentsui:id/breadcrumb_text' and @text={XPathLiteral(remoteDirectoryName)}]"));
        UntilAny(
            driver,
            By.Id("com.google.android.documentsui:id/action_menu_save"),
            By.Id("android:id/button1"),
            By.XPath("//*[@text='Save' or @text='SAVE']"))
            .Click();
    }

    private static void CancelPickerAndReturnToApplication(AndroidDriver driver)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (
            string.Equals(
                driver.CurrentPackage,
                "com.google.android.documentsui",
                StringComparison.Ordinal)
            && DateTime.UtcNow < deadline)
        {
            driver.Navigate().Back();
            Thread.Sleep(250);
        }

        driver.ExecuteScript(
            "mobile: activateApp",
            new Dictionary<string, object>
            {
                ["appId"] = AndroidAutomationConstants.AppPackage,
            });
        UntilPackage(driver, AndroidAutomationConstants.AppPackage);
    }

    private static void AssertReminderRemainsAbsent(AndroidDriver driver)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            Assert.DoesNotContain(
                driver.FindElements(By.XPath($"//*[@text={XPathLiteral(ReminderMessage)}]")),
                element => element.Displayed);
            Thread.Sleep(250);
        }
    }

    private static void EnterTestMode(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Enter Test Mode?");
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Create Team");
    }

    private static void ExitTestMode(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Exit Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Keep Test Data").Click();
        AndroidElementWait.UntilText(driver, "BACKUP REMINDER TEAM");
    }

    private static void OpenShellFlyout(AndroidDriver driver)
    {
        var wait = CreateWait(driver);
        wait.Until(current =>
        {
            if (current.FindElements(By.XPath("//*[@text='Manage Inactive Teams']"))
                .Any(element => element.Displayed))
                return true;

            var drawer = current.FindElements(
                    MobileBy.AccessibilityId("Open navigation drawer"))
                .FirstOrDefault(element => element.Displayed);
            if (drawer is null)
                return false;
            drawer.Click();
            return false;
        });
    }

    private static IWebElement UntilAny(AndroidDriver driver, params By[] selectors)
    {
        var wait = CreateWait(driver);
        return wait.Until(currentDriver =>
            selectors.SelectMany(currentDriver.FindElements)
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException());
    }

    private static void UntilPackage(AndroidDriver driver, string packageName)
    {
        var wait = CreateWait(driver);
        wait.Until(_ => string.Equals(driver.CurrentPackage, packageName, StringComparison.Ordinal));
    }

    private static WebDriverWait CreateWait(AndroidDriver driver)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250),
        };
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait;
    }

    private static string XPathLiteral(string value) =>
        !value.Contains('\'') ? $"'{value}'" : $"concat('{value.Replace("'", "',\"'\",'")}')";
}
