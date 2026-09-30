using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringRulesLifecycleRepositoryTests
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
            context.Team.Id, context.Game.Id, side, 2, false);
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
        Assert.False(state.IsHomeRunLegal());
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
    public async Task Reload_restores_complex_completed_and_pending_scoring_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        Assert.True((await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home, 7, true)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        await CompleteOffensiveHalfAsync(repository, context.Game.Id, 2, 1);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Double)).IsSuccess);
        var before = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.NotNull(before);
        Assert.Equal((2, 1, 1, 2, 7, true),
            (before.Session.CurrentInning, before.Session.CurrentOuts,
                before.Session.HomeScore, before.Session.VisitorScore,
                before.Session.HomeRunLimit, before.Session.IsOneUpEnabled));
        Assert.Single(before.BaseRunners);
        Assert.NotNull(before.PendingPlay);
        Assert.NotEmpty(before.PlateAppearances);
        Assert.NotEmpty(before.RunnerMovements);
        Assert.Single(before.HalfInnings);

        await db.Database.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        var resumed = await new GameScoringSessionRepository(reopened)
            .GetStateForGameAsync(context.Game.Id);

        AssertEquivalentScoringState(before, Assert.IsType<GameScoringState>(resumed));
        await reopened.CloseAsync();
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 1, 2, 1, 3)]
    [InlineData(TeamGameSide.Away, 2, 1, 3, 1)]
    public async Task Game_over_preserves_complete_scoring_history_and_role_mapping(
        TeamGameSide side, int expectedHomeScore, int expectedVisitorScore,
        int expectedHomeRuns, int expectedVisitorHomeRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        Assert.True((await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side, 6, true)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        await CompleteOffensiveHalfAsync(repository, context.Game.Id, 2, 3);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var before = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(context.Game.Id));

        var completed = await repository.CompleteGameAsync(context.Game.Id);
        var after = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(context.Game.Id));
        var restart = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side, 99, false);

        Assert.True(completed.IsSuccess);
        Assert.Equal(GameScoringStatus.Completed, after.Session.Status);
        Assert.Equal((expectedHomeScore, expectedVisitorScore,
                expectedHomeRuns, expectedVisitorHomeRuns),
            (after.Session.HomeScore, after.Session.VisitorScore,
                after.HomeHomeRuns, after.VisitorHomeRuns));
        Assert.Equal((6, true),
            (after.Session.HomeRunLimit, after.Session.IsOneUpEnabled));
        AssertHistoryEqual(before, after);
        Assert.Equal(InitializeScoringSessionFailure.GameCompleted, restart.FailureReason);
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
    public async Task Reset_returns_game_to_fresh_scoring_and_does_not_affect_another_game()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, 3);
        var second = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var firstStart = await repository.InitializeAsync(
            first.Team.Id, first.Game.Id, TeamGameSide.Home, 7, true);
        Assert.True(firstStart.IsSuccess);
        Assert.True((await repository.InitializeAsync(
            second.Team.Id, second.Game.Id, TeamGameSide.Away, 4, false)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(first.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(second.Game.Id)).IsSuccess);
        var firstGameBefore = GameValue(first.Game);
        var firstLineupBefore = (await db.GetGameLineupPlayersAsync())
            .Where(item => item.GameId == first.Game.Id)
            .Select(FrozenLineupValue).ToArray();
        var secondBefore = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(second.Game.Id));

        var reset = await repository.ResetGameAsync(first.Game.Id);
        var secondAfterReset = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(second.Game.Id));

        Assert.True(reset.IsSuccess);
        Assert.Null(await repository.GetForGameAsync(first.Game.Id));
        Assert.Null(await repository.GetStateForGameAsync(first.Game.Id));
        Assert.Equal(firstGameBefore, GameValue((await db.GetGamesAsync())
            .Single(item => item.Id == first.Game.Id)));
        Assert.Equal(firstLineupBefore, (await db.GetGameLineupPlayersAsync())
            .Where(item => item.GameId == first.Game.Id)
            .Select(FrozenLineupValue));
        AssertEquivalentScoringState(secondBefore, secondAfterReset);

        var restarted = await repository.InitializeAsync(
            first.Team.Id, first.Game.Id, TeamGameSide.Away, 9, false);
        var fresh = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(first.Game.Id));
        var secondAfterRestart = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(second.Game.Id));

        Assert.True(restarted.IsSuccess);
        Assert.True(restarted.WasCreated);
        Assert.NotEqual(firstStart.Session!.Id, restarted.Session!.Id);
        Assert.Equal((TeamGameSide.Away, 9, false, 0, 0, 1, 0, 1),
            (fresh.Session.TeamSide, fresh.Session.HomeRunLimit,
                fresh.Session.IsOneUpEnabled, fresh.Session.HomeScore,
                fresh.Session.VisitorScore, fresh.Session.CurrentInning,
                fresh.Session.CurrentOuts, fresh.Session.CurrentBatterPosition));
        Assert.Empty(fresh.PlateAppearances);
        Assert.Empty(fresh.RunnerMovements);
        Assert.Empty(fresh.BaseRunners);
        Assert.Empty(fresh.HalfInnings);
        Assert.Null(fresh.PendingPlay);
        Assert.Equal(firstLineupBefore.Select(item => (item.PlayerId, item.Order)),
            fresh.BattingSequence.Select(item => (item.PlayerId, item.BattingPosition)));
        AssertEquivalentScoringState(secondBefore, secondAfterRestart);
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

    private static async Task CompleteOffensiveHalfAsync(
        GameScoringSessionRepository repository, int gameId,
        int opponentRuns, int opponentHomeRuns)
    {
        for (var outNumber = 0; outNumber < 3; outNumber++)
            Assert.True((await repository.SelectOutAsync(gameId)).IsSuccess);
        var halfInning = Assert.IsType<GameScoringHalfInning>(
            await repository.GetPendingHalfInningTransitionAsync(gameId));
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            gameId, halfInning.Id));
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            gameId, halfInning.Id, opponentRuns, OpponentEntryMode.ThisHalf,
            opponentHomeRuns, OpponentEntryMode.ThisHalf)).IsSuccess);
    }

    private static void AssertEquivalentScoringState(
        GameScoringState expected, GameScoringState actual)
    {
        Assert.Equal(SessionValue(expected.Session), SessionValue(actual.Session));
        AssertHistoryEqual(expected, actual);
        Assert.Equal(expected.PlayersById.OrderBy(item => item.Key).Select(item =>
                (item.Key, item.Value.FirstName, item.Value.LastName, item.Value.IsActive)),
            actual.PlayersById.OrderBy(item => item.Key).Select(item =>
                (item.Key, item.Value.FirstName, item.Value.LastName, item.Value.IsActive)));
        Assert.Equal(PendingPlayValue(expected.PendingPlay),
            PendingPlayValue(actual.PendingPlay));
        Assert.Equal(expected.PendingPlay?.Movements.Select(item =>
                (item.Id, item.GameScoringPendingPlayId, item.PlayerId,
                    item.StartingLocation, item.Destination)),
            actual.PendingPlay?.Movements.Select(item =>
                (item.Id, item.GameScoringPendingPlayId, item.PlayerId,
                    item.StartingLocation, item.Destination)));
    }

    private static void AssertHistoryEqual(GameScoringState expected, GameScoringState actual)
    {
        Assert.Equal(expected.BattingSequence.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition)),
            actual.BattingSequence.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition)));
        Assert.Equal(expected.PlateAppearances.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition,
                    item.PlateAppearanceNumber, item.Inning, item.Result)),
            actual.PlateAppearances.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition,
                    item.PlateAppearanceNumber, item.Inning, item.Result)));
        Assert.Equal(expected.RunnerMovements.Select(item =>
                (item.Id, item.GamePlateAppearanceId, item.PlayerId,
                    item.StartingLocation, item.Destination, item.RunCounts)),
            actual.RunnerMovements.Select(item =>
                (item.Id, item.GamePlateAppearanceId, item.PlayerId,
                    item.StartingLocation, item.Destination, item.RunCounts)));
        Assert.Equal(expected.BaseRunners.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.Base)),
            actual.BaseRunners.Select(item =>
                (item.Id, item.GameScoringSessionId, item.PlayerId, item.Base)));
        Assert.Equal(expected.HalfInnings.Select(item =>
                (item.Id, item.GameScoringSessionId, item.Inning, item.TeamRuns,
                    item.TeamHomeRuns, item.TeamSummaryAcknowledged, item.OpponentRuns,
                    item.OpponentHomeRuns, item.OpponentEntryCompleted)),
            actual.HalfInnings.Select(item =>
                (item.Id, item.GameScoringSessionId, item.Inning, item.TeamRuns,
                    item.TeamHomeRuns, item.TeamSummaryAcknowledged, item.OpponentRuns,
                    item.OpponentHomeRuns, item.OpponentEntryCompleted)));
        Assert.Equal((expected.HomeHomeRuns, expected.VisitorHomeRuns),
            (actual.HomeHomeRuns, actual.VisitorHomeRuns));
    }

    private static (int Id, int GameId, GameScoringStatus Status, TeamGameSide Side,
        int HomeScore, int VisitorScore, int Batter, int Inning, int Outs,
        int HomeRunLimit, bool OneUp) SessionValue(GameScoringSession item) =>
        (item.Id, item.GameId, item.Status, item.TeamSide, item.HomeScore,
            item.VisitorScore, item.CurrentBatterPosition, item.CurrentInning,
            item.CurrentOuts, item.HomeRunLimit, item.IsOneUpEnabled);

    private static (int Id, int SessionId, int BatterId, int BatterPosition,
        BatterResult Result)? PendingPlayValue(PendingScoringPlay? pending) =>
        pending is null ? null : (pending.Play.Id, pending.Play.GameScoringSessionId,
            pending.Play.BatterPlayerId, pending.Play.BatterPosition, pending.Play.Result);

    private static (int Id, int TeamId, int SeasonId, DateTime Date, TimeSpan Time,
        string? Opponent, string? Field) GameValue(Game item) =>
        (item.Id, item.TeamId, item.SeasonId, item.ScheduledDate, item.ScheduledTime,
            item.Opponent, item.Field);

    private static (int Id, int GameId, int PlayerId, int Order) FrozenLineupValue(
        GameLineupPlayer item) =>
        (item.Id, item.GameId, item.PlayerId, item.BattingOrder);

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
