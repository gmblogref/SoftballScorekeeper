using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class GameScheduleSummaryTests
{
    [Fact]
    public void Missing_scoring_session_is_upcoming_without_score_or_result()
    {
        var summary = GameScheduleSummary.FromSession(null);

        Assert.Equal(GameScheduleStatus.Upcoming, summary.Status);
        Assert.Null(summary.Result);
        Assert.Null(summary.VisitorScore);
        Assert.Null(summary.HomeScore);
    }

    [Fact]
    public void Active_scoring_session_is_in_progress_without_final_result()
    {
        var summary = GameScheduleSummary.FromSession(new GameScoringSession
        {
            Status = GameScoringStatus.InProgress,
            TeamSide = TeamGameSide.Away,
            VisitorScore = 5,
            HomeScore = 4,
        });

        Assert.Equal(GameScheduleStatus.InProgress, summary.Status);
        Assert.Null(summary.Result);
        Assert.Null(summary.VisitorScore);
        Assert.Null(summary.HomeScore);
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 7, 10, TeamGameResult.Win)]
    [InlineData(TeamGameSide.Home, 10, 7, TeamGameResult.Loss)]
    [InlineData(TeamGameSide.Home, 8, 8, TeamGameResult.Tie)]
    [InlineData(TeamGameSide.Away, 10, 7, TeamGameResult.Win)]
    [InlineData(TeamGameSide.Away, 7, 10, TeamGameResult.Loss)]
    [InlineData(TeamGameSide.Away, 8, 8, TeamGameResult.Tie)]
    public void Completed_session_preserves_visitor_home_score_and_uses_team_perspective(
        TeamGameSide teamSide,
        int visitorScore,
        int homeScore,
        TeamGameResult expectedResult)
    {
        var summary = GameScheduleSummary.FromSession(new GameScoringSession
        {
            Status = GameScoringStatus.Completed,
            TeamSide = teamSide,
            VisitorScore = visitorScore,
            HomeScore = homeScore,
        });

        Assert.Equal(GameScheduleStatus.Final, summary.Status);
        Assert.Equal(expectedResult, summary.Result);
        Assert.Equal(visitorScore, summary.VisitorScore);
        Assert.Equal(homeScore, summary.HomeScore);
    }
}
