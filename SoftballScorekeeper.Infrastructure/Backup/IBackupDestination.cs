namespace SoftballScorekeeper.Infrastructure.Backup;

public interface IBackupDestination
{
    Task<BackupDestinationResult> SaveAsync(
        string sourceFilePath,
        string suggestedFileName,
        CancellationToken cancellationToken = default
    );
}

public enum BackupDestinationResult
{
    Saved,
    Cancelled,
}
