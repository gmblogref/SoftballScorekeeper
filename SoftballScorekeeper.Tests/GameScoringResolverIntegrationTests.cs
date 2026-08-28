using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringResolverIntegrationTests
{
    [Theory]
    [InlineData(BatterResult.Single)]
    [InlineData(BatterResult.Double)]
    [InlineData(BatterResult.Triple)]
    public async Task Accepted_pending_hit_shapes_map_from_persisted_starting_locations(
        BatterResult result)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, result);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        var mapped = PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.Play, pending.Movements, out var state);

        Assert.True(mapped);
        Assert.Equal(
            new[]
            {
                (context.Players[3].Id, RunnerStartingLocation.Third),
                (context.Players[2].Id, RunnerStartingLocation.Second),
                (context.Players[1].Id, RunnerStartingLocation.First),
                (context.Players[0].Id, RunnerStartingLocation.Home),
            },
            state!.Participants.Select(item =>
                (item.PlayerId, item.StartingLocation)));
    }

    [Fact]
    public void Pending_hit_adapter_preserves_identity_outcomes_and_canonical_pre_play_order()
    {
        var play = new GameScoringPendingPlay
        {
            BatterPlayerId = 40,
            Result = BatterResult.Single,
        };
        var movements = new[]
        {
            Movement(40, RunnerStartingLocation.Home, RunnerDestination.First),
            Movement(20, RunnerStartingLocation.Second, RunnerDestination.Scores),
            Movement(10, RunnerStartingLocation.Third, RunnerDestination.Scores),
            Movement(30, RunnerStartingLocation.First, RunnerDestination.Out),
        };

        var mapped = PendingBaseRunnerResolutionAdapter.TryCreateState(
            play, movements, out var state);

        Assert.True(mapped);
        Assert.NotNull(state);
        Assert.Equal(
            new[]
            {
                (10, RunnerStartingLocation.Third, RunnerDestination.Scores),
                (20, RunnerStartingLocation.Second, RunnerDestination.Scores),
                (30, RunnerStartingLocation.First, RunnerDestination.Out),
                (40, RunnerStartingLocation.Home, RunnerDestination.First),
            },
            state.Participants.Select(item =>
                (item.PlayerId, item.StartingLocation, item.Destination)));
    }

    [Fact]
    public void Batter_only_pending_hit_maps_as_the_home_participant()
    {
        var play = new GameScoringPendingPlay
        {
            BatterPlayerId = 40,
            Result = BatterResult.Double,
        };

        var mapped = PendingBaseRunnerResolutionAdapter.TryCreateState(
            play,
            [Movement(40, RunnerStartingLocation.Home, RunnerDestination.Second)],
            out var state);

        Assert.True(mapped);
        var batter = Assert.Single(state!.Participants);
        Assert.Equal((40, RunnerStartingLocation.Home, RunnerDestination.Second),
            (batter.PlayerId, batter.StartingLocation, batter.Destination));
    }

    [Fact]
    public void Pending_batter_out_without_a_movement_maps_an_implicit_home_out()
    {
        var play = new GameScoringPendingPlay
        {
            BatterPlayerId = 40,
            Result = BatterResult.Out,
        };

        var mapped = PendingBaseRunnerResolutionAdapter.TryCreateState(
            play,
            [Movement(20, RunnerStartingLocation.Second, RunnerDestination.Third)],
            out var state);

        Assert.True(mapped);
        Assert.Equal(
            new[]
            {
                (20, RunnerStartingLocation.Second, RunnerDestination.Third),
                (40, RunnerStartingLocation.Home, RunnerDestination.Out),
            },
            state!.Participants.Select(item =>
                (item.PlayerId, item.StartingLocation, item.Destination)));
    }

    [Fact]
    public async Task Menu_and_execution_use_resolver_for_occupied_batter_destination()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var batterId = context.Players[0].Id;

        var before = await repository.GetStateForGameAsync(context.Game.Id);
        var mapped = PendingBaseRunnerResolutionAdapter.TryCreateState(
            before!.PendingPlay!.Play, before.PendingPlay.Movements, out var resolverState);
        Assert.True(mapped);
        var expected = BaseRunnerResolver.ResolveCorrection(
            resolverState!, batterId, RunnerDestination.Second);
        var outcomeMenu = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, batterId);
        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, batterId);
        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, batterId, RunnerDestination.Second);
        var after = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(expected.IsSuccess);
        Assert.Equal(
            RunnerOutcomeChoice.ForDestination(RunnerDestination.First),
            outcomeMenu.CurrentOutcome);
        Assert.Contains(
            RunnerOutcomeChoice.ForDestination(RunnerDestination.Second),
            outcomeMenu.Choices);
        Assert.Contains(RunnerDestination.Second, choices.Destinations);
        Assert.True(adjusted.IsSuccess);
        Assert.Equal(ExpectedDestinations(expected),
            PendingDestinations(after!.PendingPlay!.Movements));
        Assert.Equal(BatterResult.Single, after.PendingPlay.Play.Result);
        Assert.Equal(1, after.Session.CurrentBatterPosition);
        Assert.Equal(after.PendingPlay.Movements.Count,
            after.PendingPlay.Movements.Select(item => item.PlayerId).Distinct().Count());
        Assert.Equal(after.PendingPlay.Movements.Count(item => IsSafe(item.Destination)),
            after.PendingPlay.Movements.Where(item => IsSafe(item.Destination))
                .Select(item => item.Destination).Distinct().Count());
        Assert.DoesNotContain(after.PendingPlay.Movements,
            item => item.Destination == RunnerDestination.Out);
    }

    [Fact]
    public async Task Menu_availability_matches_resolver_for_every_pending_participant()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.Play, pending.Movements, out var resolverState));

        foreach (var participant in resolverState!.Participants)
        {
            var repositoryChoices = await repository.GetValidRunnerDestinationsAsync(
                context.Game.Id, participant.PlayerId);
            var resolverChoices = BaseRunnerResolver.GetAvailableDestinations(
                resolverState, participant.PlayerId);

            Assert.True(repositoryChoices.IsSuccess);
            Assert.Equal(resolverChoices, repositoryChoices.Destinations);
        }
    }

    [Fact]
    public async Task Impossible_correction_is_absent_and_repository_rejection_is_non_destructive()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var leadRunnerId = context.Players[3].Id;
        var before = await db.GetPendingRunnerMovementsAsync();

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, leadRunnerId);
        var rejected = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, leadRunnerId, RunnerDestination.Third);
        var after = await db.GetPendingRunnerMovementsAsync();

        Assert.DoesNotContain(RunnerDestination.Third, choices.Destinations);
        Assert.Equal(PendingPlayFailure.DuplicateDestination, rejected.FailureReason);
        Assert.Equal(PendingDestinations(before), PendingDestinations(after));
    }

    [Fact]
    public async Task Scored_runner_correction_uses_resolver_and_persists_displaced_runner()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var scoredRunnerId = context.Players[3].Id;

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, scoredRunnerId);
        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, scoredRunnerId, RunnerDestination.Third);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        Assert.Contains(RunnerDestination.Third, choices.Destinations);
        Assert.True(adjusted.IsSuccess);
        Assert.Equal(RunnerDestination.Third, pending.Movements
            .Single(item => item.PlayerId == scoredRunnerId).Destination);
        Assert.Equal(RunnerDestination.Second, pending.Movements
            .Single(item => item.PlayerId == context.Players[2].Id).Destination);
        Assert.Equal(RunnerDestination.First, pending.Movements
            .Single(item => item.PlayerId == context.Players[0].Id).Destination);
    }

    [Fact]
    public async Task Forced_mid_arrangement_failure_rolls_back_every_destination()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var before = await db.GetPendingRunnerMovementsAsync();
        await db.ExecuteAsync(
            "CREATE TRIGGER FailResolverCorrection BEFORE UPDATE OF Destination ON GameScoringPendingRunnerMovement WHEN OLD.StartingLocation = 2 BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var failed = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Second);
        var after = await db.GetPendingRunnerMovementsAsync();
        var session = Assert.Single(await db.GetGameScoringSessionsAsync());

        Assert.Equal(PendingPlayFailure.Persistence, failed.FailureReason);
        Assert.Equal(PendingDestinations(before), PendingDestinations(after));
        Assert.Equal(1, session.CurrentBatterPosition);
    }

    [Fact]
    public async Task Implicit_batter_out_preserves_multiple_out_and_completion_lifecycle()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2);
        await repository.SelectOutAsync(context.Game.Id);
        var pendingBefore = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.DoesNotContain(pendingBefore.Movements,
            item => item.PlayerId == pendingBefore.Play.BatterPlayerId);

        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out)).IsSuccess);
        var pendingAfter = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;
        Assert.Equal(2, pendingAfter.Movements.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.DoesNotContain(pendingAfter.Movements,
            item => item.PlayerId == pendingAfter.Play.BatterPlayerId);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Out, completed.PlateAppearance!.Result);
        Assert.Equal((2, 0, 2), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Equal(2, state.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));
    }

    [Fact]
    public async Task Explicit_result_change_regenerates_from_committed_bases_not_corrections()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Second);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Triple);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        Assert.Equal(BatterResult.Triple, pending.Play.Result);
        Assert.Equal(RunnerDestination.Third, pending.Movements
            .Single(item => item.PlayerId == context.Players[0].Id).Destination);
        Assert.All(pending.Movements.Where(item =>
                item.PlayerId != context.Players[0].Id),
            item => Assert.Equal(RunnerDestination.Scores, item.Destination));
        Assert.Equal(new[]
            {
                RunnerStartingLocation.Home,
                RunnerStartingLocation.First,
                RunnerStartingLocation.Second,
                RunnerStartingLocation.Third,
            }, pending.Movements.OrderBy(item => item.StartingLocation)
                .Select(item => item.StartingLocation));
    }

    [Theory]
    [InlineData(BatterResult.Single, RunnerDestination.Second)]
    [InlineData(BatterResult.Double, RunnerDestination.Third)]
    [InlineData(BatterResult.Triple, RunnerDestination.Second)]
    public async Task Batter_final_destination_remains_distinct_from_pending_result(
        BatterResult result, RunnerDestination destination)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await repository.SelectBatterResultAsync(context.Game.Id, result);

        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, destination);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(adjusted.IsSuccess);
        Assert.Equal(result, state!.PendingPlay!.Play.Result);
        Assert.Equal(destination, Assert.Single(state.PendingPlay.Movements).Destination);
        Assert.Equal(context.Players[0].Id, state.PendingPlay.Play.BatterPlayerId);
        Assert.Equal(1, state.Session.CurrentBatterPosition);
    }

    [Theory]
    [InlineData(BatterResult.Triple, BatterResult.Double)]
    [InlineData(BatterResult.Triple, BatterResult.Single)]
    [InlineData(BatterResult.Double, BatterResult.Single)]
    public async Task Explicit_result_edit_replaces_temporary_correction_with_committed_defaults(
        BatterResult initialResult, BatterResult replacementResult)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, initialResult);
        var temporaryDestination = initialResult == BatterResult.Triple
            ? RunnerDestination.Second : RunnerDestination.Third;
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, temporaryDestination)).IsSuccess);

        await repository.SelectBatterResultAsync(context.Game.Id, replacementResult);
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay!;

        Assert.Equal(replacementResult, pending.Play.Result);
        Assert.Equal(ExpectedLoadedDefaults(replacementResult),
            pending.Movements.OrderByDescending(item => item.StartingLocation)
                .Select(item => (item.StartingLocation, item.Destination)));
        Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
            pending.Play, pending.Movements, out var resolverState));
        var repositoryChoices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[0].Id);
        Assert.Equal(BaseRunnerResolver.GetAvailableDestinations(
            resolverState!, context.Players[0].Id), repositoryChoices.Destinations);
    }

    [Fact]
    public async Task Exit_resume_restores_exact_corrected_arrangement_and_resolver_menus()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Triple);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second)).IsSuccess);
        var before = await repository.GetStateForGameAsync(context.Game.Id);
        var expected = PendingOutcomes(before!.PendingPlay!.Movements);
        var expectedResult = before.PendingPlay.Play.Result;
        var expectedBatterId = before.PendingPlay.Play.BatterPlayerId;
        var expectedPosition = before.Session.CurrentBatterPosition;
        await db.Database.CloseAsync();

        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        try
        {
            var resumedRepository = new GameScoringSessionRepository(reopenedDatabase);
            var resumed = await resumedRepository.GetStateForGameAsync(context.Game.Id);

            Assert.NotNull(resumed);
            Assert.NotNull(resumed.PendingPlay);
            Assert.Equal(expectedResult, resumed.PendingPlay.Play.Result);
            Assert.Equal(expectedBatterId, resumed.PendingPlay.Play.BatterPlayerId);
            Assert.Equal(expectedPosition, resumed.Session.CurrentBatterPosition);
            Assert.Equal(expected, PendingOutcomes(resumed.PendingPlay.Movements));
            Assert.Equal(new[]
            {
                (context.Players[3].Id, RunnerStartingLocation.Third, RunnerDestination.Scores),
                (context.Players[2].Id, RunnerStartingLocation.Second, RunnerDestination.Out),
                (context.Players[1].Id, RunnerStartingLocation.First, RunnerDestination.Second),
                (context.Players[0].Id, RunnerStartingLocation.Home, RunnerDestination.First),
            }, expected);
            Assert.True(PendingBaseRunnerResolutionAdapter.TryCreateState(
                resumed.PendingPlay.Play, resumed.PendingPlay.Movements,
                out var resolverState));

            foreach (var participant in resolverState!.Participants)
            {
                var menu = await resumedRepository.GetValidRunnerDestinationsAsync(
                    context.Game.Id, participant.PlayerId);
                Assert.True(menu.IsSuccess);
                Assert.Equal(BaseRunnerResolver.GetAvailableDestinations(
                    resolverState, participant.PlayerId), menu.Destinations);
            }
        }
        finally
        {
            await reopenedDatabase.CloseAsync();
        }
    }

    [Fact]
    public async Task Malformed_persisted_pending_state_is_rejected_without_identity_repair()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await db.ExecuteAsync(
            "UPDATE GameScoringPendingRunnerMovement SET StartingLocation = ? WHERE PlayerId = ?",
            RunnerStartingLocation.First, context.Players[0].Id);
        var before = await db.GetPendingRunnerMovementsAsync();

        var menu = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[0].Id);
        var correction = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Second);
        var after = await db.GetPendingRunnerMovementsAsync();

        Assert.Equal(PendingPlayFailure.InvalidScoringState, menu.FailureReason);
        Assert.Equal(PendingPlayFailure.DuplicateDestination, correction.FailureReason);
        Assert.Equal(PendingOutcomes(before), PendingOutcomes(after));
        Assert.Equal(context.Players[0].Id, Assert.Single(after).PlayerId);
    }

    private static GameScoringPendingRunnerMovement Movement(
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination destination) => new()
        {
            PlayerId = playerId,
            StartingLocation = startingLocation,
            Destination = destination,
        };

    private static IReadOnlyList<(int PlayerId, RunnerDestination Destination)>
        ExpectedDestinations(BaseRunnerResolutionResult resolution) => resolution.Participants
            .Select(item => (item.PlayerId, item.Destination))
            .OrderBy(item => item.PlayerId).ToArray();

    private static IReadOnlyList<(int PlayerId, RunnerDestination Destination)>
        PendingDestinations(IEnumerable<GameScoringPendingRunnerMovement> movements) => movements
            .Select(item => (item.PlayerId, item.Destination))
            .OrderBy(item => item.PlayerId).ToArray();

    private static IReadOnlyList<(int PlayerId, RunnerStartingLocation Start,
        RunnerDestination Destination)> PendingOutcomes(
        IEnumerable<GameScoringPendingRunnerMovement> movements) => movements
            .OrderByDescending(item => item.StartingLocation)
            .Select(item => (item.PlayerId, item.StartingLocation, item.Destination))
            .ToArray();

    private static IReadOnlyList<(RunnerStartingLocation Start,
        RunnerDestination Destination)> ExpectedLoadedDefaults(BatterResult result) => result switch
    {
        BatterResult.Single =>
        [
            (RunnerStartingLocation.Third, RunnerDestination.Scores),
            (RunnerStartingLocation.Second, RunnerDestination.Third),
            (RunnerStartingLocation.First, RunnerDestination.Second),
            (RunnerStartingLocation.Home, RunnerDestination.First),
        ],
        BatterResult.Double =>
        [
            (RunnerStartingLocation.Third, RunnerDestination.Scores),
            (RunnerStartingLocation.Second, RunnerDestination.Scores),
            (RunnerStartingLocation.First, RunnerDestination.Third),
            (RunnerStartingLocation.Home, RunnerDestination.Second),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    private static bool IsSafe(RunnerDestination destination) => destination is
        RunnerDestination.First or RunnerDestination.Second or RunnerDestination.Third;

    private static async Task SeedBasesAsync(
        TemporaryTestDatabase db,
        int sessionId,
        IReadOnlyList<Player> players,
        params int[] bases)
    {
        foreach (var baseNumber in bases)
            await db.SeedBaseRunnerAsync(new()
            {
                GameScoringSessionId = sessionId,
                PlayerId = players[baseNumber].Id,
                Base = (ScoringBase)baseNumber,
            });
    }

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
