using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class AppDatabase
{
    public const int MinimumSupportedSchemaVersion = 0;
    public const int CurrentSchemaVersion = 2;

    internal static readonly TimeSpan ScoringHistoryRetention = TimeSpan.FromDays(21);
    private SQLiteAsyncConnection connection;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly DatabaseLifecycleCoordinator lifecycle = new();
    private readonly AppDatabasePaths? modePaths;
    private string databasePath;
    private DatabaseMode activeMode;
    private volatile bool isInitialized;
    private volatile bool isAccessSuspended;

    public AppDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "Database path cannot be null, empty, or whitespace.",
                nameof(databasePath)
            );
        }

        this.databasePath = Path.GetFullPath(databasePath);
        activeMode = DatabaseMode.Production;
        connection = CreateConnection();
    }

    public AppDatabase(AppDatabasePaths paths, DatabaseMode initialMode)
    {
        ArgumentNullException.ThrowIfNull(paths);
        modePaths = paths;
        activeMode = initialMode;
        databasePath = paths.GetPath(initialMode);
        connection = CreateConnection();
    }

    public string DatabasePath => databasePath;

    public DatabaseMode ActiveMode => activeMode;

    public async Task<DatabaseOperationLease> BeginOperationAsync(
        CancellationToken cancellationToken = default
    )
    {
        await lifecycle.EnterOperationAsync(cancellationToken);
        try
        {
            if (isAccessSuspended)
                throw new InvalidOperationException("Database access is suspended.");
            await InitializeCoreAsync();
            if (isAccessSuspended)
                throw new InvalidOperationException("Database access is suspended.");
            return new DatabaseOperationLease(lifecycle, connection);
        }
        catch
        {
            lifecycle.ExitOperation();
            throw;
        }
    }

    public async Task<DatabaseExclusiveLifecycleLease> BeginExclusiveLifecycleAsync(
        CancellationToken cancellationToken = default
    )
    {
        await lifecycle.EnterExclusiveAsync(cancellationToken);
        return new DatabaseExclusiveLifecycleLease(lifecycle);
    }

    internal int ActiveOperationCount => lifecycle.ActiveOperationCount;

    internal bool IsExclusiveLifecyclePending => lifecycle.IsExclusivePending;

    public async Task InitializeAsync()
    {
        await using var operation = await BeginOperationAsync();
    }

    private async Task InitializeCoreAsync()
    {
        if (isAccessSuspended)
            throw new InvalidOperationException("Database access is suspended.");
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync();
        try
        {
            if (isAccessSuspended)
                throw new InvalidOperationException("Database access is suspended.");
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
            await AddColumnIfMissingAsync<TeamPlayer>(nameof(TeamPlayer.JerseyNumber), "INTEGER");
            await AddColumnIfMissingAsync<GameScoringSession>(
                nameof(GameScoringSession.CompletedAtUtc),
                "DATETIME"
            );
            await AddColumnIfMissingAsync<GameScoringHistory>(
                nameof(GameScoringHistory.PendingStartPosition),
                "INTEGER"
            );
            await AddColumnIfMissingAsync<GamePlateAppearanceRunnerMovement>(
                nameof(GamePlateAppearanceRunnerMovement.OutOrder),
                "INTEGER"
            );
            await AddColumnIfMissingAsync<GameScoringPendingRunnerMovement>(
                nameof(GameScoringPendingRunnerMovement.OutOrder),
                "INTEGER"
            );
            await AddColumnIfMissingAsync<BeerLedgerEntry>(
                nameof(BeerLedgerEntry.RuleCountAdjustment),
                "INTEGER"
            );
            await AddColumnIfMissingAsync<BeerLedgerEntry>(
                nameof(BeerLedgerEntry.PlayingDate),
                "DATETIME"
            );
            await AddColumnIfMissingAsync<Team>(
                nameof(Team.IsYearOver),
                "INTEGER NOT NULL DEFAULT 0"
            );
            await connection.ExecuteAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS UX_BeerLedgerEntry_LowMan_Source
                ON BeerLedgerEntry (TeamId, PlayingDate, PlayerId, RuleType, Origin)
                WHERE PlayingDate IS NOT NULL AND RuleType = 6 AND Origin = 1
                """
            );
            await connection.RunInTransactionAsync(transaction =>
            {
                UpgradeStory77TeamYearsAndLineage(transaction);
                UpgradeStory79TeamYearCompletion(transaction);
                if (legacyScoringRules)
                    transaction.Execute(
                        "UPDATE GameScoringSession SET HomeRunLimit = ?",
                        GameScoringSession.DefaultHomeRunLimit
                    );
                UpgradeStory60ScoringSessions(transaction);
                Repositories.BeerRuleConfigurationRepository.EnsureExistingGameSnapshots(
                    transaction
                );
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
        if (columns.Any(column => column.Name == columnName))
            return;
        await connection.ExecuteAsync(
            $"ALTER TABLE {typeof(T).Name} ADD COLUMN {columnName} {sqlType}"
        );
    }

    private async Task<bool> RequiresScoringRulesCompatibilityAsync()
    {
        var table = await connection.QueryAsync<SqliteTable>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?",
            nameof(GameScoringSession)
        );
        if (table.Count == 0)
            return false;
        var columns = await connection.GetTableInfoAsync(nameof(GameScoringSession));
        return columns.All(column => column.Name != nameof(GameScoringSession.HomeRunLimit));
    }

    private sealed class SqliteTable
    {
        public string Name { get; set; } = string.Empty;
    }

    private static void UpgradeStory79TeamYearCompletion(SQLiteConnection transaction)
    {
        if (transaction.ExecuteScalar<int>("PRAGMA user_version") >= CurrentSchemaVersion)
            return;

        transaction.Execute($"PRAGMA user_version = {CurrentSchemaVersion}");
    }

    private static void UpgradeStory77TeamYearsAndLineage(SQLiteConnection transaction)
    {
        if (transaction.ExecuteScalar<int>("PRAGMA user_version") >= 1)
            return;

        var teams = transaction.Table<Team>().OrderBy(item => item.Id).ToList();
        foreach (var team in teams)
        {
            var years = transaction
                .Table<Season>()
                .Where(item => item.TeamId == team.Id)
                .ToList()
                .Select(item => item.Year)
                .Where(item => item is >= 1 and <= 9999)
                .ToArray();
            team.Year = years.Length == 0 ? Team.LegacyDefaultYear : years.Max();
            team.TeamLineageId = Guid.NewGuid();
            team.PreviousTeamId = null;
            if (transaction.Update(team) != 1)
                throw new InvalidOperationException("Team Year migration did not affect one row.");
        }

        var duplicate = teams
            .GroupBy(item => (Name: item.Name.Trim().ToUpperInvariant(), item.Year))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Team Year migration found duplicate Team name/year '{duplicate.Key.Name}' / {duplicate.Key.Year}."
            );

        transaction.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_Team_Lineage_Year ON Team (TeamLineageId, Year)"
        );
        transaction.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_Team_PreviousTeam ON Team (PreviousTeamId) WHERE PreviousTeamId IS NOT NULL"
        );
        transaction.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_Team_Name_Year ON Team (Name COLLATE NOCASE, Year)"
        );
        transaction.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_BeerLedgerEntry_CarryForward ON BeerLedgerEntry (TeamId, PlayerId, Origin) WHERE Origin = 4"
        );
        transaction.Execute("PRAGMA user_version = 1");
    }

    internal static void CleanupExpiredScoringHistory(SQLiteConnection transaction, DateTime utcNow)
    {
        var cutoff = utcNow - ScoringHistoryRetention;
        var expiredSessionIds = transaction
            .Table<GameScoringSession>()
            .Where(item =>
                item.Status == GameScoringStatus.Completed
                && item.CompletedAtUtc != null
                && item.CompletedAtUtc <= cutoff
            )
            .ToList()
            .Select(item => item.Id)
            .ToArray();
        foreach (var sessionId in expiredSessionIds)
        {
            transaction.Execute(
                "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
                sessionId
            );
            transaction.Execute(
                "DELETE FROM GameScoringHistory WHERE GameScoringSessionId = ?",
                sessionId
            );
        }
    }

    public async Task<int> GetSchemaVersionAsync()
    {
        await using var operation = await BeginOperationAsync();
        return await operation.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
    }

    public async Task CreateSnapshotAsync(string destinationDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDatabasePath);
        await using var operation = await BeginOperationAsync();
        await operation.Connection.BackupAsync(destinationDatabasePath);
    }

    public async Task SuspendAccessAsync()
    {
        if (!lifecycle.IsExclusiveActive)
            throw new InvalidOperationException(
                "Database access can be suspended only during an exclusive lifecycle lease."
            );

        await initializationLock.WaitAsync();
        try
        {
            if (isAccessSuspended)
                return;
            isAccessSuspended = true;
            isInitialized = false;
            await connection.CloseAsync();
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task ReinitializeAccessAsync()
    {
        if (!lifecycle.IsExclusiveActive)
            throw new InvalidOperationException(
                "Database access can be reinitialized only during an exclusive lifecycle lease."
            );

        await initializationLock.WaitAsync();
        try
        {
            if (!isAccessSuspended)
                return;
            connection = CreateConnection();
            isAccessSuspended = false;
        }
        finally
        {
            initializationLock.Release();
        }

        try
        {
            await InitializeCoreAsync();
        }
        catch
        {
            await SuspendAccessAsync();
            throw;
        }
    }

    public async Task SwitchModeAsync(DatabaseMode targetMode)
    {
        EnsureModeSwitchConfigured();
        EnsureExclusiveLifecycle();
        if (activeMode == targetMode && !isAccessSuspended)
            return;

        var previousMode = activeMode;
        var previousPath = databasePath;
        await SuspendAccessAsync();
        databasePath = modePaths!.GetPath(targetMode);

        try
        {
            await ReinitializeAccessAsync();
            activeMode = targetMode;
        }
        catch (Exception switchFailure)
        {
            databasePath = previousPath;
            try
            {
                await ReinitializeAccessAsync();
                activeMode = previousMode;
            }
            catch (Exception rollbackFailure)
            {
                throw new DatabaseModeRecoveryException(switchFailure, rollbackFailure);
            }

            throw;
        }
    }

    public async Task ResetTestDatabaseAsync()
    {
        EnsureModeSwitchConfigured();
        EnsureExclusiveLifecycle();
        if (activeMode != DatabaseMode.Test)
            throw new InvalidOperationException(
                "The product Test database can be reset only while Test Mode is active."
            );

        modePaths!.ValidateTestResetTarget();
        await SwitchModeAsync(DatabaseMode.Production);

        try
        {
            DeleteTestDatabaseArtifacts();
        }
        catch (Exception deleteFailure)
        {
            try
            {
                await SwitchModeAsync(DatabaseMode.Test);
            }
            catch (Exception rollbackFailure)
            {
                await SuspendAccessAsync();
                throw new DatabaseModeRecoveryException(deleteFailure, rollbackFailure);
            }

            throw;
        }
    }

    public async Task CloseAsync()
    {
        await using var exclusive = await BeginExclusiveLifecycleAsync();
        await initializationLock.WaitAsync();
        try
        {
            if (isAccessSuspended)
                return;
            await connection.CloseAsync();
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private SQLiteAsyncConnection CreateConnection()
    {
        return new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache
        );
    }

    private void EnsureModeSwitchConfigured()
    {
        if (modePaths is null)
            throw new InvalidOperationException(
                "This AppDatabase instance was not configured for database mode switching."
            );
    }

    private void EnsureExclusiveLifecycle()
    {
        if (!lifecycle.IsExclusiveActive)
            throw new InvalidOperationException(
                "The database mode can be changed only during an exclusive lifecycle lease."
            );
    }

    private void DeleteTestDatabaseArtifacts()
    {
        modePaths!.ValidateTestResetTarget();
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
        {
            var artifactPath = modePaths.TestDatabasePath + suffix;
            if (File.Exists(artifactPath))
                File.Delete(artifactPath);
        }
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
                    $"Scoring session {session.Id} has invalid inning or out state."
                );
            }
            if (session.CurrentOuts is < 0 or > 2)
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} has invalid inning or out state."
                );

            var sessionAppearances = transaction
                .Table<GamePlateAppearance>()
                .Where(item => item.GameScoringSessionId == session.Id)
                .ToList();
            foreach (var appearance in sessionAppearances.Where(item => item.Inning == 0))
            {
                appearance.Inning = 1;
                if (transaction.Update(appearance) != 1)
                    throw new InvalidOperationException(
                        "Plate-appearance compatibility update did not affect one row."
                    );
            }
            var appearanceIds = sessionAppearances.Select(item => item.Id).ToHashSet();
            var legacyScoringMovements = transaction
                .Table<GamePlateAppearanceRunnerMovement>()
                .ToList()
                .Where(item =>
                    appearanceIds.Contains(item.GamePlateAppearanceId)
                    && item.Destination == RunnerDestination.Scores
                    && item.RunCounts is null
                );
            foreach (var movement in legacyScoringMovements)
            {
                movement.RunCounts = true;
                if (transaction.Update(movement) != 1)
                    throw new InvalidOperationException(
                        "Runner-movement compatibility update did not affect one row."
                    );
            }

            var existingSequence = transaction
                .Table<GameScoringLineupPlayer>()
                .Where(item => item.GameScoringSessionId == session.Id)
                .ToList();
            if (existingSequence.Count > 0)
            {
                if (inningStateChanged && transaction.Update(session) != 1)
                    throw new InvalidOperationException(
                        "Scoring-session compatibility update did not affect one row."
                    );
                continue;
            }

            if (session.CurrentBatterPosition != 0)
            {
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} has incomplete live scoring state."
                );
            }

            var lineup = transaction
                .Table<GameLineupPlayer>()
                .Where(item => item.GameId == session.GameId)
                .OrderBy(item => item.BattingOrder)
                .ThenBy(item => item.Id)
                .ToList();
            if (!IsStructurallyValid(lineup))
            {
                throw new InvalidOperationException(
                    $"Scoring session {session.Id} cannot be upgraded from an invalid frozen lineup."
                );
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
                        "Live batting-sequence insertion did not affect one row."
                    );
                }
            }

            session.HomeScore = 0;
            session.VisitorScore = 0;
            session.CurrentBatterPosition = 1;
            if (transaction.Update(session) != 1)
            {
                throw new InvalidOperationException(
                    "Scoring-session compatibility update did not affect one row."
                );
            }
        }
    }

    private static bool IsStructurallyValid(IReadOnlyList<GameLineupPlayer> lineup) =>
        lineup.Count > 0
        && lineup.Select(item => item.PlayerId).Distinct().Count() == lineup.Count
        && lineup
            .Select(item => item.BattingOrder)
            .SequenceEqual(Enumerable.Range(1, lineup.Count));
}

public sealed class DatabaseModeRecoveryException : Exception
{
    internal DatabaseModeRecoveryException(Exception switchFailure, Exception rollbackFailure)
        : base(
            "The database mode change failed and the previous database could not be reopened.",
            new AggregateException(switchFailure, rollbackFailure)
        ) { }
}
