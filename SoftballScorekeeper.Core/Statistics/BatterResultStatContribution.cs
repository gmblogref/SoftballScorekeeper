using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Core.Statistics;

public sealed record BatterResultStatContribution(
    BatterResult Result,
    int PlateAppearances,
    int AtBats,
    int Hits,
    int Singles,
    int Doubles,
    int Triples,
    int HomeRuns,
    int Walks,
    int FielderChoices,
    int SacrificeFlies,
    int TotalBases,
    int OnBaseNumerator,
    int OnBaseDenominator);

public static class BatterResultStatContributions
{
    private static readonly IReadOnlyList<BatterResultStatContribution> Definitions =
    [
        Define(BatterResult.Single, atBats: 1, hits: 1, singles: 1,
            totalBases: 1, onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.Double, atBats: 1, hits: 1, doubles: 1,
            totalBases: 2, onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.Triple, atBats: 1, hits: 1, triples: 1,
            totalBases: 3, onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.HomeRun, atBats: 1, hits: 1, homeRuns: 1,
            totalBases: 4, onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.InTheParkHomeRun, atBats: 1, hits: 1, homeRuns: 1,
            totalBases: 4, onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.Out, atBats: 1, onBaseDenominator: 1),
        Define(BatterResult.Walk, walks: 1,
            onBaseNumerator: 1, onBaseDenominator: 1),
        Define(BatterResult.FielderChoice, atBats: 1, fielderChoices: 1,
            onBaseDenominator: 1),
        Define(BatterResult.DoublePlay, atBats: 1, onBaseDenominator: 1),
        Define(BatterResult.TriplePlay, atBats: 1, onBaseDenominator: 1),
        Define(BatterResult.SacrificeFly, sacrificeFlies: 1,
            onBaseDenominator: 1),
        Define(BatterResult.Strikeout, atBats: 1, onBaseDenominator: 1),
        Define(BatterResult.DeadBallOut, atBats: 1, onBaseDenominator: 1),
    ];

    public static IReadOnlyList<BatterResultStatContribution> All => Definitions;

    public static BatterResultStatContribution Get(BatterResult result) =>
        Definitions.FirstOrDefault(item => item.Result == result)
        ?? throw new ArgumentOutOfRangeException(nameof(result), result,
            "The batter result has no statistical contribution definition.");

    private static BatterResultStatContribution Define(
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
        int onBaseDenominator = 0)
    {
        if (hits != singles + doubles + triples + homeRuns
            || totalBases != singles + 2 * doubles + 3 * triples + 4 * homeRuns
            || onBaseNumerator != hits + walks
            || onBaseDenominator != atBats + walks + sacrificeFlies)
            throw new InvalidOperationException(
                $"The {result} statistical contribution is internally inconsistent.");

        return new(result, 1, atBats, hits, singles, doubles, triples, homeRuns,
            walks, fielderChoices, sacrificeFlies, totalBases,
            onBaseNumerator, onBaseDenominator);
    }
}
