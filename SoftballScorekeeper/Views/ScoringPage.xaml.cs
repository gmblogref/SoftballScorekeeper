using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ScoringPage : ContentPage
{
    private readonly ScoringViewModel viewModel;
    private bool isExiting;

    public ScoringPage(ScoringViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isExiting = false;
        await viewModel.LoadAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = ExitAsync();
        return true;
    }

    private void OnScoringMenuClicked(object sender, EventArgs e) =>
        ScoringMenu.IsVisible = !ScoringMenu.IsVisible;

    private async void OnExitGameClicked(object sender, EventArgs e) => await ExitAsync();

    private async void OnBatterTapped(object sender, TappedEventArgs e)
        => await ShowBatterResultMenuAsync();

    private async Task ShowBatterResultMenuAsync()
    {
        var options = new List<string>
        {
            AppResources.SingleResultLabel,
            AppResources.DoubleResultLabel,
            AppResources.TripleResultLabel,
            AppResources.HomeRunResultLabel,
        };
        var selection = await DisplayActionSheet(
            string.Format(AppResources.BatterResultMenuFormat,
                viewModel.CurrentBatterDisplayName),
            AppResources.CancelButton,
            null,
            options.ToArray());
        var result = selection switch
        {
            var value when value == AppResources.SingleResultLabel => BatterResult.Single,
            var value when value == AppResources.DoubleResultLabel => BatterResult.Double,
            var value when value == AppResources.TripleResultLabel => BatterResult.Triple,
            var value when value == AppResources.HomeRunResultLabel => BatterResult.HomeRun,
            _ => (BatterResult?)null,
        };
        if (result is not null) await viewModel.SelectResultAsync(result.Value);
    }

    private async void OnRunnerButtonClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        if (button.CommandParameter is BaseRunnerViewModel
            {
                IsOccupied: true,
                PlayerId: int batterPlayerId,
            }
            && batterPlayerId == viewModel.CurrentBatterPlayerId)
        {
            await ShowBatterResultMenuAsync();
            return;
        }
        var participant = (button.CommandParameter ?? button.BindingContext) switch
        {
            BaseRunnerViewModel { IsOccupied: true, PlayerId: int id } runner =>
                (id, runner.PlayerName),
            ScoredPlayerViewModel scored => (scored.PlayerId, scored.PlayerName),
            _ => ((int PlayerId, string PlayerName)?)null,
        };
        if (participant is null) return;
        var destinations = await viewModel.GetValidRunnerDestinationsAsync(
            participant.Value.PlayerId);
        if (destinations.Count == 0) return;
        var options = destinations.Select(ScoringViewModel.FormatDestination).ToArray();
        var selection = await DisplayActionSheet(
            string.Format(AppResources.RunnerDestinationMenuFormat,
                participant.Value.PlayerName),
            AppResources.CancelButton,
            null,
            options);
        var destination = selection switch
        {
            var value when value == AppResources.FirstBaseLabel => RunnerDestination.First,
            var value when value == AppResources.SecondBaseLabel => RunnerDestination.Second,
            var value when value == AppResources.ThirdBaseLabel => RunnerDestination.Third,
            var value when value == AppResources.ScoresDestinationLabel => RunnerDestination.Scores,
            _ => (RunnerDestination?)null,
        };
        if (destination is not null)
            await viewModel.ChangeRunnerDestinationAsync(
                participant.Value.PlayerId, destination.Value);
    }

    private async Task ExitAsync()
    {
        if (isExiting) return;
        isExiting = true;
        ScoringMenu.IsVisible = false;
        await viewModel.ExitGameAsync();
    }
}
