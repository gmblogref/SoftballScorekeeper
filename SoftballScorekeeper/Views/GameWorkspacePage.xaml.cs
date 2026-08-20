using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.Views;

public partial class GameWorkspacePage : ContentPage
{
    private readonly GameWorkspaceViewModel viewModel;

    public GameWorkspacePage(GameWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private async void OnScoreGameClicked(object sender, EventArgs e)
    {
        var dialog = new GameRulesDialog(true,
            Core.Models.GameScoringSession.DefaultHomeRunLimit, false);
        var setup = await dialog.ShowAsync(Navigation);
        if (setup is not null)
            await viewModel.InitializeScoringAsync(setup.TeamSide,
                setup.HomeRunLimit, setup.IsOneUpEnabled);
    }

    private async void OnResetGameClicked(object sender, EventArgs e)
    {
        var firstConfirmation = await DisplayAlert(
            AppResources.ResetGameTitle,
            AppResources.ResetGameFirstConfirmation,
            AppResources.ContinueButton,
            AppResources.CancelButton);
        if (!firstConfirmation) return;
        var finalConfirmation = await DisplayAlert(
            AppResources.ResetGameTitle,
            AppResources.ResetGameFinalConfirmation,
            AppResources.ResetGameButton,
            AppResources.CancelButton);
        if (finalConfirmation)
            await viewModel.ResetScoringAsync();
    }
}
