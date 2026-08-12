using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class GameWorkspacePage : ContentPage
{
    private readonly GameWorkspaceViewModel viewModel;

    public GameWorkspacePage(GameWorkspaceViewModel viewModel)
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
