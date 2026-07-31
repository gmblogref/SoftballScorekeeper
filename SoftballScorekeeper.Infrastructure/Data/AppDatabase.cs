using SQLite;
using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class AppDatabase
{
    private readonly SQLiteAsyncConnection connection;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public AppDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "Database path cannot be null, empty, or whitespace.",
                nameof(databasePath));
        }

        DatabasePath = databasePath;
        connection = new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public string DatabasePath { get; }

    internal async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        await InitializeAsync();
        return connection;
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync();
        try
        {
            if (isInitialized)
            {
                return;
            }

            await connection.CreateTableAsync<Team>();
            await connection.CreateTableAsync<Player>();
            await connection.CreateTableAsync<TeamPlayer>();
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public Task CloseAsync()
    {
        return connection.CloseAsync();
    }
}
