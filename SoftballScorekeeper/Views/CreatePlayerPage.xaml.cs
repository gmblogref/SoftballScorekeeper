using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class CreatePlayerPage : ContentPage
{
    public CreatePlayerPage(CreatePlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
