using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class DatabaseModeTests
{
    [Fact]
    public void Fixed_paths_keep_production_test_and_android_ui_test_distinct()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mode-paths-{Guid.NewGuid():N}");
        var productionPaths = CreatePaths(directory, useAndroidUiTestDatabaseAsNormal: false);
        var androidPaths = CreatePaths(directory, useAndroidUiTestDatabaseAsNormal: true);

        Assert.Equal(
            Path.Combine(directory, "SoftballScorekeeper.db3"),
            productionPaths.NormalDatabasePath
        );
        Assert.Equal(
            Path.Combine(directory, "SoftballScorekeeper.AndroidUiTests.db3"),
            androidPaths.NormalDatabasePath
        );
        Assert.Equal(
            Path.Combine(directory, "SoftballScorekeeper.Test.db3"),
            productionPaths.TestDatabasePath
        );
        Assert.Equal(productionPaths.TestDatabasePath, androidPaths.TestDatabasePath);
    }

    [Fact]
    public void Product_test_configuration_rejects_production_or_android_ui_test_filenames()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mode-paths-{Guid.NewGuid():N}");

        Assert.Throws<ArgumentException>(() =>
            new AppDatabasePaths(
                directory,
                "SoftballScorekeeper.db3",
                "SoftballScorekeeper.db3",
                "SoftballScorekeeper.AndroidUiTests.db3",
                false
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new AppDatabasePaths(
                directory,
                "SoftballScorekeeper.db3",
                "SoftballScorekeeper.AndroidUiTests.db3",
                "SoftballScorekeeper.AndroidUiTests.db3",
                false
            )
        );
    }

    [Fact]
    public async Task Repository_instance_remains_usable_and_data_is_isolated_across_switches()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var repository = new TeamRepository(fixture.Database);
        await repository.CreateAsync(new Team { Name = "Production Sentinel", Year = 2026 });

        await SwitchAsync(fixture.Database, DatabaseMode.Test);
        Assert.Equal(fixture.Paths.TestDatabasePath, fixture.Database.DatabasePath);
        Assert.Empty(await repository.GetActiveAsync());
        Assert.Equal(
            AppDatabase.CurrentSchemaVersion,
            await fixture.Database.GetSchemaVersionAsync()
        );
        await repository.CreateAsync(new Team { Name = "Test Only", Year = 2026 });

        await SwitchAsync(fixture.Database, DatabaseMode.Production);
        Assert.Equal("Production Sentinel", Assert.Single(await repository.GetActiveAsync()).Name);

        await SwitchAsync(fixture.Database, DatabaseMode.Test);
        Assert.Equal("Test Only", Assert.Single(await repository.GetActiveAsync()).Name);
    }

    [Fact]
    public async Task Reset_deletes_only_product_test_database_and_sidecars()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var repository = new TeamRepository(fixture.Database);
        await repository.CreateAsync(new Team { Name = "Production Sentinel", Year = 2026 });
        await SwitchAsync(fixture.Database, DatabaseMode.Test);
        await repository.CreateAsync(new Team { Name = "Disposable Test", Year = 2026 });

        foreach (var path in AndroidUiSidecars(fixture.Paths))
            await File.WriteAllTextAsync(path, "preserve");
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
            await File.WriteAllTextAsync(fixture.Paths.TestDatabasePath + suffix, "delete");

        await using (var exclusive = await fixture.Database.BeginExclusiveLifecycleAsync())
            await fixture.Database.ResetTestDatabaseAsync();

        Assert.Equal(DatabaseMode.Production, fixture.Database.ActiveMode);
        Assert.Equal("Production Sentinel", Assert.Single(await repository.GetActiveAsync()).Name);
        Assert.All(
            new[] { "", "-journal", "-wal", "-shm" },
            suffix => Assert.False(File.Exists(fixture.Paths.TestDatabasePath + suffix))
        );
        Assert.All(AndroidUiSidecars(fixture.Paths), path => Assert.True(File.Exists(path)));

        await SwitchAsync(fixture.Database, DatabaseMode.Test);
        Assert.Empty(await repository.GetActiveAsync());
    }

    [Fact]
    public async Task In_progress_session_and_pending_play_remain_in_their_own_database()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var team = new Team { Name = "Scoring Team", Year = 2026 };
        await new TeamRepository(fixture.Database).CreateAsync(team);
        var player = await new PlayerRepository(fixture.Database).CreateForTeamAsync(
            new Player { FirstName = "Alex" },
            team.Id
        );
        var games = await new GameRepository(fixture.Database).CreateForTeamAsync(
            team.Id,
            new Season { Name = "Summer", Year = 2026 },
            [new Game { ScheduledDate = new DateTime(2026, 7, 1) }]
        );
        var gameId = Assert.Single(games.CreatedGameIds);
        await new GameLineupRepository(fixture.Database).SaveInitialAsync(
            team.Id,
            gameId,
            [player.PlayerId]
        );
        var scoring = new GameScoringSessionRepository(fixture.Database);
        Assert.True((await scoring.InitializeAsync(team.Id, gameId, TeamGameSide.Home)).IsSuccess);
        Assert.True((await scoring.SelectBatterResultAsync(gameId, BatterResult.Single)).IsSuccess);

        await SwitchAsync(fixture.Database, DatabaseMode.Test);
        Assert.Equal(0, await CountAsync<GameScoringSession>(fixture.Database));
        Assert.Equal(0, await CountAsync<GameScoringPendingPlay>(fixture.Database));

        await SwitchAsync(fixture.Database, DatabaseMode.Production);
        Assert.Equal(1, await CountAsync<GameScoringSession>(fixture.Database));
        Assert.Equal(1, await CountAsync<GameScoringPendingPlay>(fixture.Database));
    }

    [Fact]
    public async Task Persisted_test_mode_is_selected_before_initialization()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync(DatabaseMode.Test);

        Assert.Equal(DatabaseMode.Test, fixture.Database.ActiveMode);
        Assert.Equal(fixture.Paths.TestDatabasePath, fixture.Database.DatabasePath);
        Assert.Equal(
            AppDatabase.CurrentSchemaVersion,
            await fixture.Database.GetSchemaVersionAsync()
        );
        Assert.False(File.Exists(fixture.Paths.ProductionDatabasePath));
    }

    [Fact]
    public async Task Preference_failure_rolls_database_back_and_does_not_publish_test_mode()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var state = new DatabaseModeState(DatabaseMode.Production);
        var preference = new RecordingPreferenceStore(DatabaseMode.Production)
        {
            FailOnSave = true,
        };
        var service = new DatabaseModeService(
            fixture.Database,
            state,
            preference,
            new ApplicationWorkflowCoordinator()
        );

        var result = await service.EnterTestModeAsync(() => Task.FromResult(true));

        Assert.Equal(DatabaseModeChangeResult.Failed, result);
        Assert.Equal(DatabaseMode.Production, state.CurrentMode);
        Assert.Equal(DatabaseMode.Production, fixture.Database.ActiveMode);
        Assert.Equal(fixture.Paths.ProductionDatabasePath, fixture.Database.DatabasePath);
        Assert.Equal(DatabaseMode.Production, preference.Mode);
    }

    [Fact]
    public async Task Invalid_test_database_rolls_back_to_normal_mode_and_preference()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        await File.WriteAllTextAsync(fixture.Paths.TestDatabasePath, "not sqlite");
        var state = new DatabaseModeState(DatabaseMode.Production);
        var preference = new RecordingPreferenceStore(DatabaseMode.Production);
        var service = new DatabaseModeService(
            fixture.Database,
            state,
            preference,
            new ApplicationWorkflowCoordinator()
        );

        var result = await service.EnterTestModeAsync(() => Task.FromResult(true));

        Assert.Equal(DatabaseModeChangeResult.Failed, result);
        Assert.Equal(DatabaseMode.Production, state.CurrentMode);
        Assert.Equal(DatabaseMode.Production, preference.Mode);
        Assert.Equal(DatabaseMode.Production, fixture.Database.ActiveMode);
        Assert.Equal(
            AppDatabase.CurrentSchemaVersion,
            await fixture.Database.GetSchemaVersionAsync()
        );
    }

    [Fact]
    public async Task Mode_service_keep_updates_database_preference_and_observable_state()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var state = new DatabaseModeState(DatabaseMode.Production);
        var preference = new RecordingPreferenceStore(DatabaseMode.Production);
        var service = new DatabaseModeService(
            fixture.Database,
            state,
            preference,
            new ApplicationWorkflowCoordinator()
        );
        var notifications = new List<string?>();
        state.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        Assert.Equal(
            DatabaseModeChangeResult.Changed,
            await service.EnterTestModeAsync(() => Task.FromResult(true))
        );
        Assert.True(state.IsTestMode);
        Assert.False(state.CanUsePortableBackupAndRestore);
        Assert.Equal(DatabaseMode.Test, preference.Mode);
        Assert.Contains(nameof(DatabaseModeState.IsTestMode), notifications);

        Assert.Equal(
            DatabaseModeChangeResult.Changed,
            await service.ExitTestModeAsync(() => Task.FromResult(TestModeExitChoice.KeepTestData))
        );
        Assert.True(state.IsProductionMode);
        Assert.True(state.CanUsePortableBackupAndRestore);
        Assert.Equal(DatabaseMode.Production, preference.Mode);
    }

    [Fact]
    public async Task Shared_workflow_coordinator_rejects_overlapping_mode_change()
    {
        await using var fixture = await ModeDatabaseFixture.CreateAsync();
        var coordinator = new ApplicationWorkflowCoordinator();
        await using var existingWorkflow = await coordinator.TryBeginAsync();
        var service = new DatabaseModeService(
            fixture.Database,
            new DatabaseModeState(DatabaseMode.Production),
            new RecordingPreferenceStore(DatabaseMode.Production),
            coordinator
        );

        var result = await service.EnterTestModeAsync(() => Task.FromResult(true));

        Assert.Equal(DatabaseModeChangeResult.AlreadyInProgress, result);
        Assert.Equal(DatabaseMode.Production, fixture.Database.ActiveMode);
    }

    private static AppDatabasePaths CreatePaths(
        string directory,
        bool useAndroidUiTestDatabaseAsNormal
    ) =>
        new(
            directory,
            "SoftballScorekeeper.db3",
            AppDatabasePaths.DedicatedTestDatabaseFileName,
            "SoftballScorekeeper.AndroidUiTests.db3",
            useAndroidUiTestDatabaseAsNormal
        );

    private static async Task SwitchAsync(AppDatabase database, DatabaseMode mode)
    {
        await using var exclusive = await database.BeginExclusiveLifecycleAsync();
        await database.SwitchModeAsync(mode);
    }

    private static async Task<int> CountAsync<T>(AppDatabase database)
        where T : new()
    {
        await using var operation = await database.BeginOperationAsync();
        return await operation.Connection.Table<T>().CountAsync();
    }

    private static string[] AndroidUiSidecars(AppDatabasePaths paths) =>
        [
            paths.AndroidUiTestDatabasePath + "-journal",
            paths.AndroidUiTestDatabasePath + "-wal",
            paths.AndroidUiTestDatabasePath + "-shm",
        ];

    private sealed class RecordingPreferenceStore(DatabaseMode mode) : IDatabaseModePreferenceStore
    {
        public DatabaseMode Mode { get; private set; } = mode;
        public bool FailOnSave { get; init; }

        public DatabaseMode Load() => Mode;

        public void Save(DatabaseMode newMode)
        {
            if (FailOnSave)
                throw new InvalidOperationException("Preference write failed.");
            Mode = newMode;
        }
    }

    private sealed class ModeDatabaseFixture : IAsyncDisposable
    {
        private ModeDatabaseFixture(DatabaseMode initialMode)
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"SoftballScorekeeper.ModeTests-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(DirectoryPath);
            Paths = CreatePaths(DirectoryPath, useAndroidUiTestDatabaseAsNormal: false);
            Database = new AppDatabase(Paths, initialMode);
        }

        public string DirectoryPath { get; }
        public AppDatabasePaths Paths { get; }
        public AppDatabase Database { get; }

        public static async Task<ModeDatabaseFixture> CreateAsync(
            DatabaseMode initialMode = DatabaseMode.Production
        )
        {
            var fixture = new ModeDatabaseFixture(initialMode);
            await fixture.Database.InitializeAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.CloseAsync();
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
