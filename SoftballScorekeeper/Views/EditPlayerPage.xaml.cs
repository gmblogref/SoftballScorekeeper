using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class EditPlayerPage : ContentPage
{
    private readonly EditPlayerViewModel viewModel;

    public EditPlayerPage(EditPlayerViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadPlayerAsync();
    }
}
