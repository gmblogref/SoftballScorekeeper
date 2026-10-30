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
public sealed class ReleaseBackupRestoreAcceptanceTests(AndroidAppiumFixture fixture)
{
    private const string ReleaseCertificateSha256 =
        "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05";
    private const string DocumentsPackage = "com.google.android.documentsui";
    private static readonly string RunId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static readonly RecordSet TestSentinel = new(
        $"REL TEST {RunId}",
        $"Test{RunId}",
        "Sentinel"
    );
    private static readonly RecordSet PreBackupProduction = new(
        $"REL PRE {RunId}",
        $"Pre{RunId}",
        "Release"
    );
    private static readonly RecordSet PostBackupProduction = new(
        $"REL POST {RunId}",
        $"Post{RunId}",
        "Release"
    );

    [AndroidUiFact]
    public async Task Signed_release_backup_restore_round_trip_preserves_test_mode_isolation()
    {
        Assert.Equal(
            "1",
            Environment.GetEnvironmentVariable("SOFTBALL_ANDROID_RELEASE_ACCEPTANCE")
        );
        Assert.Equal(
            "1",
            Environment.GetEnvironmentVariable("SOFTBALL_ANDROID_RELEASE_BACKUP_RESTORE_ACCEPTANCE")
        );
        var destinationApk = RequiredApk("SOFTBALL_RELEASE_DESTINATION_APK");
        var destinationSha256 = RequiredSha256("SOFTBALL_RELEASE_DESTINATION_SHA256");
        var certificateSha256 = RequiredSha256("SOFTBALL_RELEASE_CERTIFICATE_SHA256");
        Assert.Equal(ReleaseCertificateSha256, certificateSha256);
        AssertApkHash(destinationApk, destinationSha256);

        var remoteDirectoryName = $"ReleaseBackupRestore-{RunId}";
        var remoteDirectory = $"/sdcard/Download/{remoteDirectoryName}";
        var hostDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SoftballScorekeeper.ReleaseBackupRestore-{RunId}"
        );
        Directory.CreateDirectory(hostDirectory);
        fixture.ExecuteAdb("shell", "mkdir", "-p", remoteDirectory);

        Install(destinationApk);
        AssertVersion("6", "2.0.0");
        fixture.SetOrientation(ScreenOrientation.Landscape);
        fixture.RelaunchApplicationWithoutReset();
        var driver = fixture.Driver;
        fixture.ExecuteAdb("logcat", "-c");

        try
        {
            EnterProductTestMode(driver);
            CreateRecords(driver, TestSentinel, expectTestMode: true);
            ExitProductTestModeKeepingData(driver);

            CreateRecords(driver, PreBackupProduction, expectTestMode: false);
            var backupFileName = CreateBackupThroughUi(driver, remoteDirectoryName);
            var localBackupPath = await PullAndInspectBackupAsync(
                remoteDirectory,
                backupFileName,
                hostDirectory
            );

            CreateRecords(driver, PostBackupProduction, expectTestMode: false);
            AssertRecordsPresent(driver, PreBackupProduction, expectTestMode: false);
            AssertRecordsPresent(driver, PostBackupProduction, expectTestMode: false);

            RestoreBackupThroughUi(driver, remoteDirectoryName, backupFileName);
            AssertRecordsPresent(driver, PreBackupProduction, expectTestMode: false);
            AssertRecordsAbsent(driver, PostBackupProduction);

            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AssertRecordsPresent(driver, PreBackupProduction, expectTestMode: false);
            AssertRecordsAbsent(driver, PostBackupProduction);

            EnterProductTestMode(driver);
            AssertRecordsPresent(driver, TestSentinel, expectTestMode: true);
            ExitProductTestModeKeepingData(driver);
            AssertRecordsPresent(driver, PreBackupProduction, expectTestMode: false);
            AssertRecordsAbsent(driver, PostBackupProduction);

            Assert.True(File.Exists(localBackupPath));
            var logcat = fixture.ExecuteAdb("logcat", "-d", "-t", "1000");
            Assert.DoesNotContain(
                "FATAL EXCEPTION: main",
                logcat,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.DoesNotContain("SQLiteException", logcat, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Signed_release_backup_restore_round_trip_preserves_test_mode_isolation),
                exception
            );
            throw new Xunit.Sdk.XunitException(
                $"Signed release Backup/Restore acceptance failed. Diagnostics: {artifacts}"
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

    private void Install(string apkPath)
    {
        var output = fixture.ExecuteAdb("install", "-r", apkPath);
        Assert.Contains("Success", output, StringComparison.Ordinal);
    }

    private void AssertVersion(string code, string name)
    {
        var package = fixture.ExecuteAdb(
            "shell",
            "dumpsys",
            "package",
            AndroidAutomationConstants.AppPackage
        );
        Assert.Contains($"versionCode={code}", package, StringComparison.Ordinal);
        Assert.Contains($"versionName={name}", package, StringComparison.Ordinal);
    }

    private static void EnterProductTestMode(AndroidDriver driver)
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
        PhaseAUiWorkflow.OpenShellFlyout(driver);
        var exitTestMode = driver
            .FindElements(By.XPath("//*[@text='Exit Test Mode']"))
            .FirstOrDefault(element => element.Displayed);
        if (exitTestMode is not null)
        {
            AndroidElementWait.UntilAutomationId(driver, "TeamsFlyoutMenuItem").Click();
            AndroidElementWait.UntilText(driver, "Create Team");
            AssertTestModeIndicator(driver);
            return;
        }

        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Enter Test Mode?");
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertTestModeIndicator(driver);
    }

    private static void ExitProductTestModeKeepingData(AndroidDriver driver)
    {
        PhaseAUiWorkflow.OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Exit Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Keep Test Data").Click();
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertTestModeIndicatorAbsent(driver);
    }

    private static void CreateRecords(AndroidDriver driver, RecordSet records, bool expectTestMode)
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
        AndroidElementWait.UntilText(driver, "Create Team").Click();
        var entries = VisibleEntries(driver, 2);
        entries[0].SendKeys(records.TeamName);
        entries[1].Clear();
        entries[1].SendKeys("2026");
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilTextAbsent(driver, "Save");
        PhaseAUiWorkflow.ScrollToText(driver, records.TeamName);

        PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
        AndroidElementWait.UntilText(driver, "Add Player").Click();
        entries = VisibleEntries(driver, 2);
        entries[0].SendKeys(records.PlayerFirstName);
        entries[1].SendKeys(records.PlayerLastName);
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilTextAbsent(driver, "Save");
        PhaseAUiWorkflow.ScrollToText(driver, records.PlayerName);

        if (expectTestMode)
            AssertTestModeIndicator(driver);
        else
            AssertTestModeIndicatorAbsent(driver);
    }

