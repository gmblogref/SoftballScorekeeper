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
            consequences.Add(new(appearance.PlayerId, BeerRuleType.Dbo, 1));

        if (appearance.Result == BatterResult.Strikeout)
            consequences.Add(new(appearance.PlayerId, BeerRuleType.Strikeout, 2));

        if (
            appearance.Result == BatterResult.HomeRun
            && appearance.RunnerMovements.All(item =>
                item.StartingLocation == RunnerStartingLocation.Home
            )
        )
            consequences.Add(new(appearance.PlayerId, BeerRuleType.SoloHomeRun, 1));

        if (appearance.Result == BatterResult.DoublePlay)
            consequences.Add(new(appearance.PlayerId, BeerRuleType.DoublePlay, 1));

        if (
            appearance.BatterWasRetired
            && context.CommittedPlateAppearances.Count(item =>
                item.PlayerId == appearance.PlayerId
                && item.Inning == appearance.Inning
                && item.BatterWasRetired
            ) == 2
        )
            consequences.Add(new(appearance.PlayerId, BeerRuleType.TwoOutsInOneInning, 1));

        return consequences;
    }
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
