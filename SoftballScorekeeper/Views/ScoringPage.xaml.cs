using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ScoringPage : ContentPage
{
    private readonly ScoringViewModel viewModel;
    private bool isExiting;
    private bool isHandlingInningTransition;
    private bool isProcessingPlayResolution;

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
        await ProcessCurrentPlayResolutionAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = ExitAsync();
        return true;
    }

    private void OnScoringMenuClicked(object sender, EventArgs e) =>
        ScoringMenu.IsVisible = !ScoringMenu.IsVisible;

    private async void OnExitGameClicked(object sender, EventArgs e) => await ExitAsync();

    private async void OnUndoClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        await viewModel.UndoAsync();
    }

    private async void OnRedoClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        await viewModel.RedoAsync();
    }

    private async void OnGameRulesClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var dialog = new GameRulesDialog(false, viewModel.HomeRunLimit,
            viewModel.IsOneUpEnabled,
            rules => viewModel.UpdateGameRulesAsync(
                rules.HomeRunLimit, rules.IsOneUpEnabled));
        await dialog.ShowAsync(Navigation);
    }

    private async void OnStatsClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var projection = await viewModel.LoadInGameBattingStatisticsAsync();
        if (projection is null)
        {
            await DisplayAlert(
                AppResources.BattingStatisticsHeading,
                AppResources.InGameStatisticsLoadFailedMessage,
                AppResources.OkButton);
            return;
        }

        await new InGameStatsDialog(projection.Players).ShowAsync(Navigation);
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
            await ProcessCurrentPlayResolutionAsync();
        }
    }

    private async void OnRunnerButtonClicked(object sender, EventArgs e)
    {
        if (!viewModel.HasPendingPlay || sender is not Button button) return;
        if (viewModel.DecisionRequirement == PlayDecisionRequirement.RetirementSelection)
        {
            await ProcessCurrentPlayResolutionAsync();
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
        var outcome = await ShowRunnerDestinationMenuAsync(
            participant.Value.PlayerId, participant.Value.PlayerName,
            viewModel.GetParticipantRoleLabel(participant.Value.PlayerId),
            viewModel.DecisionRequirement == PlayDecisionRequirement.ParticipantOutcome);
        if (outcome is not null)
        {
            await viewModel.ChangeRunnerOutcomeAsync(
                participant.Value.PlayerId, outcome);
            await HandlePendingInningTransitionAsync();
            await ProcessCurrentPlayResolutionAsync();
        }
    }

    private async void OnNextBatterClicked(object sender, EventArgs e)
    {
        await ProcessCurrentPlayResolutionAsync();
        if (!viewModel.CanAdvanceToNextBatter) return;
        if (await viewModel.CompletePlateAppearanceAsync())
            await HandlePendingInningTransitionAsync();
    }

    private async Task ProcessCurrentPlayResolutionAsync()
    {
        if (isProcessingPlayResolution || !viewModel.IsLoaded) return;
        isProcessingPlayResolution = true;
        try
        {
            while (viewModel.HasPendingPlay)
            {
                switch (viewModel.DecisionRequirement)
                {
                    case PlayDecisionRequirement.RetirementSelection:
                        if (!await PresentRetirementSelectionAsync()) return;
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.ParticipantOutcome:
                        var participant = viewModel.DecisionParticipants.FirstOrDefault();
                        if (participant is null) return;
                        var outcome = await ShowRunnerDestinationMenuAsync(
                            participant.PlayerId,
                            participant.PlayerName,
                            participant.RoleLabel,
                            showsGuidedResolutionContext: true);
                        if (outcome is null) return;
                        await viewModel.ChangeRunnerOutcomeAsync(
                            participant.PlayerId, outcome);
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.ThirdOutRuling:
                        if (!await CompleteWithThirdOutRulingsAsync()) return;
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.None:
                        if (viewModel.ResolutionStatus is
                            PlayResolutionStatus.AutoComplete
                            or PlayResolutionStatus.CommitImmediately)
                        {
                            if (!await viewModel.CompletePlateAppearanceAsync()) return;
                            await HandlePendingInningTransitionAsync();
                            continue;
                        }
                        return;
                    default:
                        return;
                }
            }
        }
        finally
        {
            isProcessingPlayResolution = false;
        }
    }

    private async Task<bool> PresentRetirementSelectionAsync()
    {
        IReadOnlyList<MultiOutRetirementSelection>? retirements;
        if (viewModel.RequiredRetirementCount == 1)
        {
            var dialog = new FielderChoiceThirdOutDialog(
                viewModel.DecisionParticipants);
            var outPlayerId = await dialog.ShowAsync(Navigation);
            retirements = outPlayerId is null
                ? null
                : [new(outPlayerId.Value,
                    RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))];
        }
        else
        {
            var dialog = new MultiOutSelectionDialog(
                viewModel.DecisionParticipants,
                viewModel.RequiredRetirementCount);
            retirements = await dialog.ShowAsync(Navigation);
        }
        return retirements is not null
            && await viewModel.SubmitRetirementSelectionAsync(retirements);
    }

    private async Task<bool> CompleteWithThirdOutRulingsAsync()
    {
        var requiredRulings = await viewModel.GetRunRulingRequirementsAsync();
        if (requiredRulings is null) return false;
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
        return await viewModel.CompletePlateAppearanceAsync(rulings);
    }

    private async Task<RunnerOutcomeChoice?> ShowRunnerDestinationMenuAsync(
        int playerId,
        string playerName,
        string startingLocationLabel,
        bool showsGuidedResolutionContext)
    {
        var menu = await viewModel.GetRunnerOutcomeMenuAsync(playerId);
        if (menu is null || menu.Choices.Count == 0) return null;
        var dialog = new RunnerDestinationDialog(
            playerName,
            startingLocationLabel,
            menu.CurrentOutcome,
            menu.Choices,
            showsGuidedResolutionContext);
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
