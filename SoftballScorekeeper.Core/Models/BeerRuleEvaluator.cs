namespace SoftballScorekeeper.Core.Models;

public interface IBeerRuleEvaluator
{
    IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context);
}

public sealed class BeerRuleEvaluator : IBeerRuleEvaluator
{
    public IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context)
    {
        var appearance = context.PlateAppearance;
        var consequences = new List<BeerConsequence>();

        if (appearance.Result == BatterResult.DeadBallOut)
            consequences.Add(Consequence(appearance.PlayerId, BeerRuleType.Dbo));

        if (appearance.Result == BatterResult.Strikeout)
            consequences.Add(Consequence(appearance.PlayerId, BeerRuleType.Strikeout));

        if (
            appearance.Result == BatterResult.HomeRun
            && appearance.RunnerMovements.All(item =>
                item.StartingLocation == RunnerStartingLocation.Home
            )
        )
            consequences.Add(Consequence(appearance.PlayerId, BeerRuleType.SoloHomeRun));

        if (appearance.Result == BatterResult.DoublePlay)
            consequences.Add(Consequence(appearance.PlayerId, BeerRuleType.DoublePlay));

        if (appearance.Result == BatterResult.TriplePlay)
            consequences.Add(Consequence(appearance.PlayerId, BeerRuleType.TriplePlay));

        if (
            QualifiesForTwoOutsInOneInning(appearance)
            && context.CommittedPlateAppearances.Count(item =>
                item.PlayerId == appearance.PlayerId
                && item.Inning == appearance.Inning
                && QualifiesForTwoOutsInOneInning(item)
            ) == 2
        )
            consequences.Add(Consequence(
                appearance.PlayerId, BeerRuleType.TwoOutsInOneInning));

        return consequences;
    }

    private static bool QualifiesForTwoOutsInOneInning(
        BeerCommittedPlateAppearance appearance) =>
        appearance.Result != BatterResult.TriplePlay && appearance.BatterWasRetired;

    private static BeerConsequence Consequence(int playerId, BeerRuleType ruleType) =>
        new(playerId, ruleType, BeerRuleConfigurationDefaults.For(ruleType).Amount);
}

public sealed record BeerConsequence(int PlayerId, BeerRuleType RuleType, int Amount);

public sealed record BeerEvaluationContext(
    int GameId,
    int? TeamId,
    int GameScoringSessionId,
    TeamGameSide TeamSide,
    BeerCommittedPlateAppearance PlateAppearance,
    IReadOnlyList<BeerCommittedPlateAppearance> CommittedPlateAppearances,
    IReadOnlyList<int> BattingSequencePlayerIds,
    int OutsBeforePlay,
    int OutsAfterPlay,
    int RunsScored
);

public sealed record BeerCommittedPlateAppearance(
    int Id,
    int PlayerId,
    int PlateAppearanceNumber,
    int Inning,
    BatterResult Result,
    IReadOnlyList<BeerRunnerMovement> RunnerMovements
)
{
    public bool BatterWasRetired => BatterRetirementEvaluator.WasBatterRetired(
        Result,
        RunnerMovements.Any(item =>
                item.PlayerId == PlayerId
                && item.StartingLocation == RunnerStartingLocation.Home
                && item.Destination == RunnerDestination.Out
            ));
}

public sealed record BeerRunnerMovement(
    int PlayerId,
    RunnerStartingLocation StartingLocation,
    RunnerDestination Destination,
    RunnerDestination? SafeDestination,
    RunnerDestination? AttemptedDestination,
    int? OutOrder,
    bool? RunCounts
);
