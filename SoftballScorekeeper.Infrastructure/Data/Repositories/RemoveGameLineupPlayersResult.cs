namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record RemoveGameLineupPlayersResult(
    int GameId,
    IReadOnlyList<int> RequestedPlayerIds,
    IReadOnlyList<int> RemovedPlayerIds,
    IReadOnlyList<int> MissingPlayerIds,
    IReadOnlyList<int> IgnoredPlayerIds,
    bool GameExists)
{
    public int SuccessfulRemovalCount => RemovedPlayerIds.Count;
}
