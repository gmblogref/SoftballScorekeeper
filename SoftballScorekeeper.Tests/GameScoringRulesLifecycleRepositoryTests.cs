using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringRulesLifecycleRepositoryTests
{
    [Theory]
    [InlineData(TeamGameSide.Home, 5, false)]
    [InlineData(TeamGameSide.Away, 12, true)]
    [InlineData(TeamGameSide.Home, 0, false)]
    public async Task Configured_start_persists_rules_and_is_idempotent(
        TeamGameSide side, int limit, bool oneUp)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();

        var first = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side, limit, oneUp);
        var repeated = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away, 99, !oneUp);

        Assert.True(first.IsSuccess);
        Assert.True(first.WasCreated);
        Assert.True(repeated.IsSuccess);
        Assert.False(repeated.WasCreated);
        var session = Assert.Single(await db.GetGameScoringSessionsAsync());
        Assert.Equal((side, limit, oneUp),
            (session.TeamSide, session.HomeRunLimit, session.IsOneUpEnabled));
        Assert.Equal(first.Session!.Id, repeated.Session!.Id);
        Assert.Equal(3, (await db.GetGameScoringLineupPlayersAsync()).Count);
    }

    [Fact]
    public async Task Negative_start_limit_is_rejected_without_creating_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);

        var result = await db.CreateGameScoringSessionRepository().InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home, -1, false);

        Assert.Equal(InitializeScoringSessionFailure.InvalidRequest, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
        Assert.Empty(await db.GetGameScoringLineupPlayersAsync());
    }

    [Fact]
    public async Task Game_rules_update_is_nonretroactive_and_invalid_change_preserves_configuration()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home, 5, false);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 7, 3, 2);
        var appearancesBefore = await db.GetGamePlateAppearancesAsync();

        var updated = await repository.UpdateGameRulesAsync(context.Game.Id, 3, true);
        var invalid = await repository.UpdateGameRulesAsync(context.Game.Id, -1, false);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(updated.IsSuccess);
        Assert.Equal(GameScoringLifecycleFailure.InvalidRequest, invalid.FailureReason);
        Assert.Equal((3, true, 7, 3), (state!.Session.HomeRunLimit,
            state.Session.IsOneUpEnabled, state.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(appearancesBefore.Select(item => (item.Id, item.Result)),
            state.PlateAppearances.Select(item => (item.Id, item.Result)));
        Assert.Equal(1, state.HomeHomeRuns);
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 2, 4)]
    [InlineData(TeamGameSide.Away, 4, 2)]
    public async Task Hr_totals_derive_from_committed_history_and_map_by_team_side(
        TeamGameSide side, int expectedHome, int expectedVisitor)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side, 1, false);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        await db.UseConnectionAsync(async connection => await connection.InsertAsync(new GameScoringHalfInning
        {
            GameScoringSessionId = initialized.Session!.Id,
            Inning = 1,
            TeamSummaryAcknowledged = true,
            OpponentHomeRuns = 4,
            OpponentEntryCompleted = true,
        }));

        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal((expectedHome, expectedVisitor),
            (state!.HomeHomeRuns, state.VisitorHomeRuns));
        Assert.Equal(2, state.PlateAppearances.Count(item => item.Result == BatterResult.HomeRun));
        Assert.DoesNotContain(state.PlateAppearances, item => item.Result == BatterResult.Single);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(4, 1, 2)]
    public async Task Upcoming_batters_use_live_sequence_with_safe_wraparound(
        int playerCount, int expectedOnDeckPosition, int expectedHolePosition)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, playerCount);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 0, 0, playerCount);

        var state = await repository.GetStateForGameAsync(context.Game.Id);
        var upcoming = state!.GetUpcomingPlayerIds();

        Assert.NotNull(upcoming);
        Assert.Equal(state.BattingSequence.Single(item =>
            item.BattingPosition == expectedOnDeckPosition).PlayerId,
            upcoming.Value.OnDeckPlayerId);
        Assert.Equal(state.BattingSequence.Single(item =>
            item.BattingPosition == expectedHolePosition).PlayerId,
            upcoming.Value.InTheHolePlayerId);
    }

    [Fact]
    public async Task Game_over_blocks_pending_work_then_preserves_committed_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Away, 8, true);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Double)).IsSuccess);

        var blocked = await repository.CompleteGameAsync(context.Game.Id);
        Assert.Equal(GameScoringLifecycleFailure.UnresolvedScoringState,
            blocked.FailureReason);
        Assert.Equal(GameScoringStatus.InProgress,
            (await repository.GetForGameAsync(context.Game.Id))!.Status);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var completed = await repository.CompleteGameAsync(context.Game.Id);
        var repeated = await repository.CompleteGameAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(GameScoringStatus.Completed, state!.Session.Status);
        Assert.Equal(2, state.PlateAppearances.Count);
        Assert.Equal(1, state.VisitorHomeRuns);
        Assert.Equal((8, true),
            (state.Session.HomeRunLimit, state.Session.IsOneUpEnabled));
    }

    [Fact]
    public async Task Game_over_persistence_failure_leaves_session_in_progress()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);
        await db.ExecuteAsync(
            "CREATE TRIGGER FailGameOver BEFORE UPDATE OF Status ON GameScoringSession BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var result = await repository.CompleteGameAsync(context.Game.Id);

        Assert.Equal(GameScoringLifecycleFailure.Persistence, result.FailureReason);
        Assert.Equal(GameScoringStatus.InProgress,
            (await repository.GetForGameAsync(context.Game.Id))!.Status);
    }

    [Fact]
    public async Task Reset_removes_all_scoring_owned_state_and_preserves_game_and_frozen_lineup()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home, 7, true);
        await SeedEveryScoringOwnedStateAsync(db, initialized.Session!);
        var gameBefore = Assert.Single(await db.GetGamesAsync());
        var frozenBefore = (await db.GetGameLineupPlayersAsync())
            .Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)).ToArray();

        var result = await repository.ResetGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
        Assert.Empty(await db.GetGameScoringLineupPlayersAsync());
        Assert.Empty(await db.GetGamePlateAppearancesAsync());
        Assert.Empty(await db.GetPlateAppearanceRunnerMovementsAsync());
        Assert.Empty(await db.GetGameScoringBaseRunnersAsync());
        Assert.Empty(await db.GetPendingScoringPlaysAsync());
        Assert.Empty(await db.GetPendingRunnerMovementsAsync());
        Assert.Empty(await db.GetGameScoringHalfInningsAsync());
        var gameAfter = Assert.Single(await db.GetGamesAsync());
        Assert.Equal((gameBefore.Id, gameBefore.TeamId, gameBefore.SeasonId,
            gameBefore.ScheduledDate, gameBefore.ScheduledTime, gameBefore.Opponent, gameBefore.Field),
            (gameAfter.Id, gameAfter.TeamId, gameAfter.SeasonId,
            gameAfter.ScheduledDate, gameAfter.ScheduledTime, gameAfter.Opponent, gameAfter.Field));
        Assert.Equal(frozenBefore, (await db.GetGameLineupPlayersAsync())
            .Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
    }

    [Fact]
    public async Task Reset_failure_rolls_back_all_deletions_and_completed_game_is_rejected()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedEveryScoringOwnedStateAsync(db, initialized.Session!);
        await db.ExecuteAsync(
            "CREATE TRIGGER FailScoringReset BEFORE DELETE ON GameScoringSession BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var failed = await repository.ResetGameAsync(context.Game.Id);

        Assert.Equal(GameScoringLifecycleFailure.Persistence, failed.FailureReason);
        Assert.Single(await db.GetGameScoringSessionsAsync());
        Assert.Single(await db.GetGamePlateAppearancesAsync());
        Assert.Single(await db.GetPlateAppearanceRunnerMovementsAsync());
        Assert.Single(await db.GetPendingScoringPlaysAsync());
        Assert.Single(await db.GetPendingRunnerMovementsAsync());
        await db.ExecuteAsync("DROP TRIGGER FailScoringReset");
        await db.ExecuteAsync("UPDATE GameScoringSession SET Status = ? WHERE Id = ?",
            GameScoringStatus.Completed, initialized.Session!.Id);
        Assert.Equal(GameScoringLifecycleFailure.GameCompleted,
            (await repository.ResetGameAsync(context.Game.Id)).FailureReason);
    }

    [Fact]
    public async Task Legacy_session_gets_default_rules_without_recreation_and_upgrade_is_idempotent()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        var legacy = await SeedLegacySessionAsync(db);

        await db.Database.InitializeAsync();
        var first = await db.CreateGameScoringSessionRepository().GetForGameAsync(legacy.GameId);
        await db.Database.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        var second = await new GameScoringSessionRepository(reopened)
            .GetForGameAsync(legacy.GameId);

        Assert.Equal(legacy.SessionId, first!.Id);
        Assert.Equal((5, false, 9, 4, 2, 1),
            (first.HomeRunLimit, first.IsOneUpEnabled, first.HomeScore,
                first.VisitorScore, first.CurrentInning, first.CurrentOuts));
        Assert.Equal((first.Id, first.HomeRunLimit, first.IsOneUpEnabled),
            (second!.Id, second.HomeRunLimit, second.IsOneUpEnabled));
        await reopened.CloseAsync();
    }

    private static async Task SeedEveryScoringOwnedStateAsync(
        TemporaryTestDatabase db, GameScoringSession session)
    {
        var live = await db.GetGameScoringLineupPlayersAsync();
        await db.UseConnectionAsync(async connection =>
        {
            var appearance = new GamePlateAppearance
            {
                GameScoringSessionId = session.Id, PlayerId = live[0].PlayerId,
                BattingPosition = 1, PlateAppearanceNumber = 1, Inning = 1,
                Result = BatterResult.Single,
            };
            await connection.InsertAsync(appearance);
            await connection.InsertAsync(new GamePlateAppearanceRunnerMovement
            {
                GamePlateAppearanceId = appearance.Id, PlayerId = live[0].PlayerId,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = RunnerDestination.First,
            });
            await connection.InsertAsync(new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id, PlayerId = live[0].PlayerId,
                Base = ScoringBase.First,
            });
            var pending = new GameScoringPendingPlay
            {
                GameScoringSessionId = session.Id, BatterPlayerId = live[1].PlayerId,
                BatterPosition = 2, Result = BatterResult.Double,
            };
            await connection.InsertAsync(pending);
            await connection.InsertAsync(new GameScoringPendingRunnerMovement
            {
                GameScoringPendingPlayId = pending.Id, PlayerId = live[1].PlayerId,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = RunnerDestination.Second,
            });
            await connection.InsertAsync(new GameScoringHalfInning
            {
                GameScoringSessionId = session.Id, Inning = 1,
                TeamRuns = 2, TeamHomeRuns = 1, TeamSummaryAcknowledged = true,
                OpponentRuns = 3, OpponentHomeRuns = 1, OpponentEntryCompleted = true,
            });
            return 0;
        });
    }

    private static async Task<(Team Team, Game Game)> CreateReadyGameAsync(
        TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Season", Year = 2026 };
        await db.SeedSeasonAsync(season);
        var game = new Game
        {
            TeamId = team.Id, SeasonId = season.Id, ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18), Opponent = "Opponent", Field = "Field 1",
        };
        await db.SeedGameAsync(game);
        for (var index = 0; index < playerCount; index++)
        {
            var player = new Player
            {
                FirstName = $"Player{index + 1}", LastName = "Test", IsActive = true,
            };
            await db.CreatePlayerRepository().CreateAsync(player);
            await db.SeedTeamPlayerAsync(new()
            {
                TeamId = team.Id, PlayerId = player.Id, IsActive = true,
            });
            await db.SeedGameLineupPlayerAsync(new()
            {
                GameId = game.Id, PlayerId = player.Id, BattingOrder = index + 1,
            });
        }
        return (team, game);
    }

    private static async Task<(int SessionId, int GameId)> SeedLegacySessionAsync(
        TemporaryTestDatabase db) => await db.UseConnectionAsync(async connection =>
    {
        await connection.CreateTableAsync<Team>();
        await connection.CreateTableAsync<Player>();
        await connection.CreateTableAsync<Season>();
        await connection.CreateTableAsync<Game>();
        await connection.CreateTableAsync<GameLineupPlayer>();
        await connection.CreateTableAsync<LegacyScoringSession>();
        await connection.CreateTableAsync<GameScoringLineupPlayer>();
        var team = new Team { Name = "Legacy", IsActive = true };
        await connection.InsertAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Legacy", Year = 2026 };
        await connection.InsertAsync(season);
        var game = new Game
        {
            TeamId = team.Id, SeasonId = season.Id, ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18),
        };
        await connection.InsertAsync(game);
        var player = new Player { FirstName = "Legacy", IsActive = true };
        await connection.InsertAsync(player);
        await connection.InsertAsync(new GameLineupPlayer
        {
            GameId = game.Id, PlayerId = player.Id, BattingOrder = 1,
        });
        var session = new LegacyScoringSession
        {
            GameId = game.Id, Status = GameScoringStatus.InProgress,
            TeamSide = TeamGameSide.Home, HomeScore = 9, VisitorScore = 4,
            CurrentBatterPosition = 1, CurrentInning = 2, CurrentOuts = 1,
        };
        await connection.InsertAsync(session);
        await connection.InsertAsync(new GameScoringLineupPlayer
        {
            GameScoringSessionId = session.Id,
            PlayerId = player.Id,
            BattingPosition = 1,
        });
        return (session.Id, game.Id);
    });

    [SQLite.Table("GameScoringSession")]
    private sealed class LegacyScoringSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        [SQLite.Indexed("UX_GameScoringSession_Game", 1, Unique = true)]
        public int GameId { get; set; }
        public GameScoringStatus Status { get; set; }
        public TeamGameSide TeamSide { get; set; }
        public int HomeScore { get; set; }
        public int VisitorScore { get; set; }
        public int CurrentBatterPosition { get; set; }
        public int CurrentInning { get; set; }
        public int CurrentOuts { get; set; }
    }
}
