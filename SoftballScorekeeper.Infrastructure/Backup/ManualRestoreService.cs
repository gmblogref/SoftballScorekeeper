using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class ManualRestoreService
{
    private readonly BackupPackageService backupPackageService;
    private readonly DatabaseRestoreService databaseRestoreService;
    private readonly IRestoreSource source;
    private readonly DatabaseModeState modeState;
    private readonly ApplicationWorkflowCoordinator workflowCoordinator;

    public ManualRestoreService(
        BackupPackageService backupPackageService,
        DatabaseRestoreService databaseRestoreService,
        IRestoreSource source,
        DatabaseModeState modeState,
        ApplicationWorkflowCoordinator workflowCoordinator
    )
    {
        this.backupPackageService = backupPackageService;
        this.databaseRestoreService = databaseRestoreService;
        this.source = source;
        this.modeState = modeState;
        this.workflowCoordinator = workflowCoordinator;
    }

    public ManualRestoreService(
        BackupPackageService backupPackageService,
        DatabaseRestoreService databaseRestoreService,
        IRestoreSource source
    )
        : this(
            backupPackageService,
            databaseRestoreService,
            source,
            new DatabaseModeState(DatabaseMode.Production),
            new ApplicationWorkflowCoordinator()
        ) { }

    public async Task<ManualRestoreResult> RunAsync(
        Func<BackupMetadata, BackupDatabaseCompatibility, Task<bool>> confirmRestore,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(confirmRestore);
        await using var workflow = await workflowCoordinator.TryBeginAsync(cancellationToken);
        if (workflow is null)
            return ManualRestoreResult.AlreadyInProgress();
        if (!modeState.CanUsePortableBackupAndRestore)
            return ManualRestoreResult.UnavailableInTestMode();

        await using var selected = await source.SelectAsync(cancellationToken);
        if (selected is null)
            return ManualRestoreResult.Cancelled();

        var preparation = await backupPackageService.PrepareRestoreAsync(selected.FilePath);
        if (!preparation.Inspection.IsValid || preparation.Artifact is null)
            return ManualRestoreResult.Rejected(preparation.Inspection.Failure);

        await using var prepared = preparation.Artifact;
        if (!await confirmRestore(prepared.Metadata, prepared.Compatibility))
            return ManualRestoreResult.Cancelled();

        await databaseRestoreService.RestoreAsync(prepared.DatabasePath, cancellationToken);
        return ManualRestoreResult.Restored();
    }
}

public enum ManualRestoreStatus
{
    Restored,
    Cancelled,
    AlreadyInProgress,
    UnavailableInTestMode,
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

    internal static ManualRestoreResult UnavailableInTestMode() =>
        new(ManualRestoreStatus.UnavailableInTestMode, BackupInspectionFailure.None);

    internal static ManualRestoreResult Rejected(BackupInspectionFailure failure) =>
        new(ManualRestoreStatus.Rejected, failure);
}
