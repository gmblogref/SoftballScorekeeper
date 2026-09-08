using SQLite;
using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class AppDatabase
{
    internal static readonly TimeSpan ScoringHistoryRetention = TimeSpan.FromDays(21);
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
            var legacyScoringRules = await RequiresScoringRulesCompatibilityAsync();
            await connection.CreateTableAsync<GameScoringSession>();
            await connection.CreateTableAsync<GameScoringLineupPlayer>();
            await connection.CreateTableAsync<GamePlateAppearance>();
            await connection.CreateTableAsync<GameScoringBaseRunner>();
            await connection.CreateTableAsync<GamePlateAppearanceRunnerMovement>();
            await connection.CreateTableAsync<GameScoringHalfInning>();
            await connection.CreateTableAsync<GameScoringPendingPlay>();
            await connection.CreateTableAsync<GameScoringPendingRunnerMovement>();
            await connection.CreateTableAsync<GameScoringHistory>();
            await connection.CreateTableAsync<GameScoringHistorySnapshot>();
            await connection.CreateTableAsync<BeerLedgerEntry>();
            await connection.CreateTableAsync<TeamBeerRuleConfiguration>();
            await connection.CreateTableAsync<GameScoringBeerRuleConfiguration>();
            await AddColumnIfMissingAsync<TeamPlayer>(
                nameof(TeamPlayer.JerseyNumber), "INTEGER");
            await AddColumnIfMissingAsync<GameScoringSession>(
                nameof(GameScoringSession.CompletedAtUtc), "DATETIME");
            await AddColumnIfMissingAsync<GameScoringHistory>(
                nameof(GameScoringHistory.PendingStartPosition), "INTEGER");
            await AddColumnIfMissingAsync<GamePlateAppearanceRunnerMovement>(
                nameof(GamePlateAppearanceRunnerMovement.OutOrder), "INTEGER");
            await AddColumnIfMissingAsync<GameScoringPendingRunnerMovement>(
                nameof(GameScoringPendingRunnerMovement.OutOrder), "INTEGER");
            await connection.RunInTransactionAsync(transaction =>
            {
                if (legacyScoringRules)
                    transaction.Execute(
                        "UPDATE GameScoringSession SET HomeRunLimit = ?",
                        GameScoringSession.DefaultHomeRunLimit);
                UpgradeStory60ScoringSessions(transaction);
                Repositories.BeerRuleConfigurationRepository
                    .EnsureExistingGameSnapshots(transaction);
                CleanupExpiredScoringHistory(transaction, DateTime.UtcNow);
            });
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task AddColumnIfMissingAsync<T>(string columnName, string sqlType)
    {
        var columns = await connection.GetTableInfoAsync(typeof(T).Name);
        if (columns.Any(column => column.Name == columnName)) return;
        await connection.ExecuteAsync(
            $"ALTER TABLE {typeof(T).Name} ADD COLUMN {columnName} {sqlType}");
    }

    private async Task<bool> RequiresScoringRulesCompatibilityAsync()
    {
        var table = await connection.QueryAsync<SqliteTable>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?",
            nameof(GameScoringSession));
        if (table.Count == 0) return false;
        var columns = await connection.GetTableInfoAsync(nameof(GameScoringSession));
        return columns.All(column => column.Name != nameof(GameScoringSession.HomeRunLimit));
    }

    private sealed class SqliteTable
    {
        public string Name { get; set; } = string.Empty;
    }

    internal static void CleanupExpiredScoringHistory(
        SQLiteConnection transaction, DateTime utcNow)
    {
        var cutoff = utcNow - ScoringHistoryRetention;
        var expiredSessionIds = transaction.Table<GameScoringSession>()
            .Where(item => item.Status == GameScoringStatus.Completed
                && item.CompletedAtUtc != null
                && item.CompletedAtUtc <= cutoff)
            .ToList()
            .Select(item => item.Id)
            .ToArray();
        foreach (var sessionId in expiredSessionIds)
        {
            transaction.Execute(
                "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
                sessionId);
            transaction.Execute(
                "DELETE FROM GameScoringHistory WHERE GameScoringSessionId = ?",
                sessionId);
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
            var inningStateChanged = false;
            if (session.CurrentInning == 0)
            {
                session.CurrentInning = 1;
                inningStateChanged = true;
            }
            else if (session.CurrentInning < 1)
            {
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} has invalid inning or out state.");
            }
            if (session.CurrentOuts is < 0 or > 2)
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} has invalid inning or out state.");

            var sessionAppearances = transaction.Table<GamePlateAppearance>()
                .Where(item => item.GameScoringSessionId == session.Id).ToList();
            foreach (var appearance in sessionAppearances.Where(item => item.Inning == 0))
            {
                appearance.Inning = 1;
                if (transaction.Update(appearance) != 1)
                    throw new InvalidOperationException(
                        "Plate-appearance compatibility update did not affect one row.");
            }
            var appearanceIds = sessionAppearances.Select(item => item.Id).ToHashSet();
            var legacyScoringMovements = transaction
                .Table<GamePlateAppearanceRunnerMovement>().ToList()
                .Where(item => appearanceIds.Contains(item.GamePlateAppearanceId)
                    && item.Destination == RunnerDestination.Scores
                    && item.RunCounts is null);
            foreach (var movement in legacyScoringMovements)
            {
                movement.RunCounts = true;
                if (transaction.Update(movement) != 1)
                    throw new InvalidOperationException(
                        "Runner-movement compatibility update did not affect one row.");
            }

            var existingSequence = transaction.Table<GameScoringLineupPlayer>()
                .Where(item => item.GameScoringSessionId == session.Id)
                .ToList();
            if (existingSequence.Count > 0)
            {
                if (inningStateChanged && transaction.Update(session) != 1)
                    throw new InvalidOperationException(
                        "Scoring-session compatibility update did not affect one row.");
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
