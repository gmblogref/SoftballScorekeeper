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
    private bool isGuidingFielderChoice;

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
        await HandlePendingFielderChoiceResolutionAsync();
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
        var results = viewModel.AvailableBatterResults;
        var options = results.Select(ScoringViewModel.FormatResult).ToList();
        var selection = await DisplayActionSheet(
            string.Format(AppResources.BatterResultMenuFormat,
                viewModel.CurrentBatterDisplayName),
            AppResources.CancelButton,
            null,
            options.ToArray());
        var selectedIndex = options.IndexOf(selection);
        var result = selectedIndex >= 0 ? results[selectedIndex] : (BatterResult?)null;
        if (result is not null)
        {
            await viewModel.SelectResultAsync(result.Value);
            await HandlePendingInningTransitionAsync();
            if (result == BatterResult.FielderChoice)
                await HandlePendingFielderChoiceResolutionAsync();
        }
    }

    private async void OnRunnerButtonClicked(object sender, EventArgs e)
    {
        if (!viewModel.HasPendingPlay || sender is not Button button) return;
        var participant = (button.CommandParameter ?? button.BindingContext) switch
        {
            BaseRunnerViewModel { IsOccupied: true, PlayerId: int id } runner =>
                (id, runner.PlayerName),
            ScoredPlayerViewModel scored => (scored.PlayerId, scored.PlayerName),
            _ => ((int PlayerId, string PlayerName)?)null,
        };
        if (participant is null) return;
        var outcome = await ShowRunnerDestinationMenuAsync(
            participant.Value.PlayerId, participant.Value.PlayerName,
            viewModel.HasUnresolvedRunnerOutcomes);
        if (outcome is not null)
        {
            await viewModel.ChangeRunnerOutcomeAsync(
                participant.Value.PlayerId, outcome);
            await HandlePendingFielderChoiceResolutionAsync();
        }
    }

    private async void OnNextBatterClicked(object sender, EventArgs e)
    {
        if (viewModel.HasUnresolvedRunnerOutcomes)
        {
            await HandlePendingFielderChoiceResolutionAsync();
            if (viewModel.HasUnresolvedRunnerOutcomes) return;
        }
        await CompletePendingPlayAsync();
    }

    private async Task CompletePendingPlayAsync()
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

    private async Task HandlePendingFielderChoiceResolutionAsync()
    {
        if (isGuidingFielderChoice || !viewModel.IsPendingFielderChoice) return;
        isGuidingFielderChoice = true;
        try
        {
            if (viewModel.RequiresFielderChoiceThirdOutSelection)
            {
                var dialog = new FielderChoiceThirdOutDialog(
                    viewModel.UnresolvedRunnerOutcomes);
                var outPlayerId = await dialog.ShowAsync(Navigation);
                if (outPlayerId is null) return;
                if (await viewModel.CompleteFielderChoiceThirdOutAsync(outPlayerId.Value))
                    await HandlePendingInningTransitionAsync();
                return;
            }

            while (viewModel.UnresolvedRunnerOutcomes.Count > 0)
            {
                var participant = viewModel.UnresolvedRunnerOutcomes[0];
                var outcome = await ShowRunnerDestinationMenuAsync(
                    participant.PlayerId, participant.PlayerName,
                    isFielderChoiceResolution: true);
                if (outcome is null) return;
                await viewModel.ChangeRunnerOutcomeAsync(
                    participant.PlayerId, outcome);
            }
            if (viewModel.PendingFielderChoiceCompletesHalfInning)
                await CompletePendingPlayAsync();
        }
        finally
        {
            isGuidingFielderChoice = false;
        }
    }

    private async Task<RunnerOutcomeChoice?> ShowRunnerDestinationMenuAsync(
        int playerId, string playerName, bool isFielderChoiceResolution)
    {
        var outcomes = await viewModel.GetValidRunnerOutcomesAsync(playerId);
        if (outcomes.Count == 0) return null;
        var dialog = new RunnerDestinationDialog(
            playerName, outcomes, isFielderChoiceResolution);
        return await dialog.ShowAsync(Navigation);
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
