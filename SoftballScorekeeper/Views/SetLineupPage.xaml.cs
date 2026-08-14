using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class SetLineupPage : ContentPage
{
    private readonly SetLineupViewModel viewModel;
    public SetLineupPage(SetLineupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        viewModel.RemovalConfirmationRequested = ConfirmRemovalAsync;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await viewModel.LoadAsync(); }

    protected override bool OnBackButtonPressed() => viewModel.TryExitTemporaryMode() || base.OnBackButtonPressed();

    private async Task ConfirmRemovalAsync(int selectedCount)
    {
        var template = selectedCount == 1
            ? AppResources.LineupRemovalConfirmationSingular
            : AppResources.LineupRemovalConfirmationPlural;
        var confirmed = await DisplayAlert(
            AppResources.LineupRemovalConfirmationTitle,
            string.Format(CultureInfo.CurrentCulture, template, selectedCount),
            AppResources.ConfirmRemoveLineupPlayersButton,
            AppResources.CancelButton);
        if (confirmed && viewModel.ConfirmRemovalCommand.CanExecute(null))
            await viewModel.ConfirmRemovalCommand.ExecuteAsync(null);
    }
}
