using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record CompletedGameBattingCorrection(
    int PlayerId,
    int Singles,
    int Doubles,
    int Triples,
    int HomeRuns,
    int Walks,
    int FielderChoices,
    int SacrificeFlies,
    int Outs);

public enum CompletedGameManualOverrideFailure
{
    None,
    InvalidRequest,
    GameNotCompleted,
    TeamYearFinalized,
    InvalidBattingStatistics,
    Persistence,
}

public sealed record CompletedGameManualOverrideResult(
    CompletedGameManualOverrideFailure FailureReason)
{
    public bool IsSuccess => FailureReason == CompletedGameManualOverrideFailure.None;
}

public sealed class CompletedGameManualOverrideRepository(AppDatabase database)
{
    public async Task<CompletedGameManualOverrideResult> SaveAsync(
        int gameId,
        IReadOnlyCollection<CompletedGameBattingCorrection>? corrections,
        int visitorScore,
        int homeScore)
    {
        if (gameId <= 0 || corrections is null || visitorScore < 0 || homeScore < 0
            || corrections.Any(HasNegativeValue)
            || corrections.Select(item => item.PlayerId).Distinct().Count() != corrections.Count)
            return Failure(CompletedGameManualOverrideFailure.InvalidRequest);

        await using var operation = await database.BeginOperationAsync();
        var result = Failure(CompletedGameManualOverrideFailure.Persistence);
        try
        {
            await operation.Connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadContext(transaction, gameId);
                if (context is null)
                {
                    result = Failure(CompletedGameManualOverrideFailure.GameNotCompleted);
                    return;
                }
                if (TeamYearWriteGuard.IsGameFinalized(transaction, gameId))
                {
                    result = Failure(CompletedGameManualOverrideFailure.TeamYearFinalized);
                    return;
                }

                var requestedIds = corrections.Select(item => item.PlayerId).Order().ToArray();
                var participantIds = context.Source.ParticipantPlayerIds.Order().ToArray();
                if (!requestedIds.SequenceEqual(participantIds))
                {
                    result = Failure(CompletedGameManualOverrideFailure.InvalidRequest);
                    return;
                }

                var proposed = corrections.Select(item => new CompletedGameBattingOverride
                {
                    GameId = gameId,
                    PlayerId = item.PlayerId,
                    Singles = item.Singles,
                    Doubles = item.Doubles,
                    Triples = item.Triples,
                    HomeRuns = item.HomeRuns,
                    Walks = item.Walks,
                    FielderChoices = item.FielderChoices,
                    SacrificeFlies = item.SacrificeFlies,
                    Outs = item.Outs,
                }).ToArray();
                IReadOnlyList<EffectiveCompletedBattingPlayer> projected;
                try
                {
                    projected = EffectiveCompletedBattingStatistics.ProjectGame(context.Source, proposed);
                }
                catch (Exception exception) when (exception is ArgumentException or OverflowException)
                {
                    result = Failure(CompletedGameManualOverrideFailure.InvalidBattingStatistics);
                    return;
                }

                var existing = transaction.Table<CompletedGameBattingOverride>()
                    .Where(item => item.GameId == gameId).ToList()
                    .ToDictionary(item => item.PlayerId);
                foreach (var player in projected)
                {
                    var requested = proposed.Single(item => item.PlayerId == player.PlayerId);
                    if (player.EffectiveValues == player.CanonicalValues)
                    {
                        if (existing.Remove(player.PlayerId, out var obsolete)
                            && transaction.Delete(obsolete) != 1)
                            throw new InvalidOperationException("Batting override deletion failed.");
                        continue;
                    }

                    if (existing.Remove(player.PlayerId, out var persisted))
                    {
                        persisted.Singles = requested.Singles;
                        persisted.Doubles = requested.Doubles;
                        persisted.Triples = requested.Triples;
                        persisted.HomeRuns = requested.HomeRuns;
                        persisted.Walks = requested.Walks;
                        persisted.FielderChoices = requested.FielderChoices;
                        persisted.SacrificeFlies = requested.SacrificeFlies;
                        persisted.Outs = requested.Outs;
                        if (transaction.Update(persisted) != 1)
                            throw new InvalidOperationException("Batting override update failed.");
                    }
                    else if (transaction.Insert(requested) != 1)
                    {
                        throw new InvalidOperationException("Batting override insertion failed.");
                    }
                }
                foreach (var obsolete in existing.Values)
                    if (transaction.Delete(obsolete) != 1)
                        throw new InvalidOperationException("Batting override deletion failed.");

                var scoreOverride = transaction.Find<CompletedGameScoreOverride>(gameId);
                if (visitorScore == context.Session.VisitorScore
                    && homeScore == context.Session.HomeScore)
                {
                    if (scoreOverride is not null && transaction.Delete(scoreOverride) != 1)
                        throw new InvalidOperationException("Score override deletion failed.");
                }
                else if (scoreOverride is null)
                {
                    if (transaction.Insert(new CompletedGameScoreOverride
                        {
                            GameId = gameId,
                            VisitorScore = visitorScore,
                            HomeScore = homeScore,
                        }) != 1)
                        throw new InvalidOperationException("Score override insertion failed.");
                }
                else
                {
                    scoreOverride.VisitorScore = visitorScore;
                    scoreOverride.HomeScore = homeScore;
                    if (transaction.Update(scoreOverride) != 1)
                        throw new InvalidOperationException("Score override update failed.");
                }

                result = new(CompletedGameManualOverrideFailure.None);
            });
        }
        catch
        {
            return Failure(CompletedGameManualOverrideFailure.Persistence);
        }
        return result;
    }

    public async Task<CompletedGameManualOverrideResult> RevertAsync(int gameId)
    {
        if (gameId <= 0)
            return Failure(CompletedGameManualOverrideFailure.InvalidRequest);
        await using var operation = await database.BeginOperationAsync();
        var result = Failure(CompletedGameManualOverrideFailure.Persistence);
        try
        {
            await operation.Connection.RunInTransactionAsync(transaction =>
            {
                if (LoadContext(transaction, gameId) is null)
                {
                    result = Failure(CompletedGameManualOverrideFailure.GameNotCompleted);
                    return;
                }
                if (TeamYearWriteGuard.IsGameFinalized(transaction, gameId))
                {
                    result = Failure(CompletedGameManualOverrideFailure.TeamYearFinalized);
                    return;
                }
                foreach (var row in transaction.Table<CompletedGameBattingOverride>()
                    .Where(item => item.GameId == gameId).ToList())
                    if (transaction.Delete(row) != 1)
                        throw new InvalidOperationException("Batting override deletion failed.");
                var score = transaction.Find<CompletedGameScoreOverride>(gameId);
                if (score is not null && transaction.Delete(score) != 1)
                    throw new InvalidOperationException("Score override deletion failed.");
                result = new(CompletedGameManualOverrideFailure.None);
            });
        }
        catch
        {
            return Failure(CompletedGameManualOverrideFailure.Persistence);
        }
        return result;
    }

    private static bool HasNegativeValue(CompletedGameBattingCorrection item) =>
        item.PlayerId <= 0 || item.Singles < 0 || item.Doubles < 0 || item.Triples < 0
        || item.HomeRuns < 0 || item.Walks < 0 || item.FielderChoices < 0
        || item.SacrificeFlies < 0 || item.Outs < 0;

    private static CompletedContext? LoadContext(SQLiteConnection transaction, int gameId)
    {
        var session = transaction.Table<GameScoringSession>()
            .FirstOrDefault(item => item.GameId == gameId
                && item.Status == GameScoringStatus.Completed);
        if (session is null || transaction.Find<Game>(gameId) is null)
            return null;
        var lineup = transaction.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition).ToList();
        var appearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.PlateAppearanceNumber).ToList();
        if (appearances.Any(item => item.Result is null))
            return null;
        var participants = lineup.Select(item => item.PlayerId)
            .Concat(appearances.Select(item => item.PlayerId)).Distinct().ToArray();
        return new(session, new CompletedGameBattingSource(
            gameId,
            participants,
            appearances.Select(item => new BattingOutcome(item.PlayerId, item.Result!.Value)).ToArray()));
    }

    private static CompletedGameManualOverrideResult Failure(
        CompletedGameManualOverrideFailure failure) => new(failure);

    private sealed record CompletedContext(
        GameScoringSession Session,
        CompletedGameBattingSource Source);
}
