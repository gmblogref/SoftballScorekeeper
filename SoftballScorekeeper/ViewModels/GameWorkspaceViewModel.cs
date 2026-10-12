using System.Globalization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class GameWorkspaceViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly SeasonRepository seasonRepository;
    private readonly GameRepository gameRepository;
    private readonly GameLineupRepository lineupRepository;
    private readonly GameScoringSessionRepository scoringSessionRepository;
    private readonly CompletedGameManualOverrideRepository manualOverrideRepository;
    private bool hasValidContext;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private string teamName = string.Empty;
    [ObservableProperty] private string opponentDisplay = string.Empty;
    [ObservableProperty] private string dateDisplay = string.Empty;
    [ObservableProperty] private string timeDisplay = string.Empty;
    [ObservableProperty] private string fieldDisplay = string.Empty;
    [ObservableProperty] private string seasonDisplay = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private bool hasLineup;
    [ObservableProperty] private bool hasScoringSession;
    [ObservableProperty] private bool isScoringCompleted;
    [ObservableProperty] private bool isTeamYearOpen;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowingGameStats))]
    private bool showingGamePlays;
    private string completedVisitorName = string.Empty;
    private int completedVisitorScore;
    private string completedHomeName = string.Empty;
    private int completedHomeScore;
    private string completedVisitorScoreEdit = string.Empty;
    private string completedHomeScoreEdit = string.Empty;
    [ObservableProperty] private bool isManualOverrideEditing;
    [ObservableProperty] private bool hasManualOverrides;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand)), NotifyCanExecuteChangedFor(nameof(BeginManualOverrideCommand))] private bool isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand))] private bool isLoaded;

    public GameWorkspaceViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository,
        GameScoringSessionRepository scoringSessionRepository,
        CompletedGameManualOverrideRepository manualOverrideRepository)
    {
        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
        this.scoringSessionRepository = scoringSessionRepository;
        this.manualOverrideRepository = manualOverrideRepository;
    }

    public string LineupActionText => HasLineup ? AppResources.EditLineupButton : AppResources.SetLineupTitle;
    public string CompletedVisitorName
    {
        get => completedVisitorName;
        private set => SetProperty(ref completedVisitorName, value);
    }

    public int CompletedVisitorScore
    {
        get => completedVisitorScore;
        private set => SetProperty(ref completedVisitorScore, value);
    }

    public string CompletedHomeName
    {
        get => completedHomeName;
        private set => SetProperty(ref completedHomeName, value);
    }

    public int CompletedHomeScore
    {
        get => completedHomeScore;
        private set => SetProperty(ref completedHomeScore, value);
    }

    public string CompletedVisitorScoreEdit
    {
        get => completedVisitorScoreEdit;
        set
        {
            if (!SetProperty(ref completedVisitorScoreEdit, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(IsCompletedVisitorScoreDirty));
            OnPropertyChanged(nameof(CompletedVisitorScoreEditDescription));
        }
    }

    public string CompletedHomeScoreEdit
    {
        get => completedHomeScoreEdit;
        set
        {
            if (!SetProperty(ref completedHomeScoreEdit, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(IsCompletedHomeScoreDirty));
            OnPropertyChanged(nameof(CompletedHomeScoreEditDescription));
        }
    }

    public bool IsCompletedVisitorScoreDirty =>
        !TryParseNonNegative(CompletedVisitorScoreEdit, out var value)
        || value != CompletedVisitorScore;
    public bool IsCompletedHomeScoreDirty =>
        !TryParseNonNegative(CompletedHomeScoreEdit, out var value)
        || value != CompletedHomeScore;
    public string CompletedVisitorScoreEditDescription => GetScoreEditDescription(
        AppResources.ManualOverrideVisitorScoreLabel, IsCompletedVisitorScoreDirty);
    public string CompletedHomeScoreEditDescription => GetScoreEditDescription(
        AppResources.ManualOverrideHomeScoreLabel, IsCompletedHomeScoreDirty);

    public BattingStatisticsTableViewModel CompletedBattingStatistics { get; } = new();
    public ObservableCollection<CompletedGameHalfInningViewModel> CompletedGamePlays { get; } = [];
    public bool ShowingGameStats => !ShowingGamePlays;
    public bool ShowStandardWorkspace => IsLoaded && !IsScoringCompleted;
    public bool ShowCompletedGame => IsLoaded && IsScoringCompleted;
    public bool ShowLineupAction => IsLoaded && IsTeamYearOpen && !HasScoringSession;
    public bool ShowScoreGame => IsLoaded && IsTeamYearOpen && HasLineup && !HasScoringSession;
    public bool ShowResumeGame => IsLoaded && IsTeamYearOpen
        && HasScoringSession && !IsScoringCompleted;
    public bool ShowResetGame => IsLoaded && IsTeamYearOpen
        && HasScoringSession && !IsScoringCompleted;
    public bool ShowManualOverrideActions => ShowCompletedGame && IsTeamYearOpen;
    public bool ShowBeginManualOverride => ShowManualOverrideActions && !IsManualOverrideEditing;
    public bool ShowManualOverrideChip => ShowCompletedGame && HasManualOverrides;
    public bool ShowRevertManualOverrides => ShowManualOverrideActions
        && HasManualOverrides && !IsManualOverrideEditing;
    public Func<Task<bool>>? RevertManualOverridesConfirmationRequested { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var hasTeamId = query.TryGetValue(AppShell.TeamIdQueryParameter, out var teamIdValue);
        var hasGameId = query.TryGetValue(AppShell.GameIdQueryParameter, out var gameIdValue);
        if (!hasTeamId && !hasGameId)
        {
            if (!hasValidContext)
            {
                ValidationMessage = AppResources.GameNotFound;
            }

            return;
        }

        if (!hasTeamId
            || !hasGameId
            || !TryParseId(teamIdValue, out var teamId)
            || !TryParseId(gameIdValue, out var gameId))
        {
            Reset();
            ValidationMessage = AppResources.GameNotFound;
            return;
        }

        if (hasValidContext && TeamId == teamId && GameId == gameId)
        {
            return;
        }

        Reset();
        TeamId = teamId;
        GameId = gameId;
        hasValidContext = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasValidContext) return;

        IsBusy = true;
        IsLoaded = false;
        ClearCompletedGame();
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            var game = await gameRepository.GetByIdForTeamAsync(TeamId, GameId);
            if (team is null || game is null)
            {
                ValidationMessage = AppResources.GameNotFound;
                return;
            }

            var season = (await seasonRepository.GetForTeamAsync(TeamId))
                .FirstOrDefault(item => item.Id == game.SeasonId);
            if (season is null)
            {
                ValidationMessage = AppResources.GameNotFound;
                return;
            }

            TeamName = team.Name;
            IsTeamYearOpen = !team.IsYearOver;
            OpponentDisplay = DisplayOrTbd(game.Opponent);
            DateDisplay = game.ScheduledDate.ToString("D", CultureInfo.CurrentCulture);
            TimeDisplay = DateTime.Today.Add(game.ScheduledTime).ToString("t", CultureInfo.CurrentCulture);
            FieldDisplay = DisplayOrTbd(game.Field);
            SeasonDisplay = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.SeasonSelectionDisplayFormat,
                season.Name,
                season.Year);
            ValidationMessage = string.Empty;
            HasLineup = await lineupRepository.HasLineupAsync(GameId);
            var session = await scoringSessionRepository.GetForGameAsync(GameId);
            HasScoringSession = session is not null;
            IsScoringCompleted = session?.Status == Core.Models.GameScoringStatus.Completed;
            if (IsScoringCompleted)
            {
                if (await RefreshCompletedProjectionAsync(initialLoad: true))
                {
                    var plays = await scoringSessionRepository
                        .GetCompletedGamePlaysAsync(GameId);
                    if (plays is not null)
                        foreach (var half in plays.HalfInnings)
                            CompletedGamePlays.Add(new(half,
                                plays.TeamName, plays.OpponentName));
                }
            }
            IsLoaded = true;
            NotifyStateChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditGame))]
    private async Task EditGameAsync()
    {
        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId,
            [AppShell.GameIdQueryParameter] = GameId,
        };
        await Shell.Current.GoToAsync(AppShell.EditGameRoute, parameters);
    }

    private bool CanEditGame() => !IsBusy && IsLoaded && IsTeamYearOpen && hasValidContext;

    [RelayCommand(CanExecute = nameof(CanSetLineup))]
    private Task SetLineupAsync() => Shell.Current.GoToAsync(AppShell.SetLineupRoute, new ShellNavigationQueryParameters
    {
        [AppShell.TeamIdQueryParameter] = TeamId,
        [AppShell.GameIdQueryParameter] = GameId,
    });

    private bool CanSetLineup() => !IsBusy && IsLoaded && IsTeamYearOpen && hasValidContext;

    public async Task<bool> InitializeScoringAsync(Core.Models.TeamGameSide teamSide,
        int homeRunLimit, bool isOneUpEnabled)
    {
        if (IsBusy || !IsLoaded || !IsTeamYearOpen || !hasValidContext) return false;
        IsBusy = true;
        try
        {
            var result = await scoringSessionRepository.InitializeAsync(
                TeamId, GameId, teamSide, homeRunLimit, isOneUpEnabled);
            if (!result.IsSuccess)
            {
                ValidationMessage = result.FailureReason == InitializeScoringSessionFailure.InvalidLineup
                    ? AppResources.ScoringLineupInvalidMessage
                    : AppResources.ScoringSessionStartFailed;
                return false;
            }

            await NavigateToScoringAsync();
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ResetScoringAsync()
    {
        if (IsBusy || !ShowResetGame) return false;
        IsBusy = true;
        try
        {
            var result = await scoringSessionRepository.ResetGameAsync(GameId);
            if (!result.IsSuccess)
            {
                ValidationMessage = AppResources.ResetGameFailedMessage;
                return false;
            }
            HasScoringSession = false;
            IsScoringCompleted = false;
            NotifyStateChanged();
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ResumeGameAsync() => NavigateToScoringAsync();

    [RelayCommand]
    private void ShowGameStats() => ShowingGamePlays = false;

    [RelayCommand]
    private void ShowGamePlays() => ShowingGamePlays = true;

    [RelayCommand(CanExecute = nameof(CanBeginManualOverride))]
    private void BeginManualOverride()
    {
        ShowingGamePlays = false;
        ValidationMessage = string.Empty;
        CompletedVisitorScoreEdit = CompletedVisitorScore.ToString(CultureInfo.CurrentCulture);
        CompletedHomeScoreEdit = CompletedHomeScore.ToString(CultureInfo.CurrentCulture);
        CompletedBattingStatistics.BeginEdit();
        IsManualOverrideEditing = true;
        NotifyManualOverrideStateChanged();
    }

    private bool CanBeginManualOverride() => ShowManualOverrideActions
        && !IsManualOverrideEditing && !IsBusy;

    [RelayCommand]
    private void CancelManualOverride()
    {
        CompletedBattingStatistics.CancelEdit();
        ResetScoreEdits();
        IsManualOverrideEditing = false;
        ValidationMessage = string.Empty;
        NotifyManualOverrideStateChanged();
    }

    [RelayCommand]
    private async Task SaveManualOverrideAsync()
    {
        if (!IsManualOverrideEditing || IsBusy) return;
        if (!CompletedBattingStatistics.TryGetCorrections(out var corrections)
            || !TryParseNonNegative(CompletedVisitorScoreEdit, out var visitorScore)
            || !TryParseNonNegative(CompletedHomeScoreEdit, out var homeScore))
        {
            ValidationMessage = AppResources.ManualOverrideInvalidValueMessage;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await manualOverrideRepository.SaveAsync(
                GameId, corrections, visitorScore, homeScore);
            if (!result.IsSuccess)
            {
                ValidationMessage = result.FailureReason
                    == CompletedGameManualOverrideFailure.TeamYearFinalized
                    ? AppResources.TeamYearFinalizedWriteMessage
                    : result.FailureReason == CompletedGameManualOverrideFailure.InvalidBattingStatistics
                        ? AppResources.ManualOverrideImpossibleStatsMessage
                        : AppResources.ManualOverrideSaveFailedMessage;
                return;
            }
            IsManualOverrideEditing = false;
            await RefreshCompletedProjectionAsync(initialLoad: false);
            ValidationMessage = string.Empty;
            NotifyManualOverrideStateChanged();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RevertManualOverridesAsync()
    {
        if (!ShowRevertManualOverrides || IsBusy) return;
        if (RevertManualOverridesConfirmationRequested is not null
            && !await RevertManualOverridesConfirmationRequested()) return;
        IsBusy = true;
        try
        {
            var result = await manualOverrideRepository.RevertAsync(GameId);
            if (!result.IsSuccess)
            {
                ValidationMessage = result.FailureReason
                    == CompletedGameManualOverrideFailure.TeamYearFinalized
                    ? AppResources.TeamYearFinalizedWriteMessage
                    : AppResources.ManualOverrideRevertFailedMessage;
                return;
            }
            await RefreshCompletedProjectionAsync(initialLoad: false);
            ValidationMessage = string.Empty;
            NotifyManualOverrideStateChanged();
        }
        finally { IsBusy = false; }
    }

    private Task NavigateToScoringAsync() => Shell.Current.GoToAsync(
        AppShell.ScoringRoute,
        new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId,
            [AppShell.GameIdQueryParameter] = GameId,
        });

    partial void OnHasLineupChanged(bool value)
    {
        OnPropertyChanged(nameof(LineupActionText));
        NotifyStateChanged();
    }

    partial void OnHasScoringSessionChanged(bool value) => NotifyStateChanged();
    partial void OnIsScoringCompletedChanged(bool value) => NotifyStateChanged();
    partial void OnIsLoadedChanged(bool value) => NotifyStateChanged();
    partial void OnIsManualOverrideEditingChanged(bool value) => NotifyManualOverrideStateChanged();
    partial void OnHasManualOverridesChanged(bool value) => NotifyManualOverrideStateChanged();
    partial void OnIsTeamYearOpenChanged(bool value)
    {
        EditGameCommand.NotifyCanExecuteChanged();
        SetLineupCommand.NotifyCanExecuteChanged();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowLineupAction));
        OnPropertyChanged(nameof(ShowStandardWorkspace));
        OnPropertyChanged(nameof(ShowCompletedGame));
        OnPropertyChanged(nameof(ShowScoreGame));
        OnPropertyChanged(nameof(ShowResumeGame));
        OnPropertyChanged(nameof(ShowResetGame));
        NotifyManualOverrideStateChanged();
    }

    private void NotifyManualOverrideStateChanged()
    {
        OnPropertyChanged(nameof(ShowManualOverrideActions));
        OnPropertyChanged(nameof(ShowBeginManualOverride));
        OnPropertyChanged(nameof(ShowManualOverrideChip));
        OnPropertyChanged(nameof(ShowRevertManualOverrides));
        BeginManualOverrideCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> RefreshCompletedProjectionAsync(bool initialLoad)
    {
        var completed = await scoringSessionRepository.GetCompletedGameAsync(GameId);
        if (completed is null)
        {
            ValidationMessage = AppResources.CompletedGameLoadFailedMessage;
            return false;
        }
        CompletedVisitorName = completed.VisitorName;
        CompletedVisitorScore = completed.VisitorScore;
        CompletedHomeName = completed.HomeName;
        CompletedHomeScore = completed.HomeScore;
        HasManualOverrides = completed.HasManualOverrides;
        if (initialLoad) CompletedBattingStatistics.Load(completed.Players);
        else CompletedBattingStatistics.Reload(completed.Players);
        ResetScoreEdits();
        return true;
    }

    private void ResetScoreEdits()
    {
        CompletedVisitorScoreEdit = CompletedVisitorScore.ToString(CultureInfo.CurrentCulture);
        CompletedHomeScoreEdit = CompletedHomeScore.ToString(CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(IsCompletedVisitorScoreDirty));
        OnPropertyChanged(nameof(IsCompletedHomeScoreDirty));
        OnPropertyChanged(nameof(CompletedVisitorScoreEditDescription));
        OnPropertyChanged(nameof(CompletedHomeScoreEditDescription));
    }

    private static string GetScoreEditDescription(string fieldName, bool isDirty) =>
        isDirty
            ? string.Format(CultureInfo.CurrentCulture,
                AppResources.ManualOverrideUnsavedFieldDescription, fieldName)
            : fieldName;

    private static string DisplayOrTbd(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AppResources.ScheduleTbdText : value.Trim();

    private void Reset()
    {
        TeamId = GameId = 0;
        TeamName = OpponentDisplay = DateDisplay = TimeDisplay = FieldDisplay = SeasonDisplay = string.Empty;
        ValidationMessage = string.Empty;
        HasLineup = false;
        HasScoringSession = false;
        IsScoringCompleted = false;
        IsTeamYearOpen = false;
        IsManualOverrideEditing = false;
        HasManualOverrides = false;
        ClearCompletedGame();
        IsLoaded = false;
        hasValidContext = false;
    }

    private void ClearCompletedGame()
    {
        CompletedVisitorName = CompletedHomeName = string.Empty;
        CompletedVisitorScore = CompletedHomeScore = 0;
        ResetScoreEdits();
        CompletedBattingStatistics.Clear();
        CompletedGamePlays.Clear();
        ShowingGamePlays = false;
    }

    private static bool TryParseId(object? value, out int id)
    {
        id = 0;
        return value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0;
    }

    private static bool TryParseNonNegative(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.CurrentCulture, out result)
        && result >= 0;
}
