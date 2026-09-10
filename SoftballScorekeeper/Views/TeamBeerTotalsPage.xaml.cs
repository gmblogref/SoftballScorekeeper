using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class TeamBeerTotalsPage : ContentPage
{
    private readonly TeamBeerTotalsViewModel viewModel;

    public TeamBeerTotalsPage(TeamBeerTotalsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        TotalsGrid.SaveRequested = viewModel.SaveEditAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private async void OnCalculateLowManClicked(object sender, EventArgs e) =>
        await viewModel.CalculateLowManAsync();

}
