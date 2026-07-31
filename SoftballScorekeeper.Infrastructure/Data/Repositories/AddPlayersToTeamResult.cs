namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record AddPlayersToTeamResult(
    IReadOnlyList<int> RequestedPlayerIds,
    IReadOnlyList<int> EligiblePlayerIds,
    IReadOnlyList<int> InsertedPlayerIds,
    IReadOnlyList<int> ReactivatedPlayerIds,
    IReadOnlyList<int> AlreadyActivePlayerIds,
    IReadOnlyList<int> IgnoredPlayerIds)
{
    public int SuccessfullyActiveMembershipCount =>
        InsertedPlayerIds.Count + ReactivatedPlayerIds.Count + AlreadyActivePlayerIds.Count;
}
