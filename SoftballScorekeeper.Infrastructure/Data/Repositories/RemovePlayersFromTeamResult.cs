namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record RemovePlayersFromTeamResult(
    IReadOnlyList<int> RequestedPlayerIds,
    IReadOnlyList<int> RemovedPlayerIds,
    IReadOnlyList<int> AlreadyInactivePlayerIds,
    IReadOnlyList<int> NotOnTeamPlayerIds,
    IReadOnlyList<int> IgnoredPlayerIds)
{
    public int SuccessfulRemovalCount => RemovedPlayerIds.Count;
}
