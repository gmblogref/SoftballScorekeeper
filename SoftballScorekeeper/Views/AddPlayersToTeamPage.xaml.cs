using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class AddPlayersToTeamPage : ContentPage
{
    private readonly AddPlayersToTeamViewModel viewModel;

    public AddPlayersToTeamPage(AddPlayersToTeamViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.AddPlayersConfirmationRequested = ConfirmAddPlayersAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private async Task ConfirmAddPlayersAsync(int selectedCount, string teamName)
    {
        var template = selectedCount == 1
            ? AppResources.AddPlayerToTeamConfirmationMessage
            : AppResources.AddPlayersToTeamConfirmationMessage;
        var message = string.Format(CultureInfo.CurrentCulture, template, selectedCount, teamName);
        var confirmed = await DisplayAlertAsync(
            AppResources.AddPlayersToTeamConfirmationTitle,
            message,
            AppResources.ConfirmAddPlayersToTeamButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmAddSelectedPlayersCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmAddSelectedPlayersCommand.ExecuteAsync(null);
    }
}
