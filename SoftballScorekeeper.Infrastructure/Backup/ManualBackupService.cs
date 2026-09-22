using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class ManualBackupService
{
    private readonly BackupPackageService backupPackageService;
    private readonly IBackupDestination destination;
    private readonly DatabaseModeState modeState;
    private readonly ApplicationWorkflowCoordinator workflowCoordinator;
    private readonly SeasonBackupReminderRepository? reminderRepository;

    public ManualBackupService(
        BackupPackageService backupPackageService,
        IBackupDestination destination,
        DatabaseModeState modeState,
        ApplicationWorkflowCoordinator workflowCoordinator,
        SeasonBackupReminderRepository? reminderRepository
    )
    {
        this.backupPackageService = backupPackageService;
        this.destination = destination;
        this.modeState = modeState;
        this.workflowCoordinator = workflowCoordinator;
        this.reminderRepository = reminderRepository;
    }

    public ManualBackupService(
        BackupPackageService backupPackageService,
        IBackupDestination destination
    )
        : this(
            backupPackageService,
            destination,
            new DatabaseModeState(DatabaseMode.Production),
            new ApplicationWorkflowCoordinator(),
            null
        ) { }

    public ManualBackupService(
        BackupPackageService backupPackageService,
        IBackupDestination destination,
        DatabaseModeState modeState,
        ApplicationWorkflowCoordinator workflowCoordinator
    )
        : this(
            backupPackageService,
            destination,
            modeState,
            workflowCoordinator,
            null
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
        if (result != BackupDestinationResult.Saved)
            return ManualBackupResult.Cancelled;

        if (reminderRepository is not null)
        {
            try
            {
                await reminderRepository.SatisfyEligibleAsync(cancellationToken);
            }
            catch
            {
                // The destination write succeeded. Leave checkpoints unsatisfied rather than
                // misreporting the saved backup as a failure.
            }
        }

        return ManualBackupResult.Saved;
    }
}

public enum ManualBackupResult
{
    Saved,
    Cancelled,
    AlreadyInProgress,
    UnavailableInTestMode,
}
