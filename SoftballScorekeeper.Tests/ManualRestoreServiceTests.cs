using System.IO.Compression;
using System.Text.Json;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SQLite;

namespace SoftballScorekeeper.Tests;

public sealed class ManualRestoreServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 16, 14, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirmed_restore_replaces_all_data_and_keeps_existing_repositories_usable()
    {
        await using var sourceDb = await TemporaryTestDatabase.CreateAsync();
        await sourceDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Restored Team", Year = 2025 });
        await using var backup = await CreatePackageService(sourceDb).CreateAsync();

        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        var currentRepository = currentDb.CreateTeamRepository();
        await currentRepository.CreateAsync(new Team { Name = "Replaced Team", Year = 2026 });
        var restoreSource = new FileRestoreSource(backup.FilePath, currentDb.DirectoryPath);
        var service = CreateRestoreService(currentDb, restoreSource);

        var result = await service.RunAsync(
            (metadata, compatibility) =>
            {
                Assert.Equal(CreatedAt, metadata.CreatedAt);
                Assert.Equal(BackupDatabaseCompatibility.Current, compatibility);
                return Task.FromResult(true);
            }
        );

        Assert.Equal(ManualRestoreStatus.Restored, result.Status);
        var restoredTeam = Assert.Single(await currentRepository.GetActiveAsync());
        Assert.Equal("Restored Team", restoredTeam.Name);
        Assert.Equal(2025, restoredTeam.Year);
        Assert.False(Directory.Exists(restoreSource.WorkspacePath));
        Assert.Empty(RestoreWorkFiles(currentDb));
    }

    [Fact]
    public async Task Cancelled_confirmation_preserves_current_data_and_cleans_private_files()
    {
        await using var sourceDb = await TemporaryTestDatabase.CreateAsync();
        await sourceDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Unused Restore", Year = 2025 });
        await using var backup = await CreatePackageService(sourceDb).CreateAsync();
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        await currentDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Current Team", Year = 2026 });
        var restoreSource = new FileRestoreSource(backup.FilePath, currentDb.DirectoryPath);

        var result = await CreateRestoreService(currentDb, restoreSource)
            .RunAsync((_, _) => Task.FromResult(false));

        Assert.Equal(ManualRestoreStatus.Cancelled, result.Status);
        Assert.Equal(
            "Current Team",
            Assert.Single(await currentDb.CreateTeamRepository().GetActiveAsync()).Name
        );
        Assert.False(Directory.Exists(restoreSource.WorkspacePath));
        Assert.Empty(Directory.EnumerateDirectories(currentDb.DirectoryPath));
    }

    [Fact]
    public async Task Supported_older_backup_is_identified_and_migrated_after_replacement()
    {
        await using var sourceDb = await TemporaryTestDatabase.CreateAsync();
        await sourceDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Older Backup Team", Year = 2025 });
        await sourceDb.ExecuteAsync("PRAGMA user_version = 1");
        await using var backup = await CreatePackageService(sourceDb).CreateAsync();
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        var source = new FileRestoreSource(backup.FilePath, currentDb.DirectoryPath);
        BackupDatabaseCompatibility? confirmedCompatibility = null;

        var result = await CreateRestoreService(currentDb, source)
            .RunAsync(
                (_, compatibility) =>
                {
                    confirmedCompatibility = compatibility;
                    return Task.FromResult(true);
                }
            );

        Assert.Equal(ManualRestoreStatus.Restored, result.Status);
        Assert.Equal(BackupDatabaseCompatibility.OlderSupported, confirmedCompatibility);
        Assert.Equal(
            AppDatabase.CurrentSchemaVersion,
            await currentDb.Database.GetSchemaVersionAsync()
        );
        Assert.Equal(
            "Older Backup Team",
            Assert.Single(await currentDb.CreateTeamRepository().GetActiveAsync()).Name
        );
    }

    [Fact]
    public async Task Invalid_package_is_rejected_before_confirmation_or_database_suspension()
    {
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        await currentDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Current Team", Year = 2026 });
        var invalidPackage = Path.Combine(currentDb.DirectoryPath, "invalid.zip");
        await File.WriteAllTextAsync(invalidPackage, "not a backup");
        var restoreSource = new FileRestoreSource(invalidPackage, currentDb.DirectoryPath);
        var confirmationCalled = false;

        var result = await CreateRestoreService(currentDb, restoreSource)
            .RunAsync(
                (_, _) =>
                {
                    confirmationCalled = true;
                    return Task.FromResult(true);
                }
            );

        Assert.Equal(ManualRestoreStatus.Rejected, result.Status);
        Assert.Equal(BackupInspectionFailure.InvalidPackage, result.InspectionFailure);
        Assert.False(confirmationCalled);
        Assert.Equal(
            "Current Team",
            Assert.Single(await currentDb.CreateTeamRepository().GetActiveAsync()).Name
        );
        Assert.False(Directory.Exists(restoreSource.WorkspacePath));
    }

    [Fact]
    public async Task Replacement_initialization_failure_rolls_back_same_directory_safety_copy()
    {
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        await currentDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Preserved Team", Year = 2026 });
        var invalidReplacement = await CreateMigrationFailureDatabaseAsync(currentDb.DirectoryPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DatabaseRestoreService(currentDb.Database).RestoreAsync(invalidReplacement)
        );

        var preserved = Assert.Single(await currentDb.CreateTeamRepository().GetActiveAsync());
        Assert.Equal("Preserved Team", preserved.Name);
        Assert.Equal(
            AppDatabase.CurrentSchemaVersion,
            await currentDb.Database.GetSchemaVersionAsync()
        );
        Assert.Empty(RestoreWorkFiles(currentDb));
    }

    [Fact]
    public async Task Duplicate_invocation_is_rejected_while_confirmation_is_pending()
    {
        await using var sourceDb = await TemporaryTestDatabase.CreateAsync();
        await sourceDb
            .CreateTeamRepository()
            .CreateAsync(new Team { Name = "Restored Team", Year = 2026 });
        await using var backup = await CreatePackageService(sourceDb).CreateAsync();
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        var source = new FileRestoreSource(backup.FilePath, currentDb.DirectoryPath);
        var service = CreateRestoreService(currentDb, source);
        var confirmationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var continueConfirmation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var first = service.RunAsync(
            async (_, _) =>
            {
                confirmationStarted.TrySetResult();
                await continueConfirmation.Task;
                return false;
            }
        );
        await confirmationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var duplicate = await service.RunAsync((_, _) => Task.FromResult(true));
        continueConfirmation.TrySetResult();

        Assert.Equal(ManualRestoreStatus.AlreadyInProgress, duplicate.Status);
        Assert.Equal(ManualRestoreStatus.Cancelled, (await first).Status);
        Assert.Equal(1, source.CallCount);
    }

    [Fact]
    public async Task Test_mode_is_rejected_before_source_selection()
    {
        await using var currentDb = await TemporaryTestDatabase.CreateAsync();
        var source = new CountingRestoreSource();
        var service = new ManualRestoreService(
            CreatePackageService(currentDb),
            new DatabaseRestoreService(currentDb.Database),
            source,
            new DatabaseModeState(DatabaseMode.Test),
            new ApplicationWorkflowCoordinator()
        );

        var result = await service.RunAsync((_, _) => Task.FromResult(true));

        Assert.Equal(ManualRestoreStatus.UnavailableInTestMode, result.Status);
        Assert.Equal(0, source.CallCount);
    }

    private static ManualRestoreService CreateRestoreService(
        TemporaryTestDatabase db,
        IRestoreSource source
    ) => new(CreatePackageService(db), new DatabaseRestoreService(db.Database), source);

    private static BackupPackageService CreatePackageService(TemporaryTestDatabase db) =>
        new(
            db.Database,
            new TestBackupPlatform(db.DirectoryPath, "8.2-test"),
            new FixedTimeProvider(CreatedAt)
        );

    private static IEnumerable<string> RestoreWorkFiles(TemporaryTestDatabase db) =>
        Directory.EnumerateFiles(db.DirectoryPath, "*.restore-*");

    private static async Task<string> CreateMigrationFailureDatabaseAsync(string directoryPath)
    {
        var path = Path.Combine(directoryPath, "migration-failure.sqlite3");
        var connection = new SQLiteAsyncConnection(
            path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache
        );
        try
        {
            await connection.CreateTableAsync<Team>();
            await connection.CreateTableAsync<Player>();
            await connection.CreateTableAsync<TeamPlayer>();
            await connection.CreateTableAsync<Season>();
            await connection.CreateTableAsync<Game>();
            await connection.InsertAsync(
                new Team
                {
                    Id = 1,
                    Name = "Duplicate",
                    Year = 2024,
                }
            );
            await connection.InsertAsync(
                new Team
                {
                    Id = 2,
                    Name = "Duplicate",
                    Year = 2025,
                }
            );
            await connection.ExecuteAsync("PRAGMA user_version = 0");
        }
        finally
        {
            await connection.CloseAsync();
        }

        return path;
    }

    private sealed record TestBackupPlatform(string CacheDirectory, string AppVersion)
        : IBackupPlatform;

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FileRestoreSource(string packagePath, string parentDirectory)
        : IRestoreSource
    {
        public int CallCount { get; private set; }
        public string WorkspacePath { get; } =
            Path.Combine(parentDirectory, $"restore-source-{Guid.NewGuid():N}");

        public async Task<RestoreSourceArtifact?> SelectAsync(
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            Directory.CreateDirectory(WorkspacePath);
            var selectedPath = Path.Combine(WorkspacePath, "selected.zip");
            await using var source = File.OpenRead(packagePath);
            await using var destination = File.Create(selectedPath);
            await source.CopyToAsync(destination, cancellationToken);
            return new RestoreSourceArtifact(selectedPath, WorkspacePath);
        }
    }

    private sealed class CountingRestoreSource : IRestoreSource
    {
        public int CallCount { get; private set; }

        public Task<RestoreSourceArtifact?> SelectAsync(
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            return Task.FromResult<RestoreSourceArtifact?>(null);
        }
    }
}
