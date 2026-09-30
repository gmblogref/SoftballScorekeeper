namespace SoftballScorekeeper.Core.Models;

public sealed record ResolvedPlayParticipantOutcome(
    int PlayerId,
    RunnerStartingLocation StartingLocation,
    RunnerDestination Destination,
    RunnerDestination? SafeDestination,
    RunnerDestination? AttemptedDestination,
    int? OutOrder,
    bool IsUnorderedMultiOutRetirement = false,
    bool RequiresOutcomeConfirmation = false);

public enum ThirdOutRunRulingRequirement
{
    NotApplicable,
    AutomaticNoRun,
    ScorerDecisionRequired,
}

public static class ThirdOutRunRulingEvaluator
{
    public static ThirdOutRunRulingRequirement Evaluate(
        int currentOuts,
        int batterPlayerId,
        IReadOnlyList<ResolvedPlayParticipantOutcome> participants,
        BatterResult? result = null)
    {
        if (currentOuts is < 0 or > 2 || batterPlayerId <= 0 || participants is null)
            return ThirdOutRunRulingRequirement.NotApplicable;

        if (result == BatterResult.DoublePlay && currentOuts == 1
            || result == BatterResult.TriplePlay && currentOuts == 0)
            return ThirdOutRunRulingRequirement.AutomaticNoRun;

        var outsNeeded = 3 - currentOuts;
        var retired = participants
            .Where(item => item.Destination == RunnerDestination.Out)
            .ToArray();
        if (retired.Length != outsNeeded)
            return ThirdOutRunRulingRequirement.NotApplicable;

        ResolvedPlayParticipantOutcome? thirdOut = null;
        var chronologicalThirdOuts = retired
            .Where(item => !item.IsUnorderedMultiOutRetirement
                && item.OutOrder == outsNeeded)
            .ToArray();
        if (chronologicalThirdOuts.Length == 1)
            thirdOut = chronologicalThirdOuts[0];
        else if (retired.Count(item => item.IsUnorderedMultiOutRetirement)
                 >= outsNeeded)
            return ThirdOutRunRulingRequirement.ScorerDecisionRequired;
        else if (outsNeeded == 1 && retired.Length == 1 && retired[0].OutOrder is null)
            thirdOut = retired[0];

        if (thirdOut is null)
            return ThirdOutRunRulingRequirement.ScorerDecisionRequired;

        var batterRunnerIsOutBeforeFirst = thirdOut.PlayerId == batterPlayerId
            && thirdOut.StartingLocation == RunnerStartingLocation.Home
            && thirdOut.SafeDestination is null
            && thirdOut.AttemptedDestination is null;
        var isFielderChoiceForceOut = result == BatterResult.FielderChoice
            && thirdOut.StartingLocation != RunnerStartingLocation.Home
            && thirdOut.SafeDestination is null
            && thirdOut.AttemptedDestination is null
            && IsForced(thirdOut.StartingLocation, participants);
        return batterRunnerIsOutBeforeFirst || isFielderChoiceForceOut
            ? ThirdOutRunRulingRequirement.AutomaticNoRun
            : ThirdOutRunRulingRequirement.ScorerDecisionRequired;
    }

    private static bool IsForced(
        RunnerStartingLocation runner,
        IReadOnlyList<ResolvedPlayParticipantOutcome> participants)
    {
        var occupied = participants
            .Select(item => item.StartingLocation)
            .ToHashSet();
        return runner switch
        {
            RunnerStartingLocation.First => true,
            RunnerStartingLocation.Second =>
                occupied.Contains(RunnerStartingLocation.First),
            RunnerStartingLocation.Third =>
                occupied.Contains(RunnerStartingLocation.First)
                && occupied.Contains(RunnerStartingLocation.Second),
            _ => false,
        };
    }
}
