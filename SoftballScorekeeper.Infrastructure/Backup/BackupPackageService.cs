using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoftballScorekeeper.Infrastructure.Data;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class BackupPackageService(
    AppDatabase database,
    IBackupPlatform platform,
    TimeProvider timeProvider
)
{
    public const int CurrentBackupFormatVersion = 1;
    public const string MetadataEntryName = "metadata.json";
    public const string DatabaseEntryName = "database.sqlite3";

    private static readonly string[] RecognizableTableNames =
    [
        "Team",
        "Player",
        "TeamPlayer",
        "Season",
        "Game",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<BackupArtifact> CreateAsync()
    {
        var cacheDirectory = RequireDirectory(platform.CacheDirectory);
        var appVersion = platform.AppVersion;
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);

        var createdAt = timeProvider.GetUtcNow();
        var workspacePath = CreateWorkspace(cacheDirectory, "create");
        var snapshotPath = Path.Combine(workspacePath, DatabaseEntryName);
        var metadataPath = Path.Combine(workspacePath, MetadataEntryName);
        var packagePath = Path.Combine(
            workspacePath,
            $"SoftballScorekeeper-backup-{createdAt:yyyyMMdd-HHmmss}Z.zip"
        );

        try
        {
            await database.CreateSnapshotAsync(snapshotPath);
            var databaseVersion = await ReadDatabaseVersionAsync(snapshotPath);
            var metadata = new BackupMetadata(
                CurrentBackupFormatVersion,
                appVersion,
                databaseVersion,
                createdAt
            );
            await File.WriteAllTextAsync(
                metadataPath,
                JsonSerializer.Serialize(metadata, JsonOptions)
            );

            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(metadataPath, MetadataEntryName);
                archive.CreateEntryFromFile(snapshotPath, DatabaseEntryName);
            }

            File.Delete(metadataPath);
            File.Delete(snapshotPath);
            return new BackupArtifact(packagePath, workspacePath, metadata);
        }
        catch
        {
            DeleteWorkspace(workspacePath);
            throw;
        }
    }

    public async Task<BackupInspectionResult> InspectAsync(string packagePath)
    {
        var preparation = await PrepareRestoreAsync(packagePath);
        if (preparation.Artifact is not null)
            await preparation.Artifact.DisposeAsync();
        return preparation.Inspection;
    }

    public async Task<PrepareRestoreResult> PrepareRestoreAsync(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            return InvalidPreparation(BackupInspectionFailure.FileNotFound);

        string? workspacePath = null;
        var preserveWorkspace = false;
        try
        {
            workspacePath = CreateWorkspace(RequireDirectory(platform.CacheDirectory), "inspect");
            using var archive = ZipFile.OpenRead(packagePath);
            if (
                archive.Entries.Any(entry =>
                    entry.FullName.Contains('/') || entry.FullName.Contains('\\')
                )
            )
                return InvalidPreparation(BackupInspectionFailure.InvalidPackage);

            var metadataEntries = archive
                .Entries.Where(entry => entry.FullName == MetadataEntryName)
                .ToArray();
            var databaseEntries = archive
                .Entries.Where(entry => entry.FullName == DatabaseEntryName)
                .ToArray();
            if (metadataEntries.Length != 1 || databaseEntries.Length != 1)
                return InvalidPreparation(
                    BackupInspectionFailure.MissingRequiredContent
                );
            if (archive.Entries.Count != 2)
                return InvalidPreparation(BackupInspectionFailure.InvalidPackage);

            BackupMetadata? metadata;
            await using (var metadataStream = metadataEntries[0].Open())
            {
                metadata = await JsonSerializer.DeserializeAsync<BackupMetadata>(
                    metadataStream,
                    JsonOptions
                );
            }
            if (!IsValidMetadata(metadata))
                return InvalidPreparation(BackupInspectionFailure.InvalidMetadata);
            if (metadata!.BackupFormatVersion != CurrentBackupFormatVersion)
                return InvalidPreparation(
                    BackupInspectionFailure.UnsupportedBackupFormat
                );

            var extractedDatabasePath = Path.Combine(workspacePath, DatabaseEntryName);
            await using (var source = databaseEntries[0].Open())
            await using (
                var destination = new FileStream(
                    extractedDatabasePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                await source.CopyToAsync(destination);
            }

            var databaseCheck = await InspectDatabaseAsync(extractedDatabasePath);
            if (!databaseCheck.IsRecognizable)
                return InvalidPreparation(BackupInspectionFailure.InvalidDatabase);
            if (databaseCheck.Version != metadata.DatabaseVersion)
                return InvalidPreparation(
                    BackupInspectionFailure.DatabaseVersionMismatch
                );
            if (
                databaseCheck.Version < AppDatabase.MinimumSupportedSchemaVersion
                || databaseCheck.Version > AppDatabase.CurrentSchemaVersion
            )
                return InvalidPreparation(
                    BackupInspectionFailure.UnsupportedDatabaseVersion
                );

            var compatibility =
                databaseCheck.Version == AppDatabase.CurrentSchemaVersion
                    ? BackupDatabaseCompatibility.Current
                    : BackupDatabaseCompatibility.OlderSupported;
            var inspection = BackupInspectionResult.Valid(metadata, compatibility);
            var artifact = new PreparedRestoreArtifact(
                extractedDatabasePath,
                workspacePath,
                metadata,
                compatibility
            );
            preserveWorkspace = true;
            return new PrepareRestoreResult(inspection, artifact);
        }
        catch (JsonException)
        {
            return InvalidPreparation(BackupInspectionFailure.InvalidMetadata);
        }
        catch (InvalidDataException)
        {
            return InvalidPreparation(BackupInspectionFailure.InvalidPackage);
        }
        catch (SQLiteException)
        {
            return InvalidPreparation(BackupInspectionFailure.InvalidDatabase);
        }
        catch (IOException)
        {
            return InvalidPreparation(BackupInspectionFailure.InvalidPackage);
        }
        finally
        {
            if (workspacePath is not null && !preserveWorkspace)
                DeleteWorkspace(workspacePath);
        }
    }

    private static PrepareRestoreResult InvalidPreparation(BackupInspectionFailure failure) =>
        new(BackupInspectionResult.Invalid(failure), null);

    internal static void DeleteWorkspace(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
            Directory.Delete(workspacePath, recursive: true);
    }

    private static string RequireDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }

    private static string CreateWorkspace(string cacheDirectory, string purpose)
    {
        var workspacePath = Path.Combine(
            cacheDirectory,
            $"SoftballScorekeeper.Backup-{purpose}-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(workspacePath);
        return workspacePath;
    }

    private static bool IsValidMetadata(BackupMetadata? metadata) =>
        metadata is not null
        && metadata.BackupFormatVersion >= 1
        && !string.IsNullOrWhiteSpace(metadata.AppVersion)
        && metadata.DatabaseVersion >= 0
        && metadata.CreatedAt != default
        && metadata.CreatedAt.Offset == TimeSpan.Zero;

    private static async Task<int> ReadDatabaseVersionAsync(string databasePath)
    {
        var check = await InspectDatabaseAsync(databasePath);
        if (!check.IsRecognizable)
            throw new InvalidDataException("The SQLite snapshot is not recognizable.");
        return check.Version;
    }

    private static async Task<DatabaseCheck> InspectDatabaseAsync(string databasePath)
    {
        var connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.SharedCache
        );
        try
        {
            var integrity = await connection.ExecuteScalarAsync<string>("PRAGMA quick_check");
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                return new DatabaseCheck(false, 0);

            var tableNames = await connection.QueryAsync<SqliteObjectName>(
                "SELECT name FROM sqlite_master WHERE type = 'table'"
            );
            var names = tableNames.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
            if (RecognizableTableNames.Any(name => !names.Contains(name)))
                return new DatabaseCheck(false, 0);

            var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version");
            return new DatabaseCheck(true, version);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed class SqliteObjectName
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed record DatabaseCheck(bool IsRecognizable, int Version);
}
