using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class PlayerListPage : ContentPage
{
    private readonly PlayerListViewModel viewModel;

    public PlayerListPage(PlayerListViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        viewModel.DeactivatePlayersConfirmationRequested = ConfirmPlayerDeactivationAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await viewModel.LoadPlayersAsync();
    }

    private async Task ConfirmPlayerDeactivationAsync(int selectedPlayerCount)
    {
        var messageTemplate = selectedPlayerCount == 1
            ? AppResources.DeactivatePlayerConfirmationMessage
            : AppResources.DeactivatePlayersConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            selectedPlayerCount);

        var confirmed = await DisplayAlert(
            AppResources.DeactivatePlayersConfirmationTitle,
            message,
            AppResources.ConfirmDeactivatePlayersButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmDeactivateSelectedPlayersCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmDeactivateSelectedPlayersCommand.ExecuteAsync(null);
    }
}
