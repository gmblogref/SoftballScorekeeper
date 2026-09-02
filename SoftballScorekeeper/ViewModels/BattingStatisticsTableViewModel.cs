using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class BattingStatisticsTableViewModel : ObservableObject
{
    private BattingStatSortColumn sortColumn = BattingStatSortColumn.OnBasePercentage;
    private BattingStatSortDirection sortDirection = BattingStatSortDirection.Descending;

    [ObservableProperty]
    private CompletedBattingStatRowViewModel? teamTotal;

    public ObservableCollection<CompletedBattingStatRowViewModel> Rows { get; } = [];

    public BattingStatSortColumn SortColumn
    {
        get => sortColumn;
        private set
        {
            if (!SetProperty(ref sortColumn, value)) return;
            NotifySortHeadersChanged();
        }
    }

    public BattingStatSortDirection SortDirection
    {
        get => sortDirection;
        private set
        {
            if (!SetProperty(ref sortDirection, value)) return;
            NotifySortHeadersChanged();
        }
    }

    public string PlayerStatSortHeader => GetSortHeader(
        AppResources.PlayerStatHeading, BattingStatSortColumn.Player);
    public string PlateAppearancesStatSortHeader => GetSortHeader(
        AppResources.PlateAppearancesStatHeading, BattingStatSortColumn.PlateAppearances);
    public string AtBatsStatSortHeader => GetSortHeader(
        AppResources.AtBatsStatHeading, BattingStatSortColumn.AtBats);
    public string HitsStatSortHeader => GetSortHeader(
        AppResources.HitsStatHeading, BattingStatSortColumn.Hits);
    public string SinglesStatSortHeader => GetSortHeader(
        AppResources.SinglesStatHeading, BattingStatSortColumn.Singles);
    public string DoublesStatSortHeader => GetSortHeader(
        AppResources.DoublesStatHeading, BattingStatSortColumn.Doubles);
    public string TriplesStatSortHeader => GetSortHeader(
        AppResources.TriplesStatHeading, BattingStatSortColumn.Triples);
    public string HomeRunsStatSortHeader => GetSortHeader(
        AppResources.HomeRunsStatHeading, BattingStatSortColumn.HomeRuns);
    public string WalksStatSortHeader => GetSortHeader(
        AppResources.WalksStatHeading, BattingStatSortColumn.Walks);
    public string FielderChoicesStatSortHeader => GetSortHeader(
        AppResources.FielderChoicesStatHeading, BattingStatSortColumn.FielderChoices);
    public string SacrificeFliesStatSortHeader => GetSortHeader(
        AppResources.SacrificeFliesStatHeading, BattingStatSortColumn.SacrificeFlies);
    public string BattingAverageStatSortHeader => GetSortHeader(
        AppResources.BattingAverageStatHeading, BattingStatSortColumn.BattingAverage);
    public string OnBasePercentageStatSortHeader => GetSortHeader(
        AppResources.OnBasePercentageStatHeading, BattingStatSortColumn.OnBasePercentage);
    public string SluggingPercentageStatSortHeader => GetSortHeader(
        AppResources.SluggingPercentageStatHeading, BattingStatSortColumn.SluggingPercentage);

    public void Load(IReadOnlyList<GameBattingPlayerProjection> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        Clear();
        for (var index = 0; index < players.Count; index++)
            Rows.Add(new(players[index], index));
        ApplySort();
        TeamTotal = new(
            AppResources.TeamTotalStatLabel,
            BattingStatisticsCalculator.CalculateTeam(
                players.Select(player => player.BattingStatistics)));
    }

    public void Clear()
    {
        Rows.Clear();
        TeamTotal = null;
        SortColumn = BattingStatSortColumn.OnBasePercentage;
        SortDirection = BattingStatSortDirection.Descending;
    }

    [RelayCommand]
    private void SortRows(BattingStatSortColumn column)
    {
        var next = new BattingStatSortState(SortColumn, SortDirection).Select(column);
        SortColumn = next.Column;
        SortDirection = next.Direction;
        ApplySort();
    }

    private void ApplySort()
    {
        var rowsByPlayerId = Rows.ToDictionary(row => row.PlayerId);
        var sorted = BattingStatisticsSorter.Sort(
            Rows.Select(row => new BattingStatSortEntry(
                row.PlayerName,
                row.ProjectionOrder,
                row.PlayerStatistics!)),
            SortColumn,
            SortDirection);

        for (var index = 0; index < sorted.Count; index++)
        {
            var row = rowsByPlayerId[sorted[index].PlayerId];
            var currentIndex = Rows.IndexOf(row);
            if (currentIndex != index)
                Rows.Move(currentIndex, index);
        }
    }

    private string GetSortHeader(string heading, BattingStatSortColumn column)
    {
        if (SortColumn != column) return heading;
        var indicator = SortDirection == BattingStatSortDirection.Ascending
            ? AppResources.SortAscendingIndicator
            : AppResources.SortDescendingIndicator;
        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.ActiveStatSortHeaderFormat,
            heading,
            indicator);
    }

    private void NotifySortHeadersChanged()
    {
        OnPropertyChanged(nameof(PlayerStatSortHeader));
        OnPropertyChanged(nameof(PlateAppearancesStatSortHeader));
        OnPropertyChanged(nameof(AtBatsStatSortHeader));
        OnPropertyChanged(nameof(HitsStatSortHeader));
        OnPropertyChanged(nameof(SinglesStatSortHeader));
        OnPropertyChanged(nameof(DoublesStatSortHeader));
        OnPropertyChanged(nameof(TriplesStatSortHeader));
        OnPropertyChanged(nameof(HomeRunsStatSortHeader));
        OnPropertyChanged(nameof(WalksStatSortHeader));
        OnPropertyChanged(nameof(FielderChoicesStatSortHeader));
        OnPropertyChanged(nameof(SacrificeFliesStatSortHeader));
        OnPropertyChanged(nameof(BattingAverageStatSortHeader));
        OnPropertyChanged(nameof(OnBasePercentageStatSortHeader));
        OnPropertyChanged(nameof(SluggingPercentageStatSortHeader));
    }
}
