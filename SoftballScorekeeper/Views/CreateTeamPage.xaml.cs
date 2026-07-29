using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class CreateTeamPage : ContentPage
{
    public CreateTeamPage(CreateTeamViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
