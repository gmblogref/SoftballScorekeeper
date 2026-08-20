namespace SoftballScorekeeper.Core.Models;

public sealed record BaseRunnerParticipant(
    int PlayerId,
    RunnerStartingLocation StartingLocation,
    RunnerDestination Destination);

public sealed class BaseRunnerResolutionState
{
    public BaseRunnerResolutionState(IEnumerable<BaseRunnerParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var ordered = participants.ToArray();
        if (ordered.Length is < 1 or > 4
            || ordered.Any(item => item.PlayerId <= 0
                || !Enum.IsDefined(item.StartingLocation)
                || !Enum.IsDefined(item.Destination))
            || ordered.Select(item => item.PlayerId).Distinct().Count() != ordered.Length
            || ordered.Select(item => item.StartingLocation).Distinct().Count() != ordered.Length
            || ordered.Count(item => item.StartingLocation == RunnerStartingLocation.Home) != 1)
            throw new ArgumentException(
                "A resolver state requires one to four unique participants, including one batter-runner.",
                nameof(participants));

        Array.Sort(ordered, static (left, right) =>
            ((int)right.StartingLocation).CompareTo((int)left.StartingLocation));
        Participants = Array.AsReadOnly(ordered);
    }

    public IReadOnlyList<BaseRunnerParticipant> Participants { get; }
}

public sealed class BaseRunnerResolutionResult
{
    private BaseRunnerResolutionResult(
        bool isSuccess, IReadOnlyList<BaseRunnerParticipant> participants)
    {
        IsSuccess = isSuccess;
        Participants = participants;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<BaseRunnerParticipant> Participants { get; }

    internal static BaseRunnerResolutionResult Rejected() => new(false, []);

    internal static BaseRunnerResolutionResult Resolved(
        IReadOnlyList<BaseRunnerParticipant> participants) => new(true, participants);
}

public static class BaseRunnerResolver
{
    private static readonly RunnerDestination[] CandidateDestinationOrder =
    [
        RunnerDestination.First,
        RunnerDestination.Second,
        RunnerDestination.Third,
        RunnerDestination.Scores,
        RunnerDestination.Out,
    ];

    public static BaseRunnerResolutionResult ResolveCorrection(
        BaseRunnerResolutionState state,
        int participantPlayerId,
        RunnerDestination destination)
    {
        if (state is null || participantPlayerId <= 0 || !Enum.IsDefined(destination)
            || !IsLegal(state.Participants))
            return BaseRunnerResolutionResult.Rejected();

        var selectedIndex = FindParticipantIndex(state.Participants, participantPlayerId);
        if (selectedIndex < 0) return BaseRunnerResolutionResult.Rejected();

        var candidate = new RunnerDestination[state.Participants.Count];
        RunnerDestination[]? winner = null;
        Search(0);
        if (winner is null) return BaseRunnerResolutionResult.Rejected();

        var resolved = new BaseRunnerParticipant[state.Participants.Count];
        for (var index = 0; index < resolved.Length; index++)
            resolved[index] = state.Participants[index] with { Destination = winner[index] };
        return BaseRunnerResolutionResult.Resolved(Array.AsReadOnly(resolved));

        void Search(int participantIndex)
        {
            if (participantIndex == candidate.Length)
            {
                if (IsLegal(candidate)
                    && (winner is null || IsRankedBefore(
                        candidate, winner, state.Participants, selectedIndex)))
                    winner = (RunnerDestination[])candidate.Clone();
                return;
            }

            if (participantIndex == selectedIndex)
            {
                candidate[participantIndex] = destination;
                Search(participantIndex + 1);
                return;
            }

            var current = state.Participants[participantIndex].Destination;
            if (current == RunnerDestination.Out)
            {
                candidate[participantIndex] = RunnerDestination.Out;
                Search(participantIndex + 1);
                return;
            }

            for (var destinationIndex = 0;
                destinationIndex < CandidateDestinationOrder.Length - 1;
                destinationIndex++)
            {
                candidate[participantIndex] = CandidateDestinationOrder[destinationIndex];
                Search(participantIndex + 1);
            }
        }
    }

    public static IReadOnlyList<RunnerDestination> GetAvailableDestinations(
        BaseRunnerResolutionState state, int participantPlayerId)
    {
        if (state is null || participantPlayerId <= 0) return [];
        var available = new List<RunnerDestination>(CandidateDestinationOrder.Length);
        foreach (var destination in CandidateDestinationOrder)
            if (ResolveCorrection(state, participantPlayerId, destination).IsSuccess)
                available.Add(destination);
        return available.AsReadOnly();
    }

