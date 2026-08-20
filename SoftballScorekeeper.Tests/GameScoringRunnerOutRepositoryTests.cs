using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringRunnerOutRepositoryTests
{
    [Theory]
    [InlineData(BatterResult.Single, RunnerDestination.Second)]
    [InlineData(BatterResult.Single, RunnerDestination.Third)]
    [InlineData(BatterResult.Double, RunnerDestination.Third)]
    [InlineData(BatterResult.Triple, RunnerDestination.Scores)]
    [InlineData(BatterResult.Triple, RunnerDestination.Second)]
    public async Task Batter_runner_correction_preserves_positive_PA_result(
        BatterResult result, RunnerDestination destination)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.SelectBatterResultAsync(context.Game.Id, result)).IsSuccess);
        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, context.Players[0].Id);
        Assert.Contains(destination, choices.Destinations);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, destination)).IsSuccess);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(result, completed.PlateAppearance!.Result);
        var movement = Assert.Single(state!.RunnerMovements);
        Assert.Equal(destination, movement.Destination);
    }

    [Theory]
    [InlineData(BatterResult.Single)]
    [InlineData(BatterResult.Double)]
    [InlineData(BatterResult.Triple)]
    public async Task Batter_runner_out_preserves_positive_PA_result(BatterResult result)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await repository.SelectBatterResultAsync(context.Game.Id, result);

        Assert.True((await repository.AdjustPendingRunnerAsync(context.Game.Id,
            context.Players[0].Id, RunnerDestination.Out)).IsSuccess);
        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(result, completed.PlateAppearance!.Result);
        Assert.Equal(1, state!.Session.CurrentOuts);
        Assert.Empty(state.BaseRunners);
        Assert.Equal(RunnerDestination.Out, Assert.Single(state.RunnerMovements).Destination);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Existing_runner_on_any_base_can_be_marked_out(int baseNumber)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, baseNumber);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var runnerId = context.Players[baseNumber].Id;

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, runnerId);
        Assert.Contains(RunnerDestination.Out, choices.Destinations);
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, runnerId, RunnerDestination.Out)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(BatterResult.Single, Assert.Single(state!.PlateAppearances).Result);
        Assert.Equal(1, state.Session.CurrentOuts);
        Assert.Contains(state.BaseRunners, item => item.PlayerId == context.Players[0].Id);
        Assert.DoesNotContain(state.BaseRunners, item => item.PlayerId == runnerId);
        Assert.Contains(state.RunnerMovements, item =>
            item.PlayerId == runnerId && item.Destination == RunnerDestination.Out);
    }

    [Fact]
    public async Task Non_third_out_scoring_and_runner_out_are_independent()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal((1, 1), (state!.Session.CurrentOuts, state.Session.HomeScore));
        Assert.Equal(true, state.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[2].Id).RunCounts);
        Assert.Equal(RunnerDestination.Out, state.RunnerMovements.Single(item =>
            item.PlayerId == context.Players[1].Id).Destination);
    }

    [Fact]
    public async Task Multiple_runner_outs_add_multiple_outs()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, 1, 2);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out);

        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(2, state!.Session.CurrentOuts);
        Assert.Equal(2, state.RunnerMovements.Count(item =>
            item.Destination == RunnerDestination.Out));
        Assert.Single(state.BaseRunners);
    }

    [Fact]
    public async Task Multiple_resolved_out_identities_drive_the_existing_third_out_transition()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, 1, 2);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Out);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Out);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal((2, 0, 2), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Empty(state.BaseRunners);
        Assert.Equal(new[] { context.Players[1].Id, context.Players[2].Id },
            state.RunnerMovements.Where(item => item.Destination == RunnerDestination.Out)
                .Select(item => item.PlayerId).OrderBy(item => item));
        Assert.Contains(state.RunnerMovements, item =>
            item.PlayerId == context.Players[0].Id
            && item.StartingLocation == RunnerStartingLocation.Home
            && item.Destination == RunnerDestination.First);
        Assert.Single(state.HalfInnings);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Runner_third_out_requires_ruling_and_preserves_physical_score(
        bool runCounts, int expectedRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Out);

        var required = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.Equal(new[] { context.Players[3].Id }, required.PlayerIds);
        var rejected = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        Assert.Equal(CompletePlateAppearanceFailure.InvalidScoringState,
            rejected.FailureReason);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id,
            new Dictionary<int, bool> { [context.Players[3].Id] = runCounts });
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Single, completed.PlateAppearance!.Result);
        Assert.Equal((2, 0, 2, expectedRuns), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition,
            state.Session.HomeScore));
        Assert.Empty(state.BaseRunners);
        var scoringMovement = state.RunnerMovements.Single(item =>
            item.Destination == RunnerDestination.Scores);
        Assert.Equal(runCounts, scoringMovement.RunCounts);
        Assert.Equal(expectedRuns, Assert.Single(state.HalfInnings).TeamRuns);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 2)]
    public async Task Third_out_run_rulings_follow_persisted_lead_order(
        bool leadCounts, bool trailingCounts, int expectedRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 3, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, 2, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, RunnerDestination.Scores);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Out);

        var required = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.Equal(new[] { context.Players[3].Id, context.Players[2].Id },
            required.PlayerIds);
        var rulings = new Dictionary<int, bool>
        {
            [context.Players[3].Id] = leadCounts,
        };
        if (leadCounts) rulings[context.Players[2].Id] = trailingCounts;

        var completed = await repository.CompletePlateAppearanceAsync(
            context.Game.Id, rulings);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(expectedRuns, state!.Session.VisitorScore);
        var scoring = state.RunnerMovements
            .Where(item => item.Destination == RunnerDestination.Scores)
            .OrderByDescending(item => item.StartingLocation).ToArray();
        Assert.Equal(new[] { context.Players[3].Id, context.Players[2].Id },
            scoring.Select(item => item.PlayerId));
        Assert.Contains(state.RunnerMovements, item =>
            item.PlayerId == context.Players[0].Id
            && item.StartingLocation == RunnerStartingLocation.Home
            && item.Destination == RunnerDestination.Out);
        Assert.Equal(leadCounts, scoring[0].RunCounts);
        Assert.Equal(leadCounts && trailingCounts, scoring[1].RunCounts);
        Assert.Equal(expectedRuns, Assert.Single(state.HalfInnings).TeamRuns);
    }

    [Fact]
    public async Task Failed_runner_out_third_out_rolls_back_all_authoritative_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 5, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, 3);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[0].Id, RunnerDestination.Out);
        await db.ExecuteAsync(
            "CREATE TRIGGER FailRunnerOut BEFORE UPDATE OF CurrentOuts ON GameScoringSession BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id,
            new Dictionary<int, bool> { [context.Players[3].Id] = true });
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(CompletePlateAppearanceFailure.Persistence, completed.FailureReason);
        Assert.Empty(state!.PlateAppearances);
        Assert.Empty(state.RunnerMovements);
        Assert.Empty(state.HalfInnings);
        Assert.Equal((5, 2, 1, 0), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition,
            state.Session.HomeScore));
        Assert.Single(state.BaseRunners);
        Assert.NotNull(state.PendingPlay);
    }

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
