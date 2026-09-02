using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Statistics;
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
    [ObservableProperty] private CompletedBattingStatRowViewModel? completedBattingTeamTotal;
    private BattingStatSortColumn completedBattingSortColumn =
        BattingStatSortColumn.OnBasePercentage;
    private BattingStatSortDirection completedBattingSortDirection =
        BattingStatSortDirection.Descending;
    private string completedVisitorName = string.Empty;
    private int completedVisitorScore;
    private string completedHomeName = string.Empty;
    private int completedHomeScore;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand))] private bool isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand))] private bool isLoaded;

    public GameWorkspaceViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository,
        GameScoringSessionRepository scoringSessionRepository)
    {
        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
        this.scoringSessionRepository = scoringSessionRepository;
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

    public ObservableCollection<CompletedBattingStatRowViewModel> CompletedBattingRows { get; } = [];
    public BattingStatSortColumn CompletedBattingSortColumn
    {
        get => completedBattingSortColumn;
        private set
        {
            if (!SetProperty(ref completedBattingSortColumn, value)) return;
            NotifyCompletedBattingSortHeadersChanged();
        }
    }

    public BattingStatSortDirection CompletedBattingSortDirection
    {
        get => completedBattingSortDirection;
        private set
        {
            if (!SetProperty(ref completedBattingSortDirection, value)) return;
            NotifyCompletedBattingSortHeadersChanged();
        }
    }

    public string PlayerStatSortHeader => GetStatSortHeader(
        AppResources.PlayerStatHeading, BattingStatSortColumn.Player);
    public string PlateAppearancesStatSortHeader => GetStatSortHeader(
        AppResources.PlateAppearancesStatHeading, BattingStatSortColumn.PlateAppearances);
    public string AtBatsStatSortHeader => GetStatSortHeader(
        AppResources.AtBatsStatHeading, BattingStatSortColumn.AtBats);
    public string HitsStatSortHeader => GetStatSortHeader(
        AppResources.HitsStatHeading, BattingStatSortColumn.Hits);
    public string SinglesStatSortHeader => GetStatSortHeader(
        AppResources.SinglesStatHeading, BattingStatSortColumn.Singles);
    public string DoublesStatSortHeader => GetStatSortHeader(
        AppResources.DoublesStatHeading, BattingStatSortColumn.Doubles);
    public string TriplesStatSortHeader => GetStatSortHeader(
        AppResources.TriplesStatHeading, BattingStatSortColumn.Triples);
    public string HomeRunsStatSortHeader => GetStatSortHeader(
        AppResources.HomeRunsStatHeading, BattingStatSortColumn.HomeRuns);
    public string WalksStatSortHeader => GetStatSortHeader(
        AppResources.WalksStatHeading, BattingStatSortColumn.Walks);
    public string FielderChoicesStatSortHeader => GetStatSortHeader(
        AppResources.FielderChoicesStatHeading, BattingStatSortColumn.FielderChoices);
    public string SacrificeFliesStatSortHeader => GetStatSortHeader(
        AppResources.SacrificeFliesStatHeading, BattingStatSortColumn.SacrificeFlies);
    public string BattingAverageStatSortHeader => GetStatSortHeader(
        AppResources.BattingAverageStatHeading, BattingStatSortColumn.BattingAverage);
    public string OnBasePercentageStatSortHeader => GetStatSortHeader(
        AppResources.OnBasePercentageStatHeading, BattingStatSortColumn.OnBasePercentage);
    public string SluggingPercentageStatSortHeader => GetStatSortHeader(
        AppResources.SluggingPercentageStatHeading, BattingStatSortColumn.SluggingPercentage);
    public bool ShowStandardWorkspace => IsLoaded && !IsScoringCompleted;
    public bool ShowCompletedGame => IsLoaded && IsScoringCompleted;
    public bool ShowLineupAction => IsLoaded && !HasScoringSession;
    public bool ShowScoreGame => IsLoaded && HasLineup && !HasScoringSession;
    public bool ShowResumeGame => IsLoaded && HasScoringSession && !IsScoringCompleted;
    public bool ShowResetGame => IsLoaded && HasScoringSession && !IsScoringCompleted;

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
                var completed = await scoringSessionRepository.GetCompletedGameAsync(GameId);
                if (completed is null)
                {
                    ValidationMessage = AppResources.CompletedGameLoadFailedMessage;
                }
                else
                {
                    CompletedVisitorName = completed.VisitorName;
                    CompletedVisitorScore = completed.VisitorScore;
                    CompletedHomeName = completed.HomeName;
                    CompletedHomeScore = completed.HomeScore;
                    for (var index = 0; index < completed.Players.Count; index++)
                        CompletedBattingRows.Add(new(completed.Players[index], index));
                    ApplyCompletedBattingSort();
                    var teamStatistics = BattingStatisticsCalculator.CalculateTeam(
                        completed.Players.Select(player => player.BattingStatistics));
                    CompletedBattingTeamTotal = new(
                        AppResources.TeamTotalStatLabel,
                        teamStatistics);
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

    private bool CanEditGame() => !IsBusy && IsLoaded && hasValidContext;

    [RelayCommand(CanExecute = nameof(CanSetLineup))]
    private Task SetLineupAsync() => Shell.Current.GoToAsync(AppShell.SetLineupRoute, new ShellNavigationQueryParameters
    {
        [AppShell.TeamIdQueryParameter] = TeamId,
        [AppShell.GameIdQueryParameter] = GameId,
    });

    private bool CanSetLineup() => !IsBusy && IsLoaded && hasValidContext;

    public async Task<bool> InitializeScoringAsync(Core.Models.TeamGameSide teamSide,
        int homeRunLimit, bool isOneUpEnabled)
    {
        if (IsBusy || !IsLoaded || !hasValidContext) return false;
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
    private void SortCompletedBattingRows(BattingStatSortColumn column)
    {
        var next = new BattingStatSortState(
            CompletedBattingSortColumn,
            CompletedBattingSortDirection).Select(column);
        CompletedBattingSortColumn = next.Column;
        CompletedBattingSortDirection = next.Direction;

        ApplyCompletedBattingSort();
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

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowLineupAction));
        OnPropertyChanged(nameof(ShowStandardWorkspace));
        OnPropertyChanged(nameof(ShowCompletedGame));
        OnPropertyChanged(nameof(ShowScoreGame));
        OnPropertyChanged(nameof(ShowResumeGame));
        OnPropertyChanged(nameof(ShowResetGame));
    }

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
        ClearCompletedGame();
        IsLoaded = false;
        hasValidContext = false;
    }

    private void ClearCompletedGame()
    {
        CompletedVisitorName = CompletedHomeName = string.Empty;
        CompletedVisitorScore = CompletedHomeScore = 0;
        CompletedBattingRows.Clear();
        CompletedBattingTeamTotal = null;
        CompletedBattingSortColumn = BattingStatSortColumn.OnBasePercentage;
        CompletedBattingSortDirection = BattingStatSortDirection.Descending;
    }

    private void ApplyCompletedBattingSort()
    {
        var rowsByPlayerId = CompletedBattingRows.ToDictionary(row => row.PlayerId);
        var sorted = BattingStatisticsSorter.Sort(
            CompletedBattingRows.Select(row => new BattingStatSortEntry(
                row.PlayerName,
                row.ProjectionOrder,
                row.PlayerStatistics!)),
            CompletedBattingSortColumn,
            CompletedBattingSortDirection);

        for (var index = 0; index < sorted.Count; index++)
        {
            var row = rowsByPlayerId[sorted[index].PlayerId];
            var currentIndex = CompletedBattingRows.IndexOf(row);
            if (currentIndex != index)
                CompletedBattingRows.Move(currentIndex, index);
        }
    }

    private string GetStatSortHeader(string heading, BattingStatSortColumn column)
    {
        if (CompletedBattingSortColumn != column) return heading;
        var indicator = CompletedBattingSortDirection == BattingStatSortDirection.Ascending
            ? AppResources.SortAscendingIndicator
            : AppResources.SortDescendingIndicator;
        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.ActiveStatSortHeaderFormat,
            heading,
            indicator);
    }

    private void NotifyCompletedBattingSortHeadersChanged()
    {
        OnPropertyChanged(nameof(PlayerStatSortHeader));
        OnPropertyChanged(nameof(PlateAppearancesStatSortHeader));
        OnPropertyChanged(nameof(AtBatsStatSortHeader));
        OnPropertyChanged(nameof(HitsStatSortHeader));
        OnPropertyChanged(nameof(SinglesStatSortHeader));
        OnPropertyChanged(nameof(DoublesStatSortHeader));
        OnPropertyChanged(nameof(TriplesStatSortHeader));
        OnPropertyChanged(nameof(HomeRunsStatSortHeader));
        OnPropertyChanged(nameof(WalksStatSortHeader));
        OnPropertyChanged(nameof(FielderChoicesStatSortHeader));
        OnPropertyChanged(nameof(SacrificeFliesStatSortHeader));
        OnPropertyChanged(nameof(BattingAverageStatSortHeader));
        OnPropertyChanged(nameof(OnBasePercentageStatSortHeader));
        OnPropertyChanged(nameof(SluggingPercentageStatSortHeader));
    }

    private static bool TryParseId(object? value, out int id)
    {
        id = 0;
        return value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0;
    }
}
