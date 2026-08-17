using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class ScoringViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly GameRepository gameRepository;
    private readonly GameLineupRepository lineupRepository;
    private readonly GameScoringSessionRepository sessionRepository;
    private bool hasValidContext;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private string teamName = string.Empty;
    [ObservableProperty] private string opponentDisplay = string.Empty;
    [ObservableProperty] private string teamSideDisplay = string.Empty;
    [ObservableProperty] private string statusDisplay = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private bool isLoaded;
    public ObservableCollection<string> StartingLineup { get; } = [];

    public ScoringViewModel(TeamRepository teamRepository, GameRepository gameRepository,
        GameLineupRepository lineupRepository, GameScoringSessionRepository sessionRepository)
    {
        this.teamRepository = teamRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
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
        StartingLineup.Clear();
        if (!hasValidContext)
        {
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        var team = await teamRepository.GetActiveByIdAsync(TeamId);
        var game = await gameRepository.GetByIdForTeamAsync(TeamId, GameId);
        var session = await sessionRepository.GetForGameAsync(GameId);
        if (team is null || game is null || session is null)
        {
            ValidationMessage = AppResources.ScoringSessionNotFound;
            return;
        }

        TeamName = team.Name;
        OpponentDisplay = string.IsNullOrWhiteSpace(game.Opponent)
            ? AppResources.ScheduleTbdText : game.Opponent.Trim();
        TeamSideDisplay = session.TeamSide == Core.Models.TeamGameSide.Home
            ? AppResources.HomeButton : AppResources.AwayButton;
        StatusDisplay = session.Status == Core.Models.GameScoringStatus.InProgress
            ? AppResources.InProgressStatus : AppResources.CompletedStatus;
        var players = await lineupRepository.GetPlayersForGameAsync(GameId);
        for (var index = 0; index < players.Count; index++)
            StartingLineup.Add(string.Format(AppResources.LineupPlayerFormat,
                index + 1, $"{players[index].FirstName} {players[index].LastName}".Trim()));
        ValidationMessage = string.Empty;
        IsLoaded = true;
    }

    [RelayCommand]
    public Task ExitGameAsync() => Shell.Current.GoToAsync("..");

    private static bool TryParseId(IDictionary<string, object> query, string key, out int id)
    {
        id = 0;
        return query.TryGetValue(key, out var value)
            && (value is int number ? (id = number) > 0
                : value is string text && int.TryParse(text, out id) && id > 0);
    }
}
