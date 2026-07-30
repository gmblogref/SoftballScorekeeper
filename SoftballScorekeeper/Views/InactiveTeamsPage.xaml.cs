using System.Globalization;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class InactiveTeamsPage : ContentPage
{
    private readonly InactiveTeamsViewModel viewModel;

    public InactiveTeamsPage(InactiveTeamsViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        viewModel.ReactivateTeamConfirmationRequested = ConfirmTeamReactivationAsync;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await viewModel.LoadInactiveTeamsAsync();
    }

    private async Task ConfirmTeamReactivationAsync(Team team)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.ReactivateTeamConfirmationMessage,
            team.Name);

        var confirmed = await DisplayAlert(
            AppResources.ReactivateTeamConfirmationTitle,
            message,
            AppResources.ConfirmReactivateTeamButton,
            AppResources.CancelButton);

        if (!confirmed || !viewModel.ConfirmReactivateTeamCommand.CanExecute(team))
        {
            return;
        }

        await viewModel.ConfirmReactivateTeamCommand.ExecuteAsync(team);
    }
}
