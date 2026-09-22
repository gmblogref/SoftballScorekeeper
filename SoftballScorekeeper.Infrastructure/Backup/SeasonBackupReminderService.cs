using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class SeasonBackupReminderService(
    SeasonBackupReminderRepository reminderRepository,
    ManualBackupService manualBackupService,
    DatabaseModeState modeState
)
{
    private int startupEvaluationClaimed;

    public async Task<SeasonBackupReminderResult> RunAtStartupAsync(
        Func<Task<SeasonBackupReminderChoice>> requestChoice,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(requestChoice);
        if (Interlocked.Exchange(ref startupEvaluationClaimed, 1) != 0)
            return SeasonBackupReminderResult.AlreadyEvaluated;
        if (!modeState.IsProductionMode)
            return SeasonBackupReminderResult.NoReminder;

        var candidates = await reminderRepository.GetEligibleAsync(cancellationToken);
        if (candidates.Count == 0)
            return SeasonBackupReminderResult.NoReminder;

        var choice = await requestChoice();
        if (choice == SeasonBackupReminderChoice.RemindLater)
            return SeasonBackupReminderResult.RemindLater;
        if (choice == SeasonBackupReminderChoice.Dismiss)
        {
            await reminderRepository.DismissAsync(candidates, cancellationToken);
            return SeasonBackupReminderResult.Dismissed;
        }

        try
        {
            return await manualBackupService.RunAsync(cancellationToken) switch
            {
                ManualBackupResult.Saved => SeasonBackupReminderResult.BackupSaved,
                ManualBackupResult.Cancelled => SeasonBackupReminderResult.BackupCancelled,
                ManualBackupResult.AlreadyInProgress =>
                    SeasonBackupReminderResult.BackupAlreadyInProgress,
                ManualBackupResult.UnavailableInTestMode =>
                    SeasonBackupReminderResult.BackupUnavailable,
                _ => SeasonBackupReminderResult.BackupFailed,
            };
        }
        catch
        {
            return SeasonBackupReminderResult.BackupFailed;
        }
    }
}

public enum SeasonBackupReminderChoice
{
    Ok,
    RemindLater,
    Dismiss,
}

public enum SeasonBackupReminderResult
{
    NoReminder,
    AlreadyEvaluated,
    RemindLater,
    Dismissed,
    BackupSaved,
    BackupCancelled,
    BackupAlreadyInProgress,
    BackupUnavailable,
    BackupFailed,
}
