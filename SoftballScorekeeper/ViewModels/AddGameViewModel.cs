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
    private int teamYear;
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
    [NotifyCanExecuteChangedFor(nameof(SaveAndAddAnotherCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndAddAnotherCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isTeamLoaded;

    public AddGameViewModel(
        TeamRepository teamRepository,
        SeasonRepository seasonRepository,
        GameRepository gameRepository
    )
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
        if (
            !query.TryGetValue(AppShell.TeamIdQueryParameter, out var value)
            || !TryParseTeamId(value, out var parsedTeamId)
        )
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
            teamYear = team.Year;
            SeasonYearText = team.Year.ToString(CultureInfo.CurrentCulture);
            seasonYearEdited = false;
            var seasons = (await seasonRepository.GetForTeamAsync(TeamId))
                .Where(item => item.Year == team.Year)
                .ToArray();
            var mostRecentlyUsedSeasonId = await gameRepository.GetMostRecentlyUsedSeasonIdAsync(
                TeamId
            );
            Seasons.Clear();
            foreach (var season in seasons)
            {
                Seasons.Add(new SeasonSelectionItem(season));
            }

            SelectedSeason =
                Seasons.FirstOrDefault(item => item.Season.Id == selectedSeasonId)
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
    private Task SaveAsync() => SaveCoreAsync(addAnother: false);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAndAddAnotherAsync() => SaveCoreAsync(addAnother: true);

    private async Task SaveCoreAsync(bool addAnother)
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

            var games = new List<Game> { BuildGame(ScheduledTime) };
            if (AddSecondGame)
            {
                games.Add(BuildGame(SecondGameTime));
            }

            var result = await gameRepository.CreateForTeamAsync(TeamId, season, games);
            if (!result.IsCompleteSuccess)
            {
                ValidationMessage =
                    result.FailureReason == CreateGamesFailure.DuplicateSchedule
                        ? AppResources.DuplicateGameSchedule
                        : AppResources.GameCreationFailed;
                return;
            }

            if (addAnother)
            {
                await PrepareForAnotherGameAsync(result.SeasonId);
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

        SeasonName = normalizedName;
        season = new Season
        {
            TeamId = TeamId,
            Name = normalizedName,
            Year = teamYear,
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

    private async Task PrepareForAnotherGameAsync(int resolvedSeasonId)
    {
        var nextDate = ScheduledDate.AddDays(7);
        var scheduledGames = await gameRepository.GetForTeamAsync(TeamId);
        var occupiedTimes = scheduledGames
            .Where(game =>
                game.SeasonId == resolvedSeasonId && game.ScheduledDate.Date == nextDate.Date
            )
            .Select(game => game.ScheduledTime)
            .ToHashSet();
        var nextTime = FindNextAvailableStandardTime(occupiedTimes, AddSecondGame);

        var resolvedSelection = Seasons.FirstOrDefault(item => item.Season.Id == resolvedSeasonId);
        if (resolvedSelection is null)
        {
            var resolvedSeason = await seasonRepository.GetByIdForTeamAsync(
                TeamId,
                resolvedSeasonId
            );
            if (resolvedSeason is not null)
            {
                resolvedSelection = new SeasonSelectionItem(resolvedSeason);
                Seasons.Add(resolvedSelection);
            }
        }

        SelectedSeason = resolvedSelection;
        IsCreatingNewSeason = false;
        SeasonName = string.Empty;

        ScheduledDate = nextDate;
        secondGameTimeEdited = false;
        ScheduledTime = nextTime;
        ValidationMessage = string.Empty;
    }

    private static TimeSpan FindNextAvailableStandardTime(
        IReadOnlySet<TimeSpan> occupiedTimes,
        bool requiresConsecutiveSecondTime
    )
    {
        var candidate = DefaultGameTime;
        for (var slot = 0; slot < 24; slot++)
        {
            var secondTime = AddOneHour(candidate);
            if (
                !occupiedTimes.Contains(candidate)
                && (!requiresConsecutiveSecondTime || !occupiedTimes.Contains(secondTime))
            )
            {
                return candidate;
            }

            candidate = secondTime;
        }

        throw new InvalidOperationException(
            "No standard Game time is available on the selected date."
        );
    }

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
        teamYear = 0;
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

        if (
            value is string stringValue
            && int.TryParse(stringValue, out var stringTeamId)
            && stringTeamId > 0
        )
        {
            parsedTeamId = stringTeamId;
            return true;
        }

        parsedTeamId = 0;
        return false;
    }
}
