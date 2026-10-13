using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.Services;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ScoringPage : ContentPage
{
    private readonly ScoringViewModel viewModel;
    private bool isExiting;
    private bool isHandlingInningTransition;
    private bool isProcessingPlayResolution;
    private string? appliedLayoutState;

    public ScoringPage(ScoringViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        ResponsiveLayoutState.Current.Update(width, height);
        var layoutState = ResponsiveLayoutState.Classify(width, height) switch
        {
            ResponsiveLayoutProfile.NarrowPortrait => "CompactPortraitScoreboard",
            ResponsiveLayoutProfile.CompactPortrait => "CompactPortraitScoreboard",
            ResponsiveLayoutProfile.CompactLandscape => "CompactLandscapeScoreboard",
            ResponsiveLayoutProfile.RegularLandscape => "LandscapeScoreboard",
            _ => "PortraitScoreboard",
        };
        if (layoutState == appliedLayoutState)
            return;

        VisualStateManager.GoToState(RootGrid, layoutState);
        appliedLayoutState = layoutState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isExiting = false;
        if (isHandlingInningTransition)
            return;
        await viewModel.LoadAsync();
        await HandlePendingInningTransitionAsync();
        await ProcessCurrentPlayResolutionAsync();
        await PromptForCreatedLiveLineupPlayerAsync();
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

    private async void OnEnterOpponentHalfInningClicked(object sender, EventArgs e) =>
        await HandlePendingInningTransitionAsync();

    private async void OnGameRulesClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var dialog = new GameRulesDialog(
            false,
            viewModel.HomeRunLimit,
            viewModel.IsOneUpEnabled,
            rules =>
                viewModel.UpdateGameRulesAsync(
                    rules.HomeRunLimit,
                    rules.IsOneUpEnabled,
                    rules.TeamSide
                ),
            viewModel.CurrentTeamSide,
            viewModel.CanChangeTeamSide
        );
        var result = await dialog.ShowAsync(Navigation);
        if (result is not null)
            await HandlePendingInningTransitionAsync();
    }

    private async void OnBeerRulesClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var settings = await viewModel.LoadBeerRulesAsync();
        if (settings is null)
        {
            await DisplayAlert(
                AppResources.GameBeerRulesTitle,
                AppResources.BeerRulesLoadFailed,
                AppResources.OkButton
            );
            return;
        }
        await new BeerRulesDialog(false, settings, viewModel.SaveBeerRulesAsync).ShowAsync(
            Navigation
        );
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
                AppResources.OkButton
            );
            return;
        }

        await new InGameStatsDialog(projection.Players).ShowAsync(Navigation);
    }

    private async void OnViewLineupClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var lineup = await viewModel.GetLiveLineupPlayersAsync();
        await new ViewLineupDialog(lineup).ShowAsync(Navigation);
    }

    private async void OnManageLineupClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var action = await DisplayActionSheet(
            AppResources.ManageLiveLineupTitle,
            AppResources.CancelButton,
            null,
            AppResources.AddPlayerButton,
            AppResources.RemovePlayerButton,
            AppResources.ReplacePlayerButton,
            AppResources.AddPlayersToTeamTitle,
            AppResources.CreateNewPlayerButton
        );
        if (action == AppResources.AddPlayersToTeamTitle)
        {
            await viewModel.AddPlayersToTeamForLiveLineupAsync();
            return;
        }
        if (action == AppResources.CreateNewPlayerButton)
        {
            await viewModel.CreatePlayerForLiveLineupAsync();
            return;
        }
        if (action == AppResources.AddPlayerButton)
        {
            var replacement = await SelectEligiblePlayerAsync(
                AppResources.AddPlayerButton,
                allowCreate: true
            );
            if (replacement is not null)
                await AddPlayerAtSelectedPositionAsync(replacement);
            return;
        }

        if (
            action != AppResources.RemovePlayerButton
            && action != AppResources.ReplacePlayerButton
        )
            return;

        var active = await viewModel.GetLiveLineupPlayersAsync();
        var outgoing = await SelectPlayerAsync(
            action == AppResources.ReplacePlayerButton
                ? AppResources.ReplacePlayerButton
                : AppResources.RemovePlayerButton,
            active,
            includeBattingPosition: true
        );
        if (outgoing is null)
            return;
        if (action == AppResources.RemovePlayerButton)
        {
            await viewModel.RemoveLiveLineupPlayerAsync(outgoing.PlayerId);
            return;
        }
        if (action != AppResources.ReplacePlayerButton)
            return;
        var incoming = await SelectEligiblePlayerAsync(AppResources.SelectReplacementPlayerTitle);
        if (incoming is not null)
            await viewModel.ReplaceWithSelectedLiveLineupPlayerAsync(
                outgoing.PlayerId,
                incoming.PlayerId
            );
    }

    private async Task PromptForCreatedLiveLineupPlayerAsync()
    {
        var createdPlayerId = viewModel.TakeCreatedLiveLineupPlayerId();
        if (createdPlayerId is not > 0)
            return;

        var createdPlayer = (await viewModel.GetEligibleLiveLineupPlayersAsync()).FirstOrDefault(
            item => item.PlayerId == createdPlayerId
        );
        if (createdPlayer is not null)
            await AddPlayerAtSelectedPositionAsync(createdPlayer);
    }

    private async Task AddPlayerAtSelectedPositionAsync(LiveLineupPlayerOption player)
    {
        var lineup = await viewModel.GetLiveLineupPlayersAsync();
        var labels = lineup
            .Select(item =>
                string.Format(
                    AppResources.LiveLineupInsertBeforeFormat,
                    item.BattingPosition,
                    item.DisplayName
                )
            )
            .Append(string.Format(AppResources.LiveLineupEndPositionFormat, lineup.Count + 1))
            .ToArray();
        var selection = await DisplayActionSheet(
            string.Format(AppResources.LiveLineupInsertionTitleFormat, player.DisplayName),
            AppResources.CancelButton,
            null,
            labels
        );
        var selectedIndex = Array.IndexOf(labels, selection);
        if (selectedIndex >= 0)
            await viewModel.AddSelectedLiveLineupPlayerAsync(
                player.PlayerId,
                selectedIndex + 1
            );
    }

    private async Task<LiveLineupPlayerOption?> SelectEligiblePlayerAsync(
        string title,
        bool allowCreate = false
    )
    {
        var rostered = await viewModel.GetEligibleLiveLineupPlayersAsync();
        var allPlayers = await viewModel.GetAllEligibleLiveLineupPlayersAsync();
        if (rostered.Count == 0 && allPlayers.Count == 0 && !allowCreate)
        {
            await DisplayAlert(
                title,
                AppResources.NoEligibleLiveLineupPlayers,
                AppResources.OkButton
            );
            return null;
        }

        var selection = await new LiveLineupPlayerSelectionDialog(
            title,
            rostered,
            allPlayers,
            allowCreate
        ).ShowAsync(Navigation);
        if (selection?.CreateNewPlayer == true)
        {
            await viewModel.CreatePlayerForLiveLineupAsync();
            return null;
        }

        return selection?.Player;
    }

    private async Task<LiveLineupPlayerOption?> SelectPlayerAsync(
        string title,
        IReadOnlyList<LiveLineupPlayerOption> players,
        bool includeBattingPosition
    )
    {
        var labels = players
            .Select(item =>
                includeBattingPosition
                    ? string.Format(
                        AppResources.LiveLineupPlayerPositionFormat,
                        item.BattingPosition,
                        item.DisplayName
                    )
                    : item.DisplayName
            )
            .ToArray();
        var selection = await DisplayActionSheet(title, AppResources.CancelButton, null, labels);
        var index = Array.IndexOf(labels, selection);
        return index < 0 ? null : players[index];
    }

    private async void OnGameOverClicked(object sender, EventArgs e)
    {
        ScoringMenu.IsVisible = false;
        var confirmed = await DisplayAlert(
            AppResources.GameOverTitle,
            AppResources.GameOverConfirmationMessage,
            AppResources.GameOverButton,
            AppResources.CancelButton
        );
        if (!confirmed)
            return;
        var failure = await viewModel.CompleteGameAsync();
        if (failure == Infrastructure.Data.Repositories.GameScoringLifecycleFailure.None)
            await viewModel.ExitGameAsync();
        else
            await DisplayAlert(
                AppResources.GameOverTitle,
                viewModel.ValidationMessage,
                AppResources.OkButton
            );
    }

    private async void OnBatterClicked(object sender, EventArgs e) =>
        await ShowBatterResultMenuAsync();

    private async Task ShowBatterResultMenuAsync()
    {
        var results = viewModel.AvailableBatterResults;
        var options = results.Select(ScoringViewModel.FormatResult).ToList();
        var selection = await DisplayActionSheet(
            string.Format(AppResources.BatterResultMenuFormat, viewModel.CurrentBatterDisplayName),
            AppResources.CancelButton,
            null,
            options.ToArray()
        );
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
        if (!viewModel.HasPendingPlay || sender is not Button button)
            return;
        if (viewModel.DecisionRequirement == PlayDecisionRequirement.RetirementSelection)
        {
            await ProcessCurrentPlayResolutionAsync();
            return;
        }
        var participant = (button.CommandParameter ?? button.BindingContext) switch
        {
            BaseRunnerViewModel { IsOccupied: true, PlayerId: int id } runner => (
                id,
                runner.PlayerName
            ),
            ScoredPlayerViewModel scored => (scored.PlayerId, scored.PlayerName),
            _ => ((int PlayerId, string PlayerName)?)null,
        };
        if (participant is null)
            return;
        var outcome = await ShowRunnerDestinationMenuAsync(
            participant.Value.PlayerId,
            participant.Value.PlayerName,
            viewModel.GetParticipantRoleLabel(participant.Value.PlayerId),
            viewModel.DecisionRequirement == PlayDecisionRequirement.ParticipantOutcome
        );
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
        if (!viewModel.CanAdvanceToNextBatter)
            return;
        if (await viewModel.CompletePlateAppearanceAsync())
            await HandlePendingInningTransitionAsync();
    }

    private async Task ProcessCurrentPlayResolutionAsync()
    {
        if (isProcessingPlayResolution || !viewModel.IsLoaded)
            return;
        isProcessingPlayResolution = true;
        try
        {
            while (viewModel.HasPendingPlay)
            {
                switch (viewModel.DecisionRequirement)
                {
                    case PlayDecisionRequirement.RetirementSelection:
                        if (!await PresentRetirementSelectionAsync())
                            return;
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.ParticipantOutcome:
                        var participant = viewModel.DecisionParticipants.FirstOrDefault();
                        if (participant is null)
                            return;
                        var outcome = await ShowRunnerDestinationMenuAsync(
                            participant.PlayerId,
                            participant.PlayerName,
                            participant.RoleLabel,
                            showsGuidedResolutionContext: true
                        );
                        if (outcome is null)
                            return;
                        await viewModel.ChangeRunnerOutcomeAsync(participant.PlayerId, outcome);
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.ThirdOutRuling:
                        if (!await CompleteWithThirdOutRulingsAsync())
                            return;
                        await HandlePendingInningTransitionAsync();
                        continue;
                    case PlayDecisionRequirement.None:
                        if (
                            viewModel.ResolutionStatus
                            is PlayResolutionStatus.AutoComplete
                                or PlayResolutionStatus.CommitImmediately
                        )
                        {
                            if (!await viewModel.CompletePlateAppearanceAsync())
                                return;
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
            var dialog = new FielderChoiceThirdOutDialog(viewModel.DecisionParticipants);
            var outPlayerId = await dialog.ShowAsync(Navigation);
            retirements = outPlayerId is null
                ? null
                :
                [
                    new(
                        outPlayerId.Value,
                        RunnerOutcomeChoice.ForDestination(RunnerDestination.Out)
                    ),
                ];
        }
        else
        {
            var dialog = new MultiOutSelectionDialog(
                viewModel.DecisionParticipants,
                viewModel.RequiredRetirementCount
            );
            retirements = await dialog.ShowAsync(Navigation);
        }
        return retirements is not null
            && await viewModel.SubmitRetirementSelectionAsync(retirements);
    }

    private async Task<bool> CompleteWithThirdOutRulingsAsync()
    {
        var requiredRulings = await viewModel.GetRunRulingRequirementsAsync();
        if (requiredRulings is null)
            return false;
        var rulings = new Dictionary<int, bool>();
        foreach (var ruling in requiredRulings)
        {
            var counts = await DisplayAlert(
                AppResources.RunRulingTitle,
                string.Format(AppResources.RunCountQuestionFormat, ruling.PlayerName),
                AppResources.RunCountsButton,
                AppResources.RunDoesNotCountButton
            );
            rulings[ruling.PlayerId] = counts;
            if (!counts)
                break;
        }
        return await viewModel.CompletePlateAppearanceAsync(rulings);
    }

    private async Task<RunnerOutcomeChoice?> ShowRunnerDestinationMenuAsync(
        int playerId,
        string playerName,
        string startingLocationLabel,
        bool showsGuidedResolutionContext
    )
    {
        var menu = await viewModel.GetRunnerOutcomeMenuAsync(playerId);
        if (menu is null || menu.Choices.Count == 0)
            return null;
        var dialog = new RunnerDestinationDialog(
            playerName,
            startingLocationLabel,
            menu.CurrentOutcome,
            menu.Choices,
            showsGuidedResolutionContext
        );
        return await dialog.ShowAsync(Navigation);
    }

    private async Task ExitAsync()
    {
        if (isExiting)
            return;
        isExiting = true;
        ScoringMenu.IsVisible = false;
        await viewModel.ExitGameAsync();
    }

    private async Task HandlePendingInningTransitionAsync()
    {
        if (isHandlingInningTransition || !viewModel.IsLoaded)
            return;
        isHandlingInningTransition = true;
        try
        {
            while (true)
            {
                var transition = await viewModel.GetPendingHalfInningTransitionAsync();
                if (transition is null)
                    return;
                if (!transition.TeamSummaryAcknowledged)
                {
                    await DisplayAlert(
                        transition.SummaryTitle,
                        transition.SummaryMessage,
                        AppResources.OkButton
                    );
                    if (!await viewModel.AcknowledgeHalfInningSummaryAsync(transition.Id))
                        return;
                }

                var dialog = new OpponentHalfInningDialog(
                    transition.OpponentTitle,
                    entry =>
                        viewModel.CompleteOpponentHalfInningAsync(
                            transition.Id,
                            entry.Runs,
                            entry.RunsMode,
                            entry.HomeRuns,
                            entry.HomeRunsMode
                        )
                );
                await dialog.ShowAsync(Navigation);
            }
        }
        finally
        {
            isHandlingInningTransition = false;
        }
    }
}
