using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum CopyTeamToNewYearFailure
{
    None,
    SourceNotFound,
    SourceYearFinalized,
    InvalidSourceYear,
    DestinationExists,
    PersistenceError,
}

public sealed record CopyTeamToNewYearResult(
    CopyTeamToNewYearFailure FailureReason,
    Team? DestinationTeam = null,
    int? ExistingTeamId = null
)
{
    public bool IsSuccess => FailureReason == CopyTeamToNewYearFailure.None;
}
