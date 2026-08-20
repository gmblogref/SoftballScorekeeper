using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringPlayRepositoryTests
{
    [Theory]
    [InlineData(TeamGameSide.Home, "", 1, 0)]
    [InlineData(TeamGameSide.Home, "1,2,3", 4, 0)]
    [InlineData(TeamGameSide.Away, "", 0, 1)]
    [InlineData(TeamGameSide.Away, "1,2,3", 0, 4)]
    public async Task Home_run_immediately_commits_complete_authoritative_play(
        TeamGameSide side, string occupiedBases, int expectedHome, int expectedVisitor)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, occupiedBases);

        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.HomeRun, completed.PlateAppearance!.Result);
        Assert.Equal(occupiedBases.Split(',', StringSplitOptions.RemoveEmptyEntries).Length + 1,
            completed.RunsScored);
        Assert.Equal((expectedHome, expectedVisitor),
            (state!.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(2, state.Session.CurrentBatterPosition);
        Assert.Empty(state.BaseRunners);
        Assert.Null(state.PendingPlay);
        Assert.All(state.RunnerMovements,
            item => Assert.Equal(RunnerDestination.Scores, item.Destination));
    }

    [Fact]
    public async Task Home_run_replaces_unresolved_result_from_committed_pre_play_bases()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "2,3");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id, RunnerDestination.Third);

        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(3, completed.RunsScored);
        Assert.Equal(3, state!.Session.HomeScore);
        Assert.Empty(state.BaseRunners);
        Assert.Null(state.PendingPlay);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    public async Task Home_run_advancement_supports_one_player_and_final_batter_wrap(
        int playerCount, int expectedPosition)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, playerCount);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        if (playerCount > 1)
            await db.UpdateScoringStateAsync(initialized.Session!.Id, 0, 0, playerCount);

        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(expectedPosition, completed.CurrentBatterPosition);
    }

    [Fact]
    public async Task Forced_home_run_failure_rolls_back_PA_score_bases_batter_and_pending_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "3");
        await db.ExecuteAsync("CREATE TRIGGER FailHomeRunMovement BEFORE INSERT ON GamePlateAppearanceRunnerMovement BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(CompletePlateAppearanceFailure.Persistence, completed.FailureReason);
        Assert.Empty(state!.PlateAppearances);
        Assert.Equal((0, 0, 1), (state.Session.HomeScore, state.Session.VisitorScore,
            state.Session.CurrentBatterPosition));
        Assert.Equal(ParseBases("3", context.Players), BaseValues(state.BaseRunners));
        Assert.Null(state.PendingPlay);
    }

    [Fact]
    public async Task New_scoring_session_has_empty_committed_bases_and_no_pending_play()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Empty(state!.BaseRunners);
        Assert.Null(state.PendingPlay);
    }

    [Theory]
    [InlineData(BatterResult.Single, "", "0:1", 0)]
    [InlineData(BatterResult.Single, "1", "0:1,1:2", 0)]
    [InlineData(BatterResult.Single, "3", "0:1,3:4", 1)]
    [InlineData(BatterResult.Single, "1,2,3", "0:1,1:2,2:3,3:4", 1)]
    [InlineData(BatterResult.Double, "", "0:2", 0)]
    [InlineData(BatterResult.Double, "1", "0:2,1:3", 0)]
    [InlineData(BatterResult.Double, "1,2,3", "0:2,1:3,2:4,3:4", 2)]
    [InlineData(BatterResult.Triple, "1,2,3", "0:3,1:4,2:4,3:4", 3)]
    [InlineData(BatterResult.HomeRun, "1,2,3", "0:4,1:4,2:4,3:4", 4)]
    public async Task Result_selection_generates_expected_uncommitted_default(
        BatterResult result, string occupiedBases, string expectedMovements, int expectedRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, occupiedBases);

        var selected = await repository.SelectBatterResultAsync(context.Game.Id, result);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Empty(state!.PlateAppearances);
        Assert.Equal(1, state.Session.CurrentBatterPosition);
        Assert.Equal((0, 0), (state.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(ParseMovements(expectedMovements), MovementValues(state.PendingPlay!.Movements));
        Assert.Equal(expectedRuns, state.PendingPlay.Movements.Count(item =>
            item.Destination == RunnerDestination.Scores));
        Assert.Equal(ParseBases(occupiedBases, context.Players), BaseValues(state.BaseRunners));
    }

    [Theory]
    [InlineData(TeamGameSide.Home, BatterResult.Single, "3", 1, 0)]
    [InlineData(TeamGameSide.Away, BatterResult.Single, "3", 0, 1)]
    [InlineData(TeamGameSide.Home, BatterResult.HomeRun, "1,2,3", 4, 0)]
    [InlineData(TeamGameSide.Away, BatterResult.Triple, "1,2,3", 0, 3)]
    public async Task Completion_commits_result_movements_bases_score_and_next_batter(
        TeamGameSide side, BatterResult result, string occupiedBases,
        int expectedHomeScore, int expectedVisitorScore)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, occupiedBases);
        var frozenBefore = await db.GetGameLineupPlayersAsync();
        var liveBefore = await db.GetGameScoringLineupPlayersAsync();
        await repository.SelectBatterResultAsync(context.Game.Id, result);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(result, completed.PlateAppearance!.Result);
        Assert.Equal(context.Players[0].Id, completed.PlateAppearance.PlayerId);
        Assert.Equal((expectedHomeScore, expectedVisitorScore),
            (state!.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(2, state.Session.CurrentBatterPosition);
        Assert.Null(state.PendingPlay);
        Assert.Equal(occupiedBases.Split(',', StringSplitOptions.RemoveEmptyEntries).Length + 1,
            state.RunnerMovements.Count);
        Assert.Equal(frozenBefore.Select(Row), (await db.GetGameLineupPlayersAsync()).Select(Row));
        Assert.Equal(liveBefore.Select(LiveRow),
            (await db.GetGameScoringLineupPlayersAsync()).Select(LiveRow));
    }

    [Fact]
    public async Task Scored_runner_cascades_occupied_third_base_runner_back_and_persists()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "2,3");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var scoredRunnerId = context.Players[3].Id;

        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, scoredRunnerId, RunnerDestination.Third);
        var reloaded = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(adjusted.IsSuccess);
        Assert.Equal(RunnerDestination.Third, reloaded!.PendingPlay!.Movements
            .Single(item => item.PlayerId == scoredRunnerId).Destination);
        Assert.Equal(RunnerDestination.Second, reloaded.PendingPlay.Movements
            .Single(item => item.PlayerId == context.Players[2].Id).Destination);
        Assert.DoesNotContain(reloaded.PendingPlay.Movements,
            item => item.Destination == RunnerDestination.Scores);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.True(completed.IsSuccess);
        Assert.Equal(0, completed.RunsScored);
        Assert.Equal(new[] { ScoringBase.First, ScoringBase.Second, ScoringBase.Third },
            committed!.BaseRunners.Select(item => item.Base));
        Assert.Contains(committed.RunnerMovements,
            item => item.PlayerId == scoredRunnerId
                && item.Destination == RunnerDestination.Third);
        Assert.Contains(committed.RunnerMovements,
            item => item.PlayerId == context.Players[2].Id
                && item.Destination == RunnerDestination.Second);
    }

    [Fact]
    public async Task Bases_loaded_single_excludes_cascade_that_would_push_through_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1,2,3");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var scoredRunnerId = context.Players[3].Id;

        var choices = await repository.GetValidRunnerDestinationsAsync(
            context.Game.Id, scoredRunnerId);
        var rejected = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, scoredRunnerId, RunnerDestination.Third);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(choices.IsSuccess);
        Assert.DoesNotContain(RunnerDestination.Third, choices.Destinations);
        Assert.Equal(PendingPlayFailure.DuplicateDestination, rejected.FailureReason);
        Assert.Equal(RunnerDestination.Scores, state!.PendingPlay!.Movements
            .Single(item => item.PlayerId == scoredRunnerId).Destination);
    }

    [Theory]
    [InlineData(RunnerDestination.Second)]
    [InlineData(RunnerDestination.Third)]
    [InlineData(RunnerDestination.Scores)]
    public async Task Existing_runner_can_hold_take_an_extra_base_or_score(
        RunnerDestination destination)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "2");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);

        var adjusted = await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[2].Id, destination);

        Assert.True(adjusted.IsSuccess);
        Assert.Equal(destination, adjusted.PendingPlay!.Movements
            .Single(item => item.PlayerId == context.Players[2].Id).Destination);
    }

    [Fact]
    public async Task Changing_result_regenerates_from_committed_bases_and_clear_is_non_destructive()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Scores);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Double);
        var regenerated = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(RunnerDestination.Third, regenerated!.PendingPlay!.Movements
            .Single(item => item.PlayerId == context.Players[1].Id).Destination);

        Assert.True(await repository.ClearPendingPlayAsync(context.Game.Id));
        var cleared = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Null(cleared!.PendingPlay);
        Assert.Empty(cleared.PlateAppearances);
        Assert.Equal(ParseBases("1", context.Players), BaseValues(cleared.BaseRunners));
        Assert.Equal(1, cleared.Session.CurrentBatterPosition);
        Assert.Equal((0, 0), (cleared.Session.HomeScore, cleared.Session.VisitorScore));
    }

    [Fact]
    public async Task Forced_completion_failure_rolls_back_every_committed_play_change_and_keeps_pending()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "3");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await db.ExecuteAsync("CREATE TRIGGER FailMovement BEFORE INSERT ON GamePlateAppearanceRunnerMovement BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(CompletePlateAppearanceFailure.Persistence, completed.FailureReason);
        Assert.Empty(state!.PlateAppearances);
        Assert.Empty(state.RunnerMovements);
        Assert.Equal(ParseBases("3", context.Players), BaseValues(state.BaseRunners));
        Assert.Equal((0, 0, 1), (state.Session.HomeScore, state.Session.VisitorScore,
            state.Session.CurrentBatterPosition));
        Assert.NotNull(state.PendingPlay);
    }

    [Fact]
    public async Task One_player_wraps_and_legacy_resultless_appearance_remains_loadable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await db.UseConnectionAsync(connection => connection.InsertAsync(new GamePlateAppearance
        {
            GameScoringSessionId = initialized.Session!.Id,
            PlayerId = context.Players[0].Id,
            BattingPosition = 1,
            PlateAppearanceNumber = 1,
        }));

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.HomeRun);
        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(1, state!.Session.CurrentBatterPosition);
        Assert.Null(state.PlateAppearances[0].Result);
        Assert.Equal(BatterResult.HomeRun, state.PlateAppearances[1].Result);
    }

    [Fact]
    public async Task Final_batter_completion_wraps_to_first()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 0, 0, 3);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.HomeRun);
        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(1, completed.CurrentBatterPosition);
    }

    [Fact]
    public async Task Story62_plate_appearance_schema_upgrades_additively_without_inventing_result()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.UseConnectionAsync(async connection =>
        {
            await connection.CreateTableAsync<LegacyPlateAppearance>();
            await connection.InsertAsync(new LegacyPlateAppearance
            {
                GameScoringSessionId = 12,
                PlayerId = 34,
                BattingPosition = 2,
                PlateAppearanceNumber = 7,
            });
            return 0;
        });

        await db.Database.InitializeAsync();
        var appearance = Assert.Single(await db.GetGamePlateAppearancesAsync());

        Assert.Equal((12, 34, 2, 7), (appearance.GameScoringSessionId,
            appearance.PlayerId, appearance.BattingPosition, appearance.PlateAppearanceNumber));
        Assert.Null(appearance.Result);
    }

    [Fact]
    public async Task Games_keep_bases_scores_and_history_isolated()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, 2);
        var second = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);
        await repository.InitializeAsync(second.Team.Id, second.Game.Id, TeamGameSide.Away);
        await repository.SelectBatterResultAsync(first.Game.Id, BatterResult.Double);
        await repository.CompletePlateAppearanceAsync(first.Game.Id);

        var firstState = await repository.GetStateForGameAsync(first.Game.Id);
        var secondState = await repository.GetStateForGameAsync(second.Game.Id);

        Assert.Single(firstState!.BaseRunners);
        Assert.Single(firstState.PlateAppearances);
        Assert.Empty(secondState!.BaseRunners);
        Assert.Empty(secondState.PlateAppearances);
        Assert.Equal((0, 0), (secondState.Session.HomeScore, secondState.Session.VisitorScore));
    }

    private static async Task SeedBasesAsync(TemporaryTestDatabase db, int sessionId,
        IReadOnlyList<Player> players, string occupiedBases)
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

    private static IReadOnlyList<(int Start, int Destination)> ParseMovements(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split(':')).Select(item => (int.Parse(item[0]), int.Parse(item[1])))
            .OrderBy(item => item.Item1).ToArray();

    private static IReadOnlyList<(int Start, int Destination)> MovementValues(
        IEnumerable<GameScoringPendingRunnerMovement> movements) => movements
        .Select(item => ((int)item.StartingLocation, (int)item.Destination))
        .OrderBy(item => item.Item1).ToArray();

    private static IReadOnlyList<(int Base, int PlayerId)> ParseBases(
        string value, IReadOnlyList<Player> players) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)
        .Select(item => (item, players[item].Id)).OrderBy(item => item.item).ToArray();

    private static IReadOnlyList<(int Base, int PlayerId)> BaseValues(
        IEnumerable<GameScoringBaseRunner> runners) => runners
        .Select(item => ((int)item.Base, item.PlayerId)).OrderBy(item => item.Item1).ToArray();

    private static (int Id, int GameId, int PlayerId, int Order) Row(GameLineupPlayer item) =>
        (item.Id, item.GameId, item.PlayerId, item.BattingOrder);

    private static (int Id, int SessionId, int PlayerId, int Position) LiveRow(
        GameScoringLineupPlayer item) =>
        (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition);

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

    [SQLite.Table("GamePlateAppearance")]
    private sealed class LegacyPlateAppearance
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        public int GameScoringSessionId { get; set; }
        public int PlayerId { get; set; }
        public int BattingPosition { get; set; }
        public int PlateAppearanceNumber { get; set; }
    }
}
