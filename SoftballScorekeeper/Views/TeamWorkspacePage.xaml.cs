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
        viewModel.CopyTeamConfirmationRequested = ConfirmTeamCopyAsync;
        viewModel.MarkTeamYearOverConfirmationRequested = ConfirmMarkTeamYearOverAsync;
        BindingContext = viewModel;
    }

    private async Task ConfirmGameRemovalAsync(int selectedGameCount)
    {
        var messageTemplate =
            selectedGameCount == 1
                ? AppResources.RemoveGameConfirmationMessage
                : AppResources.RemoveGamesConfirmationMessage;
        var message = string.Format(CultureInfo.CurrentCulture, messageTemplate, selectedGameCount);

        var confirmed = await DisplayAlert(
            AppResources.RemoveGamesConfirmationTitle,
            message,
            AppResources.ConfirmRemoveGamesButton,
            AppResources.CancelButton
        );

        if (confirmed && viewModel.ConfirmRemoveSelectedGamesCommand.CanExecute(null))
        {
            await viewModel.ConfirmRemoveSelectedGamesCommand.ExecuteAsync(null);
        }
    }

    private async Task ConfirmPlayerRemovalAsync(int selectedPlayerCount, string teamName)
    {
        var messageTemplate =
            selectedPlayerCount == 1
                ? AppResources.RemovePlayerFromTeamConfirmationMessage
                : AppResources.RemovePlayersFromTeamConfirmationMessage;
        var message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            selectedPlayerCount,
            teamName
        );

        var confirmed = await DisplayAlert(
            AppResources.RemovePlayersFromTeamConfirmationTitle,
            message,
            AppResources.ConfirmRemovePlayersFromTeamButton,
            AppResources.CancelButton
        );

        if (confirmed && viewModel.ConfirmRemoveSelectedPlayersCommand.CanExecute(null))
        {
            await viewModel.ConfirmRemoveSelectedPlayersCommand.ExecuteAsync(null);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TeamActionMenu.IsVisible = false;
        await viewModel.LoadTeamAsync();
    }

    private void OnTeamMenuClicked(object sender, EventArgs e) =>
        TeamActionMenu.IsVisible = !TeamActionMenu.IsVisible;

    private async void OnBeerRulesClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        var settings = await viewModel.LoadBeerRulesAsync();
        if (settings is null)
        {
            await DisplayAlert(
                AppResources.TeamBeerRulesTitle,
                AppResources.BeerRulesLoadFailed,
                AppResources.OkButton
            );
            return;
        }
        await new BeerRulesDialog(true, settings, viewModel.SaveBeerRulesAsync).ShowAsync(
            Navigation
        );
    }

    private async void OnBeerTotalsClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        await Shell.Current.GoToAsync(
            AppShell.TeamBeerTotalsRoute,
            new ShellNavigationQueryParameters
            {
                [AppShell.TeamIdQueryParameter] = viewModel.TeamId,
            }
        );
    }

    private async void OnTeamStatsClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        await Shell.Current.GoToAsync(
            AppShell.TeamStatsRoute,
            new ShellNavigationQueryParameters
            {
                [AppShell.TeamIdQueryParameter] = viewModel.TeamId,
            }
        );
    }

    private async void OnTeamYearSummaryClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        await Shell.Current.GoToAsync(
            AppShell.TeamYearSummaryRoute,
            new ShellNavigationQueryParameters
            {
                [AppShell.TeamIdQueryParameter] = viewModel.TeamId,
            }
        );
    }

    private void OnMarkTeamYearOverClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        if (viewModel.RequestMarkTeamYearOverCommand.CanExecute(null))
            viewModel.RequestMarkTeamYearOverCommand.Execute(null);
    }

    private void OnDeactivateTeamClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        if (viewModel.RequestDeactivateTeamCommand.CanExecute(null))
        {
            viewModel.RequestDeactivateTeamCommand.Execute(null);
        }
    }

    private void OnCopyTeamClicked(object sender, EventArgs e)
    {
        TeamActionMenu.IsVisible = false;
        if (viewModel.RequestCopyTeamCommand.CanExecute(null))
            viewModel.RequestCopyTeamCommand.Execute(null);
    }

    private async Task ConfirmTeamCopyAsync(string teamName, int sourceYear, int destinationYear)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.CopyTeamConfirmationMessage,
            teamName,
            sourceYear,
            destinationYear
        );
        var confirmed = await DisplayAlert(
            AppResources.CopyTeamConfirmationTitle,
            message,
            AppResources.CopyTeamConfirmButton,
            AppResources.CancelButton
        );
        if (confirmed && viewModel.ConfirmCopyTeamCommand.CanExecute(null))
            await viewModel.ConfirmCopyTeamCommand.ExecuteAsync(null);
    }

    private async Task ConfirmMarkTeamYearOverAsync(string teamName, int teamYear)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.MarkTeamYearOverConfirmationMessage,
            teamName,
            teamYear
        );
        var continued = await DisplayAlert(
            AppResources.MarkTeamYearOverConfirmationTitle,
            message,
            AppResources.MarkTeamYearOverConfirmButton,
            AppResources.CancelButton
        );
        if (!continued)
            return;

        var confirmed = await DisplayAlert(
            AppResources.MarkTeamYearOverFinalConfirmationTitle,
            AppResources.MarkTeamYearOverFinalConfirmationMessage,
            AppResources.MarkTeamYearOverFinalConfirmButton,
            AppResources.CancelButton
        );
        if (confirmed && viewModel.ConfirmMarkTeamYearOverCommand.CanExecute(null))
            await viewModel.ConfirmMarkTeamYearOverCommand.ExecuteAsync(null);
    }

    private async Task ConfirmTeamDeactivationAsync(string teamName)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.DeactivateTeamConfirmationMessage,
            teamName
        );

        var confirmed = await DisplayAlert(
            AppResources.DeactivateTeamConfirmationTitle,
            message,
            AppResources.ConfirmDeactivateTeamButton,
            AppResources.CancelButton
        );

        if (!confirmed || !viewModel.ConfirmDeactivateTeamCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.ConfirmDeactivateTeamCommand.ExecuteAsync(null);
    }
}
