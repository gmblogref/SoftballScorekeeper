using System.IO.Compression;
using System.Text.Json;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SQLite;

namespace SoftballScorekeeper.Tests;

public sealed class BackupPackageServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_preserves_complete_authoritative_state_in_readable_snapshot()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await SeedRepresentativeStateAsync(db);
        var beforeCounts = await ReadTableCountsAsync(db.DatabasePath);
        var service = CreateService(db);

        var artifact = await service.CreateAsync();
        var workspace = Path.GetDirectoryName(artifact.FilePath)!;
        try
        {
            Assert.True(File.Exists(artifact.FilePath));
            Assert.Equal(
                [BackupPackageService.DatabaseEntryName, BackupPackageService.MetadataEntryName],
                ReadEntryNames(artifact.FilePath)
            );
            Assert.Equal(
                BackupPackageService.CurrentBackupFormatVersion,
                artifact.Metadata.BackupFormatVersion
            );
            Assert.Equal("8.0-test", artifact.Metadata.AppVersion);
            Assert.Equal(AppDatabase.CurrentSchemaVersion, artifact.Metadata.DatabaseVersion);
            Assert.Equal(CreatedAt, artifact.Metadata.CreatedAt);
            Assert.Single(Directory.EnumerateFiles(workspace));

            var snapshotPath = await ExtractDatabaseAsync(db.DirectoryPath, artifact.FilePath);
            var snapshotCounts = await ReadTableCountsAsync(snapshotPath);
            Assert.Equal(beforeCounts, snapshotCounts);
            await AssertRepresentativeStateAsync(snapshotPath);

            Assert.Equal(beforeCounts, await ReadTableCountsAsync(db.DatabasePath));
            Assert.Equal(
                AppDatabase.CurrentSchemaVersion,
                await db.Database.GetSchemaVersionAsync()
            );
        }
        finally
        {
            await artifact.DisposeAsync();
        }

        Assert.False(Directory.Exists(workspace));
    }

    [Fact]
    public async Task Inspect_accepts_current_package_and_cleans_temporary_files()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var service = CreateService(db);
        await using var artifact = await service.CreateAsync();
        var existingDirectories = Directory.EnumerateDirectories(db.DirectoryPath).ToHashSet();

        var result = await service.InspectAsync(artifact.FilePath);

        Assert.True(result.IsValid);
        Assert.Equal(BackupInspectionFailure.None, result.Failure);
        Assert.Equal(BackupDatabaseCompatibility.Current, result.DatabaseCompatibility);
        Assert.Equal(artifact.Metadata, result.Metadata);
        Assert.Equal(
            existingDirectories,
            Directory.EnumerateDirectories(db.DirectoryPath).ToHashSet()
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Inspect_accepts_supported_older_database_for_later_migration(int version)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var packagePath = await CreatePackageWithVersionsAsync(
            db,
            BackupPackageService.CurrentBackupFormatVersion,
            version,
            version
        );

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.True(result.IsValid);
        Assert.Equal(BackupDatabaseCompatibility.OlderSupported, result.DatabaseCompatibility);
        Assert.Equal(version, result.Metadata!.DatabaseVersion);
        Assert.Equal(AppDatabase.CurrentSchemaVersion, await db.Database.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task Inspect_rejects_newer_database_without_changing_source()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var newerVersion = AppDatabase.CurrentSchemaVersion + 1;
        var packagePath = await CreatePackageWithVersionsAsync(
            db,
            1,
            newerVersion,
            newerVersion
        );

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(BackupInspectionFailure.UnsupportedDatabaseVersion, result.Failure);
        Assert.Equal(AppDatabase.CurrentSchemaVersion, await db.Database.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task Inspect_rejects_unsupported_backup_format()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var packagePath = await CreatePackageWithVersionsAsync(
            db,
            backupFormatVersion: 2,
            metadataDatabaseVersion: AppDatabase.CurrentSchemaVersion,
            embeddedDatabaseVersion: AppDatabase.CurrentSchemaVersion
        );

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(BackupInspectionFailure.UnsupportedBackupFormat, result.Failure);
    }

    [Fact]
    public async Task Inspect_rejects_metadata_database_version_mismatch()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var packagePath = await CreatePackageWithVersionsAsync(db, 1, 1, 2);

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(BackupInspectionFailure.DatabaseVersionMismatch, result.Failure);
    }

    [Theory]
    [InlineData(true, false, BackupInspectionFailure.MissingRequiredContent)]
    [InlineData(false, true, BackupInspectionFailure.MissingRequiredContent)]
    [InlineData(false, false, BackupInspectionFailure.MissingRequiredContent)]
    public async Task Inspect_rejects_packages_missing_required_content(
        bool includeMetadata,
        bool includeDatabase,
        BackupInspectionFailure expectedFailure
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var databaseCopy = Path.Combine(db.DirectoryPath, "missing-content.sqlite3");
        await db.Database.CreateSnapshotAsync(databaseCopy);
        var packagePath = Path.Combine(db.DirectoryPath, $"missing-{Guid.NewGuid():N}.zip");
        CreatePackage(
            packagePath,
            databaseCopy,
            CurrentMetadata(),
            includeMetadata,
            includeDatabase
        );

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(expectedFailure, result.Failure);
    }

    [Fact]
    public async Task Inspect_rejects_corrupt_archive_and_invalid_sqlite_without_source_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var corruptArchive = Path.Combine(db.DirectoryPath, "corrupt.zip");
        await File.WriteAllTextAsync(corruptArchive, "not a zip archive");
        var service = CreateService(db);

        var archiveResult = await service.InspectAsync(corruptArchive);

        Assert.False(archiveResult.IsValid);
        Assert.Equal(BackupInspectionFailure.InvalidPackage, archiveResult.Failure);

        var invalidDatabase = Path.Combine(db.DirectoryPath, "invalid.sqlite3");
        await File.WriteAllTextAsync(invalidDatabase, "not a sqlite database");
        var invalidPackage = Path.Combine(db.DirectoryPath, "invalid-database.zip");
        CreatePackage(invalidPackage, invalidDatabase, CurrentMetadata(), true, true);

        var databaseResult = await service.InspectAsync(invalidPackage);

        Assert.False(databaseResult.IsValid);
        Assert.Equal(BackupInspectionFailure.InvalidDatabase, databaseResult.Failure);
        Assert.Equal(AppDatabase.CurrentSchemaVersion, await db.Database.GetSchemaVersionAsync());
        Assert.Empty(Directory.EnumerateDirectories(db.DirectoryPath));
    }

    [Fact]
    public async Task Inspect_rejects_unexpected_package_content()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var artifact = await CreateService(db).CreateAsync();
        var databaseCopy = await ExtractDatabaseAsync(db.DirectoryPath, artifact.FilePath);
        var packagePath = Path.Combine(db.DirectoryPath, "extra-content.zip");
        CreatePackage(packagePath, databaseCopy, CurrentMetadata(), true, true);
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
            archive.CreateEntry("unexpected.txt");

        var result = await CreateService(db).InspectAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(BackupInspectionFailure.InvalidPackage, result.Failure);
    }

    [Fact]
    public async Task Create_cleans_workspace_when_snapshot_creation_fails()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var existingDirectories = Directory.EnumerateDirectories(db.DirectoryPath).ToHashSet();
        await using (var exclusive = await db.Database.BeginExclusiveLifecycleAsync())
            await db.Database.SuspendAccessAsync();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).CreateAsync()
            );
            Assert.Equal(
                existingDirectories,
                Directory.EnumerateDirectories(db.DirectoryPath).ToHashSet()
            );
        }
        finally
        {
            await using var exclusive = await db.Database.BeginExclusiveLifecycleAsync();
            await db.Database.ReinitializeAccessAsync();
        }
    }

    [Fact]
    public async Task Database_can_suspend_repository_access_and_reinitialize_same_file()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var repository = db.CreateTeamRepository();
        var team = new Team { Name = "Lifecycle Team", Year = 2026 };
        Assert.Equal(1, await repository.CreateAsync(team));

        var exclusive = await db.Database.BeginExclusiveLifecycleAsync();
        await db.Database.SuspendAccessAsync();
        await exclusive.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetActiveAsync());

        await using (var reinitialize = await db.Database.BeginExclusiveLifecycleAsync())
            await db.Database.ReinitializeAccessAsync();

        var reloaded = Assert.Single(await repository.GetActiveAsync());
        Assert.Equal(team.Id, reloaded.Id);
        Assert.Equal("Lifecycle Team", reloaded.Name);
    }

    private static BackupPackageService CreateService(TemporaryTestDatabase db) =>
        new(
            db.Database,
            new TestBackupPlatform(db.DirectoryPath, "8.0-test"),
            new FixedTimeProvider(CreatedAt)
        );

    private static BackupMetadata CurrentMetadata() =>
        new(1, "8.0-test", AppDatabase.CurrentSchemaVersion, CreatedAt);

    private static async Task<string> CreatePackageWithVersionsAsync(
        TemporaryTestDatabase db,
        int backupFormatVersion,
        int metadataDatabaseVersion,
        int embeddedDatabaseVersion
    )
    {
        var databaseCopy = Path.Combine(db.DirectoryPath, $"database-{Guid.NewGuid():N}.sqlite3");
        await db.Database.CreateSnapshotAsync(databaseCopy);
        var connection = new SQLiteAsyncConnection(databaseCopy);
        try
        {
            await connection.ExecuteAsync($"PRAGMA user_version = {embeddedDatabaseVersion}");
        }
        finally
        {
            await connection.CloseAsync();
        }

        var packagePath = Path.Combine(db.DirectoryPath, $"package-{Guid.NewGuid():N}.zip");
        CreatePackage(
            packagePath,
            databaseCopy,
            new BackupMetadata(backupFormatVersion, "8.0-test", metadataDatabaseVersion, CreatedAt),
            includeMetadata: true,
            includeDatabase: true
        );
        return packagePath;
    }

    private static void CreatePackage(
        string packagePath,
        string databasePath,
        BackupMetadata metadata,
        bool includeMetadata,
        bool includeDatabase
    )
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        if (includeMetadata)
        {
            var entry = archive.CreateEntry(BackupPackageService.MetadataEntryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(JsonSerializer.Serialize(metadata));
        }
        if (includeDatabase)
            archive.CreateEntryFromFile(databasePath, BackupPackageService.DatabaseEntryName);
    }

    private static string[] ReadEntryNames(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries.Select(entry => entry.FullName).Order().ToArray();
    }

    private static async Task<string> ExtractDatabaseAsync(string directory, string packagePath)
    {
        var destinationPath = Path.Combine(directory, $"extracted-{Guid.NewGuid():N}.sqlite3");
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(BackupPackageService.DatabaseEntryName)!;
        await using var source = entry.Open();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
        return destinationPath;
    }

    private static async Task<Dictionary<string, int>> ReadTableCountsAsync(string databasePath)
    {
        var tableNames = new[]
        {
            "Team",
            "Player",
            "TeamPlayer",
            "Season",
            "Game",
            "GameLineupPlayer",
            "GameScoringSession",
            "GameScoringLineupPlayer",
            "GamePlateAppearance",
            "GameScoringBaseRunner",
            "GamePlateAppearanceRunnerMovement",
            "GameScoringHalfInning",
            "GameScoringPendingPlay",
            "GameScoringPendingRunnerMovement",
            "GameScoringHistory",
            "GameScoringHistorySnapshot",
            "BeerLedgerEntry",
            "TeamBeerRuleConfiguration",
            "GameScoringBeerRuleConfiguration",
            "SeasonBackupReminderCheckpoint",
        };
        var connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.SharedCache
        );
        try
        {
            var result = new Dictionary<string, int>();
            foreach (var tableName in tableNames)
                result[tableName] = await connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {tableName}"
                );
            return result;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task AssertRepresentativeStateAsync(string databasePath)
    {
        var connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.SharedCache
        );
        try
        {
            Assert.Equal("ok", await connection.ExecuteScalarAsync<string>("PRAGMA quick_check"));
            Assert.Equal(
                AppDatabase.CurrentSchemaVersion,
                await connection.ExecuteScalarAsync<int>("PRAGMA user_version")
            );

            var predecessor = await connection.FindAsync<Team>(1);
            var current = await connection.FindAsync<Team>(2);
            Assert.True(predecessor!.IsYearOver);
            Assert.Equal(2025, predecessor.Year);
            Assert.Equal(predecessor.TeamLineageId, current!.TeamLineageId);
            Assert.Equal(predecessor.Id, current.PreviousTeamId);
            Assert.True(current.IsActive);

            var session = Assert.Single(await connection.Table<GameScoringSession>().ToListAsync());
            Assert.Equal(GameScoringStatus.InProgress, session.Status);
            Assert.Equal(3, session.HomeScore);
            Assert.Equal(2, session.CurrentInning);
            Assert.Equal(1, session.CurrentOuts);
            Assert.Single(await connection.Table<GameScoringBaseRunner>().ToListAsync());
            Assert.Single(await connection.Table<GameScoringPendingPlay>().ToListAsync());
            Assert.Single(await connection.Table<GameScoringPendingRunnerMovement>().ToListAsync());
            var history = Assert.Single(await connection.Table<GameScoringHistory>().ToListAsync());
            Assert.Equal(1, history.CurrentPosition);
            Assert.Single(await connection.Table<GameScoringHistorySnapshot>().ToListAsync());

            Assert.Single(await connection.Table<TeamBeerRuleConfiguration>().ToListAsync());
            Assert.Single(await connection.Table<GameScoringBeerRuleConfiguration>().ToListAsync());
            var ledger = await connection.Table<BeerLedgerEntry>().ToListAsync();
            Assert.Contains(ledger, item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring);
            Assert.Contains(ledger, item => item.Origin == BeerLedgerEntryOrigin.ManualAdjustment);
            Assert.Contains(ledger, item => item.Origin == BeerLedgerEntryOrigin.BroughtCredit);
            Assert.Contains(ledger, item => item.Origin == BeerLedgerEntryOrigin.CarryForward);
            Assert.Contains(
                ledger,
                item =>
                    item.RuleType == BeerRuleType.LowMan
                    && item.PlayingDate == new DateTime(2026, 7, 10)
            );
            var reminderCheckpoint = Assert.Single(
                await connection.Table<SeasonBackupReminderCheckpoint>().ToListAsync()
            );
            Assert.Equal("representative-checkpoint", reminderCheckpoint.CheckpointId);
            Assert.Equal(
                SeasonBackupReminderCheckpointDisposition.Dismissed,
                reminderCheckpoint.Disposition
            );
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task SeedRepresentativeStateAsync(TemporaryTestDatabase db)
    {
        var lineageId = Guid.NewGuid();
        await db.UseConnectionAsync(async connection =>
        {
            await connection.InsertAsync(
                new Team
                {
                    Id = 1,
                    Name = "Backup History",
                    Year = 2025,
                    TeamLineageId = lineageId,
                    IsYearOver = true,
                    IsActive = true,
                }
            );
            await connection.InsertAsync(
                new Team
                {
                    Id = 2,
                    Name = "Backup Current",
                    Year = 2026,
                    TeamLineageId = lineageId,
                    PreviousTeamId = 1,
                    IsActive = true,
                }
            );
            await connection.InsertAsync(
                new Player
                {
                    Id = 1,
                    FirstName = "Alex",
                    LastName = "Backup",
                    IsActive = true,
                }
            );
            await connection.InsertAsync(
                new TeamPlayer
                {
                    Id = 1,
                    TeamId = 2,
                    PlayerId = 1,
                    JerseyNumber = 12,
                    IsActive = true,
                }
            );
            await connection.InsertAsync(
                new Season
                {
                    Id = 1,
                    TeamId = 2,
                    Name = "Summer",
                    Year = 2026,
                }
            );
            await connection.InsertAsync(
                new Game
                {
                    Id = 1,
                    TeamId = 2,
                    SeasonId = 1,
                    ScheduledDate = new DateTime(2026, 7, 10),
                    ScheduledTime = new TimeSpan(18, 30, 0),
                    Opponent = "Snapshot Opponent",
                    Field = "Field 1",
                }
            );
            await connection.InsertAsync(
                new GameLineupPlayer
                {
                    Id = 1,
                    GameId = 1,
                    PlayerId = 1,
                    BattingOrder = 1,
                }
            );
            await connection.InsertAsync(
                new GameScoringSession
                {
                    Id = 1,
                    GameId = 1,
                    Status = GameScoringStatus.InProgress,
                    TeamSide = TeamGameSide.Home,
                    HomeScore = 3,
                    VisitorScore = 2,
                    CurrentBatterPosition = 1,
                    CurrentInning = 2,
                    CurrentOuts = 1,
                    HomeRunLimit = 5,
                    IsOneUpEnabled = true,
                }
            );
            await connection.InsertAsync(
                new GameScoringLineupPlayer
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    PlayerId = 1,
                    BattingPosition = 1,
                }
            );
            await connection.InsertAsync(
                new GamePlateAppearance
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    PlayerId = 1,
                    BattingPosition = 1,
                    PlateAppearanceNumber = 1,
                    Inning = 1,
                    Result = BatterResult.Single,
                }
            );
            await connection.InsertAsync(
                new GameScoringBaseRunner
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    PlayerId = 1,
                    Base = ScoringBase.Second,
                }
            );
            await connection.InsertAsync(
                new GamePlateAppearanceRunnerMovement
                {
                    Id = 1,
                    GamePlateAppearanceId = 1,
                    PlayerId = 1,
                    StartingLocation = RunnerStartingLocation.Home,
                    Destination = RunnerDestination.First,
                    RunCounts = false,
                }
            );
            await connection.InsertAsync(
                new GameScoringHalfInning
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    Inning = 1,
                    TeamRuns = 3,
                    TeamHomeRuns = 0,
                    TeamSummaryAcknowledged = true,
                    OpponentRuns = 2,
                    OpponentHomeRuns = 0,
                    OpponentEntryCompleted = true,
                }
            );
            await connection.InsertAsync(
                new GameScoringPendingPlay
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    BatterPlayerId = 1,
                    BatterPosition = 1,
                    Result = BatterResult.Double,
                }
            );
            await connection.InsertAsync(
                new GameScoringPendingRunnerMovement
                {
                    Id = 1,
                    GameScoringPendingPlayId = 1,
                    PlayerId = 1,
                    StartingLocation = RunnerStartingLocation.Second,
                    Destination = RunnerDestination.Scores,
                }
            );
            await connection.InsertAsync(
                new GameScoringHistory
                {
                    GameScoringSessionId = 1,
                    CurrentPosition = 1,
                    LatestPosition = 2,
                    PendingStartPosition = 1,
                }
            );
            await connection.InsertAsync(
                new GameScoringHistorySnapshot
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    Position = 1,
                    StateJson = "{\"HomeScore\":3,\"CurrentInning\":2}",
                }
            );
            await connection.InsertAsync(
                new TeamBeerRuleConfiguration
                {
                    Id = 1,
                    TeamId = 2,
                    RuleType = BeerRuleType.Strikeout,
                    IsEnabled = true,
                    Amount = 2,
                }
            );
            await connection.InsertAsync(
                new GameScoringBeerRuleConfiguration
                {
                    Id = 1,
                    GameScoringSessionId = 1,
                    RuleType = BeerRuleType.Strikeout,
                    IsEnabled = true,
                    Amount = 3,
                }
            );
            await connection.InsertAsync(
                new BeerLedgerEntry
                {
                    Id = 1,
                    TeamId = 2,
                    GameId = 1,
                    GameScoringSessionId = 1,
                    PlayerId = 1,
                    GamePlateAppearanceId = 1,
                    RuleType = BeerRuleType.Dbo,
                    Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                    Amount = 1,
                }
            );
            await connection.InsertAsync(
                new BeerLedgerEntry
                {
                    Id = 2,
                    TeamId = 2,
                    PlayerId = 1,
                    RuleType = BeerRuleType.Strikeout,
                    Origin = BeerLedgerEntryOrigin.ManualAdjustment,
                    RuleCountAdjustment = 1,
                    Amount = 2,
                }
            );
            await connection.InsertAsync(
                new BeerLedgerEntry
                {
                    Id = 3,
                    TeamId = 2,
                    PlayerId = 1,
                    Origin = BeerLedgerEntryOrigin.BroughtCredit,
                    Amount = -1,
                }
            );
            await connection.InsertAsync(
                new BeerLedgerEntry
                {
                    Id = 4,
                    TeamId = 2,
                    PlayerId = 1,
                    SourceTeamId = 1,
                    Origin = BeerLedgerEntryOrigin.CarryForward,
                    Amount = 4,
                }
            );
            await connection.InsertAsync(
                new BeerLedgerEntry
                {
                    Id = 5,
                    TeamId = 2,
                    PlayerId = 1,
                    RuleType = BeerRuleType.LowMan,
                    Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                    PlayingDate = new DateTime(2026, 7, 10),
                    Amount = 1,
                }
            );
            await connection.InsertAsync(
                new SeasonBackupReminderCheckpoint
                {
                    CheckpointId = "representative-checkpoint",
                    SeasonId = 1,
                    Disposition = SeasonBackupReminderCheckpointDisposition.Dismissed,
                }
            );
            return 0;
        });
    }

    private sealed record TestBackupPlatform(string CacheDirectory, string AppVersion)
        : IBackupPlatform;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
