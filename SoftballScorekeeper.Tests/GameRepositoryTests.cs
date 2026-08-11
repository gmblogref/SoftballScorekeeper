using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

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

    [Fact]
    public async Task CreateForTeamAsync_OneGame_PersistsCompleteNormalizedValues()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var requested = new Game
        {
            ScheduledDate = new DateTime(2026, 8, 20, 13, 45, 0),
            ScheduledTime = new TimeSpan(18, 30, 0),
            Opponent = "  Beer Bellies  ",
            Field = "  Field 2  ",
        };

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [requested]);

        Assert.True(result.IsCompleteSuccess);
        Assert.Equal(season.Id, result.SeasonId);
        Assert.Single(result.CreatedGameIds);
        var persisted = Assert.Single(await testDatabase.GetGamesAsync());
        Assert.Equal(
            (team.Id, season.Id, new DateTime(2026, 8, 20), new TimeSpan(18, 30, 0),
                "Beer Bellies", "Field 2"),
            (persisted.TeamId, persisted.SeasonId, persisted.ScheduledDate,
                persisted.ScheduledTime, persisted.Opponent, persisted.Field));
    }

    [Fact]
    public async Task CreateForTeamAsync_BlankOptionalValues_PersistNull()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var game = new Game
        {
            ScheduledDate = new DateTime(2026, 8, 20),
            ScheduledTime = new TimeSpan(18, 30, 0),
            Opponent = "   ",
            Field = string.Empty,
        };

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [game]);

        Assert.True(result.IsCompleteSuccess);
        var persisted = Assert.Single(await testDatabase.GetGamesAsync());
        Assert.Null(persisted.Opponent);
        Assert.Null(persisted.Field);
    }

    [Fact]
    public async Task CreateForTeamAsync_InvalidTeamOrSeasonOwnership_InsertsNothing()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);
        var missingSeason = new Season { Id = int.MaxValue, TeamId = selectedTeam.Id };
        var game = ValidGame();
        var repository = testDatabase.CreateGameRepository();

        Assert.False((await repository.CreateForTeamAsync(0, otherSeason, [game])).IsCompleteSuccess);
        Assert.False((await repository.CreateForTeamAsync(int.MaxValue, otherSeason, [game])).IsCompleteSuccess);
        Assert.False((await repository.CreateForTeamAsync(selectedTeam.Id, otherSeason, [game])).IsCompleteSuccess);
        Assert.False((await repository.CreateForTeamAsync(selectedTeam.Id, missingSeason, [game])).IsCompleteSuccess);
        Assert.Empty(await testDatabase.GetGamesAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_InactiveTeam_InsertsNothing()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        Assert.Equal(1, await testDatabase.CreateTeamRepository().DeactivateAsync(team.Id));

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [ValidGame()]);

        Assert.False(result.IsCompleteSuccess);
        Assert.Empty(await testDatabase.GetGamesAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_ZeroRosterTeam_CreatesDoubleheader()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var first = ValidGame(new TimeSpan(18, 30, 0));
        var second = ValidGame(new TimeSpan(19, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [first, second]);

        Assert.True(result.IsCompleteSuccess);
        Assert.Equal(2, result.CreatedGameIds.Count);
        Assert.Equal(2, result.CreatedGameIds.Distinct().Count());
        var games = await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id);
        Assert.Equal([first.ScheduledTime, second.ScheduledTime], games.Select(game => game.ScheduledTime));
        Assert.All(games, game => Assert.Equal(team.Id, game.TeamId));
        Assert.All(games, game => Assert.Equal(season.Id, game.SeasonId));
        Assert.All(games, game => Assert.Equal(first.ScheduledDate, game.ScheduledDate));
        Assert.Empty(await testDatabase.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_InvalidSecondGame_CreatesNoGameOrNewSeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var newSeason = new Season { TeamId = team.Id, Name = "Summer", Year = 2026 };
        var invalidSecondGame = ValidGame(TimeSpan.FromDays(1));

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, newSeason, [ValidGame(), invalidSecondGame]);

        Assert.False(result.IsCompleteSuccess);
        Assert.Empty(await testDatabase.GetGamesAsync());
        Assert.Empty(await testDatabase.GetSeasonsAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_NewDuplicateSeason_ReusesSeasonAndCreatesGamesAtomically()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var existingSeason = await CreateSeasonAsync(testDatabase, team, "Summer 2", 2026);
        var requestedSeason = new Season
        {
            TeamId = team.Id,
            Name = "  summer 2  ",
            Year = 2026,
        };

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, requestedSeason, [ValidGame()]);

        Assert.True(result.IsCompleteSuccess);
        Assert.Equal(existingSeason.Id, result.SeasonId);
        Assert.Single(await testDatabase.GetSeasonsAsync());
        Assert.Single(await testDatabase.GetGamesAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_NewSeason_CreatesSeasonAndDoubleheaderInOneOperation()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var requestedSeason = new Season
        {
            TeamId = team.Id,
            Name = "  Fall Tournament  ",
            Year = 2026,
        };

        var result = await testDatabase.CreateGameRepository().CreateForTeamAsync(
            team.Id,
            requestedSeason,
            [ValidGame(new TimeSpan(18, 30, 0)), ValidGame(new TimeSpan(19, 30, 0))]);

        Assert.True(result.IsCompleteSuccess);
        var season = Assert.Single(await testDatabase.GetSeasonsAsync());
        Assert.Equal((team.Id, "Fall Tournament", 2026),
            (season.TeamId, season.Name, season.Year));
        Assert.Equal(season.Id, result.SeasonId);
        var games = await testDatabase.GetGamesAsync();
        Assert.Equal(2, games.Count);
        Assert.All(games, game => Assert.Equal(season.Id, game.SeasonId));
    }

    [Fact]
    public async Task CreateForTeamAsync_PreservesExistingGames()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var existing = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(17, 0, 0));

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [ValidGame()]);

        Assert.True(result.IsCompleteSuccess);
        var persisted = await testDatabase.CreateGameRepository().GetForTeamAsync(team.Id);
        Assert.Equal(2, persisted.Count);
        Assert.Contains(persisted, game => game.Id == existing.Id);
    }

    [Fact]
    public async Task GetMostRecentlyUsedSeasonIdAsync_ReturnsLatestInsertedGameSeasonForTeam()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var summer = await CreateSeasonAsync(testDatabase, selectedTeam, "Summer", 2026);
        var fall = await CreateSeasonAsync(testDatabase, selectedTeam, "Fall", 2026);
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Fall", 2026);
        await CreateGameAsync(
            testDatabase, selectedTeam, summer, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        await CreateGameAsync(
            testDatabase, otherTeam, otherSeason, new DateTime(2026, 8, 2), new TimeSpan(18, 30, 0));
        await CreateGameAsync(
            testDatabase, selectedTeam, fall, new DateTime(2026, 9, 1), new TimeSpan(18, 30, 0));
        var repository = testDatabase.CreateGameRepository();

        Assert.Equal(fall.Id, await repository.GetMostRecentlyUsedSeasonIdAsync(selectedTeam.Id));
        Assert.Equal(otherSeason.Id, await repository.GetMostRecentlyUsedSeasonIdAsync(otherTeam.Id));
        Assert.Null(await repository.GetMostRecentlyUsedSeasonIdAsync(0));
        Assert.Null(await repository.GetMostRecentlyUsedSeasonIdAsync(int.MaxValue));
    }

    [Fact]
    public async Task CreateForTeamAsync_ExistingDuplicateKeyWithDifferentDetails_IsRejectedUnchanged()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var existing = await CreateGameAsync(
            testDatabase,
            team,
            season,
            new DateTime(2026, 8, 20),
            new TimeSpan(18, 30, 0),
            "Original Opponent",
            "Original Field");
        var requested = ValidGame();
        requested.Opponent = "Different Opponent";
        requested.Field = "Different Field";

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, [requested]);

        Assert.False(result.IsCompleteSuccess);
        Assert.Equal(CreateGamesFailure.DuplicateSchedule, result.FailureReason);
        var persisted = Assert.Single(await testDatabase.GetGamesAsync());
        Assert.Equal(
            (existing.Id, "Original Opponent", "Original Field"),
            (persisted.Id, persisted.Opponent, persisted.Field));
    }

    [Fact]
    public async Task CreateForTeamAsync_DifferentDateTimeSeasonOrTeam_AreAllowed()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var firstTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var secondTeam = await CreateTeamAsync(testDatabase, "Royals");
        var firstSeason = await CreateSeasonAsync(testDatabase, firstTeam, "Summer", 2026);
        var secondSeason = await CreateSeasonAsync(testDatabase, firstTeam, "Fall", 2026);
        var otherTeamSeason = await CreateSeasonAsync(testDatabase, secondTeam, "Summer", 2026);
        await CreateGameAsync(
            testDatabase, firstTeam, firstSeason,
            new DateTime(2026, 8, 20), new TimeSpan(18, 30, 0));
        var repository = testDatabase.CreateGameRepository();

        Assert.True((await repository.CreateForTeamAsync(
            firstTeam.Id, firstSeason, [ValidGame(new TimeSpan(19, 30, 0))])).IsCompleteSuccess);
        var differentDate = ValidGame();
        differentDate.ScheduledDate = new DateTime(2026, 8, 21);
        Assert.True((await repository.CreateForTeamAsync(
            firstTeam.Id, firstSeason, [differentDate])).IsCompleteSuccess);
        Assert.True((await repository.CreateForTeamAsync(
            firstTeam.Id, secondSeason, [ValidGame()])).IsCompleteSuccess);
        Assert.True((await repository.CreateForTeamAsync(
            secondTeam.Id, otherTeamSeason, [ValidGame()])).IsCompleteSuccess);
        Assert.Equal(5, (await testDatabase.GetGamesAsync()).Count);
    }

    [Fact]
    public async Task CreateForTeamAsync_DuplicateRequestedDoubleheader_CreatesNothing()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var newSeason = new Season { TeamId = team.Id, Name = "Summer", Year = 2026 };

        var result = await testDatabase.CreateGameRepository().CreateForTeamAsync(
            team.Id,
            newSeason,
            [ValidGame(), ValidGame()]);

        Assert.False(result.IsCompleteSuccess);
        Assert.Equal(CreateGamesFailure.DuplicateSchedule, result.FailureReason);
        Assert.Empty(await testDatabase.GetGamesAsync());
        Assert.Empty(await testDatabase.GetSeasonsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateForTeamAsync_DoubleheaderConflictWithExistingGame_InsertsNeither(
        bool firstRequestedGameConflicts)
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var existing = await CreateGameAsync(
            testDatabase, team, season,
            new DateTime(2026, 8, 20), new TimeSpan(18, 30, 0), "Existing", "Field 1");
        var nonconflicting = ValidGame(
            firstRequestedGameConflicts ? new TimeSpan(19, 30, 0) : new TimeSpan(17, 30, 0));
        var requestedGames = firstRequestedGameConflicts
            ? new[] { ValidGame(), nonconflicting }
            : new[] { nonconflicting, ValidGame() };

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, season, requestedGames);

        Assert.False(result.IsCompleteSuccess);
        Assert.Equal(CreateGamesFailure.DuplicateSchedule, result.FailureReason);
        var persisted = Assert.Single(await testDatabase.GetGamesAsync());
        Assert.Equal(existing.Id, persisted.Id);
    }

    [Fact]
    public async Task CreateForTeamAsync_DuplicateThroughProposedLogicalSeason_RollsBackWithoutNewRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var existingSeason = await CreateSeasonAsync(testDatabase, team, "Summer 2", 2026);
        var existingGame = await CreateGameAsync(
            testDatabase, team, existingSeason,
            new DateTime(2026, 8, 20), new TimeSpan(18, 30, 0));
        var proposedSeason = new Season
        {
            TeamId = team.Id,
            Name = "  summer 2  ",
            Year = 2026,
        };

        var result = await testDatabase.CreateGameRepository()
            .CreateForTeamAsync(team.Id, proposedSeason, [ValidGame()]);

        Assert.False(result.IsCompleteSuccess);
        Assert.Equal(CreateGamesFailure.DuplicateSchedule, result.FailureReason);
        Assert.Equal(existingSeason.Id, Assert.Single(await testDatabase.GetSeasonsAsync()).Id);
        Assert.Equal(existingGame.Id, Assert.Single(await testDatabase.GetGamesAsync()).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_OneEligibleGame_DeletesOnlySelectedGameAndPreservesSeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var selected = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var unselected = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 8), new TimeSpan(18, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .RemoveGamesAsync(team.Id, [selected.Id]);

        Assert.Equal([selected.Id], result.RequestedGameIds);
        Assert.Equal([selected.Id], result.DeletedGameIds);
        Assert.Empty(result.MissingGameIds);
        Assert.Empty(result.WrongTeamGameIds);
        Assert.Empty(result.BlockedGameIds);
        Assert.Empty(result.IgnoredGameIds);
        Assert.Equal(unselected.Id, Assert.Single(await testDatabase.GetGamesAsync()).Id);
        Assert.Equal(season.Id, Assert.Single(await testDatabase.GetSeasonsAsync()).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_MultipleGames_DeletesRequestedAndLeavesUnrelatedGame()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);
        var first = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var second = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 8), new TimeSpan(18, 30, 0));
        var unrelated = await CreateGameAsync(
            testDatabase, otherTeam, otherSeason, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .RemoveGamesAsync(team.Id, [first.Id, second.Id]);

        Assert.Equal(2, result.SuccessfulRemovalCount);
        Assert.Equal(unrelated.Id, Assert.Single(await testDatabase.GetGamesAsync()).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_WrongTeamGame_ReportsItWithoutDeletingAnyGame()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase);
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var selectedSeason = await CreateSeasonAsync(testDatabase, selectedTeam, "Summer", 2026);
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);
        var selectedGame = await CreateGameAsync(
            testDatabase, selectedTeam, selectedSeason, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var otherGame = await CreateGameAsync(
            testDatabase, otherTeam, otherSeason, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .RemoveGamesAsync(selectedTeam.Id, [otherGame.Id]);

        Assert.Empty(result.DeletedGameIds);
        Assert.Equal([otherGame.Id], result.WrongTeamGameIds);
        Assert.Equal(
            [selectedGame.Id, otherGame.Id],
            (await testDatabase.GetGamesAsync()).Select(game => game.Id));
    }

    [Fact]
    public async Task RemoveGamesAsync_InvalidDuplicateMissingAndEmptyInputs_ReturnAccurateNoOpResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var game = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var repository = testDatabase.CreateGameRepository();

        var mixed = await repository.RemoveGamesAsync(
            team.Id, [game.Id, game.Id, 0, -1, int.MaxValue]);

        Assert.Equal([game.Id, 0, -1, int.MaxValue], mixed.RequestedGameIds);
        Assert.Equal([game.Id], mixed.DeletedGameIds);
        Assert.Equal([int.MaxValue], mixed.MissingGameIds);
        Assert.Equal([0, -1], mixed.IgnoredGameIds);
        Assert.Empty((await repository.RemoveGamesAsync(team.Id, null)).RequestedGameIds);
        Assert.Empty((await repository.RemoveGamesAsync(team.Id, [])).RequestedGameIds);
        Assert.Empty((await repository.RemoveGamesAsync(0, [game.Id])).DeletedGameIds);
    }

    [Fact]
    public async Task RemoveGamesAsync_OneDoubleheaderGame_LeavesOtherGameUntouched()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var first = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var second = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(19, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .RemoveGamesAsync(team.Id, [first.Id]);

        Assert.Equal([first.Id], result.DeletedGameIds);
        Assert.Equal(second.Id, Assert.Single(await testDatabase.GetGamesAsync()).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_BothDoubleheaderGames_DeletesBothIndependentRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var first = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0));
        var second = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 1), new TimeSpan(19, 30, 0));

        var result = await testDatabase.CreateGameRepository()
            .RemoveGamesAsync(team.Id, [first.Id, second.Id]);

        Assert.Equal(2, result.SuccessfulRemovalCount);
        Assert.Empty(await testDatabase.GetGamesAsync());
        Assert.Equal(season.Id, Assert.Single(await testDatabase.GetSeasonsAsync()).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_DeletedSlot_CanBeRecreatedUsingPreservedSeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var original = await CreateGameAsync(
            testDatabase, team, season, new DateTime(2026, 8, 20), new TimeSpan(18, 30, 0));
        var repository = testDatabase.CreateGameRepository();

        var removal = await repository.RemoveGamesAsync(team.Id, [original.Id]);
        var replacement = await repository.CreateForTeamAsync(team.Id, season, [ValidGame()]);

        Assert.Equal([original.Id], removal.DeletedGameIds);
        Assert.True(replacement.IsCompleteSuccess);
        Assert.NotEqual(original.Id, Assert.Single(await testDatabase.GetGamesAsync()).Id);
        Assert.Equal(season.Id, Assert.Single(await testDatabase.GetSeasonsAsync()).Id);
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

    private static Game ValidGame(TimeSpan? scheduledTime = null)
    {
        return new Game
        {
            ScheduledDate = new DateTime(2026, 8, 20),
            ScheduledTime = scheduledTime ?? new TimeSpan(18, 30, 0),
            Opponent = "Beer Bellies",
            Field = "Field 2",
        };
    }
}