    private static void AssertRecordsPresent(
        AndroidDriver driver,
        RecordSet records,
        bool expectTestMode
    )
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
        Assert.True(PhaseAUiWorkflow.ScrollToText(driver, records.TeamName).Displayed);
        PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
        Assert.True(PhaseAUiWorkflow.ScrollToText(driver, records.PlayerName).Displayed);

        if (expectTestMode)
            AssertTestModeIndicator(driver);
        else
            AssertTestModeIndicatorAbsent(driver);
    }

    private static void AssertRecordsAbsent(AndroidDriver driver, RecordSet records)
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
        Assert.Throws<WebDriverTimeoutException>(() =>
            PhaseAUiWorkflow.ScrollToText(driver, records.TeamName)
        );
        PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
        Assert.Throws<WebDriverTimeoutException>(() =>
            PhaseAUiWorkflow.ScrollToText(driver, records.PlayerName)
        );
        AssertTestModeIndicatorAbsent(driver);
    }

    private static string CreateBackupThroughUi(AndroidDriver driver, string directoryName)
    {
        OpenDataManagement(driver);
        AndroidElementWait.UntilAutomationId(driver, "BackupDataButton").Click();
        UntilPackage(driver, DocumentsPackage);
        var fileNameEntry = UntilAny(
            driver,
            By.XPath("//android.widget.EditText[@resource-id='android:id/title']"),
            By.Id("com.google.android.documentsui:id/file_name"),
            By.ClassName("android.widget.EditText")
        );
        var fileName = fileNameEntry.Text;
        Assert.Matches(
            new Regex(
                "^SoftballScorekeeper-Backup-[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9]{4}\\.zip$",
                RegexOptions.CultureInvariant
            ),
            fileName
        );
        OpenDownloadsRoot(driver);
        AndroidElementWait.UntilText(driver, directoryName).Click();
        UntilAny(
                driver,
                By.Id("com.google.android.documentsui:id/action_menu_save"),
                By.Id("android:id/button1"),
                By.XPath("//*[@text='Save' or @text='SAVE']")
            )
            .Click();
        AndroidElementWait.UntilAutomationIdText(
            driver,
            "BackupDataStatus",
            "Backup saved successfully.",
            TimeSpan.FromSeconds(30)
        );
        return fileName;
    }

    private async Task<string> PullAndInspectBackupAsync(
        string remoteDirectory,
        string fileName,
        string hostDirectory
    )
    {
        var remotePath = $"{remoteDirectory}/{fileName}";
        Assert.Contains(fileName, fixture.ExecuteAdb("shell", "ls", "-l", remotePath));
        var localPath = Path.Combine(hostDirectory, fileName);
        fixture.ExecuteAdb("pull", remotePath, localPath);

        using (var archive = ZipFile.OpenRead(localPath))
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
        var inspection = await inspectionService.InspectAsync(localPath);
        Assert.True(inspection.IsValid, $"Saved package inspection failed: {inspection.Failure}");
        Assert.Equal(
            BackupPackageService.CurrentBackupFormatVersion,
            inspection.Metadata!.BackupFormatVersion
        );
        Assert.Equal("2.0.0", inspection.Metadata.AppVersion);
        Assert.Equal(AppDatabase.CurrentSchemaVersion, inspection.Metadata.DatabaseVersion);
        Assert.Equal(BackupDatabaseCompatibility.Current, inspection.DatabaseCompatibility);
        return localPath;
    }

    private static void RestoreBackupThroughUi(
        AndroidDriver driver,
        string directoryName,
        string fileName
    )
    {
        OpenDataManagement(driver);
        AndroidElementWait.UntilAutomationId(driver, "RestoreDataButton").Click();
        UntilPackage(driver, DocumentsPackage);
        OpenDownloadsRoot(driver);
        AndroidElementWait.UntilText(driver, directoryName).Click();
        AndroidElementWait.UntilText(driver, fileName).Click();

        UntilPackage(driver, AndroidAutomationConstants.AppPackage);
        AndroidElementWait.UntilText(driver, "Restore This Backup?");
        UntilAny(
                driver,
                By.XPath("//*[@resource-id='android:id/button1' and @text='Restore Data']")
            )
            .Click();
        AndroidElementWait.UntilText(driver, "Restore Complete", TimeSpan.FromSeconds(30));
        AndroidElementWait.UntilText(driver, "OK").Click();
        AndroidElementWait.UntilText(driver, "Create Team", TimeSpan.FromSeconds(30));
    }

    private static void OpenDataManagement(AndroidDriver driver)
    {
        PhaseAUiWorkflow.OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Data Management").Click();
    }

    private static void OpenDownloadsRoot(AndroidDriver driver)
    {
        UntilAny(
                driver,
                MobileBy.AccessibilityId("Show roots"),
                By.Id("com.google.android.documentsui:id/roots_toolbar")
            )
            .Click();
        UntilAny(
                driver,
                By.XPath(
                    "//*[@resource-id='com.google.android.documentsui:id/roots_list']//*[@text='Downloads']"
                )
            )
            .Click();
    }

    private static string RequiredApk(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(File.Exists(path), $"Release APK not found: {path}");
        return path!;
    }

    private static string RequiredSha256(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Matches("^[0-9A-Fa-f]{64}$", value!);
        return value!.ToUpperInvariant();
    }

    private static void AssertApkHash(string path, string expectedSha256) =>
        Assert.Equal(
            expectedSha256,
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))
            )
        );

    private static IWebElement[] VisibleEntries(AndroidDriver driver, int count) =>
        Wait(driver)
            .Until(current =>
            {
                var entries = current
                    .FindElements(By.XPath("//android.widget.EditText"))
                    .Where(element => element.Displayed)
                    .ToArray();
                return entries.Length >= count ? entries : null;
            })!;

    private static void AssertTestModeIndicator(AndroidDriver driver) =>
        AndroidElementWait.UntilAutomationIdText(driver, "TestModeIndicator", "TEST MODE");

    private static void AssertTestModeIndicatorAbsent(AndroidDriver driver) =>
        Assert.Empty(
            driver
                .FindElements(By.Id(ResourceId("TestModeIndicator")))
                .Where(element => element.Displayed)
        );

    private static IWebElement UntilAny(AndroidDriver driver, params By[] selectors) =>
        Wait(driver)
            .Until(currentDriver =>
                selectors
                    .SelectMany(currentDriver.FindElements)
                    .FirstOrDefault(element => element.Displayed)
                ?? throw new NoSuchElementException()
            );

    private static void UntilPackage(AndroidDriver driver, string packageName) =>
        Wait(driver)
            .Until(_ =>
                string.Equals(driver.CurrentPackage, packageName, StringComparison.Ordinal)
            );

    private static WebDriverWait Wait(AndroidDriver driver)
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

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private sealed record RecordSet(string TeamName, string PlayerFirstName, string PlayerLastName)
    {
        public string PlayerName => $"{PlayerFirstName} {PlayerLastName}";
    }

    private sealed record HostBackupPlatform(string CacheDirectory) : IBackupPlatform
    {
        public string AppVersion => "release-acceptance-host";
    }
}
