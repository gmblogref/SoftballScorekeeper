using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class DataManagementPage : ContentPage
{
    public DataManagementPage(DataManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
