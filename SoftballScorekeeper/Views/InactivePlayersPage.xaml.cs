using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class InactivePlayersPage : ContentPage
{
    private readonly InactivePlayersViewModel viewModel;

    public InactivePlayersPage(InactivePlayersViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.ReactivatePlayersConfirmationRequested = ConfirmPlayerReactivationAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadInactivePlayersAsync();
    }

    private async Task ConfirmPlayerReactivationAsync(int selectedPlayerCount)
    {
        var messageTemplate = selectedPlayerCount == 1
            ? AppResources.ReactivatePlayerConfirmationMessage
            : AppResources.ReactivatePlayersConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            selectedPlayerCount);

        var confirmed = await DisplayAlert(
            AppResources.ReactivatePlayersConfirmationTitle,
            message,
            AppResources.ConfirmReactivatePlayersButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmReactivateSelectedPlayersCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmReactivateSelectedPlayersCommand.ExecuteAsync(null);
    }
}
