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

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var placeholders = string.Join(",", requestedIds.Select(_ => "?"));
        var teams = await connection.QueryAsync<Team>(
            $"SELECT * FROM Team WHERE Id IN ({placeholders})",
            requestedIds.Cast<object>().ToArray()
        );
        var teamsById = teams.ToDictionary(team => team.Id);
        var seasons = await connection.QueryAsync<Season>(
            $"SELECT * FROM Season WHERE TeamId IN ({placeholders})",
            requestedIds.Cast<object>().ToArray()
        );
        var records = requestedIds.ToDictionary(
            teamId => teamId,
            teamId =>
            {
                var year = teamsById.TryGetValue(teamId, out var team)
                    ? team.Year
                    : (int?)null;
                return new TeamYearRecord(year, 0, 0, 0, 0);
            }
        );

        var teamYearSeasonIds = seasons
            .Where(season =>
                teamsById.TryGetValue(season.TeamId, out var team)
                && season.Year == team.Year
            )
            .Select(season => season.Id)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (teamYearSeasonIds.Length == 0)
            return records;

        placeholders = string.Join(",", teamYearSeasonIds.Select(_ => "?"));
        var games = await connection.QueryAsync<Game>(
            $"""
            SELECT Game.*
            FROM Game
            INNER JOIN Season ON Season.Id = Game.SeasonId
                AND Season.TeamId = Game.TeamId
            WHERE Game.SeasonId IN ({placeholders})
            """,
            teamYearSeasonIds.Cast<object>().ToArray()
        );
        await operation.DisposeAsync();
        var sessions = await scoringSessionRepository.GetForGamesAsync(
            games.Select(game => game.Id)
        );

        foreach (var gamesForTeam in games.GroupBy(game => game.TeamId))
        {
            var wins = 0;
            var losses = 0;
            var ties = 0;
            var gamesPlayed = 0;
            foreach (var game in gamesForTeam)
            {
                sessions.TryGetValue(game.Id, out var session);
                var summary = GameScheduleSummary.FromSession(session);
                if (summary.Status != GameScheduleStatus.Final)
                    continue;

                gamesPlayed++;
                if (summary.Result == TeamGameResult.Win)
                    wins++;
                else if (summary.Result == TeamGameResult.Loss)
                    losses++;
                else if (summary.Result == TeamGameResult.Tie)
                    ties++;
            }

            if (records.TryGetValue(gamesForTeam.Key, out var record))
                records[gamesForTeam.Key] = record with
                {
                    GamesPlayed = gamesPlayed,
                    Wins = wins,
                    Losses = losses,
                    Ties = ties,
                };
        }

        return records;
    }
}
