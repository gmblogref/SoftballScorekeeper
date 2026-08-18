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
    private bool hasValidContext;
    private int currentBatterPlayerId;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private string homeTeamDisplayName = string.Empty;
    [ObservableProperty] private string visitorTeamDisplayName = string.Empty;
    [ObservableProperty] private int homeScore;
    [ObservableProperty] private int visitorScore;
    [ObservableProperty] private string currentBatterDisplayName = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private string selectedResultDisplay = string.Empty;
    [ObservableProperty] private string runsOnPlayDisplay = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAwaitingResult))]
    private bool hasPendingPlay;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompletePlateAppearanceCommand))]
    private bool isLoaded;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompletePlateAppearanceCommand))]
    private bool isCompletingPlateAppearance;

    public ObservableCollection<ScoredPlayerViewModel> ScoredPlayers { get; } = [];
    public BaseRunnerViewModel FirstBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.FirstBaseHeadingLabel);
    public BaseRunnerViewModel SecondBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.SecondBaseHeadingLabel);
    public BaseRunnerViewModel ThirdBaseRunner { get; private set; } =
        EmptyBaseRunner(AppResources.ThirdBaseHeadingLabel);
    public bool HasScoredPlayers => ScoredPlayers.Count > 0;
    public bool HasNoScoredPlayers => !HasScoredPlayers;
    public bool IsAwaitingResult => !HasPendingPlay;
    public int CurrentBatterPlayerId => currentBatterPlayerId;

    public ScoringViewModel(TeamRepository teamRepository, GameRepository gameRepository,
        GameScoringSessionRepository sessionRepository)
    {
        this.teamRepository = teamRepository;
        this.gameRepository = gameRepository;
        this.sessionRepository = sessionRepository;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var hasTeamId = TryParseId(query, AppShell.TeamIdQueryParameter, out var teamId);
        var hasGameId = TryParseId(query, AppShell.GameIdQueryParameter, out var gameId);
        hasValidContext = hasTeamId && hasGameId;
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
            || !state.PlayersById.TryGetValue(currentSequencePlayer.PlayerId, out var currentPlayer))
        {
            ResetPresentation();
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        HomeScore = state.Session.HomeScore;
        VisitorScore = state.Session.VisitorScore;
        CurrentBatterDisplayName = FormatPlayerName(currentPlayer.FirstName, currentPlayer.LastName);
        currentBatterPlayerId = currentPlayer.Id;
        RestoreBaseRunners(state);
        RestorePendingPlay(state);
        ValidationMessage = string.Empty;
        IsLoaded = true;
    }

    [RelayCommand]
    public Task ExitGameAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand(CanExecute = nameof(CanCompletePlateAppearance))]
    private async Task CompletePlateAppearanceAsync()
    {
        if (!CanCompletePlateAppearance()) return;

        IsCompletingPlateAppearance = true;
        try
        {
            var result = await sessionRepository.CompletePlateAppearanceAsync(GameId);
            if (!result.IsSuccess)
            {
                await LoadAsync();
                ValidationMessage = AppResources.PlateAppearanceCompletionFailed;
                return;
            }

            await LoadAsync();
        }
        finally
        {
            IsCompletingPlateAppearance = false;
        }
    }

    private bool CanCompletePlateAppearance() =>
        hasValidContext && IsLoaded && HasPendingPlay && !IsCompletingPlateAppearance;

    [RelayCommand]
    public async Task SelectResultAsync(BatterResult result)
    {
        if (!IsLoaded || IsCompletingPlateAppearance) return;
        if (result == BatterResult.HomeRun)
        {
            IsCompletingPlateAppearance = true;
            try
            {
                var completed = await sessionRepository.CompleteHomeRunAsync(GameId);
                await LoadAsync();
                if (!completed.IsSuccess)
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

    public async Task ChangeRunnerDestinationAsync(int playerId, RunnerDestination destination)
    {
        var response = await sessionRepository.AdjustPendingRunnerAsync(GameId, playerId, destination);
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

    private void ResetPresentation()
    {
        HomeTeamDisplayName = string.Empty;
        VisitorTeamDisplayName = string.Empty;
        HomeScore = 0;
        VisitorScore = 0;
        CurrentBatterDisplayName = string.Empty;
        SelectedResultDisplay = string.Empty;
        RunsOnPlayDisplay = string.Empty;
        HasPendingPlay = false;
        FirstBaseRunner = EmptyBaseRunner(AppResources.FirstBaseHeadingLabel);
        SecondBaseRunner = EmptyBaseRunner(AppResources.SecondBaseHeadingLabel);
        ThirdBaseRunner = EmptyBaseRunner(AppResources.ThirdBaseHeadingLabel);
        OnPropertyChanged(nameof(FirstBaseRunner));
        OnPropertyChanged(nameof(SecondBaseRunner));
        OnPropertyChanged(nameof(ThirdBaseRunner));
        ScoredPlayers.Clear();
        OnPropertyChanged(nameof(HasScoredPlayers));
        OnPropertyChanged(nameof(HasNoScoredPlayers));
        currentBatterPlayerId = 0;
    }

    private static string FormatPlayerName(string firstName, string? lastName) =>
        string.Join(" ", new[] { firstName, lastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim()));

    private void RestoreBaseRunners(GameScoringState state)
    {
        foreach (var scoringBase in Enum.GetValues<ScoringBase>())
        {
            var playerId = state.PendingPlay is null
                ? state.BaseRunners.FirstOrDefault(item => item.Base == scoringBase)?.PlayerId
                : state.PendingPlay.Movements.FirstOrDefault(item =>
                    item.Destination == (RunnerDestination)scoringBase)?.PlayerId;
            var player = playerId is int id
                && state.PlayersById.TryGetValue(id, out var resolvedPlayer)
                    ? resolvedPlayer
                    : null;
            var name = player is not null
                ? FormatPlayerName(player.FirstName, player.LastName)
                : AppResources.EmptyBaseLabel;
            var isCurrentBatter = playerId == currentBatterPlayerId;
            var baseRunner = new BaseRunnerViewModel(FormatBase(scoringBase), name,
                player is not null ? playerId : null, player is not null,
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

    private void RestorePendingPlay(GameScoringState state)
    {
        ScoredPlayers.Clear();
        HasPendingPlay = state.PendingPlay is not null;
        CompletePlateAppearanceCommand.NotifyCanExecuteChanged();
        if (state.PendingPlay is null) return;
        SelectedResultDisplay = string.Format(AppResources.SelectedResultFormat,
            FormatResult(state.PendingPlay.Play.Result));
        var runs = state.PendingPlay.Movements.Count(item =>
            item.Destination == RunnerDestination.Scores);
        RunsOnPlayDisplay = string.Format(AppResources.RunsOnPlayFormat, runs);
        foreach (var movement in state.PendingPlay.Movements.Where(item =>
            item.Destination == RunnerDestination.Scores))
        {
            if (!state.PlayersById.TryGetValue(movement.PlayerId, out var player)) continue;
            ScoredPlayers.Add(new(
                movement.PlayerId,
                movement.StartingLocation == RunnerStartingLocation.Home
                    ? AppResources.BatterBadgeLabel : AppResources.RunnerBadgeLabel,
                FormatPlayerName(player.FirstName, player.LastName)));
        }
        OnPropertyChanged(nameof(HasScoredPlayers));
        OnPropertyChanged(nameof(HasNoScoredPlayers));
        RestoreBaseRunners(state);
    }

    private static string FormatResult(BatterResult result) => result switch
    {
        BatterResult.Single => AppResources.SingleResultLabel,
        BatterResult.Double => AppResources.DoubleResultLabel,
        BatterResult.Triple => AppResources.TripleResultLabel,
        BatterResult.HomeRun => AppResources.HomeRunResultLabel,
        _ => string.Empty,
    };

    public static string FormatDestination(RunnerDestination destination) => destination switch
    {
        RunnerDestination.First => AppResources.FirstBaseLabel,
        RunnerDestination.Second => AppResources.SecondBaseLabel,
        RunnerDestination.Third => AppResources.ThirdBaseLabel,
        RunnerDestination.Scores => AppResources.ScoresDestinationLabel,
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
        new(baseLabel, AppResources.EmptyBaseLabel, null, false, string.Empty, false);

    private static bool TryParseId(IDictionary<string, object> query, string key, out int id)
    {
        id = 0;
        return query.TryGetValue(key, out var value)
            && (value is int number ? (id = number) > 0
                : value is string text && int.TryParse(text, out id) && id > 0);
    }
}

public sealed record BaseRunnerViewModel(
    string BaseLabel, string PlayerName, int? PlayerId, bool IsOccupied, string RoleLabel,
    bool IsCurrentBatter);

public sealed record ScoredPlayerViewModel(
    int PlayerId, string RoleLabel, string PlayerName)
{
    public string DisplayText => string.Format(AppResources.ScoredPlayerCardFormat,
        RoleLabel, PlayerName);
}
