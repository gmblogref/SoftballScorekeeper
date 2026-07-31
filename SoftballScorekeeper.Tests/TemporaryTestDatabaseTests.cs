using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class TemporaryTestDatabaseTests
{
    [Fact]
    public async Task CreateAsync_InitializesTeamAndPlayerTables()
    {
        await using var testDatabase = await TemporaryTestDatabase.CreateAsync();
        var teamRepository = testDatabase.CreateTeamRepository();
        var playerRepository = testDatabase.CreatePlayerRepository();
        var teamPlayerRepository = testDatabase.CreateTeamPlayerRepository();
        var team = new Team { Name = "Smoke Test Team" };
        var player = new Player { FirstName = "Smoke", LastName = "Tester" };

        var teamRowsInserted = await teamRepository.CreateAsync(team);
        var playerRowsInserted = await playerRepository.CreateAsync(player);
        await testDatabase.SeedTeamPlayerAsync(
            new TeamPlayer { TeamId = team.Id, PlayerId = player.Id });
        await testDatabase.Database.InitializeAsync();
        var teams = await teamRepository.GetActiveAsync();
        var players = await playerRepository.GetActiveAsync();
        var roster = await teamPlayerRepository.GetActivePlayersForTeamAsync(team.Id);

        Assert.Equal(1, teamRowsInserted);
        Assert.Equal(1, playerRowsInserted);
        Assert.Equal(team.Id, Assert.Single(teams).Id);
        Assert.Equal(player.Id, Assert.Single(players).Id);
        Assert.Equal(player.Id, Assert.Single(roster).Id);
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
