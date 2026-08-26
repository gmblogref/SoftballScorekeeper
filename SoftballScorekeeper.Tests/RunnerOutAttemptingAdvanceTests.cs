using SQLite;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class RunnerOutAttemptingAdvanceTests
{
    [Theory]
    [InlineData(RunnerStartingLocation.Home, RunnerDestination.First,
        RunnerDestination.Second)]
    [InlineData(RunnerStartingLocation.First, RunnerDestination.Second,
        RunnerDestination.Third)]
    [InlineData(RunnerStartingLocation.Second, RunnerDestination.Third,
        RunnerDestination.Scores)]
    public void Resolver_exposes_only_the_immediate_advance_after_a_safe_destination(
        RunnerStartingLocation startingLocation,
        RunnerDestination safeDestination,
        RunnerDestination attemptedDestination)
    {
        var participants = new List<BaseRunnerParticipant>
        {
            new(10, startingLocation, safeDestination),
        };
        if (startingLocation != RunnerStartingLocation.Home)
            participants.Add(new(11, RunnerStartingLocation.Home,
                RunnerDestination.First));
        var state = new BaseRunnerResolutionState(participants);

        var available = BaseRunnerResolver.TryGetOutAttemptingAdvance(
            state, 10, out var outcome);

        Assert.True(available);
        Assert.Equal(new RunnerOutcomeChoice(
            RunnerDestination.Out, safeDestination, attemptedDestination), outcome);
    }

    [Theory]
    [InlineData(RunnerStartingLocation.First, RunnerDestination.First,
        RunnerDestination.Second)]
    [InlineData(RunnerStartingLocation.Second, RunnerDestination.Third,
        RunnerDestination.Second)]
    [InlineData(RunnerStartingLocation.Home, RunnerDestination.Second,
        RunnerDestination.Scores)]
    [InlineData(RunnerStartingLocation.Third, RunnerDestination.Scores,
        RunnerDestination.Out)]
    public void Invalid_or_fictional_attempted_advance_relationships_are_rejected(
        RunnerStartingLocation startingLocation,
        RunnerDestination safeDestination,
        RunnerDestination attemptedDestination)
    {
        var outcome = new RunnerOutcomeChoice(
            RunnerDestination.Out, safeDestination, attemptedDestination);

        Assert.False(outcome.IsValidFor(startingLocation));
    }

    [Fact]
    public async Task Single_batter_attempting_second_preserves_safe_history_and_counts_one_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var selectedState = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            selectedState.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            selectedState.PlayResolution.DecisionRequirement);

        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[0].Id);
        var attempted = Assert.Single(choices.Choices.Where(item =>
            item.IsOutAttemptingAdvance));
        Assert.Equal((RunnerDestination.First, RunnerDestination.Second),
            (attempted.SafeDestination, attempted.AttemptedDestination));
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[0].Id, attempted)).IsSuccess);

        var pending = await repository.GetStateForGameAsync(context.Game.Id);
        var pendingMovement = Assert.Single(pending!.PendingPlay!.Movements);
        AssertMovement(pendingMovement, context.Players[0].Id,
            RunnerStartingLocation.Home, RunnerDestination.First,
            RunnerDestination.Second);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pending.PlayResolution!.Status);
        Assert.Empty(pending.BaseRunners);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Single, completed.PlateAppearance!.Result);
        Assert.Equal(1, state!.Session.CurrentOuts);
        Assert.Empty(state.BaseRunners);
        AssertMovement(Assert.Single(state.RunnerMovements), context.Players[0].Id,
            RunnerStartingLocation.Home, RunnerDestination.First,
            RunnerDestination.Second);
        Assert.Equal(new[] { context.Players[0].Id },
            state.GetCurrentHalfInningOutPlayerIds());
        Assert.Null(state.PlayResolution);
    }

    [Fact]
    public async Task Existing_runner_attempting_third_is_removed_while_other_bases_commit()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1].Id, ScoringBase.First);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);

        var attempted = Assert.Single((await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[1].Id)).Choices.Where(item =>
            item.IsOutAttemptingAdvance));
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[1].Id, attempted)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(BatterResult.Single, Assert.Single(state!.PlateAppearances).Result);
        Assert.Equal(1, state.Session.CurrentOuts);
        var batter = Assert.Single(state.BaseRunners);
        Assert.Equal((context.Players[0].Id, ScoringBase.First),
            (batter.PlayerId, batter.Base));
        AssertMovement(state.RunnerMovements.Single(item =>
                item.PlayerId == context.Players[1].Id),
            context.Players[1].Id, RunnerStartingLocation.First,
            RunnerDestination.Second, RunnerDestination.Third);
    }

    [Fact]
    public async Task Runner_attempting_home_can_make_third_out_without_inventing_run_timing()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 4, 2);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2].Id, ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3].Id, ScoringBase.Third);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);

        var attempted = Assert.Single((await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id)).Choices.Where(item =>
            item.IsOutAttemptingAdvance));
        Assert.Equal(RunnerDestination.Scores, attempted.AttemptedDestination);
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[2].Id, attempted)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(PlayDecisionRequirement.ThirdOutRuling,
            pending.PlayResolution!.DecisionRequirement);
        Assert.Equal(new[] { context.Players[3].Id },
            pending.PlayResolution.DecisionPlayerIds);
        var required = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.Equal(new[] { context.Players[3].Id }, required.PlayerIds);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id,
            new Dictionary<int, bool> { [context.Players[3].Id] = true });
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal((5, 0, 1), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.HomeScore));
        Assert.Empty(state.BaseRunners);
        AssertMovement(state.RunnerMovements.Single(item =>
                item.PlayerId == context.Players[2].Id),
            context.Players[2].Id, RunnerStartingLocation.Second,
            RunnerDestination.Third, RunnerDestination.Scores);
        Assert.Equal(new[] { context.Players[2].Id },
            state.RunnerMovements.Where(item => item.Destination == RunnerDestination.Out)
                .Select(item => item.PlayerId));
    }

    [Fact]
    public async Task Pending_and_committed_attempted_advance_survive_database_reopen()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var attempted = Assert.Single((await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[0].Id)).Choices.Where(item =>
            item.IsOutAttemptingAdvance));
        await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[0].Id, attempted);

        await db.Database.CloseAsync();
        var resumedDatabase = new AppDatabase(db.DatabasePath);
        var resumed = new GameScoringSessionRepository(resumedDatabase);
        var pending = await resumed.GetStateForGameAsync(context.Game.Id);
        AssertMovement(Assert.Single(pending!.PendingPlay!.Movements),
            context.Players[0].Id, RunnerStartingLocation.Home,
            RunnerDestination.First, RunnerDestination.Second);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pending.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None,
            pending.PlayResolution.DecisionRequirement);

        Assert.True((await resumed.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        await resumedDatabase.CloseAsync();
        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        var reopened = await new GameScoringSessionRepository(reopenedDatabase)
            .GetStateForGameAsync(context.Game.Id);
        AssertMovement(Assert.Single(reopened!.RunnerMovements),
            context.Players[0].Id, RunnerStartingLocation.Home,
            RunnerDestination.First, RunnerDestination.Second);
        await reopenedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Ordinary_out_and_walk_history_remain_unreinterpreted()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[2].Id, ScoringBase.Second);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out);
        await repository.CompletePlateAppearanceAsync(context.Game.Id);

        var ordinary = (await repository.GetStateForGameAsync(context.Game.Id))!
            .RunnerMovements.Single(item => item.PlayerId == context.Players[2].Id);
        Assert.Equal(RunnerDestination.Out, ordinary.Destination);
        Assert.Null(ordinary.SafeDestination);
        Assert.Null(ordinary.AttemptedDestination);

        var walk = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Walk);
        var state = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.True(walk.IsSuccess);
        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Walk, state.PlateAppearances.Last().Result);
        Assert.All(state.RunnerMovements.Where(item =>
            item.GamePlateAppearanceId == state.PlateAppearances.Last().Id), item =>
        {
            Assert.Null(item.SafeDestination);
            Assert.Null(item.AttemptedDestination);
        });
    }

    [Fact]
    public async Task Pre_play_base_is_not_treated_as_a_safe_advance_and_forged_choice_is_rejected()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[2].Id, ScoringBase.First);
        await repository.SelectOutAsync(context.Game.Id);

        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id);
        Assert.DoesNotContain(choices.Choices, item => item.IsOutAttemptingAdvance);
        var forged = new RunnerOutcomeChoice(RunnerDestination.Out,
            RunnerDestination.First, RunnerDestination.Second);
        var rejected = await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[2].Id, forged);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(PendingPlayFailure.InvalidRequest, rejected.FailureReason);
        var movement = state!.PendingPlay!.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id);
        Assert.Equal(RunnerDestination.First, movement.Destination);
        Assert.Null(movement.SafeDestination);
        Assert.Null(movement.AttemptedDestination);
    }

    [Fact]
    public async Task Fielder_choice_can_preserve_an_attempted_advance_without_changing_PA_result()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[2].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3].Id, ScoringBase.Third);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        var unresolvedMenu = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id);
        Assert.DoesNotContain(unresolvedMenu.Choices, item =>
            item.IsOutAttemptingAdvance);
        var premature = new RunnerOutcomeChoice(
            RunnerDestination.Out, RunnerDestination.Second, RunnerDestination.Third);
        var rejected = await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[2].Id, premature);
        Assert.Equal(PendingPlayFailure.InvalidRequest, rejected.FailureReason);
        var unchanged = (await repository.GetStateForGameAsync(context.Game.Id))!
            .PendingPlay!.Movements.Single(item =>
                item.PlayerId == context.Players[2].Id);
        Assert.Equal(RunnerDestination.First, unchanged.Destination);
        Assert.True(unchanged.RequiresOutcomeConfirmation);
        Assert.Null(unchanged.SafeDestination);
        Assert.Null(unchanged.AttemptedDestination);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Second)).IsSuccess);

        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id);
        var attempted = Assert.Single(choices.Choices.Where(item =>
            item.IsOutAttemptingAdvance
            && item.SafeDestination == RunnerDestination.Second));
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[2].Id, attempted)).IsSuccess);
        var pending = await repository.GetStateForGameAsync(context.Game.Id);
        if (pending!.PendingPlay is not null)
        {
            foreach (var unresolved in pending.PendingPlay.Movements.Where(item =>
                         item.RequiresOutcomeConfirmation).ToArray())
            {
                var destination = (await repository.GetValidRunnerDestinationsAsync(
                    context.Game.Id, unresolved.PlayerId)).Destinations.First();
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id, unresolved.PlayerId, destination);
            }
            pending = await repository.GetStateForGameAsync(context.Game.Id);
            if (pending!.PendingPlay is not null)
                await repository.CompletePlateAppearanceAsync(context.Game.Id);
        }
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(state!.PlateAppearances).Result);
        AssertMovement(state.RunnerMovements.Single(item =>
                item.PlayerId == context.Players[2].Id),
            context.Players[2].Id, RunnerStartingLocation.First,
            RunnerDestination.Second, RunnerDestination.Third);
        Assert.Equal(1, state.Session.CurrentOuts);
    }

    [Fact]
    public async Task Runner_committed_to_second_by_FC_does_not_gain_attempted_third_in_next_play()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[2].Id, ScoringBase.First);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Second)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Null(committed!.PendingPlay);
        Assert.Contains(committed.BaseRunners, item =>
            item.PlayerId == context.Players[2].Id && item.Base == ScoringBase.Second);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id);

        Assert.Contains(choices.Choices, item =>
            item == RunnerOutcomeChoice.ForDestination(RunnerDestination.Out));
        Assert.DoesNotContain(choices.Choices, item => item.IsOutAttemptingAdvance);
    }

    [Fact]
    public async Task Runner_confirmed_safe_at_second_in_pending_FC_can_execute_attempted_third()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[2].Id, ScoringBase.First);
        await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Second)).IsSuccess);
        var pending = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.NotNull(pending!.PendingPlay);
        var runner = pending.PendingPlay.Movements.Single(item =>
            item.PlayerId == context.Players[2].Id);
        Assert.False(runner.RequiresOutcomeConfirmation);
        Assert.Equal(RunnerDestination.Second, runner.Destination);

        var menu = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[2].Id);
        var attempted = Assert.Single(menu.Choices.Where(item =>
            item.SafeDestination == RunnerDestination.Second
            && item.AttemptedDestination == RunnerDestination.Third));
        var adjusted = await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[2].Id, attempted);
        var after = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(adjusted.IsSuccess);
        Assert.Null(adjusted.PendingPlay);
        Assert.Null(after!.PendingPlay);
        Assert.Equal(BatterResult.FielderChoice,
            Assert.Single(after.PlateAppearances).Result);
        var movement = after.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[2].Id);
        AssertMovement(movement, context.Players[2].Id,
            RunnerStartingLocation.First, RunnerDestination.Second,
            RunnerDestination.Third);
        Assert.Equal((2, 2), (after.Session.CurrentOuts,
            after.Session.CurrentBatterPosition));
        Assert.Empty(after.BaseRunners);
        var outPlayerIds = after.GetCurrentHalfInningOutPlayerIds();
        Assert.Equal(2, outPlayerIds.Count);
        Assert.Contains(context.Players[0].Id, outPlayerIds);
        Assert.Contains(context.Players[2].Id, outPlayerIds);
    }

    [Fact]
    public async Task Scored_runner_keeps_single_pending_after_batter_is_out_attempting_second()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[2].Id, ScoringBase.Third);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);

        var batterMenu = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[0].Id);
        var attemptedSecond = Assert.Single(batterMenu.Choices.Where(item =>
            item.SafeDestination == RunnerDestination.First
            && item.AttemptedDestination == RunnerDestination.Second));
        Assert.True((await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id, context.Players[0].Id, attemptedSecond)).IsSuccess);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.NotNull(pending.PendingPlay);
        Assert.Empty(pending.PlateAppearances);
        Assert.Equal((0, 1, 1), (pending.Session.VisitorScore,
            pending.Session.CurrentOuts, pending.Session.CurrentBatterPosition));
        Assert.Contains(pending.PendingPlay.Movements, item =>
            item.PlayerId == context.Players[2].Id
            && item.Destination == RunnerDestination.Scores);
        AssertMovement(pending.PendingPlay.Movements.Single(item =>
                item.PlayerId == context.Players[0].Id),
            context.Players[0].Id, RunnerStartingLocation.Home,
            RunnerDestination.First, RunnerDestination.Second);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Third)).IsSuccess);
        var corrected = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.NotNull(corrected.PendingPlay);
        Assert.DoesNotContain(corrected.PendingPlay.Movements, item =>
            item.Destination == RunnerDestination.Scores);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores)).IsSuccess);

        Assert.True((await repository.CompletePlateAppearanceAsync(
            context.Game.Id)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(committed.PendingPlay);
        Assert.Equal(BatterResult.Single,
            Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal((1, 2, 2), (committed.Session.VisitorScore,
            committed.Session.CurrentOuts, committed.Session.CurrentBatterPosition));
        Assert.Empty(committed.BaseRunners);
        Assert.Single(committed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Scores));
        Assert.Single(committed.RunnerMovements.Where(item =>
            item.Destination == RunnerDestination.Out));
    }

    [Fact]
    public async Task Pre_story_schema_adds_nullable_columns_without_reinterpreting_old_out()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.ExecuteAsync(
            "CREATE TABLE GamePlateAppearanceRunnerMovement (Id INTEGER PRIMARY KEY AUTOINCREMENT, GamePlateAppearanceId INTEGER NOT NULL, PlayerId INTEGER NOT NULL, StartingLocation INTEGER NOT NULL, Destination INTEGER NOT NULL, RunCounts INTEGER NULL)");
        await db.ExecuteAsync(
            "CREATE TABLE GameScoringPendingRunnerMovement (Id INTEGER PRIMARY KEY AUTOINCREMENT, GameScoringPendingPlayId INTEGER NOT NULL, PlayerId INTEGER NOT NULL, StartingLocation INTEGER NOT NULL, Destination INTEGER NOT NULL, RequiresOutcomeConfirmation INTEGER NOT NULL)");
        await db.ExecuteAsync(
            "INSERT INTO GamePlateAppearanceRunnerMovement (GamePlateAppearanceId, PlayerId, StartingLocation, Destination, RunCounts) VALUES (1, 99, 1, 5, NULL)");

        await db.Database.InitializeAsync();
        var movement = Assert.Single(await db.GetPlateAppearanceRunnerMovementsAsync());
        var committedColumns = await db.UseConnectionAsync(connection =>
            connection.GetTableInfoAsync(nameof(GamePlateAppearanceRunnerMovement)));
        var pendingColumns = await db.UseConnectionAsync(connection =>
            connection.GetTableInfoAsync(nameof(GameScoringPendingRunnerMovement)));

        Assert.Equal(RunnerDestination.Out, movement.Destination);
        Assert.Null(movement.SafeDestination);
        Assert.Null(movement.AttemptedDestination);
        Assert.Contains(committedColumns, item => item.Name == "SafeDestination");
        Assert.Contains(committedColumns, item => item.Name == "AttemptedDestination");
        Assert.Contains(pendingColumns, item => item.Name == "SafeDestination");
        Assert.Contains(pendingColumns, item => item.Name == "AttemptedDestination");
    }

    [Fact]
    public void UI_uses_specific_resource_backed_attempted_destination_labels()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var resources = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Resources", "Strings", "AppResources.resx"));
        var viewModel = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        var dialog = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "RunnerDestinationDialog.xaml.cs"));
        var page = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Views", "ScoringPage.xaml.cs"));

        Assert.Contains("Out Attempting Second", resources);
        Assert.Contains("Out Attempting Third", resources);
        Assert.Contains("Out Attempting Home", resources);
        Assert.Contains("OutAttemptingHomeDestinationLabel", viewModel);
        Assert.Contains("FormatRunnerOutcome(outcome)", dialog);
        Assert.Contains("if (!viewModel.HasPendingPlay", page);
        Assert.Contains("ProcessCurrentPlayResolutionAsync", page);
    }

    private static void AssertMovement(
        GameScoringPendingRunnerMovement movement,
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination safeDestination,
        RunnerDestination attemptedDestination)
    {
        Assert.Equal(playerId, movement.PlayerId);
        Assert.Equal(startingLocation, movement.StartingLocation);
        Assert.Equal(RunnerDestination.Out, movement.Destination);
        Assert.Equal(safeDestination, movement.SafeDestination);
        Assert.Equal(attemptedDestination, movement.AttemptedDestination);
    }

    private static void AssertMovement(
        GamePlateAppearanceRunnerMovement movement,
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination safeDestination,
        RunnerDestination attemptedDestination)
    {
        Assert.Equal(playerId, movement.PlayerId);
        Assert.Equal(startingLocation, movement.StartingLocation);
        Assert.Equal(RunnerDestination.Out, movement.Destination);
        Assert.Equal(safeDestination, movement.SafeDestination);
        Assert.Equal(attemptedDestination, movement.AttemptedDestination);
    }

    private static Task SeedBaseAsync(
        TemporaryTestDatabase db,
        int sessionId,
        int playerId,
        ScoringBase scoringBase) => db.SeedBaseRunnerAsync(new()
        {
            GameScoringSessionId = sessionId,
            PlayerId = playerId,
            Base = scoringBase,
        });

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
                FirstName = $"Player{index}", LastName = "Test", IsActive = true,
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
