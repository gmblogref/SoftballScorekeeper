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
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand))] private bool isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand)), NotifyCanExecuteChangedFor(nameof(SetLineupCommand))] private bool isLoaded;

    public GameWorkspaceViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository)
    {
        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
    }

    public ObservableCollection<string> LineupPlayers { get; } = [];

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
            LineupPlayers.Clear();
            var lineupPlayers = await lineupRepository.GetPlayersForGameAsync(GameId);
            for (var index = 0; index < lineupPlayers.Count; index++)
                LineupPlayers.Add(string.Format(CultureInfo.CurrentCulture, AppResources.LineupPlayerFormat, index + 1,
                    string.Join(" ", new[] { lineupPlayers[index].FirstName, lineupPlayers[index].LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))));
            HasLineup = LineupPlayers.Count > 0;
            IsLoaded = true;
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

    private static string DisplayOrTbd(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AppResources.ScheduleTbdText : value.Trim();

    private void Reset()
    {
        TeamId = GameId = 0;
        TeamName = OpponentDisplay = DateDisplay = TimeDisplay = FieldDisplay = SeasonDisplay = string.Empty;
        ValidationMessage = string.Empty;
        LineupPlayers.Clear(); HasLineup = false;
        IsLoaded = false;
        hasValidContext = false;
    }

    private static bool TryParseId(object? value, out int id)
    {
        id = 0;
        return value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0;
    }
}
