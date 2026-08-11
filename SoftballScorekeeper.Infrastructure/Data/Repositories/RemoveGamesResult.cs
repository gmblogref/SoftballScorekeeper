namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record RemoveGamesResult(
    IReadOnlyList<int> RequestedGameIds,
    IReadOnlyList<int> DeletedGameIds,
    IReadOnlyList<int> MissingGameIds,
    IReadOnlyList<int> WrongTeamGameIds,
    IReadOnlyList<int> BlockedGameIds,
    IReadOnlyList<int> IgnoredGameIds)
{
    public int SuccessfulRemovalCount => DeletedGameIds.Count;
}
