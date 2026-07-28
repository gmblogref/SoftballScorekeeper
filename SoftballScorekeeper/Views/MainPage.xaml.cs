using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
