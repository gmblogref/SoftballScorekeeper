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

    [Fact]
    public async Task GetEligiblePlayersForTeamAsync_ExcludesActiveMemberAndInactivePlayer_ButIncludesInactiveAndOtherTeamMembers()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Selected");
        var otherTeam = await CreateTeamAsync(db, "Other");
        var unassociated = await CreatePlayerAsync(db, "Amy", "Able");
        var activeMember = await CreatePlayerAsync(db, "Bob", "Baker");
        var inactiveMember = await CreatePlayerAsync(db, "Cara", "Clark");
        var otherMember = await CreatePlayerAsync(db, "Drew", "Dover");
        var inactivePlayer = await CreatePlayerAsync(db, "Evan", "Evans");
        await AddTeamPlayerAsync(db, team, activeMember);
        await AddTeamPlayerAsync(db, team, inactiveMember, isActive: false);
        await AddTeamPlayerAsync(db, otherTeam, otherMember);
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([inactivePlayer.Id]));

        var eligible = await db.CreateTeamPlayerRepository().GetEligiblePlayersForTeamAsync(team.Id);

        Assert.Equal(
            [unassociated.Id, inactiveMember.Id, otherMember.Id],
            eligible.Select(player => player.Id));
    }

    [Fact]
    public async Task GetEligiblePlayersForTeamAsync_MissingOrInactiveTeam_ReturnsEmpty()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        await CreatePlayerAsync(db, "Amy", "Able");
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(team.Id));
        var repository = db.CreateTeamPlayerRepository();

        Assert.Empty(await repository.GetEligiblePlayersForTeamAsync(int.MaxValue));
        Assert.Empty(await repository.GetEligiblePlayersForTeamAsync(team.Id));
        Assert.Empty(await repository.GetEligiblePlayersForTeamAsync(0));
    }

    [Fact]
    public async Task GetEligiblePlayersForTeamAsync_OrdersCaseInsensitivelyAndKeepsDuplicateNamesDistinct()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var charlie = await CreatePlayerAsync(db, "charlie", "Zulu");
        var amyNull = await CreatePlayerAsync(db, "Amy", null);
        var amyAble = await CreatePlayerAsync(db, "amy", "able");
        var duplicate = await CreatePlayerAsync(db, "amy", "able");

        var eligible = await db.CreateTeamPlayerRepository().GetEligiblePlayersForTeamAsync(team.Id);

        Assert.Equal([amyNull.Id, amyAble.Id, duplicate.Id, charlie.Id], eligible.Select(player => player.Id));
    }

    [Fact]
    public async Task GetEligiblePlayersForTeamAsync_DoesNotMutateRelationships()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var relationship = await AddTeamPlayerAsync(db, team, player, isActive: false);

        Assert.Single(await db.CreateTeamPlayerRepository().GetEligiblePlayersForTeamAsync(team.Id));

        var persisted = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal((relationship.Id, false), (persisted.Id, persisted.IsActive));
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_NewPlayer_InsertsActiveRelationshipAndReportsCounts()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");

        var result = await db.CreateTeamPlayerRepository().AddPlayersToTeamAsync(team.Id, [player.Id]);

        Assert.Equal([player.Id], result.RequestedPlayerIds);
        Assert.Equal([player.Id], result.EligiblePlayerIds);
        Assert.Equal([player.Id], result.InsertedPlayerIds);
        Assert.Empty(result.ReactivatedPlayerIds);
        Assert.Empty(result.AlreadyActivePlayerIds);
        Assert.Empty(result.IgnoredPlayerIds);
        Assert.Equal(1, result.SuccessfullyActiveMembershipCount);
        var relationship = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.True(relationship.IsActive);
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_InactiveRelationship_ReactivatesOriginalRow()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var original = await AddTeamPlayerAsync(db, team, player, isActive: false);

        var result = await db.CreateTeamPlayerRepository().AddPlayersToTeamAsync(team.Id, [player.Id]);

        Assert.Equal([player.Id], result.ReactivatedPlayerIds);
        Assert.Empty(result.InsertedPlayerIds);
        Assert.Equal(1, result.SuccessfullyActiveMembershipCount);
        var relationship = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal(original.Id, relationship.Id);
        Assert.True(relationship.IsActive);
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_ActiveRelationship_ReportsAlreadyActiveWithoutDuplicate()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var original = await AddTeamPlayerAsync(db, team, player);

        var result = await db.CreateTeamPlayerRepository().AddPlayersToTeamAsync(team.Id, [player.Id, player.Id]);

        Assert.Equal([player.Id], result.RequestedPlayerIds);
        Assert.Equal([player.Id], result.AlreadyActivePlayerIds);
        Assert.Equal(1, result.SuccessfullyActiveMembershipCount);
        var relationship = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal(original.Id, relationship.Id);
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_MixedValidAndInvalidIds_ReportsAccuratePartialResult()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var inserted = await CreatePlayerAsync(db, "Amy", "Able");
        var reactivated = await CreatePlayerAsync(db, "Bob", "Baker");
        var alreadyActive = await CreatePlayerAsync(db, "Cara", "Clark");
        var inactive = await CreatePlayerAsync(db, "Drew", "Dover");
        await AddTeamPlayerAsync(db, team, reactivated, isActive: false);
        await AddTeamPlayerAsync(db, team, alreadyActive);
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([inactive.Id]));

        var result = await db.CreateTeamPlayerRepository().AddPlayersToTeamAsync(
            team.Id,
            [inserted.Id, reactivated.Id, alreadyActive.Id, inactive.Id, int.MaxValue, 0, -1]);

        Assert.Equal([inserted.Id], result.InsertedPlayerIds);
        Assert.Equal([reactivated.Id], result.ReactivatedPlayerIds);
        Assert.Equal([alreadyActive.Id], result.AlreadyActivePlayerIds);
        Assert.Equal(3, result.SuccessfullyActiveMembershipCount);
        Assert.Equal([inactive.Id, int.MaxValue, 0, -1], result.IgnoredPlayerIds);
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_MissingOrInactiveTeam_PerformsNoWrites()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var inactiveTeam = await CreateTeamAsync(db, "Inactive");
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(inactiveTeam.Id));
        var repository = db.CreateTeamPlayerRepository();

        var missingResult = await repository.AddPlayersToTeamAsync(int.MaxValue, [player.Id]);
        var inactiveResult = await repository.AddPlayersToTeamAsync(inactiveTeam.Id, [player.Id]);

        Assert.Equal([player.Id], missingResult.IgnoredPlayerIds);
        Assert.Equal([player.Id], inactiveResult.IgnoredPlayerIds);
        Assert.Empty(await db.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_EmptyOrNullInput_ReturnsSafely()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var repository = db.CreateTeamPlayerRepository();

        Assert.Empty((await repository.AddPlayersToTeamAsync(team.Id, [])).RequestedPlayerIds);
        Assert.Empty((await repository.AddPlayersToTeamAsync(team.Id, null)).RequestedPlayerIds);
        Assert.Empty(await db.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task AddPlayersToTeamAsync_DoesNotChangeUnrelatedRelationshipsOrOtherTeamMemberships()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(db, "Selected");
        var otherTeam = await CreateTeamAsync(db, "Other");
        var selected = await CreatePlayerAsync(db, "Amy", "Able");
        var unrelated = await CreatePlayerAsync(db, "Bob", "Baker");
        var otherRelationship = await AddTeamPlayerAsync(db, otherTeam, selected);
        var unrelatedRelationship = await AddTeamPlayerAsync(db, selectedTeam, unrelated);

        await db.CreateTeamPlayerRepository().AddPlayersToTeamAsync(selectedTeam.Id, [selected.Id]);

        var relationships = await db.GetTeamPlayersAsync();
        Assert.Equal(3, relationships.Count);
        Assert.Contains(relationships, item => item.Id == otherRelationship.Id && item.IsActive);
        Assert.Contains(relationships, item => item.Id == unrelatedRelationship.Id && item.IsActive);
        Assert.DoesNotContain(relationships, item => !item.IsActive);
    }

    [Fact]
    public async Task GetActiveTeamsForPlayerAsync_ReturnsOnlyActiveMembershipsForActivePlayer()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var otherPlayer = await CreatePlayerAsync(db, "Bob", "Baker");
        var activeTeam = await CreateTeamAsync(db, "Active");
        var inactiveTeam = await CreateTeamAsync(db, "Inactive");
        var inactiveRelationshipTeam = await CreateTeamAsync(db, "Relationship Inactive");
        var otherPlayerTeam = await CreateTeamAsync(db, "Other Player");
        await AddTeamPlayerAsync(db, activeTeam, player);
        await AddTeamPlayerAsync(db, inactiveTeam, player);
        await AddTeamPlayerAsync(db, inactiveRelationshipTeam, player, isActive: false);
        await AddTeamPlayerAsync(db, otherPlayerTeam, otherPlayer);
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(inactiveTeam.Id));

        var teams = await db.CreateTeamPlayerRepository().GetActiveTeamsForPlayerAsync(player.Id);

        Assert.Equal([activeTeam.Id], teams.Select(team => team.Id));
    }

    [Fact]
    public async Task GetActiveTeamsForPlayerAsync_MissingInactiveOrNonPositivePlayer_ReturnsEmpty()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var team = await CreateTeamAsync(db);
        await AddTeamPlayerAsync(db, team, player);
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([player.Id]));
        var repository = db.CreateTeamPlayerRepository();

        Assert.Empty(await repository.GetActiveTeamsForPlayerAsync(player.Id));
        Assert.Empty(await repository.GetActiveTeamsForPlayerAsync(int.MaxValue));
        Assert.Empty(await repository.GetActiveTeamsForPlayerAsync(0));
    }

    [Fact]
    public async Task GetActiveTeamsForPlayerAsync_OrdersCaseInsensitivelyAndKeepsDuplicateNamesDistinct()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var zulu = await CreateTeamAsync(db, "zulu");
        var alpha = await CreateTeamAsync(db, "Alpha");
        var duplicate = await CreateTeamAsync(db, "Alpha");
        await AddTeamPlayerAsync(db, zulu, player);
        await AddTeamPlayerAsync(db, alpha, player);
        await AddTeamPlayerAsync(db, duplicate, player);

        var teams = await db.CreateTeamPlayerRepository().GetActiveTeamsForPlayerAsync(player.Id);

        Assert.Equal([alpha.Id, duplicate.Id, zulu.Id], teams.Select(team => team.Id));
    }

    [Fact]
    public async Task GetActiveTeamsForPlayerAsync_DoesNotMutateRows()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var team = await CreateTeamAsync(db);
        var relationship = await AddTeamPlayerAsync(db, team, player);

        Assert.Single(await db.CreateTeamPlayerRepository().GetActiveTeamsForPlayerAsync(player.Id));

        var persisted = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal((relationship.Id, relationship.IsActive), (persisted.Id, persisted.IsActive));
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
