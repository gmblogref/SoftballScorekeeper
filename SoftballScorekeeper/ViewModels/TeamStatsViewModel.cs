using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class TeamStatsViewModel(
    TeamRepository teamRepository,
    TeamBattingStatisticsRepository statisticsRepository)
    : ObservableObject, IQueryAttributable
{
    private bool hasValidTeamId;
    private bool hasLoaded;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private string teamName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isLoaded;
    [ObservableProperty] private bool hasNoSelectedGames;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private string activeFilterText = AppResources.TeamStatsAllGamesFilter;
    [ObservableProperty] private TeamStatsGameFilter activeFilter = TeamStatsGameFilter.AllGames;

    public BattingStatisticsTableViewModel Statistics { get; } = new();
    public ObservableCollection<TeamCompletedGame> CompletedGames { get; } = [];
    public IReadOnlyList<int> SelectedGameIds { get; private set; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value)
            || !int.TryParse(value?.ToString(), out var parsedTeamId)
            || parsedTeamId <= 0)
        {
            hasValidTeamId = false;
            ValidationMessage = AppResources.TeamStatsLoadFailed;
            return;
        }

        if (TeamId == parsedTeamId && hasValidTeamId)
            return;
        TeamId = parsedTeamId;
        hasValidTeamId = true;
        hasLoaded = false;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || hasLoaded || !hasValidTeamId)
            return;
        IsBusy = true;
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            var projection = await statisticsRepository.GetAsync(TeamId);
            if (team is null || projection is null)
            {
                ValidationMessage = AppResources.TeamStatsLoadFailed;
                return;
            }

            TeamName = team.Name;
            CompletedGames.Clear();
            foreach (var game in projection.CompletedGames)
                CompletedGames.Add(game);
            SelectedGameIds = TeamStatsGameSelection.Resolve(
                TeamStatsGameFilter.AllGames, projection.CompletedGames);
            ActiveFilter = TeamStatsGameFilter.AllGames;
            ActiveFilterText = AppResources.TeamStatsAllGamesFilter;
            Statistics.Load(projection.Players);
            HasNoSelectedGames = SelectedGameIds.Count == 0;
            ValidationMessage = string.Empty;
            IsLoaded = true;
            hasLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ShowAllGamesAsync() => ApplyPresetAsync(TeamStatsGameFilter.AllGames);

    [RelayCommand]
    private Task ShowLastGameNightAsync() => ApplyPresetAsync(TeamStatsGameFilter.LastGameNight);

    [RelayCommand]
    private Task ShowCurrentLeagueSeasonAsync() =>
        ApplyPresetAsync(TeamStatsGameFilter.CurrentLeagueSeason);

    public async Task ApplyCustomAsync(IEnumerable<int> gameIds) =>
        await ApplyFilterAsync(TeamStatsGameFilter.Custom, gameIds);

    private Task ApplyPresetAsync(TeamStatsGameFilter filter) =>
        ApplyFilterAsync(filter, null);

    private async Task ApplyFilterAsync(TeamStatsGameFilter filter, IEnumerable<int>? customIds)
    {
        if (IsBusy || !IsLoaded)
            return;
        var selectedIds = TeamStatsGameSelection.Resolve(filter, CompletedGames, customIds);
        IsBusy = true;
        try
        {
            var projection = await statisticsRepository.GetAsync(TeamId, selectedIds);
            if (projection is null)
            {
                ValidationMessage = AppResources.TeamStatsLoadFailed;
                return;
            }

            SelectedGameIds = selectedIds;
            ActiveFilter = filter;
            ActiveFilterText = filter switch
            {
                TeamStatsGameFilter.AllGames => AppResources.TeamStatsAllGamesFilter,
                TeamStatsGameFilter.LastGameNight => AppResources.TeamStatsLastGameNightFilter,
                TeamStatsGameFilter.CurrentLeagueSeason =>
                    AppResources.TeamStatsCurrentLeagueSeasonFilter,
                TeamStatsGameFilter.Custom => string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.TeamStatsCustomFilterCount,
                    selectedIds.Count),
                _ => throw new ArgumentOutOfRangeException(nameof(filter)),
            };
            Statistics.Reload(projection.Players);
            HasNoSelectedGames = selectedIds.Count == 0;
            ValidationMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
