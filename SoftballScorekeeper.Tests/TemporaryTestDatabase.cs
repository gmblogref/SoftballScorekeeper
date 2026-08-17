using SQLite;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

internal sealed class TemporaryTestDatabase : IAsyncDisposable
{
    private const string DatabaseFileName = "repository-tests.db3";
    private bool isDisposed;

    private TemporaryTestDatabase()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            $"SoftballScorekeeper.Tests-{Guid.NewGuid():N}");
        DatabasePath = Path.Combine(DirectoryPath, DatabaseFileName);
        Database = new AppDatabase(DatabasePath);
    }

    public AppDatabase Database { get; }

    public string DatabasePath { get; }

    public string DirectoryPath { get; }

    public static async Task<TemporaryTestDatabase> CreateAsync()
    {
        var testDatabase = await CreateUninitializedAsync();

        try
        {
            await testDatabase.Database.InitializeAsync();
            return testDatabase;
        }
        catch
        {
            await testDatabase.DisposeAsync();
            throw;
        }
    }

    public static Task<TemporaryTestDatabase> CreateUninitializedAsync()
    {
        var testDatabase = new TemporaryTestDatabase();
        Directory.CreateDirectory(testDatabase.DirectoryPath);
        return Task.FromResult(testDatabase);
    }

    public TeamRepository CreateTeamRepository()
    {
        return new TeamRepository(Database);
    }

    public PlayerRepository CreatePlayerRepository()
    {
        return new PlayerRepository(Database);
    }

    public TeamPlayerRepository CreateTeamPlayerRepository()
    {
        return new TeamPlayerRepository(Database);
    }

    public SeasonRepository CreateSeasonRepository()
    {
        return new SeasonRepository(Database);
    }

    public GameRepository CreateGameRepository()
    {
        return new GameRepository(Database);
    }

    public GameLineupRepository CreateGameLineupRepository() => new(Database);

    public GameScoringSessionRepository CreateGameScoringSessionRepository() => new(Database);

    public Task<List<GameScoringSession>> GetGameScoringSessionsAsync() =>
        UseSeedingConnectionAsync(connection => connection.Table<GameScoringSession>().OrderBy(item => item.Id).ToListAsync());

    public Task<List<GameScoringLineupPlayer>> GetGameScoringLineupPlayersAsync() =>
        UseSeedingConnectionAsync(connection => connection.Table<GameScoringLineupPlayer>()
            .OrderBy(item => item.GameScoringSessionId)
            .ThenBy(item => item.BattingPosition)
            .ToListAsync());

    public Task<int> UpdateScoringStateAsync(
        int sessionId,
        int homeScore,
        int visitorScore,
        int currentBatterPosition) =>
        UseSeedingConnectionAsync(connection => connection.ExecuteAsync(
            "UPDATE GameScoringSession SET HomeScore = ?, VisitorScore = ?, CurrentBatterPosition = ? WHERE Id = ?",
            homeScore, visitorScore, currentBatterPosition, sessionId));

    public Task<int> ExecuteAsync(string command, params object[] arguments) =>
        UseSeedingConnectionAsync(connection => connection.ExecuteAsync(command, arguments));

    public Task<TResult> UseConnectionAsync<TResult>(
        Func<SQLiteAsyncConnection, Task<TResult>> operation) =>
        UseSeedingConnectionAsync(operation);

    public Task<int> DeleteGameLineupAsync(int gameId) =>
        UseSeedingConnectionAsync(connection => connection.ExecuteAsync(
            "DELETE FROM GameLineupPlayer WHERE GameId = ?", gameId));

    public Task<int> SeedGameLineupPlayerAsync(GameLineupPlayer item) =>
        UseSeedingConnectionAsync(connection => connection.InsertAsync(item));

    public Task<List<GameLineupPlayer>> GetGameLineupPlayersAsync() =>
        UseSeedingConnectionAsync(connection => connection.Table<GameLineupPlayer>().OrderBy(item => item.GameId).ThenBy(item => item.BattingOrder).ToListAsync());

    public Task<int> SeedTeamPlayerAsync(TeamPlayer teamPlayer)
    {
        return UseSeedingConnectionAsync(connection => connection.InsertAsync(teamPlayer));
    }

    public Task<int> SeedSeasonAsync(Season season)
    {
        return UseSeedingConnectionAsync(connection => connection.InsertAsync(season));
    }

    public Task<int> SeedGameAsync(Game game)
    {
        return UseSeedingConnectionAsync(connection => connection.InsertAsync(game));
    }

    public Task<List<TeamPlayer>> GetTeamPlayersAsync()
    {
        return UseSeedingConnectionAsync(
            connection => connection.Table<TeamPlayer>().OrderBy(item => item.Id).ToListAsync());
    }

    public Task<List<Player>> GetPlayersAsync()
    {
        return UseSeedingConnectionAsync(
            connection => connection.Table<Player>().OrderBy(item => item.Id).ToListAsync());
    }

    public Task<List<Season>> GetSeasonsAsync()
    {
        return UseSeedingConnectionAsync(
            connection => connection.Table<Season>().OrderBy(item => item.Id).ToListAsync());
    }

    public Task<List<Game>> GetGamesAsync()
    {
        return UseSeedingConnectionAsync(
            connection => connection.Table<Game>().OrderBy(item => item.Id).ToListAsync());
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        await Database.CloseAsync();

        DeleteIfExists(DatabasePath);
        DeleteIfExists($"{DatabasePath}-journal");
        DeleteIfExists($"{DatabasePath}-wal");
        DeleteIfExists($"{DatabasePath}-shm");

        if (!Directory.Exists(DirectoryPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(DirectoryPath))
        {
            File.Delete(filePath);
        }

        Directory.Delete(DirectoryPath);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private async Task<TResult> UseSeedingConnectionAsync<TResult>(
        Func<SQLiteAsyncConnection, Task<TResult>> operation)
    {
        var connection = new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        try
        {
            return await operation(connection);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
