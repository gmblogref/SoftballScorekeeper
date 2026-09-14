using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record TeamYearSummaryProjection(
    Team Team,
    TeamYearRecord Record,
    TeamBattingStatisticsProjection Batting,
    IReadOnlyList<TeamBeerPlayerSummary> Beer
);

public sealed class TeamYearSummaryRepository(
    TeamRepository teamRepository,
    TeamRecordRepository recordRepository,
    TeamBattingStatisticsRepository battingRepository,
    BeerAccountingRepository beerRepository
)
{
    public async Task<TeamYearSummaryProjection?> GetAsync(int teamId)
    {
        if (teamId <= 0)
            return null;

        var team = await teamRepository.GetActiveByIdAsync(teamId);
        if (team is null)
            return null;
        var eligibility = await teamRepository.GetYearSummaryEligibilityAsync(teamId);
        if (eligibility?.CanViewSummary != true)
            return null;

        var records = await recordRepository.GetForTeamsAsync([teamId]);
        var batting = await battingRepository.GetAsync(teamId);
        var beer = await beerRepository.GetTeamLifetimeAsync(teamId);
        if (batting is null || beer is null || !records.TryGetValue(teamId, out var record))
            return null;

        return new(team, record, batting, beer);
    }
}
