using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class TeamRepositoryTests
{
    [Fact]
    public async Task CreateAsync_ValidTeam_PersistsSingleActiveTeam()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = new Team { Name = "Falcons" };

        var rowsInserted = await repository.CreateAsync(team);
        var activeTeams = await repository.GetActiveAsync();
        var inactiveTeams = await repository.GetInactiveAsync();

        Assert.Equal(1, rowsInserted);
        Assert.True(team.Id > 0);
        var persistedTeam = Assert.Single(activeTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.True(persistedTeam.IsActive);
        Assert.Empty(inactiveTeams);
    }

    [Fact]
    public async Task GetActiveAsync_ActiveAndInactiveTeamsExist_ReturnsOnlyActiveTeams()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var falcons = await CreateTeamAsync(repository, "Falcons");
        var royals = await CreateTeamAsync(repository, "Royals");
        await CreateTeamAsync(repository, "Tigers", isActive: false);

        var activeTeams = await repository.GetActiveAsync();

        Assert.Equal(2, activeTeams.Count);
        Assert.Equal([falcons.Id, royals.Id], activeTeams.Select(team => team.Id));
        Assert.Equal(["Falcons", "Royals"], activeTeams.Select(team => team.Name));
        Assert.All(activeTeams, team => Assert.True(team.IsActive));
    }

    [Fact]
    public async Task GetActiveAsync_UnorderedTeamsExist_ReturnsTeamsOrderedByName()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Tigers");
        await CreateTeamAsync(repository, "Falcons");
        await CreateTeamAsync(repository, "Royals");

        var activeTeams = await repository.GetActiveAsync();

        Assert.Equal(["Falcons", "Royals", "Tigers"], activeTeams.Select(team => team.Name));
    }

    [Fact]
    public async Task GetInactiveAsync_ActiveAndUnorderedInactiveTeamsExist_ReturnsOnlyOrderedInactiveTeams()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Active Team");
        var tigers = await CreateTeamAsync(repository, "Tigers", isActive: false);
        var falcons = await CreateTeamAsync(repository, "Falcons", isActive: false);
        var royals = await CreateTeamAsync(repository, "Royals", isActive: false);

        var inactiveTeams = await repository.GetInactiveAsync();

        Assert.Equal([falcons.Id, royals.Id, tigers.Id], inactiveTeams.Select(team => team.Id));
        Assert.Equal(["Falcons", "Royals", "Tigers"], inactiveTeams.Select(team => team.Name));
        Assert.All(inactiveTeams, team => Assert.False(team.IsActive));
    }

    [Fact]
    public async Task ActiveNameExistsAsync_ExactActiveNameAndDifferentName_ReturnsExpectedResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons");

        var exactNameExists = await repository.ActiveNameExistsAsync("Falcons");
        var differentNameExists = await repository.ActiveNameExistsAsync("Royals");

        Assert.True(exactNameExists);
        Assert.False(differentNameExists);
    }

    [Fact]
    public async Task ActiveNameExistsAsync_NameDiffersByCaseOrWhitespace_ReturnsTrue()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons");

        Assert.True(await repository.ActiveNameExistsAsync("falcons"));
        Assert.True(await repository.ActiveNameExistsAsync(" Falcons"));
        Assert.True(await repository.ActiveNameExistsAsync("Falcons "));
        Assert.True(await repository.ActiveNameExistsAsync("  falcons  "));
    }

    [Fact]
    public async Task ActiveNameExistsAsync_OnlyInactiveTeamMatches_ReturnsFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons", isActive: false);

        var nameExists = await repository.ActiveNameExistsAsync("Falcons");

        Assert.False(nameExists);
    }

    [Fact]
    public async Task ActiveNameExistsAsync_CurrentTeamIsExcluded_ReturnsFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons");

        var nameExists = await repository.ActiveNameExistsAsync("Falcons", team.Id);

        Assert.False(nameExists);
    }

    [Fact]
    public async Task ActiveNameExistsAsync_OtherTeamMatches_ReturnsTrueDespiteExclusion()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var excludedTeam = await CreateTeamAsync(repository, "Falcons");
        await CreateTeamAsync(repository, " falcons ", year: 2027);

        var excludingOneTeam = await repository.ActiveNameExistsAsync("FALCONS", excludedTeam.Id);
        var excludingMissingTeam = await repository.ActiveNameExistsAsync("Falcons", int.MaxValue);

        Assert.True(excludingOneTeam);
        Assert.True(excludingMissingTeam);
    }

    [Fact]
    public async Task InactiveNameExistsAsync_ExactInactiveNameAndDifferentName_ReturnsExpectedResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons", isActive: false);

        var exactNameExists = await repository.InactiveNameExistsAsync("Falcons");
        var differentNameExists = await repository.InactiveNameExistsAsync("Royals");

        Assert.True(exactNameExists);
        Assert.False(differentNameExists);
    }

    [Fact]
    public async Task InactiveNameExistsAsync_NameDiffersByCaseOrWhitespace_ReturnsTrue()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons", isActive: false);

        Assert.True(await repository.InactiveNameExistsAsync("falcons"));
        Assert.True(await repository.InactiveNameExistsAsync("  falcons  "));
    }

    [Fact]
    public async Task InactiveNameExistsAsync_OnlyActiveTeamMatches_ReturnsFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        await CreateTeamAsync(repository, "Falcons");

        var nameExists = await repository.InactiveNameExistsAsync("Falcons");

        Assert.False(nameExists);
    }

    [Fact]
    public async Task InactiveNameExistsAsync_CurrentTeamIsExcluded_ReturnsFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons", isActive: false);

        var nameExists = await repository.InactiveNameExistsAsync("Falcons", team.Id);

        Assert.False(nameExists);
    }

    [Fact]
    public async Task InactiveNameExistsAsync_OtherTeamMatches_ReturnsTrueDespiteExclusion()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var excludedTeam = await CreateTeamAsync(repository, "Falcons", isActive: false);
        await CreateTeamAsync(repository, " falcons ", isActive: false, year: 2027);

        var excludingOneTeam = await repository.InactiveNameExistsAsync("FALCONS", excludedTeam.Id);
        var excludingMissingTeam = await repository.InactiveNameExistsAsync(
            "Falcons",
            int.MaxValue
        );

        Assert.True(excludingOneTeam);
        Assert.True(excludingMissingTeam);
    }

    [Fact]
    public async Task DeactivateAsync_ActiveTeamExists_PreservesRowAsInactive()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons");

        var rowsAffected = await repository.DeactivateAsync(team.Id);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(1, rowsAffected);
        Assert.Empty(await repository.GetActiveAsync());
        var persistedTeam = Assert.Single(await repository.GetInactiveAsync());
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.False(persistedTeam.IsActive);
        Assert.Single(allTeams);
    }

    [Fact]
    public async Task DeactivateAsync_MissingOrInvalidId_ReturnsZeroWithoutChangingRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons");

        var missingRowsAffected = await repository.DeactivateAsync(int.MaxValue);
        var invalidRowsAffected = await repository.DeactivateAsync(0);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(0, missingRowsAffected);
        Assert.Equal(0, invalidRowsAffected);
        var persistedTeam = Assert.Single(allTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.True(persistedTeam.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_TeamAlreadyInactive_ReturnsZeroWithoutReplacingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons", isActive: false);

        var rowsAffected = await repository.DeactivateAsync(team.Id);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(0, rowsAffected);
        var persistedTeam = Assert.Single(allTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.False(persistedTeam.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_InactiveTeamExists_PreservesRowAsActive()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons", isActive: false);

        var rowsAffected = await repository.ReactivateAsync(team.Id);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(1, rowsAffected);
        Assert.Empty(await repository.GetInactiveAsync());
        var persistedTeam = Assert.Single(await repository.GetActiveAsync());
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.True(persistedTeam.IsActive);
        Assert.Single(allTeams);
    }

    [Fact]
    public async Task ReactivateAsync_MissingOrInvalidId_ReturnsZeroWithoutChangingRows()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons", isActive: false);

        var missingRowsAffected = await repository.ReactivateAsync(int.MaxValue);
        var invalidRowsAffected = await repository.ReactivateAsync(0);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(0, missingRowsAffected);
        Assert.Equal(0, invalidRowsAffected);
        var persistedTeam = Assert.Single(allTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.False(persistedTeam.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_TeamAlreadyActive_ReturnsZeroWithoutReplacingRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons");

        var rowsAffected = await repository.ReactivateAsync(team.Id);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(0, rowsAffected);
        var persistedTeam = Assert.Single(allTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Falcons", persistedTeam.Name);
        Assert.True(persistedTeam.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_ExistingTeam_UpdatesSameRow()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var repository = testDatabase.CreateTeamRepository();
        var team = await CreateTeamAsync(repository, "Falcons");
        team.Name = "Royals";

        var rowsAffected = await repository.UpdateAsync(team);
        var allTeams = await GetAllTeamsAsync(repository);

        Assert.Equal(1, rowsAffected);
        var persistedTeam = Assert.Single(allTeams);
        Assert.Equal(team.Id, persistedTeam.Id);
        Assert.Equal("Royals", persistedTeam.Name);
        Assert.True(persistedTeam.IsActive);
    }

    private static async Task<Team> CreateTeamAsync(
        TeamRepository repository,
        string name,
        bool isActive = true,
        int year = 2026
    )
    {
        var team = new Team
        {
            Name = name,
            IsActive = isActive,
            Year = year,
        };

        var rowsInserted = await repository.CreateAsync(team);
        Assert.Equal(1, rowsInserted);
        return team;
    }

    private static async Task<List<Team>> GetAllTeamsAsync(TeamRepository repository)
    {
        var activeTeams = await repository.GetActiveAsync();
        var inactiveTeams = await repository.GetInactiveAsync();
        return activeTeams.Concat(inactiveTeams).ToList();
    }
}
