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
        BindingContext = viewModel;
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
