using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class MainPage : ContentPage
{
    public MainPage(LandingPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
