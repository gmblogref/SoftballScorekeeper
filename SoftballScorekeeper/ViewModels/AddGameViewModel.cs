using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class AddGameViewModel : ObservableObject, IQueryAttributable
{
    private static readonly TimeSpan DefaultGameTime = new(18, 30, 0);
    private readonly TeamRepository teamRepository;
    private readonly SeasonRepository seasonRepository;
    private readonly GameRepository gameRepository;
    private bool hasValidTeamId;
    private bool isUpdatingSeasonYear;
    private bool isUpdatingSecondGameTime;
    private bool seasonYearEdited;
    private bool secondGameTimeEdited;
    private DateTime scheduledDate = DateTime.Today;
    private TimeSpan scheduledTime;
    private TimeSpan secondGameTime;
    private string seasonYearText = DateTime.Today.Year.ToString(CultureInfo.CurrentCulture);

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private SeasonSelectionItem? selectedSeason;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUsingExistingSeason))]
    private bool isCreatingNewSeason;

    [ObservableProperty]
    private string seasonName = string.Empty;

    [ObservableProperty]
    private string opponent = string.Empty;

    [ObservableProperty]
    private string field = string.Empty;

    [ObservableProperty]
    private bool addSecondGame = true;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isTeamLoaded;

    public AddGameViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(seasonRepository);
        ArgumentNullException.ThrowIfNull(gameRepository);

        this.teamRepository = teamRepository;
        this.seasonRepository = seasonRepository;
        this.gameRepository = gameRepository;

        scheduledTime = DefaultGameTime;
        secondGameTime = AddOneHour(scheduledTime);
    }

    public ObservableCollection<SeasonSelectionItem> Seasons { get; } = new();

    public bool IsUsingExistingSeason => !IsCreatingNewSeason;

    public DateTime ScheduledDate
    {
        get => scheduledDate;
        set
        {
            if (!SetProperty(ref scheduledDate, value.Date) || seasonYearEdited)
            {
                return;
            }

            isUpdatingSeasonYear = true;
            SeasonYearText = value.Year.ToString(CultureInfo.CurrentCulture);
            isUpdatingSeasonYear = false;
        }
    }

    public TimeSpan ScheduledTime
    {
        get => scheduledTime;
        set
        {
            if (!SetProperty(ref scheduledTime, value) || secondGameTimeEdited)
            {
                return;
            }

            isUpdatingSecondGameTime = true;
            SecondGameTime = AddOneHour(value);
            isUpdatingSecondGameTime = false;
        }
    }

    public TimeSpan SecondGameTime
    {
        get => secondGameTime;
        set
        {
            if (SetProperty(ref secondGameTime, value) && !isUpdatingSecondGameTime)
            {
                secondGameTimeEdited = true;
            }
        }
    }

    public string SeasonYearText
    {
        get => seasonYearText;
        set
        {
            if (SetProperty(ref seasonYearText, value) && !isUpdatingSeasonYear)
            {
                seasonYearEdited = true;
            }
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value)
            || !TryParseTeamId(value, out var parsedTeamId))
        {
            ResetTeamState();
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        if (hasValidTeamId && TeamId == parsedTeamId)
        {
            return;
        }

        ResetTeamState();
        TeamId = parsedTeamId;
        hasValidTeamId = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasValidTeamId)
        {
            if (!hasValidTeamId)
            {
                ValidationMessage = AppResources.TeamNotFound;
            }

            return;
        }

        IsBusy = true;
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            if (team is null)
            {
                IsTeamLoaded = false;
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            var selectedSeasonId = SelectedSeason?.Season.Id;
            var seasons = await seasonRepository.GetForTeamAsync(TeamId);
            var mostRecentlyUsedSeasonId = await gameRepository
                .GetMostRecentlyUsedSeasonIdAsync(TeamId);
            Seasons.Clear();
            foreach (var season in seasons)
            {
                Seasons.Add(new SeasonSelectionItem(season));
            }

            SelectedSeason = Seasons.FirstOrDefault(item => item.Season.Id == selectedSeasonId)
                ?? Seasons.FirstOrDefault(item => item.Season.Id == mostRecentlyUsedSeasonId)
                ?? Seasons.OrderByDescending(item => item.Season.Id).FirstOrDefault();
            if (Seasons.Count == 0)
            {
                IsCreatingNewSeason = true;
            }

            TeamName = team.Name;
            IsTeamLoaded = true;
            ValidationMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave())
        {
            return;
        }

        ValidationMessage = string.Empty;
        if (!TryBuildSeason(out var season))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (await teamRepository.GetActiveByIdAsync(TeamId) is null)
            {
                IsTeamLoaded = false;
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            var games = new List<Game>
            {
                BuildGame(ScheduledTime),
            };
            if (AddSecondGame)
            {
                games.Add(BuildGame(SecondGameTime));
            }

            var result = await gameRepository.CreateForTeamAsync(TeamId, season, games);
            if (!result.IsCompleteSuccess)
            {
                ValidationMessage = result.FailureReason == CreateGamesFailure.DuplicateSchedule
                    ? AppResources.DuplicateGameSchedule
                    : AppResources.GameCreationFailed;
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ValidationMessage = AppResources.GameCreationFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private bool TryBuildSeason(out Season season)
    {
        if (!IsCreatingNewSeason)
        {
            if (SelectedSeason is null)
            {
                ValidationMessage = AppResources.SeasonRequired;
                season = new Season();
                return false;
            }

            season = SelectedSeason.Season;
            return true;
        }

        var normalizedName = SeasonName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ValidationMessage = AppResources.SeasonNameRequired;
            season = new Season();
            return false;
        }

        if (!int.TryParse(SeasonYearText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var year)
            || year is < 1 or > 9999)
        {
            ValidationMessage = AppResources.SeasonYearInvalid;
            season = new Season();
            return false;
        }

        SeasonName = normalizedName;
        SeasonYearText = year.ToString(CultureInfo.CurrentCulture);
        season = new Season
        {
            TeamId = TeamId,
            Name = normalizedName,
            Year = year,
        };
        return true;
    }

    private Game BuildGame(TimeSpan time)
    {
        return new Game
        {
            ScheduledDate = ScheduledDate.Date,
            ScheduledTime = time,
            Opponent = Opponent,
            Field = Field,
        };
    }

    private bool CanSave() => !IsBusy && IsTeamLoaded && hasValidTeamId;

    private bool CanCancel() => !IsBusy;

    private void ResetTeamState()
    {
        TeamId = 0;
        TeamName = string.Empty;
        Seasons.Clear();
        SelectedSeason = null;
        IsCreatingNewSeason = false;
        SeasonName = string.Empty;
        Opponent = string.Empty;
        Field = string.Empty;
        AddSecondGame = true;
        scheduledDate = DateTime.Today;
        seasonYearText = scheduledDate.Year.ToString(CultureInfo.CurrentCulture);
        seasonYearEdited = false;
        scheduledTime = DefaultGameTime;
        secondGameTime = AddOneHour(scheduledTime);
        secondGameTimeEdited = false;
        OnPropertyChanged(nameof(ScheduledDate));
        OnPropertyChanged(nameof(SeasonYearText));
        OnPropertyChanged(nameof(ScheduledTime));
        OnPropertyChanged(nameof(SecondGameTime));
        IsTeamLoaded = false;
        hasValidTeamId = false;
        ValidationMessage = string.Empty;
    }

    private static TimeSpan AddOneHour(TimeSpan time)
    {
        var result = time.Add(TimeSpan.FromHours(1));
        return result >= TimeSpan.FromDays(1) ? result - TimeSpan.FromDays(1) : result;
    }

    private static bool TryParseTeamId(object? value, out int parsedTeamId)
    {
        if (value is int integerValue && integerValue > 0)
        {
            parsedTeamId = integerValue;
            return true;
        }

        if (value is string stringValue
            && int.TryParse(stringValue, out var stringTeamId)
            && stringTeamId > 0)
        {
            parsedTeamId = stringTeamId;
            return true;
        }

        parsedTeamId = 0;
        return false;
    }
}
