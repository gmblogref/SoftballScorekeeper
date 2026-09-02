namespace SoftballScorekeeper.Core.Statistics;

public enum BattingStatSortColumn
{
    Player,
    PlateAppearances,
    AtBats,
    Hits,
    Singles,
    Doubles,
    Triples,
    HomeRuns,
    Walks,
    FielderChoices,
    SacrificeFlies,
    BattingAverage,
    OnBasePercentage,
    SluggingPercentage,
}

public enum BattingStatSortDirection
{
    Ascending,
    Descending,
}

public readonly record struct BattingStatSortState(
    BattingStatSortColumn Column,
    BattingStatSortDirection Direction)
{
    public static BattingStatSortState Default { get; } = new(
        BattingStatSortColumn.OnBasePercentage,
        BattingStatSortDirection.Descending);

    public BattingStatSortState Select(BattingStatSortColumn column)
    {
        if (!Enum.IsDefined(column))
            throw new ArgumentOutOfRangeException(nameof(column));

        if (column == Column)
        {
            return this with
            {
                Direction = Direction == BattingStatSortDirection.Ascending
                    ? BattingStatSortDirection.Descending
                    : BattingStatSortDirection.Ascending,
            };
        }

        return new(
            column,
            column == BattingStatSortColumn.Player
                ? BattingStatSortDirection.Ascending
                : BattingStatSortDirection.Descending);
    }
}

public sealed record BattingStatSortEntry(
    string PlayerName,
    int ProjectionOrder,
    PlayerBattingStatLine Statistics)
{
    public int PlayerId => Statistics.PlayerId;
}

public static class BattingStatisticsSorter
{
    public static IReadOnlyList<BattingStatSortEntry> Sort(
        IEnumerable<BattingStatSortEntry> entries,
        BattingStatSortColumn column,
        BattingStatSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!Enum.IsDefined(column))
            throw new ArgumentOutOfRangeException(nameof(column));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var materialized = entries.ToArray();
        foreach (var entry in materialized)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.PlayerName);
            if (entry.ProjectionOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(entries),
                    "Projection order cannot be negative.");
        }

        var ordered = column switch
        {
            BattingStatSortColumn.Player => Order(
                materialized, entry => entry.PlayerName, direction,
                StringComparer.OrdinalIgnoreCase),
            BattingStatSortColumn.PlateAppearances => Order(
                materialized, entry => entry.Statistics.PlateAppearances, direction),
            BattingStatSortColumn.AtBats => Order(
                materialized, entry => entry.Statistics.AtBats, direction),
            BattingStatSortColumn.Hits => Order(
                materialized, entry => entry.Statistics.Hits, direction),
            BattingStatSortColumn.Singles => Order(
                materialized, entry => entry.Statistics.Singles, direction),
            BattingStatSortColumn.Doubles => Order(
                materialized, entry => entry.Statistics.Doubles, direction),
            BattingStatSortColumn.Triples => Order(
                materialized, entry => entry.Statistics.Triples, direction),
            BattingStatSortColumn.HomeRuns => Order(
                materialized, entry => entry.Statistics.HomeRuns, direction),
            BattingStatSortColumn.Walks => Order(
                materialized, entry => entry.Statistics.Walks, direction),
            BattingStatSortColumn.FielderChoices => Order(
                materialized, entry => entry.Statistics.FielderChoices, direction),
            BattingStatSortColumn.SacrificeFlies => Order(
                materialized, entry => entry.Statistics.SacrificeFlies, direction),
            BattingStatSortColumn.BattingAverage => Order(
                materialized, entry => entry.Statistics.BattingAverage, direction),
            BattingStatSortColumn.OnBasePercentage => Order(
                materialized, entry => entry.Statistics.OnBasePercentage, direction),
            BattingStatSortColumn.SluggingPercentage => Order(
                materialized, entry => entry.Statistics.SluggingPercentage, direction),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };

        return ordered
            .ThenBy(entry => entry.ProjectionOrder)
            .ThenBy(entry => entry.PlayerId)
            .ToArray();
    }

    private static IOrderedEnumerable<BattingStatSortEntry> Order<TValue>(
        IEnumerable<BattingStatSortEntry> entries,
        Func<BattingStatSortEntry, TValue> selector,
        BattingStatSortDirection direction,
        IComparer<TValue>? comparer = null) =>
        direction == BattingStatSortDirection.Ascending
            ? entries.OrderBy(selector, comparer)
            : entries.OrderByDescending(selector, comparer);
}
