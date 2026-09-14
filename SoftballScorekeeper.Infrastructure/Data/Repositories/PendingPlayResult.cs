using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum PendingPlayFailure
{
    None,
    InvalidRequest,
    SessionNotFound,
    GameCompleted,
    InvalidScoringState,
    DuplicateDestination,
    TeamYearFinalized,
    Persistence,
}

public sealed record PendingPlayResult(
    PendingScoringPlay? PendingPlay,
    PendingPlayFailure FailureReason)
{
    public bool IsSuccess => FailureReason == PendingPlayFailure.None;
}

public sealed record RunnerDestinationChoicesResult(
    IReadOnlyList<RunnerDestination> Destinations,
    PendingPlayFailure FailureReason)
{
    public bool IsSuccess => FailureReason == PendingPlayFailure.None;
}

public sealed record RunnerOutcomeChoicesResult(
    IReadOnlyList<RunnerOutcomeChoice> Choices,
    PendingPlayFailure FailureReason,
    RunnerOutcomeChoice? CurrentOutcome = null)
{
    public bool IsSuccess => FailureReason == PendingPlayFailure.None;
}
