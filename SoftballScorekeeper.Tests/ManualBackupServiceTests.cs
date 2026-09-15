using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SQLite;

namespace SoftballScorekeeper.Tests;

public sealed class ManualBackupServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(
        2026,
        9,
        15,
        20,
        42,
        0,
        TimeSpan.Zero
    );

    [Fact]
    public async Task Success_saves_valid_package_and_cleans_private_artifact_without_source_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await SeedRepresentativeStateAsync(db);
        var before = await ReadStateAsync(db.DatabasePath);
        var destination = new RecordingDestination(db.DirectoryPath);
        var packageService = CreatePackageService(db);
        var service = new ManualBackupService(packageService, destination);

        var result = await service.RunAsync();

        Assert.Equal(ManualBackupResult.Saved, result);
        Assert.Equal("SoftballScorekeeper-Backup-2026-09-15-2042.zip", destination.FileName);
        Assert.NotNull(destination.SavedPath);
        Assert.True(File.Exists(destination.SavedPath));
        Assert.True(destination.SourceExistedDuringSave);
        Assert.False(Directory.Exists(destination.SourceWorkspace));
        Assert.True((await packageService.InspectAsync(destination.SavedPath!)).IsValid);
        Assert.Equal(before, await ReadStateAsync(db.DatabasePath));
    }

    [Fact]
    public async Task Cancellation_is_normal_and_cleans_private_artifact()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var destination = new RecordingDestination(
            db.DirectoryPath,
            BackupDestinationResult.Cancelled
        );

        var result = await new ManualBackupService(CreatePackageService(db), destination)
            .RunAsync();

        Assert.Equal(ManualBackupResult.Cancelled, result);
        Assert.Null(destination.SavedPath);
        Assert.True(destination.SourceExistedDuringSave);
        Assert.False(Directory.Exists(destination.SourceWorkspace));
    }

    [Fact]
    public async Task Destination_failure_is_surfaced_and_cleans_private_artifact_without_source_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await db.CreateTeamRepository().CreateAsync(new Team { Name = "Unchanged", Year = 2026 });
        var before = await ReadStateAsync(db.DatabasePath);
        var destination = new RecordingDestination(db.DirectoryPath) { Failure = new IOException("write failed") };
        var service = new ManualBackupService(CreatePackageService(db), destination);

        var failure = await Assert.ThrowsAsync<IOException>(() => service.RunAsync());

        Assert.Equal("write failed", failure.Message);
        Assert.False(Directory.Exists(destination.SourceWorkspace));
        Assert.Equal(before, await ReadStateAsync(db.DatabasePath));
    }

    [Fact]
    public async Task Creation_failure_is_surfaced_without_opening_destination()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var destination = new RecordingDestination(db.DirectoryPath);
        await db.Database.SuspendAccessAsync();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ManualBackupService(CreatePackageService(db), destination).RunAsync()
            );
            Assert.Equal(0, destination.CallCount);
            Assert.Empty(Directory.EnumerateDirectories(db.DirectoryPath));
        }
        finally
        {
            await db.Database.ReinitializeAccessAsync();
        }
    }

    [Fact]
    public async Task Duplicate_invocation_is_rejected_while_destination_is_open()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var destination = new BlockingDestination();
        var service = new ManualBackupService(CreatePackageService(db), destination);

        var first = service.RunAsync();
        await destination.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var duplicate = await service.RunAsync();
        destination.Continue.TrySetResult();

        Assert.Equal(ManualBackupResult.AlreadyInProgress, duplicate);
        Assert.Equal(ManualBackupResult.Saved, await first);
        Assert.Equal(1, destination.CallCount);
    }

    [Fact]
    public async Task In_progress_game_is_present_in_saved_package_and_source_remains_in_progress()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await SeedRepresentativeStateAsync(db);
        var destination = new RecordingDestination(db.DirectoryPath);
        var packageService = CreatePackageService(db);

        Assert.Equal(
            ManualBackupResult.Saved,
            await new ManualBackupService(packageService, destination).RunAsync()
        );
        Assert.True((await packageService.InspectAsync(destination.SavedPath!)).IsValid);

        var extracted = Path.Combine(db.DirectoryPath, "manual-backup.sqlite3");
        using (var archive = System.IO.Compression.ZipFile.OpenRead(destination.SavedPath!))
        await using (var source = archive.GetEntry(BackupPackageService.DatabaseEntryName)!.Open())
        await using (var target = File.Create(extracted))
            await source.CopyToAsync(target);
        var savedState = await ReadStateAsync(extracted);
        Assert.Equal(2, savedState.GameCount);
        Assert.Equal(1, savedState.InProgressCount);
        Assert.Equal(1, savedState.CompletedCount);
        Assert.Equal(3, savedState.BeerLedgerCount);
        Assert.True(savedState.HasYearOverTeam);
        Assert.Equal(savedState, await ReadStateAsync(db.DatabasePath));
    }

    private static BackupPackageService CreatePackageService(TemporaryTestDatabase db) =>
        new(
            db.Database,
            new TestBackupPlatform(db.DirectoryPath, "8.1-test"),
            new FixedTimeProvider(CreatedAt)
        );

    private static async Task SeedRepresentativeStateAsync(TemporaryTestDatabase db)
    {
        await db.UseConnectionAsync(async connection =>
        {
            var lineage = Guid.NewGuid();
            await connection.InsertAsync(new Team { Id = 1, Name = "Prior", Year = 2025, TeamLineageId = lineage, IsYearOver = true });
            await connection.InsertAsync(new Team { Id = 2, Name = "Current", Year = 2026, TeamLineageId = lineage, PreviousTeamId = 1 });
            await connection.InsertAsync(new Player { Id = 1, FirstName = "Alex", LastName = "Backup" });
            await connection.InsertAsync(new TeamPlayer { Id = 1, TeamId = 2, PlayerId = 1, JerseyNumber = 8, IsActive = true });
            await connection.InsertAsync(new Season { Id = 1, TeamId = 2, Name = "Summer", Year = 2026 });
            await connection.InsertAsync(new Game { Id = 1, TeamId = 2, SeasonId = 1, ScheduledDate = new DateTime(2026, 6, 1) });
            await connection.InsertAsync(new Game { Id = 2, TeamId = 2, SeasonId = 1, ScheduledDate = new DateTime(2026, 6, 8) });
            await connection.InsertAsync(new GameScoringSession { Id = 1, GameId = 1, Status = GameScoringStatus.InProgress, TeamSide = TeamGameSide.Home, HomeScore = 4, VisitorScore = 3, CurrentInning = 4 });
            await connection.InsertAsync(new GameScoringSession { Id = 2, GameId = 2, Status = GameScoringStatus.Completed, TeamSide = TeamGameSide.Away, HomeScore = 2, VisitorScore = 5, CompletedAtUtc = new DateTime(2026, 6, 8, 22, 0, 0, DateTimeKind.Utc) });
            await connection.InsertAsync(new BeerLedgerEntry { Id = 1, TeamId = 2, PlayerId = 1, Origin = BeerLedgerEntryOrigin.ManualAdjustment, RuleType = BeerRuleType.Strikeout, RuleCountAdjustment = 1, Amount = 1 });
            await connection.InsertAsync(new BeerLedgerEntry { Id = 2, TeamId = 2, PlayerId = 1, Origin = BeerLedgerEntryOrigin.BroughtCredit, Amount = -1 });
            await connection.InsertAsync(new BeerLedgerEntry { Id = 3, TeamId = 2, PlayerId = 1, SourceTeamId = 1, Origin = BeerLedgerEntryOrigin.CarryForward, Amount = 2 });
            return 0;
        });
    }

    private static async Task<StoredState> ReadStateAsync(string databasePath)
    {
        var connection = new SQLiteAsyncConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.SharedCache);
        try
        {
            return new StoredState(
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Team"),
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Team WHERE IsYearOver = 1") > 0,
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Game"),
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GameScoringSession WHERE Status = 1"),
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GameScoringSession WHERE Status = 2"),
                await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BeerLedgerEntry")
            );
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed record StoredState(int TeamCount, bool HasYearOverTeam, int GameCount, int InProgressCount, int CompletedCount, int BeerLedgerCount);
    private sealed record TestBackupPlatform(string CacheDirectory, string AppVersion) : IBackupPlatform;
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingDestination(
        string directory,
        BackupDestinationResult result = BackupDestinationResult.Saved
    ) : IBackupDestination
    {
        public int CallCount { get; private set; }
        public string? FileName { get; private set; }
        public string? SavedPath { get; private set; }
        public string? SourceWorkspace { get; private set; }
        public bool SourceExistedDuringSave { get; private set; }
        public Exception? Failure { get; init; }

        public async Task<BackupDestinationResult> SaveAsync(string sourceFilePath, string suggestedFileName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            FileName = suggestedFileName;
            SourceWorkspace = Path.GetDirectoryName(sourceFilePath);
            SourceExistedDuringSave = File.Exists(sourceFilePath);
            if (Failure is not null)
                throw Failure;
            if (result == BackupDestinationResult.Cancelled)
                return result;
            SavedPath = Path.Combine(directory, $"saved-{suggestedFileName}");
            await using var source = File.OpenRead(sourceFilePath);
            await using var destination = File.Create(SavedPath);
            await source.CopyToAsync(destination, cancellationToken);
            return result;
        }
    }

    private sealed class BlockingDestination : IBackupDestination
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public async Task<BackupDestinationResult> SaveAsync(string sourceFilePath, string suggestedFileName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Started.TrySetResult();
            await Continue.Task.WaitAsync(cancellationToken);
            return BackupDestinationResult.Saved;
        }
    }
}
