using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class TeamStatsPage : ContentPage
{
    private readonly TeamStatsViewModel viewModel;

    public TeamStatsPage(TeamStatsViewModel viewModel)
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

    private async void OnCustomClicked(object sender, EventArgs e)
    {
        var dialog = new TeamStatsCustomFilterDialog(
            viewModel.CompletedGames,
            viewModel.SelectedGameIds);
        var selectedIds = await dialog.ShowAsync(Navigation);
        if (selectedIds is not null)
            await viewModel.ApplyCustomAsync(selectedIds);
    }
}
