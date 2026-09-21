using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class ManualBackupService
{
    private readonly BackupPackageService backupPackageService;
    private readonly IBackupDestination destination;
    private readonly DatabaseModeState modeState;
    private readonly ApplicationWorkflowCoordinator workflowCoordinator;

    public ManualBackupService(
        BackupPackageService backupPackageService,
        IBackupDestination destination,
        DatabaseModeState modeState,
        ApplicationWorkflowCoordinator workflowCoordinator
    )
    {
        this.backupPackageService = backupPackageService;
        this.destination = destination;
        this.modeState = modeState;
        this.workflowCoordinator = workflowCoordinator;
    }

    public ManualBackupService(
        BackupPackageService backupPackageService,
        IBackupDestination destination
    )
        : this(
            backupPackageService,
            destination,
            new DatabaseModeState(DatabaseMode.Production),
            new ApplicationWorkflowCoordinator()
        ) { }

    public async Task<ManualBackupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var workflow = await workflowCoordinator.TryBeginAsync(cancellationToken);
        if (workflow is null)
            return ManualBackupResult.AlreadyInProgress;
        if (!modeState.CanUsePortableBackupAndRestore)
            return ManualBackupResult.UnavailableInTestMode;

        await using var artifact = await backupPackageService.CreateAsync();
        var fileName =
            $"SoftballScorekeeper-Backup-{artifact.Metadata.CreatedAt:yyyy-MM-dd-HHmm}.zip";
        var result = await destination.SaveAsync(artifact.FilePath, fileName, cancellationToken);
        return result == BackupDestinationResult.Saved
            ? ManualBackupResult.Saved
            : ManualBackupResult.Cancelled;
    }
}

public enum ManualBackupResult
{
    Saved,
    Cancelled,
    AlreadyInProgress,
    UnavailableInTestMode,
}
