using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class PlayerRepositoryTests
{
    [Fact]
    public async Task CreateAsync_PlayerWithLastName_PersistsSingleActivePlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = new Player { FirstName = "Amy", LastName = "Adams" };

        var rowsInserted = await repository.CreateAsync(player);
        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal(1, rowsInserted);
        Assert.True(player.Id > 0);
        var persistedPlayer = Assert.Single(activePlayers);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Equal("Amy", persistedPlayer.FirstName);
        Assert.Equal("Adams", persistedPlayer.LastName);
        Assert.True(persistedPlayer.IsActive);
        Assert.Empty(await repository.GetInactiveAsync());
    }

    [Fact]
    public async Task CreateAsync_NullLastName_PersistsNullLastName()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = new Player { FirstName = "Amy", LastName = null };

        var rowsInserted = await repository.CreateAsync(player);
        var persistedPlayer = Assert.Single(await repository.GetActiveAsync());

        Assert.Equal(1, rowsInserted);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Equal("Amy", persistedPlayer.FirstName);
        Assert.Null(persistedPlayer.LastName);
        Assert.True(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNames_InsertsDistinctPlayers()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var firstPlayer = new Player { FirstName = "Amy", LastName = "Adams" };
        var secondPlayer = new Player { FirstName = "Amy", LastName = "Adams" };

        var firstRowsInserted = await repository.CreateAsync(firstPlayer);
        var secondRowsInserted = await repository.CreateAsync(secondPlayer);
        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal(1, firstRowsInserted);
        Assert.Equal(1, secondRowsInserted);
        Assert.True(firstPlayer.Id > 0);
        Assert.True(secondPlayer.Id > 0);
        Assert.NotEqual(firstPlayer.Id, secondPlayer.Id);
        Assert.Equal(2, activePlayers.Count);
        Assert.Equal(2, activePlayers.Select(player => player.Id).Distinct().Count());
        Assert.All(activePlayers, player =>
        {
            Assert.Equal("Amy", player.FirstName);
            Assert.Equal("Adams", player.LastName);
        });
    }

    [Fact]
    public async Task GetActiveAsync_ActiveAndInactivePlayersExist_ReturnsOnlyActivePlayers()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var amy = await CreatePlayerAsync(repository, "Amy", "Adams");
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker");
        await CreatePlayerAsync(repository, "Charlie", "Clark", isActive: false);

        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal([amy.Id, bob.Id], activePlayers.Select(player => player.Id));
        Assert.Equal(["Amy", "Bob"], activePlayers.Select(player => player.FirstName));
        Assert.Equal(["Adams", "Baker"], activePlayers.Select(player => player.LastName));
        Assert.All(activePlayers, player => Assert.True(player.IsActive));
    }

    [Fact]
    public async Task GetInactiveAsync_ActiveAndInactivePlayersExist_ReturnsOnlyInactivePlayers()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        await CreatePlayerAsync(repository, "Amy", "Adams");
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker", isActive: false);

        var inactivePlayers = await repository.GetInactiveAsync();

        var persistedPlayer = Assert.Single(inactivePlayers);
        Assert.Equal(bob.Id, persistedPlayer.Id);
        Assert.Equal("Bob", persistedPlayer.FirstName);
        Assert.Equal("Baker", persistedPlayer.LastName);
        Assert.False(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task GetActiveAsync_MixedCaseFirstNames_OrdersCaseInsensitively()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        await CreatePlayerAsync(repository, "charlie", "Clark");
        await CreatePlayerAsync(repository, "Bob", "Baker");
        await CreatePlayerAsync(repository, "amy", "Adams");

        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal(["amy", "Bob", "charlie"], activePlayers.Select(player => player.FirstName));
    }

    [Fact]
    public async Task GetActiveAsync_SameFirstName_OrdersCaseInsensitivelyByLastName()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        await CreatePlayerAsync(repository, "Alex", "zulu");
        await CreatePlayerAsync(repository, "alex", "Alpha");
        await CreatePlayerAsync(repository, "ALEX", "bravo");

        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal(["Alpha", "bravo", "zulu"], activePlayers.Select(player => player.LastName));
    }

    [Fact]
    public async Task GetActiveAsync_NullLastNameAmongMatchingFirstNames_OrdersNullAsEmpty()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        await CreatePlayerAsync(repository, "Alex", "bravo");
        await CreatePlayerAsync(repository, "alex", null);
        await CreatePlayerAsync(repository, "ALEX", "Alpha");

        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal([null, "Alpha", "bravo"], activePlayers.Select(player => player.LastName));
    }

    [Fact]
    public async Task GetActiveByIdAsync_ActivePlayerExists_ReturnsMatchingPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");

        var persistedPlayer = await repository.GetActiveByIdAsync(player.Id);

        Assert.NotNull(persistedPlayer);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Equal("Amy", persistedPlayer.FirstName);
        Assert.Equal("Adams", persistedPlayer.LastName);
        Assert.True(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task GetActiveByIdAsync_MissingOrInvalidId_ReturnsNull()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        await CreatePlayerAsync(repository, "Amy", "Adams");

        Assert.Null(await repository.GetActiveByIdAsync(int.MaxValue));
        Assert.Null(await repository.GetActiveByIdAsync(0));
        Assert.Null(await repository.GetActiveByIdAsync(-1));
    }

    [Fact]
    public async Task GetActiveByIdAsync_PlayerIsInactive_ReturnsNullWithoutDeletingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);

        var activePlayer = await repository.GetActiveByIdAsync(player.Id);
        var inactivePlayer = Assert.Single(await repository.GetInactiveAsync());

        Assert.Null(activePlayer);
        Assert.Equal(player.Id, inactivePlayer.Id);
        Assert.False(inactivePlayer.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_ActivePlayerExists_UpdatesNamesWithoutReplacingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");
        player.FirstName = "Beth";
        player.LastName = "Baker";

        var rowsAffected = await repository.UpdateAsync(player);
        var allPlayers = await GetAllPlayersAsync(repository);

        Assert.Equal(1, rowsAffected);
        var persistedPlayer = Assert.Single(allPlayers);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Equal("Beth", persistedPlayer.FirstName);
        Assert.Equal("Baker", persistedPlayer.LastName);
        Assert.True(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_LastNameChangedToNull_PersistsNullOnSameRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");
        player.LastName = null;

        var rowsAffected = await repository.UpdateAsync(player);
        var persistedPlayer = Assert.Single(await repository.GetActiveAsync());

        Assert.Equal(1, rowsAffected);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Null(persistedPlayer.LastName);
        Assert.True(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_PlayerIdIsMissing_ReturnsZeroWithoutInsertingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var existingPlayer = await CreatePlayerAsync(repository, "Amy", "Adams");
        var missingPlayer = new Player
        {
            Id = int.MaxValue,
            FirstName = "Beth",
            LastName = "Baker",
        };

        var rowsAffected = await repository.UpdateAsync(missingPlayer);
        var allPlayers = await GetAllPlayersAsync(repository);

        Assert.Equal(0, rowsAffected);
        var persistedPlayer = Assert.Single(allPlayers);
        Assert.Equal(existingPlayer.Id, persistedPlayer.Id);
        Assert.Equal("Amy", persistedPlayer.FirstName);
        Assert.Equal("Adams", persistedPlayer.LastName);
    }

    [Fact]
    public async Task UpdateAsync_PlayerIsInactive_ReturnsZeroWithoutChangingOrReactivatingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);
        player.FirstName = "Beth";
        player.LastName = "Baker";

        var rowsAffected = await repository.UpdateAsync(player);
        var allPlayers = await GetAllPlayersAsync(repository);

        Assert.Equal(0, rowsAffected);
        var persistedPlayer = Assert.Single(allPlayers);
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.Equal("Amy", persistedPlayer.FirstName);
        Assert.Equal("Adams", persistedPlayer.LastName);
        Assert.False(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_SelectedPlayers_PreservesRowsAndUnselectedState()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var amy = await CreatePlayerAsync(repository, "Amy", "Adams");
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker");
        var charlie = await CreatePlayerAsync(repository, "Charlie", "Clark");

        var rowsAffected = await repository.DeactivateAsync([amy.Id, charlie.Id]);
        var activePlayers = await repository.GetActiveAsync();
        var inactivePlayers = await repository.GetInactiveAsync();
        var allPlayers = activePlayers.Concat(inactivePlayers).ToList();

        Assert.Equal(2, rowsAffected);
        var persistedBob = Assert.Single(activePlayers);
        Assert.Equal(bob.Id, persistedBob.Id);
        Assert.Equal("Bob", persistedBob.FirstName);
        Assert.Equal("Baker", persistedBob.LastName);
        Assert.Equal([amy.Id, charlie.Id], inactivePlayers.Select(player => player.Id));
        Assert.Equal(["Amy", "Charlie"], inactivePlayers.Select(player => player.FirstName));
        Assert.Equal(["Adams", "Clark"], inactivePlayers.Select(player => player.LastName));
        Assert.Equal(3, allPlayers.Count);
        Assert.Equal([amy.Id, bob.Id, charlie.Id], allPlayers.Select(player => player.Id).Order());
    }

    [Fact]
    public async Task DeactivateAsync_ValidAndMissingIds_ChangesOnlyValidPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var amy = await CreatePlayerAsync(repository, "Amy", "Adams");
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker");

        var rowsAffected = await repository.DeactivateAsync([amy.Id, int.MaxValue]);

        Assert.Equal(1, rowsAffected);
        Assert.Equal(bob.Id, Assert.Single(await repository.GetActiveAsync()).Id);
        Assert.Equal(amy.Id, Assert.Single(await repository.GetInactiveAsync()).Id);
    }

    [Fact]
    public async Task DeactivateAsync_DuplicateIdsProvided_UpdatesPlayerOnce()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");

        var rowsAffected = await repository.DeactivateAsync([player.Id, player.Id, player.Id]);

        Assert.Equal(1, rowsAffected);
        Assert.Single(await repository.GetInactiveAsync());
        Assert.Single(await GetAllPlayersAsync(repository));
    }

    [Fact]
    public async Task DeactivateAsync_InvalidIdsAndValidId_IgnoresInvalidIds()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");

        var rowsAffected = await repository.DeactivateAsync([0, -1, player.Id]);

        Assert.Equal(1, rowsAffected);
        Assert.Equal(player.Id, Assert.Single(await repository.GetInactiveAsync()).Id);
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyInactiveAndActiveIds_CountsOnlyActualTransition()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var inactivePlayer = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);
        var activePlayer = await CreatePlayerAsync(repository, "Bob", "Baker");

        var rowsAffected = await repository.DeactivateAsync([inactivePlayer.Id, activePlayer.Id]);
        var inactivePlayers = await repository.GetInactiveAsync();

        Assert.Equal(1, rowsAffected);
        Assert.Empty(await repository.GetActiveAsync());
        Assert.Equal([inactivePlayer.Id, activePlayer.Id], inactivePlayers.Select(player => player.Id).Order());
        Assert.Equal(2, inactivePlayers.Count);
    }

    [Fact]
    public async Task DeactivateAsync_EmptyOrNullInput_ReturnsZeroWithoutChangingRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams");

        var emptyRowsAffected = await repository.DeactivateAsync([]);
        var nullRowsAffected = await repository.DeactivateAsync(null);

        Assert.Equal(0, emptyRowsAffected);
        Assert.Equal(0, nullRowsAffected);
        Assert.Equal(player.Id, Assert.Single(await repository.GetActiveAsync()).Id);
        Assert.Empty(await repository.GetInactiveAsync());
    }

    [Fact]
    public async Task ReactivateAsync_SelectedPlayers_PreservesRowsAndUnselectedState()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var amy = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker", isActive: false);
        var charlie = await CreatePlayerAsync(repository, "Charlie", "Clark", isActive: false);

        var rowsAffected = await repository.ReactivateAsync([amy.Id, charlie.Id]);
        var activePlayers = await repository.GetActiveAsync();
        var inactivePlayers = await repository.GetInactiveAsync();
        var allPlayers = activePlayers.Concat(inactivePlayers).ToList();

        Assert.Equal(2, rowsAffected);
        Assert.Equal([amy.Id, charlie.Id], activePlayers.Select(player => player.Id));
        Assert.Equal(["Amy", "Charlie"], activePlayers.Select(player => player.FirstName));
        Assert.Equal(["Adams", "Clark"], activePlayers.Select(player => player.LastName));
        var persistedBob = Assert.Single(inactivePlayers);
        Assert.Equal(bob.Id, persistedBob.Id);
        Assert.Equal("Bob", persistedBob.FirstName);
        Assert.Equal("Baker", persistedBob.LastName);
        Assert.Equal(3, allPlayers.Count);
        Assert.Equal([amy.Id, bob.Id, charlie.Id], allPlayers.Select(player => player.Id).Order());
    }

    [Fact]
    public async Task ReactivateAsync_ValidAndMissingIds_ChangesOnlyValidPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var amy = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);
        var bob = await CreatePlayerAsync(repository, "Bob", "Baker", isActive: false);

        var rowsAffected = await repository.ReactivateAsync([amy.Id, int.MaxValue]);

        Assert.Equal(1, rowsAffected);
        Assert.Equal(amy.Id, Assert.Single(await repository.GetActiveAsync()).Id);
        Assert.Equal(bob.Id, Assert.Single(await repository.GetInactiveAsync()).Id);
    }

    [Fact]
    public async Task ReactivateAsync_DuplicateIdsProvided_UpdatesPlayerOnce()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);

        var rowsAffected = await repository.ReactivateAsync([player.Id, player.Id, player.Id]);

        Assert.Equal(1, rowsAffected);
        Assert.Single(await repository.GetActiveAsync());
        Assert.Single(await GetAllPlayersAsync(repository));
    }

    [Fact]
    public async Task ReactivateAsync_InvalidIdsAndValidId_IgnoresInvalidIds()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);

        var rowsAffected = await repository.ReactivateAsync([0, -1, player.Id]);

        Assert.Equal(1, rowsAffected);
        Assert.Equal(player.Id, Assert.Single(await repository.GetActiveAsync()).Id);
    }

    [Fact]
    public async Task ReactivateAsync_AlreadyActiveAndInactiveIds_CountsOnlyActualTransition()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var activePlayer = await CreatePlayerAsync(repository, "Amy", "Adams");
        var inactivePlayer = await CreatePlayerAsync(repository, "Bob", "Baker", isActive: false);

        var rowsAffected = await repository.ReactivateAsync([activePlayer.Id, inactivePlayer.Id]);
        var activePlayers = await repository.GetActiveAsync();

        Assert.Equal(1, rowsAffected);
        Assert.Empty(await repository.GetInactiveAsync());
        Assert.Equal([activePlayer.Id, inactivePlayer.Id], activePlayers.Select(player => player.Id).Order());
        Assert.Equal(2, activePlayers.Count);
    }

    [Fact]
    public async Task ReactivateAsync_EmptyOrNullInput_ReturnsZeroWithoutChangingRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreatePlayerRepository();
        var player = await CreatePlayerAsync(repository, "Amy", "Adams", isActive: false);

        var emptyRowsAffected = await repository.ReactivateAsync([]);
        var nullRowsAffected = await repository.ReactivateAsync(null);

        Assert.Equal(0, emptyRowsAffected);
        Assert.Equal(0, nullRowsAffected);
        Assert.Equal(player.Id, Assert.Single(await repository.GetInactiveAsync()).Id);
        Assert.Empty(await repository.GetActiveAsync());
    }

    [Fact]
    public async Task CreateForTeamAsync_AtomicallyCreatesPlayerAndCorrectActiveMembership()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = new Team { Name = "Tigers" };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        var player = new Player { FirstName = "Amy", LastName = "Able" };

        var result = await db.CreatePlayerRepository().CreateForTeamAsync(player, team.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(player.Id, result.PlayerId);
        Assert.Equal(player.Id, Assert.Single(await db.GetPlayersAsync()).Id);
        var membership = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal(team.Id, membership.TeamId);
        Assert.Equal(player.Id, membership.PlayerId);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task CreateForTeamAsync_InvalidOrInactiveTeamRollsBackPlayer()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = new Team { Name = "Tigers" };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(team.Id));

        var result = await db.CreatePlayerRepository().CreateForTeamAsync(
            new Player { FirstName = "Amy" }, team.Id);

        Assert.Equal(CreatePlayerFailure.InvalidTeam, result.Failure);
        Assert.Empty(await db.GetPlayersAsync());
        Assert.Empty(await db.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task CreateForGameLineupAsync_EmptyLineupCreatesCompleteChainAtOrderOne()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, game) = await CreateGameContextAsync(db, "Tigers");
        var player = new Player { FirstName = "Amy" };

        var result = await db.CreatePlayerRepository().CreateForGameLineupAsync(player, team.Id, game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(player.Id, Assert.Single(await db.GetPlayersAsync()).Id);
        var membership = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal((team.Id, player.Id, true), (membership.TeamId, membership.PlayerId, membership.IsActive));
        var lineup = Assert.Single(await db.GetGameLineupPlayersAsync());
        Assert.Equal((game.Id, player.Id, 1), (lineup.GameId, lineup.PlayerId, lineup.BattingOrder));
    }

    [Fact]
    public async Task CreateForGameLineupAsync_RepeatedPlayersAppendWithoutChangingExistingOrOtherGame()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, game) = await CreateGameContextAsync(db, "Tigers");
        var otherGame = new Game { TeamId = team.Id, SeasonId = game.SeasonId, ScheduledDate = game.ScheduledDate.AddDays(1), ScheduledTime = game.ScheduledTime };
        Assert.Equal(1, await db.SeedGameAsync(otherGame));
        var existing = new Player { FirstName = "Existing" };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(existing));
        Assert.Equal(1, await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = team.Id, PlayerId = existing.Id }));
        Assert.Equal(1, await db.SeedGameLineupPlayerAsync(new GameLineupPlayer { GameId = game.Id, PlayerId = existing.Id, BattingOrder = 4 }));
        var first = new Player { FirstName = "Amy" };
        var second = new Player { FirstName = "Bob" };

        Assert.True((await db.CreatePlayerRepository().CreateForGameLineupAsync(first, team.Id, game.Id)).IsSuccess);
        Assert.True((await db.CreatePlayerRepository().CreateForGameLineupAsync(second, team.Id, game.Id)).IsSuccess);

        var lineup = await db.CreateGameLineupRepository().GetForGameAsync(game.Id);
        Assert.Equal([existing.Id, first.Id, second.Id], lineup.Select(item => item.PlayerId));
        Assert.Equal([4, 5, 6], lineup.Select(item => item.BattingOrder));
        Assert.Empty(await db.CreateGameLineupRepository().GetForGameAsync(otherGame.Id));
        Assert.Equal(3, (await db.GetTeamPlayersAsync()).Select(item => item.PlayerId).Distinct().Count());
    }

    [Fact]
    public async Task CreateForGameLineupAsync_WrongTeamOwnershipRejectsEntireChain()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (gameTeam, game) = await CreateGameContextAsync(db, "Tigers");
        var otherTeam = new Team { Name = "Bears" };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(otherTeam));

        var result = await db.CreatePlayerRepository().CreateForGameLineupAsync(
            new Player { FirstName = "Amy" }, otherTeam.Id, game.Id);

        Assert.Equal(CreatePlayerFailure.TeamMismatch, result.Failure);
        Assert.Empty(await db.GetPlayersAsync());
        Assert.Empty(await db.GetTeamPlayersAsync());
        Assert.Empty(await db.GetGameLineupPlayersAsync());
        Assert.NotEqual(gameTeam.Id, otherTeam.Id);
    }

    private static async Task<(Team Team, Game Game)> CreateGameContextAsync(TemporaryTestDatabase db, string teamName)
    {
        var team = new Team { Name = teamName };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        var season = new Season { TeamId = team.Id, Name = "Summer", Year = 2026 };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        var game = new Game { TeamId = team.Id, SeasonId = season.Id, ScheduledDate = new(2026, 8, 20), ScheduledTime = new(18, 30, 0) };
        Assert.Equal(1, await db.SeedGameAsync(game));
        return (team, game);
    }

    private static async Task<Player> CreatePlayerAsync(
        PlayerRepository repository,
        string firstName,
        string? lastName,
        bool isActive = true)
    {
        var player = new Player
        {
            FirstName = firstName,
            LastName = lastName,
        };

        var rowsInserted = await repository.CreateAsync(player);
        Assert.Equal(1, rowsInserted);

        if (!isActive)
        {
            var rowsAffected = await repository.DeactivateAsync([player.Id]);
            Assert.Equal(1, rowsAffected);
        }

        return player;
    }

    private static async Task<List<Player>> GetAllPlayersAsync(PlayerRepository repository)
    {
        var activePlayers = await repository.GetActiveAsync();
        var inactivePlayers = await repository.GetInactiveAsync();
        return activePlayers.Concat(inactivePlayers).ToList();
    }
}
