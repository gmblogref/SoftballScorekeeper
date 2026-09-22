using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Resources.Strings;
#if DEBUG && ANDROID
using SoftballScorekeeper.Testing;
#endif

namespace SoftballScorekeeper.Views;

public partial class MainPage : ContentPage
{
    private readonly LandingPageViewModel viewModel;
    private readonly SeasonBackupReminderService backupReminderService;

    public MainPage(
        LandingPageViewModel viewModel,
        SeasonBackupReminderService backupReminderService
    )
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.backupReminderService = backupReminderService;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if DEBUG && ANDROID
        if (
            AndroidUiTestDataInitializer.IsInitializationRequested(FileSystem.AppDataDirectory)
            || AndroidUiTestDataInitializer.IsInitializationInProgress
        )
            return;
#endif
        await ReloadAsync();
        var result = await backupReminderService.RunAtStartupAsync(RequestBackupReminderChoiceAsync);
        if (result == SeasonBackupReminderResult.BackupSaved)
        {
            await DisplayAlert(
                AppResources.DataManagementTitle,
                AppResources.BackupDataSucceeded,
                AppResources.OkButton
            );
        }
        else if (result == SeasonBackupReminderResult.BackupFailed)
        {
            await DisplayAlert(
                AppResources.DataManagementTitle,
                AppResources.BackupDataFailed,
                AppResources.OkButton
            );
        }
    }

    public Task ReloadAsync() => viewModel.LoadTeamsAsync();

    private async Task<SeasonBackupReminderChoice> RequestBackupReminderChoiceAsync()
    {
        var selection = await DisplayActionSheet(
            AppResources.SeasonBackupReminderMessage,
            AppResources.SeasonBackupReminderRemindLater,
            AppResources.SeasonBackupReminderDismiss,
            AppResources.OkButton
        );
        if (selection == AppResources.OkButton)
            return SeasonBackupReminderChoice.Ok;
        if (selection == AppResources.SeasonBackupReminderDismiss)
            return SeasonBackupReminderChoice.Dismiss;
        return SeasonBackupReminderChoice.RemindLater;
    }
}
