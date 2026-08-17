using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum CompletePlateAppearanceFailure
{
    None,
    InvalidRequest,
    SessionNotFound,
    GameCompleted,
    InvalidScoringState,
    Persistence,
}

public sealed record CompletePlateAppearanceResult(
    GamePlateAppearance? PlateAppearance,
    int CurrentBatterPosition,
    CompletePlateAppearanceFailure FailureReason)
{
    public bool IsSuccess => FailureReason == CompletePlateAppearanceFailure.None
        && PlateAppearance is not null;
}
