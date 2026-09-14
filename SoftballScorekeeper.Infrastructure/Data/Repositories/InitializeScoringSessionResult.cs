using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum InitializeScoringSessionFailure
{
    None,
    InvalidRequest,
    InvalidTeamOrGame,
    InvalidLineup,
    GameCompleted,
    TeamYearFinalized,
    Persistence,
}

public sealed record InitializeScoringSessionResult(
    GameScoringSession? Session,
    bool WasCreated,
    InitializeScoringSessionFailure FailureReason)
{
    public bool IsSuccess => FailureReason == InitializeScoringSessionFailure.None && Session is not null;
}
