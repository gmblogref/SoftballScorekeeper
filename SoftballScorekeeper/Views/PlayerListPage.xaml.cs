using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class PlayerListPage : ContentPage
{
    private readonly PlayerListViewModel viewModel;

    public PlayerListPage(PlayerListViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await viewModel.LoadPlayersAsync();
    }
}
