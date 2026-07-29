using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class EditTeamPage : ContentPage
{
    private readonly EditTeamViewModel viewModel;

    public EditTeamPage(EditTeamViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await viewModel.LoadTeamAsync();
    }
}
