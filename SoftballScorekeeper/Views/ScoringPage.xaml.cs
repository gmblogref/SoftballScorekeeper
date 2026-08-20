using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ScoringPage : ContentPage
{
    private readonly ScoringViewModel viewModel;
    private bool isExiting;
    private bool isHandlingInningTransition;
    private bool isResolvingPlay;

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
        if (isHandlingInningTransition) return;
        await viewModel.LoadAsync();
        await HandlePendingInningTransitionAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = ExitAsync();
        return true;
    }

    private void OnScoringMenuClicked(object sender, EventArgs e) =>
        ScoringMenu.IsVisible = !ScoringMenu.IsVisible;

    private async void OnExitGameClicked(object sender, EventArgs e) => await ExitAsync();

    private async void OnGameRulesClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var dialog = new GameRulesDialog(false, viewModel.HomeRunLimit,
            viewModel.IsOneUpEnabled,
            rules => viewModel.UpdateGameRulesAsync(
                rules.HomeRunLimit, rules.IsOneUpEnabled));
        await dialog.ShowAsync(Navigation);
    }

    private async void OnGameOverClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var confirmed = await DisplayAlert(
            AppResources.GameOverTitle,
            AppResources.GameOverConfirmationMessage,
            AppResources.GameOverButton,
            AppResources.CancelButton);
        if (!confirmed) return;
        var failure = await viewModel.CompleteGameAsync();
        if (failure == Infrastructure.Data.Repositories.GameScoringLifecycleFailure.None)
            await viewModel.ExitGameAsync();
        else
            await DisplayAlert(AppResources.GameOverTitle, viewModel.ValidationMessage,
                AppResources.OkButton);
    }

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
            AppResources.OutResultLabel,
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
            var value when value == AppResources.OutResultLabel => BatterResult.Out,
            _ => (BatterResult?)null,
        };
        if (result is not null)
        {
            await viewModel.SelectResultAsync(result.Value);
            await HandlePendingInningTransitionAsync();
        }
    }

    private async void OnRunnerButtonClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
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
            var value when value == AppResources.OutDestinationLabel => RunnerDestination.Out,
            _ => (RunnerDestination?)null,
        };
        if (destination is not null)
            await viewModel.ChangeRunnerDestinationAsync(
                participant.Value.PlayerId, destination.Value);
    }

    private async void OnNextBatterClicked(object sender, EventArgs e)
    {
        if (isResolvingPlay || !viewModel.CanCompletePlateAppearance) return;
        isResolvingPlay = true;
        try
        {
            var requiredRulings = await viewModel.GetRunRulingRequirementsAsync();
            if (requiredRulings is null) return;
            var rulings = new Dictionary<int, bool>();
            foreach (var ruling in requiredRulings)
            {
                var counts = await DisplayAlert(
                    AppResources.RunRulingTitle,
                    string.Format(AppResources.RunCountQuestionFormat, ruling.PlayerName),
                    AppResources.RunCountsButton,
                    AppResources.RunDoesNotCountButton);
                rulings[ruling.PlayerId] = counts;
                if (!counts) break;
            }
            if (await viewModel.CompletePlateAppearanceAsync(rulings))
                await HandlePendingInningTransitionAsync();
        }
        finally
        {
            isResolvingPlay = false;
        }
    }

    private async Task ExitAsync()
    {
        if (isExiting) return;
        isExiting = true;
        ScoringMenu.IsVisible = false;
        await viewModel.ExitGameAsync();
    }

    private async Task HandlePendingInningTransitionAsync()
    {
        if (isHandlingInningTransition || !viewModel.IsLoaded) return;
        isHandlingInningTransition = true;
        try
        {
            while (true)
            {
                var transition = await viewModel.GetPendingHalfInningTransitionAsync();
                if (transition is null) return;
                if (!transition.TeamSummaryAcknowledged)
                {
                    await DisplayAlert(transition.SummaryTitle,
                        transition.SummaryMessage, AppResources.OkButton);
                    if (!await viewModel.AcknowledgeHalfInningSummaryAsync(transition.Id))
                        return;
                }

                var dialog = new OpponentHalfInningDialog(
                    transition.OpponentTitle,
                    entry => viewModel.CompleteOpponentHalfInningAsync(
                        transition.Id,
                        entry.Runs, entry.RunsMode,
                        entry.HomeRuns, entry.HomeRunsMode));
                await dialog.ShowAsync(Navigation);
            }
        }
        finally
        {
            isHandlingInningTransition = false;
        }
    }
}
