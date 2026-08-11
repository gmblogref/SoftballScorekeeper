using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class AddGamePage : ContentPage
{
    private readonly AddGameViewModel viewModel;

    public AddGamePage(AddGameViewModel viewModel)
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
