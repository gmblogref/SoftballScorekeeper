using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ManualRestoreTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public async Task Restore_data_replaces_local_state_from_valid_backup()
    {
        var remoteDirectoryName = $"SoftballScorekeeperRestore-{Guid.NewGuid():N}";
        var remoteDirectory = $"/sdcard/Download/{remoteDirectoryName}";
        var remotePackagePath = $"{remoteDirectory}/Story-8.2-restore.zip";
        var hostDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SoftballScorekeeper.AndroidUiTests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(hostDirectory);

        try
        {
            var localPackagePath = await CreateRestorePackageAsync(hostDirectory);
            fixture.ExecuteAdb("shell", "mkdir", "-p", remoteDirectory);
            fixture.ExecuteAdb("push", localPackagePath, remotePackagePath);
            fixture.ExecuteAdb(
                "shell",
                "am",
                "broadcast",
                "-a",
                "android.intent.action.MEDIA_SCANNER_SCAN_FILE",
                "-d",
                $"file://{remotePackagePath}"
            );
            fixture.ResetApplication(
                AndroidAutomationConstants.ManualRestoreTestSeedRequestFileName
            );
            var driver = fixture.Driver;

            OpenTeamManagerRoot(driver);
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamName-1",
                "RESTORE DESTINATION ONLY"
            );
            OpenShellFlyout(driver);
            AndroidElementWait.UntilText(driver, "Data Management").Click();
            AndroidElementWait.UntilAutomationId(driver, "RestoreDataButton").Click();

            UntilPackage(driver, "com.google.android.documentsui");
            OpenDownloadsRoot(driver);
            AndroidElementWait.UntilText(driver, remoteDirectoryName).Click();
            AndroidElementWait.UntilText(driver, "Story-8.2-restore.zip").Click();

            UntilPackage(driver, AndroidAutomationConstants.AppPackage);
            AndroidElementWait.UntilText(driver, "Restore This Backup?");
            UntilAny(
                driver,
                By.XPath(
                    "//*[@resource-id='android:id/button1' and @text='Restore Data']"
                )
            ).Click();

            AndroidElementWait.UntilText(
                driver,
                "Restore Complete",
                TimeSpan.FromSeconds(30)
            );
            AndroidElementWait.UntilText(driver, "OK").Click();

            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamName-1",
                "RESTORED TEAM",
                TimeSpan.FromSeconds(30)
            );
            Assert.Empty(
                driver
                    .FindElements(By.XPath("//*[@text='RESTORE DESTINATION ONLY']"))
                    .Where(element => element.Displayed)
            );

            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            AndroidElementWait.UntilText(driver, "RESTORED TEAM");
            Assert.True(
                AndroidElementWait
                    .UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton")
                    .Displayed
            );
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Restore_data_replaces_local_state_from_valid_backup),
                exception
            );
            throw new Xunit.Sdk.XunitException(
                $"Android manual restore workflow failed. Diagnostics: {artifacts}"
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

    private static void OpenTeamManagerRoot(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Teams").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamYearGroup-2026");
    }

    private static async Task<string> CreateRestorePackageAsync(string hostDirectory)
    {
        var sourceDatabasePath = Path.Combine(hostDirectory, "restore-source.db3");
        var sourceDatabase = new AppDatabase(sourceDatabasePath);
        try
        {
            var team = new Team
            {
                Name = "RESTORED TEAM",
                Year = 2026,
                IsActive = true,
            };
            Assert.Equal(1, await new TeamRepository(sourceDatabase).CreateAsync(team));

            var packageService = new BackupPackageService(
                sourceDatabase,
                new HostBackupPlatform(hostDirectory),
                TimeProvider.System
            );
            await using var artifact = await packageService.CreateAsync();
            var packagePath = Path.Combine(hostDirectory, "Story-8.2-restore.zip");
            File.Copy(artifact.FilePath, packagePath);
            return packagePath;
        }
        finally
        {
            await sourceDatabase.CloseAsync();
        }
    }

    private static void OpenShellFlyout(AndroidDriver driver)
        => PhaseAUiWorkflow.OpenShellFlyout(driver);

    private static void OpenDownloadsRoot(AndroidDriver driver)
    {
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
    }

    private static IWebElement UntilAny(AndroidDriver driver, params By[] selectors)
    {
        var wait = CreateWait(driver);
        return wait.Until(currentDriver =>
            selectors
                .SelectMany(currentDriver.FindElements)
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
