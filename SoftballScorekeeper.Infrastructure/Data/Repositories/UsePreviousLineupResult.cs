namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum UsePreviousLineupFailure
{
    None,
    InvalidContext,
    NoPreviousLineup,
    NoEligiblePlayers,
    ScoringStarted,
}

public sealed record UsePreviousLineupResult(
    int TargetGameId,
    int? SourceGameId,
    IReadOnlyList<int> CopiedPlayerIds,
    IReadOnlyList<int> SkippedPlayerIds,
    UsePreviousLineupFailure FailureReason)
{
    public bool IsSuccess => FailureReason == UsePreviousLineupFailure.None;
    public int SkippedPlayerCount => SkippedPlayerIds.Count;
}
