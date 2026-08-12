using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class SetLineupPage : ContentPage
{
    private readonly SetLineupViewModel viewModel;
    public SetLineupPage(SetLineupViewModel viewModel) { InitializeComponent(); BindingContext = this.viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await viewModel.LoadAsync(); }
}
