using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class TeamRecordRepository
{
    private readonly AppDatabase database;
    private readonly GameScoringSessionRepository scoringSessionRepository;

    public TeamRecordRepository(
        AppDatabase database,
        GameScoringSessionRepository scoringSessionRepository
    )
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(scoringSessionRepository);
        this.database = database;
        this.scoringSessionRepository = scoringSessionRepository;
    }

    public async Task<IReadOnlyDictionary<int, TeamYearRecord>> GetForTeamsAsync(
        IEnumerable<int>? teamIds
    )
    {
        var requestedIds = (teamIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (requestedIds.Length == 0)
            return new Dictionary<int, TeamYearRecord>();

        var connection = await database.GetConnectionAsync();
        var placeholders = string.Join(",", requestedIds.Select(_ => "?"));
        var seasons = await connection.QueryAsync<Season>(
            $"SELECT * FROM Season WHERE TeamId IN ({placeholders})",
            requestedIds.Cast<object>().ToArray()
        );
        var latestYears = seasons
            .GroupBy(season => season.TeamId)
            .ToDictionary(group => group.Key, group => group.Max(season => season.Year));
        var records = requestedIds.ToDictionary(
            teamId => teamId,
            teamId =>
            {
                var year = latestYears.TryGetValue(teamId, out var latestYear)
                    ? latestYear
                    : (int?)null;
                return new TeamYearRecord(year, 0, 0);
            }
        );

        var latestSeasonIds = seasons
            .Where(season => latestYears.GetValueOrDefault(season.TeamId) == season.Year)
            .Select(season => season.Id)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (latestSeasonIds.Length == 0)
            return records;

        placeholders = string.Join(",", latestSeasonIds.Select(_ => "?"));
        var games = await connection.QueryAsync<Game>(
            $"""
            SELECT Game.*
            FROM Game
            INNER JOIN Season ON Season.Id = Game.SeasonId
                AND Season.TeamId = Game.TeamId
            WHERE Game.SeasonId IN ({placeholders})
            """,
            latestSeasonIds.Cast<object>().ToArray()
        );
        var sessions = await scoringSessionRepository.GetForGamesAsync(
            games.Select(game => game.Id)
        );

        foreach (var gamesForTeam in games.GroupBy(game => game.TeamId))
        {
            var wins = 0;
            var losses = 0;
            foreach (var game in gamesForTeam)
            {
                sessions.TryGetValue(game.Id, out var session);
                var summary = GameScheduleSummary.FromSession(session);
                if (summary.Status != GameScheduleStatus.Final)
                    continue;

                if (summary.Result == TeamGameResult.Win)
                    wins++;
                else if (summary.Result == TeamGameResult.Loss)
                    losses++;
            }

            if (records.TryGetValue(gamesForTeam.Key, out var record))
                records[gamesForTeam.Key] = record with { Wins = wins, Losses = losses };
        }

        return records;
    }
}
