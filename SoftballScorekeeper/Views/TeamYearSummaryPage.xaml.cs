using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class TeamYearSummaryPage : ContentPage
{
    private readonly TeamYearSummaryViewModel viewModel;

    public TeamYearSummaryPage(TeamYearSummaryViewModel viewModel)
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
}
