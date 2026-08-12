using System.Globalization;
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
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand))] private bool isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EditGameCommand))] private bool isLoaded;

    public GameWorkspaceViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository)
    {
        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;
    }

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

    private static string DisplayOrTbd(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AppResources.ScheduleTbdText : value.Trim();

    private void Reset()
    {
        TeamId = GameId = 0;
        TeamName = OpponentDisplay = DateDisplay = TimeDisplay = FieldDisplay = SeasonDisplay = string.Empty;
        ValidationMessage = string.Empty;
        IsLoaded = false;
        hasValidContext = false;
    }

    private static bool TryParseId(object? value, out int id)
    {
        id = 0;
        return value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0;
    }
}
