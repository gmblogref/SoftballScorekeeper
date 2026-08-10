using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class TemporaryTestDatabaseTests
{
    [Fact]
    public async Task CreateAsync_InitializesAllCurrentTablesIdempotently()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var teamRepository = testDatabase.CreateTeamRepository();
        var playerRepository = testDatabase.CreatePlayerRepository();
        var teamPlayerRepository = testDatabase.CreateTeamPlayerRepository();
        var team = new Team { Name = "Smoke Test Team" };
        var player = new Player { FirstName = "Smoke", LastName = "Tester" };

        var teamRowsInserted = await teamRepository.CreateAsync(team);
        var playerRowsInserted = await playerRepository.CreateAsync(player);
        var fallSeason = new Season { TeamId = team.Id, Name = "Fall", Year = 2026 };
        var springSeason = new Season { TeamId = team.Id, Name = "Spring Invitational", Year = 2027 };
        var fallSeasonRowsInserted = await testDatabase.SeedSeasonAsync(fallSeason);
        var springSeasonRowsInserted = await testDatabase.SeedSeasonAsync(springSeason);
        var game = new Game
        {
            TeamId = team.Id,
            SeasonId = fallSeason.Id,
            ScheduledDate = new DateTime(2026, 6, 15),
            ScheduledTime = new TimeSpan(18, 30, 0),
        };
        var gameRowsInserted = await testDatabase.SeedGameAsync(game);
        await testDatabase.SeedTeamPlayerAsync(
            new TeamPlayer { TeamId = team.Id, PlayerId = player.Id });
        await testDatabase.Database.InitializeAsync();
        var teams = await teamRepository.GetActiveAsync();
        var players = await playerRepository.GetActiveAsync();
        var roster = await teamPlayerRepository.GetActivePlayersForTeamAsync(team.Id);
        var seasons = await testDatabase.GetSeasonsAsync();
        var games = await testDatabase.GetGamesAsync();

        Assert.Equal(1, teamRowsInserted);
        Assert.Equal(1, playerRowsInserted);
        Assert.Equal(1, fallSeasonRowsInserted);
        Assert.Equal(1, springSeasonRowsInserted);
        Assert.Equal(1, gameRowsInserted);
        Assert.Equal(team.Id, Assert.Single(teams).Id);
        Assert.Equal(player.Id, Assert.Single(players).Id);
        Assert.Equal(player.Id, Assert.Single(roster).Id);
        Assert.Equal(2, seasons.Count);
        Assert.Equal(
            [
                (fallSeason.Id, team.Id, "Fall", 2026),
                (springSeason.Id, team.Id, "Spring Invitational", 2027),
            ],
            seasons.Select(season => (season.Id, season.TeamId, season.Name, season.Year)));
        Assert.Equal(game.Id, Assert.Single(games).Id);
        Assert.True(File.Exists(testDatabase.DatabasePath));
        Assert.Equal("repository-tests.db3", Path.GetFileName(testDatabase.DatabasePath));
        Assert.NotEqual("SoftballScorekeeper.db3", Path.GetFileName(testDatabase.DatabasePath));
        Assert.True(IsUnderTemporaryDirectory(testDatabase.DatabasePath));
    }

    [Fact]
    public async Task DisposeAsync_InitializedDatabase_RemovesTemporaryFiles()
    {
        var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var databasePath = testDatabase.DatabasePath;
        var directoryPath = testDatabase.DirectoryPath;

        Assert.True(File.Exists(databasePath));
        Assert.True(Directory.Exists(directoryPath));

        await testDatabase.DisposeAsync();

        Assert.False(File.Exists(databasePath));
        Assert.False(Directory.Exists(directoryPath));
    }

    private static bool IsUnderTemporaryDirectory(string path)
    {
        var temporaryDirectory = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return fullPath.StartsWith(temporaryDirectory, comparison);
    }
}
