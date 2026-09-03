using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class AppDatabaseFileIsolationTests
{
    [Fact]
    public void Normal_and_android_ui_modes_resolve_distinct_configured_paths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"database-path-{Guid.NewGuid():N}");

        var normalPath = AppDatabaseFileIsolation.ResolveDatabasePath(
            directory,
            "SoftballScorekeeper.db3",
            "SoftballScorekeeper.AndroidUiTests.db3",
            useAndroidUiTestDatabase: false
        );
        var testPath = AppDatabaseFileIsolation.ResolveDatabasePath(
            directory,
            "SoftballScorekeeper.db3",
            "SoftballScorekeeper.AndroidUiTests.db3",
            useAndroidUiTestDatabase: true
        );

        Assert.Equal(Path.Combine(directory, "SoftballScorekeeper.db3"), normalPath);
        Assert.Equal(Path.Combine(directory, "SoftballScorekeeper.AndroidUiTests.db3"), testPath);
        Assert.NotEqual(normalPath, testPath);
    }

    [Fact]
    public void Android_ui_reset_removes_only_test_database_artifacts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"database-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var normalPath = Path.Combine(directory, "SoftballScorekeeper.db3");
            var testPath = Path.Combine(directory, "SoftballScorekeeper.AndroidUiTests.db3");
            File.WriteAllText(normalPath, "normal-data");
            foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
                File.WriteAllText(testPath + suffix, "test-data");

            AppDatabaseFileIsolation.ResetAndroidUiTestDatabase(normalPath, testPath);

            Assert.Equal("normal-data", File.ReadAllText(normalPath));
            Assert.All(
                new[] { "", "-journal", "-wal", "-shm" },
                suffix => Assert.False(File.Exists(testPath + suffix))
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Reset_rejects_the_normal_database_as_the_test_target()
    {
        var path = Path.Combine(Path.GetTempPath(), $"same-database-{Guid.NewGuid():N}.db3");

        Assert.Throws<InvalidOperationException>(() =>
            AppDatabaseFileIsolation.ResetAndroidUiTestDatabase(path, path)
        );
    }

    [Fact]
    public async Task Resetting_test_database_preserves_normal_persisted_data()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"database-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var normalPath = Path.Combine(directory, "SoftballScorekeeper.db3");
        var testPath = Path.Combine(directory, "SoftballScorekeeper.AndroidUiTests.db3");
        try
        {
            var normalDatabase = new AppDatabase(normalPath);
            var testDatabase = new AppDatabase(testPath);
            await new TeamRepository(normalDatabase).CreateAsync(
                new Team { Name = "Preserved Normal Team" }
            );
            await new TeamRepository(testDatabase).CreateAsync(
                new Team { Name = "Discarded Test Team" }
            );
            await normalDatabase.CloseAsync();
            await testDatabase.CloseAsync();

            AppDatabaseFileIsolation.ResetAndroidUiTestDatabase(normalPath, testPath);

            var reopenedNormal = new AppDatabase(normalPath);
            var reopenedTest = new AppDatabase(testPath);
            try
            {
                Assert.Equal(
                    "Preserved Normal Team",
                    Assert.Single(await new TeamRepository(reopenedNormal).GetActiveAsync()).Name
                );
                Assert.Empty(await new TeamRepository(reopenedTest).GetActiveAsync());
            }
            finally
            {
                await reopenedNormal.CloseAsync();
                await reopenedTest.CloseAsync();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