    private static int FindParticipantIndex(
        IReadOnlyList<BaseRunnerParticipant> participants, int playerId)
    {
        for (var index = 0; index < participants.Count; index++)
            if (participants[index].PlayerId == playerId)
                return index;
        return -1;
    }

    private static bool IsLegal(IReadOnlyList<BaseRunnerParticipant> participants)
    {
        var destinations = new RunnerDestination[participants.Count];
        for (var index = 0; index < participants.Count; index++)
            destinations[index] = participants[index].Destination;
        return IsLegal(destinations);
    }

    private static bool IsLegal(IReadOnlyList<RunnerDestination> destinations)
    {
        var occupiedSafeBases = new bool[3];
        for (var index = 0; index < destinations.Count; index++)
        {
            var destination = destinations[index];
            if (!Enum.IsDefined(destination)) return false;
            if (destination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third)
            {
                var baseIndex = (int)destination - 1;
                if (occupiedSafeBases[baseIndex]) return false;
                occupiedSafeBases[baseIndex] = true;
            }
        }

        for (var leadIndex = 0; leadIndex < destinations.Count; leadIndex++)
        {
            if (destinations[leadIndex] == RunnerDestination.Out) continue;
            for (var trailIndex = leadIndex + 1;
                trailIndex < destinations.Count;
                trailIndex++)
            {
                if (destinations[trailIndex] == RunnerDestination.Out) continue;
                if (PhysicalRank(destinations[leadIndex])
                    < PhysicalRank(destinations[trailIndex]))
                    return false;
            }
        }
        return true;
    }

    private static bool IsRankedBefore(
        IReadOnlyList<RunnerDestination> candidate,
        IReadOnlyList<RunnerDestination> currentWinner,
        IReadOnlyList<BaseRunnerParticipant> current,
        int selectedIndex)
    {
        var candidateChanges = CountNonSelectedChanges(candidate, current, selectedIndex);
        var winnerChanges = CountNonSelectedChanges(currentWinner, current, selectedIndex);
        if (candidateChanges != winnerChanges) return candidateChanges < winnerChanges;

        var candidateDisplacement = TotalPhysicalDisplacement(candidate, current, selectedIndex);
        var winnerDisplacement = TotalPhysicalDisplacement(currentWinner, current, selectedIndex);
        if (candidateDisplacement != winnerDisplacement)
            return candidateDisplacement < winnerDisplacement;

        for (var index = 0; index < current.Count; index++)
        {
            if (index == selectedIndex) continue;
            var candidateChanged = candidate[index] != current[index].Destination;
            var winnerChanged = currentWinner[index] != current[index].Destination;
            if (candidateChanged != winnerChanged) return !candidateChanged;
        }

        for (var index = 0; index < current.Count; index++)
        {
            var destinationComparison = StableDestinationRank(candidate[index])
                .CompareTo(StableDestinationRank(currentWinner[index]));
            if (destinationComparison != 0) return destinationComparison < 0;
        }
        return false;
    }

    private static int CountNonSelectedChanges(
        IReadOnlyList<RunnerDestination> candidate,
        IReadOnlyList<BaseRunnerParticipant> current,
        int selectedIndex)
    {
        var changes = 0;
        for (var index = 0; index < current.Count; index++)
            if (index != selectedIndex && candidate[index] != current[index].Destination)
                changes++;
        return changes;
    }

    private static int TotalPhysicalDisplacement(
        IReadOnlyList<RunnerDestination> candidate,
        IReadOnlyList<BaseRunnerParticipant> current,
        int selectedIndex)
    {
        var displacement = 0;
        for (var index = 0; index < current.Count; index++)
        {
            if (index == selectedIndex) continue;
            displacement += Math.Abs(PhysicalRank(candidate[index])
                - PhysicalRank(current[index].Destination));
        }
        return displacement;
    }

    private static int PhysicalRank(RunnerDestination destination) => destination switch
    {
        RunnerDestination.First => 1,
        RunnerDestination.Second => 2,
        RunnerDestination.Third => 3,
        RunnerDestination.Scores => 4,
        RunnerDestination.Out => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(destination)),
    };

    private static int StableDestinationRank(RunnerDestination destination) => destination switch
    {
        RunnerDestination.First => 0,
        RunnerDestination.Second => 1,
        RunnerDestination.Third => 2,
        RunnerDestination.Scores => 3,
        RunnerDestination.Out => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(destination)),
    };
}
