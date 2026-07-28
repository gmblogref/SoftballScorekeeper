using SQLite;

namespace SoftballScorekeeper.Data;

public sealed class AppDatabase
{
    public const string DatabaseFilename = "SoftballScorekeeper.db3";

    private readonly SQLiteAsyncConnection connection;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public AppDatabase()
    {
        DatabasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
        connection = new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public string DatabasePath { get; }

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

            await connection.ExecuteScalarAsync<int>("SELECT 1");
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }
}
