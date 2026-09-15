using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class DataManagementViewModel(ManualBackupService manualBackupService)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupDataCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasStatusMessage;

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
}
