using System.Xml.Linq;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class WalkFielderChoiceScoringTests
{
    [Fact]
    public void Walk_and_fielder_choice_are_distinct_selectable_plate_appearance_results()
    {
        Assert.NotEqual((int)BatterResult.Walk, (int)BatterResult.FielderChoice);
        Assert.Contains("DoublePlay", Enum.GetNames<BatterResult>());
        Assert.Contains("TriplePlay", Enum.GetNames<BatterResult>());
        Assert.DoesNotContain(BatterResult.FielderChoice,
            PlateAppearanceResultDefinitions.GetSelectableResults(false));
        Assert.Contains(BatterResult.FielderChoice,
            PlateAppearanceResultDefinitions.GetSelectableResults(true));
        Assert.Contains(BatterResult.DoublePlay,
            PlateAppearanceResultDefinitions.GetSelectableResults(1));
        Assert.DoesNotContain(BatterResult.TriplePlay,
            PlateAppearanceResultDefinitions.GetSelectableResults(1));
        Assert.Contains(BatterResult.TriplePlay,
            PlateAppearanceResultDefinitions.GetSelectableResults(2));
    }

    [Theory]
    [InlineData("", "0:1", 0)]
    [InlineData("1", "0:1,1:2", 0)]
    [InlineData("2", "0:1,2:2", 0)]
    [InlineData("3", "0:1,3:3", 0)]
    [InlineData("1,2", "0:1,1:2,2:3", 0)]
    [InlineData("1,3", "0:1,1:2,3:3", 0)]
    [InlineData("2,3", "0:1,2:2,3:3", 0)]
    [InlineData("1,2,3", "0:1,1:2,2:3,3:4", 1)]
    public async Task Walk_auto_commits_forced_only_movement(
        string occupiedBases, string expectedMovements, int expectedRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, occupiedBases);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Walk);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Walk, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal(ParseMovements(expectedMovements),
            MovementValues(state.RunnerMovements));
        Assert.Equal(2, state.Session.CurrentBatterPosition);
        Assert.Equal(expectedRuns, state.Session.HomeScore);
    }

    [Fact]
    public async Task Walk_persists_as_walk_and_remains_distinct_from_single()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Walk);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(new BatterResult?[] { BatterResult.Walk, BatterResult.Single },
            state!.PlateAppearances.Select(item => item.Result));
    }

    [Fact]
    public async Task Bases_loaded_away_walk_maps_score_and_survives_reload()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Walk)).IsSuccess);
        var reloaded = await db.CreateGameScoringSessionRepository()
            .GetStateForGameAsync(context.Game.Id);

        Assert.NotNull(reloaded);
        Assert.Null(reloaded.PendingPlay);
        Assert.Equal((0, 1), (reloaded.Session.HomeScore, reloaded.Session.VisitorScore));
        Assert.Equal(BatterResult.Walk, Assert.Single(reloaded.PlateAppearances).Result);
        Assert.Equal(ParseMovements("0:1,1:2,2:3,3:4"),
            MovementValues(reloaded.RunnerMovements));
        Assert.Equal(new[] { 1, 2, 3 }, reloaded.BaseRunners
            .OrderBy(item => item.Base).Select(item => (int)item.Base));
    }

    [Fact]
    public async Task Fielder_choice_is_rejected_without_an_existing_runner()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(PendingPlayFailure.InvalidRequest, selected.FailureReason);
        Assert.Null(state!.PendingPlay);
        Assert.Empty(state.PlateAppearances);
    }

    [Fact]
    public async Task Fielder_choice_runner_menu_uses_pre_play_participants_and_resolver_arrangements()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.Equal(RunnerDestination.First, pending.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.Play, pending.Movements, out var seed));
        var definition = PlateAppearanceResultDefinitions.Get(BatterResult.FielderChoice);
        var resolverDestinations = BaseRunnerResolver.GetLegalArrangements(seed!)
            .Where(definition.IsCompatibleWithArrangement)
            .Select(arrangement => arrangement.Participants.Single(item =>
                item.PlayerId == context.Players[1].Id).Destination)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[1].Id);

        Assert.True(choices.IsSuccess);
        Assert.Contains(RunnerDestination.Second, choices.Destinations);
        Assert.Equal(resolverDestinations, choices.Destinations);
    }

    [Fact]
    public async Task Safe_forced_runner_selection_resolves_outcomes_but_waits_for_next_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(adjusted.IsSuccess);
        Assert.NotNull(adjusted.PendingPlay);
        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal((0, 1), (pending.Session.CurrentOuts,
            pending.Session.CurrentBatterPosition));
        Assert.Equal(new[] { (ScoringBase.First, context.Players[1].Id) },
            pending.BaseRunners.Select(item => (item.Base, item.PlayerId)));
        Assert.Equal(RunnerDestination.Second, pending.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.Out, pending.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(RunnerDestination.Second, committed.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.Out, committed.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(1, committed.Session.CurrentOuts);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
        Assert.Equal(new[] { (ScoringBase.Second, context.Players[1].Id) },
            committed.BaseRunners.Select(item => (item.Base, item.PlayerId)));

        var duplicate = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var unchanged = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.False(duplicate.IsSuccess);
        Assert.Single(unchanged!.PlateAppearances);
        Assert.Equal((1, 2), (unchanged.Session.CurrentOuts,
            unchanged.Session.CurrentBatterPosition));
    }

    [Fact]
    public async Task Fielder_choice_runner_out_and_batter_first_commit_only_the_batter_on_first()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.First)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal(1, pending.Session.CurrentBatterPosition);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Null(committed.PendingPlay);
        Assert.Equal(new[] { (ScoringBase.First, context.Players[0].Id) },
            committed.BaseRunners.Select(item => (item.Base, item.PlayerId)));
    }

    [Fact]
    public async Task Current_half_outs_restore_normal_batters_in_recorded_order()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);

        var firstLoad = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var resumedLoad = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var expected = new[] { context.Players[0].Id, context.Players[1].Id };
        Assert.Equal(expected, firstLoad.GetCurrentHalfInningOutPlayerIds());
        Assert.Equal(expected, resumedLoad.GetCurrentHalfInningOutPlayerIds());
        var upcoming = resumedLoad.GetUpcomingPlayerIds();
        Assert.Equal(context.Players[0].Id, upcoming!.Value.OnDeckPlayerId);
        Assert.Equal(context.Players[1].Id, upcoming.Value.InTheHolePlayerId);
    }

    [Fact]
    public async Task Current_half_outs_do_not_deduplicate_repeated_player_identity()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(new[] { context.Players[0].Id, context.Players[0].Id },
            state.GetCurrentHalfInningOutPlayerIds());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Current_half_outs_use_actual_fielder_choice_retired_player(bool runnerIsOut)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);

        if (runnerIsOut)
        {
            Assert.True((await repository.AdjustPendingRunnerAsync(
                context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
            Assert.True((await repository.AdjustPendingRunnerAsync(
                context.Game.Id, context.Players[0].Id, RunnerDestination.First)).IsSuccess);
        }
        else
        {
            Assert.True((await repository.AdjustPendingRunnerAsync(
                context.Game.Id, context.Players[1].Id, RunnerDestination.Second)).IsSuccess);
        }

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(new[] { context.Players[runnerIsOut ? 1 : 0].Id },
            state.GetCurrentHalfInningOutPlayerIds());
    }

    [Fact]
    public async Task Current_half_outs_clear_after_third_out_and_reset_removes_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteOutAsync(context.Game.Id)).IsSuccess);
        var nextHalf = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(2, nextHalf.Session.CurrentInning);
        Assert.Empty(nextHalf.GetCurrentHalfInningOutPlayerIds());
        Assert.Equal(3, nextHalf.PlateAppearances.Count);

        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);
        Assert.Null(await repository.GetStateForGameAsync(context.Game.Id));
        Assert.Empty(await db.GetGamePlateAppearancesAsync());
        Assert.Empty(await db.GetPlateAppearanceRunnerMovementsAsync());
    }

    [Fact]
    public async Task Current_batter_visibility_derives_from_shared_pending_resolution_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");

        var unresolved = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.False(unresolved.IsPendingBatterResolved());

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var pendingHit = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(pendingHit.IsPendingBatterResolved());

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);
        var unresolvedFielderChoice =
            (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.False(unresolvedFielderChoice.IsPendingBatterResolved());

        await repository.SelectOutAsync(context.Game.Id);
        var pendingOut = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(pendingOut.IsPendingBatterResolved());
    }

    [Fact]
    public async Task Two_out_fielder_choice_requires_dedicated_runner_or_batter_selection()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        var ordinaryChoices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[1].Id);
        var ordinaryAdjustment = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        Assert.True(ordinaryChoices.IsSuccess);
        Assert.Empty(ordinaryChoices.Destinations);
        Assert.False(ordinaryAdjustment.IsSuccess);
        Assert.Equal(new[] { context.Players[1].Id, context.Players[0].Id },
            pending.Movements.OrderByDescending(item => item.StartingLocation)
                .Select(item => item.PlayerId));
        Assert.All(pending.Movements,
            item => Assert.True(item.RequiresOutcomeConfirmation));
    }

    [Fact]
    public async Task Multi_participant_choices_propagate_only_after_outcome_becomes_unique()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Third)).IsSuccess);
        var afterLead = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.False(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id).RequiresOutcomeConfirmation);
        Assert.True(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).RequiresOutcomeConfirmation);
        Assert.True(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).RequiresOutcomeConfirmation);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal(1, pending.Session.CurrentBatterPosition);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var resolved = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(resolved.PendingPlay);
        Assert.Equal(RunnerDestination.Out, resolved.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(resolved.PlateAppearances).Result);
        Assert.Equal(new[]
        {
            context.Players[2].Id,
            context.Players[1].Id,
            context.Players[0].Id,
        }, resolved.RunnerMovements.OrderByDescending(item => item.StartingLocation)
            .Select(item => item.PlayerId));
        Assert.Equal(new[]
        {
            (ScoringBase.Second, context.Players[1].Id),
            (ScoringBase.Third, context.Players[2].Id),
        }, resolved.BaseRunners.OrderBy(item => item.Base)
            .Select(item => (item.Base, item.PlayerId)));
    }

    [Fact]
    public async Task Resolved_fielder_choice_runner_can_continue_safely_before_next_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second)).IsSuccess);
        var atSecond = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(atSecond.PendingPlay);
        Assert.Empty(atSecond.PlateAppearances);
        Assert.Equal(1, atSecond.Session.CurrentBatterPosition);
        Assert.Contains(RunnerDestination.Third,
            (await repository.GetValidRunnerDestinationsAsync(
                context.Game.Id, context.Players[1].Id)).Destinations);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Third)).IsSuccess);
        var atThird = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(atThird.PendingPlay);
        Assert.Equal(RunnerDestination.Third, atThird.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.Out, atThird.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((1, 2), (committed.Session.CurrentOuts,
            committed.Session.CurrentBatterPosition));
        Assert.Equal((ScoringBase.Third, context.Players[1].Id),
            (Assert.Single(committed.BaseRunners).Base,
                Assert.Single(committed.BaseRunners).PlayerId));
        Assert.Equal(RunnerDestination.Third, committed.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
    }

    [Fact]
    public async Task Resolved_non_third_out_fielder_choice_survives_exit_and_resume_pending()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second);

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumedRepository = new GameScoringSessionRepository(resumedDatabase);
        var resumed = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;

        Assert.NotNull(resumed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice, resumed.PendingPlay.Play.Result);
        Assert.Equal(1, resumed.Session.CurrentBatterPosition);
        Assert.Empty(resumed.PlateAppearances);
        Assert.Equal(RunnerDestination.Second, resumed.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.All(resumed.PendingPlay.Movements,
            item => Assert.False(item.RequiresOutcomeConfirmation));
        Assert.True((await resumedRepository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Resolved_fielder_choice_that_creates_third_out_still_completes_immediately()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, "1,2");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
        var completed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Null(completed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(completed.PlateAppearances).Result);
        Assert.Equal((2, 0, 2), (completed.Session.CurrentInning,
            completed.Session.CurrentOuts, completed.Session.CurrentBatterPosition));
        Assert.Empty(completed.BaseRunners);
        Assert.Equal(2, completed.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Single(completed.HalfInnings);
    }

    [Fact]
    public async Task Scored_fielder_choice_runners_remain_editable_until_next_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id, RunnerDestination.Scores)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
        var beforeTerminalChoice = (await repository.GetStateForGameAsync(
            context.Game.Id))!;

        Assert.NotNull(beforeTerminalChoice.PendingPlay);
        Assert.Empty(beforeTerminalChoice.PlateAppearances);
        Assert.Contains(beforeTerminalChoice.PendingPlay.Movements, item =>
            item.PlayerId == context.Players[0].Id
            && item.RequiresOutcomeConfirmation);

        var retiredBatter = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Out);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(retiredBatter.IsSuccess);
        Assert.NotNull(retiredBatter.PendingPlay);
        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal((0, 1), (pending.Session.HomeScore,
            pending.Session.CurrentBatterPosition));
        Assert.Equal(new[] { context.Players[2].Id, context.Players[3].Id },
            pending.PendingPlay.Movements
                .Where(item => item.Destination == RunnerDestination.Scores)
                .Select(item => item.PlayerId).OrderBy(item => item));

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Third)).IsSuccess);
        var corrected = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(corrected.PendingPlay);
        Assert.Single(corrected.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Scores));
        Assert.Contains(corrected.PendingPlay.Movements, item =>
            item.PlayerId == context.Players[2].Id
            && item.Destination == RunnerDestination.Third);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((2, 2, 2), (committed.Session.HomeScore,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Empty(committed.BaseRunners);
        Assert.Equal(2, committed.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(2, committed.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Scores));
    }

    [Fact]
    public async Task Scored_fielder_choice_runner_survives_exit_and_resume_as_editable_pending()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,3");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id, RunnerDestination.Scores);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Out);

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumedRepository = new GameScoringSessionRepository(resumedDatabase);
        var resumed = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;

        Assert.NotNull(resumed.PendingPlay);
        Assert.Empty(resumed.PlateAppearances);
        Assert.Equal((0, 0, 1), (resumed.Session.VisitorScore,
            resumed.Session.CurrentOuts, resumed.Session.CurrentBatterPosition));
        var scored = Assert.Single(resumed.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Scores));
        Assert.Equal((context.Players[3].Id, RunnerStartingLocation.Third),
            (scored.PlayerId, scored.StartingLocation));
        Assert.True((await resumedRepository.AdjustPendingRunnerAsync(
            context.Game.Id, scored.PlayerId, RunnerDestination.Third)).IsSuccess);
        var corrected = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(corrected.PendingPlay);
        Assert.DoesNotContain(corrected.PendingPlay.Movements, item =>
            item.Destination == RunnerDestination.Scores);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Guided_resolution_does_not_guess_when_multiple_batter_outcomes_remain()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        var batter = pending.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id);
        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, batter.PlayerId);

        Assert.True(batter.RequiresOutcomeConfirmation);
        Assert.Equal(new[] { RunnerDestination.First, RunnerDestination.Out },
            choices.Destinations);
    }

    [Theory]
    [InlineData("2", 1)]
    [InlineData("1", 1)]
    [InlineData("0", 1)]
    [InlineData("2,1", 2)]
    public async Task Fielder_choice_guidance_supports_runner_batter_and_multiple_outs(
        string outParticipants, int expectedOuts)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2");

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True(selected.IsSuccess);
        Assert.All(selected.PendingPlay!.Movements,
            item => Assert.True(item.RequiresOutcomeConfirmation));
        var premature = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        Assert.False(premature.IsSuccess);

        var outStarts = outParticipants.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse).ToHashSet();
        foreach (var startingLocation in new[]
                 {
                     RunnerStartingLocation.Second,
                     RunnerStartingLocation.First,
                     RunnerStartingLocation.Home,
                 })
        {
            var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
            if (state.PendingPlay is null) break;
            var movement = state.PendingPlay.Movements.Single(item =>
                item.StartingLocation == startingLocation);
            var destination = outStarts.Contains((int)startingLocation)
                ? RunnerDestination.Out
                : startingLocation switch
                {
                    RunnerStartingLocation.Second => RunnerDestination.Third,
                    RunnerStartingLocation.First => RunnerDestination.Second,
                    RunnerStartingLocation.Home => RunnerDestination.First,
                    _ => throw new ArgumentOutOfRangeException(nameof(startingLocation)),
                };
            var choices = await repository.GetValidRunnerDestinationsAsync(
                context.Game.Id, movement.PlayerId);
            Assert.Contains(destination, choices.Destinations);
            Assert.True((await repository.AdjustPendingRunnerAsync(
                context.Game.Id, movement.PlayerId, destination)).IsSuccess);
        }

        var pending = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.NotNull(pending!.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal(1, pending.Session.CurrentBatterPosition);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Null(committed!.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(expectedOuts, committed!.Session.CurrentOuts);
        Assert.Equal(expectedOuts, committed.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(outStarts.Order(), committed.RunnerMovements
            .Where(item => item.Destination == RunnerDestination.Out)
            .Select(item => (int)item.StartingLocation).Order());
    }

    [Fact]
    public async Task Fielder_choice_selected_out_at_two_outs_completes_half_inning_immediately()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.CompleteFielderChoiceThirdOutAsync(
            context.Game.Id, context.Players[1].Id)).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Null(committed!.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((2, 0, 2), (committed.Session.CurrentInning,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Empty(committed.BaseRunners);
        Assert.Single(committed.HalfInnings);
        Assert.Single(committed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(context.Players[1].Id,
            Assert.Single(committed.RunnerMovements).PlayerId);

        var duplicate = await repository.CompleteFielderChoiceThirdOutAsync(
            context.Game.Id, context.Players[1].Id);
        Assert.False(duplicate.IsSuccess);
        Assert.Single((await repository.GetStateForGameAsync(context.Game.Id))!
            .PlateAppearances);
    }

    [Fact]
    public async Task Changing_fielder_choice_to_walk_rebuilds_defaults_from_committed_bases()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Walk);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Walk, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal(ParseMovements("0:1,1:2"),
            MovementValues(state.RunnerMovements));
        Assert.Equal(new[] { (1, context.Players[0].Id), (2, context.Players[1].Id) },
            state.BaseRunners.Select(item => ((int)item.Base, item.PlayerId)));
    }

    [Fact]
    public async Task Batter_can_be_selected_as_the_fielder_choice_third_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        var completed = await repository.CompleteFielderChoiceThirdOutAsync(
            context.Game.Id, context.Players[0].Id);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.FielderChoice, completed.PlateAppearance!.Result);
        Assert.Null(committed!.PendingPlay);
        Assert.Equal((2, 0, 2), (committed.Session.CurrentInning,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Empty(committed.BaseRunners);
        Assert.Single(committed.HalfInnings);
        var outMovement = Assert.Single(committed.RunnerMovements);
        Assert.Equal(context.Players[0].Id, outMovement.PlayerId);
        Assert.Equal(RunnerStartingLocation.Home, outMovement.StartingLocation);
        Assert.Equal(RunnerDestination.Out, outMovement.Destination);
    }

    [Fact]
    public async Task Bases_loaded_two_out_fielder_choice_lists_exact_participants_and_commits_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        Assert.True(selected.IsSuccess);
        Assert.Equal(new[]
        {
            (context.Players[3].Id, RunnerStartingLocation.Third),
            (context.Players[2].Id, RunnerStartingLocation.Second),
            (context.Players[1].Id, RunnerStartingLocation.First),
            (context.Players[0].Id, RunnerStartingLocation.Home),
        }, pending.Movements.OrderByDescending(item => item.StartingLocation)
            .Select(item => (item.PlayerId, item.StartingLocation)));
        Assert.All(pending.Movements,
            item => Assert.True(item.RequiresOutcomeConfirmation));
        Assert.Empty((await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[3].Id)).Destinations);

        var completed = await repository.CompleteFielderChoiceThirdOutAsync(
            context.Game.Id, context.Players[2].Id);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(context.Players[2].Id,
            Assert.Single(committed.RunnerMovements).PlayerId);
        Assert.Equal(RunnerDestination.Out,
            Assert.Single(committed.RunnerMovements).Destination);
        Assert.Null(committed.PendingPlay);
        Assert.Empty(committed.BaseRunners);
        Assert.Equal((2, 0, 2), (committed.Session.CurrentInning,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Single(committed.HalfInnings);

        Assert.False((await repository.CompleteFielderChoiceThirdOutAsync(
            context.Game.Id, context.Players[2].Id)).IsSuccess);
        Assert.Single((await repository.GetStateForGameAsync(context.Game.Id))!
            .PlateAppearances);
    }

    [Fact]
    public void Runner_destination_dialog_presents_the_selected_player_as_a_prominent_header()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dialogPath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "RunnerDestinationDialog.xaml");
        var codePath = dialogPath + ".cs";
        var dialog = XDocument.Load(dialogPath);
        var ns = dialog.Root!.Name.Namespace;
        var nameLabel = dialog.Descendants(ns + "Label").Single(item =>
            item.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml")
                + "Name")?.Value == "PlayerNameLabel");
        var code = File.ReadAllText(codePath);

        Assert.Equal("Center", nameLabel.Attribute("HorizontalTextAlignment")?.Value);
        Assert.Equal("Bold", nameLabel.Attribute("FontAttributes")?.Value);
        Assert.True(int.Parse(nameLabel.Attribute("FontSize")!.Value) >= 24);
        Assert.Contains("PlayerNameLabel.Text = playerName;", code);
        var pageCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "ScoringPage.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        Assert.Contains("new RunnerDestinationDialog(", pageCode);
        Assert.Contains("playerName, outcomes, isFielderChoiceResolution", pageCode);
        Assert.Contains("FormatRunnerOutcome(outcome)", code);
        Assert.Contains("!HasUnresolvedRunnerOutcomes", viewModelCode);
        Assert.DoesNotContain("OnCancelClicked", code);
        Assert.DoesNotContain("OnCancelClicked", File.ReadAllText(dialogPath));
    }

    [Fact]
    public void Base_and_third_out_presentation_expose_identity_without_empty_base_text()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var pagePath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "ScoringPage.xaml");
        var page = XDocument.Load(pagePath);
        var ns = page.Root!.Name.Namespace;
        var baseButtons = page.Descendants(ns + "Button").Where(item =>
            item.Attribute("Text")?.Value.Contains("BaseRunner.PlayerName") == true).ToArray();
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        var dialogCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "FielderChoiceThirdOutDialog.xaml.cs"));

        Assert.Equal(3, baseButtons.Length);
        Assert.All(baseButtons, button =>
        {
            var prefix = button.Attribute("Text")!.Value.Replace(".PlayerName}", string.Empty);
            Assert.Equal(prefix + ".IsOccupied}", button.Attribute("IsEnabled")?.Value);
            Assert.Equal(prefix + ".BackgroundColor}",
                button.Attribute("BackgroundColor")?.Value);
            Assert.Equal(prefix + ".BorderColor}", button.Attribute("BorderColor")?.Value);
        });
        Assert.DoesNotContain(page.Descendants(ns + "DataTrigger"), trigger =>
            trigger.Attribute("Binding")?.Value.EndsWith(".IsOccupied}") == true);
        Assert.Contains(": string.Empty;", viewModelCode);
        Assert.DoesNotContain(": AppResources.EmptyBaseLabel;", viewModelCode);
        Assert.Contains("public bool IsOccupied => PlayerId.HasValue;", viewModelCode);
        Assert.Contains("public string BackgroundColor => IsOccupied", viewModelCode);
        Assert.Contains("public string BorderColor => IsOccupied", viewModelCode);
        Assert.Contains("Text = participant.PlayerName", dialogCode);
        Assert.Contains("Text = participant.RoleLabel", dialogCode);
        Assert.Contains("CommandParameter = participant.PlayerId", dialogCode);
    }

    [Fact]
    public void Scoring_information_uses_resource_backed_portrait_panels_and_compact_landscape_rails()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var pagePath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "ScoringPage.xaml");
        var page = XDocument.Load(pagePath);
        var ns = page.Root!.Name.Namespace;
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var namedElements = page.Descendants().Where(item =>
                item.Attribute(x + "Name") is not null)
            .ToDictionary(item => item.Attribute(x + "Name")!.Value);
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));

        var portraitPanels = namedElements["PortraitInfoPanels"];
        Assert.Equal("*,*", portraitPanels.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(2, portraitPanels.Elements(ns + "Border").Count());
        Assert.Contains(page.Descendants(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.UpcomingHeading}");
        Assert.Contains(page.Descendants(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.OutsThisInningHeading}");
        Assert.Equal(2, page.Descendants().Count(item =>
            item.Attribute("BindableLayout.ItemsSource")?.Value == "{Binding OutsThisInning}"));
        Assert.Equal("False", namedElements["LandscapeUpcomingBattersList"]
            .Attribute("IsVisible")?.Value);
        Assert.Contains(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "LandscapeUpcomingBattersList"
            && item.Attribute("Property")?.Value == "IsVisible"
            && item.Attribute("Value")?.Value == "True");
        var landscapeOuts = namedElements["LandscapeOutsThisInning"];
        Assert.Contains(landscapeOuts.Descendants(), item =>
            item.Attribute("BindableLayout.ItemsSource")?.Value == "{Binding OutsThisInning}");
        Assert.DoesNotContain(landscapeOuts.Descendants(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.EmptyBaseLabel}");
        Assert.Equal("LandscapeScoredRail",
            landscapeOuts.Ancestors().First(item => item.Attribute(x + "Name") is not null)
                .Attribute(x + "Name")?.Value);
        Assert.Contains(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "LandscapeScoredRail"
            && item.Attribute("Property")?.Value == "Grid.Column"
            && item.Attribute("Value")?.Value == "4");
        Assert.DoesNotContain(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "CurrentBatterBorder"
            && item.Attribute("Property")?.Value == "IsVisible");
        Assert.Contains("RestoreOutsThisInning(state);", viewModelCode);
        Assert.Contains("state.GetCurrentHalfInningOutPlayerIds()", viewModelCode);
        Assert.Contains("SetPendingBatterResolved(state.IsPendingBatterResolved());",
            viewModelCode);
        Assert.Contains("OutsThisInning.Clear();", viewModelCode);
    }

    private static async Task SeedBasesAsync(
        TemporaryTestDatabase db,
        int sessionId,
        IReadOnlyList<Player> players,
        string occupiedBases)
    {
        foreach (var text in occupiedBases.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var baseNumber = int.Parse(text);
            await db.SeedBaseRunnerAsync(new()
            {
                GameScoringSessionId = sessionId,
                PlayerId = players[baseNumber].Id,
                Base = (ScoringBase)baseNumber,
            });
        }
    }

    private static IReadOnlyList<(int Start, int Destination)> ParseMovements(
        string movements) => movements
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Split(':'))
        .Select(item => (int.Parse(item[0]), int.Parse(item[1])))
        .OrderBy(item => item.Item1)
        .ToArray();

    private static IReadOnlyList<(int Start, int Destination)> MovementValues(
        IEnumerable<GameScoringPendingRunnerMovement> movements) => movements
        .Select(item => ((int)item.StartingLocation, (int)item.Destination))
        .OrderBy(item => item.Item1)
        .ToArray();

    private static IReadOnlyList<(int Start, int Destination)> MovementValues(
        IEnumerable<GamePlateAppearanceRunnerMovement> movements) => movements
        .Select(item => ((int)item.StartingLocation, (int)item.Destination))
        .OrderBy(item => item.Item1)
        .ToArray();

    private static async Task<(Team Team, Game Game, IReadOnlyList<Player> Players)>
        CreateReadyGameAsync(TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Season", Year = 2026 };
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
            players.Add(player);
        }
        return (team, game, players);
    }
}
