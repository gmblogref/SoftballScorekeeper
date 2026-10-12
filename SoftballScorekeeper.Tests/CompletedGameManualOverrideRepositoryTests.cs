using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Infrastructure.Backup;
using SQLite;

namespace SoftballScorekeeper.Tests;

public sealed class CompletedGameManualOverrideRepositoryTests
{
    [Fact]
    public async Task Atomic_save_drives_effective_stats_score_team_aggregate_and_record_without_rewriting_plays()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var before = await db.GetGamePlateAppearancesAsync();

        var result = await db.CreateCompletedGameManualOverrideRepository().SaveAsync(
            context.Game.Id,
            [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 1)],
            visitorScore: 2,
            homeScore: 4);

        Assert.True(result.IsSuccess);
        Assert.Single(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Single(await db.GetCompletedGameScoreOverridesAsync());
        var completed = (await db.CreateGameScoringSessionRepository()
            .GetCompletedGameAsync(context.Game.Id))!;
        var line = Assert.Single(completed.Players).BattingStatistics;
        Assert.Equal((5, 4, 2, 1, 1, 4),
            (line.PlateAppearances, line.AtBats, line.Hits, line.Singles,
                line.HomeRuns, completed.HomeScore));
        Assert.Equal(2, completed.VisitorScore);
        Assert.True(completed.HasManualOverrides);

        var teamLine = Assert.Single((await db.CreateTeamBattingStatisticsRepository()
            .GetAsync(context.Team.Id))!.Players).BattingStatistics;
        Assert.Equal((5, 4, 2, 1), (teamLine.PlateAppearances,
            teamLine.AtBats, teamLine.Hits, teamLine.Walks));
        Assert.Equal(0.6m, teamLine.OnBasePercentage);
        var record = (await db.CreateTeamRecordRepository()
            .GetForTeamsAsync([context.Team.Id]))[context.Team.Id];
        Assert.Equal((1, 1, 0, 0),
            (record.GamesPlayed, record.Wins, record.Losses, record.Ties));
        Assert.Equal(before.Select(item => (item.Id, item.Result)),
            (await db.GetGamePlateAppearancesAsync()).Select(item => (item.Id, item.Result)));
        var canonicalSession = Assert.Single(await db.GetGameScoringSessionsAsync());
        Assert.Equal((5, 3), (canonicalSession.VisitorScore, canonicalSession.HomeScore));
    }

    [Fact]
    public async Task Invalid_save_is_atomic_and_preserves_previous_overrides()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var repository = db.CreateCompletedGameManualOverrideRepository();
        Assert.True((await repository.SaveAsync(context.Game.Id,
            [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 0)], 2, 4)).IsSuccess);

        var failed = await repository.SaveAsync(context.Game.Id,
            [new(context.Player.Id, int.MaxValue, 1, 0, 1, 0, 0, 0, 0)], 9, 9);

        Assert.Equal(CompletedGameManualOverrideFailure.InvalidBattingStatistics,
            failed.FailureReason);
        var batting = Assert.Single(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Equal((1, 1, 1), (batting.Singles, batting.HomeRuns, batting.Walks));
        var score = Assert.Single(await db.GetCompletedGameScoreOverridesAsync());
        Assert.Equal((2, 4), (score.VisitorScore, score.HomeScore));
    }

    [Fact]
    public async Task Canonical_values_remove_rows_and_revert_clears_all_overrides()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var repository = db.CreateCompletedGameManualOverrideRepository();
        Assert.True((await repository.SaveAsync(context.Game.Id,
            [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 0)], 2, 4)).IsSuccess);

        var reverted = await repository.RevertAsync(context.Game.Id);

        Assert.True(reverted.IsSuccess);
        Assert.Empty(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Empty(await db.GetCompletedGameScoreOverridesAsync());
        var completed = (await db.CreateGameScoringSessionRepository()
            .GetCompletedGameAsync(context.Game.Id))!;
        Assert.False(completed.HasManualOverrides);
        Assert.Equal((5, 3), (completed.VisitorScore, completed.HomeScore));
        var line = Assert.Single(completed.Players).BattingStatistics;
        Assert.Equal((4, 3, 1, 1),
            (line.PlateAppearances, line.AtBats, line.Hits, line.Walks));
    }

    [Fact]
    public async Task Finalized_team_year_rejects_save_and_revert_at_transaction_boundary()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var repository = db.CreateCompletedGameManualOverrideRepository();
        Assert.True((await repository.SaveAsync(context.Game.Id,
            [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 0)], 2, 4)).IsSuccess);
        await db.ExecuteAsync("UPDATE Team SET IsYearOver = 1 WHERE Id = ?", context.Team.Id);

        var save = await repository.SaveAsync(context.Game.Id,
            [new(context.Player.Id, 0, 1, 0, 1, 1, 0, 0, 0)], 1, 5);
        var revert = await repository.RevertAsync(context.Game.Id);

        Assert.Equal(CompletedGameManualOverrideFailure.TeamYearFinalized, save.FailureReason);
        Assert.Equal(CompletedGameManualOverrideFailure.TeamYearFinalized, revert.FailureReason);
        Assert.Single(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Single(await db.GetCompletedGameScoreOverridesAsync());
    }

    [Fact]
    public async Task Overrides_survive_database_close_and_repository_recreation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        Assert.True((await db.CreateCompletedGameManualOverrideRepository().SaveAsync(
            context.Game.Id, [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 0)], 2, 4)).IsSuccess);
        await db.Database.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        try
        {
            var completed = (await new GameScoringSessionRepository(reopened)
                .GetCompletedGameAsync(context.Game.Id))!;
            Assert.True(completed.HasManualOverrides);
            Assert.Equal((2, 4), (completed.VisitorScore, completed.HomeScore));
            Assert.Equal(2, Assert.Single(completed.Players).BattingStatistics.Hits);
        }
        finally { await reopened.CloseAsync(); }
    }

    [Fact]
    public async Task Save_leaves_low_man_history_unchanged_and_deliberate_recalculation_uses_effective_stats()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var second = new Player { FirstName = "Riley", LastName = "Rates", IsActive = true };
        await db.UseConnectionAsync(connection => connection.InsertAsync(second));
        var session = Assert.Single(await db.GetGameScoringSessionsAsync());
        await db.UseConnectionAsync(connection => connection.InsertAsync(new GameScoringLineupPlayer
        {
            GameScoringSessionId = session.Id, PlayerId = second.Id, BattingPosition = 2,
        }));
        var secondResults = new[]
        {
            BatterResult.Single, BatterResult.Single, BatterResult.Single,
            BatterResult.Out, BatterResult.Out,
        };
        for (var index = 0; index < secondResults.Length; index++)
            await db.UseConnectionAsync(connection => connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = session.Id, PlayerId = second.Id, BattingPosition = 2,
                PlateAppearanceNumber = index + 5, Inning = index + 1,
                Result = secondResults[index],
            }));

        var lowMan = db.CreateLowManCalculationRepository();
        var initial = await lowMan.CalculateAsync(context.Team.Id, context.Game.ScheduledDate);
        Assert.Equal([context.Player.Id], initial.PlayerIds);

        var saved = await db.CreateCompletedGameManualOverrideRepository().SaveAsync(
            context.Game.Id,
            [
                new(context.Player.Id, 0, 0, 0, 1, 2, 0, 0, 0),
                new(second.Id, 3, 0, 0, 0, 0, 0, 0, 2),
            ], 5, 3);

        Assert.True(saved.IsSuccess);
        Assert.Equal([context.Player.Id], (await lowMan.GetPersistedAutomaticWinnersAsync(
            context.Team.Id, context.Game.ScheduledDate))!.Select(item => item.PlayerId));
        var recalculated = await lowMan.CalculateAsync(context.Team.Id, context.Game.ScheduledDate);
        Assert.Equal([second.Id], recalculated.PlayerIds);
    }

    [Fact]
    public async Task Low_man_recalculation_accepts_dynamic_pa_for_lineup_player_without_canonical_pa()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        var added = new Player
        {
            FirstName = "Morgan",
            LastName = "Missing PA",
            IsActive = true,
        };
        await db.UseConnectionAsync(connection => connection.InsertAsync(added));
        var session = Assert.Single(await db.GetGameScoringSessionsAsync());
        await db.UseConnectionAsync(connection => connection.InsertAsync(
            new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id,
                PlayerId = added.Id,
                BattingPosition = 2,
            }));
        var canonicalPlays = (await db.GetGamePlateAppearancesAsync())
            .Select(item => (item.Id, item.PlayerId, item.Result))
            .ToArray();
        var lowMan = db.CreateLowManCalculationRepository();

        var initial = await lowMan.CalculateAsync(
            context.Team.Id, context.Game.ScheduledDate);
        Assert.Equal([context.Player.Id], initial.PlayerIds);
        var initialEntry = Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
                && item.RuleType == BeerRuleType.LowMan));

        var saved = await db.CreateCompletedGameManualOverrideRepository().SaveAsync(
            context.Game.Id,
            [
                new(context.Player.Id, 0, 0, 0, 1, 1, 0, 0, 1),
                new(added.Id, 1, 0, 0, 0, 0, 0, 0, 1),
            ],
            session.VisitorScore,
            session.HomeScore);

        Assert.True(saved.IsSuccess);
        Assert.Equal(2, Assert.Single((await db.CreateGameScoringSessionRepository()
            .GetCompletedGameAsync(context.Game.Id))!.Players
            .Where(item => item.PlayerId == added.Id)).BattingStatistics.PlateAppearances);
        Assert.Equal(initialEntry.Id, Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(
            item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
                && item.RuleType == BeerRuleType.LowMan)).Id);
        Assert.Equal(canonicalPlays, (await db.GetGamePlateAppearancesAsync())
            .Select(item => (item.Id, item.PlayerId, item.Result)));

        var recalculated = await lowMan.CalculateAsync(
            context.Team.Id, context.Game.ScheduledDate);

        Assert.True(recalculated.IsSuccess);
        Assert.Equal([context.Player.Id, added.Id], recalculated.PlayerIds.Order());
        Assert.Equal(canonicalPlays, (await db.GetGamePlateAppearancesAsync())
            .Select(item => (item.Id, item.PlayerId, item.Result)));

        Assert.True((await db.CreateCompletedGameManualOverrideRepository()
            .RevertAsync(context.Game.Id)).IsSuccess);
        var afterRevert = await lowMan.CalculateAsync(
            context.Team.Id, context.Game.ScheduledDate);
        Assert.True(afterRevert.IsSuccess);
        Assert.Equal([context.Player.Id], afterRevert.PlayerIds);
        Assert.Equal(canonicalPlays, (await db.GetGamePlateAppearancesAsync())
            .Select(item => (item.Id, item.PlayerId, item.Result)));
    }

    [Fact]
    public async Task Schema_four_migrates_additively_to_six()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        var raw = new SQLiteAsyncConnection(db.DatabasePath);
        await raw.ExecuteAsync("PRAGMA user_version = 4");
        await raw.CloseAsync();

        await db.Database.InitializeAsync();

        Assert.Equal(6, await db.Database.GetSchemaVersionAsync());
        Assert.Empty(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Empty(await db.GetCompletedGameScoreOverridesAsync());
    }

    [Fact]
    public async Task Schema_five_override_migrates_its_fixed_pa_remainder_to_ordinary_outs()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        var raw = new SQLiteAsyncConnection(db.DatabasePath);
        await raw.CreateTableAsync<GameScoringSession>();
        await raw.CreateTableAsync<GameScoringLineupPlayer>();
        await raw.CreateTableAsync<GamePlateAppearance>();
        await raw.ExecuteAsync(
            """
            CREATE TABLE CompletedGameBattingOverride (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GameId INTEGER NOT NULL,
                PlayerId INTEGER NOT NULL,
                Singles INTEGER NOT NULL,
                Doubles INTEGER NOT NULL,
                Triples INTEGER NOT NULL,
                HomeRuns INTEGER NOT NULL,
                Walks INTEGER NOT NULL,
                FielderChoices INTEGER NOT NULL,
                SacrificeFlies INTEGER NOT NULL)
            """);
        var legacySession = new GameScoringSession
        {
            GameId = 17,
            Status = GameScoringStatus.Completed,
        };
        await raw.InsertAsync(legacySession);
        await raw.InsertAsync(new GameScoringLineupPlayer
        {
            GameScoringSessionId = legacySession.Id,
            PlayerId = 23,
            BattingPosition = 1,
        });
        foreach (var (number, result) in new[]
        {
            (1, BatterResult.Out),
            (2, BatterResult.DoublePlay),
            (3, BatterResult.InTheParkHomeRun),
            (4, BatterResult.Walk),
        })
            await raw.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = legacySession.Id,
                PlayerId = 23,
                BattingPosition = 1,
                PlateAppearanceNumber = number,
                Inning = 1,
                Result = result,
            });
        await raw.ExecuteAsync(
            """
            INSERT INTO CompletedGameBattingOverride
                (GameId, PlayerId, Singles, Doubles, Triples, HomeRuns, Walks,
                 FielderChoices, SacrificeFlies)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            17, 23, 0, 1, 0, 1, 0, 0, 0);
        await raw.ExecuteAsync("PRAGMA user_version = 5");
        await raw.CloseAsync();

        await db.Database.InitializeAsync();

        Assert.Equal(6, await db.Database.GetSchemaVersionAsync());
        var migrated = Assert.Single(await db.GetCompletedGameBattingOverridesAsync());
        Assert.Equal(1, migrated.Outs);
    }

    [Fact]
    public async Task Whole_database_backup_and_restore_preserve_effective_overrides_and_canonical_plays()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await SeedCompletedGameAsync(db);
        Assert.True((await db.CreateCompletedGameManualOverrideRepository().SaveAsync(
            context.Game.Id, [new(context.Player.Id, 1, 0, 0, 1, 1, 0, 0, 0)], 2, 4)).IsSuccess);
        var packageService = new BackupPackageService(
            db.Database, new TestBackupPlatform(db.DirectoryPath, "story-14-test"), TimeProvider.System);
        await using var artifact = await packageService.CreateAsync();
        var preparation = await packageService.PrepareRestoreAsync(artifact.FilePath);
        Assert.True(preparation.Inspection.IsValid);
        await using var prepared = preparation.Artifact!;

        var targetPath = Path.Combine(db.DirectoryPath, "restore-target.db3");
        var target = new AppDatabase(targetPath);
        try
        {
            await target.InitializeAsync();
            await new DatabaseRestoreService(target).RestoreAsync(prepared.DatabasePath);
            var completed = (await new GameScoringSessionRepository(target)
                .GetCompletedGameAsync(context.Game.Id))!;
            Assert.True(completed.HasManualOverrides);
            Assert.Equal((2, 4, 2), (completed.VisitorScore, completed.HomeScore,
                Assert.Single(completed.Players).BattingStatistics.Hits));
            await using var operation = await target.BeginOperationAsync();
            var plays = await operation.Connection.Table<GamePlateAppearance>()
                .OrderBy(item => item.PlateAppearanceNumber).ToListAsync();
            Assert.Equal(new[] { BatterResult.Out, BatterResult.DoublePlay,
                BatterResult.InTheParkHomeRun, BatterResult.Walk },
                plays.Select(item => item.Result!.Value));
        }
        finally { await target.CloseAsync(); }
    }

    private static async Task<Context> SeedCompletedGameAsync(TemporaryTestDatabase db)
    {
        var team = new Team
        {
            Name = "Override Team", Year = 2026, TeamLineageId = Guid.NewGuid(), IsActive = true,
        };
        await db.UseConnectionAsync(connection => connection.InsertAsync(team));
        var season = new Season { TeamId = team.Id, Name = "Summer", Year = team.Year };
        await db.SeedSeasonAsync(season);
        var game = new Game
        {
            TeamId = team.Id, SeasonId = season.Id, ScheduledDate = new(2026, 7, 1),
            ScheduledTime = new(18, 30, 0), Opponent = "Visitors",
        };
        await db.SeedGameAsync(game);
        var player = new Player { FirstName = "Casey", LastName = "Stats", IsActive = true };
        await db.UseConnectionAsync(connection => connection.InsertAsync(player));
        var session = new GameScoringSession
        {
            GameId = game.Id, Status = GameScoringStatus.Completed,
            TeamSide = TeamGameSide.Home, VisitorScore = 5, HomeScore = 3,
            CurrentBatterPosition = 1, CurrentInning = 7, CompletedAtUtc = DateTime.UtcNow,
        };
        await db.UseConnectionAsync(connection => connection.InsertAsync(session));
        await db.UseConnectionAsync(connection => connection.InsertAsync(
            new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id, PlayerId = player.Id, BattingPosition = 1,
            }));
        var results = new[]
        {
            BatterResult.Out, BatterResult.DoublePlay,
            BatterResult.InTheParkHomeRun, BatterResult.Walk,
        };
        for (var index = 0; index < results.Length; index++)
            await db.UseConnectionAsync(connection => connection.InsertAsync(
                new GamePlateAppearance
                {
                    GameScoringSessionId = session.Id, PlayerId = player.Id,
                    BattingPosition = 1, PlateAppearanceNumber = index + 1,
                    Inning = index + 1, Result = results[index],
                }));
        return new(team, game, player);
    }

    private sealed record Context(Team Team, Game Game, Player Player);

    private sealed record TestBackupPlatform(string CacheDirectory, string AppVersion)
        : IBackupPlatform;
}
