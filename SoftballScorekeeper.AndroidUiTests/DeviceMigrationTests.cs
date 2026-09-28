using System.IO.Compression;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SQLite;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class DeviceMigrationTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public async Task Backup_restore_migrates_complete_state_resumes_game_and_remains_backup_capable()
    {
        var sourceDirectoryName = $"Story-8.3-Source-{Guid.NewGuid():N}";
        var postRestoreDirectoryName = $"Story-8.3-Restored-{Guid.NewGuid():N}";
        var sourceDirectory = $"/sdcard/Download/{sourceDirectoryName}";
        var postRestoreDirectory = $"/sdcard/Download/{postRestoreDirectoryName}";
        var hostDirectory = Path.Combine(
            Path.GetTempPath(), $"SoftballScorekeeper.DeviceMigration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(hostDirectory);
        fixture.ExecuteAdb("shell", "mkdir", "-p", sourceDirectory);
        fixture.ExecuteAdb("shell", "mkdir", "-p", postRestoreDirectory);

        try
        {
            fixture.ResetApplication(
                AndroidAutomationConstants.DeviceMigrationTestSeedRequestFileName);
            var driver = fixture.Driver;
            AndroidElementWait.UntilAutomationIdText(driver, "TeamName-2", "MIGRATION CURRENT");
            AssertSourceStateBeforeBackup(driver);

            var sourcePackageName = CreateBackupThroughUi(
                driver, sourceDirectoryName, "BackupDataStatus");
            var sourcePackage = PullAndInspectPackage(
                sourceDirectory, sourcePackageName, hostDirectory, "source");

            fixture.ResetApplication(
                AndroidAutomationConstants.ManualRestoreTestSeedRequestFileName);
            driver = fixture.Driver;
            OpenTeamManagerRoot(driver, 2026);
            AndroidElementWait.UntilAutomationIdText(
                driver, "TeamName-1", "RESTORE DESTINATION ONLY");

            OpenDataManagement(driver);
            AndroidElementWait.UntilAutomationId(driver, "RestoreDataButton").Click();
            UntilPackage(driver, DocumentsPackage);
            OpenDownloadsRoot(driver);
            AndroidElementWait.UntilText(driver, sourceDirectoryName).Click();
            AndroidElementWait.UntilText(driver, sourcePackageName).Click();

            UntilPackage(driver, AndroidAutomationConstants.AppPackage);
            AndroidElementWait.UntilText(driver, "Restore This Backup?");
            UntilAny(driver, By.XPath(
                "//*[@resource-id='android:id/button1' and @text='Restore Data']")).Click();
            AndroidElementWait.UntilText(driver, "Restore Complete", TimeSpan.FromSeconds(30));
            AndroidElementWait.UntilText(driver, "OK").Click();

            AndroidElementWait.UntilAutomationIdText(
                driver, "TeamName-2", "MIGRATION CURRENT", TimeSpan.FromSeconds(30));
            Assert.Empty(driver.FindElements(By.XPath("//*[@text='RESTORE DESTINATION ONLY']"))
                .Where(element => element.Displayed));
            AssertRestoredTeamData(driver);
            AssertRestoredHistoryAndFinalization(driver);
            ResumeScoreAndAssertPersistence(driver);

            var postRestorePackageName = CreateBackupThroughUi(
                driver, postRestoreDirectoryName, "BackupDataStatus");
            var postRestorePackage = PullAndInspectPackage(
                postRestoreDirectory, postRestorePackageName, hostDirectory, "restored");
            await AssertPostRestoreDatabaseAsync(postRestorePackage);

            Assert.True(File.Exists(sourcePackage));
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Backup_restore_migrates_complete_state_resumes_game_and_remains_backup_capable),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android device-migration workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
        finally
        {
            fixture.ExecuteAdb("shell", "rm", "-rf", sourceDirectory);
            fixture.ExecuteAdb("shell", "rm", "-rf", postRestoreDirectory);
            if (Directory.Exists(hostDirectory))
                Directory.Delete(hostDirectory, recursive: true);
        }
    }

    private static void AssertSourceStateBeforeBackup(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-2", "2027");
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
        AndroidElementWait.UntilAutomationIdText(
            driver, "GameScheduleStatus2", "In Progress").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
        AssertScoringState(driver, "Drew Migration", "4",
            "Blake Migration", "Alex Migration", string.Empty);
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        NavigateUp(driver);
        NavigateUp(driver);
    }

    private static void AssertRestoredTeamData(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-2", "2027");
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
        AndroidElementWait.UntilText(driver, "MIGRATION CURRENT");
        AndroidElementWait.UntilText(driver, "Summer Migration");
        AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton").Click();
        foreach (var expected in new[]
        {
            "Alex Migration", "No. 11", "Blake Migration", "No. 22",
            "Casey Migration", "No. 33", "Drew Migration", "No. 44",
        })
            AndroidElementWait.UntilText(driver, expected);

        AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
        AndroidElementWait.UntilAutomationIdText(driver, "BeerTotalsDbo-1", "1");
        Assert.Equal("1", FindHorizontally(driver, "BeerTotalsLowMan-1").Text);
        Assert.Equal("2", FindHorizontally(driver, "BeerTotalsBrought-1").Text);
        Assert.Equal("4", FindHorizontally(driver, "BeerTotalsOwed-1").Text);
        Assert.Equal("-2", FindHorizontally(driver, "BeerTotalsOwed-2").Text);
        NavigateUp(driver);
        NavigateUp(driver);
    }

    private static void AssertRestoredHistoryAndFinalization(AndroidDriver driver)
    {
        ExpandTeamYear(driver, 2026, 1);
        AndroidElementWait.UntilAutomationIdText(driver, "TeamRecord-1", "Record: 1-0");
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidElementWait.UntilText(driver, "Spring Migration");
        AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
        AndroidElementWait.UntilAutomationIdText(
            driver, "TeamStatsActiveFilter", "All Games", TimeSpan.FromSeconds(30));
        Assert.Equal("6", UntilAny(driver, MobileBy.AccessibilityId(
            "CompletedBattingPlateAppearancesTeamTotal")).Text);
        Assert.Equal("2", UntilAny(driver, MobileBy.AccessibilityId(
            "CompletedBattingHitsTeamTotal")).Text);
        NavigateUp(driver);
        AndroidTeamWorkspaceMenu.Select(driver, "Team Year Summary");
        AndroidElementWait.UntilAutomationIdText(
            driver, "TeamYearGamesPlayed", "1", TimeSpan.FromSeconds(30));
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearWins", "1");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearLosses", "0");
        NavigateUp(driver);
        NavigateUp(driver);

        CollapseTeamYear(driver, 2026, 1);
        ExpandTeamYear(driver, 2025, 3);
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-3").Click();
        AndroidElementWait.UntilText(driver, "MIGRATION FINALIZED");
        AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
        AndroidElementWait.UntilText(driver, "Team Year Summary");
        Assert.Empty(driver.FindElements(By.Id(ResourceId("MarkTeamYearOverButton")))
            .Where(element => element.Displayed));
        Assert.Empty(driver.FindElements(By.Id(ResourceId("CopyTeamToNewYearButton")))
            .Where(element => element.Displayed));
        AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
        NavigateUp(driver);
    }

    private static void ResumeScoreAndAssertPersistence(AndroidDriver driver)
    {
        if (!driver.FindElements(By.Id(ResourceId("TeamCard-2")))
            .Any(element => element.Displayed))
            ExpandTeamYear(driver, 2027, 2);
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
        var gameStatus = AndroidElementWait.UntilAutomationIdText(
            driver, "GameScheduleStatus2", "In Progress");
        gameStatus.Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();

        AssertScoringState(driver, "Drew Migration", "4",
            "Blake Migration", "Alex Migration", string.Empty);
        AndroidScoringWorkflow.SelectResult(driver, "Single");
        AndroidScoringWorkflow.CompletePendingPlay(driver);
        AssertScoringState(driver, "Evan Migration", "4",
            "Drew Migration", "Blake Migration", "Alex Migration");

        AndroidScoringWorkflow.ExitAndResume(driver);
        AssertScoringState(driver, "Evan Migration", "4",
            "Drew Migration", "Blake Migration", "Alex Migration");
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
    }

    private static void AssertScoringState(
        AndroidDriver driver,
        string batter,
        string visitorScore,
        string first,
        string second,
        string third)
    {
        AndroidScoringWorkflow.AssertBatterInningAndOuts(driver, batter, "Top 3", "1 Out");
        AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", visitorScore);
        AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "2");
        AndroidElementWait.UntilAutomationIdText(driver, "FirstBaseRunnerButton", first);
        AndroidElementWait.UntilAutomationIdText(driver, "SecondBaseRunnerButton", second);
        AndroidElementWait.UntilAutomationIdText(driver, "ThirdBaseRunnerButton", third);
    }

    private static string CreateBackupThroughUi(
        AndroidDriver driver, string directoryName, string statusAutomationId)
    {
        OpenDataManagement(driver);
        AndroidElementWait.UntilAutomationId(driver, "BackupDataButton").Click();
        UntilPackage(driver, DocumentsPackage);
        var fileName = UntilAny(
            driver,
            By.XPath("//android.widget.EditText[@resource-id='android:id/title']"),
            By.Id("com.google.android.documentsui:id/file_name"),
            By.ClassName("android.widget.EditText")).Text;
        OpenDownloadsRoot(driver);
        AndroidElementWait.UntilText(driver, directoryName).Click();
        UntilAny(driver, By.Id("com.google.android.documentsui:id/action_menu_save"),
            By.Id("android:id/button1"), By.XPath("//*[@text='Save' or @text='SAVE']")).Click();
        AndroidElementWait.UntilAutomationIdText(
            driver, statusAutomationId, "Backup saved successfully.", TimeSpan.FromSeconds(30));
        return fileName;
    }

    private string PullAndInspectPackage(
        string remoteDirectory, string fileName, string hostDirectory, string prefix)
    {
        var remotePath = $"{remoteDirectory}/{fileName}";
        Assert.Contains(fileName, fixture.ExecuteAdb("shell", "ls", "-l", remotePath));
        var localPath = Path.Combine(hostDirectory, $"{prefix}-{fileName}");
        fixture.ExecuteAdb("pull", remotePath, localPath);
        using var archive = ZipFile.OpenRead(localPath);
        Assert.Equal(
            [BackupPackageService.DatabaseEntryName, BackupPackageService.MetadataEntryName],
            archive.Entries.Select(entry => entry.FullName).Order().ToArray());
        return localPath;
    }

    private static async Task AssertPostRestoreDatabaseAsync(string packagePath)
    {
        var cache = Path.GetDirectoryName(packagePath)!;
        var service = new BackupPackageService(
            new Infrastructure.Data.AppDatabase(Path.Combine(cache, "unused.db3")),
            new HostBackupPlatform(cache), TimeProvider.System);
        var prepared = await service.PrepareRestoreAsync(packagePath);
        Assert.True(prepared.Inspection.IsValid, prepared.Inspection.Failure.ToString());
        await using var artifact = prepared.Artifact!;
        var connection = new SQLiteAsyncConnection(
            artifact.DatabasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.SharedCache);
        try
        {
            var teams = await connection.Table<Team>().OrderBy(team => team.Id).ToListAsync();
            Assert.Equal(["MIGRATION HISTORY", "MIGRATION CURRENT", "MIGRATION FINALIZED"],
                teams.Select(team => team.Name).ToArray());
            Assert.Equal(teams[0].TeamLineageId, teams[1].TeamLineageId);
            Assert.Equal(teams[0].Id, teams[1].PreviousTeamId);
            Assert.True(teams[2].IsYearOver);
            Assert.DoesNotContain(teams, team => team.Name == "RESTORE DESTINATION ONLY");

            var seasons = await connection.Table<Season>().ToListAsync();
            Assert.Contains(seasons, season => season.Name == "Spring Migration");
            Assert.Contains(seasons, season => season.Name == "Summer Migration");
            var currentTeamId = teams[1].Id;
            var memberships = await connection.Table<TeamPlayer>()
                .Where(item => item.TeamId == currentTeamId).ToListAsync();
            Assert.Equal([11, 22, 33, 44, 55, 66],
                memberships.OrderBy(item => item.PlayerId)
                    .Select(item => item.JerseyNumber!.Value).ToArray());

            var sessions = await connection.Table<GameScoringSession>()
                .OrderBy(session => session.Id).ToListAsync();
            Assert.Equal(GameScoringStatus.Completed, sessions[0].Status);
            Assert.Equal(GameScoringStatus.InProgress, sessions[1].Status);
            Assert.Equal(4, sessions[1].VisitorScore);
            Assert.Equal(2, sessions[1].HomeScore);
            Assert.Equal(3, sessions[1].CurrentInning);
            Assert.Equal(1, sessions[1].CurrentOuts);
            Assert.Equal(5, sessions[1].CurrentBatterPosition);
            var currentSessionId = sessions[1].Id;
            Assert.Equal(4, await connection.Table<GamePlateAppearance>()
                .Where(item => item.GameScoringSessionId == currentSessionId).CountAsync());

            var runners = await connection.Table<GameScoringBaseRunner>()
                .Where(item => item.GameScoringSessionId == currentSessionId).ToListAsync();
            Assert.Equal(3, runners.Count);
            var beer = await connection.Table<BeerLedgerEntry>()
                .Where(item => item.TeamId == currentTeamId).ToListAsync();
            Assert.Contains(beer, item => item.Origin == BeerLedgerEntryOrigin.CarryForward
                && item.SourceTeamId == teams[0].Id && item.Amount == 3);
            Assert.Contains(beer, item => item.Origin == BeerLedgerEntryOrigin.CarryForward
                && item.SourceTeamId == teams[0].Id && item.Amount == -2);
            Assert.Contains(beer, item => item.RuleType == BeerRuleType.LowMan
                && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
                && item.Amount == 2);
            Assert.Contains(beer, item => item.Origin == BeerLedgerEntryOrigin.BroughtCredit
                && item.Amount == 2);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static void OpenTeamManagerRoot(AndroidDriver driver, int year)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Teams").Click();
        AndroidElementWait.UntilAutomationId(driver, $"TeamYearGroup-{year}");
    }

    private static void ExpandTeamYear(AndroidDriver driver, int year, int teamId)
    {
        var teamResourceId = ResourceId($"TeamCard-{teamId}");
        var yearResourceId = ResourceId($"TeamYearGroup-{year}");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (driver.FindElements(By.Id(teamResourceId)).Any(element => element.Displayed))
                return;

            var header = driver.FindElements(By.Id(yearResourceId))
                .FirstOrDefault(element => element.Displayed)
                ?? driver.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiScrollable(new UiSelector().scrollable(true))"
                        + $".scrollIntoView(new UiSelector().resourceId(\"{yearResourceId}\"))"));
            PhaseAUiWorkflow.TapCenter(driver, header);
            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(5)).Until(currentDriver =>
                    currentDriver.FindElements(By.Id(teamResourceId))
                        .Any(element => element.Displayed));
                return;
            }
            catch (WebDriverTimeoutException) when (attempt < 2)
            {
            }
        }

        throw new WebDriverTimeoutException($"Team year {year} did not expand.");
    }

    private static void CollapseTeamYear(AndroidDriver driver, int year, int teamId)
    {
        var teamResourceId = ResourceId($"TeamCard-{teamId}");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!driver.FindElements(By.Id(teamResourceId)).Any(element => element.Displayed))
                return;

            PhaseAUiWorkflow.TapCenter(
                driver,
                AndroidElementWait.UntilAutomationId(driver, $"TeamYearGroup-{year}"));
            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(5)).Until(currentDriver =>
                    !currentDriver.FindElements(By.Id(teamResourceId))
                        .Any(element => element.Displayed));
                return;
            }
            catch (WebDriverTimeoutException) when (attempt < 2)
            {
            }
        }

        throw new WebDriverTimeoutException($"Team year {year} did not collapse.");
    }

    private static void OpenDataManagement(AndroidDriver driver)
    {
        OpenShellFlyout(driver);
        AndroidElementWait.UntilText(driver, "Data Management").Click();
    }

    private static void OpenShellFlyout(AndroidDriver driver)
    {
        var nextActionUtc = DateTime.MinValue;
        CreateWait(driver).Until(currentDriver =>
        {
            if (currentDriver.FindElements(By.XPath("//*[@text='Data Management']")).Count > 0)
                return true;
            if (DateTime.UtcNow < nextActionUtc) return false;

            var drawer = currentDriver.FindElements(
                MobileBy.AccessibilityId("Open navigation drawer")).FirstOrDefault();
            if (drawer is null)
            {
                var navigateUp = currentDriver.FindElements(
                    MobileBy.AccessibilityId("Navigate up")).FirstOrDefault();
                if (navigateUp is not null)
                {
                    navigateUp.Click();
                    nextActionUtc = DateTime.UtcNow.AddSeconds(1);
                }
                return false;
            }
            drawer.Click();
            nextActionUtc = DateTime.UtcNow.AddSeconds(1);
            return false;
        });
    }

    private static void OpenDownloadsRoot(AndroidDriver driver)
    {
        UntilAny(driver, MobileBy.AccessibilityId("Show roots"),
            By.Id("com.google.android.documentsui:id/roots_toolbar")).Click();
        UntilAny(driver, By.XPath(
            "//*[@resource-id='com.google.android.documentsui:id/roots_list']//*[@text='Downloads']"))
            .Click();
    }

    private static IWebElement FindHorizontally(AndroidDriver driver, string automationId)
    {
        var resourceId = ResourceId(automationId);
        var displayed = driver.FindElements(By.Id(resourceId)).FirstOrDefault(item => item.Displayed);
        return displayed ?? driver.FindElement(MobileBy.AndroidUIAutomator(
            "new UiScrollable(new UiSelector().scrollable(true)).setAsHorizontalList()"
                + $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
    }

    private static void NavigateUp(AndroidDriver driver) =>
        UntilAny(driver, MobileBy.AccessibilityId("Navigate up")).Click();

    private static IWebElement UntilAny(AndroidDriver driver, params By[] selectors) =>
        CreateWait(driver).Until(currentDriver => selectors
            .SelectMany(currentDriver.FindElements)
            .FirstOrDefault(element => element.Displayed) ?? throw new NoSuchElementException());

    private static void UntilPackage(AndroidDriver driver, string packageName) =>
        CreateWait(driver).Until(_ => string.Equals(
            driver.CurrentPackage, packageName, StringComparison.Ordinal));

    private static WebDriverWait CreateWait(AndroidDriver driver)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250),
        };
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait;
    }

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private const string DocumentsPackage = "com.google.android.documentsui";

    private sealed record HostBackupPlatform(string CacheDirectory) : IBackupPlatform
    {
        public string AppVersion => "android-ui-test";
    }
}
