using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringRulesLifecycleRepositoryTests
{
    [Fact]
    public async Task Home_start_persists_opponent_top_one_before_bottom_one_and_resumes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);

        var pending = Assert.IsType<GameScoringHalfInning>(
            await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Equal(0, pending.Inning);
        Assert.True(pending.TeamSummaryAcknowledged);
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, pending.Id, 2, 1)).IsSuccess);

        var resumed = await db.CreateGameScoringSessionRepository()
            .GetStateForGameAsync(context.Game.Id);
        Assert.Empty(resumed!.PlateAppearances);
        Assert.Equal((1, 0, 2), (resumed.Session.CurrentInning,
            resumed.Session.HomeScore, resumed.Session.VisitorScore));
        Assert.True(Assert.Single(resumed.HalfInnings).OpponentEntryCompleted);
        Assert.Null(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
    }

    [Fact]
    public async Task Home_opening_opponent_entry_undo_and_redo_restore_exact_startup_states()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);
        var opening = Assert.IsType<GameScoringHalfInning>(
            await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));

        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 2, 1)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal((1, 0, 2), (committed.Session.CurrentInning,
            committed.Session.HomeScore, committed.Session.VisitorScore));

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        var restored = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var restoredOpening = Assert.Single(restored.HalfInnings);
        Assert.Equal(0, restoredOpening.Inning);
        Assert.True(restoredOpening.TeamSummaryAcknowledged);
        Assert.False(restoredOpening.OpponentEntryCompleted);
        Assert.Equal((0, 0), (restored.Session.HomeScore, restored.Session.VisitorScore));
        Assert.Equal(new(false, true),
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id));
        Assert.Equal(restoredOpening.Id,
            (await repository.GetPendingHalfInningTransitionAsync(context.Game.Id))!.Id);

        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        var redone = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var redoneOpening = Assert.Single(redone.HalfInnings);
        Assert.Equal((committed.Session.HomeScore, committed.Session.VisitorScore,
                committed.Session.CurrentInning, committed.Session.CurrentOuts),
            (redone.Session.HomeScore, redone.Session.VisitorScore,
                redone.Session.CurrentInning, redone.Session.CurrentOuts));
        Assert.Equal((committed.HalfInnings[0].OpponentRuns,
                committed.HalfInnings[0].OpponentHomeRuns,
                committed.HalfInnings[0].OpponentEntryCompleted),
            (redoneOpening.OpponentRuns, redoneOpening.OpponentHomeRuns,
                redoneOpening.OpponentEntryCompleted));
    }

    [Fact]
    public async Task New_opening_opponent_entry_after_undo_clears_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);
        var opening = (await repository.GetPendingHalfInningTransitionAsync(
            context.Game.Id))!;
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 2, 1)).IsSuccess);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        opening = (await repository.GetPendingHalfInningTransitionAsync(
            context.Game.Id))!;
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 3, 0)).IsSuccess);

        Assert.Equal(new(true, false),
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id));
        Assert.Equal(3, (await repository.GetStateForGameAsync(
            context.Game.Id))!.Session.VisitorScore);
    }

    [Fact]
    public async Task Home_side_can_change_to_away_after_opening_entry_is_undone()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);
        var opening = (await repository.GetPendingHalfInningTransitionAsync(
            context.Game.Id))!;
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 1, 0)).IsSuccess);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        var changed = await repository.UpdateGameRulesAsync(
            context.Game.Id, 5, false, TeamGameSide.Away);
        var away = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(changed.IsSuccess);
        Assert.Equal(TeamGameSide.Away, away.Session.TeamSide);
        Assert.Equal((1, 0, 0), (away.Session.CurrentInning,
            away.Session.CurrentOuts, away.Session.VisitorScore));
        Assert.Empty(away.HalfInnings);
        Assert.Null(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Equal(new(false, false),
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id));
    }

    [Fact]
    public async Task Reset_after_opening_undo_allows_a_fresh_valid_home_start()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home);
        var opening = (await repository.GetPendingHalfInningTransitionAsync(
            context.Game.Id))!;
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 0, 0)).IsSuccess);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home)).IsSuccess);
        var freshOpening = Assert.IsType<GameScoringHalfInning>(
            await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Equal(0, freshOpening.Inning);
        Assert.False(freshOpening.OpponentEntryCompleted);
    }

    [Fact]
    public async Task Away_start_has_no_opening_opponent_entry()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Away);

        Assert.Null(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Empty((await repository.GetStateForGameAsync(context.Game.Id))!.HalfInnings);
    }

    [Fact]
    public async Task Team_side_change_is_allowed_only_without_committed_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Away);

        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 5, false, TeamGameSide.Home)).IsSuccess);
        var opening = Assert.Single((await repository.GetStateForGameAsync(
            context.Game.Id))!.HalfInnings);
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, opening.Id, 0, null)).IsSuccess);
        Assert.Equal(GameScoringLifecycleFailure.ScoringHistoryExists,
            (await repository.UpdateGameRulesAsync(
                context.Game.Id, 5, false, TeamGameSide.Away)).FailureReason);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 5, false, TeamGameSide.Away)).IsSuccess);
        Assert.Empty((await repository.GetStateForGameAsync(context.Game.Id))!.HalfInnings);

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Walk)).IsSuccess);
        Assert.Equal(GameScoringLifecycleFailure.ScoringHistoryExists,
            (await repository.UpdateGameRulesAsync(
                context.Game.Id, 5, false, TeamGameSide.Home)).FailureReason);
    }

    [Fact]
    public async Task Completed_game_plays_use_canonical_order_scores_and_nullable_opponent_values()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Walk)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.InTheParkHomeRun)).IsSuccess);
        await db.UseConnectionAsync(async connection => await connection.InsertAsync(
            new GameScoringHalfInning
            {
                GameScoringSessionId = initialized.Session!.Id,
                Inning = 1,
                TeamSummaryAcknowledged = true,
                OpponentRuns = null,
                OpponentHomeRuns = 0,
                OpponentEntryCompleted = true,
            }));
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var projection = Assert.IsType<CompletedGamePlaysProjection>(
            await repository.GetCompletedGamePlaysAsync(context.Game.Id));
        Assert.Equal(new[] { CompletedGameHalf.Top, CompletedGameHalf.Bottom },
            projection.HalfInnings.Select(item => item.Half));
        var plays = projection.HalfInnings[0].Plays;
        Assert.Equal(new[] { 1, 2 }, plays.Select(item => item.PlateAppearanceNumber));
        Assert.Equal((0, 2), (plays[0].TeamScore, plays[1].TeamScore));
        Assert.Equal(BatterResult.InTheParkHomeRun, plays[1].Result);
        Assert.Equal(2, plays[1].RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Scores));
        var opponent = Assert.IsType<CompletedOpponentHalfSummary>(
            projection.HalfInnings[1].OpponentSummary);
        Assert.Null(opponent.Runs);
        Assert.Equal(0, opponent.HomeRuns);
    }
}

