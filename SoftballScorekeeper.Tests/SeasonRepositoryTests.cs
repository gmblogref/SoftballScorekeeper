using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class SeasonRepositoryTests
{
    [Fact]
    public async Task GetForTeamAsync_SameTeamHasSeasonsAcrossYears_ReturnsPersistedValues()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase, "Tigers");
        var fall = await CreateSeasonAsync(testDatabase, team, "Fall", 2026);
        var invitational = await CreateSeasonAsync(testDatabase, team, "Spring Invitational", 2027);

        var seasons = await testDatabase.CreateSeasonRepository().GetForTeamAsync(team.Id);

        Assert.Equal(2, seasons.Count);
        Assert.Equal(
            [
                (fall.Id, team.Id, "Fall", 2026),
                (invitational.Id, team.Id, "Spring Invitational", 2027),
            ],
            seasons.Select(season => (season.Id, season.TeamId, season.Name, season.Year)));
    }

    [Fact]
    public async Task GetForTeamAsync_FewerThanFourSeasons_ReturnsEverySeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        await CreateSeasonAsync(testDatabase, team, "Only Session", 2026);

        var seasons = await testDatabase.CreateSeasonRepository().GetForTeamAsync(team.Id);

        Assert.Equal(["Only Session"], seasons.Select(season => season.Name));
    }

    [Fact]
    public async Task GetForTeamAsync_MoreThanFourArbitrarySeasons_ReturnsEverySeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var names = new[] { "A League", "Bash Series", "City Nights", "Dome Session", "Extra Innings" };
        foreach (var name in names)
        {
            await CreateSeasonAsync(testDatabase, team, name, 2026);
        }

        var seasons = await testDatabase.CreateSeasonRepository().GetForTeamAsync(team.Id);

        Assert.Equal(names, seasons.Select(season => season.Name));
    }

    [Fact]
    public async Task GetForTeamAsync_MultipleTeams_ReturnsOnlyRequestedTeamSeasons()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var selectedSeason = await CreateSeasonAsync(testDatabase, selectedTeam, "Summer", 2026);
        await CreateSeasonAsync(testDatabase, otherTeam, "Fall", 2026);

        var seasons = await testDatabase.CreateSeasonRepository().GetForTeamAsync(selectedTeam.Id);

        Assert.Equal([selectedSeason.Id], seasons.Select(season => season.Id));
        Assert.All(seasons, season => Assert.Equal(selectedTeam.Id, season.TeamId));
    }

    [Fact]
    public async Task GetForTeamAsync_OrdersByYearThenDefaultNameThenId()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var laterYear = await CreateSeasonAsync(testDatabase, team, "Alpha", 2027);
        var zulu = await CreateSeasonAsync(testDatabase, team, "Zulu", 2026);
        var firstAlpha = await CreateSeasonAsync(testDatabase, team, "Alpha", 2026);
        var secondAlpha = await CreateSeasonAsync(testDatabase, team, "Alpha", 2026);

        var seasons = await testDatabase.CreateSeasonRepository().GetForTeamAsync(team.Id);

        Assert.Equal(
            [firstAlpha.Id, secondAlpha.Id, zulu.Id, laterYear.Id],
            seasons.Select(season => season.Id));
    }

    [Fact]
    public async Task GetByIdForTeamAsync_SeasonBelongsToTeam_ReturnsSeason()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer 2", 2026);

        var result = await testDatabase.CreateSeasonRepository()
            .GetByIdForTeamAsync(team.Id, season.Id);

        Assert.NotNull(result);
        Assert.Equal((season.Id, team.Id, "Summer 2", 2026),
            (result.Id, result.TeamId, result.Name, result.Year));
    }

    [Fact]
    public async Task GetByIdForTeamAsync_SeasonBelongsToAnotherTeam_ReturnsNull()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        var otherSeason = await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);

        var result = await testDatabase.CreateSeasonRepository()
            .GetByIdForTeamAsync(selectedTeam.Id, otherSeason.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task Queries_InvalidOrMissingIds_ReturnSafeResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Summer", 2026);
        var repository = testDatabase.CreateSeasonRepository();

        Assert.Empty(await repository.GetForTeamAsync(0));
        Assert.Empty(await repository.GetForTeamAsync(-1));
        Assert.Empty(await repository.GetForTeamAsync(int.MaxValue));
        Assert.Null(await repository.GetByIdForTeamAsync(0, season.Id));
        Assert.Null(await repository.GetByIdForTeamAsync(-1, season.Id));
        Assert.Null(await repository.GetByIdForTeamAsync(team.Id, 0));
        Assert.Null(await repository.GetByIdForTeamAsync(team.Id, -1));
        Assert.Null(await repository.GetByIdForTeamAsync(team.Id, int.MaxValue));
        Assert.Null(await repository.GetByIdForTeamAsync(int.MaxValue, season.Id));
        Assert.False(await repository.HasSeasonsForTeamAsync(0));
        Assert.False(await repository.HasSeasonsForTeamAsync(-1));
        Assert.False(await repository.HasSeasonsForTeamAsync(int.MaxValue));
    }

    [Fact]
    public async Task HasSeasonsForTeamAsync_NoneOneAndMultiple_ReturnsExpectedResults()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var emptyTeam = await CreateTeamAsync(testDatabase, "Empty");
        var oneSeasonTeam = await CreateTeamAsync(testDatabase, "One");
        var multipleSeasonTeam = await CreateTeamAsync(testDatabase, "Multiple");
        await CreateSeasonAsync(testDatabase, oneSeasonTeam, "Summer", 2026);
        await CreateSeasonAsync(testDatabase, multipleSeasonTeam, "Fall", 2026);
        await CreateSeasonAsync(testDatabase, multipleSeasonTeam, "Spring", 2027);
        var repository = testDatabase.CreateSeasonRepository();

        Assert.False(await repository.HasSeasonsForTeamAsync(emptyTeam.Id));
        Assert.True(await repository.HasSeasonsForTeamAsync(oneSeasonTeam.Id));
        Assert.True(await repository.HasSeasonsForTeamAsync(multipleSeasonTeam.Id));
    }

    [Fact]
    public async Task HasSeasonsForTeamAsync_OnlyOtherTeamHasSeason_ReturnsFalse()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var selectedTeam = await CreateTeamAsync(testDatabase, "Tigers");
        var otherTeam = await CreateTeamAsync(testDatabase, "Royals");
        await CreateSeasonAsync(testDatabase, otherTeam, "Summer", 2026);

        var result = await testDatabase.CreateSeasonRepository()
            .HasSeasonsForTeamAsync(selectedTeam.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task Queries_DoNotMutatePersistedSeasons()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(testDatabase);
        var season = await CreateSeasonAsync(testDatabase, team, "Rainout Makeup", 2025);
        var repository = testDatabase.CreateSeasonRepository();

        Assert.Single(await repository.GetForTeamAsync(team.Id));
        Assert.NotNull(await repository.GetByIdForTeamAsync(team.Id, season.Id));
        Assert.True(await repository.HasSeasonsForTeamAsync(team.Id));

        var persisted = Assert.Single(await testDatabase.GetSeasonsAsync());
        Assert.Equal(
            (season.Id, team.Id, "Rainout Makeup", 2025),
            (persisted.Id, persisted.TeamId, persisted.Name, persisted.Year));
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
}
