using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class InGameStatsDialog : ContentPage
{
    private bool isClosing;

    public InGameStatsDialog(IReadOnlyList<GameBattingPlayerProjection> players)
    {
        InitializeComponent();
        var viewModel = new BattingStatisticsTableViewModel();
        viewModel.Load(players);
        BindingContext = viewModel;
    }

    public Task ShowAsync(INavigation navigation) => navigation.PushModalAsync(this);

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await CloseAsync();

    private async Task CloseAsync()
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
    }
}
