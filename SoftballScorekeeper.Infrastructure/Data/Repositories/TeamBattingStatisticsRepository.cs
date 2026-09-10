using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record TeamCompletedGame(
    int GameId,
    int SeasonId,
    int ScoringSessionId,
    DateTime ScheduledDate,
    TimeSpan ScheduledTime,
    string Opponent,
    string SeasonName,
    int SeasonYear);

public sealed record TeamBattingStatisticsProjection(
    IReadOnlyList<TeamCompletedGame> CompletedGames,
    IReadOnlyList<GameBattingPlayerProjection> Players);

public enum TeamStatsGameFilter
{
    AllGames,
    LastGameNight,
    CurrentLeagueSeason,
    Custom,
}

public static class TeamStatsGameSelection
{
    public static IReadOnlyList<int> Resolve(
        TeamStatsGameFilter filter,
        IReadOnlyList<TeamCompletedGame> completedGames,
        IEnumerable<int>? customGameIds = null)
    {
        ArgumentNullException.ThrowIfNull(completedGames);
        var ordered = completedGames
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.ScheduledTime)
            .ThenBy(item => item.GameId)
            .ToArray();
        return filter switch
        {
            TeamStatsGameFilter.AllGames => ordered.Select(item => item.GameId).ToArray(),
            TeamStatsGameFilter.LastGameNight => ordered.Length == 0
                ? []
                : ordered
                    .Where(item => item.ScheduledDate.Date
                        == ordered[^1].ScheduledDate.Date)
                    .Select(item => item.GameId)
                    .ToArray(),
            TeamStatsGameFilter.CurrentLeagueSeason => ordered.Length == 0
                ? []
                : ordered
                    .Where(item => item.SeasonId == ordered[^1].SeasonId)
                    .Select(item => item.GameId)
                    .ToArray(),
            TeamStatsGameFilter.Custom => (customGameIds ?? [])
                .Distinct()
                .Where(id => ordered.Any(item => item.GameId == id))
                .OrderBy(id => Array.FindIndex(ordered, item => item.GameId == id))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(filter)),
        };
    }
}

public sealed class TeamBattingStatisticsRepository(AppDatabase database)
{
    public async Task<TeamBattingStatisticsProjection?> GetAsync(
        int teamId,
        IEnumerable<int>? selectedGameIds = null)
    {
        if (teamId <= 0)
            return null;

        var connection = await database.GetConnectionAsync();
        if (await connection.FindAsync<Team>(teamId) is null)
            return null;

        var seasons = await connection.Table<Season>()
            .Where(item => item.TeamId == teamId)
            .ToListAsync();
        if (seasons.Count == 0)
            return new([], []);

        var seasonsById = seasons.ToDictionary(item => item.Id);
        var games = (await connection.Table<Game>()
                .Where(item => item.TeamId == teamId)
                .ToListAsync())
            .Where(item => seasonsById.ContainsKey(item.SeasonId))
            .ToArray();
        var gameIds = games.Select(item => item.Id).ToHashSet();
        var sessions = (await connection.Table<GameScoringSession>().ToListAsync())
            .Where(item => gameIds.Contains(item.GameId)
                && item.Status == GameScoringStatus.Completed)
            .ToDictionary(item => item.GameId);
        var completedGames = games
            .Where(item => sessions.ContainsKey(item.Id))
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.ScheduledTime)
            .ThenBy(item => item.Id)
            .Select(item => new TeamCompletedGame(
                item.Id,
                item.SeasonId,
                sessions[item.Id].Id,
                item.ScheduledDate.Date,
                item.ScheduledTime,
                item.Opponent?.Trim() ?? string.Empty,
                seasonsById[item.SeasonId].Name.Trim(),
                seasonsById[item.SeasonId].Year))
            .ToArray();

        var selectedIds = selectedGameIds?.Distinct().ToArray()
            ?? completedGames.Select(item => item.GameId).ToArray();
        if (selectedIds.Any(id => completedGames.All(item => item.GameId != id)))
            return null;

        var selected = completedGames
            .Where(item => selectedIds.Contains(item.GameId))
            .ToArray();
        if (selected.Length == 0)
            return new(completedGames, []);

        var sessionOrder = selected
            .Select((item, index) => (item.ScoringSessionId, index))
            .ToDictionary(item => item.ScoringSessionId, item => item.index);
        var selectedSessionIds = sessionOrder.Keys.ToHashSet();
        var lineup = (await connection.Table<GameScoringLineupPlayer>().ToListAsync())
            .Where(item => selectedSessionIds.Contains(item.GameScoringSessionId))
            .OrderBy(item => sessionOrder[item.GameScoringSessionId])
            .ThenBy(item => item.BattingPosition)
            .ToArray();
        var appearances = (await connection.Table<GamePlateAppearance>().ToListAsync())
            .Where(item => selectedSessionIds.Contains(item.GameScoringSessionId))
            .OrderBy(item => sessionOrder[item.GameScoringSessionId])
            .ThenBy(item => item.PlateAppearanceNumber)
            .ToArray();
        if (appearances.Any(item => item.Result is null))
            throw new InvalidOperationException(
                "A completed Game contains a plate appearance without a final result.");

        var participantIds = lineup.Select(item => item.PlayerId)
            .Concat(appearances.Select(item => item.PlayerId))
            .Distinct()
            .ToArray();
        var statLines = BattingStatisticsCalculator.Calculate(
            participantIds,
            appearances.Select(item => new BattingOutcome(item.PlayerId, item.Result!.Value)));
        var players = (await connection.Table<Player>().ToListAsync())
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionary(item => item.Id);
        if (participantIds.Any(id => !players.ContainsKey(id)))
            throw new InvalidOperationException("Team statistics could not resolve a Player.");

        var projections = statLines.Select((line, index) => new GameBattingPlayerProjection(
            line.PlayerId,
            FormatPlayerName(players[line.PlayerId]),
            index + 1,
            line)).ToArray();
        return new(completedGames, projections);
    }

    private static string FormatPlayerName(Player player) => string.Join(
        " ",
        new[] { player.FirstName, player.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
}
