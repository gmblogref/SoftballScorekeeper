using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class TeamYearSummaryViewModel(TeamYearSummaryRepository repository)
    : ObservableObject, IQueryAttributable
{
    private bool hasValidTeamId;

    [ObservableProperty] private int teamId;
    [ObservableProperty] private string header = string.Empty;
    [ObservableProperty] private int gamesPlayed;
    [ObservableProperty] private int wins;
    [ObservableProperty] private int losses;
    [ObservableProperty] private int ties;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isLoaded;
    [ObservableProperty] private bool hasNoBatting;
    [ObservableProperty] private bool hasNoBeer;
    [ObservableProperty] private string validationMessage = string.Empty;

    public BattingStatisticsTableViewModel Statistics { get; } = new();
    public ObservableCollection<TeamYearBeerRowViewModel> BeerRows { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value))
        {
            if (TeamId > 0 && hasValidTeamId)
                return;

            hasValidTeamId = false;
            ValidationMessage = AppResources.TeamYearSummaryLoadFailed;
            return;
        }

        if (!int.TryParse(value?.ToString(), out var parsedTeamId) || parsedTeamId <= 0)
        {
            hasValidTeamId = false;
            ValidationMessage = AppResources.TeamYearSummaryLoadFailed;
            return;
        }

        TeamId = parsedTeamId;
        hasValidTeamId = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasValidTeamId)
            return;

        IsBusy = true;
        try
        {
            var projection = await repository.GetAsync(TeamId);
            if (projection is null)
            {
                Clear(AppResources.TeamYearSummaryLoadFailed);
                return;
            }

            Header = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.TeamYearSummaryHeaderFormat,
                projection.Team.Name,
                projection.Team.Year
            );
            GamesPlayed = projection.Record.GamesPlayed;
            Wins = projection.Record.Wins;
            Losses = projection.Record.Losses;
            Ties = projection.Record.Ties;
            Statistics.Reload(projection.Batting.Players);
            BeerRows.Clear();
            foreach (var summary in projection.Beer)
                BeerRows.Add(new(summary));
            HasNoBatting = Statistics.Rows.Count == 0;
            HasNoBeer = BeerRows.Count == 0;
            ValidationMessage = string.Empty;
            IsLoaded = true;
        }
        catch
        {
            Clear(AppResources.TeamYearSummaryLoadFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Clear(string message)
    {
        Header = string.Empty;
        GamesPlayed = 0;
        Wins = 0;
        Losses = 0;
        Ties = 0;
        Statistics.Clear();
        BeerRows.Clear();
        HasNoBatting = true;
        HasNoBeer = true;
        ValidationMessage = message;
        IsLoaded = false;
    }
}
