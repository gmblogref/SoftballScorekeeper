namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum CreatePlayerFailure
{
    None,
    InvalidTeam,
    InvalidGame,
    TeamMismatch,
    Persistence,
}

public sealed record CreatePlayerResult(int PlayerId, CreatePlayerFailure Failure)
{
    public bool IsSuccess => Failure == CreatePlayerFailure.None && PlayerId > 0;
}
