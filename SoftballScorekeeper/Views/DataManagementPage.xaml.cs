using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Resources.Strings;
using System.Globalization;

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

    private Task ShowRestoreCompletedAsync() => DisplayAlert(
        AppResources.RestoreDataSucceededTitle,
        AppResources.RestoreDataSucceeded,
        AppResources.OkButton
    );
}
