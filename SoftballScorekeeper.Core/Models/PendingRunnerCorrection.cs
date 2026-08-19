namespace SoftballScorekeeper.Core.Models;

public static class PendingRunnerCorrection
{
    public static bool TryResolve(
        IReadOnlyList<GameScoringPendingRunnerMovement> currentMovements,
        int batterPlayerId,
        int selectedPlayerId,
        RunnerDestination destination,
        out IReadOnlyDictionary<int, RunnerDestination> resolvedDestinations,
        bool batterReachedBase = true)
    {
        resolvedDestinations = new Dictionary<int, RunnerDestination>();
        if (batterPlayerId <= 0 || selectedPlayerId <= 0
            || !Enum.IsDefined(destination)
            || currentMovements.Count == 0
            || currentMovements.Select(item => item.PlayerId).Distinct().Count()
                != currentMovements.Count
            || currentMovements.Any(item => !Enum.IsDefined(item.Destination))
            || (batterReachedBase
                && currentMovements.All(item => item.PlayerId != batterPlayerId))
            || (!batterReachedBase
                && currentMovements.Any(item => item.PlayerId == batterPlayerId))
            || currentMovements.All(item => item.PlayerId != selectedPlayerId))
            return false;

        var destinations = currentMovements.ToDictionary(
            item => item.PlayerId, item => item.Destination);
        destinations.Remove(selectedPlayerId);
        if (destination is RunnerDestination.Scores or RunnerDestination.Out)
        {
            destinations[selectedPlayerId] = destination;
            resolvedDestinations = destinations;
            return true;
        }

        if (!TryPlaceOnBase(destinations, batterPlayerId, selectedPlayerId, destination))
            return false;
        resolvedDestinations = destinations;
        return true;
    }

    private static bool TryPlaceOnBase(
        IDictionary<int, RunnerDestination> destinations,
        int batterPlayerId,
        int playerId,
        RunnerDestination destination)
    {
        var occupant = destinations.FirstOrDefault(item => item.Value == destination);
        if (occupant.Key != 0)
        {
            if (occupant.Key == batterPlayerId || destination == RunnerDestination.First)
                return false;
            destinations.Remove(occupant.Key);
            var previousBase = (RunnerDestination)((int)destination - 1);
            if (!TryPlaceOnBase(destinations, batterPlayerId, occupant.Key, previousBase))
                return false;
        }
        destinations[playerId] = destination;
        return true;
    }
}
