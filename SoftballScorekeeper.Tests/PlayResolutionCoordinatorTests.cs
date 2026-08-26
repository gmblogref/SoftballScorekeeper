using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class PlayResolutionCoordinatorTests
{
    private readonly PlayResolutionCoordinator coordinator = new();

    [Fact]
    public void Single_with_editable_batter_is_ready_for_next_batter()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Single, 0, Participant(1, RunnerStartingLocation.Home,
                RunnerDestination.First)));

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
        Assert.Equal(PlayDecisionRequirement.None, result.DecisionRequirement);
    }

    [Fact]
    public void Double_remains_pending_for_next_batter()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Double, 0, Participant(1, RunnerStartingLocation.Home,
                RunnerDestination.Second)));

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
    }

    [Fact]
    public void Triple_remains_pending_for_next_batter()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Triple, 0, Participant(1, RunnerStartingLocation.Home,
                RunnerDestination.Third)));

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
    }

    [Fact]
    public void Home_run_is_classified_for_immediate_commit()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.HomeRun, 0, Participant(1, RunnerStartingLocation.Home,
                RunnerDestination.Scores)));

        Assert.Equal(PlayResolutionStatus.CommitImmediately, result.Status);
    }

    [Fact]
    public void Walk_factory_preserves_forced_only_defaults_and_immediate_commit()
    {
        var bases = new[]
        {
            Base(2, ScoringBase.First),
            Base(3, ScoringBase.Second),
            Base(4, ScoringBase.Third),
        };
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.Walk, bases, out var context));

        Assert.Equal(
            new[]
            {
                (RunnerStartingLocation.Third, RunnerDestination.Scores),
                (RunnerStartingLocation.Second, RunnerDestination.Third),
                (RunnerStartingLocation.First, RunnerDestination.Second),
                (RunnerStartingLocation.Home, RunnerDestination.First),
            },
            context!.Participants.Select(item =>
                (item.StartingLocation, item.Destination)));
        Assert.Equal(
            PlayResolutionStatus.CommitImmediately,
            coordinator.Evaluate(context).Status);
    }

    [Fact]
    public void Empty_base_out_is_classified_for_immediate_commit()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.Out, [], out var context));

        Assert.Equal(
            PlayResolutionStatus.CommitImmediately,
            coordinator.Evaluate(context).Status);
    }

    [Fact]
    public void Resolved_fielder_choice_with_safe_survivor_is_ready_for_next_batter()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.FielderChoice,
            0,
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
    }

    [Fact]
    public void Fielder_choice_decisions_preserve_lead_to_trail_order_and_starting_locations()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0,
            1,
            BatterResult.FielderChoice,
            [
                Base(2, ScoringBase.First),
                Base(3, ScoringBase.Second),
                Base(4, ScoringBase.Third),
            ],
            out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayDecisionRequirement.ParticipantOutcome,
            result.DecisionRequirement);
        Assert.Equal(new[] { 4, 3, 2, 1 }, result.DecisionPlayerIds);
        Assert.Equal(new[]
        {
            RunnerStartingLocation.Third,
            RunnerStartingLocation.Second,
            RunnerStartingLocation.First,
            RunnerStartingLocation.Home,
        }, result.DecisionPlayerIds.Select(playerId =>
            result.Context!.Participants.Single(item =>
                item.PlayerId == playerId).StartingLocation));
    }

    [Fact]
    public void Normal_fielder_choice_stops_guided_decisions_after_one_retirement()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.FielderChoice,
            0,
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First,
                confirmation: true)));

        Assert.Equal(BatterResult.FielderChoice, result.Context!.Result);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
        Assert.Equal(PlayDecisionRequirement.None, result.DecisionRequirement);
        Assert.Empty(result.DecisionPlayerIds);
        Assert.All(result.Context.Participants,
            item => Assert.False(item.RequiresOutcomeConfirmation));
        Assert.Single(result.Context.Participants.Where(item =>
            item.Destination == RunnerDestination.Out));
    }

    [Fact]
    public void Fielder_choice_can_record_two_outs_without_reclassification()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.FielderChoice,
            0,
            Participant(3, RunnerStartingLocation.Second, RunnerDestination.Out),
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(BatterResult.FielderChoice, result.Context!.Result);
        Assert.Equal(2, result.Context.Participants.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
    }

    [Fact]
    public void All_terminal_non_third_out_fielder_choice_auto_completes()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.FielderChoice,
            0,
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.Out)));

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
    }

    [Fact]
    public void Two_out_fielder_choice_completes_automatically_without_a_decision()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            2, 1, BatterResult.FielderChoice,
            [Base(2, ScoringBase.First)], out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
        Assert.Equal(PlayDecisionRequirement.None, result.DecisionRequirement);
        Assert.Empty(result.DecisionPlayerIds);
        Assert.Equal(0, result.RequiredRetirementCount);
        Assert.Equal(ThirdOutRunRulingRequirement.AutomaticNoRun,
            result.ThirdOutRulingRequirement);
        Assert.Equal(BatterResult.FielderChoice, result.Context!.Result);
        Assert.Single(result.Context.Participants.Where(item =>
            item.Destination == RunnerDestination.Out));
        Assert.All(result.Context.Participants,
            item => Assert.False(item.RequiresOutcomeConfirmation));
    }

    [Fact]
    public void Resolved_two_out_fielder_choice_completes_without_run_timing_question()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.FielderChoice,
            2,
            Participant(3, RunnerStartingLocation.Third, RunnerDestination.Scores),
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
        Assert.Equal(PlayDecisionRequirement.None, result.DecisionRequirement);
        Assert.Equal(ThirdOutRunRulingRequirement.AutomaticNoRun,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Out_attempting_metadata_is_preserved_by_evaluation()
    {
        var context = Context(
            BatterResult.Single,
            0,
            new ResolvedPlayParticipantOutcome(
                1, RunnerStartingLocation.Home, RunnerDestination.Out,
                RunnerDestination.First, RunnerDestination.Second, null));

        var result = coordinator.Evaluate(context);

        var batter = Assert.Single(result.Context!.Participants);
        Assert.Equal(RunnerDestination.First, batter.SafeDestination);
        Assert.Equal(RunnerDestination.Second, batter.AttemptedDestination);
    }

    [Fact]
    public void Ambiguous_zero_out_double_play_requires_membership_selection()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.DoublePlay,
            [Base(2, ScoringBase.First), Base(3, ScoringBase.Second)],
            out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayDecisionRequirement.RetirementSelection,
            result.DecisionRequirement);
        Assert.Equal(2, result.RequiredRetirementCount);
        Assert.Equal(new[] { 3, 2, 1 }, result.DecisionPlayerIds);
    }

    [Fact]
    public void Unambiguous_zero_out_double_play_auto_retires_both_participants()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.DoublePlay,
            [Base(2, ScoringBase.First)], out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
        Assert.All(result.Context!.Participants, item =>
        {
            Assert.Equal(RunnerDestination.Out, item.Destination);
            Assert.True(item.IsUnorderedMultiOutRetirement);
            Assert.False(item.RequiresOutcomeConfirmation);
        });
    }

    [Fact]
    public void One_out_double_play_is_classified_for_immediate_commit()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            1, 1, BatterResult.DoublePlay,
            [Base(2, ScoringBase.First)], out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.CommitImmediately, result.Status);
        Assert.Equal(ThirdOutRunRulingRequirement.AutomaticNoRun,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Two_out_double_play_is_rejected()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            2, 1, BatterResult.DoublePlay,
            [Base(2, ScoringBase.First)], out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.Rejected, result.Status);
        Assert.Equal(PlayResolutionFailure.ResultUnavailable, result.FailureReason);
    }

    [Fact]
    public void Zero_out_triple_play_is_classified_for_immediate_commit()
    {
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.TriplePlay,
            [Base(2, ScoringBase.First), Base(3, ScoringBase.Second)],
            out var context));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.CommitImmediately, result.Status);
        Assert.Equal(ThirdOutRunRulingRequirement.AutomaticNoRun,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Participant_correction_reevaluates_all_terminal_double_play_status()
    {
        var context = Context(
            BatterResult.DoublePlay,
            0,
            Participant(3, RunnerStartingLocation.Third, RunnerDestination.Out,
                outOrder: 1, unordered: true),
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out,
                outOrder: 2, unordered: true),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First));

        var result = coordinator.ApplyParticipantOutcome(
            context, 1, RunnerOutcomeChoice.ForDestination(RunnerDestination.Out));

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
        Assert.Equal(3, result.Context!.Participants.Count(item =>
            item.Destination == RunnerDestination.Out));
    }

    [Fact]
    public void Available_destinations_are_owned_by_base_runner_resolver()
    {
        var context = Context(
            BatterResult.Single,
            0,
            Participant(2, RunnerStartingLocation.Second, RunnerDestination.Third),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First));
        var state = new BaseRunnerResolutionState(context.Participants.Select(item =>
            new BaseRunnerParticipant(
                item.PlayerId, item.StartingLocation, item.Destination)));

        var expected = BaseRunnerResolver.GetAvailableDestinations(state, 1);
        var actual = coordinator.GetAvailableOutcomes(context, 1)
            .Select(item => item.Destination).Distinct().ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Third_out_evaluator_is_not_used_before_third_out_boundary()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Single,
            1,
            Participant(2, RunnerStartingLocation.Third, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(ThirdOutRunRulingRequirement.NotApplicable,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Third_out_evaluator_is_used_at_third_out_boundary()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Single,
            2,
            Participant(2, RunnerStartingLocation.Third, RunnerDestination.Out),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(ThirdOutRunRulingRequirement.ScorerDecisionRequired,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Hit_with_scoring_runner_and_out_attempting_third_out_requires_run_ruling()
    {
        var context = Context(
            BatterResult.Single,
            2,
            Participant(3, RunnerStartingLocation.Third, RunnerDestination.Scores),
            new ResolvedPlayParticipantOutcome(
                2,
                RunnerStartingLocation.Second,
                RunnerDestination.Out,
                RunnerDestination.Third,
                RunnerDestination.Scores,
                null),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First));

        var result = coordinator.Evaluate(context);

        Assert.Equal(PlayResolutionStatus.AwaitingDecision, result.Status);
        Assert.Equal(PlayDecisionRequirement.ThirdOutRuling,
            result.DecisionRequirement);
        Assert.Equal(new[] { 3 }, result.DecisionPlayerIds);
        Assert.Equal(ThirdOutRunRulingRequirement.ScorerDecisionRequired,
            result.ThirdOutRulingRequirement);
    }

    [Fact]
    public void Safe_and_scored_participants_are_ready_and_editable_before_commit()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.Single,
            0,
            Participant(2, RunnerStartingLocation.Third, RunnerDestination.Scores),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First)));

        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, result.Status);
        Assert.False(result.RequiresDecision);
    }

    [Fact]
    public void All_terminal_continuing_double_play_auto_completes()
    {
        var result = coordinator.Evaluate(Context(
            BatterResult.DoublePlay,
            0,
            Participant(2, RunnerStartingLocation.First, RunnerDestination.Out,
                outOrder: 1, unordered: true),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.Out,
                outOrder: 2, unordered: true)));

        Assert.Equal(PlayResolutionStatus.AutoComplete, result.Status);
    }

    [Fact]
    public async Task Context_and_resolution_queries_do_not_advance_current_batter_position()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var game = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(game.Team.Id, game.Game.Id, TeamGameSide.Home);
        var before = (await repository.GetStateForGameAsync(game.Game.Id))!;
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            before.Session.CurrentOuts,
            game.Players[0].Id,
            BatterResult.Single,
            before.BaseRunners,
            out var context));

        coordinator.Evaluate(context);
        coordinator.GetAvailableOutcomes(context, game.Players[0].Id);
        var after = (await repository.GetStateForGameAsync(game.Game.Id))!;

        Assert.Equal(before.Session.CurrentBatterPosition,
            after.Session.CurrentBatterPosition);
    }

    [Fact]
    public async Task Coordinator_evaluation_does_not_recalculate_completed_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var game = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(game.Team.Id, game.Game.Id, TeamGameSide.Home);
        await repository.CompleteHomeRunAsync(game.Game.Id);
        var before = (await repository.GetStateForGameAsync(game.Game.Id))!;
        var appearance = Assert.Single(before.PlateAppearances);
        var movements = before.RunnerMovements.Select(MovementValue).ToArray();
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            before.Session.CurrentOuts,
            game.Players[1].Id,
            BatterResult.Single,
            before.BaseRunners,
            out var context));

        coordinator.Evaluate(context);
        var after = (await repository.GetStateForGameAsync(game.Game.Id))!;

        Assert.Equal((appearance.Id, appearance.PlayerId, appearance.Result),
            (after.PlateAppearances.Single().Id,
                after.PlateAppearances.Single().PlayerId,
                after.PlateAppearances.Single().Result));
        Assert.Equal(movements, after.RunnerMovements.Select(MovementValue));
    }

    [Fact]
    public void Malformed_rehydrated_pending_state_is_rejected_without_repair()
    {
        var play = PendingPlay(BatterResult.Single);
        var movements = new[]
        {
            PendingMovement(2, RunnerStartingLocation.First, RunnerDestination.Second),
            PendingMovement(1, RunnerStartingLocation.Home, RunnerDestination.First),
        };
        movements[0].StartingLocation = RunnerStartingLocation.Second;

        var created = PlayContextFactory.TryRehydrate(
            0, play, movements, [Base(2, ScoringBase.First)], out var context);

        Assert.False(created);
        Assert.Null(context);
        Assert.Equal(RunnerStartingLocation.Second, movements[0].StartingLocation);
    }

    [Fact]
    public void Identical_context_produces_deterministic_identical_resolution()
    {
        var context = Context(
            BatterResult.DoublePlay,
            0,
            Participant(3, RunnerStartingLocation.Second, RunnerDestination.Second,
                confirmation: true),
            Participant(2, RunnerStartingLocation.First, RunnerDestination.First,
                confirmation: true),
            Participant(1, RunnerStartingLocation.Home, RunnerDestination.First,
                confirmation: true));

        var first = coordinator.Evaluate(context);
        var second = coordinator.Evaluate(context);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.DecisionRequirement, second.DecisionRequirement);
        Assert.Equal(first.RequiredRetirementCount, second.RequiredRetirementCount);
        Assert.Equal(first.ThirdOutRulingRequirement,
            second.ThirdOutRulingRequirement);
        Assert.Equal(first.FailureReason, second.FailureReason);
        Assert.Equal(first.DecisionPlayerIds, second.DecisionPlayerIds);
        Assert.Equal(first.Context!.Participants, second.Context!.Participants);
    }

    [Fact]
    public void Resume_rehydrates_persisted_destinations_instead_of_regenerating_defaults()
    {
        var bases = new[] { Base(2, ScoringBase.First) };
        Assert.True(PlayContextFactory.TryCreateForNewResult(
            0, 1, BatterResult.Single, bases, out var fresh));
        var play = PendingPlay(BatterResult.Single);
        var movements = new[]
        {
            PendingMovement(2, RunnerStartingLocation.First, RunnerDestination.Third),
            PendingMovement(1, RunnerStartingLocation.Home, RunnerDestination.First),
        };

        Assert.True(PlayContextFactory.TryRehydrate(
            0, play, movements, bases, out var resumed));

        Assert.Equal(RunnerDestination.Second, fresh!.Participants.Single(item =>
            item.PlayerId == 2).Destination);
        Assert.Equal(RunnerDestination.Third, resumed!.Participants.Single(item =>
            item.PlayerId == 2).Destination);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            coordinator.Evaluate(resumed).Status);
    }

    private static PlayContext Context(
        BatterResult result,
        int currentOuts,
        params ResolvedPlayParticipantOutcome[] participants) =>
        new(1, result, currentOuts, participants);

    private static ResolvedPlayParticipantOutcome Participant(
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination destination,
        bool confirmation = false,
        int? outOrder = null,
        bool unordered = false) =>
        new(playerId, startingLocation, destination, null, null,
            outOrder, unordered, confirmation);

    private static GameScoringBaseRunner Base(int playerId, ScoringBase scoringBase) =>
        new() { GameScoringSessionId = 1, PlayerId = playerId, Base = scoringBase };

    private static GameScoringPendingPlay PendingPlay(BatterResult result) =>
        new()
        {
            Id = 1,
            GameScoringSessionId = 1,
            BatterPlayerId = 1,
            BatterPosition = 1,
            Result = result,
        };

    private static GameScoringPendingRunnerMovement PendingMovement(
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination destination) =>
        new()
        {
            GameScoringPendingPlayId = 1,
            PlayerId = playerId,
            StartingLocation = startingLocation,
            Destination = destination,
        };

    private static (int PlayerId, RunnerStartingLocation Start,
        RunnerDestination Destination, RunnerDestination? Safe,
        RunnerDestination? Attempted, int? OutOrder, bool? RunCounts)
        MovementValue(GamePlateAppearanceRunnerMovement movement) =>
        (movement.PlayerId, movement.StartingLocation, movement.Destination,
            movement.SafeDestination, movement.AttemptedDestination,
            movement.OutOrder, movement.RunCounts);

    private static async Task<(
        Team Team,
        Game Game,
        IReadOnlyList<Player> Players)> CreateReadyGameAsync(
        TemporaryTestDatabase db,
        int playerCount)
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
            await db.SeedTeamPlayerAsync(new()
            {
                TeamId = team.Id,
                PlayerId = player.Id,
                IsActive = true,
            });
            await db.SeedGameLineupPlayerAsync(new()
            {
                GameId = game.Id,
                PlayerId = player.Id,
                BattingOrder = index + 1,
            });
            players.Add(player);
        }
        return (team, game, players);
    }
}
