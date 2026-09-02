using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Tests;

public sealed class BattingStatisticsSorterTests
{
    public static IEnumerable<object[]> NumericColumns
    {
        get
        {
            yield return [BattingStatSortColumn.PlateAppearances];
            yield return [BattingStatSortColumn.AtBats];
            yield return [BattingStatSortColumn.Hits];
            yield return [BattingStatSortColumn.Singles];
            yield return [BattingStatSortColumn.Doubles];
            yield return [BattingStatSortColumn.Triples];
            yield return [BattingStatSortColumn.HomeRuns];
            yield return [BattingStatSortColumn.Walks];
            yield return [BattingStatSortColumn.FielderChoices];
            yield return [BattingStatSortColumn.SacrificeFlies];
            yield return [BattingStatSortColumn.BattingAverage];
            yield return [BattingStatSortColumn.OnBasePercentage];
            yield return [BattingStatSortColumn.SluggingPercentage];
        }
    }

    [Fact]
    public void Default_story_order_is_obp_descending_with_stable_projection_ties()
    {
        var entries = Entries();

        var sorted = BattingStatisticsSorter.Sort(
            entries,
            BattingStatSortColumn.OnBasePercentage,
            BattingStatSortDirection.Descending);

        Assert.Equal([3, 1, 2], sorted.Select(entry => entry.PlayerId));
    }

    [Fact]
    public void Sort_state_defaults_to_obp_descending()
    {
        Assert.Equal(
            new BattingStatSortState(
                BattingStatSortColumn.OnBasePercentage,
                BattingStatSortDirection.Descending),
            BattingStatSortState.Default);
    }

    [Theory]
    [InlineData(BattingStatSortDirection.Ascending, BattingStatSortDirection.Descending)]
    [InlineData(BattingStatSortDirection.Descending, BattingStatSortDirection.Ascending)]
    public void Selecting_the_active_column_reverses_direction(
        BattingStatSortDirection current,
        BattingStatSortDirection expected)
    {
        var next = new BattingStatSortState(
            BattingStatSortColumn.Hits,
            current).Select(BattingStatSortColumn.Hits);

        Assert.Equal(
            new BattingStatSortState(BattingStatSortColumn.Hits, expected),
            next);
    }

    [Fact]
    public void Selecting_player_defaults_to_ascending_and_a_stat_defaults_to_descending()
    {
        var current = BattingStatSortState.Default;

        Assert.Equal(
            new BattingStatSortState(
                BattingStatSortColumn.Player,
                BattingStatSortDirection.Ascending),
            current.Select(BattingStatSortColumn.Player));
        Assert.Equal(
            new BattingStatSortState(
                BattingStatSortColumn.HomeRuns,
                BattingStatSortDirection.Descending),
            current.Select(BattingStatSortColumn.HomeRuns));
    }

    [Fact]
    public void Player_sort_is_case_insensitive_and_reversible()
    {
        var entries = Entries();

        var ascending = BattingStatisticsSorter.Sort(
            entries, BattingStatSortColumn.Player, BattingStatSortDirection.Ascending);
        var descending = BattingStatisticsSorter.Sort(
            entries, BattingStatSortColumn.Player, BattingStatSortDirection.Descending);

        Assert.Equal([2, 3, 1], ascending.Select(entry => entry.PlayerId));
        Assert.Equal([1, 3, 2], descending.Select(entry => entry.PlayerId));
    }

    [Theory]
    [MemberData(nameof(NumericColumns))]
    public void Every_stat_column_sorts_underlying_numeric_values_in_both_directions(
        BattingStatSortColumn column)
    {
        var entries = EntriesForColumn(column);

        var ascending = BattingStatisticsSorter.Sort(
            entries, column, BattingStatSortDirection.Ascending);
        var descending = BattingStatisticsSorter.Sort(
            entries, column, BattingStatSortDirection.Descending);

        Assert.Equal([21, 22, 23], ascending.Select(entry => entry.PlayerId));
        Assert.Equal([23, 22, 21], descending.Select(entry => entry.PlayerId));
    }

    [Fact]
    public void Rate_sorting_uses_unformatted_values()
    {
        var lower = BattingStatisticsCalculator.Calculate(new BattingOutcomeCounts(
            PlayerId: 11,
            Singles: 21,
            Outs: 125));
        var higher = BattingStatisticsCalculator.Calculate(new BattingOutcomeCounts(
            PlayerId: 12,
            Singles: 20,
            Outs: 119));
        Assert.Equal(
            BattingRateFormatter.Format(lower.BattingAverage),
            BattingRateFormatter.Format(higher.BattingAverage));

        var sorted = BattingStatisticsSorter.Sort(
            [new("Lower", 0, lower), new("Higher", 1, higher)],
            BattingStatSortColumn.BattingAverage,
            BattingStatSortDirection.Descending);

        Assert.Equal([12, 11], sorted.Select(entry => entry.PlayerId));
    }

    private static IReadOnlyList<BattingStatSortEntry> Entries() =>
    [
        new("Zulu", 0, Calculate(1, BatterResult.Single, BatterResult.Out)),
        new("alpha", 1, Calculate(2, BatterResult.Single, BatterResult.Out)),
        new("Mike", 2, Calculate(3, BatterResult.Walk)),
    ];

    private static IReadOnlyList<BattingStatSortEntry> EntriesForColumn(
        BattingStatSortColumn column)
    {
        var low = CountsFor(column, 21, 1);
        var middle = CountsFor(column, 22, 2);
        var high = CountsFor(column, 23, 3);
        return
        [
            new("Low", 0, low),
            new("Middle", 1, middle),
            new("High", 2, high),
        ];
    }

    private static PlayerBattingStatLine CountsFor(
        BattingStatSortColumn column,
        int playerId,
        int count)
    {
        var counts = column switch
        {
            BattingStatSortColumn.PlateAppearances => new BattingOutcomeCounts(
                playerId, Walks: count),
            BattingStatSortColumn.AtBats => new BattingOutcomeCounts(
                playerId, Outs: count),
            BattingStatSortColumn.Hits or BattingStatSortColumn.Singles
                or BattingStatSortColumn.BattingAverage
                or BattingStatSortColumn.OnBasePercentage => new BattingOutcomeCounts(
                    playerId, Singles: count, Outs: 4 - count),
            BattingStatSortColumn.Doubles => new BattingOutcomeCounts(
                playerId, Doubles: count),
            BattingStatSortColumn.Triples => new BattingOutcomeCounts(
                playerId, Triples: count),
            BattingStatSortColumn.HomeRuns => new BattingOutcomeCounts(
                playerId, HomeRuns: count),
            BattingStatSortColumn.Walks => new BattingOutcomeCounts(
                playerId, Walks: count),
            BattingStatSortColumn.FielderChoices => new BattingOutcomeCounts(
                playerId, FielderChoices: count),
            BattingStatSortColumn.SacrificeFlies => new BattingOutcomeCounts(
                playerId, SacrificeFlies: count),
            BattingStatSortColumn.SluggingPercentage => new BattingOutcomeCounts(
                playerId, HomeRuns: count, Outs: 4 - count),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
        return BattingStatisticsCalculator.Calculate(counts);
    }

    private static PlayerBattingStatLine Calculate(
        int playerId,
        params BatterResult[] results) => Assert.Single(
        BattingStatisticsCalculator.Calculate(results.Select(result =>
            new BattingOutcome(playerId, result))));
}
