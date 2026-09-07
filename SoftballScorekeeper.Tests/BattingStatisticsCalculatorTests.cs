using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Tests;

public sealed class BattingStatisticsCalculatorTests
{
    public static IEnumerable<object[]> ExpectedContributions
    {
        get
        {
            yield return [Contribution(BatterResult.Single, atBats: 1, hits: 1,
                singles: 1, totalBases: 1, onBaseNumerator: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.Double, atBats: 1, hits: 1,
                doubles: 1, totalBases: 2, onBaseNumerator: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.Triple, atBats: 1, hits: 1,
                triples: 1, totalBases: 3, onBaseNumerator: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.HomeRun, atBats: 1, hits: 1,
                homeRuns: 1, totalBases: 4, onBaseNumerator: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.Out, atBats: 1,
                onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.Walk, walks: 1,
                onBaseNumerator: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.FielderChoice, atBats: 1,
                fielderChoices: 1, onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.DoublePlay, atBats: 1,
                onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.TriplePlay, atBats: 1,
                onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.SacrificeFly, sacrificeFlies: 1,
                onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.Strikeout, atBats: 1,
                onBaseDenominator: 1)];
            yield return [Contribution(BatterResult.DeadBallOut, atBats: 1,
                onBaseDenominator: 1)];
        }
    }

    [Theory]
    [MemberData(nameof(ExpectedContributions))]
    public void Every_current_result_has_the_required_statistical_contribution(
        BatterResultStatContribution expected)
    {
        Assert.Equal(expected, BatterResultStatContributions.Get(expected.Result));
    }

