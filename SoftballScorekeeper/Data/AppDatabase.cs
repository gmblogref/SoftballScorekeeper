using SQLite;
using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.Data;

public sealed class AppDatabase
{
    private readonly SQLiteAsyncConnection connection;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public AppDatabase(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.DatabaseFileName))
        {
            throw new ArgumentException(
                "Database filename cannot be null, empty, or whitespace.",
                nameof(settings.DatabaseFileName));
        }

        DatabasePath = Path.Combine(FileSystem.AppDataDirectory, settings.DatabaseFileName);
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
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }
}
