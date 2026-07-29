using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class MainPage : ContentPage
{
    private readonly LandingPageViewModel viewModel;

    public MainPage(LandingPageViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadTeamsAsync();
    }
}