    [Fact]
    public void Statistical_definitions_exhaustively_match_the_batter_result_enum()
    {
        Assert.Equal(
            Enum.GetValues<BatterResult>().Order(),
            BatterResultStatContributions.All.Select(item => item.Result).Order());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BatterResultStatContributions.Get((BatterResult)int.MaxValue));
    }

    [Fact]
    public void Three_outs_produce_a_zero_for_three_line()
    {
        var line = Calculate(1, BatterResult.Out, BatterResult.Out, BatterResult.Out);

        AssertCounts(line, plateAppearances: 3, atBats: 3, hits: 0, totalBases: 0);
        AssertRates(line, ".000", ".000", ".000");
    }

    [Fact]
    public void Walk_only_produces_on_base_percentage_without_an_at_bat()
    {
        var line = Calculate(1, BatterResult.Walk);

        AssertCounts(line, plateAppearances: 1, atBats: 0, hits: 0, totalBases: 0);
        Assert.Equal(1, line.Walks);
        AssertRates(line, ".000", "1.000", ".000");
    }

    [Fact]
    public void Sacrifice_fly_only_contributes_to_the_on_base_denominator()
    {
        var line = Calculate(1, BatterResult.SacrificeFly);

        AssertCounts(line, plateAppearances: 1, atBats: 0, hits: 0, totalBases: 0);
        Assert.Equal(1, line.SacrificeFlies);
        AssertRates(line, ".000", ".000", ".000");
    }

    [Fact]
    public void Fielder_choice_only_is_an_at_bat_without_hit_or_on_base_credit()
    {
        var line = Calculate(1, BatterResult.FielderChoice);

        AssertCounts(line, plateAppearances: 1, atBats: 1, hits: 0, totalBases: 0);
        Assert.Equal(1, line.FielderChoices);
        AssertRates(line, ".000", ".000", ".000");
    }

    [Fact]
    public void Mixed_results_produce_the_approved_stat_line()
    {
        var line = Calculate(1,
            BatterResult.Single,
            BatterResult.Double,
            BatterResult.Walk,
            BatterResult.SacrificeFly,
            BatterResult.FielderChoice,
            BatterResult.Out);

        AssertCounts(line, plateAppearances: 6, atBats: 4, hits: 2, totalBases: 3);
        Assert.Equal((1, 1, 0, 0),
            (line.Singles, line.Doubles, line.Triples, line.HomeRuns));
        Assert.Equal((1, 1, 1),
            (line.Walks, line.FielderChoices, line.SacrificeFlies));
        AssertRates(line, ".500", ".500", ".750");
    }

    [Fact]
    public void Four_hit_line_allows_slugging_above_one()
    {
        var line = Calculate(1,
            BatterResult.Single,
            BatterResult.Double,
            BatterResult.Triple,
            BatterResult.HomeRun);

        AssertCounts(line, plateAppearances: 4, atBats: 4, hits: 4, totalBases: 10);
        AssertRates(line, "1.000", "1.000", "2.500");
    }

    [Theory]
    [InlineData(0, 1, ".000")]
    [InlineData(1, 3, ".333")]
    [InlineData(2, 3, ".667")]
    [InlineData(1, 2, ".500")]
    [InlineData(1, 1, "1.000")]
    [InlineData(5, 4, "1.250")]
    [InlineData(5, 2, "2.500")]
    public void Rate_formatting_rounds_invariantly_to_three_places(
        int numerator, int denominator, string expected)
    {
        Assert.Equal(expected,
            BattingRateFormatter.Format((decimal)numerator / denominator));
    }

    [Theory]
    [InlineData(BatterResult.DoublePlay)]
    [InlineData(BatterResult.TriplePlay)]
    public void Multi_out_result_is_exactly_one_plate_appearance_and_one_at_bat(
        BatterResult result)
    {
        var line = Calculate(1, result);

        AssertCounts(line, plateAppearances: 1, atBats: 1, hits: 0, totalBases: 0);
    }

    [Fact]
    public void Different_player_ids_produce_separate_lines()
    {
        var lines = BattingStatisticsCalculator.Calculate(
        [
            new BattingOutcome(11, BatterResult.Single),
            new BattingOutcome(22, BatterResult.Out),
        ]);

        Assert.Equal([11, 22], lines.Select(item => item.PlayerId));
        Assert.Equal((1, 1), (lines[0].Hits, lines[1].AtBats));
        Assert.Equal(0, lines[1].Hits);
    }

    [Fact]
    public void Explicit_participant_without_outcomes_receives_a_zero_line()
    {
        var line = Assert.Single(BattingStatisticsCalculator.Calculate([17], []));

        Assert.Equal(17, line.PlayerId);
        AssertCounts(line, plateAppearances: 0, atBats: 0, hits: 0, totalBases: 0);
        AssertRates(line, ".000", ".000", ".000");
    }

    [Fact]
    public void Corrected_outcome_counts_use_the_same_calculation_rules_without_fake_pas()
    {
        var line = BattingStatisticsCalculator.Calculate(new BattingOutcomeCounts(
            PlayerId: 31,
            Singles: 1,
            Doubles: 1,
            Walks: 1,
            Outs: 1,
            FielderChoices: 1,
            SacrificeFlies: 1));

        AssertCounts(line, plateAppearances: 6, atBats: 4, hits: 2, totalBases: 3);
        AssertRates(line, ".500", ".500", ".750");
    }

    [Fact]
    public void Team_totals_sum_outcomes_and_calculate_rates_from_aggregate_counts()
    {
        var firstPlayer = Calculate(1,
            BatterResult.Single,
            BatterResult.Walk);
        var secondPlayer = Calculate(2,
            BatterResult.Single,
            BatterResult.Single,
            BatterResult.Single,
            BatterResult.Double,
            BatterResult.Triple,
            BatterResult.Out,
            BatterResult.Out,
            BatterResult.Out,
            BatterResult.Out,
            BatterResult.Walk,
            BatterResult.SacrificeFly);

        var team = BattingStatisticsCalculator.CalculateTeam(
            [firstPlayer, secondPlayer]);

        Assert.Equal((13, 10, 6, 4, 1, 1, 0, 2, 0, 1, 9),
            (team.PlateAppearances,
                team.AtBats,
                team.Hits,
                team.Singles,
                team.Doubles,
                team.Triples,
                team.HomeRuns,
                team.Walks,
                team.FielderChoices,
                team.SacrificeFlies,
                team.TotalBases));
        Assert.Equal((".600", ".615", ".900"),
            (BattingRateFormatter.Format(team.BattingAverage),
                BattingRateFormatter.Format(team.OnBasePercentage),
                BattingRateFormatter.Format(team.SluggingPercentage)));

        Assert.NotEqual(
            (firstPlayer.BattingAverage + secondPlayer.BattingAverage) / 2,
            team.BattingAverage);
        Assert.NotEqual(
            (firstPlayer.OnBasePercentage + secondPlayer.OnBasePercentage) / 2,
            team.OnBasePercentage);
        Assert.NotEqual(
            (firstPlayer.SluggingPercentage + secondPlayer.SluggingPercentage) / 2,
            team.SluggingPercentage);
    }

    [Fact]
    public void Empty_team_uses_the_shared_zero_denominator_behavior()
    {
        var team = BattingStatisticsCalculator.CalculateTeam([]);

        Assert.Equal((0, 0, 0, 0),
            (team.PlateAppearances, team.AtBats, team.Hits, team.TotalBases));
        Assert.Equal((".000", ".000", ".000"),
            (BattingRateFormatter.Format(team.BattingAverage),
                BattingRateFormatter.Format(team.OnBasePercentage),
                BattingRateFormatter.Format(team.SluggingPercentage)));
    }

    private static PlayerBattingStatLine Calculate(
        int playerId, params BatterResult[] results) =>
        Assert.Single(BattingStatisticsCalculator.Calculate(
            results.Select(result => new BattingOutcome(playerId, result))));

    private static void AssertCounts(
        PlayerBattingStatLine line,
        int plateAppearances,
        int atBats,
        int hits,
        int totalBases)
    {
        Assert.Equal(plateAppearances, line.PlateAppearances);
        Assert.Equal(atBats, line.AtBats);
        Assert.Equal(hits, line.Hits);
        Assert.Equal(totalBases, line.TotalBases);
    }

    private static void AssertRates(
        PlayerBattingStatLine line,
        string battingAverage,
        string onBasePercentage,
        string sluggingPercentage)
    {
        Assert.Equal(battingAverage, BattingRateFormatter.Format(line.BattingAverage));
        Assert.Equal(onBasePercentage, BattingRateFormatter.Format(line.OnBasePercentage));
        Assert.Equal(sluggingPercentage, BattingRateFormatter.Format(line.SluggingPercentage));
    }

    private static BatterResultStatContribution Contribution(
        BatterResult result,
        int atBats = 0,
        int hits = 0,
        int singles = 0,
        int doubles = 0,
        int triples = 0,
        int homeRuns = 0,
        int walks = 0,
        int fielderChoices = 0,
        int sacrificeFlies = 0,
        int totalBases = 0,
        int onBaseNumerator = 0,
        int onBaseDenominator = 0) => new(
            result, 1, atBats, hits, singles, doubles, triples, homeRuns,
            walks, fielderChoices, sacrificeFlies, totalBases,
            onBaseNumerator, onBaseDenominator);
}
