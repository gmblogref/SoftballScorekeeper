using System.IO.Compression;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ManualBackupTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public async Task Backup_data_saves_valid_portable_package_through_document_provider()
    {
        var remoteDirectoryName = $"SoftballScorekeeperUiTest-{Guid.NewGuid():N}";
        var remoteDirectory = $"/sdcard/Download/{remoteDirectoryName}";
        var hostDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SoftballScorekeeper.AndroidUiTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(hostDirectory);
        fixture.ExecuteAdb("shell", "mkdir", "-p", remoteDirectory);
        fixture.ResetApplication();
        var driver = fixture.Driver;

        try
        {
            ReturnToApplication(driver);
            OpenShellFlyout(driver);
            AndroidElementWait.UntilText(driver, "Data Management").Click();
            AndroidElementWait.UntilAutomationId(driver, "BackupDataButton").Click();

            UntilPackage(driver, "com.google.android.documentsui");
            var fileNameEntry = UntilAny(
                driver,
                By.XPath("//android.widget.EditText[@resource-id='android:id/title']"),
                By.Id("com.google.android.documentsui:id/file_name"),
                By.ClassName("android.widget.EditText")
            );
            var suggestedFileName = fileNameEntry.Text;
            Assert.Matches(
                new Regex(
                    "^SoftballScorekeeper-Backup-[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9]{4}\\.zip$",
                    RegexOptions.CultureInvariant
                ),
                suggestedFileName
            );

            UntilAny(
                driver,
                MobileBy.AccessibilityId("Show roots"),
                By.Id("com.google.android.documentsui:id/roots_toolbar")
            ).Click();
            UntilAny(
                driver,
                By.XPath(
                    "//*[@resource-id='com.google.android.documentsui:id/roots_list']//*[@text='Downloads']"
                )
            ).Click();
            AndroidElementWait.UntilText(driver, remoteDirectoryName).Click();
            UntilAny(
                driver,
                By.XPath(
                    $"//*[@resource-id='com.google.android.documentsui:id/breadcrumb_text' and @text='{remoteDirectoryName}']"
                )
            );
            UntilAny(
                driver,
                By.Id("com.google.android.documentsui:id/action_menu_save"),
                By.Id("android:id/button1"),
                By.XPath("//*[@text='Save' or @text='SAVE']")
            ).Click();

            AndroidElementWait.UntilAutomationIdText(
                driver,
                "BackupDataStatus",
                "Backup saved successfully.",
                TimeSpan.FromSeconds(30)
            );

            var remoteFilePath = $"{remoteDirectory}/{suggestedFileName}";
            var remoteListing = fixture.ExecuteAdb("shell", "ls", "-l", remoteFilePath);
            Assert.Contains(suggestedFileName, remoteListing, StringComparison.Ordinal);

            var localPackagePath = Path.Combine(hostDirectory, suggestedFileName);
            fixture.ExecuteAdb("pull", remoteFilePath, localPackagePath);
            Assert.True(File.Exists(localPackagePath));

            using (var archive = ZipFile.OpenRead(localPackagePath))
            {
                Assert.Equal(
                    [BackupPackageService.DatabaseEntryName, BackupPackageService.MetadataEntryName],
                    archive.Entries.Select(entry => entry.FullName).Order().ToArray()
                );
            }

            var inspectionService = new BackupPackageService(
                new AppDatabase(Path.Combine(hostDirectory, "unused.db3")),
                new HostBackupPlatform(hostDirectory),
                TimeProvider.System
            );
            var inspection = await inspectionService.InspectAsync(localPackagePath);
            Assert.True(
                inspection.IsValid,
                $"Saved package inspection failed: {inspection.Failure}"
            );

            UntilAccessibilityId(driver, "Navigate up").Click();
            AndroidElementWait.UntilText(driver, "Automation Team");
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Backup_data_saves_valid_portable_package_through_document_provider),
                exception
            );
            throw new Xunit.Sdk.XunitException(
                $"Android manual backup workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}"
            );
        }
        finally
        {
            fixture.ExecuteAdb("shell", "rm", "-rf", remoteDirectory);
            if (Directory.Exists(hostDirectory))
                Directory.Delete(hostDirectory, recursive: true);
        }
    }

    private static IWebElement UntilAccessibilityId(AndroidDriver driver, string accessibilityId)
    {
        var wait = CreateWait(driver);
        return wait.Until(currentDriver =>
            currentDriver.FindElements(MobileBy.AccessibilityId(accessibilityId))
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException()
        );
    }

    private static void OpenShellFlyout(AndroidDriver driver)
        => PhaseAUiWorkflow.OpenShellFlyout(driver);

    private static void ReturnToApplication(AndroidDriver driver)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (
            string.Equals(
                driver.CurrentPackage,
                "com.google.android.documentsui",
                StringComparison.Ordinal
            )
            && DateTime.UtcNow < deadline
        )
        {
            driver.Navigate().Back();
            Thread.Sleep(250);
        }

        driver.ExecuteScript(
            "mobile: activateApp",
            new Dictionary<string, object>
            {
                ["appId"] = AndroidAutomationConstants.AppPackage,
            }
        );
        UntilPackage(driver, AndroidAutomationConstants.AppPackage);
    }

    private static IWebElement UntilAny(AndroidDriver driver, params By[] selectors)
    {
        var wait = CreateWait(driver);
        return wait.Until(currentDriver =>
            selectors.SelectMany(currentDriver.FindElements)
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException()
        );
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
            typeof(StaleElementReferenceException)
        );
        return wait;
    }

    private sealed record HostBackupPlatform(string CacheDirectory) : IBackupPlatform
    {
        public string AppVersion => "android-ui-test";
    }
}
