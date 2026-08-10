using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class GameRepositoryTests
{
    [Fact]
    public async Task GetForTeamAsync_GameWithUnsetOptionalValues_RoundTripsPersistedValues()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var scheduledDate = new DateTime(2026, 6, 15);
        var scheduledTime = new TimeSpan(18, 30, 0);
        var game = await CreateGameAsync(
            testDatabase,
            team,
            season,
            scheduledDate,
            scheduledTime);

        var persisted = Assert.Single(
            await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id));

        Assert.Equal(
            (game.Id, team.Id, season.Id, scheduledDate, scheduledTime),
            (persisted.Id, persisted.TeamId, persisted.SeasonId,
                persisted.ScheduledDate, persisted.ScheduledTime));
        Assert.Null(persisted.Opponent);
        Assert.Null(persisted.Field);
    }

    [Fact]
    public async Task GetForTeamAsync_ProvidedOpponentAndField_PersistUnchanged()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        await CreateGameAsync(
            testDatabase,
            team,
            season,
            new DateTime(2026, 6, 15),
            new TimeSpan(19, 45, 0),
            opponent: "River City",
            field: "Diamond 3");

        var persisted = Assert.Single(
            await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id));

        Assert.Equal("River City", persisted.Opponent);
        Assert.Equal("Diamond 3", persisted.Field);
    }

    [Fact]
    public async Task GetForTeamAsync_MultipleSeasonsAcrossYears_ReturnsAllTeamGames()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var fall = await CreateSeasonAsync(testDatabase, team, "Fall", 2026);
        var spring = await CreateSeasonAsync(testDatabase, team, "Spring", 2027);
        var fallGame = await CreateGameAsync(
            testDatabase, team, fall, new DateTime(2026, 10, 1), new TimeSpan(18, 0, 0));
        var springGame = await CreateGameAsync(
            testDatabase, team, spring, new DateTime(2027, 4, 1), new TimeSpan(18, 0, 0));

        var games = await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id);

        Assert.Equal([fallGame.Id, springGame.Id], games.Select(game => game.Id));
        Assert.Equal([fall.Id, spring.Id], games.Select(game => game.SeasonId));
        Assert.All(games, game => Assert.Equal(team.Id, game.TeamId));
    }

    [Fact]
    public async Task GetForTeamAsync_MultipleTeams_ReturnsOnlyRequestedTeamGames()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var selectedSeason = await CreateSeasonAsync(testDatabase, selectedTeam, "Summer", 2026);
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);
        var selectedGame = await CreateGameAsync(
            testDatabase, selectedTeam, selectedSeason,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        await CreateGameAsync(
            testDatabase, otherTeam, otherSeason,
            new DateTime(2026, 6, 1), new TimeSpan(19, 0, 0));

        var games = await testDatabase.CreateGameRepository().GetForTeamAsync(selectedTeam.Id);

        Assert.Equal([selectedGame.Id], games.Select(game => game.Id));
        Assert.All(games, game => Assert.Equal(selectedTeam.Id, game.TeamId));
    }

    [Fact]
    public async Task GetForTeamAsync_OrdersByDateThenTimeThenIdAndKeepsGamesSeparate()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var laterDate = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 6, 2), new TimeSpan(17, 0, 0));
        var laterTime = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 6, 1), new TimeSpan(20, 0, 0));
        var firstSameTime = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        var secondSameTime = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));

        var games = await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id);

        Assert.Equal(
            [firstSameTime.Id, secondSameTime.Id, laterTime.Id, laterDate.Id],
            games.Select(game => game.Id));
        Assert.Equal(4, games.Select(game => game.Id).Distinct().Count());
    }

    [Fact]
    public async Task GetForTeamAsync_InvalidOrMissingTeamIds_ReturnEmpty()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateGameRepository();

        Assert.Empty(await repository.GetForTeamAsync(0));
        Assert.Empty(await repository.GetForTeamAsync(-1));
        Assert.Empty(await repository.GetForTeamAsync(int.MaxValue));
    }

    [Fact]
    public async Task HasGamesForTeamAsync_NoneOneAndMultiple_ReturnsExpectedResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var emptyTeam = await CreateTeamAsync(testDatabase, "Empty");
        var oneGameTeam = await CreateTeamAsync(testDatabase, "One");
        var multipleGameTeam = await CreateTeamAsync(testDatabase, "Multiple");
        var oneGameSeason = await CreateSeasonAsync(testDatabase, oneGameTeam, "Summer", 2026);
        var multipleGameSeason = await CreateSeasonAsync(testDatabase, multipleGameTeam, "Summer", 2026);
        await CreateGameAsync(
            testDatabase, oneGameTeam, oneGameSeason,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        await CreateGameAsync(
            testDatabase, multipleGameTeam, multipleGameSeason,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        await CreateGameAsync(
            testDatabase, multipleGameTeam, multipleGameSeason,
            new DateTime(2026, 6, 8), new TimeSpan(18, 0, 0));
        var repository = testDatabase.CreateGameRepository();

        Assert.False(await repository.HasGamesForTeamAsync(emptyTeam.Id));
        Assert.True(await repository.HasGamesForTeamAsync(oneGameTeam.Id));
        Assert.True(await repository.HasGamesForTeamAsync(multipleGameTeam.Id));
    }

    [Fact]
    public async Task HasGamesForTeamAsync_InvalidMissingAndOtherTeamContexts_ReturnFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);
        await CreateGameAsync(
            testDatabase, otherTeam, otherSeason,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        var repository = testDatabase.CreateGameRepository();

        Assert.False(await repository.HasGamesForTeamAsync(0));
        Assert.False(await repository.HasGamesForTeamAsync(-1));
        Assert.False(await repository.HasGamesForTeamAsync(int.MaxValue));
        Assert.False(await repository.HasGamesForTeamAsync(selectedTeam.Id));
    }

    [Fact]
    public async Task TeamWithNoRosterPlayers_CanOwnAndQueryGame()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var game = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 6, 1), new TimeSpan(18, 0, 0));
        var repository = testDatabase.CreateGameRepository();

        var games = await repository.GetForTeamAsync(team.Id);

        Assert.Equal([game.Id], games.Select(item => item.Id));
        Assert.True(await repository.HasGamesForTeamAsync(team.Id));
        Assert.Empty(await testDatabase.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task Queries_DoNotMutatePersistedGame()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var scheduledDate = new DateTime(2026, 6, 15);
        var scheduledTime = new TimeSpan(21, 15, 0);
        var game = await CreateGameAsync(
            testDatabase,
            team,
            season,
            scheduledDate,
            scheduledTime,
            opponent: "Falcons",
            field: "North Field");
        var repository = testDatabase.CreateGameRepository();

        Assert.Single(await repository.GetForTeamAsync(team.Id));
        Assert.True(await repository.HasGamesForTeamAsync(team.Id));

        var persisted = Assert.Single(await testDatabase.GetGamesAsync());
        Assert.Equal(
            (game.Id, team.Id, season.Id, scheduledDate, scheduledTime, "Falcons", "North Field"),
            (persisted.Id, persisted.TeamId, persisted.SeasonId,
                persisted.ScheduledDate, persisted.ScheduledTime, persisted.Opponent, persisted.Field));
    }

    private static async Task<Team> CreateTeamAsync(
        TemporaryTestDatabase testDatabase,
        string name = "Tigers")
    {
        var team = new Team { Name = name };
        Assert.Equal(1, await testDatabase.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<Season> CreateSeasonAsync(
        TemporaryTestDatabase testDatabase,
        Team team,
        string name,
        int year)
    {
        var season = new Season
        {
            TeamId = team.Id,
            Name = name,
            Year = year,
        };

        Assert.Equal(1, await testDatabase.SeedSeasonAsync(season));
        return season;
    }

    private static async Task<Game> CreateGameAsync(
        TemporaryTestDatabase testDatabase,
        Team team,
        Season season,
        DateTime scheduledDate,
        TimeSpan scheduledTime,
        string? opponent = null,
        string? field = null)
    {
        var game = new Game
        {
            TeamId = team.Id,
            SeasonId = season.Id,
            ScheduledDate = scheduledDate,
            ScheduledTime = scheduledTime,
            Opponent = opponent,
            Field = field,
        };

        Assert.Equal(1, await testDatabase.SeedGameAsync(game));
        return game;
    }
}
