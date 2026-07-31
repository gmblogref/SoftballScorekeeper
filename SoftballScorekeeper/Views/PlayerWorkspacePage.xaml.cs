using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class PlayerWorkspacePage : ContentPage
{
    private readonly PlayerWorkspaceViewModel viewModel;

    public PlayerWorkspacePage(PlayerWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.DeactivatePlayerConfirmationRequested = ConfirmPlayerDeactivationAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadPlayerAsync();
    }

    private async Task ConfirmPlayerDeactivationAsync(string playerName)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.DeactivateSinglePlayerConfirmationMessage,
            playerName);

        var confirmed = await DisplayAlert(
            AppResources.DeactivatePlayerConfirmationTitle,
            message,
            AppResources.ConfirmDeactivatePlayersButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmDeactivatePlayerCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmDeactivatePlayerCommand.ExecuteAsync(null);
    }
}
