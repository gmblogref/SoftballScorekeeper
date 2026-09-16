using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class DatabaseRestoreService(AppDatabase database)
{
    private static readonly string[] ControlledSidecarSuffixes = ["-journal", "-wal", "-shm"];

    public async Task RestoreAsync(
        string replacementDatabasePath,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementDatabasePath);
        if (!File.Exists(replacementDatabasePath))
            throw new FileNotFoundException(
                "The prepared replacement database was not found.",
                replacementDatabasePath
            );

        await using var exclusive = await database.BeginExclusiveLifecycleAsync(cancellationToken);
        await database.SuspendAccessAsync();

        var databasePath = Path.GetFullPath(database.DatabasePath);
        var directoryPath = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("The application database directory is invalid.");
        Directory.CreateDirectory(directoryPath);
        var operationId = Guid.NewGuid().ToString("N");
        var safetyCopyPath = Path.Combine(
            directoryPath,
            $"{Path.GetFileName(databasePath)}.restore-safety-{operationId}"
        );
        var stagingPath = Path.Combine(
            directoryPath,
            $"{Path.GetFileName(databasePath)}.restore-staging-{operationId}"
        );
        var replacementInstalled = false;
        var safetyCopyCreated = false;
        var cleanupSafetyCopy = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(databasePath, safetyCopyPath, overwrite: false);
            safetyCopyCreated = true;
            File.Copy(replacementDatabasePath, stagingPath, overwrite: false);
            cancellationToken.ThrowIfCancellationRequested();

            DeleteControlledSidecars(databasePath);
            File.Move(stagingPath, databasePath, overwrite: true);
            replacementInstalled = true;

            await database.ReinitializeAccessAsync();
            cleanupSafetyCopy = true;
        }
        catch (Exception restoreFailure)
        {
            try
            {
                if (replacementInstalled)
                {
                    DeleteControlledSidecars(databasePath);
                    File.Copy(safetyCopyPath, stagingPath, overwrite: true);
                    File.Move(stagingPath, databasePath, overwrite: true);
                }

                await database.ReinitializeAccessAsync();
                cleanupSafetyCopy = true;
            }
            catch (Exception rollbackFailure)
            {
                throw new DatabaseRestoreRecoveryException(
                    safetyCopyCreated ? safetyCopyPath : null,
                    restoreFailure,
                    rollbackFailure
                );
            }

            throw;
        }
        finally
        {
            DeleteIfExists(stagingPath);
            if (cleanupSafetyCopy)
                DeleteIfExists(safetyCopyPath);
        }
    }

    private static void DeleteControlledSidecars(string databasePath)
    {
        foreach (var suffix in ControlledSidecarSuffixes)
            DeleteIfExists(databasePath + suffix);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

public sealed class DatabaseRestoreRecoveryException : Exception
{
    internal DatabaseRestoreRecoveryException(
        string? safetyCopyPath,
        Exception restoreFailure,
        Exception rollbackFailure
    )
        : base(
            "Database restore failed and the prior database could not be reinitialized.",
            new AggregateException(restoreFailure, rollbackFailure)
        )
    {
        SafetyCopyPath = safetyCopyPath;
    }

    public string? SafetyCopyPath { get; }
}
