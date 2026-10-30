namespace SoftballScorekeeper.Tests;

public sealed class ReleaseBackupRestoreAcceptanceWiringTests
{
    private const string AuthoritativeFilter =
        "FullyQualifiedName=SoftballScorekeeper.AndroidUiTests.ReleaseBackupRestoreAcceptanceTests.Signed_release_backup_restore_round_trip_preserves_test_mode_isolation";

    [Fact]
    public void Release_backup_restore_uses_one_exact_no_reset_workflow()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseBackupRestoreAcceptanceTests.cs"
        );

        Assert.Equal(1, CountOccurrences(acceptance, "[AndroidUiFact]"));
        Assert.Contains(
            "public async Task Signed_release_backup_restore_round_trip_preserves_test_mode_isolation()",
            acceptance
        );
        Assert.DoesNotContain("ResetApplication", acceptance);
        Assert.DoesNotContain("RequestDeterministicSeed", acceptance);
        AssertOrdered(
            acceptance,
            "fixture.RelaunchApplicationWithoutReset()",
            "var driver = fixture.Driver"
        );
        Assert.Contains("fixture.ExecuteAdb(\"install\", \"-r\", apkPath)", acceptance);
        Assert.DoesNotContain("\"uninstall\"", acceptance);
        Assert.DoesNotContain("\"pm\", \"clear\"", acceptance);
    }

    [Fact]
    public void Release_backup_restore_exercises_real_documents_and_database_isolation()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseBackupRestoreAcceptanceTests.cs"
        );

        Assert.Contains("/sdcard/Download/{remoteDirectoryName}", acceptance);
        Assert.Contains("com.google.android.documentsui", acceptance);
        Assert.Contains("BackupDataButton", acceptance);
        Assert.Contains("RestoreDataButton", acceptance);
        Assert.Contains("BackupPackageService.MetadataEntryName", acceptance);
        Assert.Contains("BackupPackageService.DatabaseEntryName", acceptance);
        Assert.Contains("BackupDatabaseCompatibility.Current", acceptance);
        Assert.Contains("ExitProductTestModeKeepingData", acceptance);
        Assert.True(
            CountOccurrences(acceptance, "AssertRecordsAbsent(driver, PostBackupProduction)") >= 2
        );
        Assert.True(CountOccurrences(acceptance, "AssertRecordsPresent(driver, TestSentinel") >= 1);
        Assert.Contains("CaptureFailureArtifacts", acceptance);
        Assert.Contains("FATAL EXCEPTION: main", acceptance);
        Assert.Contains("SQLiteException", acceptance);
    }

    [Fact]
    public void Release_backup_restore_requeries_documentsui_elements_after_stale_references()
    {
        var acceptance = Read(
            "SoftballScorekeeper.AndroidUiTests",
            "ReleaseBackupRestoreAcceptanceTests.cs"
        );

        AssertOrdered(
            acceptance,
            "private static IWebElement UntilAny",
            ".Until(currentDriver =>",
            ".SelectMany(currentDriver.FindElements)",
            ".FirstOrDefault(element => element.Displayed)"
        );
        AssertOrdered(
            acceptance,
            "private static WebDriverWait Wait",
            "wait.IgnoreExceptionTypes(",
            "typeof(NoSuchElementException)",
            "typeof(StaleElementReferenceException)",
            "return wait;"
        );
    }

    [Fact]
    public void Runner_requires_exact_filter_artifact_identity_and_release_avd()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");

        Assert.Contains("[switch]$ReleaseBackupRestoreAcceptance", runner);
        Assert.Contains(
            $"$releaseBackupRestoreAcceptanceTestFilter = '{AuthoritativeFilter}'",
            runner
        );
        Assert.Contains(
            "[string]::Equals($TestFilter, $releaseBackupRestoreAcceptanceTestFilter, [StringComparison]::Ordinal)",
            runner
        );
        Assert.DoesNotContain("FullyQualifiedName~ReleaseBackupRestoreAcceptanceTests", runner);
        Assert.Contains("Assert-ReleaseApkHash", runner);
        Assert.Contains("Assert-ReleaseApkSignature", runner);
        Assert.Contains("Assert-ReleaseApkMetadata", runner);
        Assert.Contains("-ExpectedPackageName $releasePackageName", runner);
        Assert.Contains("-ExpectedVersionName '2.0.0'", runner);
        Assert.Contains("-ExpectedVersionCode '6'", runner);
        Assert.Contains("foreach ($scheme in @('v1', 'v2', 'v3'))", runner);
        Assert.Contains("$certificateMatches.Count -ne 1", runner);
        Assert.Contains("$releaseCertificateSha256", runner);
        Assert.Contains(
            "$releaseAcceptanceAvdName = 'softball_tablet_release_acceptance_api_35'",
            runner
        );
        Assert.Contains("physical device '$DeviceId' is not allowed", runner);
        Assert.Contains(
            "[string]::Equals($selectedAvdName, $releaseAcceptanceAvdName, [StringComparison]::OrdinalIgnoreCase)",
            runner
        );
    }

    [Fact]
    public void Runner_skips_debug_build_and_retains_release_evidence()
    {
        var runner = Read("scripts", "run-android-ui-tests.ps1");

        Assert.Contains(
            "$releaseModeEnabled = $ReleaseAcceptance -or $ReleaseBackupRestoreAcceptance",
            runner
        );
        Assert.Contains("if ($releaseModeEnabled)", runner);
        Assert.Contains("else {\n        $appProject", runner.Replace("\r\n", "\n"));
        Assert.Contains("SOFTBALL_ANDROID_RELEASE_BACKUP_RESTORE_ACCEPTANCE", runner);
        Assert.Contains(
            "SoftballScorekeeper.AndroidUiTests\\TestResults\\ReleaseAcceptance",
            runner
        );
        Assert.Contains("release-backup-restore-acceptance", runner);
        Assert.Contains("'--logger', \"trx;LogFileName=$releaseTrxName\"", runner);
        Assert.Contains("'--results-directory', $releaseResultsDirectory", runner);
    }

    private static void AssertOrdered(string source, params string[] values)
    {
        var prior = -1;
        foreach (var value in values)
        {
            var current = source.IndexOf(value, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{value}' after index {prior}.");
            prior = current;
        }
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
