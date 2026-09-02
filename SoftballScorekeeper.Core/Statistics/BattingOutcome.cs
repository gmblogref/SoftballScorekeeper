using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Core.Statistics;

public sealed record BattingOutcome(int PlayerId, BatterResult Result);

public sealed record BattingOutcomeCounts(
    int PlayerId,
    int Singles = 0,
    int Doubles = 0,
    int Triples = 0,
    int HomeRuns = 0,
    int Walks = 0,
    int Outs = 0,
    int FielderChoices = 0,
    int SacrificeFlies = 0,
    int DoublePlays = 0,
    int TriplePlays = 0)
{
    internal IEnumerable<(BatterResult Result, int Count)> GetResultCounts()
    {
        yield return (BatterResult.Single, Singles);
        yield return (BatterResult.Double, Doubles);
        yield return (BatterResult.Triple, Triples);
        yield return (BatterResult.HomeRun, HomeRuns);
        yield return (BatterResult.Walk, Walks);
        yield return (BatterResult.Out, Outs);
        yield return (BatterResult.FielderChoice, FielderChoices);
        yield return (BatterResult.SacrificeFly, SacrificeFlies);
        yield return (BatterResult.DoublePlay, DoublePlays);
        yield return (BatterResult.TriplePlay, TriplePlays);
    }
}
