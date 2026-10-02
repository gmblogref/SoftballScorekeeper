namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record LiveLineupChangeResult(
    bool IsSuccess,
    LiveLineupChangeFailure FailureReason,
    int CurrentBatterPosition = 0
);

public enum LiveLineupChangeFailure
{
    None,
    InvalidRequest,
    SessionNotFound,
    GameCompleted,
    PlayerIneligible,
    PlayerAlreadyActive,
    PlayerNotActive,
    LastActivePlayer,
    UnresolvedScoringState,
    TeamYearFinalized,
    Persistence,
}
