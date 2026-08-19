namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record RunRulingRequirementsResult(
    IReadOnlyList<int> PlayerIds,
    PendingPlayFailure FailureReason)
{
    public bool IsSuccess => FailureReason == PendingPlayFailure.None;
}
