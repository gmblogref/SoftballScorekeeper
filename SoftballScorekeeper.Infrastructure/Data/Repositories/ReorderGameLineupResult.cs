namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum ReorderGameLineupFailure
{
    None,
    InvalidRequest,
    InvalidGame,
    LineupMismatch,
    ScoringStarted,
    Persistence,
}

public sealed record ReorderGameLineupResult(
    int GameId,
    IReadOnlyList<int> OrderedPlayerIds,
    ReorderGameLineupFailure FailureReason)
{
    public bool IsSuccess => FailureReason == ReorderGameLineupFailure.None;
}
