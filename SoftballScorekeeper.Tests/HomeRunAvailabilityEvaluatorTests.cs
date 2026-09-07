using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class HomeRunAvailabilityEvaluatorTests
{
    [Theory]
    [InlineData(5, false, 4, 0, true)]
    [InlineData(5, false, 5, 5, false)]
    [InlineData(5, true, 5, 4, false)]
    [InlineData(5, true, 5, 5, true)]
    [InlineData(5, true, 6, 5, false)]
    [InlineData(5, true, 5, 6, true)]
    [InlineData(5, true, 3, 7, true)]
    [InlineData(5, true, 6, 6, true)]
    [InlineData(0, false, 0, 0, false)]
    [InlineData(0, true, 0, 0, true)]
    [InlineData(0, true, 1, 0, false)]
    [InlineData(0, true, 0, 1, true)]
    public void Evaluates_authoritative_limit_and_one_up_rule(
        int limit, bool oneUp, int battingTotal, int opponentTotal, bool expected)
    {
        Assert.Equal(expected, HomeRunAvailabilityEvaluator.IsHomeRunLegal(
            limit, oneUp, battingTotal, opponentTotal));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, -1)]
    public void Rejects_negative_authoritative_values(
        int limit, int battingTotal, int opponentTotal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HomeRunAvailabilityEvaluator.IsHomeRunLegal(
                limit, false, battingTotal, opponentTotal));
    }

    [Theory]
    [InlineData(true, BatterResult.HomeRun, BatterResult.DeadBallOut)]
    [InlineData(false, BatterResult.DeadBallOut, BatterResult.HomeRun)]
    public void Selectable_results_expose_exactly_one_hr_classification(
        bool isHomeRunLegal, BatterResult included, BatterResult excluded)
    {
        var results = PlateAppearanceResultDefinitions.GetSelectableResults(
            new HashSet<RunnerStartingLocation>
            {
                RunnerStartingLocation.First,
                RunnerStartingLocation.Second,
                RunnerStartingLocation.Third,
            },
            currentOuts: 2,
            isHomeRunLegal);

        Assert.Contains(BatterResult.Strikeout, results);
        Assert.Contains(included, results);
        Assert.DoesNotContain(excluded, results);
    }
}
