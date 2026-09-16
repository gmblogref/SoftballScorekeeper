using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record LowManGameNight(
    DateTime PlayingDate,
    int CompletedGameCount,
    int TotalGameCount)
{
    public int IncompleteGameCount => TotalGameCount - CompletedGameCount;
}

public sealed record LowManCalculationResult(
    LowManCalculationFailure FailureReason,
    DateTime PlayingDate,
    int CompletedGameCount,
    int IncompleteGameCount,
    IReadOnlyList<LowManCalculationWinner> Winners)
{
    public bool IsSuccess => FailureReason == LowManCalculationFailure.None;

    public IReadOnlyList<int> PlayerIds => Winners.Select(item => item.PlayerId).ToArray();
}

public sealed record LowManCalculationWinner(int PlayerId, string DisplayName);

public enum LowManCalculationFailure
{
    None = 0,
    InvalidRequest = 1,
    TeamNotFound = 2,
    InvalidCompletedGameData = 3,
    PersistenceError = 4,
    TeamYearFinalized = 5,
}

public sealed class LowManCalculationRepository(AppDatabase database)
{
    public async Task<IReadOnlyList<LowManCalculationWinner>?>
        GetPersistedAutomaticWinnersAsync(int teamId, DateTime playingDate)
    {
        if (teamId <= 0 || playingDate == default)
            return null;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        if (await connection.FindAsync<Team>(teamId) is null)
            return null;

        var date = playingDate.Date;
        var winnerIds = (await connection.Table<BeerLedgerEntry>()
                .Where(item => item.TeamId == teamId
                    && item.RuleType == BeerRuleType.LowMan
                    && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
                .ToListAsync())
            .Where(item => item.PlayingDate?.Date == date)
            .OrderBy(item => item.PlayerId)
            .Select(item => item.PlayerId)
            .Distinct()
            .ToArray();
        if (winnerIds.Length == 0)
            return [];

        var players = (await connection.Table<Player>().ToListAsync())
            .Where(item => winnerIds.Contains(item.Id))
            .ToDictionary(item => item.Id);
        if (winnerIds.Any(playerId => !players.ContainsKey(playerId)))
            throw new InvalidOperationException(
                "A persisted Low Man Player identity could not be resolved.");

        return winnerIds
            .Select(playerId => new LowManCalculationWinner(
                playerId,
                FormatPlayerName(players[playerId])))
            .ToArray();
    }

    public async Task<IReadOnlyList<LowManGameNight>?> GetGameNightsAsync(int teamId)
    {
        if (teamId <= 0)
            return null;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        if (await connection.FindAsync<Team>(teamId) is null)
            return null;

        var games = await connection.Table<Game>()
            .Where(item => item.TeamId == teamId)
            .ToListAsync();
        var gameIds = games.Select(item => item.Id).ToHashSet();
        var sessions = (await connection.Table<GameScoringSession>().ToListAsync())
            .Where(item => gameIds.Contains(item.GameId))
            .ToDictionary(item => item.GameId);

        return games
            .GroupBy(item => item.ScheduledDate.Date)
            .Select(group => new LowManGameNight(
                group.Key,
                group.Count(game => sessions.TryGetValue(game.Id, out var session)
                    && session.Status == GameScoringStatus.Completed),
                group.Count()))
            .OrderByDescending(item => item.PlayingDate)
            .ToArray();
    }

    public async Task<LowManCalculationResult> CalculateAsync(
        int teamId,
        DateTime playingDate)
    {
        var date = playingDate.Date;
        if (teamId <= 0 || playingDate == default)
            return Failure(LowManCalculationFailure.InvalidRequest, date);

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = Failure(LowManCalculationFailure.PersistenceError, date);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var team = transaction.Find<Team>(teamId);
                if (team is null)
                {
                    result = Failure(LowManCalculationFailure.TeamNotFound, date);
                    return;
                }
                if (team.IsYearOver)
                {
                    result = Failure(LowManCalculationFailure.TeamYearFinalized, date);
                    return;
                }

                var games = transaction.Table<Game>()
                    .Where(item => item.TeamId == teamId)
                    .ToList()
                    .Where(item => item.ScheduledDate.Date == date)
                    .ToArray();
                var gameIds = games.Select(item => item.Id).ToHashSet();
                var sessions = transaction.Table<GameScoringSession>()
                    .ToList()
                    .Where(item => gameIds.Contains(item.GameId))
                    .ToArray();
                var completedSessions = sessions
                    .Where(item => item.Status == GameScoringStatus.Completed)
                    .ToArray();
                var completedSessionIds = completedSessions.Select(item => item.Id).ToHashSet();
                var appearances = transaction.Table<GamePlateAppearance>()
                    .ToList()
                    .Where(item => completedSessionIds.Contains(item.GameScoringSessionId))
                    .OrderBy(item => item.GameScoringSessionId)
                    .ThenBy(item => item.PlateAppearanceNumber)
                    .ToArray();

                if (appearances.Any(item => item.Result is null))
                {
                    result = Failure(
                        LowManCalculationFailure.InvalidCompletedGameData,
                        date,
                        completedSessions.Length,
                        games.Length - completedSessions.Length);
                    return;
                }

                var statLines = BattingStatisticsCalculator.Calculate(
                        appearances.Select(item =>
                            new BattingOutcome(item.PlayerId, item.Result!.Value)))
                    .Where(item => item.PlateAppearances >= 1)
                    .ToArray();
                var setting = BeerRuleConfigurationRepository.ResolveTeamSettings(
                        transaction.Table<TeamBeerRuleConfiguration>()
                            .Where(item => item.TeamId == teamId)
                            .ToList())
                    .Single(item => item.RuleType == BeerRuleType.LowMan);
                var winnerIds = setting.IsEnabled && statLines.Length > 0
                    ? statLines
                        .Where(item => item.OnBasePercentage
                            == statLines.Min(candidate => candidate.OnBasePercentage))
                        .Select(item => item.PlayerId)
                        .Order()
                        .ToArray()
                    : [];
                var winners = winnerIds.Select(playerId =>
                {
                    var player = transaction.Find<Player>(playerId)
                        ?? throw new InvalidOperationException(
                            "Low Man Player identity could not be resolved.");
                    return new LowManCalculationWinner(playerId, FormatPlayerName(player));
                }).ToArray();

                ReconcileAutomaticResults(
                    transaction, teamId, date, winnerIds, setting.Amount);
                result = new(
                    LowManCalculationFailure.None,
                    date,
                    completedSessions.Length,
                    games.Length - completedSessions.Length,
                    winners);
            });
        }
        catch
        {
            return Failure(LowManCalculationFailure.PersistenceError, date);
        }

        return result;
    }

    private static string FormatPlayerName(Player player) => string.Join(
        " ",
        new[] { player.FirstName, player.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

    private static void ReconcileAutomaticResults(
        SQLiteConnection transaction,
        int teamId,
        DateTime playingDate,
        IReadOnlyCollection<int> winnerIds,
        int amount)
    {
        var existing = transaction.Table<BeerLedgerEntry>()
            .Where(item => item.TeamId == teamId
                && item.RuleType == BeerRuleType.LowMan
                && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
            .ToList()
            .Where(item => item.PlayingDate?.Date == playingDate)
            .ToArray();
        if (existing.Length == winnerIds.Count
            && existing.All(item => winnerIds.Contains(item.PlayerId)
                && item.Amount == amount
                && item.GameId is null
                && item.GameScoringSessionId is null
                && item.GamePlateAppearanceId is null))
            return;

        foreach (var entry in existing)
            if (transaction.Delete(entry) != 1)
                throw new InvalidOperationException("Low Man reconciliation delete failed.");

        foreach (var playerId in winnerIds)
        {
            if (transaction.Find<Player>(playerId) is null)
                throw new InvalidOperationException("Low Man Player identity could not be resolved.");
            if (transaction.Insert(new BeerLedgerEntry
                {
                    TeamId = teamId,
                    PlayerId = playerId,
                    RuleType = BeerRuleType.LowMan,
                    Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                    PlayingDate = playingDate,
                    Amount = amount,
                }) != 1)
                throw new InvalidOperationException("Low Man consequence insertion failed.");
        }
    }

    private static LowManCalculationResult Failure(
        LowManCalculationFailure failure,
        DateTime playingDate,
        int completedGameCount = 0,
        int incompleteGameCount = 0) =>
        new(failure, playingDate, completedGameCount, incompleteGameCount, []);
}
