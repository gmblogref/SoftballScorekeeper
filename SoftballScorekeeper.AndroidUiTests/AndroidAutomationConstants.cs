namespace SoftballScorekeeper.AndroidUiTests;

internal static class AndroidAutomationConstants
{
    public const string TestModeFileName = "android-ui-test-mode";
    public const string ProductTestDatabaseFileName = "SoftballScorekeeper.Test.db3";
    public const string PlatformName = "Android";
    public const string AutomationName = "UiAutomator2";
    public const string AppPackage = "com.companyname.softballscorekeeper";
    public const string DefaultDeviceName = "Android Emulator";
    public const string DefaultAppiumServerUrl = "http://127.0.0.1:4723/";
    public const string TestSeedRequestFileName = "android-ui-test-seed";
    public const string MissingGameTestSeedRequestFileName = "android-ui-test-missing-game";
    public const string CompletedStatisticsSortingTestSeedRequestFileName =
        "android-ui-test-completed-statistics-sorting";
    public const string TeamRecordTestSeedRequestFileName = "android-ui-test-team-record";
    public const string BeerTotalsTestSeedRequestFileName = "android-ui-test-beer-totals";
    public const string TeamStatsTestSeedRequestFileName = "android-ui-test-team-stats";
    public const string TeamYearCopyTestSeedRequestFileName = "android-ui-test-team-year-copy";
    public const string TeamYearSummaryTestSeedRequestFileName =
        "android-ui-test-team-year-summary";
    public const string ManualRestoreTestSeedRequestFileName = "android-ui-test-manual-restore";
    public const string DeviceMigrationTestSeedRequestFileName = "android-ui-test-device-migration";
    public const string ManualFindingsReproductionTestSeedRequestFileName =
        "android-ui-test-manual-findings-reproduction";
    public const string ScheduleGamesTestSeedRequestFileName = "android-ui-test-schedule-games";
    public const string SeasonBackupReminderTestSeedRequestFileName =
        "android-ui-test-season-backup-reminder";
    public const string PhaseALineupTestSeedRequestFileName = "android-ui-test-phase-a-lineup";
    public const string LiveLineupTestSeedRequestFileName = "android-ui-test-live-lineup";
    public static readonly IReadOnlyList<string> TestSeedRequestFileNames =
    [
        TestSeedRequestFileName,
        MissingGameTestSeedRequestFileName,
        CompletedStatisticsSortingTestSeedRequestFileName,
        TeamRecordTestSeedRequestFileName,
        BeerTotalsTestSeedRequestFileName,
        TeamStatsTestSeedRequestFileName,
        TeamYearCopyTestSeedRequestFileName,
        TeamYearSummaryTestSeedRequestFileName,
        ManualRestoreTestSeedRequestFileName,
        DeviceMigrationTestSeedRequestFileName,
        ManualFindingsReproductionTestSeedRequestFileName,
        ScheduleGamesTestSeedRequestFileName,
        SeasonBackupReminderTestSeedRequestFileName,
        PhaseALineupTestSeedRequestFileName,
        LiveLineupTestSeedRequestFileName,
    ];
    public const string FirstBatterName = "Alex Automation";
    public const string SecondBatterName = "Blake Automation";
    public const string ThirdBatterName = "Casey Automation";
    public const string FourthBatterName = "Drew Automation";
}
