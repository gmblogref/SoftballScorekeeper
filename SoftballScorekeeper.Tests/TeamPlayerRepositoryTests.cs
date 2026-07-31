using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class TeamPlayerRepositoryTests
{
    [Fact]
    public async Task GetActivePlayersForTeamAsync_EmptyRoster_ReturnsEmptyCollection()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);

        var players = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Empty(players);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_ActiveRelationshipAndPlayer_ReturnsPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, team, player);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        var rosterPlayer = Assert.Single(roster);
        Assert.Equal(player.Id, rosterPlayer.Id);
        Assert.Equal("Amy", rosterPlayer.FirstName);
        Assert.Equal("Adams", rosterPlayer.LastName);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_RelationshipIsInactive_ExcludesPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, team, player, isActive: false);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_PlayerIsInactive_ExcludesPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, team, player);
        Assert.Equal(1, await testDatabase.CreatePlayerRepository().DeactivateAsync([player.Id]));

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_TeamIsInactive_ReturnsEmptyCollection()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, team, player);
        Assert.Equal(1, await testDatabase.CreateTeamRepository().DeactivateAsync(team.Id));

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_OtherTeamRelationshipExists_ExcludesPlayer()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Selected");
        var otherTeam = await CreateTeamAsync(testDatabase, "Other");
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, otherTeam, player);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(selectedTeam.Id);

        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_MultipleActiveRelationships_ReturnsAllPlayers()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var amy = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        var bob = await CreatePlayerAsync(testDatabase, "Bob", "Baker");
        await AddTeamPlayerAsync(testDatabase, team, amy);
        await AddTeamPlayerAsync(testDatabase, team, bob);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal([amy.Id, bob.Id], roster.Select(player => player.Id));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_MixedCaseFirstNames_OrdersCaseInsensitively()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var charlie = await CreatePlayerAsync(testDatabase, "charlie", "Clark");
        var amy = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        var bob = await CreatePlayerAsync(testDatabase, "bob", "Baker");
        await AddTeamPlayerAsync(testDatabase, team, charlie);
        await AddTeamPlayerAsync(testDatabase, team, amy);
        await AddTeamPlayerAsync(testDatabase, team, bob);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal(["Amy", "bob", "charlie"], roster.Select(player => player.FirstName));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_MatchingFirstNames_OrdersLastNamesCaseInsensitively()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var clark = await CreatePlayerAsync(testDatabase, "Amy", "clark");
        var adams = await CreatePlayerAsync(testDatabase, "amy", "Adams");
        var baker = await CreatePlayerAsync(testDatabase, "AMY", "baker");
        await AddTeamPlayerAsync(testDatabase, team, clark);
        await AddTeamPlayerAsync(testDatabase, team, adams);
        await AddTeamPlayerAsync(testDatabase, team, baker);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal([adams.Id, baker.Id, clark.Id], roster.Select(player => player.Id));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_NullLastName_OrdersNullAsEmpty()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var named = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        var unnamed = await CreatePlayerAsync(testDatabase, "Amy", null);
        await AddTeamPlayerAsync(testDatabase, team, named);
        await AddTeamPlayerAsync(testDatabase, team, unnamed);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal([unnamed.Id, named.Id], roster.Select(player => player.Id));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_DuplicatePlayerNames_ReturnsDistinctPlayersById()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var first = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        var second = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        await AddTeamPlayerAsync(testDatabase, team, first);
        await AddTeamPlayerAsync(testDatabase, team, second);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal(2, roster.Count);
        Assert.Equal([first.Id, second.Id], roster.Select(player => player.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_TeamIdIsMissing_ReturnsEmptyCollection()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(int.MaxValue);

        Assert.Empty(roster);
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_TeamIdIsNonPositive_ReturnsEmptyCollection()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamPlayerRepository();

        Assert.Empty(await repository.GetActivePlayersForTeamAsync(0));
        Assert.Empty(await repository.GetActivePlayersForTeamAsync(-1));
    }

    [Fact]
    public async Task GetActivePlayersForTeamAsync_ValidRosterExists_DoesNotAlterPersistedRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var player = await CreatePlayerAsync(testDatabase, "Amy", "Adams");
        var teamPlayer = await AddTeamPlayerAsync(testDatabase, team, player);

        var roster = await testDatabase.CreateTeamPlayerRepository()
            .GetActivePlayersForTeamAsync(team.Id);

        Assert.Single(roster);
        var persistedTeam = Assert.Single(await testDatabase.CreateTeamRepository().GetActiveAsync());
        var persistedPlayer = Assert.Single(await testDatabase.CreatePlayerRepository().GetActiveAsync());
        var persistedTeamPlayer = Assert.Single(await testDatabase.GetTeamPlayersAsync());
        Assert.Equal((team.Id, team.Name, team.IsActive),
            (persistedTeam.Id, persistedTeam.Name, persistedTeam.IsActive));
        Assert.Equal((player.Id, player.FirstName, player.LastName, player.IsActive),
            (persistedPlayer.Id, persistedPlayer.FirstName, persistedPlayer.LastName, persistedPlayer.IsActive));
        Assert.Equal((teamPlayer.Id, teamPlayer.TeamId, teamPlayer.PlayerId, teamPlayer.IsActive),
            (persistedTeamPlayer.Id, persistedTeamPlayer.TeamId, persistedTeamPlayer.PlayerId, persistedTeamPlayer.IsActive));
    }

    private static async Task<Team> CreateTeamAsync(
        TemporaryTestDatabase testDatabase,
        string name = "Falcons")
    {
        var team = new Team { Name = name };
        Assert.Equal(1, await testDatabase.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<Player> CreatePlayerAsync(
        TemporaryTestDatabase testDatabase,
        string firstName,
        string? lastName)
    {
        var player = new Player { FirstName = firstName, LastName = lastName };
        Assert.Equal(1, await testDatabase.CreatePlayerRepository().CreateAsync(player));
        return player;
    }

    private static async Task<TeamPlayer> AddTeamPlayerAsync(
        TemporaryTestDatabase testDatabase,
        Team team,
        Player player,
        bool isActive = true)
    {
        var teamPlayer = new TeamPlayer
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            IsActive = isActive,
        };

        Assert.Equal(1, await testDatabase.SeedTeamPlayerAsync(teamPlayer));
        return teamPlayer;
    }
}
