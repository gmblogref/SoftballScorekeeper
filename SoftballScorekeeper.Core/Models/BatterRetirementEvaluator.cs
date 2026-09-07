namespace SoftballScorekeeper.Core.Models;

public static class BatterRetirementEvaluator
{
    public static bool AddsImplicitBatterOut(BatterResult result) =>
        result is BatterResult.Out
            or BatterResult.Strikeout
            or BatterResult.DeadBallOut;

    public static bool WasBatterRetired(
        BatterResult result,
        bool batterMovementIsOut) =>
        AddsImplicitBatterOut(result)
        || result == BatterResult.SacrificeFly
        || ((result is BatterResult.DoublePlay or BatterResult.TriplePlay)
            && batterMovementIsOut);
}
