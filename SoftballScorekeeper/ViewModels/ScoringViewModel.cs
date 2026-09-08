using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class ScoringViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly GameRepository gameRepository;
    private readonly GameScoringSessionRepository sessionRepository;
    private readonly BeerRuleConfigurationRepository? beerRuleConfigurationRepository;
    private bool hasValidContext;
    private int currentBatterPlayerId;
    private TeamGameSide currentTeamSide;
    private IReadOnlyDictionary<int, string> participantRoleLabels =
        new Dictionary<int, string>();

    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private string homeTeamDisplayName = string.Empty;
    [ObservableProperty] private string visitorTeamDisplayName = string.Empty;
    [ObservableProperty] private int homeScore;
    [ObservableProperty] private int visitorScore;
    private string homeHomeRunDisplay = string.Empty;
    private string visitorHomeRunDisplay = string.Empty;
    private bool isOneUpEnabled;
    private int homeRunLimit = GameScoringSession.DefaultHomeRunLimit;
    [ObservableProperty] private string currentBatterDisplayName = string.Empty;
    private string onDeckDisplayName = string.Empty;
    private string inTheHoleDisplayName = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private string selectedResultDisplay = string.Empty;
    [ObservableProperty] private string runsOnPlayDisplay = string.Empty;
    private string inningOutDisplay = string.Empty;
    private string inningDisplay = string.Empty;
    private string outsDisplay = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAwaitingResult)),
        NotifyPropertyChangedFor(nameof(CanAdvanceToNextBatter)),
        NotifyPropertyChangedFor(nameof(CanEditParticipantOutcomes))]
    private bool hasPendingPlay;
    private bool isPendingBatterResolved;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanAdvanceToNextBatter))]
    private bool isLoaded;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanAdvanceToNextBatter))]
    private bool isCompletingPlateAppearance;
    private bool canUndo;
    private bool canRedo;
    private PlayResolutionStatus resolutionStatus = PlayResolutionStatus.Completed;
    private PlayDecisionRequirement decisionRequirement = PlayDecisionRequirement.None;
    private int requiredRetirementCount;
    private ThirdOutRunRulingRequirement thirdOutRulingRequirement =
        ThirdOutRunRulingRequirement.NotApplicable;

    public ObservableCollection<ScoredPlayerViewModel> ScoredPlayers { get; } = [];
    public ObservableCollection<OutPlayerViewModel> OutsThisInning { get; } = [];
    public IReadOnlyList<BatterResult> AvailableBatterResults { get; private set; } =
        PlateAppearanceResultDefinitions.GetSelectableResults(0);
    public IReadOnlyList<RunnerOutcomeViewModel> DecisionParticipants { get; private set; } = [];

    public Task<GameBattingStatisticsProjection?> LoadInGameBattingStatisticsAsync() =>
        sessionRepository.GetInProgressBattingStatisticsAsync(GameId);

    public string HomeHomeRunDisplay
    {
        get => homeHomeRunDisplay;
        private set => SetProperty(ref homeHomeRunDisplay, value);
    }

    public string VisitorHomeRunDisplay
    {
        get => visitorHomeRunDisplay;
        private set => SetProperty(ref visitorHomeRunDisplay, value);
    }

    public bool IsOneUpEnabled
    {
        get => isOneUpEnabled;
        private set => SetProperty(ref isOneUpEnabled, value);
    }

    public int HomeRunLimit
    {
        get => homeRunLimit;
        private set => SetProperty(ref homeRunLimit, value);
    }

    public string OnDeckDisplayName
    {
        get => onDeckDisplayName;
        private set => SetProperty(ref onDeckDisplayName, value);
    }

    public string InTheHoleDisplayName
    {
        get => inTheHoleDisplayName;
        private set => SetProperty(ref inTheHoleDisplayName, value);
    }

    public string InningOutDisplay
    {
        get => inningOutDisplay;
        private set => SetProperty(ref inningOutDisplay, value);
    }

    public string InningDisplay
    {
        get => inningDisplay;
        private set => SetProperty(ref inningDisplay, value);
    }

    public string OutsDisplay
    {
        get => outsDisplay;
        private set => SetProperty(ref outsDisplay, value);
    }

    public BaseRunnerViewModel FirstBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.FirstBaseHeadingLabel);
    public BaseRunnerViewModel SecondBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.SecondBaseHeadingLabel);
    public BaseRunnerViewModel ThirdBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.ThirdBaseHeadingLabel);
    public bool HasScoredPlayers => ScoredPlayers.Count > 0;
    public bool HasNoScoredPlayers => !HasScoredPlayers;
    public PlayResolutionStatus ResolutionStatus
    {
        get => resolutionStatus;
        private set
        {
            if (!SetProperty(ref resolutionStatus, value)) return;
            OnPropertyChanged(nameof(CanAdvanceToNextBatter));
            OnPropertyChanged(nameof(CanEditParticipantOutcomes));
        }
    }

    public PlayDecisionRequirement DecisionRequirement
    {
        get => decisionRequirement;
        private set
        {
            if (!SetProperty(ref decisionRequirement, value)) return;
            OnPropertyChanged(nameof(CanAdvanceToNextBatter));
            OnPropertyChanged(nameof(CanEditParticipantOutcomes));
        }
    }

    public int RequiredRetirementCount
    {
        get => requiredRetirementCount;
        private set => SetProperty(ref requiredRetirementCount, value);
    }

    public ThirdOutRunRulingRequirement ThirdOutRulingRequirement
    {
        get => thirdOutRulingRequirement;
        private set => SetProperty(ref thirdOutRulingRequirement, value);
    }

    public IReadOnlyList<int> DecisionPlayerIds =>
        DecisionParticipants.Select(item => item.PlayerId).ToArray();
    public bool IsAwaitingResult => !HasPendingPlay;
    public bool IsCurrentBatterVisible => !isPendingBatterResolved;
    public bool CanAdvanceToNextBatter =>
        hasValidContext && IsLoaded && HasPendingPlay
        && ResolutionStatus == PlayResolutionStatus.ReadyForNextBatter
        && DecisionRequirement == PlayDecisionRequirement.None
        && !IsCompletingPlateAppearance;
    public bool CanEditParticipantOutcomes => HasPendingPlay
        && DecisionRequirement != PlayDecisionRequirement.RetirementSelection;
    public int CurrentBatterPlayerId => currentBatterPlayerId;

    public string GetParticipantRoleLabel(int playerId) =>
        participantRoleLabels.GetValueOrDefault(playerId, string.Empty);

    public ScoringViewModel(TeamRepository teamRepository, GameRepository gameRepository,
        GameScoringSessionRepository sessionRepository,
        BeerRuleConfigurationRepository? beerRuleConfigurationRepository = null)
    {
        this.teamRepository = teamRepository;
        this.gameRepository = gameRepository;
        this.sessionRepository = sessionRepository;
        this.beerRuleConfigurationRepository = beerRuleConfigurationRepository;
    }

    public async Task<IReadOnlyList<BeerRuleSetting>?> LoadBeerRulesAsync()
    {
        if (!IsLoaded || beerRuleConfigurationRepository is null) return null;
        return await beerRuleConfigurationRepository.GetGameAsync(GameId);
    }

    public async Task<bool> SaveBeerRulesAsync(IReadOnlyList<BeerRuleSetting> settings)
    {
        if (!IsLoaded || IsCompletingPlateAppearance
            || beerRuleConfigurationRepository is null)
            return false;
        IsCompletingPlateAppearance = true;
        try
        {
            if (await beerRuleConfigurationRepository.SaveGameAsync(GameId, settings))
                return true;
            ValidationMessage = AppResources.BeerRulesSaveFailed;
            return false;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var hasTeamId = query.ContainsKey(AppShell.TeamIdQueryParameter);
        var hasGameId = query.ContainsKey(AppShell.GameIdQueryParameter);
        if (!hasTeamId && !hasGameId) return;

        var hasValidTeamId = TryParseId(
            query, AppShell.TeamIdQueryParameter, out var teamId);
        var hasValidGameId = TryParseId(
            query, AppShell.GameIdQueryParameter, out var gameId);
        hasValidContext = hasValidTeamId && hasValidGameId;
        TeamId = hasValidContext ? teamId : 0;
        GameId = hasValidContext ? gameId : 0;
    }

    public async Task LoadAsync()
    {
        IsLoaded = false;
        ResetPresentation();
        if (!hasValidContext)
        {
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        var team = await teamRepository.GetActiveByIdAsync(TeamId);
        var game = await gameRepository.GetByIdForTeamAsync(TeamId, GameId);
        var state = await sessionRepository.GetStateForGameAsync(GameId);
        if (team is null || game is null || state is null)
        {
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        var opponentDisplay = string.IsNullOrWhiteSpace(game.Opponent)
            ? AppResources.ScheduleTbdText : game.Opponent.Trim();
        if (state.Session.TeamSide == Core.Models.TeamGameSide.Home)
        {
            HomeTeamDisplayName = team.Name;
            VisitorTeamDisplayName = opponentDisplay;
        }
        else if (state.Session.TeamSide == Core.Models.TeamGameSide.Away)
        {
            HomeTeamDisplayName = opponentDisplay;
            VisitorTeamDisplayName = team.Name;
        }
        else
        {
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        var currentSequencePlayer = state.BattingSequence.FirstOrDefault(item =>
            item.BattingPosition == state.Session.CurrentBatterPosition);
        if (currentSequencePlayer is null
            || !state.PlayersById.TryGetValue(currentSequencePlayer.PlayerId, out var currentPlayer)
            || state.Session.CurrentInning < 1
            || state.Session.CurrentOuts is < 0 or > 2)
        {
            ResetPresentation();
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        HomeScore = state.Session.HomeScore;
        VisitorScore = state.Session.VisitorScore;
        HomeRunLimit = state.Session.HomeRunLimit;
        IsOneUpEnabled = state.Session.IsOneUpEnabled;
        HomeHomeRunDisplay = FormatHomeRunScoreboard(
            state.HomeHomeRuns, HomeRunLimit, IsOneUpEnabled);
        VisitorHomeRunDisplay = FormatHomeRunScoreboard(
            state.VisitorHomeRuns, HomeRunLimit, IsOneUpEnabled);
        currentTeamSide = state.Session.TeamSide;
        InningDisplay = FormatInning(state.Session);
        OutsDisplay = FormatOuts(state.Session);
        InningOutDisplay = FormatInningOut(state.Session);
        CurrentBatterDisplayName = FormatPlayerName(currentPlayer.FirstName, currentPlayer.LastName);
        ResolveUpcomingBatters(state);
        RestoreOutsThisInning(state);
        currentBatterPlayerId = currentPlayer.Id;
        AvailableBatterResults = PlateAppearanceResultDefinitions
            .GetSelectableResults(
                state.BaseRunners.Select(item =>
                    (RunnerStartingLocation)item.Base).ToHashSet(),
                state.Session.CurrentOuts,
                state.IsHomeRunLegal());
        OnPropertyChanged(nameof(AvailableBatterResults));
        RestoreBaseRunners(state);
        RestorePendingPlay(state);
        var historyAvailability = await sessionRepository
            .GetUndoRedoAvailabilityAsync(GameId);
        CanUndo = historyAvailability.CanUndo;
        CanRedo = historyAvailability.CanRedo;
        ValidationMessage = string.Empty;
        IsLoaded = true;
    }

    [RelayCommand]
    public Task ExitGameAsync() => Shell.Current.GoToAsync("..");

    public Task<bool> UndoAsync() => RestoreScoringHistoryAsync(isUndo: true);

    public Task<bool> RedoAsync() => RestoreScoringHistoryAsync(isUndo: false);

    private async Task<bool> RestoreScoringHistoryAsync(bool isUndo)
    {
        if (!IsLoaded || IsCompletingPlateAppearance
            || isUndo && !CanUndo || !isUndo && !CanRedo)
            return false;
        IsCompletingPlateAppearance = true;
        try
        {
            var result = isUndo
                ? await sessionRepository.UndoAsync(GameId)
                : await sessionRepository.RedoAsync(GameId);
            await LoadAsync();
            if (result.IsSuccess) return true;
            ValidationMessage = AppResources.UndoRedoFailedMessage;
            return false;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public bool CanUndo
    {
        get => canUndo;
        private set => SetProperty(ref canUndo, value);
    }

    public bool CanRedo
    {
        get => canRedo;
        private set => SetProperty(ref canRedo, value);
    }

    public async Task<bool> UpdateGameRulesAsync(int homeRunLimit, bool isOneUpEnabled)
    {
        if (!IsLoaded || IsCompletingPlateAppearance || homeRunLimit < 0) return false;
        IsCompletingPlateAppearance = true;
        try
        {
            var result = await sessionRepository.UpdateGameRulesAsync(
                GameId, homeRunLimit, isOneUpEnabled);
            if (!result.IsSuccess)
            {
                ValidationMessage = AppResources.GameRulesSaveFailed;
                return false;
            }
            await LoadAsync();
            return true;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public async Task<GameScoringLifecycleFailure> CompleteGameAsync()
    {
        if (!IsLoaded || IsCompletingPlateAppearance)
            return GameScoringLifecycleFailure.InvalidRequest;
        IsCompletingPlateAppearance = true;
        try
        {
            var result = await sessionRepository.CompleteGameAsync(GameId);
            if (!result.IsSuccess)
                ValidationMessage = result.FailureReason ==
                    GameScoringLifecycleFailure.UnresolvedScoringState
                    ? AppResources.GameOverBlockedMessage
                    : AppResources.GameOverFailedMessage;
            return result.FailureReason;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public async Task<bool> CompletePlateAppearanceAsync(
        IReadOnlyDictionary<int, bool>? runRulings = null)
    {
        if (!CanCommitCurrentPlay()) return false;

        IsCompletingPlateAppearance = true;
        try
        {
            var result = await sessionRepository.CompletePlateAppearanceAsync(
                GameId, runRulings);
            if (!result.IsSuccess)
            {
                await LoadAsync();
                ValidationMessage = AppResources.PlateAppearanceCompletionFailed;
                return false;
            }

            await LoadAsync();
            return true;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public async Task<bool> SubmitRetirementSelectionAsync(
        IReadOnlyList<MultiOutRetirementSelection> retirements)
    {
        if (DecisionRequirement != PlayDecisionRequirement.RetirementSelection
            || IsCompletingPlateAppearance
            || retirements.Count != RequiredRetirementCount
            || retirements.Select(item => item.PlayerId).Distinct().Count()
                != RequiredRetirementCount
            || retirements.Any(item => DecisionParticipants.All(participant =>
                participant.PlayerId != item.PlayerId)))
            return false;

        IsCompletingPlateAppearance = true;
        try
        {
            var succeeded = (await sessionRepository.SelectMultiOutRetirementsAsync(
                GameId, retirements)).IsSuccess;
            if (!succeeded)
            {
                await LoadAsync();
                ValidationMessage = AppResources.ScoringProposalFailed;
                return false;
            }

            await LoadAsync();
            return true;
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    public async Task<IReadOnlyList<RunRulingViewModel>?> GetRunRulingRequirementsAsync()
    {
        if (DecisionRequirement != PlayDecisionRequirement.ThirdOutRuling) return [];
        var result = await sessionRepository.GetRunRulingRequirementsAsync(GameId);
        if (!result.IsSuccess)
        {
            ValidationMessage = AppResources.ScoringProposalFailed;
            return null;
        }
        if (result.PlayerIds.Count == 0) return [];
        var rulings = new List<RunRulingViewModel>();
        foreach (var playerId in result.PlayerIds)
        {
            var participant = DecisionParticipants.FirstOrDefault(item =>
                item.PlayerId == playerId);
            if (participant is null) return null;
            rulings.Add(new(playerId, participant.PlayerName));
        }
        return rulings;
    }

    [RelayCommand]
    public async Task SelectResultAsync(BatterResult result)
    {
        if (!IsLoaded || IsCompletingPlateAppearance) return;
        if (result is BatterResult.HomeRun or BatterResult.Out)
        {
            IsCompletingPlateAppearance = true;
            try
            {
                var succeeded = result == BatterResult.HomeRun
                    ? (await sessionRepository.CompleteHomeRunAsync(GameId)).IsSuccess
                    : (await sessionRepository.SelectOutAsync(GameId)).IsSuccess;
                await LoadAsync();
                if (!succeeded)
                    ValidationMessage = AppResources.PlateAppearanceCompletionFailed;
            }
            finally
            {
                IsCompletingPlateAppearance = false;
            }
            return;
        }
        var response = await sessionRepository.SelectBatterResultAsync(GameId, result);
        if (!response.IsSuccess)
        {
            ValidationMessage = AppResources.ScoringProposalFailed;
            return;
        }
        await LoadAsync();
    }

    public async Task<HalfInningTransitionViewModel?> GetPendingHalfInningTransitionAsync()
    {
        if (!hasValidContext) return null;
        var halfInning = await sessionRepository
            .GetPendingHalfInningTransitionAsync(GameId);
        if (halfInning is null) return null;
        var teamHalf = currentTeamSide == TeamGameSide.Away
            ? AppResources.TopInningHalfLabel
            : AppResources.BottomInningHalfLabel;
        var opponentHalf = currentTeamSide == TeamGameSide.Away
            ? AppResources.BottomInningHalfLabel
            : AppResources.TopInningHalfLabel;
        var opponentInning = currentTeamSide == TeamGameSide.Away
            ? halfInning.Inning
            : halfInning.Inning + 1;
        return new(
            halfInning.Id,
            halfInning.TeamSummaryAcknowledged,
            string.Format(AppResources.HalfInningCompleteTitleFormat,
                teamHalf, halfInning.Inning),
            string.Format(AppResources.HalfInningSummaryMessageFormat,
                halfInning.TeamRuns, halfInning.TeamHomeRuns),
            string.Format(AppResources.OpponentHalfInningTitleFormat,
                opponentHalf, opponentInning));
    }

    public async Task<bool> AcknowledgeHalfInningSummaryAsync(int halfInningId)
    {
        var succeeded = await sessionRepository
            .AcknowledgeHalfInningSummaryAsync(GameId, halfInningId);
        if (!succeeded)
            ValidationMessage = AppResources.HalfInningTransitionFailed;
        return succeeded;
    }

    public async Task<string?> CompleteOpponentHalfInningAsync(
        int halfInningId,
        int? runs, OpponentEntryMode runsMode,
        int? homeRuns, OpponentEntryMode homeRunsMode)
    {
        var result = await sessionRepository.CompleteOpponentHalfInningAsync(
            GameId, halfInningId, runs, runsMode, homeRuns, homeRunsMode);
        await LoadAsync();
        if (result.IsSuccess) return null;
        ValidationMessage = result.FailureReason switch
        {
            OpponentHalfInningEntryFailure.TotalRunsBelowCurrent =>
                AppResources.OpponentTotalScoreTooLowMessage,
            OpponentHalfInningEntryFailure.TotalHomeRunsBelowCurrent =>
                AppResources.OpponentTotalHomeRunsTooLowMessage,
            _ => AppResources.HalfInningTransitionFailed,
        };
        return ValidationMessage;
    }

    public async Task ChangeRunnerDestinationAsync(int playerId, RunnerDestination destination)
        => await ChangeRunnerOutcomeAsync(
            playerId, RunnerOutcomeChoice.ForDestination(destination));

    public async Task ChangeRunnerOutcomeAsync(int playerId, RunnerOutcomeChoice outcome)
    {
        var response = await sessionRepository.AdjustPendingRunnerOutcomeAsync(
            GameId, playerId, outcome);
        if (!response.IsSuccess)
        {
            var message = response.FailureReason == PendingPlayFailure.DuplicateDestination
                ? AppResources.DuplicateBaseDestinationMessage
                : AppResources.ScoringProposalFailed;
            await LoadAsync();
            ValidationMessage = message;
            return;
        }
        await LoadAsync();
    }

    public async Task<IReadOnlyList<RunnerDestination>> GetValidRunnerDestinationsAsync(
        int playerId)
    {
        var result = await sessionRepository.GetValidRunnerDestinationsAsync(GameId, playerId);
        if (result.IsSuccess) return result.Destinations;
        ValidationMessage = AppResources.ScoringProposalFailed;
        return [];
    }

    public async Task<RunnerOutcomeMenuViewModel?> GetRunnerOutcomeMenuAsync(
        int playerId)
    {
        var result = await sessionRepository.GetValidRunnerOutcomeChoicesAsync(
            GameId, playerId);
        if (result is { IsSuccess: true, CurrentOutcome: not null })
            return new(result.CurrentOutcome, result.Choices);
        ValidationMessage = AppResources.ScoringProposalFailed;
        return null;
    }

    private void ResetPresentation()
    {
        HomeTeamDisplayName = string.Empty;
        VisitorTeamDisplayName = string.Empty;
        HomeScore = 0;
        VisitorScore = 0;
        HomeHomeRunDisplay = VisitorHomeRunDisplay = string.Empty;
        HomeRunLimit = GameScoringSession.DefaultHomeRunLimit;
        IsOneUpEnabled = false;
        CurrentBatterDisplayName = string.Empty;
        OnDeckDisplayName = InTheHoleDisplayName = string.Empty;
        SelectedResultDisplay = string.Empty;
        RunsOnPlayDisplay = string.Empty;
        InningOutDisplay = string.Empty;
        InningDisplay = OutsDisplay = string.Empty;
        HasPendingPlay = false;
        CanUndo = false;
        CanRedo = false;
        SetPlayResolution(null, new Dictionary<int, Player>());
        SetPendingBatterResolved(false);
        FirstBaseRunner = EmptyBaseRunner(AppResources.FirstBaseHeadingLabel);
        SecondBaseRunner = EmptyBaseRunner(AppResources.SecondBaseHeadingLabel);
        ThirdBaseRunner = EmptyBaseRunner(AppResources.ThirdBaseHeadingLabel);
        OnPropertyChanged(nameof(FirstBaseRunner));
        OnPropertyChanged(nameof(SecondBaseRunner));
        OnPropertyChanged(nameof(ThirdBaseRunner));
        ScoredPlayers.Clear();
        OutsThisInning.Clear();
        OnPropertyChanged(nameof(HasScoredPlayers));
        OnPropertyChanged(nameof(HasNoScoredPlayers));
        AvailableBatterResults = PlateAppearanceResultDefinitions.GetSelectableResults(0);
        OnPropertyChanged(nameof(AvailableBatterResults));
        currentBatterPlayerId = 0;
        currentTeamSide = default;
    }

    private void ResolveUpcomingBatters(GameScoringState state)
    {
        var upcoming = state.GetUpcomingPlayerIds();
        if (upcoming is null) return;
        OnDeckDisplayName = ResolvePlayerName(state, upcoming.Value.OnDeckPlayerId);
        InTheHoleDisplayName = ResolvePlayerName(state, upcoming.Value.InTheHolePlayerId);
    }

    private void RestoreOutsThisInning(GameScoringState state)
    {
        OutsThisInning.Clear();
        foreach (var playerId in state.GetCurrentHalfInningOutPlayerIds())
        {
            if (!state.PlayersById.TryGetValue(playerId, out var player)) continue;
            OutsThisInning.Add(new(OutsThisInning.Count + 1,
                FormatPlayerName(player.FirstName, player.LastName)));
        }
    }

    private static string ResolvePlayerName(GameScoringState state, int playerId) =>
        state.PlayersById.TryGetValue(playerId, out var player)
            ? FormatPlayerName(player.FirstName, player.LastName)
            : string.Empty;

    private static string FormatPlayerName(string firstName, string? lastName) =>
        string.Join(" ", new[] { firstName, lastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim()));

    private void RestoreBaseRunners(GameScoringState state)
    {
        foreach (var scoringBase in Enum.GetValues<ScoringBase>())
        {
            var playerId = ResolveDisplayedBaseRunner(state, scoringBase);
            var player = playerId is int id
                && state.PlayersById.TryGetValue(id, out var resolvedPlayer)
                    ? resolvedPlayer
                    : null;
            var name = player is not null
                ? FormatPlayerName(player.FirstName, player.LastName)
                : string.Empty;
            var isCurrentBatter = playerId == currentBatterPlayerId;
            var baseRunner = new BaseRunnerViewModel(FormatBase(scoringBase), name,
                player is not null ? playerId : null,
                isCurrentBatter ? AppResources.BatterBadgeLabel : string.Empty,
                isCurrentBatter);
            switch (scoringBase)
            {
                case ScoringBase.First:
                    FirstBaseRunner = baseRunner;
                    OnPropertyChanged(nameof(FirstBaseRunner));
                    break;
                case ScoringBase.Second:
                    SecondBaseRunner = baseRunner;
                    OnPropertyChanged(nameof(SecondBaseRunner));
                    break;
                case ScoringBase.Third:
                    ThirdBaseRunner = baseRunner;
                    OnPropertyChanged(nameof(ThirdBaseRunner));
                    break;
            }
        }
    }

    private static int? ResolveDisplayedBaseRunner(
        GameScoringState state, ScoringBase scoringBase)
    {
        if (state.PendingPlay is null)
            return state.BaseRunners.FirstOrDefault(item =>
                item.Base == scoringBase)?.PlayerId;

        var confirmed = state.PendingPlay.Movements.FirstOrDefault(item =>
            !item.RequiresOutcomeConfirmation
            && item.Destination == (RunnerDestination)scoringBase);
        if (confirmed is not null) return confirmed.PlayerId;

        return state.PendingPlay.Movements.FirstOrDefault(item =>
            item.RequiresOutcomeConfirmation
            && item.StartingLocation == (RunnerStartingLocation)scoringBase)?.PlayerId;
    }

    private void RestorePendingPlay(GameScoringState state)
    {
        ScoredPlayers.Clear();
        HasPendingPlay = state.PendingPlay is not null;
        SetPlayResolution(state.PlayResolution, state.PlayersById);
        if (state.PendingPlay is null) return;
        SetPendingBatterResolved(state.IsPendingBatterResolved());
        SelectedResultDisplay = string.Format(AppResources.SelectedResultFormat,
            FormatResult(state.PendingPlay.Play.Result));
        var runs = state.PendingPlay.Movements.Count(item =>
            !item.RequiresOutcomeConfirmation
            && item.Destination == RunnerDestination.Scores);
        RunsOnPlayDisplay = string.Format(AppResources.RunsOnPlayFormat, runs);
        foreach (var movement in state.PendingPlay.Movements.Where(item =>
            !item.RequiresOutcomeConfirmation
            && item.Destination == RunnerDestination.Scores))
        {
            if (!state.PlayersById.TryGetValue(movement.PlayerId, out var player)) continue;
            ScoredPlayers.Add(new(
                movement.PlayerId,
                movement.StartingLocation == RunnerStartingLocation.Home
                    ? AppResources.BatterBadgeLabel : AppResources.RunnerBadgeLabel,
                FormatPlayerName(player.FirstName, player.LastName),
                movement.StartingLocation));
        }
        OnPropertyChanged(nameof(HasScoredPlayers));
        OnPropertyChanged(nameof(HasNoScoredPlayers));
        RestoreBaseRunners(state);
    }

    public static string FormatResult(BatterResult result) => result switch
    {
        BatterResult.Single => AppResources.SingleResultLabel,
        BatterResult.Double => AppResources.DoubleResultLabel,
        BatterResult.Triple => AppResources.TripleResultLabel,
        BatterResult.HomeRun => AppResources.HomeRunResultLabel,
        BatterResult.DeadBallOut => AppResources.DeadBallOutResultLabel,
        BatterResult.Walk => AppResources.WalkResultLabel,
        BatterResult.FielderChoice => AppResources.FielderChoiceResultLabel,
        BatterResult.DoublePlay => AppResources.DoublePlayResultLabel,
        BatterResult.TriplePlay => AppResources.TriplePlayResultLabel,
        BatterResult.SacrificeFly => AppResources.SacrificeFlyResultLabel,
        BatterResult.Strikeout => AppResources.StrikeoutResultLabel,
        BatterResult.Out => AppResources.OutResultLabel,
        _ => string.Empty,
    };

    private static string FormatHomeRunScoreboard(
        int currentHomeRuns,
        int homeRunLimit,
        bool isOneUpEnabled) => isOneUpEnabled
        ? string.Format(AppResources.HomeRunScoreboardCurrentFormat, currentHomeRuns)
        : string.Format(AppResources.HomeRunScoreboardFormat,
            currentHomeRuns, homeRunLimit);

    private static string FormatInningOut(GameScoringSession session)
    {
        return string.Format(AppResources.InningOutDisplayFormat,
            FormatInningHalf(session), session.CurrentInning, FormatOuts(session));
    }

    private static string FormatInning(GameScoringSession session) =>
        string.Format(AppResources.InningDisplayFormat,
            FormatInningHalf(session), session.CurrentInning);

    private static string FormatInningHalf(GameScoringSession session) =>
        session.TeamSide == TeamGameSide.Away
            ? AppResources.TopInningHalfLabel
            : AppResources.BottomInningHalfLabel;

    private static string FormatOuts(GameScoringSession session) =>
        string.Format(session.CurrentOuts == 1
            ? AppResources.SingleOutFormat
            : AppResources.MultipleOutsFormat, session.CurrentOuts);

    public static string FormatDestination(RunnerDestination destination) => destination switch
    {
        RunnerDestination.First => AppResources.FirstBaseLabel,
        RunnerDestination.Second => AppResources.SecondBaseLabel,
        RunnerDestination.Third => AppResources.ThirdBaseLabel,
        RunnerDestination.Scores => AppResources.ScoresDestinationLabel,
        RunnerDestination.Out => AppResources.OutDestinationLabel,
        _ => string.Empty,
    };

    public static string FormatRunnerOutcome(RunnerOutcomeChoice outcome)
    {
        if (!outcome.IsOutAttemptingAdvance)
            return FormatDestination(outcome.Destination);
        return outcome.AttemptedDestination switch
        {
            RunnerDestination.Second => AppResources.OutAttemptingSecondDestinationLabel,
            RunnerDestination.Third => AppResources.OutAttemptingThirdDestinationLabel,
            RunnerDestination.Scores => AppResources.OutAttemptingHomeDestinationLabel,
            _ => string.Empty,
        };
    }

    private static string FormatStartingLocation(RunnerStartingLocation startingLocation) =>
        startingLocation switch
        {
            RunnerStartingLocation.First => AppResources.RunnerOnFirstRoleLabel,
            RunnerStartingLocation.Second => AppResources.RunnerOnSecondRoleLabel,
            RunnerStartingLocation.Third => AppResources.RunnerOnThirdRoleLabel,
            RunnerStartingLocation.Home => AppResources.BatterBadgeLabel,
            _ => string.Empty,
        };

    private static string FormatBase(ScoringBase scoringBase) => scoringBase switch
    {
        ScoringBase.First => AppResources.FirstBaseHeadingLabel,
        ScoringBase.Second => AppResources.SecondBaseHeadingLabel,
        ScoringBase.Third => AppResources.ThirdBaseHeadingLabel,
        _ => string.Empty,
    };

    private static BaseRunnerViewModel EmptyBaseRunner(string baseLabel) =>
        new(baseLabel, string.Empty, null, string.Empty, false);

    private void SetPendingBatterResolved(bool value)
    {
        if (SetProperty(ref isPendingBatterResolved, value))
            OnPropertyChanged(nameof(IsCurrentBatterVisible));
    }

    private void SetPlayResolution(
        PlayResolutionResult? resolution,
        IReadOnlyDictionary<int, Player> playersById)
    {
        ResolutionStatus = resolution?.Status ?? PlayResolutionStatus.Completed;
        DecisionRequirement = resolution?.DecisionRequirement
            ?? PlayDecisionRequirement.None;
        RequiredRetirementCount = resolution?.RequiredRetirementCount ?? 0;
        ThirdOutRulingRequirement = resolution?.ThirdOutRulingRequirement
            ?? ThirdOutRunRulingRequirement.NotApplicable;
        participantRoleLabels = resolution?.Context?.Participants.ToDictionary(
            item => item.PlayerId,
            item => FormatStartingLocation(item.StartingLocation))
            ?? new Dictionary<int, string>();
        DecisionParticipants = resolution?.DecisionPlayerIds
            .Select(playerId =>
            {
                var participant = resolution.Context?.Participants.FirstOrDefault(item =>
                    item.PlayerId == playerId);
                return participant is not null
                    && playersById.TryGetValue(playerId, out var player)
                        ? new RunnerOutcomeViewModel(
                            playerId,
                            FormatPlayerName(player.FirstName, player.LastName),
                            participant.StartingLocation,
                            FormatStartingLocation(participant.StartingLocation))
                        : null;
            })
            .Where(item => item is not null)
            .Cast<RunnerOutcomeViewModel>()
            .ToArray() ?? [];
        OnPropertyChanged(nameof(DecisionParticipants));
        OnPropertyChanged(nameof(DecisionPlayerIds));
    }

    private bool CanCommitCurrentPlay() =>
        hasValidContext && IsLoaded && HasPendingPlay && !IsCompletingPlateAppearance
        && (DecisionRequirement == PlayDecisionRequirement.None
            && ResolutionStatus is PlayResolutionStatus.ReadyForNextBatter
                or PlayResolutionStatus.AutoComplete
                or PlayResolutionStatus.CommitImmediately
            || DecisionRequirement == PlayDecisionRequirement.ThirdOutRuling);

    private static bool TryParseId(IDictionary<string, object> query, string key, out int id)
    {
        id = 0;
        return query.TryGetValue(key, out var value)
            && (value is int number ? (id = number) > 0
                : value is string text && int.TryParse(text, out id) && id > 0);
    }
}

public sealed record BaseRunnerViewModel(
    string BaseLabel, string PlayerName, int? PlayerId, string RoleLabel, bool IsCurrentBatter)
{
    public bool IsOccupied => PlayerId.HasValue;
    public string BackgroundColor => IsOccupied ? "#4DFFD54F" : "White";
    public string BorderColor => IsOccupied ? "#B8860B" : "#919191";
}

public sealed record ScoredPlayerViewModel(
    int PlayerId,
    string RoleLabel,
    string PlayerName,
    RunnerStartingLocation StartingLocation)
{
    public string DisplayText => string.Format(AppResources.ScoredPlayerCardFormat,
        RoleLabel, PlayerName);
    public string AutomationId => StartingLocation switch
    {
        RunnerStartingLocation.Home => "ScoredBatterButton",
        RunnerStartingLocation.First => "ScoredRunnerFromFirstButton",
        RunnerStartingLocation.Second => "ScoredRunnerFromSecondButton",
        RunnerStartingLocation.Third => "ScoredRunnerFromThirdButton",
        _ => "ScoredRunnerButton",
    };
}

public sealed record RunnerOutcomeMenuViewModel(
    RunnerOutcomeChoice CurrentOutcome,
    IReadOnlyList<RunnerOutcomeChoice> Choices);

public sealed record OutPlayerViewModel(int OutNumber, string PlayerName)
{
    public string DisplayText => string.Format(
        AppResources.OutPlayerDisplayFormat, OutNumber, PlayerName);
}

public sealed record RunnerOutcomeViewModel(
    int PlayerId, string PlayerName, RunnerStartingLocation StartingLocation, string RoleLabel)
{
    public string RetirementAutomationId => StartingLocation switch
    {
        RunnerStartingLocation.Home => "RetireBatterButton",
        RunnerStartingLocation.First => "RetireRunnerFromFirstButton",
        RunnerStartingLocation.Second => "RetireRunnerFromSecondButton",
        RunnerStartingLocation.Third => "RetireRunnerFromThirdButton",
        _ => "RetireParticipantButton",
    };
}

public sealed record HalfInningTransitionViewModel(
    int Id,
    bool TeamSummaryAcknowledged,
    string SummaryTitle,
    string SummaryMessage,
    string OpponentTitle);

public sealed record RunRulingViewModel(int PlayerId, string PlayerName);
