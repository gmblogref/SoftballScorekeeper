using System.Globalization;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class DataManagementPage : ContentPage
{
    public DataManagementPage(DataManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.RestoreConfirmationRequested = ConfirmRestoreAsync;
        viewModel.RestoreCompletedNotificationRequested = ShowRestoreCompletedAsync;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((DataManagementViewModel)BindingContext).BeginObservingMode();
    }

    protected override void OnDisappearing()
    {
        ((DataManagementViewModel)BindingContext).EndObservingMode();
        base.OnDisappearing();
    }

    private Task<bool> ConfirmRestoreAsync(
        BackupMetadata metadata,
        BackupDatabaseCompatibility compatibility
    )
    {
        var messageTemplate =
            compatibility == BackupDatabaseCompatibility.OlderSupported
                ? AppResources.RestoreDataOlderConfirmationMessage
                : AppResources.RestoreDataConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            metadata.CreatedAt.ToLocalTime(),
            metadata.AppVersion
        );
        return DisplayAlert(
            AppResources.RestoreDataConfirmationTitle,
            message,
            AppResources.RestoreDataConfirmButton,
            AppResources.CancelButton
        );
    }

    private Task ShowRestoreCompletedAsync() =>
        DisplayAlert(
            AppResources.RestoreDataSucceededTitle,
            AppResources.RestoreDataSucceeded,
            AppResources.OkButton
        );
}
