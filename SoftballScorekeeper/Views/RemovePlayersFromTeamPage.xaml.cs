using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class RemovePlayersFromTeamPage : ContentPage
{
    private readonly RemovePlayersFromTeamViewModel viewModel;

    public RemovePlayersFromTeamPage(RemovePlayersFromTeamViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.RemovePlayersConfirmationRequested = ConfirmRemovePlayersAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private async Task ConfirmRemovePlayersAsync(int selectedCount, string teamName)
    {
        var template = selectedCount == 1
            ? AppResources.RemovePlayerFromTeamConfirmationMessage
            : AppResources.RemovePlayersFromTeamConfirmationMessage;
        var message = string.Format(CultureInfo.CurrentCulture, template, selectedCount, teamName);
        var confirmed = await DisplayAlert(
            AppResources.RemovePlayersFromTeamConfirmationTitle,
            message,
            AppResources.ConfirmRemovePlayersFromTeamButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmRemoveSelectedPlayersCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmRemoveSelectedPlayersCommand.ExecuteAsync(null);
    }
}
