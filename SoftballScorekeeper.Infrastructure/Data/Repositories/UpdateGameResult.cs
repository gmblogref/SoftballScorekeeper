namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record UpdateGameResult(int GameId, int SeasonId, UpdateGameFailure FailureReason)
{
    public bool IsSuccess => FailureReason == UpdateGameFailure.None;
}

public enum UpdateGameFailure
{
    None,
    InvalidRequest,
    InvalidTeamOrGame,
    InvalidSeason,
    DuplicateSchedule,
    Persistence,
}
