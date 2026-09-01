using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using static SoftballScorekeeper.Tests.ScoringUndoRedoTestState;

namespace SoftballScorekeeper.Tests;

public sealed partial class MultiOutPlayResolutionTests
{
    [Theory]
    [InlineData(BatterResult.DoublePlay, 1, 0)]
    [InlineData(BatterResult.DoublePlay, 1, 1)]
    [InlineData(BatterResult.TriplePlay, 2, 0)]
    public async Task Multi_out_actions_undo_and_redo_atomically(
        BatterResult result,
        int runnerCount,
        int startingOuts
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, startingOuts);
        for (var index = 0; index < runnerCount; index++)
            await SeedBaseAsync(
                db,
                initialized.Session.Id,
                context.Players[index + 1],
                (ScoringBase)(index + 1)
            );
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.SelectBatterResultAsync(context.Game.Id, result)).IsSuccess);
        await CompleteTransitionIfNeeded(repository, context.Game.Id);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(committed),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    private static async Task CompleteTransitionIfNeeded(
        GameScoringSessionRepository repository,
        int gameId
    )
    {
        var transition = await repository.GetPendingHalfInningTransitionAsync(gameId);
        if (transition is null)
            return;
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(gameId, transition.Id));
        Assert.True(
            (
                await repository.CompleteOpponentHalfInningAsync(gameId, transition.Id, 0, 0)
            ).IsSuccess
        );
    }
}

public sealed partial class WalkFielderChoiceScoringTests
{
    [Fact]
    public async Task Forced_walk_undo_and_redo_restore_bases_and_run_exactly()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(
            (await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Walk)).IsSuccess
        );
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(committed),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fielder_choice_and_reconciled_out_undo_and_redo_exactly(bool reconcileToOut)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);
        var retiredPlayer = reconcileToOut ? context.Players[0] : context.Players[1];
        Assert.True(
            (
                await repository.SelectMultiOutRetirementsAsync(
                    context.Game.Id,
                    [
                        new(
                            retiredPlayer.Id,
                            RunnerOutcomeChoice.ForDestination(RunnerDestination.Out)
                        ),
                    ]
                )
            ).IsSuccess
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(committed),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Fact]
    public async Task Fielder_choice_displacement_chain_undo_restores_every_runner()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);
        Assert.True(
            (
                await repository.SelectMultiOutRetirementsAsync(
                    context.Game.Id,
                    [
                        new(
                            context.Players[1].Id,
                            RunnerOutcomeChoice.ForDestination(RunnerDestination.Out)
                        ),
                    ]
                )
            ).IsSuccess
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }
}

public sealed partial class RunnerOutAttemptingAdvanceTests
{
    [Fact]
    public async Task Out_attempting_metadata_is_removed_by_undo_and_restored_by_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Second
        );
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id,
            context.Players[1].Id
        );
        var attemptedThird = Assert.Single(
            choices.Choices,
            item =>
                item.Destination == RunnerDestination.Out
                && item.SafeDestination == RunnerDestination.Second
                && item.AttemptedDestination == RunnerDestination.Third
        );
        Assert.True(
            (
                await repository.AdjustPendingRunnerOutcomeAsync(
                    context.Game.Id,
                    context.Players[1].Id,
                    attemptedThird
                )
            ).IsSuccess
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(committed),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }
}

public sealed partial class SacrificeFlyScoringTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sacrifice_fly_and_reconciled_out_undo_and_redo_exactly(bool reconcileToOut)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[3].Id, ScoringBase.Third);
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        if (reconcileToOut)
            Assert.True(
                (
                    await repository.AdjustPendingRunnerAsync(
                        context.Game.Id,
                        context.Players[3].Id,
                        RunnerDestination.Third
                    )
                ).IsSuccess
            );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(before),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            UndoRedoState(committed),
            UndoRedoState((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }
}

internal static class ScoringUndoRedoTestState
{
    public static string UndoRedoState(GameScoringState state) =>
        string.Join(
            '|',
            state.Session.HomeScore,
            state.Session.VisitorScore,
            state.Session.CurrentBatterPosition,
            state.Session.CurrentInning,
            state.Session.CurrentOuts,
            state.HomeHomeRuns,
            state.VisitorHomeRuns,
            string.Join(
                ';',
                state.PlateAppearances.Select(item =>
                    $"{item.Id},{item.PlayerId},{item.BattingPosition},{item.PlateAppearanceNumber},{item.Inning},{item.Result}"
                )
            ),
            string.Join(
                ';',
                state.BaseRunners.Select(item => $"{item.Id},{item.PlayerId},{item.Base}")
            ),
            string.Join(
                ';',
                state.RunnerMovements.Select(item =>
                    $"{item.Id},{item.GamePlateAppearanceId},{item.PlayerId},{item.StartingLocation},{item.Destination},{item.SafeDestination},{item.AttemptedDestination},{item.OutOrder},{item.RunCounts}"
                )
            ),
            string.Join(
                ';',
                state.HalfInnings.Select(item =>
                    $"{item.Id},{item.Inning},{item.TeamRuns},{item.TeamHomeRuns},{item.TeamSummaryAcknowledged},{item.OpponentRuns},{item.OpponentHomeRuns},{item.OpponentEntryCompleted}"
                )
            )
        );
}
