namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record CreateGamesResult(
    int RequestedGameCount,
    int SeasonId,
    IReadOnlyList<int> CreatedGameIds,
    CreateGamesFailure FailureReason,
    string? ExceptionType = null,
    string? ExceptionMessage = null)
{
    public bool IsCompleteSuccess =>
        RequestedGameCount > 0
        && SeasonId > 0
        && CreatedGameIds.Count == RequestedGameCount
        && FailureReason == CreateGamesFailure.None;
}

public enum CreateGamesFailure
{
    None,
    InvalidRequest,
    InvalidTeamOrSeason,
    TeamYearMismatch,
    TeamYearFinalized,
    DuplicateSchedule,
    Persistence,
}