public sealed partial class WalkFielderChoiceScoringTests
{
    [Fact]
    public async Task In_the_park_home_run_scores_everyone_without_fence_hr_or_solo_beer()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away, 0, false);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");

        var result = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.InTheParkHomeRun);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(result.IsSuccess);
        Assert.Null(result.PendingPlay);
        Assert.Equal(BatterResult.InTheParkHomeRun,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(4, committed.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Scores && item.RunCounts == true));
        Assert.Equal(4, committed.Session.VisitorScore);
        Assert.Equal(0, committed.VisitorHomeRuns);
        Assert.True(committed.IsHomeRunLegal() == false);
        Assert.Empty((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.RuleType == BeerRuleType.SoloHomeRun));

        var stats = await repository.GetInProgressBattingStatisticsAsync(context.Game.Id);
        var batter = stats!.Players.Single(item => item.PlayerId == context.Players[0].Id)
            .BattingStatistics;
        Assert.Equal((1, 1, 1, 1, 4), (batter.PlateAppearances,
            batter.AtBats, batter.Hits, batter.HomeRuns, batter.TotalBases));
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty((await repository.GetStateForGameAsync(context.Game.Id))!.PlateAppearances);
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(BatterResult.InTheParkHomeRun,
            Assert.Single((await repository.GetStateForGameAsync(
                context.Game.Id))!.PlateAppearances).Result);
    }
}
