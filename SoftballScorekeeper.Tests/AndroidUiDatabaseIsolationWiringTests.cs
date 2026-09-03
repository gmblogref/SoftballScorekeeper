namespace SoftballScorekeeper.Tests;

public sealed class AndroidUiDatabaseIsolationWiringTests
{
    [Fact]
    public void App_configuration_selects_and_resets_only_the_ui_test_database()
    {
        var settings = Read("SoftballScorekeeper", "Configuration", "ApplicationSettings.cs");
        var composition = Read("SoftballScorekeeper", "MauiProgram.cs");
        var initializer = Read("SoftballScorekeeper", "Testing", "AndroidUiTestDataInitializer.cs");

        Assert.Contains("SoftballScorekeeper.db3", settings);
        Assert.Contains("SoftballScorekeeper.AndroidUiTests.db3", settings);
        Assert.Contains("IsInitializationRequested", composition);
        Assert.Contains("AppDatabaseFileIsolation.ResolveDatabasePath", composition);
        Assert.Contains("AppDatabaseFileIsolation.ResetAndroidUiTestDatabase", composition);
        Assert.Contains("RequestFileNames.Any", initializer);
    }

    [Fact]
    public void Appium_fixture_requests_a_seed_without_clearing_application_data()
    {
        var fixture = Read("SoftballScorekeeper.AndroidUiTests", "AndroidAppiumFixture.cs");

        Assert.Contains("RequestDeterministicSeed", fixture);
        Assert.DoesNotContain("\"pm\", \"clear\"", fixture);
        Assert.Contains("TestSeedRequestFileNames", fixture);
        Assert.Contains("\"touch\", $\"files/{seedRequestFileName}\"", fixture);
    }

    private static string Read(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
