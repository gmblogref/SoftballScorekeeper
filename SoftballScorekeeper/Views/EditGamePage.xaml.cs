using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class EditGamePage : ContentPage
{
    private readonly EditGameViewModel viewModel;

    public EditGamePage(EditGameViewModel viewModel)
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
