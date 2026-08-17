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
        var selection = await DisplayActionSheet(
            AppResources.HomeAwayPromptTitle,
            AppResources.CancelButton,
            null,
            AppResources.AwayButton,
            AppResources.HomeButton);
        if (selection == AppResources.AwayButton)
            await viewModel.InitializeScoringAsync(Core.Models.TeamGameSide.Away);
        else if (selection == AppResources.HomeButton)
            await viewModel.InitializeScoringAsync(Core.Models.TeamGameSide.Home);
    }
}
