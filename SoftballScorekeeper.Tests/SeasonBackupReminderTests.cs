using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SQLite;

namespace SoftballScorekeeper.Tests;

public sealed class SeasonBackupReminderTests
{
    private static readonly DateTime Today = new(2026, 9, 22);

    [Fact]
    public async Task Season_without_games_is_not_eligible()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Empty Schedule");
        Assert.NotNull(
            await db.CreateSeasonRepository().GetOrCreateForTeamAsync(team.Id, "Fall", team.Year)
        );

        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
    }

    [Fact]
    public async Task Incomplete_season_is_not_eligible()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Incomplete", "Fall", (Today.AddDays(-1), false));

        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
    }

    [Fact]
    public async Task Completed_season_becomes_eligible_only_after_its_final_local_date()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Today", "Fall", (Today, true));
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());

        await CreateSeasonAsync(db, "Yesterday", "Fall", (Today.AddDays(-1), true));
        var eligible = await CreateReminderRepository(db).GetEligibleAsync();

        Assert.Single(eligible);
    }

    [Fact]
    public async Task Final_date_doubleheader_requires_every_game_to_be_completed()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateSeasonAsync(
            db,
            "Doubleheader",
            "Fall",
            (Today.AddDays(-1), true),
            (Today.AddDays(-1), false)
        );
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());

        await CompleteAsync(db, context.Games[1].Id);

        Assert.Single(await CreateReminderRepository(db).GetEligibleAsync());
    }

    [Fact]
    public async Task Multiple_seasons_and_teams_produce_one_startup_decision()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "First", "Spring", (Today.AddDays(-2), true));
        await CreateSeasonAsync(db, "Second", "Summer", (Today.AddDays(-1), true));
        var promptCount = 0;
        var service = CreateStartupService(db, new RecordingDestination(db.DirectoryPath));

        var result = await service.RunAtStartupAsync(() =>
        {
            promptCount++;
            return Task.FromResult(SeasonBackupReminderChoice.RemindLater);
        });
        var repeated = await service.RunAtStartupAsync(() =>
        {
            promptCount++;
            return Task.FromResult(SeasonBackupReminderChoice.RemindLater);
        });

        Assert.Equal(SeasonBackupReminderResult.RemindLater, result);
        Assert.Equal(SeasonBackupReminderResult.AlreadyEvaluated, repeated);
        Assert.Equal(1, promptCount);
    }

    [Fact]
    public async Task Ok_invokes_manual_backup_and_saved_result_satisfies_all_eligible_seasons()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "First", "Spring", (Today.AddDays(-2), true));
        await CreateSeasonAsync(db, "Second", "Summer", (Today.AddDays(-1), true));
        var destination = new RecordingDestination(db.DirectoryPath);
        var service = CreateStartupService(db, destination);

        var result = await service.RunAtStartupAsync(() =>
            Task.FromResult(SeasonBackupReminderChoice.Ok)
        );

        Assert.Equal(SeasonBackupReminderResult.BackupSaved, result);
        Assert.Equal(1, destination.CallCount);
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
        Assert.All(
            await GetCheckpointsAsync(db),
            checkpoint => Assert.Equal(
                SeasonBackupReminderCheckpointDisposition.BackupSatisfied,
                checkpoint.Disposition
            )
        );
    }

    [Fact]
    public async Task Cancelled_backup_does_not_satisfy_checkpoint()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Cancelled", "Fall", (Today.AddDays(-1), true));
        var service = CreateStartupService(
            db,
            new RecordingDestination(db.DirectoryPath, BackupDestinationResult.Cancelled)
        );

        Assert.Equal(
            SeasonBackupReminderResult.BackupCancelled,
            await service.RunAtStartupAsync(() =>
                Task.FromResult(SeasonBackupReminderChoice.Ok)
            )
        );
        Assert.Single(await CreateReminderRepository(db).GetEligibleAsync());
        Assert.Empty(await GetCheckpointsAsync(db));
    }

    [Fact]
    public async Task Failed_backup_does_not_satisfy_checkpoint()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Failed", "Fall", (Today.AddDays(-1), true));
        var service = CreateStartupService(
            db,
            new RecordingDestination(db.DirectoryPath) { Failure = new IOException("failed") }
        );

        Assert.Equal(
            SeasonBackupReminderResult.BackupFailed,
            await service.RunAtStartupAsync(() =>
                Task.FromResult(SeasonBackupReminderChoice.Ok)
            )
        );
        Assert.Single(await CreateReminderRepository(db).GetEligibleAsync());
        Assert.Empty(await GetCheckpointsAsync(db));
    }

    [Fact]
    public async Task Ordinary_manual_backup_satisfies_only_checkpoints_eligible_when_save_finishes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateSeasonAsync(
            db,
            "Manual",
            "Fall",
            (Today.AddDays(-1), false)
        );
        var destination = new RecordingDestination(db.DirectoryPath);
        var manualBackup = CreateManualBackupService(db, destination);

        Assert.Equal(ManualBackupResult.Saved, await manualBackup.RunAsync());
        Assert.Empty(await GetCheckpointsAsync(db));

        await CompleteAsync(db, context.Games[0].Id);
        Assert.Single(await CreateReminderRepository(db).GetEligibleAsync());

        Assert.Equal(ManualBackupResult.Saved, await manualBackup.RunAsync());
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
    }

    [Fact]
    public async Task Backup_rechecks_eligibility_after_the_destination_write_completes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateSeasonAsync(
            db,
            "During Picker",
            "Fall",
            (Today.AddDays(-1), false)
        );
        var destination = new BlockingDestination();
        var backup = CreateManualBackupService(db, destination);

        var backupTask = backup.RunAsync();
        await destination.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CompleteAsync(db, context.Games[0].Id);
        destination.Continue.TrySetResult();

        Assert.Equal(ManualBackupResult.Saved, await backupTask);
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
        Assert.Single(await GetCheckpointsAsync(db));
    }

    [Fact]
    public async Task Remind_later_is_session_only_and_next_session_can_prompt_again()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Later", "Fall", (Today.AddDays(-1), true));
        var firstSession = CreateStartupService(db, new RecordingDestination(db.DirectoryPath));
        var promptCount = 0;

        Assert.Equal(
            SeasonBackupReminderResult.RemindLater,
            await firstSession.RunAtStartupAsync(() =>
            {
                promptCount++;
                return Task.FromResult(SeasonBackupReminderChoice.RemindLater);
            })
        );
        Assert.Equal(
            SeasonBackupReminderResult.AlreadyEvaluated,
            await firstSession.RunAtStartupAsync(() =>
            {
                promptCount++;
                return Task.FromResult(SeasonBackupReminderChoice.RemindLater);
            })
        );
        Assert.Empty(await GetCheckpointsAsync(db));

        var nextSession = CreateStartupService(db, new RecordingDestination(db.DirectoryPath));
        Assert.Equal(
            SeasonBackupReminderResult.RemindLater,
            await nextSession.RunAtStartupAsync(() =>
            {
                promptCount++;
                return Task.FromResult(SeasonBackupReminderChoice.RemindLater);
            })
        );
        Assert.Equal(2, promptCount);
    }

    [Fact]
    public async Task Dismiss_persists_without_blocking_a_future_season()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Seasons", "Spring", (Today.AddDays(-10), true));
        var firstSession = CreateStartupService(db, new RecordingDestination(db.DirectoryPath));
        Assert.Equal(
            SeasonBackupReminderResult.Dismissed,
            await firstSession.RunAtStartupAsync(() =>
                Task.FromResult(SeasonBackupReminderChoice.Dismiss)
            )
        );
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());

        await CreateSeasonForTeamAsync(
            db,
            (await db.CreateTeamRepository().GetActiveAsync()).Single(),
            "Summer",
            (Today.AddDays(-1), true)
        );
        Assert.Single(await CreateReminderRepository(db).GetEligibleAsync());

        var nextSession = CreateStartupService(db, new RecordingDestination(db.DirectoryPath));
        Assert.Equal(
            SeasonBackupReminderResult.RemindLater,
            await nextSession.RunAtStartupAsync(() =>
                Task.FromResult(SeasonBackupReminderChoice.RemindLater)
            )
        );
        Assert.Contains(
            await GetCheckpointsAsync(db),
            checkpoint =>
                checkpoint.Disposition == SeasonBackupReminderCheckpointDisposition.Dismissed
        );
    }

    [Fact]
    public async Task Handled_checkpoint_changes_for_game_membership_or_playing_date_but_not_metadata()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateSeasonAsync(
            db,
            "Schedule Changes",
            "Fall",
            (Today.AddDays(-2), true)
        );
        var repository = CreateReminderRepository(db);
        await repository.DismissAsync(await repository.GetEligibleAsync());
        var game = context.Games[0];

        await db.ExecuteAsync(
            "UPDATE Game SET ScheduledTime = ?, Opponent = ?, Field = ? WHERE Id = ?",
            TimeSpan.FromHours(20),
            "Changed",
            "Changed Field",
            game.Id
        );
        Assert.Empty(await repository.GetEligibleAsync());

        await db.ExecuteAsync(
            "UPDATE Game SET ScheduledDate = ? WHERE Id = ?",
            Today.AddDays(-1),
            game.Id
        );
        Assert.Single(await repository.GetEligibleAsync());

        await db.ExecuteAsync(
            "UPDATE Game SET ScheduledDate = ? WHERE Id = ?",
            Today.AddDays(-2),
            game.Id
        );
        Assert.Empty(await repository.GetEligibleAsync());

        var added = await AddGameAsync(db, context.Team, context.Season, Today.AddDays(-1), false);
        Assert.Empty(await repository.GetEligibleAsync());
        await CompleteAsync(db, added.Id);
        Assert.Single(await repository.GetEligibleAsync());
    }

    [Fact]
    public async Task Moving_completed_game_between_seasons_changes_both_checkpoints()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Move Game");
        var first = await CreateSeasonForTeamAsync(
            db,
            team,
            "Spring",
            (Today.AddDays(-3), true),
            (Today.AddDays(-2), true)
        );
        var second = await CreateSeasonForTeamAsync(
            db,
            team,
            "Summer",
            (Today.AddDays(-1), true)
        );
        var repository = CreateReminderRepository(db);
        await repository.DismissAsync(await repository.GetEligibleAsync());

        await db.ExecuteAsync(
            "UPDATE Game SET SeasonId = ? WHERE Id = ?",
            second.Season.Id,
            first.Games[1].Id
        );

        Assert.Equal(2, (await repository.GetEligibleAsync()).Count);
    }

    [Fact]
    public async Task Test_mode_consumes_startup_without_prompting_or_writing_checkpoints()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await CreateSeasonAsync(db, "Production", "Fall", (Today.AddDays(-1), true));
        var state = new DatabaseModeState(DatabaseMode.Test);
        var timeProvider = new FixedTimeProvider(Today);
        var repository = new SeasonBackupReminderRepository(db.Database, state, timeProvider);
        var package = new BackupPackageService(
            db.Database,
            new TestBackupPlatform(db.DirectoryPath),
            timeProvider
        );
        var manual = new ManualBackupService(
            package,
            new RecordingDestination(db.DirectoryPath),
            state,
            new ApplicationWorkflowCoordinator(),
            repository
        );
        var service = new SeasonBackupReminderService(repository, manual, state);
        var promptCount = 0;

        Assert.Equal(
            SeasonBackupReminderResult.NoReminder,
            await service.RunAtStartupAsync(() =>
            {
                promptCount++;
                return Task.FromResult(SeasonBackupReminderChoice.Ok);
            })
        );
        Assert.Equal(0, promptCount);
        Assert.Empty(await GetCheckpointsAsync(db));

        Assert.Equal(
            SeasonBackupReminderResult.AlreadyEvaluated,
            await service.RunAtStartupAsync(() =>
            {
                promptCount++;
                return Task.FromResult(SeasonBackupReminderChoice.Ok);
            })
        );
    }

    [Fact]
    public async Task Test_mode_activity_leaves_physical_production_reminder_state_unchanged()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"SoftballScorekeeper.ReminderModeTests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directory);
        var paths = new AppDatabasePaths(
            directory,
            "Production.db3",
            AppDatabasePaths.DedicatedTestDatabaseFileName,
            "AndroidUi.db3",
            useAndroidUiTestDatabaseAsNormal: false
        );
        var database = new AppDatabase(paths, DatabaseMode.Production);
        try
        {
            await database.InitializeAsync();
            var team = new Team { Name = "Production Only", Year = Today.Year };
            Assert.Equal(1, await new TeamRepository(database).CreateAsync(team));
            var season = await new SeasonRepository(database).GetOrCreateForTeamAsync(
                team.Id,
                "Fall",
                team.Year
            );
            Assert.NotNull(season);
            var created = await new GameRepository(database).CreateForTeamAsync(
                team.Id,
                season,
                [new Game { ScheduledDate = Today.AddDays(-1) }]
            );
            await using (var operation = await database.BeginOperationAsync())
            {
                var session = new GameScoringSession
                    {
                        GameId = Assert.Single(created.CreatedGameIds),
                        Status = GameScoringStatus.Completed,
                        CurrentBatterPosition = 1,
                    };
                await operation.Connection.InsertAsync(session);
                await operation.Connection.InsertAsync(
                    new GameScoringLineupPlayer
                    {
                        GameScoringSessionId = session.Id,
                        PlayerId = 1,
                        BattingPosition = 1,
                    }
                );
            }

            await using (var exclusive = await database.BeginExclusiveLifecycleAsync())
                await database.SwitchModeAsync(DatabaseMode.Test);
            var testState = new DatabaseModeState(DatabaseMode.Test);
            var testRepository = new SeasonBackupReminderRepository(
                database,
                testState,
                new FixedTimeProvider(Today)
            );
            Assert.Empty(await testRepository.GetEligibleAsync());
            await testRepository.SatisfyEligibleAsync();

            await using (var exclusive = await database.BeginExclusiveLifecycleAsync())
                await database.SwitchModeAsync(DatabaseMode.Production);
            var productionRepository = new SeasonBackupReminderRepository(
                database,
                new DatabaseModeState(DatabaseMode.Production),
                new FixedTimeProvider(Today)
            );
            Assert.Single(await productionRepository.GetEligibleAsync());
            await using var productionOperation = await database.BeginOperationAsync();
            Assert.Equal(
                0,
                await productionOperation.Connection.Table<SeasonBackupReminderCheckpoint>()
                    .CountAsync()
            );
        }
        finally
        {
            await database.CloseAsync();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Version_two_migration_baselines_existing_completed_schedules()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.UseConnectionAsync(async connection =>
        {
            await connection.CreateTableAsync<Team>();
            await connection.CreateTableAsync<Season>();
            await connection.CreateTableAsync<Game>();
            await connection.CreateTableAsync<GameScoringSession>();
            await connection.CreateTableAsync<GameScoringLineupPlayer>();
            var team = new Team
            {
                Name = "Historical",
                Year = 2026,
                TeamLineageId = Guid.NewGuid(),
            };
            await connection.InsertAsync(team);
            var season = new Season { TeamId = team.Id, Name = "Summer", Year = team.Year };
            await connection.InsertAsync(season);
            var game = new Game
            {
                TeamId = team.Id,
                SeasonId = season.Id,
                ScheduledDate = Today.AddDays(-30),
            };
            await connection.InsertAsync(game);
            var session = new GameScoringSession
                {
                    GameId = game.Id,
                    Status = GameScoringStatus.Completed,
                    CurrentBatterPosition = 1,
                    CompletedAtUtc = Today.AddDays(-30).ToUniversalTime(),
                };
            await connection.InsertAsync(session);
            await connection.InsertAsync(
                new GameScoringLineupPlayer
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = 1,
                    BattingPosition = 1,
                }
            );
            await connection.ExecuteAsync("PRAGMA user_version = 2");
            return 0;
        });

        await db.Database.InitializeAsync();

        Assert.Equal(AppDatabase.CurrentSchemaVersion, await db.Database.GetSchemaVersionAsync());
        var checkpoint = Assert.Single(await GetCheckpointsAsync(db));
        Assert.Equal(
            SeasonBackupReminderCheckpointDisposition.HistoricalBaseline,
            checkpoint.Disposition
        );
        Assert.Empty(await CreateReminderRepository(db).GetEligibleAsync());
    }

    private static SeasonBackupReminderRepository CreateReminderRepository(
        TemporaryTestDatabase db
    ) =>
        new(
            db.Database,
            new DatabaseModeState(DatabaseMode.Production),
            new FixedTimeProvider(Today)
        );

    private static SeasonBackupReminderService CreateStartupService(
        TemporaryTestDatabase db,
        IBackupDestination destination
    )
    {
        var state = new DatabaseModeState(DatabaseMode.Production);
        var timeProvider = new FixedTimeProvider(Today);
        var repository = new SeasonBackupReminderRepository(db.Database, state, timeProvider);
        var package = new BackupPackageService(
            db.Database,
            new TestBackupPlatform(db.DirectoryPath),
            timeProvider
        );
        var manual = new ManualBackupService(
            package,
            destination,
            state,
            new ApplicationWorkflowCoordinator(),
            repository
        );
        return new SeasonBackupReminderService(repository, manual, state);
    }

    private static ManualBackupService CreateManualBackupService(
        TemporaryTestDatabase db,
        IBackupDestination destination
    )
    {
        var state = new DatabaseModeState(DatabaseMode.Production);
        var timeProvider = new FixedTimeProvider(Today);
        var repository = new SeasonBackupReminderRepository(db.Database, state, timeProvider);
        return new ManualBackupService(
            new BackupPackageService(
                db.Database,
                new TestBackupPlatform(db.DirectoryPath),
                timeProvider
            ),
            destination,
            state,
            new ApplicationWorkflowCoordinator(),
            repository
        );
    }

    private static async Task<Team> CreateTeamAsync(TemporaryTestDatabase db, string name)
    {
        var team = new Team { Name = name, Year = Today.Year };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<SeasonContext> CreateSeasonAsync(
        TemporaryTestDatabase db,
        string teamName,
        string seasonName,
        params (DateTime Date, bool Completed)[] games
    ) =>
        await CreateSeasonForTeamAsync(
            db,
            await CreateTeamAsync(db, teamName),
            seasonName,
            games
        );

    private static async Task<SeasonContext> CreateSeasonForTeamAsync(
        TemporaryTestDatabase db,
        Team team,
        string seasonName,
        params (DateTime Date, bool Completed)[] games
    )
    {
        var season = await db
            .CreateSeasonRepository()
            .GetOrCreateForTeamAsync(team.Id, seasonName, team.Year);
        Assert.NotNull(season);
        var createdGames = new List<Game>();
        foreach (var game in games)
            createdGames.Add(await AddGameAsync(db, team, season!, game.Date, game.Completed));
        return new SeasonContext(team, season!, createdGames);
    }

    private static async Task<Game> AddGameAsync(
        TemporaryTestDatabase db,
        Team team,
        Season season,
        DateTime date,
        bool completed
    )
    {
        var existingGameCount = (await db.CreateGameRepository().GetForTeamAsync(team.Id))
            .Count(game => game.SeasonId == season.Id);
        var result = await db.CreateGameRepository().CreateForTeamAsync(
            team.Id,
            season,
            [
                new Game
                {
                    ScheduledDate = date.Date,
                    ScheduledTime = TimeSpan.FromHours(18 + existingGameCount),
                },
            ]
        );
        var gameId = Assert.Single(result.CreatedGameIds);
        var game = Assert.Single(
            (await db.CreateGameRepository().GetForTeamAsync(team.Id)).Where(item =>
                item.Id == gameId
            )
        );
        if (completed)
            await CompleteAsync(db, game.Id);
        return game;
    }

    private static async Task CompleteAsync(TemporaryTestDatabase db, int gameId)
    {
        await db.UseConnectionAsync(connection =>
            connection.InsertAsync(
                new GameScoringSession
                {
                    GameId = gameId,
                    Status = GameScoringStatus.Completed,
                    CompletedAtUtc = Today.ToUniversalTime(),
                }
            )
        );
    }

    private static Task<List<SeasonBackupReminderCheckpoint>> GetCheckpointsAsync(
        TemporaryTestDatabase db
    ) =>
        db.UseConnectionAsync(connection =>
            connection.Table<SeasonBackupReminderCheckpoint>().ToListAsync()
        );

    private sealed record SeasonContext(Team Team, Season Season, IReadOnlyList<Game> Games);

    private sealed class FixedTimeProvider(DateTime localDate) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() =>
            new(localDate, TimeSpan.Zero);
    }

    private sealed record TestBackupPlatform(string CacheDirectory) : IBackupPlatform
    {
        public string AppVersion => "8.5-test";
    }

    private sealed class RecordingDestination(
        string directory,
        BackupDestinationResult result = BackupDestinationResult.Saved
    ) : IBackupDestination
    {
        public int CallCount { get; private set; }

        public Exception? Failure { get; init; }

        public async Task<BackupDestinationResult> SaveAsync(
            string sourceFilePath,
            string suggestedFileName,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            if (Failure is not null)
                throw Failure;
            if (result == BackupDestinationResult.Saved)
            {
                await using var source = File.OpenRead(sourceFilePath);
                await using var destination = File.Create(
                    Path.Combine(directory, $"saved-{Guid.NewGuid():N}.zip")
                );
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class BlockingDestination : IBackupDestination
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Continue { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<BackupDestinationResult> SaveAsync(
            string sourceFilePath,
            string suggestedFileName,
            CancellationToken cancellationToken = default
        )
        {
            Started.TrySetResult();
            await Continue.Task.WaitAsync(cancellationToken);
            return BackupDestinationResult.Saved;
        }
    }
}
