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
            await connection.CreateTableAsync<Season>();
            await connection.CreateTableAsync<Game>();
            await connection.CreateTableAsync<GameLineupPlayer>();
            await connection.CreateTableAsync<GameScoringSession>();
            await connection.CreateTableAsync<GameScoringLineupPlayer>();
            await connection.RunInTransactionAsync(UpgradeStory60ScoringSessions);
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

    private static void UpgradeStory60ScoringSessions(SQLiteConnection transaction)
    {
        var sessions = transaction.Table<GameScoringSession>().ToList();
        foreach (var session in sessions)
        {
            var existingSequence = transaction.Table<GameScoringLineupPlayer>()
                .Where(item => item.GameScoringSessionId == session.Id)
                .ToList();
            if (existingSequence.Count > 0)
            {
                continue;
            }

            if (session.CurrentBatterPosition != 0)
            {
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} has incomplete live scoring state.");
            }

            var lineup = transaction.Table<GameLineupPlayer>()
                .Where(item => item.GameId == session.GameId)
                .OrderBy(item => item.BattingOrder)
                .ThenBy(item => item.Id)
                .ToList();
            if (!IsStructurallyValid(lineup))
            {
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} cannot be upgraded from an invalid frozen lineup.");
            }

            foreach (var lineupPlayer in lineup)
            {
                var sequencePlayer = new GameScoringLineupPlayer
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = lineupPlayer.PlayerId,
                    BattingPosition = lineupPlayer.BattingOrder,
                };
                if (transaction.Insert(sequencePlayer) != 1)
                {
                    throw new InvalidOperationException(
                        "Live batting-sequence insertion did not affect one row.");
                }
            }

            session.HomeScore = 0;
            session.VisitorScore = 0;
            session.CurrentBatterPosition = 1;
            if (transaction.Update(session) != 1)
            {
                throw new InvalidOperationException(
                    "Scoring-session compatibility update did not affect one row.");
            }
        }
    }

    private static bool IsStructurallyValid(IReadOnlyList<GameLineupPlayer> lineup) =>
        lineup.Count > 0
        && lineup.Select(item => item.PlayerId).Distinct().Count() == lineup.Count
        && lineup.Select(item => item.BattingOrder)
            .SequenceEqual(Enumerable.Range(1, lineup.Count));
}
