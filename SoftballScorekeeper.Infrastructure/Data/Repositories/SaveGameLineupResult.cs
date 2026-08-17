namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum SaveGameLineupFailure
{
    None,
    InvalidGame,
    TeamMismatch,
    EmptySelection,
    IneligiblePlayers,
    LineupAlreadyExists,
    ScoringStarted,
    Persistence,
}

public sealed record SaveGameLineupResult(
    int GameId,
    IReadOnlyList<int> SavedPlayerIds,
    IReadOnlyList<int> IneligiblePlayerIds,
    SaveGameLineupFailure FailureReason)
{
    public bool IsSuccess => FailureReason == SaveGameLineupFailure.None;
}
