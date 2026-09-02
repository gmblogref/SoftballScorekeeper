namespace SoftballScorekeeper.Core.Models;

public enum GameScheduleStatus
{
    Upcoming,
    InProgress,
    Final,
}

public enum TeamGameResult
{
    Win,
    Loss,
    Tie,
}

public sealed record GameScheduleSummary(
    GameScheduleStatus Status,
    TeamGameResult? Result,
    int? VisitorScore,
    int? HomeScore)
{
    public static GameScheduleSummary FromSession(GameScoringSession? session)
    {
        if (session is null)
            return new(GameScheduleStatus.Upcoming, null, null, null);

        return session.Status switch
        {
            GameScoringStatus.InProgress =>
                new(GameScheduleStatus.InProgress, null, null, null),
            GameScoringStatus.Completed => CreateFinal(session),
            _ => throw new ArgumentOutOfRangeException(
                nameof(session), session.Status, "The scoring status is not supported."),
        };
    }

    private static GameScheduleSummary CreateFinal(GameScoringSession session)
    {
        var (ourScore, opponentScore) = session.TeamSide switch
        {
            TeamGameSide.Home => (session.HomeScore, session.VisitorScore),
            TeamGameSide.Away => (session.VisitorScore, session.HomeScore),
            _ => throw new ArgumentOutOfRangeException(
                nameof(session), session.TeamSide, "The Team side is not supported."),
        };
        var result = ourScore.CompareTo(opponentScore) switch
        {
            > 0 => TeamGameResult.Win,
            < 0 => TeamGameResult.Loss,
            _ => TeamGameResult.Tie,
        };
        return new(
            GameScheduleStatus.Final,
            result,
            session.VisitorScore,
            session.HomeScore);
    }
}
