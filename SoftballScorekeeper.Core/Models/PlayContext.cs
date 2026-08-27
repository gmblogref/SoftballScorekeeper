namespace SoftballScorekeeper.Core.Models;

public sealed class PlayContext
{
    public PlayContext(
        int batterPlayerId,
        BatterResult result,
        int currentOuts,
        IEnumerable<ResolvedPlayParticipantOutcome> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (batterPlayerId <= 0 || !Enum.IsDefined(result) || currentOuts is < 0 or > 2)
            throw new ArgumentException("The play context has invalid identifying state.");

        var supplied = participants.ToArray();
        var canonicalState = new BaseRunnerResolutionState(supplied.Select(item =>
            new BaseRunnerParticipant(
                item.PlayerId, item.StartingLocation, item.Destination)));
        var byPlayerId = supplied.ToDictionary(item => item.PlayerId);
        var canonical = canonicalState.Participants
            .Select(item => byPlayerId[item.PlayerId])
            .ToArray();
        if (canonical.Single(item =>
                item.StartingLocation == RunnerStartingLocation.Home).PlayerId
            != batterPlayerId)
            throw new ArgumentException(
                "The play context batter does not match its Home participant.");

        BatterPlayerId = batterPlayerId;
        Result = result;
        CurrentOuts = currentOuts;
        Participants = Array.AsReadOnly(canonical);
        OccupiedStartingLocations = canonical
            .Where(item => item.StartingLocation != RunnerStartingLocation.Home)
            .Select(item => item.StartingLocation)
            .ToHashSet();
    }

    public int BatterPlayerId { get; }

    public BatterResult Result { get; }

    public int CurrentOuts { get; }

    public IReadOnlyList<ResolvedPlayParticipantOutcome> Participants { get; }

    public IReadOnlySet<RunnerStartingLocation> OccupiedStartingLocations { get; }

    public PlayContext WithParticipants(
        IEnumerable<ResolvedPlayParticipantOutcome> participants) =>
        new(BatterPlayerId, Result, CurrentOuts, participants);

    public PlayContext WithResultAndParticipants(
        BatterResult result,
        IEnumerable<ResolvedPlayParticipantOutcome> participants) =>
        new(BatterPlayerId, result, CurrentOuts, participants);
}

public enum PlayResolutionStatus
{
    Rejected,
    AwaitingDecision,
    ReadyForNextBatter,
    AutoComplete,
    CommitImmediately,
    Completed,
}

public enum PlayDecisionRequirement
{
    None,
    ParticipantOutcome,
    RetirementSelection,
    ThirdOutRuling,
}

public enum PlayResolutionFailure
{
    None,
    InvalidContext,
    ResultUnavailable,
    IllegalArrangement,
    InvalidDecision,
}

public sealed record PlayResolutionResult(
    PlayContext? Context,
    PlayResolutionStatus Status,
    PlayDecisionRequirement DecisionRequirement,
    IReadOnlyList<int> DecisionPlayerIds,
    int RequiredRetirementCount,
    ThirdOutRunRulingRequirement ThirdOutRulingRequirement,
    PlayResolutionFailure FailureReason)
{
    public bool IsAccepted => FailureReason == PlayResolutionFailure.None
        && Context is not null
        && Status != PlayResolutionStatus.Rejected;

    public bool RequiresDecision => DecisionRequirement != PlayDecisionRequirement.None;
}
