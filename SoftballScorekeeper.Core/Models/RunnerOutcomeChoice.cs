namespace SoftballScorekeeper.Core.Models;

public sealed record RunnerOutcomeChoice(
    RunnerDestination Destination,
    RunnerDestination? SafeDestination = null,
    RunnerDestination? AttemptedDestination = null)
{
    public bool IsOutAttemptingAdvance =>
        Destination == RunnerDestination.Out
        && SafeDestination is not null
        && AttemptedDestination is not null;

    public bool IsValidFor(RunnerStartingLocation startingLocation)
    {
        if (!Enum.IsDefined(startingLocation) || !Enum.IsDefined(Destination)) return false;
        if (!IsOutAttemptingAdvance)
            return SafeDestination is null && AttemptedDestination is null;

        return SafeDestination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third
            && AttemptedDestination == GetNextDestination(SafeDestination.Value)
            && (int)SafeDestination.Value >= (int)startingLocation;
    }

    public static RunnerOutcomeChoice ForDestination(RunnerDestination destination) =>
        new(destination);

    public static RunnerDestination? GetNextDestination(
        RunnerDestination safeDestination) => safeDestination switch
        {
            RunnerDestination.First => RunnerDestination.Second,
            RunnerDestination.Second => RunnerDestination.Third,
            RunnerDestination.Third => RunnerDestination.Scores,
            _ => null,
        };
}
