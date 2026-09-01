using System.Xml.Linq;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class WalkFielderChoiceScoringTests
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
    public async Task Fielder_choice_single_runner_still_offers_runner_and_batter_retirements()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        var pendingState = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(PlayDecisionRequirement.RetirementSelection,
            pendingState.PlayResolution!.DecisionRequirement);
        Assert.Equal(new[] { context.Players[1].Id, context.Players[0].Id },
            pendingState.PlayResolution.DecisionPlayerIds);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);
        pendingState = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var pending = pendingState.PendingPlay!;
        Assert.Equal(PlayDecisionRequirement.None,
            pendingState.PlayResolution!.DecisionRequirement);
        Assert.Empty(pendingState.PlayResolution.DecisionPlayerIds);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pendingState.PlayResolution.Status);
        var batter = pending.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        Assert.Equal(RunnerDestination.First, batter.Destination);
        Assert.False(batter.RequiresOutcomeConfirmation);
        var retired = pending.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id);
        Assert.Equal(RunnerDestination.Out, retired.Destination);
        Assert.False(retired.RequiresOutcomeConfirmation);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(RunnerDestination.Out,
            committed.RunnerMovements.Single(item =>
                item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.First,
            committed.RunnerMovements.Single(item =>
                item.PlayerId == context.Players[0].Id).Destination);
        Assert.Equal((1, 2), (committed.Session.CurrentOuts,
            committed.Session.CurrentBatterPosition));
    }

    [Fact]
    public async Task Fielder_choice_batter_primary_out_reconciles_and_keeps_runner_safe()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        var selected = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[0].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))]);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.Equal(BatterResult.Out, pending.PendingPlay!.Play.Result);
        Assert.DoesNotContain(pending.PendingPlay.Movements, item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        var runner = Assert.Single(pending.PendingPlay.Movements);
        Assert.Equal(context.Players[1].Id, runner.PlayerId);
        Assert.Equal(RunnerDestination.First, runner.Destination);
        Assert.Empty(pending.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.Out,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(1, committed.Session.CurrentOuts);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
    }

    [Theory]
    [InlineData(RunnerDestination.Second)]
    [InlineData(RunnerDestination.Third)]
    [InlineData(RunnerDestination.Scores)]
    public async Task Fielder_choice_batter_runner_can_advance_beyond_first_and_remains_FC(
        RunnerDestination destination)
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
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[0].Id);
        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, destination);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(new[]
        {
            RunnerDestination.First,
            RunnerDestination.Second,
            RunnerDestination.Third,
            RunnerDestination.Scores,
        }, choices.Destinations);
        Assert.True(adjusted.IsSuccess);
        Assert.Equal(BatterResult.FielderChoice, pending.PendingPlay!.Play.Result);
        Assert.Equal(destination, pending.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(destination, committed.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[0].Id).Destination);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
    }

    [Fact]
    public async Task Multi_runner_fielder_choice_batter_menu_matches_resolver_legality()
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
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.PendingPlay!.Play, pending.PendingPlay.Movements, out var state));

        var expected = BaseRunnerResolver.GetAvailableDestinations(
                state!, context.Players[0].Id)
            .Where(item => item != RunnerDestination.Out)
            .ToArray();
        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[0].Id);

        Assert.Equal(expected, choices.Destinations);
        Assert.DoesNotContain(RunnerDestination.Out, choices.Destinations);
        Assert.Contains(RunnerDestination.Third, choices.Destinations);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id,
            RunnerDestination.Third)).IsSuccess);
        var corrected = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            corrected.PendingPlay!.Play,
            corrected.PendingPlay.Movements,
            out var correctedState));
        Assert.True(BaseRunnerResolver.IsLegalArrangement(correctedState!));
    }

    [Fact]
    public async Task Fielder_choice_batter_advanced_to_third_survives_resume_and_commits_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id,
            RunnerDestination.Third)).IsSuccess);

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumedRepository = new GameScoringSessionRepository(resumedDatabase);
        var resumed = (await resumedRepository.GetStateForGameAsync(
            context.Game.Id))!;

        Assert.Equal(BatterResult.FielderChoice, resumed.PendingPlay!.Play.Result);
        Assert.Equal(RunnerDestination.Third,
            resumed.PendingPlay.Movements.Single(item =>
                item.PlayerId == context.Players[0].Id).Destination);
        Assert.True((await resumedRepository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await resumedRepository.GetStateForGameAsync(
            context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(RunnerDestination.Third,
            Assert.Single(committed.RunnerMovements.Where(item =>
                item.PlayerId == context.Players[0].Id)).Destination);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
        Assert.False((await resumedRepository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Selected_fielder_choice_primary_out_survives_exit_and_resume_pending()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);
        Assert.True(selected.IsSuccess);

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumed = (await new GameScoringSessionRepository(resumedDatabase)
            .GetStateForGameAsync(context.Game.Id))!;

        Assert.NotNull(resumed.PendingPlay);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            resumed.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            resumed.PlayResolution.DecisionRequirement);
        Assert.Equal(RunnerDestination.Out,
            resumed.PendingPlay.Movements.Single(item =>
                item.PlayerId == context.Players[1].Id).Destination);
        Assert.Equal(RunnerDestination.First,
            resumed.PendingPlay.Movements.Single(item =>
                item.PlayerId == context.Players[0].Id).Destination);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Batter_out_with_surviving_runner_advancement_uses_out_workflow()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectOutAsync(context.Game.Id);

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
        Assert.DoesNotContain(pending.PendingPlay.Movements, item =>
            item.PlayerId == context.Players[0].Id);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pending.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            pending.PlayResolution.DecisionRequirement);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(RunnerDestination.Second, committed.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
        Assert.DoesNotContain(committed.RunnerMovements, item =>
            item.PlayerId == context.Players[0].Id);
        Assert.Equal(BatterResult.Out,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(1, committed.Session.CurrentOuts);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
        Assert.Null(committed.PlayResolution);
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

    [Fact]
    public async Task Current_half_outs_use_actual_existing_fielder_choice_retiree()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(new[] { context.Players[1].Id },
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
        Assert.True(unresolvedFielderChoice.IsPendingBatterResolved());

        await repository.SelectOutAsync(context.Game.Id);
        var pendingOut = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(pendingOut.IsPendingBatterResolved());
    }

    [Fact]
    public async Task Two_out_fielder_choice_with_one_runner_retires_that_runner_not_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.NotNull(selected.PendingPlay);
        var retirement = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[1].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))]);
        var completed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(selected.IsSuccess);
        Assert.True(retirement.IsSuccess);
        Assert.Null(completed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(completed.PlateAppearances).Result);
        var retired = Assert.Single(completed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(context.Players[1].Id, retired.PlayerId);
        var batter = completed.RunnerMovements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        Assert.Equal(context.Players[0].Id, batter.PlayerId);
        Assert.Equal(RunnerDestination.First, batter.Destination);
        Assert.Equal((2, 0, 2), (completed.Session.CurrentInning,
            completed.Session.CurrentOuts, completed.Session.CurrentBatterPosition));
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
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Third)).IsSuccess);
        var afterLead = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.False(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id).RequiresOutcomeConfirmation);
        Assert.False(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id).RequiresOutcomeConfirmation);
        Assert.False(afterLead.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id).RequiresOutcomeConfirmation);

        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal(1, pending.Session.CurrentBatterPosition);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var resolved = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(resolved.PendingPlay);
        Assert.Equal(RunnerDestination.First, resolved.RunnerMovements.Single(item =>
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
            (ScoringBase.First, context.Players[0].Id),
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
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out)).IsSuccess);
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
            item.PlayerId == context.Players[2].Id).Destination);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((1, 2), (committed.Session.CurrentOuts,
            committed.Session.CurrentBatterPosition));
        Assert.Equal(new[]
        {
            (ScoringBase.First, context.Players[0].Id),
            (ScoringBase.Third, context.Players[1].Id),
        }, committed.BaseRunners.OrderBy(item => item.Base)
            .Select(item => (item.Base, item.PlayerId)));
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
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out);

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
    public async Task Fielder_choice_rejects_a_second_ordinary_primary_out()
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
        var secondOut = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);
        var completed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.False(secondOut.IsSuccess);
        Assert.NotNull(completed.PendingPlay);
        Assert.Empty(completed.PlateAppearances);
        Assert.Equal((1, 1, 1), (completed.Session.CurrentInning,
            completed.Session.CurrentOuts, completed.Session.CurrentBatterPosition));
        Assert.Single(completed.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Empty(completed.HalfInnings);
    }

    [Theory]
    [InlineData("1,2,3", 1, "0:1,1:5,2:3,3:4")]
    [InlineData("1,2", 1, "0:1,1:5,2:3")]
    [InlineData("1,3", 1, "0:1,1:5,3:3")]
    [InlineData("2,3", 2, "0:1,2:5,3:4")]
    [InlineData("2", 2, "0:1,2:5")]
    [InlineData("3", 3, "0:1,3:5")]
    public async Task Fielder_choice_defaults_only_retirement_displacement_chain(
        string occupiedBases,
        int retiredStartingBase,
        string expectedMovements)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, occupiedBases);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);

        var retirement = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[retiredStartingBase].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))]);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(retirement.IsSuccess);
        Assert.Equal(BatterResult.FielderChoice, pending.PendingPlay!.Play.Result);
        Assert.Equal(ParseMovements(expectedMovements),
            MovementValues(pending.PendingPlay.Movements));
        Assert.Single(pending.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Out));
        var retired = pending.PendingPlay.Movements.Single(item =>
            item.StartingLocation == (RunnerStartingLocation)retiredStartingBase);
        Assert.Null(retired.SafeDestination);
        Assert.Null(retired.AttemptedDestination);
    }

    [Fact]
    public async Task Corrected_fielder_choice_displacement_defaults_survive_exit_and_resume()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[1].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))])).IsSuccess);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id,
            RunnerDestination.Third)).IsSuccess);
        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumedRepository = new GameScoringSessionRepository(resumedDatabase);
        var resumed = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(BatterResult.FielderChoice, resumed.PendingPlay!.Play.Result);
        Assert.Equal(RunnerDestination.First, resumed.PendingPlay.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home).Destination);
        Assert.Equal(RunnerDestination.Out, resumed.PendingPlay.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.First).Destination);
        Assert.Equal(RunnerDestination.Second, resumed.PendingPlay.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Second).Destination);
        Assert.Equal(RunnerDestination.Third, resumed.PendingPlay.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Third).Destination);
        Assert.Single(resumed.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.True((await resumedRepository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        await resumedDatabase.CloseAsync();
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
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Out)).IsSuccess);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id, RunnerDestination.Scores)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores)).IsSuccess);
        var beforeTerminalChoice = (await repository.GetStateForGameAsync(
            context.Game.Id))!;

        Assert.NotNull(beforeTerminalChoice.PendingPlay);
        Assert.Empty(beforeTerminalChoice.PlateAppearances);
        Assert.Contains(beforeTerminalChoice.PendingPlay.Movements, item =>
            item.PlayerId == context.Players[0].Id
            && !item.RequiresOutcomeConfirmation
            && item.Destination == RunnerDestination.First);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            beforeTerminalChoice.PlayResolution!.Status);

        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

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
        Assert.Equal((2, 1, 2), (committed.Session.HomeScore,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Equal((ScoringBase.First, context.Players[0].Id),
            (Assert.Single(committed.BaseRunners).Base,
                Assert.Single(committed.BaseRunners).PlayerId));
        Assert.Single(committed.RunnerMovements.Where(item =>
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
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            resumed.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            resumed.PlayResolution.DecisionRequirement);
        Assert.True((await resumedRepository.AdjustPendingRunnerAsync(
            context.Game.Id, scored.PlayerId, RunnerDestination.Third)).IsSuccess);
        var corrected = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(corrected.PendingPlay);
        Assert.DoesNotContain(corrected.PendingPlay.Movements, item =>
            item.Destination == RunnerDestination.Scores);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Guided_resolution_stops_after_normal_fielder_choice_retirement_is_known()
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
        var pendingState = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var pending = pendingState.PendingPlay!;
        var batter = pending.Movements.Single(item =>
            item.PlayerId == context.Players[0].Id);
        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, batter.PlayerId);

        Assert.False(batter.RequiresOutcomeConfirmation);
        Assert.All(pending.Movements,
            item => Assert.False(item.RequiresOutcomeConfirmation));
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pendingState.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            pendingState.PlayResolution.DecisionRequirement);
        Assert.Single(pending.Movements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(new[]
        {
            RunnerDestination.First,
            RunnerDestination.Second,
            RunnerDestination.Third,
            RunnerDestination.Scores,
        }, choices.Destinations);
        Assert.DoesNotContain(RunnerDestination.Out, choices.Destinations);
    }

    [Theory]
    [InlineData("2", 1)]
    [InlineData("1", 1)]
    public async Task Fielder_choice_primary_out_selection_supports_each_existing_runner(
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
        Assert.All(selected.PendingPlay!.Movements.Where(item =>
                item.StartingLocation != RunnerStartingLocation.Home),
            item => Assert.True(item.RequiresOutcomeConfirmation));
        var batter = selected.PendingPlay.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        Assert.Equal(RunnerDestination.First, batter.Destination);
        Assert.False(batter.RequiresOutcomeConfirmation);
        var premature = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        Assert.False(premature.IsSuccess);

        var outStarts = outParticipants.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse).ToHashSet();
        var primaryStart = outStarts.Single();
        var primary = selected.PendingPlay.Movements.Single(item =>
            (int)item.StartingLocation == primaryStart);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(primary.PlayerId,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))])).IsSuccess);
        foreach (var startingLocation in new[]
                 {
                     RunnerStartingLocation.Second,
                     RunnerStartingLocation.First,
                 })
        {
            var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
            if (state.PendingPlay is null) break;
            var movement = state.PendingPlay.Movements.Single(item =>
                item.StartingLocation == startingLocation);
            if ((int)startingLocation == primaryStart) continue;
            var destination = outStarts.Contains((int)startingLocation)
                ? RunnerDestination.Out
                : startingLocation switch
                {
                    RunnerStartingLocation.Second => RunnerDestination.Third,
                    RunnerStartingLocation.First => RunnerDestination.Second,
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
    public async Task Two_out_fielder_choice_completes_half_inning_without_pending_decision()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.NotNull(selected.PendingPlay);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[1].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))])).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Null(committed!.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((2, 0, 2), (committed.Session.CurrentInning,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Empty(committed.BaseRunners);
        Assert.Single(committed.HalfInnings);
        Assert.Single(committed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(2, committed.RunnerMovements.Count);

        var duplicate = await repository.CompletePlateAppearanceAsync(
            context.Game.Id);
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
    public async Task Bases_loaded_two_out_fielder_choice_selects_existing_retiree_once()
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

        Assert.True(selected.IsSuccess);
        Assert.NotNull(selected.PendingPlay);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(PlayDecisionRequirement.RetirementSelection,
            pending.PlayResolution!.DecisionRequirement);
        Assert.Equal(1, pending.PlayResolution.RequiredRetirementCount);
        Assert.Equal(new[]
        {
            context.Players[3].Id,
            context.Players[2].Id,
            context.Players[1].Id,
            context.Players[0].Id,
        }, pending.PlayResolution.DecisionPlayerIds);

        var retirement = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[3].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))]);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.True(retirement.IsSuccess);
        Assert.Null(retirement.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(committed.PlateAppearances).Result);
        var retired = Assert.Single(committed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(context.Players[3].Id, retired.PlayerId);
        var batterMovement = committed.RunnerMovements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        Assert.Equal(RunnerDestination.First, batterMovement.Destination);
        Assert.Equal(4, committed.RunnerMovements.Count);
        Assert.Null(committed.PendingPlay);
        Assert.Empty(committed.BaseRunners);
        Assert.Equal((2, 0, 2), (committed.Session.CurrentInning,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Single(committed.HalfInnings);

        Assert.False((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var resumed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(resumed.PendingPlay);
        Assert.Single(resumed.PlateAppearances);
        Assert.Equal((2, 0, 2), (resumed.Session.CurrentInning,
            resumed.Session.CurrentOuts, resumed.Session.CurrentBatterPosition));
        Assert.Single(resumed.HalfInnings);
    }

    [Fact]
    public async Task Fielder_choice_batter_out_reconciles_canonical_pending_state_and_resumes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,3");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.FielderChoice);
        var result = await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[0].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))]);
        Assert.True(result.IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id,
            RunnerDestination.Second)).IsSuccess);
        var attempted = new RunnerOutcomeChoice(RunnerDestination.Out,
            RunnerDestination.Second, RunnerDestination.Third);
        await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[1].Id, attempted);

        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.True(result.IsSuccess);
        Assert.Equal(BatterResult.Out, pending.Play.Result);
        Assert.DoesNotContain(pending.Movements, item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        var preserved = pending.Movements.Single(item =>
            item.PlayerId == context.Players[1].Id);
        Assert.Equal((RunnerDestination.Out, RunnerDestination.Second,
                RunnerDestination.Third),
            (preserved.Destination, preserved.SafeDestination,
                preserved.AttemptedDestination));
        Assert.All(pending.Movements, item => Assert.Null(item.OutOrder));

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumedRepository = new GameScoringSessionRepository(resumedDatabase);
        var resumed = (await resumedRepository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.Out, resumed.PendingPlay!.Play.Result);
        Assert.True((await resumedRepository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        Assert.Equal(BatterResult.Out, Assert.Single((await resumedRepository
            .GetStateForGameAsync(context.Game.Id))!.PlateAppearances).Result);
        await resumedDatabase.CloseAsync();
    }

    [Fact]
    public void Scoring_presentation_reacts_to_authoritative_generic_resolution_state()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var stateCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper.Infrastructure", "Data", "Repositories",
            "GameScoringState.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        var pageCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "ScoringPage.xaml.cs"));

        Assert.Contains("PlayResolutionResult? PlayResolution", stateCode);
        Assert.Contains("state.PlayResolution", viewModelCode);
        Assert.Contains("public PlayResolutionStatus ResolutionStatus", viewModelCode);
        Assert.Contains("public PlayDecisionRequirement DecisionRequirement", viewModelCode);
        Assert.Contains("ProcessCurrentPlayResolutionAsync", pageCode);
        Assert.Contains("switch (viewModel.DecisionRequirement)", pageCode);
        Assert.DoesNotContain("RequiresFielderChoiceThirdOutSelection", viewModelCode);
        Assert.DoesNotContain("PendingFielderChoiceCompletesHalfInning", viewModelCode);
        Assert.DoesNotContain("RequiresMultiOutRetirementSelection", viewModelCode);
        Assert.DoesNotContain("if (result is BatterResult.DoublePlay", pageCode);
        Assert.DoesNotContain("if (result == BatterResult.FielderChoice)", pageCode);
    }

    [Fact]
    public void Runner_destination_dialog_presents_player_and_immutable_starting_location()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dialogPath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "RunnerDestinationDialog.xaml");
        var codePath = dialogPath + ".cs";
        var dialog = XDocument.Load(dialogPath);
        var ns = dialog.Root!.Name.Namespace;
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var nameLabel = dialog.Descendants(ns + "Label").Single(item =>
            item.Attribute(x + "Name")?.Value == "PlayerNameLabel");
        var startingLocationLabel = dialog.Descendants(ns + "Label").Single(item =>
            item.Attribute(x + "Name")?.Value == "StartingLocationLabel");
        var currentOutcomeLabel = dialog.Descendants(ns + "Label").Single(item =>
            item.Attribute(x + "Name")?.Value == "CurrentOutcomeLabel");
        var cancelButton = dialog.Descendants(ns + "Button").Single(item =>
            item.Attribute("AutomationId")?.Value == "RunnerDestinationCancelButton");
        var code = File.ReadAllText(codePath);

        Assert.Equal("Center", nameLabel.Attribute("HorizontalTextAlignment")?.Value);
        Assert.Equal("Bold", nameLabel.Attribute("FontAttributes")?.Value);
        Assert.True(int.Parse(nameLabel.Attribute("FontSize")!.Value) >= 24);
        Assert.Contains("PlayerNameLabel.Text = playerName;", code);
        Assert.Equal("Center",
            startingLocationLabel.Attribute("HorizontalTextAlignment")?.Value);
        Assert.Contains("StartingLocationLabel.Text = startingLocationLabel;", code);
        Assert.Equal("RunnerCurrentOutcomeLabel",
            currentOutcomeLabel.Attribute("AutomationId")?.Value);
        Assert.Equal("Bold", currentOutcomeLabel.Attribute("FontAttributes")?.Value);
        Assert.Equal("{x:Static strings:AppResources.CancelButton}",
            cancelButton.Attribute("Text")?.Value);
        var pageCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "ScoringPage.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        Assert.Contains("new RunnerDestinationDialog(", pageCode);
        Assert.Contains("startingLocationLabel", pageCode);
        Assert.Contains("participant.RoleLabel", pageCode);
        Assert.Contains("FormatRunnerOutcome(outcome)", code);
        Assert.Contains("CurrentRunnerOutcomeFormat", code);
        Assert.Contains("GetRunnerOutcomeMenuAsync", pageCode);
        Assert.Contains("RunnerDestinationSecondButton", code);
        Assert.Contains("RunnerOutAttemptingHomeButton", code);
        Assert.Contains("ResolutionStatus == PlayResolutionStatus.ReadyForNextBatter",
            viewModelCode);
        Assert.Contains("OnCancelClicked", code);
        Assert.Contains("OnCancelClicked", File.ReadAllText(dialogPath));
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
        Assert.Equal(
            new[]
            {
                "FirstBaseRunnerButton",
                "SecondBaseRunnerButton",
                "ThirdBaseRunnerButton",
            },
            baseButtons.Select(button => button.Attribute("AutomationId")?.Value)
                .OrderBy(value => value));
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
    public void Scoring_information_uses_shared_portrait_and_landscape_undo_redo_panels()
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
        Assert.Equal("Auto,Auto", portraitPanels.Attribute("RowDefinitions")?.Value);
        var upcomingPanel = namedElements["UpcomingInfoPanel"];
        var outsPanel = namedElements["OutsInfoPanel"];
        Assert.Equal(portraitPanels, upcomingPanel.Parent);
        Assert.Equal(portraitPanels, outsPanel.Parent);
        Assert.Equal(upcomingPanel.Attribute("Grid.Row")?.Value,
            outsPanel.Attribute("Grid.Row")?.Value);
        Assert.Contains(page.Descendants(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.UpcomingHeading}");
        Assert.Contains(page.Descendants(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.OutsThisInningHeading}");
        Assert.Equal(1, page.Descendants().Count(item =>
            item.Attribute("BindableLayout.ItemsSource")?.Value == "{Binding OutsThisInning}"));
        Assert.Contains(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "PortraitInfoPanels"
            && item.Attribute("Property")?.Value == "ColumnDefinitions"
            && item.Attribute("Value")?.Value == "210,*,240");
        Assert.Contains(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "OutsInfoPanel"
            && item.Attribute("Property")?.Value == "Grid.Column"
            && item.Attribute("Value")?.Value == "2");
        Assert.Contains(page.Descendants(ns + "Setter"), item =>
            item.Attribute("TargetName")?.Value == "PortraitInfoPanels"
            && item.Attribute("Property")?.Value == "RowDefinitions"
            && item.Attribute("Value")?.Value == "Auto,*,Auto");
        Assert.All(new[] { "UndoScoringButton", "RedoScoringButton" }, buttonName =>
            Assert.Contains(page.Descendants(ns + "Setter"), item =>
                item.Attribute("TargetName")?.Value == buttonName
                && item.Attribute("Property")?.Value == "Grid.Row"
                && item.Attribute("Value")?.Value == "2"));
        Assert.Equal("UndoScoringButton", portraitPanels.Descendants(ns + "Button")
            .Single(item => item.Attribute("AutomationId")?.Value == "UndoScoringButton")
            .Attribute("AutomationId")?.Value);
        Assert.Equal("RedoScoringButton", portraitPanels.Descendants(ns + "Button")
            .Single(item => item.Attribute("AutomationId")?.Value == "RedoScoringButton")
            .Attribute("AutomationId")?.Value);
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
