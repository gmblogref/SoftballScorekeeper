namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record ScoringUndoRedoAvailability(bool CanUndo, bool CanRedo);

public sealed record ScoringUndoRedoResult(
    bool IsSuccess,
    ScoringUndoRedoFailure FailureReason,
    ScoringUndoRedoAvailability Availability
);

public enum ScoringUndoRedoFailure
{
    None,
    InvalidRequest,
    SessionNotFound,
    GameCompleted,
    UnresolvedScoringState,
    NothingToUndo,
    NothingToRedo,
    InvalidHistory,
    Persistence,
}
