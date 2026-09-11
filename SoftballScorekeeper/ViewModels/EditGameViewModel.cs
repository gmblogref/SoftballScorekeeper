using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class EditGameViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly SeasonRepository seasonRepository;
    private readonly GameRepository gameRepository;
    private bool hasValidContext;
    private bool seasonYearEdited;
    private bool isUpdatingSeasonYear;
    private DateTime scheduledDate;
    private TimeSpan scheduledTime;
    private string seasonYearText = string.Empty;
    private int teamYear;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private int gameId;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private SeasonSelectionItem? selectedSeason;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsUsingExistingSeason))]
    private bool isCreatingNewSeason;

    [ObservableProperty]
    private string seasonName = string.Empty;

    [ObservableProperty]
    private string opponent = string.Empty;

    [ObservableProperty]
    private string field = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [
        ObservableProperty,
        NotifyCanExecuteChangedFor(nameof(SaveCommand)),
        NotifyCanExecuteChangedFor(nameof(CancelCommand))
    ]
    private bool isBusy;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isLoaded;

    public EditGameViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository
    )
    {
        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;
    }

    public ObservableCollection<SeasonSelectionItem> Seasons { get; } = new();
    public bool IsUsingExistingSeason => !IsCreatingNewSeason;

    public DateTime ScheduledDate
    {
        get => scheduledDate;
        set
        {
            if (!SetProperty(ref scheduledDate, value.Date) || seasonYearEdited)
                return;
            isUpdatingSeasonYear = true;
            SeasonYearText = value.Year.ToString(CultureInfo.CurrentCulture);
            isUpdatingSeasonYear = false;
        }
    }

    public TimeSpan ScheduledTime
    {
        get => scheduledTime;
        set => SetProperty(ref scheduledTime, value);
    }

    public string SeasonYearText
    {
        get => seasonYearText;
        set
        {
            if (SetProperty(ref seasonYearText, value) && !isUpdatingSeasonYear)
                seasonYearEdited = true;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Reset();
        if (
            !TryParseId(query, AppShell.TeamIdQueryParameter, out var teamId)
            || !TryParseId(query, AppShell.GameIdQueryParameter, out var gameId)
        )
        {
            ValidationMessage = AppResources.GameNotFound;
            return;
        }

        TeamId = teamId;
        GameId = gameId;
        hasValidContext = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasValidContext)
            return;
        IsBusy = true;
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            var game = await gameRepository.GetByIdForTeamAsync(TeamId, GameId);
            if (team is null || game is null)
            {
                ValidationMessage = AppResources.GameNotFound;
                return;
            }

            teamYear = team.Year;
            Seasons.Clear();
            foreach (
                var season in (await seasonRepository.GetForTeamAsync(TeamId)).Where(item =>
                    item.Year == team.Year || item.Id == game.SeasonId
                )
            )
                Seasons.Add(new(season));
            SelectedSeason = Seasons.FirstOrDefault(item => item.Season.Id == game.SeasonId);
            if (SelectedSeason is null)
            {
                ValidationMessage = AppResources.GameNotFound;
                return;
            }

            TeamName = team.Name;
            scheduledDate = game.ScheduledDate.Date;
            scheduledTime = game.ScheduledTime;
            seasonYearText = team.Year.ToString(CultureInfo.CurrentCulture);
            seasonYearEdited = false;
            Opponent = game.Opponent ?? string.Empty;
            Field = game.Field ?? string.Empty;
            OnPropertyChanged(nameof(ScheduledDate));
            OnPropertyChanged(nameof(ScheduledTime));
            OnPropertyChanged(nameof(SeasonYearText));
            ValidationMessage = string.Empty;
            IsLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!TryBuildSeason(out var season))
            return;
        IsBusy = true;
        try
        {
            var requested = new Game
            {
                ScheduledDate = ScheduledDate,
                ScheduledTime = ScheduledTime,
                Opponent = Opponent,
                Field = Field,
            };
            var result = await gameRepository.UpdateForTeamAsync(TeamId, GameId, season, requested);
            if (!result.IsSuccess)
            {
                ValidationMessage =
                    result.FailureReason == UpdateGameFailure.DuplicateSchedule
                        ? AppResources.DuplicateGameSchedule
                        : AppResources.GameUpdateFailed;
                return;
            }
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ValidationMessage = AppResources.GameUpdateFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    private bool TryBuildSeason(out Season season)
    {
        if (!IsCreatingNewSeason)
        {
            if (SelectedSeason is not null)
            {
                season = SelectedSeason.Season;
                return true;
            }
            ValidationMessage = AppResources.SeasonRequired;
            season = new();
            return false;
        }
        var name = SeasonName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            ValidationMessage = AppResources.SeasonNameRequired;
            season = new();
            return false;
        }
        SeasonName = name;
        season = new Season
        {
            TeamId = TeamId,
            Name = name,
            Year = teamYear,
        };
        return true;
    }

    private bool CanSave() => !IsBusy && IsLoaded && hasValidContext;

    private bool CanCancel() => !IsBusy;

    private void Reset()
    {
        TeamId = GameId = 0;
        TeamName = string.Empty;
        Seasons.Clear();
        SelectedSeason = null;
        IsCreatingNewSeason = false;
        SeasonName = Opponent = Field = ValidationMessage = string.Empty;
        IsLoaded = false;
        hasValidContext = false;
        seasonYearEdited = false;
        teamYear = 0;
    }

    private static bool TryParseId(IDictionary<string, object> query, string key, out int id)
    {
        id = 0;
        if (!query.TryGetValue(key, out var value))
            return false;
        return value is int number
            ? (id = number) > 0
            : value is string text && int.TryParse(text, out id) && id > 0;
    }
}
