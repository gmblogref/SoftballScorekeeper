using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class DataManagementViewModel(
    ManualBackupService manualBackupService,
    ManualRestoreService manualRestoreService
)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupDataCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDataCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasStatusMessage;

    public Func<BackupMetadata, BackupDatabaseCompatibility, Task<bool>>?
        RestoreConfirmationRequested { get; set; }

    public Func<Task>? RestoreCompletedNotificationRequested { get; set; }

    [RelayCommand(CanExecute = nameof(CanBackupData))]
    private async Task BackupDataAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
        try
        {
            var result = await manualBackupService.RunAsync();
            if (result == ManualBackupResult.Saved)
            {
                StatusMessage = AppResources.BackupDataSucceeded;
                HasStatusMessage = true;
            }
        }
        catch
        {
            StatusMessage = AppResources.BackupDataFailed;
            HasStatusMessage = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBackupData() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRestoreData))]
    private async Task RestoreDataAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
        try
        {
            var result = await manualRestoreService.RunAsync(
                async (metadata, compatibility) =>
                    RestoreConfirmationRequested is not null
                    && await RestoreConfirmationRequested(metadata, compatibility)
            );
            if (result.Status == ManualRestoreStatus.Restored)
            {
                if (RestoreCompletedNotificationRequested is not null)
                    await RestoreCompletedNotificationRequested();
                await Shell.Current.GoToAsync(AppShell.TeamsSectionRoute);
            }
            else if (result.Status == ManualRestoreStatus.Rejected)
            {
                StatusMessage =
                    result.InspectionFailure
                        is BackupInspectionFailure.UnsupportedBackupFormat
                            or BackupInspectionFailure.UnsupportedDatabaseVersion
                        ? AppResources.RestoreDataUnsupported
                        : AppResources.RestoreDataInvalid;
                HasStatusMessage = true;
            }
        }
        catch (DatabaseRestoreRecoveryException)
        {
            StatusMessage = AppResources.RestoreDataRecoveryFailed;
            HasStatusMessage = true;
        }
        catch
        {
            StatusMessage = AppResources.RestoreDataFailed;
            HasStatusMessage = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRestoreData() => !IsBusy;
}
