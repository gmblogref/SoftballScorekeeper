namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class ManualBackupService(
    BackupPackageService backupPackageService,
    IBackupDestination destination
)
{
    private int isRunning;

    public async Task<ManualBackupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
            return ManualBackupResult.AlreadyInProgress;

        try
        {
            await using var artifact = await backupPackageService.CreateAsync();
            var fileName = $"SoftballScorekeeper-Backup-{artifact.Metadata.CreatedAt:yyyy-MM-dd-HHmm}.zip";
            var result = await destination.SaveAsync(
                artifact.FilePath,
                fileName,
                cancellationToken
            );
            return result == BackupDestinationResult.Saved
                ? ManualBackupResult.Saved
                : ManualBackupResult.Cancelled;
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }
}

public enum ManualBackupResult
{
    Saved,
    Cancelled,
    AlreadyInProgress,
}
