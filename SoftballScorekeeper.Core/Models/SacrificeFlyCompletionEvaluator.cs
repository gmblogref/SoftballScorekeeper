namespace SoftballScorekeeper.Core.Models;

public static class SacrificeFlyCompletionEvaluator
{
    public static bool IsSatisfied(
        IReadOnlyList<ResolvedPlayParticipantOutcome> participants) =>
        participants is not null
        && participants.Any(item =>
            item.StartingLocation != RunnerStartingLocation.Home
            && item.Destination == RunnerDestination.Scores);
}
