using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class ScoringViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly GameRepository gameRepository;
    private readonly GameScoringSessionRepository sessionRepository;
    private bool hasValidContext;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private string homeTeamDisplayName = string.Empty;
    [ObservableProperty] private string visitorTeamDisplayName = string.Empty;
    [ObservableProperty] private int homeScore;
    [ObservableProperty] private int visitorScore;
    [ObservableProperty] private string currentBatterDisplayName = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private bool isLoaded;

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
        ValidationMessage = string.Empty;
        IsLoaded = true;
    }

    [RelayCommand]
    public Task ExitGameAsync() => Shell.Current.GoToAsync("..");

    private void ResetPresentation()
    {
        HomeTeamDisplayName = string.Empty;
        VisitorTeamDisplayName = string.Empty;
        HomeScore = 0;
        VisitorScore = 0;
        CurrentBatterDisplayName = string.Empty;
    }

    private static string FormatPlayerName(string firstName, string? lastName) =>
        string.Join(" ", new[] { firstName, lastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim()));

    private static bool TryParseId(IDictionary<string, object> query, string key, out int id)
    {
        id = 0;
        return query.TryGetValue(key, out var value)
            && (value is int number ? (id = number) > 0
                : value is string text && int.TryParse(text, out id) && id > 0);
    }
}
