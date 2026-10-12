using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Tests;

public sealed class EffectiveCompletedBattingStatisticsTests
{
    [Theory]
    [InlineData(nameof(CompletedGameBattingOverride.Singles), 1, 1, 1, 1)]
    [InlineData(nameof(CompletedGameBattingOverride.Doubles), 1, 1, 1, 2)]
    [InlineData(nameof(CompletedGameBattingOverride.Triples), 1, 1, 1, 3)]
    [InlineData(nameof(CompletedGameBattingOverride.HomeRuns), 1, 1, 1, 4)]
    [InlineData(nameof(CompletedGameBattingOverride.Walks), 1, 0, 0, 0)]
    [InlineData(nameof(CompletedGameBattingOverride.FielderChoices), 1, 1, 0, 0)]
    [InlineData(nameof(CompletedGameBattingOverride.SacrificeFlies), 1, 0, 0, 0)]
    public void Each_editable_outcome_contributes_one_derived_plate_appearance(
        string propertyName, int pa, int ab, int hits, int totalBases)
    {
        var row = Override(outs: 0);
        typeof(CompletedGameBattingOverride).GetProperty(propertyName)!.SetValue(row, 1);

        var projected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.Out), [row]));

        Assert.Equal((pa, ab, hits, totalBases),
            (projected.Statistics.PlateAppearances, projected.Statistics.AtBats,
                projected.Statistics.Hits, projected.Statistics.TotalBases));
    }

    [Fact]
    public void Inside_the_park_home_run_is_in_official_hr_total_without_double_counting()
    {
        var canonical = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.InTheParkHomeRun)));
        var corrected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.InTheParkHomeRun), [Override(homeRuns: 0, outs: 1)]));

        Assert.Equal(1, canonical.Statistics.HomeRuns);
        Assert.Equal((1, 1, 1, 4), (canonical.Statistics.PlateAppearances,
            canonical.Statistics.AtBats, canonical.Statistics.Hits,
            canonical.Statistics.TotalBases));
        Assert.Equal((1, 1, 0), (corrected.Statistics.PlateAppearances,
            corrected.Statistics.AtBats, corrected.Statistics.HomeRuns));
    }

    [Fact]
    public void Special_outs_remain_fixed_and_each_stays_one_plate_appearance()
    {
        var source = Source(BatterResult.Out, BatterResult.DoublePlay,
            BatterResult.TriplePlay, BatterResult.Strikeout, BatterResult.DeadBallOut);
        var row = Override(singles: 1, outs: 1);

        var projected = Assert.Single(
            EffectiveCompletedBattingStatistics.ProjectGame(source, [row]));

        Assert.Equal((6, 6, 1), (projected.Statistics.PlateAppearances,
            projected.Statistics.AtBats, projected.Statistics.Hits));
    }

    [Fact]
    public void Adding_single_increases_pa_ab_and_hits()
    {
        var projected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.Single, BatterResult.Out, BatterResult.Out, BatterResult.Out),
            [Override(singles: 2, outs: 3)]));

        Assert.Equal((5, 5, 2), (projected.Statistics.PlateAppearances,
            projected.Statistics.AtBats, projected.Statistics.Hits));
    }

    [Fact]
    public void Adding_single_while_decreasing_out_preserves_pa()
    {
        var projected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.Single, BatterResult.Out, BatterResult.Out, BatterResult.Out),
            [Override(singles: 2, outs: 2)]));

        Assert.Equal((4, 4, 2), (projected.Statistics.PlateAppearances,
            projected.Statistics.AtBats, projected.Statistics.Hits));
    }

    [Fact]
    public void Removing_double_decreases_pa_ab_and_hits()
    {
        var projected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.Double, BatterResult.Out), [Override(outs: 1)]));

        Assert.Equal((1, 1, 0), (projected.Statistics.PlateAppearances,
            projected.Statistics.AtBats, projected.Statistics.Hits));
    }

    [Fact]
    public void Adding_walk_increases_pa_but_not_ab()
    {
        var projected = Assert.Single(EffectiveCompletedBattingStatistics.ProjectGame(
            Source(BatterResult.Out), [Override(walks: 1, outs: 1)]));

        Assert.Equal((2, 1, 0, 1), (projected.Statistics.PlateAppearances,
            projected.Statistics.AtBats, projected.Statistics.Hits,
            projected.Statistics.Walks));
    }

    [Fact]
    public void Negative_and_overflowing_corrections_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EffectiveCompletedBattingStatistics.ProjectGame(
                Source(BatterResult.Out), [Override(singles: -1, outs: 1)]));
        Assert.Throws<OverflowException>(() =>
            EffectiveCompletedBattingStatistics.ProjectGame(
                Source(BatterResult.Out),
                [Override(singles: int.MaxValue, doubles: 1, outs: 1)]));
    }

    private static CompletedGameBattingSource Source(params BatterResult[] results) => new(
        1, [7], results.Select(result => new BattingOutcome(7, result)).ToArray());

    private static CompletedGameBattingOverride Override(
        int singles = 0, int doubles = 0, int triples = 0, int homeRuns = 0,
        int walks = 0, int fielderChoices = 0, int sacrificeFlies = 0,
        int outs = 0) => new()
        {
            GameId = 1,
            PlayerId = 7,
            Singles = singles,
            Doubles = doubles,
            Triples = triples,
            HomeRuns = homeRuns,
            Walks = walks,
            FielderChoices = fielderChoices,
            SacrificeFlies = sacrificeFlies,
            Outs = outs,
        };
}
