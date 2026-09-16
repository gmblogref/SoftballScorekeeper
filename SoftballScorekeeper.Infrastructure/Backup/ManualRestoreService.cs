namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class ManualRestoreService(
    BackupPackageService backupPackageService,
    DatabaseRestoreService databaseRestoreService,
    IRestoreSource source
)
{
    private int isRunning;

    public async Task<ManualRestoreResult> RunAsync(
        Func<BackupMetadata, BackupDatabaseCompatibility, Task<bool>> confirmRestore,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(confirmRestore);
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
            return ManualRestoreResult.AlreadyInProgress();

        try
        {
            await using var selected = await source.SelectAsync(cancellationToken);
            if (selected is null)
                return ManualRestoreResult.Cancelled();

            var preparation = await backupPackageService.PrepareRestoreAsync(selected.FilePath);
            if (!preparation.Inspection.IsValid || preparation.Artifact is null)
                return ManualRestoreResult.Rejected(preparation.Inspection.Failure);

            await using var prepared = preparation.Artifact;
            if (!await confirmRestore(prepared.Metadata, prepared.Compatibility))
                return ManualRestoreResult.Cancelled();

            await databaseRestoreService.RestoreAsync(
                prepared.DatabasePath,
                cancellationToken
            );
            return ManualRestoreResult.Restored();
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }
}

public enum ManualRestoreStatus
{
    Restored,
    Cancelled,
    AlreadyInProgress,
    Rejected,
}

public sealed record ManualRestoreResult(
    ManualRestoreStatus Status,
    BackupInspectionFailure InspectionFailure
)
{
    internal static ManualRestoreResult Restored() =>
        new(ManualRestoreStatus.Restored, BackupInspectionFailure.None);

    internal static ManualRestoreResult Cancelled() =>
        new(ManualRestoreStatus.Cancelled, BackupInspectionFailure.None);

    internal static ManualRestoreResult AlreadyInProgress() =>
        new(ManualRestoreStatus.AlreadyInProgress, BackupInspectionFailure.None);

    internal static ManualRestoreResult Rejected(BackupInspectionFailure failure) =>
        new(ManualRestoreStatus.Rejected, failure);
}
