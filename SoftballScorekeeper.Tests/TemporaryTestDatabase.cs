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
        var testDatabase = new TemporaryTestDatabase();

        try
        {
            Directory.CreateDirectory(testDatabase.DirectoryPath);
            await testDatabase.Database.InitializeAsync();
            return testDatabase;
        }
        catch
        {
            await testDatabase.DisposeAsync();
            throw;
        }
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
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

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
