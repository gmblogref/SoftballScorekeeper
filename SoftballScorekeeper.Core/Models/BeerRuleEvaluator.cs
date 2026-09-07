namespace SoftballScorekeeper.Core.Models;

public interface IBeerRuleEvaluator
{
    IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context);
}

public sealed class BeerRuleEvaluator : IBeerRuleEvaluator
{
    public IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context) => [];
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
);

public sealed record BeerRunnerMovement(
    int PlayerId,
    RunnerStartingLocation StartingLocation,
    RunnerDestination Destination,
    RunnerDestination? SafeDestination,
    RunnerDestination? AttemptedDestination,
    int? OutOrder,
    bool? RunCounts
);
