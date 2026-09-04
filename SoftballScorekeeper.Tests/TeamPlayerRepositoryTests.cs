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

    [Fact]
    public async Task RemovePlayersFromTeamAsync_ActiveRelationship_SoftDeactivatesRelationshipOnly()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var relationship = await AddTeamPlayerAsync(db, team, player);

        var result = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(team.Id, [player.Id]);

        Assert.Equal([player.Id], result.RequestedPlayerIds);
        Assert.Equal([player.Id], result.RemovedPlayerIds);
        Assert.Equal(1, result.SuccessfulRemovalCount);
        Assert.Empty(result.AlreadyInactivePlayerIds);
        Assert.Empty(result.NotOnTeamPlayerIds);
        Assert.Empty(result.IgnoredPlayerIds);
        var persistedRelationship = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal(
            (relationship.Id, relationship.TeamId, relationship.PlayerId, false),
            (persistedRelationship.Id, persistedRelationship.TeamId, persistedRelationship.PlayerId, persistedRelationship.IsActive));
        var persistedPlayer = Assert.Single(await db.CreatePlayerRepository().GetActiveAsync());
        Assert.Equal(player.Id, persistedPlayer.Id);
        Assert.True(persistedPlayer.IsActive);
    }

    [Fact]
    public async Task RemovePlayersFromTeamAsync_OnlyChangesSelectedRelationshipOnSelectedTeam()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(db, "Selected");
        var otherTeam = await CreateTeamAsync(db, "Other");
        var selected = await CreatePlayerAsync(db, "Amy", "Able");
        var unselected = await CreatePlayerAsync(db, "Bob", "Baker");
        var selectedRelationship = await AddTeamPlayerAsync(db, selectedTeam, selected);
        var unselectedRelationship = await AddTeamPlayerAsync(db, selectedTeam, unselected);
        var otherTeamRelationship = await AddTeamPlayerAsync(db, otherTeam, selected);

        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(selectedTeam.Id, [selected.Id]);

        var relationships = await db.GetTeamPlayersAsync();
        Assert.Equal(3, relationships.Count);
        Assert.Contains(relationships, item => item.Id == selectedRelationship.Id && !item.IsActive);
        Assert.Contains(relationships, item => item.Id == unselectedRelationship.Id && item.IsActive);
        Assert.Contains(relationships, item => item.Id == otherTeamRelationship.Id && item.IsActive);
        Assert.Equal(2, (await db.CreatePlayerRepository().GetActiveAsync()).Count);
    }

    [Fact]
    public async Task RemovePlayersFromTeamAsync_DuplicateIds_DeactivatesAllMatchingActiveRowsAndReportsPlayerOnce()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        await AddTeamPlayerAsync(db, team, player);
        await AddTeamPlayerAsync(db, team, player);

        var result = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(
            team.Id,
            [player.Id, player.Id]);

        Assert.Equal([player.Id], result.RequestedPlayerIds);
        Assert.Equal([player.Id], result.RemovedPlayerIds);
        var relationships = await db.GetTeamPlayersAsync();
        Assert.Equal(2, relationships.Count);
        Assert.All(relationships, item => Assert.False(item.IsActive));
    }

    [Fact]
    public async Task RemovePlayersFromTeamAsync_ClassifiesInvalidMissingNotOnTeamAndAlreadyInactiveIds()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var inactiveMember = await CreatePlayerAsync(db, "Amy", "Able");
        var notOnTeam = await CreatePlayerAsync(db, "Bob", "Baker");
        var inactivePlayer = await CreatePlayerAsync(db, "Cara", "Clark");
        await AddTeamPlayerAsync(db, team, inactiveMember, isActive: false);
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([inactivePlayer.Id]));

        var result = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(
            team.Id,
            [inactiveMember.Id, notOnTeam.Id, inactivePlayer.Id, int.MaxValue, 0, -1]);

        Assert.Empty(result.RemovedPlayerIds);
        Assert.Equal([inactiveMember.Id], result.AlreadyInactivePlayerIds);
        Assert.Equal([notOnTeam.Id], result.NotOnTeamPlayerIds);
        Assert.Equal([inactivePlayer.Id, int.MaxValue, 0, -1], result.IgnoredPlayerIds);
        Assert.Single(await db.GetTeamPlayersAsync());
    }

    [Fact]
    public async Task RemovePlayersFromTeamAsync_NullEmptyMissingOrInactiveTeam_PerformsNoWrites()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var activeTeam = await CreateTeamAsync(db, "Active");
        var inactiveTeam = await CreateTeamAsync(db, "Inactive");
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var relationship = await AddTeamPlayerAsync(db, activeTeam, player);
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(inactiveTeam.Id));
        var repository = db.CreateTeamPlayerRepository();

        Assert.Empty((await repository.RemovePlayersFromTeamAsync(activeTeam.Id, null)).RequestedPlayerIds);
        Assert.Empty((await repository.RemovePlayersFromTeamAsync(activeTeam.Id, [])).RequestedPlayerIds);
        Assert.Equal([player.Id], (await repository.RemovePlayersFromTeamAsync(int.MaxValue, [player.Id])).IgnoredPlayerIds);
        Assert.Equal([player.Id], (await repository.RemovePlayersFromTeamAsync(inactiveTeam.Id, [player.Id])).IgnoredPlayerIds);
        var persisted = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal((relationship.Id, true), (persisted.Id, persisted.IsActive));
    }

    [Fact]
    public async Task RemovePlayersFromTeamAsync_MixedRequest_ReportsAccurateCompletePartialAndNoOpCounts()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var first = await CreatePlayerAsync(db, "Amy", "Able");
        var second = await CreatePlayerAsync(db, "Bob", "Baker");
        var notOnTeam = await CreatePlayerAsync(db, "Cara", "Clark");
        await AddTeamPlayerAsync(db, team, first);
        await AddTeamPlayerAsync(db, team, second);

        var complete = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(team.Id, [first.Id]);
        var partial = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(team.Id, [second.Id, notOnTeam.Id]);
        var noOp = await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(team.Id, [first.Id]);

        Assert.Equal(1, complete.SuccessfulRemovalCount);
        Assert.Equal(1, partial.SuccessfulRemovalCount);
        Assert.Equal([notOnTeam.Id], partial.NotOnTeamPlayerIds);
        Assert.Equal(0, noOp.SuccessfulRemovalCount);
        Assert.Equal([first.Id], noOp.AlreadyInactivePlayerIds);
    }

    [Fact]
    public async Task RemoveThenAddPlayer_ReactivatesSameRelationshipWithoutDuplicate()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var original = await AddTeamPlayerAsync(db, team, player);
        var repository = db.CreateTeamPlayerRepository();

        Assert.Equal([player.Id], (await repository.RemovePlayersFromTeamAsync(team.Id, [player.Id])).RemovedPlayerIds);
        Assert.Equal([player.Id], (await repository.GetEligiblePlayersForTeamAsync(team.Id)).Select(item => item.Id));
        Assert.Equal([player.Id], (await repository.AddPlayersToTeamAsync(team.Id, [player.Id])).ReactivatedPlayerIds);

        var relationship = Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Equal(original.Id, relationship.Id);
        Assert.True(relationship.IsActive);
        Assert.Single(await repository.GetActivePlayersForTeamAsync(team.Id));
    }

    [Fact]
    public async Task GetActiveRosterForTeamAsync_ReturnsMembershipIdentityAndTeamSpecificJerseyNumber()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var alpha = await CreateTeamAsync(db, "Alpha");
        var beta = await CreateTeamAsync(db, "Beta");
        var amy = await CreatePlayerAsync(db, "Amy", "Able");
        var bob = await CreatePlayerAsync(db, "Bob", "Baker");
        var alphaAmy = await AddTeamPlayerAsync(db, alpha, amy, jerseyNumber: 7);
        await AddTeamPlayerAsync(db, alpha, bob);
        await AddTeamPlayerAsync(db, beta, amy, jerseyNumber: 22);

        var roster = await db.CreateTeamPlayerRepository().GetActiveRosterForTeamAsync(alpha.Id);

        Assert.Collection(
            roster,
            item =>
            {
                Assert.Equal((alphaAmy.Id, alpha.Id, amy.Id),
                    (item.TeamPlayerId, item.TeamId, item.Player.Id));
                Assert.Equal(7, item.JerseyNumber);
            },
            item =>
            {
                Assert.Equal(bob.Id, item.Player.Id);
                Assert.Null(item.JerseyNumber);
            });
    }

    [Fact]
    public async Task UpdateJerseyNumberAsync_SetsChangesAndClearsOnlyRequestedMembership()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var alpha = await CreateTeamAsync(db, "Alpha");
        var beta = await CreateTeamAsync(db, "Beta");
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var alphaMembership = await AddTeamPlayerAsync(db, alpha, player);
        var betaMembership = await AddTeamPlayerAsync(db, beta, player, jerseyNumber: 22);
        var repository = db.CreateTeamPlayerRepository();

        Assert.Equal(1, await repository.UpdateJerseyNumberAsync(alpha.Id, alphaMembership.Id, 7));
        Assert.Equal(1, await repository.UpdateJerseyNumberAsync(alpha.Id, alphaMembership.Id, 11));
        Assert.Equal(1, await repository.UpdateJerseyNumberAsync(alpha.Id, alphaMembership.Id, null));

        var relationships = await db.GetTeamPlayersAsync();
        Assert.Null(relationships.Single(item => item.Id == alphaMembership.Id).JerseyNumber);
        Assert.Equal(22, relationships.Single(item => item.Id == betaMembership.Id).JerseyNumber);
    }

    [Fact]
    public async Task UpdateJerseyNumberAsync_InvalidOrInactiveTargets_PerformNoWrites()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var activeTeam = await CreateTeamAsync(db, "Active");
        var inactiveTeam = await CreateTeamAsync(db, "Inactive");
        var activePlayer = await CreatePlayerAsync(db, "Amy", "Able");
        var inactivePlayer = await CreatePlayerAsync(db, "Bob", "Baker");
        var activeMembership = await AddTeamPlayerAsync(db, activeTeam, activePlayer, jerseyNumber: 7);
        var inactiveMembership = await AddTeamPlayerAsync(db, activeTeam, activePlayer, isActive: false, jerseyNumber: 8);
        var inactiveTeamMembership = await AddTeamPlayerAsync(db, inactiveTeam, activePlayer, jerseyNumber: 9);
        var inactivePlayerMembership = await AddTeamPlayerAsync(db, activeTeam, inactivePlayer, jerseyNumber: 10);
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(inactiveTeam.Id));
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([inactivePlayer.Id]));
        var repository = db.CreateTeamPlayerRepository();

        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(activeTeam.Id, activeMembership.Id, -1));
        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(activeTeam.Id, int.MaxValue, 12));
        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(activeTeam.Id, inactiveMembership.Id, 12));
        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(inactiveTeam.Id, inactiveTeamMembership.Id, 12));
        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(activeTeam.Id, inactivePlayerMembership.Id, 12));
        Assert.Equal(0, await repository.UpdateJerseyNumberAsync(inactiveTeam.Id, activeMembership.Id, 12));

        var relationships = await db.GetTeamPlayersAsync();
        Assert.Equal(7, relationships.Single(item => item.Id == activeMembership.Id).JerseyNumber);
        Assert.Equal(8, relationships.Single(item => item.Id == inactiveMembership.Id).JerseyNumber);
        Assert.Equal(9, relationships.Single(item => item.Id == inactiveTeamMembership.Id).JerseyNumber);
        Assert.Equal(10, relationships.Single(item => item.Id == inactivePlayerMembership.Id).JerseyNumber);
    }

    [Fact]
    public async Task InitializeAsync_ExistingTeamPlayerTable_AddsNullableJerseyNumberColumn()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.ExecuteAsync(
            """
            CREATE TABLE TeamPlayer (
                Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                TeamId INTEGER NOT NULL,
                PlayerId INTEGER NOT NULL,
                IsActive INTEGER NOT NULL)
            """);

        await db.Database.InitializeAsync();

        var columns = await db.UseConnectionAsync(
            connection => connection.GetTableInfoAsync(nameof(TeamPlayer)));
        Assert.Contains(columns, column => column.Name == nameof(TeamPlayer.JerseyNumber));

        var team = await CreateTeamAsync(db);
        var player = await CreatePlayerAsync(db, "Amy", "Able");
        var membership = await AddTeamPlayerAsync(db, team, player);
        Assert.Equal(1, await db.CreateTeamPlayerRepository()
            .UpdateJerseyNumberAsync(team.Id, membership.Id, 7));
        Assert.Equal(7, Assert.Single(await db.GetTeamPlayersAsync()).JerseyNumber);
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
        bool isActive = true,
        int? jerseyNumber = null)
    {
        var teamPlayer = new TeamPlayer
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            JerseyNumber = jerseyNumber,
            IsActive = isActive,
        };

        Assert.Equal(1, await testDatabase.SeedTeamPlayerAsync(teamPlayer));
        return teamPlayer;
    }
}
