using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class MultiOutPlayResolutionTests
{
    [Theory]
    [InlineData(0, 0, false, false)]
    [InlineData(1, 0, true, false)]
    [InlineData(2, 0, true, true)]
    [InlineData(2, 1, true, false)]
    [InlineData(2, 2, false, false)]
    public void Multi_out_result_availability_requires_participants_and_outs_remaining(
        int existingRunnerCount,
        int currentOuts,
        bool expectDoublePlay,
        bool expectTriplePlay)
    {
        var results = PlateAppearanceResultDefinitions.GetSelectableResults(
            existingRunnerCount, currentOuts);

        Assert.Equal(expectDoublePlay, results.Contains(BatterResult.DoublePlay));
        Assert.Equal(expectTriplePlay, results.Contains(BatterResult.TriplePlay));
    }

    [Fact]
    public async Task Double_play_persists_explicit_classification_membership_and_two_outs()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);

        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state.PendingPlay);
        Assert.Equal(BatterResult.DoublePlay, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal(2, state.Session.CurrentOuts);
        Assert.Empty(state.BaseRunners);
        Assert.True(new HashSet<int>
        {
            context.Players[1].Id,
            context.Players[0].Id,
        }.SetEquals(state.RunnerMovements.Select(item => item.PlayerId)));
        Assert.Equal(
            new int?[] { 1, 2 },
            state.RunnerMovements.OrderBy(item => item.OutOrder).Select(item => item.OutOrder)
        );
        Assert.True(new HashSet<int>
        {
            context.Players[1].Id,
            context.Players[0].Id,
        }.SetEquals(state.GetCurrentHalfInningOutPlayerIds()));
    }

    [Theory]
    [InlineData(ScoringBase.Second)]
    [InlineData(ScoringBase.Third)]
    public async Task Double_play_supports_different_runner_and_batter_combinations(
        ScoringBase runnerBase
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], runnerBase);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Equal(2, state.Session.CurrentOuts);
        Assert.Equal(
            new[] { context.Players[1].Id, context.Players[0].Id },
            state.GetCurrentHalfInningOutPlayerIds()
        );
    }

    [Fact]
    public async Task Two_existing_runners_can_be_out_while_batter_remains_safe_until_commit()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);

        var selected = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[2].Id), Out(context.Players[1].Id)]
        );
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal(
            RunnerDestination.First,
            pending
                .PendingPlay.Movements.Single(item => item.PlayerId == context.Players[0].Id)
                .Destination
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.DoublePlay, Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(
            new[] { (ScoringBase.First, context.Players[0].Id) },
            committed.BaseRunners.Select(item => (item.Base, item.PlayerId))
        );
    }

    [Theory]
    [InlineData(0, 2, 1, 2)]
    [InlineData(1, 0, 2, 0)]
    public async Task Double_play_respects_existing_outs_and_third_out_transition(
        int startingOuts,
        int expectedOuts,
        int expectedInning,
        int expectedCurrentHalfOuts
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, startingOuts);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[1], ScoringBase.First);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Equal(expectedOuts, state.Session.CurrentOuts);
        Assert.Equal(expectedInning, state.Session.CurrentInning);
        Assert.Equal(expectedCurrentHalfOuts, state.GetCurrentHalfInningOutPlayerIds().Count);
        Assert.Equal(startingOuts == 0 ? 2 : 0,
            state.RunnerMovements.Count(item => item.Destination == RunnerDestination.Out));
        Assert.Equal(startingOuts == 1 ? 1 : 0, state.HalfInnings.Count);
    }

    [Fact]
    public async Task Two_out_double_play_is_unavailable_and_cannot_create_partial_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 2);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[1], ScoringBase.First);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(PendingPlayFailure.InvalidRequest, selected.FailureReason);
        Assert.DoesNotContain(BatterResult.DoublePlay,
            PlateAppearanceResultDefinitions.GetSelectableResults(1, 2));
        Assert.Null(state.PendingPlay);
        Assert.Empty(state.PlateAppearances);
        Assert.Empty(state.RunnerMovements);
        Assert.Equal(1, state.Session.CurrentInning);
        Assert.Equal(2, state.Session.CurrentOuts);
        Assert.Empty(state.HalfInnings);
    }

    [Fact]
    public async Task Safe_remaining_runner_keeps_base_and_double_play_does_not_clear_bases()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);
        await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[1].Id), Out(context.Players[0].Id)]
        );

        var defaulted = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(RunnerDestination.Third,
            defaulted.PendingPlay!.Movements.Single(item =>
                item.PlayerId == context.Players[3].Id).Destination);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id,
            RunnerDestination.Scores)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id,
            RunnerDestination.Third)).IsSuccess);

        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(2, state.Session.CurrentOuts);
        Assert.Equal(
            new[] { (ScoringBase.Third, context.Players[3].Id) },
            state.BaseRunners.Select(item => (item.Base, item.PlayerId))
        );
    }

    [Fact]
    public async Task First_and_second_force_chain_defaults_second_survivor_to_third()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);

        var selected = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[1].Id), Out(context.Players[0].Id)]);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.NotNull(state.PendingPlay);
        Assert.Equal(RunnerDestination.Third,
            state.PendingPlay.Movements.Single(item =>
                item.PlayerId == context.Players[2].Id).Destination);
        Assert.False(state.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id).RequiresOutcomeConfirmation);
        Assert.Empty(state.PlateAppearances);
    }

    [Fact]
    public async Task Bases_loaded_force_chain_defaults_survivors_and_preserves_edits_on_resume()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);

        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[0].Id), Out(context.Players[1].Id)])).IsSuccess);
        var defaulted = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var pending = defaulted.PendingPlay!;

        Assert.Equal(RunnerDestination.Out, pending.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.Out, pending.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);
        Assert.All(pending.Movements.Where(item => item.OutOrder is not null), item =>
        {
            Assert.Null(item.SafeDestination);
            Assert.Null(item.AttemptedDestination);
        });
        Assert.Equal(RunnerDestination.Third, pending.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id).Destination);
        Assert.Equal(RunnerDestination.Scores, pending.Movements.Single(item =>
            item.PlayerId == context.Players[3].Id).Destination);
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.Play, pending.Movements, out var resolverState));
        var resolvedState = Assert.IsType<BaseRunnerResolutionState>(resolverState);
        Assert.True(BaseRunnerResolver.IsLegalArrangement(resolvedState));
        Assert.Equal(4, resolvedState.Participants.Count);
        Assert.Single(resolvedState.Participants.Where(item =>
            item.Destination == RunnerDestination.Third));
        Assert.Empty((await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[1].Id)).Choices);
        Assert.Equal(PendingPlayFailure.InvalidRequest,
            (await repository.AdjustPendingRunnerAsync(
                context.Game.Id, context.Players[1].Id,
                RunnerDestination.Second)).FailureReason);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id,
            RunnerDestination.Third)).IsSuccess);
        var resumed = (await db.CreateGameScoringSessionRepository()
            .GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(RunnerDestination.Third, resumed.PendingPlay!.Movements.Single(item =>
            item.PlayerId == context.Players[3].Id).Destination);

        Assert.True((await db.CreateGameScoringSessionRepository()
            .CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(new[]
        {
            (ScoringBase.Second, context.Players[2].Id),
            (ScoringBase.Third, context.Players[3].Id),
        }, committed.BaseRunners.OrderBy(item => item.Base)
            .Select(item => (item.Base, item.PlayerId)));
    }

    [Fact]
    public async Task Bases_loaded_triple_play_completes_without_participant_resolution()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.TriplePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state.PendingPlay);
        Assert.Equal(BatterResult.TriplePlay, Assert.Single(state.PlateAppearances).Result);
        Assert.Empty(state.RunnerMovements);
        Assert.Empty(state.BaseRunners);
        Assert.Equal((2, 0, 0), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.HomeScore));
        Assert.Equal(0, Assert.Single(state.HalfInnings).TeamRuns);
    }

    [Fact]
    public async Task Triple_play_completes_once_without_run_ruling_or_retiree_selection()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.TriplePlay);

        var reloaded = (await db.CreateGameScoringSessionRepository()
            .GetStateForGameAsync(context.Game.Id))!;
        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(reloaded.PendingPlay);
        Assert.Equal(BatterResult.TriplePlay,
            Assert.Single(reloaded.PlateAppearances).Result);
        Assert.Equal((2, 0, 0), (reloaded.Session.CurrentInning,
            reloaded.Session.CurrentOuts, reloaded.Session.HomeScore));
        Assert.Empty(reloaded.BaseRunners);
        Assert.Empty(reloaded.RunnerMovements);
        Assert.Equal(0, Assert.Single(reloaded.HalfInnings).TeamRuns);

        var duplicate = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        Assert.False(duplicate.IsSuccess);
        var afterDuplicate = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Single(afterDuplicate.PlateAppearances);
        Assert.Single(afterDuplicate.HalfInnings);
        Assert.Empty(afterDuplicate.RunnerMovements);
    }

    [Fact]
    public async Task One_out_double_play_completes_atomically_without_run_ruling()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);

        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state.PendingPlay);
        Assert.Equal(BatterResult.DoublePlay, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal(0, state.Session.HomeScore);
        Assert.Equal((2, 0), (state.Session.CurrentInning, state.Session.CurrentOuts));
        Assert.Empty(state.RunnerMovements);
        Assert.Empty(state.BaseRunners);
        Assert.Equal(0, Assert.Single(state.HalfInnings).TeamRuns);
        Assert.Single(state.HalfInnings);
    }

    [Fact]
    public async Task Known_batter_runner_third_out_still_automatically_rejects_run()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 2);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id,
            RunnerDestination.Out)).IsSuccess);

        var requirements = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.True(requirements.IsSuccess);
        Assert.Empty(requirements.PlayerIds);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(0, state.Session.HomeScore);
        Assert.Equal(false, state.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[3].Id).RunCounts);
        Assert.Single(state.HalfInnings);
    }

    [Theory]
    [InlineData(BatterResult.DoublePlay, 1)]
    [InlineData(BatterResult.TriplePlay, 0)]
    public void Explicit_inning_ending_multi_out_is_automatic_no_run(
        BatterResult result, int currentOuts)
    {
        Assert.Equal(ThirdOutRunRulingRequirement.AutomaticNoRun,
            ThirdOutRunRulingEvaluator.Evaluate(currentOuts, 1, [], result));
    }

    [Fact]
    public async Task Out_attempting_as_third_out_preserves_scorer_run_ruling()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[2].Id), Out(context.Players[0].Id)]);
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[1].Id);
        Assert.Contains(new RunnerOutcomeChoice(
            RunnerDestination.Out,
            RunnerDestination.Second,
            RunnerDestination.Third), choices.Choices);
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[1].Id,
            new(RunnerDestination.Out,
                RunnerDestination.Second, RunnerDestination.Third))).IsSuccess);

        var requirements = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.Equal(new[] { context.Players[3].Id }, requirements.PlayerIds);
        Assert.Equal(CompletePlateAppearanceFailure.InvalidScoringState,
            (await repository.CompletePlateAppearanceAsync(context.Game.Id)).FailureReason);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id,
            new Dictionary<int, bool> { [context.Players[3].Id] = true })).IsSuccess);

        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.DoublePlay, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal(1, state.Session.HomeScore);
        var retiredRunner = state.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id);
        Assert.Equal(3, retiredRunner.OutOrder);
        Assert.Equal(RunnerDestination.Second, retiredRunner.SafeDestination);
        Assert.Equal(RunnerDestination.Third, retiredRunner.AttemptedDestination);
        Assert.All(state.RunnerMovements.Where(item => item.OutOrder is 1 or 2), item =>
        {
            Assert.Null(item.SafeDestination);
            Assert.Null(item.AttemptedDestination);
        });
        Assert.Equal(true, state.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[3].Id).RunCounts);
    }

    [Fact]
    public async Task Scored_remaining_runner_stays_editable_until_next_batter_commit()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);
        await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[1].Id), Out(context.Players[0].Id)]
        );

        Assert.True(
            (
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id,
                    context.Players[3].Id,
                    RunnerDestination.Scores
                )
            ).IsSuccess
        );
        var scoredPending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(scoredPending.PendingPlay);
        Assert.Equal(0, scoredPending.Session.HomeScore);
        Assert.True(
            (
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id,
                    context.Players[3].Id,
                    RunnerDestination.Third
                )
            ).IsSuccess
        );
        Assert.True(
            (
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id,
                    context.Players[3].Id,
                    RunnerDestination.Scores
                )
            ).IsSuccess
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(1, committed.Session.HomeScore);
        Assert.Equal(
            RunnerDestination.Scores,
            committed
                .RunnerMovements.Single(item => item.PlayerId == context.Players[3].Id)
                .Destination
        );
    }

    [Fact]
    public async Task Runner_updater_out_attempting_auto_completes_all_terminal_double_play()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[2].Id), Out(context.Players[0].Id)])).IsSuccess);
        var completed = await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[1].Id,
            new(RunnerDestination.Out,
                RunnerDestination.Second, RunnerDestination.Third));
        var reloaded = (
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id)
        )!;

        Assert.True(completed.IsSuccess);
        Assert.Null(completed.PendingPlay);
        Assert.Null(reloaded.PendingPlay);
        Assert.Equal(BatterResult.DoublePlay, Assert.Single(reloaded.PlateAppearances).Result);
        var movement = reloaded.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id
        );
        Assert.Equal(RunnerDestination.Out, movement.Destination);
        Assert.Equal(RunnerDestination.Second, movement.SafeDestination);
        Assert.Equal(RunnerDestination.Third, movement.AttemptedDestination);
        Assert.Equal(3, movement.OutOrder);
        Assert.Equal(new int?[] { 1, 2, 3 }, reloaded.RunnerMovements
            .OrderBy(item => item.OutOrder).Select(item => item.OutOrder));
        Assert.Equal((2, 0, 2), (reloaded.Session.CurrentInning,
            reloaded.Session.CurrentOuts, reloaded.Session.CurrentBatterPosition));
        Assert.Single(reloaded.HalfInnings);
        Assert.Equal(3, reloaded.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));

        Assert.False((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var afterDuplicate = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Single(afterDuplicate.PlateAppearances);
        Assert.Single(afterDuplicate.HalfInnings);
        Assert.Equal(2, afterDuplicate.Session.CurrentBatterPosition);
    }

    [Fact]
    public async Task Multi_out_retiree_selection_rejects_out_attempting_without_mutation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        var attempting = new MultiOutRetirementSelection(
            context.Players[1].Id,
            new(RunnerDestination.Out,
                RunnerDestination.Second, RunnerDestination.Third));

        var rejected = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id, [attempting, Out(context.Players[0].Id)]);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(PendingPlayFailure.InvalidRequest, rejected.FailureReason);
        Assert.Empty(state.PlateAppearances);
        Assert.All(state.PendingPlay!.Movements, movement =>
        {
            Assert.True(movement.RequiresOutcomeConfirmation);
            Assert.Null(movement.OutOrder);
            Assert.Null(movement.SafeDestination);
            Assert.Null(movement.AttemptedDestination);
        });
    }

    [Fact]
    public async Task Pending_double_play_selection_survives_resume_and_next_batter_commits()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);
        await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[1].Id), Out(context.Players[0].Id)]
        );

        var resumed = (
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id)
        )!;
        Assert.Equal(BatterResult.DoublePlay, resumed.PendingPlay!.Play.Result);
        Assert.Equal(
            new int?[] { 1, 2 },
            resumed
                .PendingPlay.Movements.Where(item => item.OutOrder is not null)
                .OrderBy(item => item.OutOrder)
                .Select(item => item.OutOrder)
        );
        Assert.Empty(resumed.PlateAppearances);

        Assert.True(
            (
                await db.CreateGameScoringSessionRepository()
                    .CompletePlateAppearanceAsync(context.Game.Id)
            ).IsSuccess
        );
    }

    [Fact]
    public async Task Invalid_retirement_count_fails_without_partial_persistence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay);

        var rejected = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [Out(context.Players[1].Id)]
        );
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.False(rejected.IsSuccess);
        Assert.Empty(state.PlateAppearances);
        Assert.Equal(0, state.Session.CurrentOuts);
        Assert.All(
            state.PendingPlay!.Movements,
            item =>
            {
                Assert.True(item.RequiresOutcomeConfirmation);
                Assert.Null(item.OutOrder);
            }
        );
    }

    [Fact]
    public async Task Three_participant_triple_play_ends_half_inning_without_movements()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.TriplePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Equal(BatterResult.TriplePlay, Assert.Single(state.PlateAppearances).Result);
        Assert.Empty(state.RunnerMovements);
        Assert.Equal(2, state.Session.CurrentInning);
        Assert.Equal(0, state.Session.CurrentOuts);
        Assert.Single(state.HalfInnings);
        Assert.Empty(state.BaseRunners);
    }

    [Fact]
    public async Task Bases_loaded_double_play_keeps_membership_selection_pending()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3], ScoringBase.Third);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DoublePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.NotNull(selected.PendingPlay);
        Assert.NotNull(state.PendingPlay);
        Assert.Equal(4, state.PendingPlay.Movements.Count);
        Assert.All(state.PendingPlay.Movements, movement =>
        {
            Assert.True(movement.RequiresOutcomeConfirmation);
            Assert.Null(movement.OutOrder);
        });
        Assert.Empty(state.PlateAppearances);
    }

    [Fact]
    public void Multi_out_ui_toggles_unordered_ordinary_out_membership()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dialog = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "MultiOutSelectionDialog.xaml.cs"));
        var page = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "ScoringPage.xaml.cs"));
        var resources = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Resources", "Strings", "AppResources.resx"));
        var normalized = dialog.Replace("\r\n", "\n");
        var normalizedPage = page.Replace("\r\n", "\n");

        Assert.Contains("selectedPlayerIds.Add(playerId)", dialog);
        Assert.Contains("selectedPlayerIds.RemoveAt(existingIndex)", dialog);
        Assert.Contains("new MultiOutRetirementSelection(\n                playerId, RunnerOutcomeChoice.ForDestination(\n                    RunnerDestination.Out))",
            normalized);
        Assert.Contains("Select {0} Players Who Were Out", resources);
        Assert.DoesNotContain("RunnerDestinationDialog", dialog);
        Assert.DoesNotContain("GetRetirementOutcomes", dialog);
        Assert.DoesNotContain("selectedOutcomes", dialog);
        Assert.DoesNotContain("orderLabels", dialog);
        Assert.DoesNotContain("MultiOutSelectionOrderFormat", dialog);
        Assert.DoesNotContain("Outs in Order", resources);
        Assert.DoesNotContain("#{0}", resources);
        Assert.Contains("await viewModel.ChangeRunnerOutcomeAsync(\n                participant.Value.PlayerId, outcome);\n            await HandlePendingInningTransitionAsync();",
            normalizedPage);
    }

    [Fact]
    public async Task Triple_play_with_one_existing_out_is_unavailable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2], ScoringBase.Second);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.TriplePlay);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(PendingPlayFailure.InvalidRequest, selected.FailureReason);
        Assert.DoesNotContain(BatterResult.TriplePlay,
            PlateAppearanceResultDefinitions.GetSelectableResults(2, 1));
        Assert.Null(state.PendingPlay);
        Assert.Empty(state.PlateAppearances);
        Assert.Empty(state.RunnerMovements);
        Assert.Equal(1, state.Session.CurrentInning);
        Assert.Equal(1, state.Session.CurrentOuts);
        Assert.Empty(state.HalfInnings);
    }

    private static MultiOutRetirementSelection Out(int playerId) =>
        new(playerId, RunnerOutcomeChoice.ForDestination(RunnerDestination.Out));

    private static Task<int> SeedBaseAsync(
        TemporaryTestDatabase db,
        int sessionId,
        Player player,
        ScoringBase scoringBase
    ) =>
        db.SeedBaseRunnerAsync(
            new()
            {
                GameScoringSessionId = sessionId,
                PlayerId = player.Id,
                Base = scoringBase,
            }
        );

    private static async Task<(
        Team Team,
        Game Game,
        IReadOnlyList<Player> Players
    )> CreateReadyGameAsync(TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season
        {
            TeamId = team.Id,
            Name = "Season",
            Year = 2026,
        };
        await db.SeedSeasonAsync(season);
        var game = new Game
        {
            TeamId = team.Id,
            SeasonId = season.Id,
            ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18),
        };
        await db.SeedGameAsync(game);
        var players = new List<Player>();
        for (var index = 0; index < playerCount; index++)
        {
            var player = new Player
            {
                FirstName = $"Player{index + 1}",
                LastName = "Test",
                IsActive = true,
            };
            await db.CreatePlayerRepository().CreateAsync(player);
            await db.SeedTeamPlayerAsync(
                new()
                {
                    TeamId = team.Id,
                    PlayerId = player.Id,
                    IsActive = true,
                }
            );
            await db.SeedGameLineupPlayerAsync(
                new()
                {
                    GameId = game.Id,
                    PlayerId = player.Id,
                    BattingOrder = index + 1,
                }
            );
            players.Add(player);
        }
        return (team, game, players);
    }
}
