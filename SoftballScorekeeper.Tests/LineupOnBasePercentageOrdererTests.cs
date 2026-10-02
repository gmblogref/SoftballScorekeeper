using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Tests;

public sealed class LineupOnBasePercentageOrdererTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    public void Initial_lineup_uses_every_eligible_player_when_roster_does_not_exceed_ten(
        int playerCount)
    {
        var players = Enumerable.Range(1, playerCount)
            .Select(id => (id, (decimal?)(id / 100m)));

        var ordered = LineupOnBasePercentageOrderer.Order(players, 10);

        Assert.Equal(playerCount, ordered.Count);
        Assert.Equal(Enumerable.Range(1, playerCount).Reverse(), ordered);
    }

    [Fact]
    public void Initial_lineup_takes_only_top_ten_defined_obp_players()
    {
        var players = Enumerable.Range(1, 14)
            .Select(id => (id, (decimal?)(id / 100m)));

        var ordered = LineupOnBasePercentageOrderer.Order(players, 10);

        Assert.Equal(10, ordered.Count);
        Assert.Equal(Enumerable.Range(5, 10).Reverse(), ordered);
    }

    [Fact]
    public void Undefined_obp_fills_remaining_positions_in_stable_order()
    {
        var players = Enumerable.Range(1, 14)
            .Select(id => (id, id <= 7 ? (decimal?)(id / 10m) : null));

        var ordered = LineupOnBasePercentageOrderer.Order(players, 10);

        Assert.Equal(new[] { 7, 6, 5, 4, 3, 2, 1, 8, 9, 10 }, ordered);
    }

    [Fact]
    public void Equal_and_undefined_obp_values_preserve_input_order()
    {
        var ordered = LineupOnBasePercentageOrderer.Order(
        [
            (11, (decimal?)0.500m),
            (12, (decimal?)0.500m),
            (13, null),
            (14, null),
        ]);

        Assert.Equal(new[] { 11, 12, 13, 14 }, ordered);
    }

    [Fact]
    public void Nonempty_lineup_order_has_no_automatic_roster_limit()
    {
        var currentLineup = Enumerable.Range(1, 12)
            .Select(id => (id, (decimal?)(id / 100m)));

        var ordered = LineupOnBasePercentageOrderer.Order(currentLineup);

        Assert.Equal(12, ordered.Count);
        Assert.Equal(Enumerable.Range(1, 12).Reverse(), ordered);
    }
}
