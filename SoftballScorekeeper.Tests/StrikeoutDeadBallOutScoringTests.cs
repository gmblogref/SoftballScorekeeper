using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class StrikeoutDeadBallOutScoringTests
{
    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task Fixed_batter_out_commits_exact_result_and_holds_runners(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home,
            homeRunLimit, false);
        await db.SeedBaseRunnerAsync(new GameScoringBaseRunner
        {
            GameScoringSessionId = initialized.Session!.Id,
            PlayerId = context.Players[1].Id,
            Base = ScoringBase.First,
        });

        var selected = await repository.SelectBatterResultAsync(context.Game.Id, result);
        var state = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(context.Game.Id));

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state.PendingPlay);
        var appearance = Assert.Single(state.PlateAppearances);
        Assert.Equal(result, appearance.Result);
        Assert.NotEqual(BatterResult.Out, appearance.Result);
        Assert.Equal((1, 2, 0, 0), (state.Session.CurrentOuts,
            state.Session.CurrentBatterPosition, state.HomeHomeRuns,
            state.Session.HomeScore));
        var runner = Assert.Single(state.BaseRunners);
        Assert.Equal((context.Players[1].Id, ScoringBase.First),
            (runner.PlayerId, runner.Base));
        Assert.All(state.RunnerMovements, movement =>
            Assert.Equal((int)movement.StartingLocation, (int)movement.Destination));
        Assert.Equal([context.Players[0].Id], state.GetCurrentHalfInningOutPlayerIds());

        var stats = (await repository
            .GetInProgressBattingStatisticsAsync(context.Game.Id))!
            .Players.Single(item => item.PlayerId == context.Players[0].Id)
            .BattingStatistics;
        Assert.Equal((1, 1, 0, 0, 0m, 0m, 0m),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.HomeRuns,
                stats.BattingAverage, stats.OnBasePercentage,
                stats.SluggingPercentage));
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task Third_out_uses_normal_transition_and_preserves_classification(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away,
            homeRunLimit, false);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 2);

        var selected = await repository.SelectBatterResultAsync(context.Game.Id, result);
        var state = Assert.IsType<GameScoringState>(
            await repository.GetStateForGameAsync(context.Game.Id));

        Assert.True(selected.IsSuccess);
        Assert.Equal(result, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal((2, 0, 2), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Empty(state.BaseRunners);
        Assert.NotNull(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task Undo_and_redo_restore_exact_result_and_out_state(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        var undone = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Empty(undone.PlateAppearances);
        Assert.Equal(0, undone.Session.CurrentOuts);

        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        var redone = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(result, Assert.Single(redone.PlateAppearances).Result);
        Assert.Equal(1, redone.Session.CurrentOuts);
    }

    [Fact]
    public async Task Transaction_rejects_hr_and_dbo_when_classification_is_stale()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, 0, false);

        Assert.False((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DeadBallOut)).IsSuccess);
        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 5, false)).IsSuccess);
        Assert.False((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DeadBallOut)).IsSuccess);
    }

    [Fact]
    public async Task Hr_and_dbo_history_recalculates_availability_from_exact_results()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, 1, false);

        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.False((await repository.GetStateForGameAsync(context.Game.Id))!
            .IsHomeRunLegal());
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DeadBallOut)).IsSuccess);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal([BatterResult.HomeRun, BatterResult.DeadBallOut],
            committed.PlateAppearances.Select(item => item.Result));
        Assert.Equal(1, committed.HomeHomeRuns);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.False((await repository.GetStateForGameAsync(context.Game.Id))!
            .IsHomeRunLegal());
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.GetStateForGameAsync(context.Game.Id))!
            .IsHomeRunLegal());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.False((await repository.GetStateForGameAsync(context.Game.Id))!
            .IsHomeRunLegal());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        var restored = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.DeadBallOut,
            restored.PlateAppearances.Last().Result);
        Assert.Equal(1, restored.HomeHomeRuns);
    }

    [Theory]
    [InlineData(TeamGameSide.Home)]
    [InlineData(TeamGameSide.Away)]
    public async Task Lowered_limit_and_one_up_recalculate_without_reclassifying_history(
        TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side, 3, false);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 1, false)).IsSuccess);
        var lowered = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.False(lowered.IsHomeRunLegal());
        Assert.All(lowered.PlateAppearances,
            item => Assert.Equal(BatterResult.HomeRun, item.Result));

        await db.UseConnectionAsync(connection => connection.InsertAsync(
            new GameScoringHalfInning
            {
                GameScoringSessionId = initialized.Session!.Id,
                Inning = 1,
                TeamSummaryAcknowledged = true,
                OpponentHomeRuns = 2,
                OpponentEntryCompleted = true,
            }));
        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 1, true)).IsSuccess);
        var oneUp = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(oneUp.IsHomeRunLegal());
        Assert.Equal(2, side == TeamGameSide.Home
            ? oneUp.HomeHomeRuns : oneUp.VisitorHomeRuns);
        Assert.Equal(2, side == TeamGameSide.Home
            ? oneUp.VisitorHomeRuns : oneUp.HomeHomeRuns);
        Assert.All(oneUp.PlateAppearances,
            item => Assert.Equal(BatterResult.HomeRun, item.Result));
    }

    [Fact]
    public async Task Dbo_rule_change_undo_and_rescore_hr_is_nonretroactive_and_invalidates_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, 0, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.DeadBallOut)).IsSuccess);

        Assert.True((await repository.UpdateGameRulesAsync(
            context.Game.Id, 0, true)).IsSuccess);
        var unchanged = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.DeadBallOut,
            Assert.Single(unchanged.PlateAppearances).Result);
        Assert.True(unchanged.IsHomeRunLegal());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        var corrected = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.True(corrected.Session.IsOneUpEnabled);
        Assert.True(corrected.IsHomeRunLegal());
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.False((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        var rescored = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.HomeRun, Assert.Single(rescored.PlateAppearances).Result);
        Assert.Equal(1, rescored.HomeHomeRuns);
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task Exact_result_survives_repository_reopen_and_completion(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        await reopenedDatabase.InitializeAsync();
        var reopened = new GameScoringSessionRepository(reopenedDatabase);
        var resumed = (await reopened.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(result, Assert.Single(resumed.PlateAppearances).Result);
        Assert.Equal(result == BatterResult.DeadBallOut,
            !resumed.IsHomeRunLegal());

        Assert.True((await reopened.CompleteGameAsync(context.Game.Id)).IsSuccess);
        var completed = (await reopened.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(result, Assert.Single(completed.PlateAppearances).Result);
        Assert.Equal(GameScoringStatus.Completed, completed.Session.Status);
        var completedProjection = Assert.IsType<CompletedGameProjection>(
            await reopened.GetCompletedGameAsync(context.Game.Id));
        var completedStats = Assert.Single(completedProjection.Players)
            .BattingStatistics;
        Assert.Equal((1, 1, 0, 0, 0m, 0m, 0m),
            (completedStats.PlateAppearances, completedStats.AtBats,
                completedStats.Hits, completedStats.HomeRuns,
                completedStats.BattingAverage, completedStats.OnBasePercentage,
                completedStats.SluggingPercentage));
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task Reset_clears_exact_result_and_recalculates_availability(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);
        Assert.Null(await repository.GetStateForGameAsync(context.Game.Id));
        var restarted = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home,
            homeRunLimit, false);
        Assert.True(restarted.IsSuccess);
        var fresh = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Empty(fresh.PlateAppearances);
        Assert.Equal(0, fresh.HomeHomeRuns);
        Assert.Equal(homeRunLimit > 0, fresh.IsHomeRunLegal());
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, 0)]
    public async Task New_fixed_out_only_feeds_existing_two_outs_beer_rule(
        BatterResult result, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerRuleType.TwoOutsInOneInning, entry.RuleType);
        Assert.Equal(1, entry.Amount);
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
            await db.SeedTeamPlayerAsync(new TeamPlayer
            {
                TeamId = team.Id, PlayerId = player.Id, IsActive = true,
            });
            await db.SeedGameLineupPlayerAsync(new GameLineupPlayer
            {
                GameId = game.Id, PlayerId = player.Id, BattingOrder = index + 1,
            });
            players.Add(player);
        }
        return (team, game, players);
    }
}
