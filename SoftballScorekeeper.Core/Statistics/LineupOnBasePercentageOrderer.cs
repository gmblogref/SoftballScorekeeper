namespace SoftballScorekeeper.Core.Statistics;

public static class LineupOnBasePercentageOrderer
{
    public static IReadOnlyList<int> Order(
        IEnumerable<(int PlayerId, decimal? OnBasePercentage)> players,
        int? maximumPlayers = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (maximumPlayers is <= 0) return [];

        var ordered = players
            .Select((player, index) => (player, index))
            .OrderByDescending(entry => entry.player.OnBasePercentage.HasValue)
            .ThenByDescending(entry => entry.player.OnBasePercentage)
            .ThenBy(entry => entry.index);
        var limited = maximumPlayers is int maximum
            ? ordered.Take(maximum)
            : ordered;
        return limited.Select(entry => entry.player.PlayerId).ToArray();
    }
}
