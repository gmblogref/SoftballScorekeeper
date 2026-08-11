using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class TeamWorkspacePage : ContentPage
{
    private readonly TeamWorkspaceViewModel viewModel;

    public TeamWorkspacePage(TeamWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.DeactivateTeamConfirmationRequested = ConfirmTeamDeactivationAsync;
        viewModel.RemovePlayersConfirmationRequested = ConfirmPlayerRemovalAsync;
        viewModel.RemoveGamesConfirmationRequested = ConfirmGameRemovalAsync;
        BindingContext = viewModel;
    }

    private async Task ConfirmGameRemovalAsync(int selectedGameCount)
    {
        var messageTemplate = selectedGameCount == 1
            ? AppResources.RemoveGameConfirmationMessage
            : AppResources.RemoveGamesConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            selectedGameCount);

        var confirmed = await DisplayAlert(
            AppResources.RemoveGamesConfirmationTitle,
            message,
            AppResources.ConfirmRemoveGamesButton,
            AppResources.CancelButton);

        if (confirmed && viewModel.ConfirmRemoveSelectedGamesCommand.CanExecute(null))
        {
            await viewModel.ConfirmRemoveSelectedGamesCommand.ExecuteAsync(null);
        }
    }

    private async Task ConfirmPlayerRemovalAsync(int selectedPlayerCount, string teamName)
    {
        var messageTemplate = selectedPlayerCount == 1
            ? AppResources.RemovePlayerFromTeamConfirmationMessage
            : AppResources.RemovePlayersFromTeamConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            selectedPlayerCount,
            teamName);

        var confirmed = await DisplayAlert(
            AppResources.RemovePlayersFromTeamConfirmationTitle,
            message,
            AppResources.ConfirmRemovePlayersFromTeamButton,
            AppResources.CancelButton);

        if (confirmed && viewModel.ConfirmRemoveSelectedPlayersCommand.CanExecute(null))
        {
            await viewModel.ConfirmRemoveSelectedPlayersCommand.ExecuteAsync(null);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadTeamAsync();
    }

    private async Task ConfirmTeamDeactivationAsync(string teamName)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.DeactivateTeamConfirmationMessage,
            teamName);

        var confirmed = await DisplayAlert(
            AppResources.DeactivateTeamConfirmationTitle,
            message,
            AppResources.ConfirmDeactivateTeamButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmDeactivateTeamCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmDeactivateTeamCommand.ExecuteAsync(null);
    }
}
