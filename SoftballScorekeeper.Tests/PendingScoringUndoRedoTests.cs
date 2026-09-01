using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Pending_single_undoes_to_exact_plate_appearance_start_and_redoes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var start = PendingState((await repository.GetStateForGameAsync(context.Game.Id))!);

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        var selected = PendingState((await repository.GetStateForGameAsync(context.Game.Id))!);
        Assert.True((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanUndo);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(start, PendingState((await repository.GetStateForGameAsync(context.Game.Id))!));
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(selected, PendingState((await repository.GetStateForGameAsync(context.Game.Id))!));
    }

    [Fact]
    public async Task Resolver_defaults_are_one_checkpoint_and_runner_override_is_separate()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await CreatePendingSingleWithRunnerOnFirstAsync(db);
        var defaultState = (await setup.Repository.GetStateForGameAsync(setup.GameId))!;
        var runnerId = Assert.Single(defaultState.PendingPlay!.Movements,
            item => item.StartingLocation == RunnerStartingLocation.First).PlayerId;

        Assert.True((await setup.Repository.AdjustPendingRunnerAsync(
            setup.GameId, runnerId, RunnerDestination.Third)).IsSuccess);
        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Second,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));

        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        var restoredStart = (await setup.Repository.GetStateForGameAsync(setup.GameId))!;
        Assert.Null(restoredStart.PendingPlay);
        Assert.Equal(ScoringBase.First, Assert.Single(restoredStart.BaseRunners).Base);
    }

    [Fact]
    public async Task Progressive_runner_changes_remain_separately_undoable_and_redoable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await CreatePendingSingleWithRunnerOnFirstAsync(db);
        var runnerId = Assert.Single(
            (await setup.Repository.GetStateForGameAsync(setup.GameId))!.PendingPlay!.Movements,
            item => item.StartingLocation == RunnerStartingLocation.First).PlayerId;

        Assert.True((await setup.Repository.AdjustPendingRunnerAsync(
            setup.GameId, runnerId, RunnerDestination.Third)).IsSuccess);
        Assert.True((await setup.Repository.AdjustPendingRunnerAsync(
            setup.GameId, runnerId, RunnerDestination.Scores)).IsSuccess);

        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Third,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));
        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Second,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));
        Assert.True((await setup.Repository.RedoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Third,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));
        Assert.True((await setup.Repository.RedoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Scores,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));
    }

    [Fact]
    public async Task Backtracking_to_default_coalesces_repeated_override_taps()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await CreatePendingSingleWithRunnerOnFirstAsync(db);
        var runnerId = Assert.Single(
            (await setup.Repository.GetStateForGameAsync(setup.GameId))!.PendingPlay!.Movements,
            item => item.StartingLocation == RunnerStartingLocation.First).PlayerId;

        foreach (var destination in new[]
                 { RunnerDestination.Third, RunnerDestination.Second, RunnerDestination.Third })
            Assert.True((await setup.Repository.AdjustPendingRunnerAsync(
                setup.GameId, runnerId, destination)).IsSuccess);

        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        Assert.Equal(RunnerDestination.Second,
            PendingDestination((await setup.Repository.GetStateForGameAsync(setup.GameId))!, runnerId));
        Assert.True((await setup.Repository.UndoAsync(setup.GameId)).IsSuccess);
        Assert.Null((await setup.Repository.GetStateForGameAsync(setup.GameId))!.PendingPlay);
    }

    [Fact]
    public async Task Undo_and_redo_cross_pending_and_committed_action_boundaries()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 4);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Double)).IsSuccess);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Single((await repository.GetStateForGameAsync(context.Game.Id))!.PlateAppearances);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty((await repository.GetStateForGameAsync(context.Game.Id))!.PlateAppearances);

        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Single((await repository.GetStateForGameAsync(context.Game.Id))!.PlateAppearances);
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(BatterResult.Double,
            (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!.Play.Result);
    }

    [Fact]
    public async Task New_pending_action_after_undo_invalidates_abandoned_redo_branch()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Double)).IsSuccess);

        Assert.False((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
        Assert.Equal(BatterResult.Double,
            (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!.Play.Result);
    }

    [Fact]
    public async Task Fielder_choice_retirement_checkpoint_restores_exact_metadata()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var runnerId = state.BattingSequence[1].PlayerId;
        await db.SeedBaseRunnerAsync(new()
        {
            GameScoringSessionId = initialized.Session!.Id,
            PlayerId = runnerId,
            Base = ScoringBase.First,
        });
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice)).IsSuccess);
        var beforeDecision = PendingState((await repository.GetStateForGameAsync(context.Game.Id))!);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(runnerId, RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))])).IsSuccess);
        var decided = PendingState((await repository.GetStateForGameAsync(context.Game.Id))!);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(beforeDecision,
            PendingState((await repository.GetStateForGameAsync(context.Game.Id))!));
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(decided,
            PendingState((await repository.GetStateForGameAsync(context.Game.Id))!));
    }

    [Fact]
    public async Task Reset_removes_pending_recovery_history_with_the_scoring_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await CreatePendingSingleWithRunnerOnFirstAsync(db);
        var sessionId = (await setup.Repository.GetStateForGameAsync(setup.GameId))!.Session.Id;

        Assert.True((await setup.Repository.ResetGameAsync(setup.GameId)).IsSuccess);

        Assert.Null(await setup.Repository.GetStateForGameAsync(setup.GameId));
        Assert.Equal(0, await db.UseConnectionAsync(connection =>
            connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
                sessionId)));
        Assert.Equal(0, await db.UseConnectionAsync(connection =>
            connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GameScoringHistory WHERE GameScoringSessionId = ?",
                sessionId)));
    }

    private static async Task<(GameScoringSessionRepository Repository, int GameId)>
        CreatePendingSingleWithRunnerOnFirstAsync(TemporaryTestDatabase db)
    {
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await db.SeedBaseRunnerAsync(new()
        {
            GameScoringSessionId = initialized.Session!.Id,
            PlayerId = state.BattingSequence[1].PlayerId,
            Base = ScoringBase.First,
        });
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        return (repository, context.Game.Id);
    }

    private static RunnerDestination PendingDestination(GameScoringState state, int playerId) =>
        Assert.Single(state.PendingPlay!.Movements, item => item.PlayerId == playerId).Destination;

    private static string PendingState(GameScoringState state) =>
        string.Join('|', ScoringStateValues(state), state.PendingPlay is null
            ? "none"
            : string.Join(';', state.PendingPlay.Movements
                .OrderBy(item => item.Id)
                .Select(item => $"{item.Id},{item.PlayerId},{item.StartingLocation},{item.Destination},{item.SafeDestination},{item.AttemptedDestination},{item.OutOrder},{item.RequiresOutcomeConfirmation}")));
}
