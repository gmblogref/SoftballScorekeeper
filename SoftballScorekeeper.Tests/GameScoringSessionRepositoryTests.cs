using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Initialize_rejects_game_without_lineup_and_leaves_no_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, "Opponent", "Field");

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.False(result.IsSuccess);
        Assert.Equal(InitializeScoringSessionFailure.InvalidLineup, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Initialize_accepts_past_today_and_future_games(int dayOffset)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today.AddDays(dayOffset), "Opponent", "Field", 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(result.IsSuccess);
        Assert.True(result.WasCreated);
    }

    [Theory]
    [InlineData(null, "Field")]
    [InlineData("Opponent", null)]
    public async Task Initialize_does_not_require_opponent_or_field(string? opponent, string? field)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, opponent, field, 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    public async Task Initialize_accepts_any_positive_contiguous_lineup_size(int playerCount)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, playerCount);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Initialize_rejects_invalid_team_game_ownership_and_invalid_ids()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var otherTeam = new Team { Name = "Other", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(otherTeam);
        var repository = db.CreateGameScoringSessionRepository();

        Assert.Equal(InitializeScoringSessionFailure.InvalidRequest,
            (await repository.InitializeAsync(0, context.Game.Id, TeamGameSide.Home)).FailureReason);
        Assert.Equal(InitializeScoringSessionFailure.InvalidTeamOrGame,
            (await repository.InitializeAsync(otherTeam.Id, context.Game.Id, TeamGameSide.Home)).FailureReason);
        Assert.Equal(InitializeScoringSessionFailure.InvalidTeamOrGame,
            (await repository.InitializeAsync(context.Team.Id, 999999, TeamGameSide.Home)).FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    [Theory]
    [InlineData(TeamGameSide.Home)]
    [InlineData(TeamGameSide.Away)]
    public async Task Initialize_persists_selected_side_and_in_progress_status(TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, side);
        var loaded = await db.CreateGameScoringSessionRepository().GetForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(side, loaded!.TeamSide);
        Assert.Equal(GameScoringStatus.InProgress, loaded.Status);
    }

    [Fact]
    public async Task Repeated_and_concurrent_initialization_resolves_to_exactly_one_original_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away)));
        var sessions = await db.GetGameScoringSessionsAsync();

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => result.WasCreated);
        Assert.Single(sessions);
        Assert.All(results, result => Assert.Equal(sessions[0].Id, result.Session!.Id));
        Assert.Equal(TeamGameSide.Away, sessions[0].TeamSide);
    }

    [Fact]
    public async Task Session_and_frozen_lineup_are_independent_between_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var secondGame = new Game { TeamId = first.Team.Id, SeasonId = first.Game.SeasonId, ScheduledDate = DateTime.Today.AddDays(1), ScheduledTime = TimeSpan.FromHours(18) };
        await db.SeedGameAsync(secondGame);
        var secondPlayer = new Player { FirstName = "Second", LastName = "Game", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(secondPlayer);
        await db.SeedGameLineupPlayerAsync(new() { GameId = secondGame.Id, PlayerId = secondPlayer.Id, BattingOrder = 1 });
        var before = await db.GetGameLineupPlayersAsync();

        await db.CreateGameScoringSessionRepository().InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);

        Assert.Equal(LineupValues(before), LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.Null(await db.CreateGameScoringSessionRepository().GetForGameAsync(secondGame.Id));
    }

    [Fact]
    public async Task Session_blocks_every_pre_game_lineup_mutation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var lineupRepository = db.CreateGameLineupRepository();
        await db.CreateGameScoringSessionRepository().InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var before = await db.GetGameLineupPlayersAsync();
        var extra = await CreateEligiblePlayerAsync(db, context.Team.Id, "Extra");

        var append = await lineupRepository.AppendAsync(context.Team.Id, context.Game.Id, [extra.Id]);
        var remove = await lineupRepository.RemovePlayersAsync(context.Game.Id, [before[0].PlayerId]);
        var reorder = await lineupRepository.ReorderAsync(context.Game.Id, before.Select(row => row.PlayerId).Reverse().ToArray());
        var previous = await lineupRepository.UsePreviousAsync(context.Team.Id, context.Game.Id);
        var created = new Player { FirstName = "New", LastName = "Player" };
        var create = await db.CreatePlayerRepository().CreateForGameLineupAsync(created, context.Team.Id, context.Game.Id);

        Assert.Equal(SaveGameLineupFailure.ScoringStarted, append.FailureReason);
        Assert.Empty(remove.RemovedPlayerIds);
        Assert.Equal(ReorderGameLineupFailure.ScoringStarted, reorder.FailureReason);
        Assert.Equal(UsePreviousLineupFailure.ScoringStarted, previous.FailureReason);
        Assert.Equal(CreatePlayerFailure.ScoringStarted, create.Failure);
        Assert.Equal(LineupValues(before), LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.DoesNotContain(await db.GetPlayersAsync(), player => player.FirstName == "New");
    }

    [Fact]
    public async Task Session_blocks_game_deletion_even_if_lineup_rows_are_missing()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        await db.CreateGameScoringSessionRepository().InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.DeleteGameLineupAsync(context.Game.Id);

        var result = await db.CreateGameRepository().RemoveGamesAsync(context.Team.Id, [context.Game.Id]);

        Assert.Contains(context.Game.Id, result.BlockedGameIds);
        Assert.Contains(await db.GetGamesAsync(), game => game.Id == context.Game.Id);
    }

    [Fact]
    public async Task Invalid_noncontiguous_lineup_fails_without_partial_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, null, null);
        var player = new Player { FirstName = "Invalid", LastName = "Order", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(player);
        await db.SeedGameLineupPlayerAsync(new() { GameId = context.Game.Id, PlayerId = player.Id, BattingOrder = 2 });

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.Equal(InitializeScoringSessionFailure.InvalidLineup, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    private static async Task<(Team Team, Game Game)> CreateReadyGameAsync(
        TemporaryTestDatabase db, DateTime date, string? opponent, string? field, int playerCount)
    {
        var context = await CreateGameAsync(db, date, opponent, field);
        for (var index = 0; index < playerCount; index++)
        {
            var player = await CreateEligiblePlayerAsync(db, context.Team.Id, $"Player{index}");
            await db.SeedGameLineupPlayerAsync(new() { GameId = context.Game.Id, PlayerId = player.Id, BattingOrder = index + 1 });
        }
        return context;
    }

    private static async Task<(Team Team, Game Game)> CreateGameAsync(
        TemporaryTestDatabase db, DateTime date, string? opponent, string? field)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Season", Year = 2026 };
        await db.SeedSeasonAsync(season);
        var game = new Game { TeamId = team.Id, SeasonId = season.Id, ScheduledDate = date.Date, ScheduledTime = TimeSpan.FromHours(18), Opponent = opponent, Field = field };
        await db.SeedGameAsync(game);
        return (team, game);
    }

    private static async Task<Player> CreateEligiblePlayerAsync(TemporaryTestDatabase db, int teamId, string firstName)
    {
        var player = new Player { FirstName = firstName, LastName = "Player", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(player);
        await db.SeedTeamPlayerAsync(new() { TeamId = teamId, PlayerId = player.Id, IsActive = true });
        return player;
    }

    private static IEnumerable<(int Id, int GameId, int PlayerId, int BattingOrder)> LineupValues(
        IEnumerable<GameLineupPlayer> rows) =>
        rows.Select(row => (row.Id, row.GameId, row.PlayerId, row.BattingOrder));
}
