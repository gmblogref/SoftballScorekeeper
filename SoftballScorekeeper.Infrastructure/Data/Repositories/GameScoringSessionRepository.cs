using System.Text.Json;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameScoringSessionRepository
{
    private readonly AppDatabase database;
    private readonly PlayResolutionCoordinator playResolutionCoordinator;
    private readonly IBeerRuleEvaluator beerRuleEvaluator;

    public GameScoringSessionRepository(AppDatabase database)
        : this(database, new PlayResolutionCoordinator(), new BeerRuleEvaluator())
    {
    }

    public GameScoringSessionRepository(
        AppDatabase database,
        PlayResolutionCoordinator playResolutionCoordinator)
        : this(database, playResolutionCoordinator, new BeerRuleEvaluator())
    {
    }

    public GameScoringSessionRepository(
        AppDatabase database,
        PlayResolutionCoordinator playResolutionCoordinator,
        IBeerRuleEvaluator beerRuleEvaluator)
    {
        this.database = database;
        this.playResolutionCoordinator = playResolutionCoordinator;
        this.beerRuleEvaluator = beerRuleEvaluator;
    }

    public async Task<GameScoringSession?> GetForGameAsync(int gameId)
    {
        if (gameId <= 0) return null;
        var connection = await database.GetConnectionAsync();
        return await connection.Table<GameScoringSession>()
            .Where(session => session.GameId == gameId)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyDictionary<int, GameScoringSession>> GetForGamesAsync(
        IEnumerable<int>? gameIds)
    {
        var requestedIds = (gameIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (requestedIds.Length == 0)
            return new Dictionary<int, GameScoringSession>();

        var connection = await database.GetConnectionAsync();
        var placeholders = string.Join(",", requestedIds.Select(_ => "?"));
        var sessions = await connection.QueryAsync<GameScoringSession>(
            $"SELECT * FROM GameScoringSession WHERE GameId IN ({placeholders})",
            requestedIds.Cast<object>().ToArray());
        return sessions.ToDictionary(session => session.GameId);
    }

    public async Task<bool> ExistsForGameAsync(int gameId) =>
        await GetForGameAsync(gameId) is not null;

    public async Task<CompletedGameProjection?> GetCompletedGameAsync(int gameId)
    {
        if (gameId <= 0) return null;

        var connection = await database.GetConnectionAsync();
        var session = await connection.Table<GameScoringSession>()
            .Where(item => item.GameId == gameId
                && item.Status == GameScoringStatus.Completed)
            .FirstOrDefaultAsync();
        if (session is null) return null;

        var game = await connection.FindAsync<Game>(gameId)
            ?? throw new InvalidOperationException(
                "The completed scoring session has no scheduled Game.");
        var team = await connection.FindAsync<Team>(game.TeamId)
            ?? throw new InvalidOperationException(
                "The completed Game has no scored Team.");
        var projectedPlayers = await ProjectBattingPlayersAsync(connection, session);

        if (session.TeamSide is not TeamGameSide.Away and not TeamGameSide.Home)
            throw new InvalidOperationException(
                "The completed Game has an invalid Team side.");
        var opponentName = game.Opponent?.Trim() ?? string.Empty;
        var teamName = team.Name.Trim();
        var visitorName = session.TeamSide == TeamGameSide.Away
            ? teamName : opponentName;
        var homeName = session.TeamSide == TeamGameSide.Home
            ? teamName : opponentName;
        return new(
            game.Id,
            session.Id,
            session.Status,
            session.CompletedAtUtc,
            session.TeamSide,
            visitorName,
            session.VisitorScore,
            homeName,
            session.HomeScore,
            projectedPlayers);
    }

    public async Task<GameBattingStatisticsProjection?> GetInProgressBattingStatisticsAsync(
        int gameId)
    {
        if (gameId <= 0) return null;

        var connection = await database.GetConnectionAsync();
        var session = await connection.Table<GameScoringSession>()
            .Where(item => item.GameId == gameId
                && item.Status == GameScoringStatus.InProgress)
            .FirstOrDefaultAsync();
        if (session is null) return null;

        return new(
            gameId,
            session.Id,
            await ProjectBattingPlayersAsync(connection, session));
    }

    private static async Task<IReadOnlyList<GameBattingPlayerProjection>>
        ProjectBattingPlayersAsync(
            SQLite.SQLiteAsyncConnection connection,
            GameScoringSession session)
    {
        var lineup = await connection.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition)
            .ToListAsync();
        var appearances = await connection.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.PlateAppearanceNumber)
            .ToListAsync();
        if (appearances.Any(item => item.Result is null))
            throw new InvalidOperationException(
                "The Game contains a committed plate appearance without a final result.");

        var outcomes = appearances.Select(item =>
            new BattingOutcome(item.PlayerId, item.Result!.Value)).ToArray();
        var statLines = BattingStatisticsCalculator.Calculate(
            lineup.Select(item => item.PlayerId), outcomes);
        var playerIds = statLines.Select(item => item.PlayerId).ToArray();
        List<Player> players = playerIds.Length == 0
            ? []
            : await connection.QueryAsync<Player>(
                $"SELECT * FROM Player WHERE Id IN ({string.Join(",", playerIds.Select(_ => "?"))})",
                playerIds.Cast<object>().ToArray());
        var playersById = players.ToDictionary(item => item.Id);
        var missingPlayerIds = playerIds.Where(id => !playersById.ContainsKey(id)).ToArray();
        if (missingPlayerIds.Length > 0)
            throw new InvalidOperationException(
                $"The Game cannot resolve Player Ids: {string.Join(", ", missingPlayerIds)}.");

        var battingPositions = lineup.ToDictionary(
            item => item.PlayerId, item => item.BattingPosition);
        foreach (var appearance in appearances)
            battingPositions.TryAdd(appearance.PlayerId, appearance.BattingPosition);
        var projectedPlayers = statLines.Select(statLine =>
        {
            var player = playersById[statLine.PlayerId];
            return new GameBattingPlayerProjection(
                statLine.PlayerId,
                FormatPlayerName(player),
                battingPositions[statLine.PlayerId],
                statLine);
        }).ToArray();

        return projectedPlayers;
    }

    private static string FormatPlayerName(Player player) => string.Join(
        " ", new[] { player.FirstName, player.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

    public async Task<ScoringUndoRedoAvailability> GetUndoRedoAvailabilityAsync(int gameId)
    {
        if (gameId <= 0) return new(false, false);
        var connection = await database.GetConnectionAsync();
        var availability = new ScoringUndoRedoAvailability(false, false);
        await connection.RunInTransactionAsync(transaction =>
        {
            var session = transaction.Table<GameScoringSession>()
                .FirstOrDefault(item => item.GameId == gameId);
            availability = GetUndoRedoAvailability(transaction, session);
        });
        return availability;
    }

    public Task<ScoringUndoRedoResult> UndoAsync(int gameId) =>
        RestoreHistoryAsync(gameId, -1);

    public Task<ScoringUndoRedoResult> RedoAsync(int gameId) =>
        RestoreHistoryAsync(gameId, 1);

    private async Task<ScoringUndoRedoResult> RestoreHistoryAsync(int gameId, int direction)
    {
        var unavailable = new ScoringUndoRedoAvailability(false, false);
        if (gameId <= 0 || direction is not (-1 or 1))
            return new(false, ScoringUndoRedoFailure.InvalidRequest, unavailable);

        var connection = await database.GetConnectionAsync();
        var result = new ScoringUndoRedoResult(
            false, ScoringUndoRedoFailure.SessionNotFound, unavailable);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null) return;
                if (session.Status != GameScoringStatus.InProgress)
                {
                    result = new(false, ScoringUndoRedoFailure.GameCompleted, unavailable);
                    return;
                }
                if (HasPendingHalfInningTransition(transaction, session.Id))
                {
                    result = new(false, ScoringUndoRedoFailure.UnresolvedScoringState,
                        unavailable);
                    return;
                }

                var history = transaction.Find<GameScoringHistory>(session.Id);
                var targetPosition = history is null
                    ? -1 : history.CurrentPosition + direction;
                if (history is null || targetPosition < 0)
                {
                    result = new(false, direction < 0
                            ? ScoringUndoRedoFailure.NothingToUndo
                            : ScoringUndoRedoFailure.NothingToRedo,
                        GetUndoRedoAvailability(transaction, session));
                    return;
                }
                if (targetPosition > history.LatestPosition)
                {
                    result = new(false, ScoringUndoRedoFailure.NothingToRedo,
                        GetUndoRedoAvailability(transaction, session));
                    return;
                }

                var snapshot = transaction.Table<GameScoringHistorySnapshot>()
                    .FirstOrDefault(item => item.GameScoringSessionId == session.Id
                        && item.Position == targetPosition);
                if (snapshot is null)
                {
                    result = new(false, ScoringUndoRedoFailure.InvalidHistory,
                        unavailable);
                    return;
                }

                RestoreSnapshot(transaction, session, snapshot);
                history.CurrentPosition = targetPosition;
                if (transaction.Update(history) != 1)
                    throw new InvalidOperationException(
                        "Scoring-history cursor update did not affect one row.");
                result = new(true, ScoringUndoRedoFailure.None,
                    GetUndoRedoAvailability(transaction, session));
            });
        }
        catch
        {
            return new(false, ScoringUndoRedoFailure.Persistence, unavailable);
        }
        return result;
    }

    public async Task<UpdateGameRulesResult> UpdateGameRulesAsync(
        int gameId, int homeRunLimit, bool isOneUpEnabled)
    {
        if (gameId <= 0 || homeRunLimit < 0)
            return new(null, GameScoringLifecycleFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var result = new UpdateGameRulesResult(null,
            GameScoringLifecycleFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null) return;
                if (session.Status != GameScoringStatus.InProgress)
                {
                    result = new(session, GameScoringLifecycleFailure.GameCompleted);
                    return;
                }
                session.HomeRunLimit = homeRunLimit;
                session.IsOneUpEnabled = isOneUpEnabled;
                if (transaction.Update(session) != 1)
                    throw new InvalidOperationException(
                        "Game-rules update did not affect one scoring session.");
                result = new(session, GameScoringLifecycleFailure.None);
            });
        }
        catch
        {
            return new(null, GameScoringLifecycleFailure.Persistence);
        }
        return result;
    }

    public async Task<GameOverResult> CompleteGameAsync(int gameId)
    {
        if (gameId <= 0)
            return new(null, GameScoringLifecycleFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var result = new GameOverResult(null,
            GameScoringLifecycleFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null) return;
                if (session.Status == GameScoringStatus.Completed)
                {
                    result = new(session, GameScoringLifecycleFailure.None);
                    return;
                }
                if (transaction.Table<GameScoringPendingPlay>()
                        .Any(item => item.GameScoringSessionId == session.Id)
                    || transaction.Table<GameScoringHalfInning>()
                        .Any(item => item.GameScoringSessionId == session.Id
                            && !item.OpponentEntryCompleted))
                {
                    result = new(session,
                        GameScoringLifecycleFailure.UnresolvedScoringState);
                    return;
                }
                session.Status = GameScoringStatus.Completed;
                session.CompletedAtUtc = DateTime.UtcNow;
                if (transaction.Update(session) != 1)
                    throw new InvalidOperationException(
                        "Game completion did not affect one scoring session.");
                result = new(session, GameScoringLifecycleFailure.None);
            });
        }
        catch
        {
            return new(null, GameScoringLifecycleFailure.Persistence);
        }
        return result;
    }

    public async Task<ResetScoringResult> ResetGameAsync(int gameId)
    {
        if (gameId <= 0)
            return new(GameScoringLifecycleFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var result = new ResetScoringResult(
            GameScoringLifecycleFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null) return;
                if (session.Status != GameScoringStatus.InProgress)
                {
                    result = new(GameScoringLifecycleFailure.GameCompleted);
                    return;
                }

                transaction.Execute(
                    "DELETE FROM GameScoringPendingRunnerMovement WHERE GameScoringPendingPlayId IN (SELECT Id FROM GameScoringPendingPlay WHERE GameScoringSessionId = ?)",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringPendingPlay WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GamePlateAppearanceRunnerMovement WHERE GamePlateAppearanceId IN (SELECT Id FROM GamePlateAppearance WHERE GameScoringSessionId = ?)",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GamePlateAppearance WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringHalfInning WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringHistory WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM BeerLedgerEntry WHERE GameScoringSessionId = ?",
                    session.Id);
                transaction.Execute(
                    "DELETE FROM GameScoringLineupPlayer WHERE GameScoringSessionId = ?",
                    session.Id);
                if (transaction.Delete(session) != 1)
                    throw new InvalidOperationException(
                        "Scoring reset did not remove one session.");
                result = new(GameScoringLifecycleFailure.None);
            });
        }
        catch
        {
            return new(GameScoringLifecycleFailure.Persistence);
        }
        return result;
    }

    public async Task<GameScoringHalfInning?> GetPendingHalfInningTransitionAsync(int gameId)
    {
        if (gameId <= 0) return null;
        var session = await GetForGameAsync(gameId);
        if (session is null) return null;
        var connection = await database.GetConnectionAsync();
        return await connection.Table<GameScoringHalfInning>()
            .Where(item => item.GameScoringSessionId == session.Id
                && !item.OpponentEntryCompleted)
            .OrderBy(item => item.Inning)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AcknowledgeHalfInningSummaryAsync(int gameId, int halfInningId)
    {
        if (gameId <= 0 || halfInningId <= 0) return false;
        var connection = await database.GetConnectionAsync();
        var succeeded = false;
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                var halfInning = transaction.Find<GameScoringHalfInning>(halfInningId);
                if (session is null || halfInning is null
                    || halfInning.GameScoringSessionId != session.Id) return;
                if (!halfInning.TeamSummaryAcknowledged)
                {
                    if (transaction.Execute(
                        "UPDATE GameScoringHalfInning SET TeamSummaryAcknowledged = 1 WHERE Id = ? AND GameScoringSessionId = ?",
                        halfInning.Id, session.Id) != 1)
                        throw new InvalidOperationException(
                            "Half-inning summary acknowledgement did not affect one row.");
                }
                succeeded = true;
            });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "Half-inning summary acknowledgement failed: {0}", exception);
            return false;
        }
        return succeeded;
    }

    public async Task<OpponentHalfInningEntryResult> CompleteOpponentHalfInningAsync(
        int gameId, int halfInningId, int? opponentRuns, int? opponentHomeRuns)
        => await CompleteOpponentHalfInningAsync(gameId, halfInningId,
            opponentRuns, OpponentEntryMode.ThisHalf,
            opponentHomeRuns, OpponentEntryMode.ThisHalf);

    public async Task<OpponentHalfInningEntryResult> CompleteOpponentHalfInningAsync(
        int gameId, int halfInningId,
        int? opponentRuns, OpponentEntryMode runsMode,
        int? opponentHomeRuns, OpponentEntryMode homeRunsMode)
    {
        if (gameId <= 0 || halfInningId <= 0
            || opponentRuns < 0 || opponentHomeRuns < 0
            || !Enum.IsDefined(runsMode) || !Enum.IsDefined(homeRunsMode))
            return new(null, false, OpponentHalfInningEntryFailure.InvalidValue);
        var connection = await database.GetConnectionAsync();
        var result = new OpponentHalfInningEntryResult(null, false);
        var stage = "load scoring session";
        int? sessionId = null;
        int? completedInning = null;
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                stage = "load scoring session";
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                sessionId = session?.Id;
                stage = "load completed half-inning";
                var halfInning = transaction.Find<GameScoringHalfInning>(halfInningId);
                completedInning = halfInning?.Inning;
                if (session is null || session.Status != GameScoringStatus.InProgress
                    || halfInning is null
                    || halfInning.GameScoringSessionId != session.Id)
                    throw new InvalidOperationException(
                        "Opponent entry did not identify the expected completed half-inning.");
                if (halfInning.OpponentEntryCompleted)
                {
                    result = new(halfInning, true);
                    return;
                }
                if (!halfInning.TeamSummaryAcknowledged)
                    throw new InvalidOperationException(
                        "Opponent entry cannot complete before the team summary is acknowledged.");

                if (session.TeamSide is not TeamGameSide.Home and not TeamGameSide.Away)
                    throw new InvalidOperationException(
                        "Scoring session has an invalid Team side.");

                var currentOpponentScore = session.TeamSide == TeamGameSide.Home
                    ? session.VisitorScore
                    : session.HomeScore;
                var currentOpponentHomeRuns = transaction.Table<GameScoringHalfInning>()
                    .Where(item => item.GameScoringSessionId == session.Id
                        && item.OpponentEntryCompleted)
                    .ToList()
                    .Sum(item => item.OpponentHomeRuns ?? 0);

                int? halfRuns = null;
                var newOpponentScore = currentOpponentScore;
                if (opponentRuns is not null)
                {
                    if (runsMode == OpponentEntryMode.Total
                        && opponentRuns.Value < currentOpponentScore)
                    {
                        result = new(null, false,
                            OpponentHalfInningEntryFailure.TotalRunsBelowCurrent);
                        return;
                    }
                    halfRuns = runsMode == OpponentEntryMode.Total
                        ? opponentRuns.Value - currentOpponentScore
                        : opponentRuns.Value;
                    newOpponentScore = runsMode == OpponentEntryMode.Total
                        ? opponentRuns.Value
                        : checked(currentOpponentScore + opponentRuns.Value);
                }

                int? halfHomeRuns = null;
                if (opponentHomeRuns is not null)
                {
                    if (homeRunsMode == OpponentEntryMode.Total
                        && opponentHomeRuns.Value < currentOpponentHomeRuns)
                    {
                        result = new(null, false,
                            OpponentHalfInningEntryFailure.TotalHomeRunsBelowCurrent);
                        return;
                    }
                    halfHomeRuns = homeRunsMode == OpponentEntryMode.Total
                        ? opponentHomeRuns.Value - currentOpponentHomeRuns
                        : opponentHomeRuns.Value;
                }

                if (opponentRuns is not null)
                {
                    stage = "update opponent score";
                    if (session.TeamSide == TeamGameSide.Home)
                        session.VisitorScore = newOpponentScore;
                    else
                        session.HomeScore = newOpponentScore;
                    if (transaction.Update(session) != 1)
                        throw new InvalidOperationException(
                            "Opponent score update did not affect one scoring session.");
                }

                stage = "complete opponent half-inning";
                halfInning.OpponentRuns = halfRuns;
                halfInning.OpponentHomeRuns = halfHomeRuns;
                halfInning.OpponentEntryCompleted = true;
                if (transaction.Update(halfInning) != 1)
                    throw new InvalidOperationException(
                        "Opponent entry did not update one completed half-inning.");
                ReplaceCurrentHistorySnapshot(transaction, session.Id);
                result = new(halfInning, true);
            });
        }
        catch (Exception exception)
        {
            LogOpponentHalfInningFailure(stage, exception, gameId, sessionId,
                halfInningId, completedInning, opponentRuns, opponentHomeRuns);
            return new(null, false, OpponentHalfInningEntryFailure.PersistenceFailure);
        }
        return result;
    }

    private static void LogOpponentHalfInningFailure(string stage, Exception exception,
        int gameId, int? sessionId, int halfInningId, int? completedInning,
        int? opponentRuns, int? opponentHomeRuns)
    {
        var message =
            $"Opponent half-inning completion failed during '{stage}'. "
            + $"Exception={exception.GetType().FullName}: {exception.Message}; "
            + $"GameId={gameId}; SessionId={sessionId?.ToString() ?? "null"}; "
            + $"HalfInningId={halfInningId}; CompletedInning={completedInning?.ToString() ?? "null"}; "
            + $"OpponentRuns={opponentRuns?.ToString() ?? "null"}; "
            + $"OpponentHomeRuns={opponentHomeRuns?.ToString() ?? "null"}. "
            + exception;
        System.Diagnostics.Trace.TraceError(message);
        System.Diagnostics.Debug.WriteLine(message);
        Console.Error.WriteLine(message);
    }

    public async Task<GameScoringState?> GetStateForGameAsync(int gameId)
    {
        var session = await GetForGameAsync(gameId);
        if (session is null) return null;

        var connection = await database.GetConnectionAsync();
        var sequence = await connection.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition)
            .ToListAsync();
        var plateAppearances = await connection.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.PlateAppearanceNumber)
            .ToListAsync();
        var baseRunners = await connection.Table<GameScoringBaseRunner>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.Base)
            .ToListAsync();
        var halfInnings = await connection.Table<GameScoringHalfInning>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.Inning)
            .ToListAsync();
        var appearanceIds = plateAppearances.Select(item => item.Id).ToArray();
        var runnerMovements = appearanceIds.Length == 0
            ? []
            : await connection.QueryAsync<GamePlateAppearanceRunnerMovement>(
                $"SELECT * FROM GamePlateAppearanceRunnerMovement WHERE GamePlateAppearanceId IN ({string.Join(",", appearanceIds.Select(_ => "?"))}) ORDER BY GamePlateAppearanceId, Id",
                appearanceIds.Cast<object>().ToArray());
        var pending = await connection.Table<GameScoringPendingPlay>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .FirstOrDefaultAsync();
        PendingScoringPlay? pendingPlay = null;
        PlayResolutionResult? playResolution = null;
        if (pending is not null)
        {
            var pendingMovements = await connection.Table<GameScoringPendingRunnerMovement>()
                .Where(item => item.GameScoringPendingPlayId == pending.Id)
                .OrderBy(item => item.StartingLocation)
                .ToListAsync();
            pendingPlay = new(pending, pendingMovements);
            PlayContextFactory.TryRehydrate(
                session.CurrentOuts,
                pending,
                pendingMovements,
                baseRunners,
                out var playContext);
            playResolution = playResolutionCoordinator.Evaluate(playContext);
        }
        var teamHomeRuns = plateAppearances.Count(item =>
            item.Result == BatterResult.HomeRun);
        var opponentHomeRuns = halfInnings.Where(item => item.OpponentEntryCompleted)
            .Sum(item => item.OpponentHomeRuns ?? 0);
        var homeHomeRuns = session.TeamSide == TeamGameSide.Home
            ? teamHomeRuns : opponentHomeRuns;
        var visitorHomeRuns = session.TeamSide == TeamGameSide.Away
            ? teamHomeRuns : opponentHomeRuns;
        if (sequence.Count == 0)
            return new(session, sequence, new Dictionary<int, Player>(), plateAppearances,
                baseRunners, runnerMovements, pendingPlay, halfInnings,
                homeHomeRuns, visitorHomeRuns, playResolution);

        var playerIds = sequence.Select(item => item.PlayerId)
            .Concat(baseRunners.Select(item => item.PlayerId))
            .Concat(runnerMovements.Select(item => item.PlayerId))
            .Concat(pendingPlay?.Movements.Select(item => item.PlayerId) ?? [])
            .Distinct().ToArray();
        var placeholders = string.Join(",", playerIds.Select(_ => "?"));
        var players = await connection.QueryAsync<Player>(
            $"SELECT * FROM Player WHERE Id IN ({placeholders})",
            playerIds.Cast<object>().ToArray());
        return new(
            session,
            sequence,
            players.ToDictionary(player => player.Id),
            plateAppearances,
            baseRunners,
            runnerMovements,
            pendingPlay,
            halfInnings,
            homeHomeRuns,
            visitorHomeRuns,
            playResolution);
    }

    public async Task<PendingPlayResult> SelectBatterResultAsync(int gameId, BatterResult result)
    {
        if (gameId <= 0
            || !PlateAppearanceResultDefinitions.TryGet(result, out var definition)
            || definition is null
            || definition.CompletionBehavior == PlateAppearanceCompletionBehavior.ContextualOut)
            return PendingFailure(PendingPlayFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var response = PendingFailure(PendingPlayFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    response = PendingFailure(MapPendingFailure(context.Failure));
                    return;
                }
                if ((result is BatterResult.HomeRun or BatterResult.DeadBallOut)
                    && IsHomeRunLegal(transaction, context.Session!)
                        != (result == BatterResult.HomeRun))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                if (definition.RequiresExistingRunner && context.BaseRunners!.Count == 0)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                if (context.BaseRunners!.Count + 1 < definition.MinimumParticipants)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                if (definition.RequiredPlayOuts > 3 - context.Session!.CurrentOuts)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                if (!PlayContextFactory.TryCreateForNewResult(
                        context.Session.CurrentOuts,
                        context.CurrentBatter!.PlayerId,
                        result,
                        context.BaseRunners!,
                        out var playContext)
                    || playContext is null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var resolution = playResolutionCoordinator.InitializeResult(playContext);
                if (!resolution.IsAccepted)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                if (definition.RequiredPlayOuts > 0
                    && resolution.Status == PlayResolutionStatus.CommitImmediately)
                {
                    var completed = CompleteInningEndingMultiOut(
                        transaction,
                        context,
                        result,
                        definition.RequiredPlayOuts,
                        resolution.ThirdOutRulingRequirement);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
                }

                EnsureHistoryInitialized(transaction, context.Session!.Id);
                ClearPending(transaction, context.Session!.Id);
                var play = new GameScoringPendingPlay
                {
                    GameScoringSessionId = context.Session.Id,
                    BatterPlayerId = context.CurrentBatter!.PlayerId,
                    BatterPosition = context.CurrentBatter.BattingPosition,
                    Result = result,
                };
                if (transaction.Insert(play) != 1)
                    throw new InvalidOperationException("Pending-play insertion did not affect one row.");

                var movements = CreateDefaultMovements(play.Id, context.CurrentBatter.PlayerId,
                    context.BaseRunners!, result);
                if (resolution.Context is null
                    || !PlayContextFactory.TryApplyToPendingMovements(
                        resolution.Context, movements))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }
                foreach (var movement in movements)
                    if (transaction.Insert(movement) != 1)
                        throw new InvalidOperationException("Pending movement insertion did not affect one row.");
                if (resolution.Status == PlayResolutionStatus.AutoComplete)
                {
                    var completed = CompletePendingPlay(transaction, gameId, result);
                    if (!completed.IsSuccess)
                    {
                        response = PendingFailure(MapPendingFailure(completed.FailureReason));
                        return;
                    }
                    response = new(null, PendingPlayFailure.None);
                    return;
                }
                if (result != BatterResult.HomeRun
                    && resolution.Status == PlayResolutionStatus.CommitImmediately)
                {
                    var completed = CompletePendingPlay(transaction, gameId, result);
                    if (!completed.IsSuccess)
                    {
                        response = PendingFailure(MapPendingFailure(completed.FailureReason));
                        return;
                    }
                    response = new(null, PendingPlayFailure.None);
                    return;
                }
                RecordPendingCheckpoint(transaction, context.Session.Id);
                response = new(new(play, movements), PendingPlayFailure.None);
            });
        }
        catch
        {
            return PendingFailure(PendingPlayFailure.Persistence);
        }
        return response;
    }

    public async Task<PendingPlayResult> SelectMultiOutRetirementsAsync(
        int gameId,
        IReadOnlyList<MultiOutRetirementSelection> retirements)
    {
        if (gameId <= 0 || retirements is null)
            return PendingFailure(PendingPlayFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var response = PendingFailure(PendingPlayFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    response = PendingFailure(MapPendingFailure(context.Failure));
                    return;
                }

                var session = context.Session!;
                var play = transaction.Table<GameScoringPendingPlay>()
                    .FirstOrDefault(item => item.GameScoringSessionId == session.Id);
                if (play is null
                    || !PlateAppearanceResultDefinitions.TryGet(
                        play.Result, out var definition)
                    || definition is null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }

                var movements = transaction.Table<GameScoringPendingRunnerMovement>()
                    .Where(item => item.GameScoringPendingPlayId == play.Id).ToList();
                if (!PlayContextFactory.TryRehydrate(
                        session.CurrentOuts,
                        play,
                        movements,
                        context.BaseRunners!,
                        out var playContext)
                    || playContext is null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var pendingResolution = playResolutionCoordinator.Evaluate(playContext);
                if (!pendingResolution.IsAccepted
                    || pendingResolution.DecisionRequirement
                        != PlayDecisionRequirement.RetirementSelection
                    || retirements.Count != pendingResolution.RequiredRetirementCount
                    || retirements.Select(item => item.PlayerId).Distinct().Count()
                        != retirements.Count
                    || retirements.Any(item => item.PlayerId <= 0
                        || item.Outcome is null
                        || item.Outcome != RunnerOutcomeChoice.ForDestination(
                            RunnerDestination.Out)
                        || !pendingResolution.DecisionPlayerIds.Contains(item.PlayerId)))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                var resolution = playResolutionCoordinator.ApplyRetirementSelection(
                    playContext,
                    retirements.Select(item => item.PlayerId).ToHashSet());
                if (!resolution.IsAccepted
                    || resolution.Context is null
                    || !TrySynchronizePendingPlay(
                        transaction, play, movements, resolution.Context,
                        out var synchronizedMovements))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                if (resolution.Status == PlayResolutionStatus.AutoComplete)
                {
                    var completed = CompletePendingPlay(transaction, gameId, play.Result);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
                }
                if (play.Result == BatterResult.Out
                    && resolution.Status == PlayResolutionStatus.CommitImmediately)
                {
                    var completed = CompleteImmediateOut(transaction, context);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
                }

                RecordPendingCheckpoint(transaction, session.Id);
                response = new(new(play, synchronizedMovements), PendingPlayFailure.None);
            });
        }
        catch
        {
            return PendingFailure(PendingPlayFailure.Persistence);
        }
        return response;
    }

    public async Task<PendingPlayResult> AdjustPendingRunnerAsync(
        int gameId, int playerId, RunnerDestination destination) =>
        await AdjustPendingRunnerOutcomeAsync(gameId, playerId,
            RunnerOutcomeChoice.ForDestination(destination));

    public async Task<PendingPlayResult> AdjustPendingRunnerOutcomeAsync(
        int gameId, int playerId, RunnerOutcomeChoice outcome)
    {
        if (gameId <= 0 || playerId <= 0 || outcome is null
            || !Enum.IsDefined(outcome.Destination))
            return PendingFailure(PendingPlayFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var response = PendingFailure(PendingPlayFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null) return;
                if (session.Status != GameScoringStatus.InProgress)
                {
                    response = PendingFailure(PendingPlayFailure.GameCompleted);
                    return;
                }
                var play = transaction.Table<GameScoringPendingPlay>()
                    .FirstOrDefault(item => item.GameScoringSessionId == session.Id);
                if (play is null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var movements = transaction.Table<GameScoringPendingRunnerMovement>()
                    .Where(item => item.GameScoringPendingPlayId == play.Id).ToList();
                var movement = movements.FirstOrDefault(item => item.PlayerId == playerId);
                if (movement is null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                if (!outcome.IsValidFor(movement.StartingLocation))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                var baseRunners = transaction.Table<GameScoringBaseRunner>()
                    .Where(item => item.GameScoringSessionId == session.Id)
                    .ToList();
                if (!PlayContextFactory.TryRehydrate(
                        session.CurrentOuts,
                        play,
                        movements,
                        baseRunners,
                        out var playContext)
                    || playContext is null)
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                var currentResolution = playResolutionCoordinator.Evaluate(playContext);
                var resolution = currentResolution.DecisionRequirement
                        == PlayDecisionRequirement.RetirementSelection
                    && currentResolution.DecisionPlayerIds.Contains(playerId)
                    && outcome == RunnerOutcomeChoice.ForDestination(
                        RunnerDestination.Out)
                        ? playResolutionCoordinator.ApplyRetirementSelection(
                            playContext, new HashSet<int> { playerId })
                        : playResolutionCoordinator.ApplyParticipantOutcome(
                            playContext, playerId, outcome);
                if (!resolution.IsAccepted || resolution.Context is null)
                {
                    response = PendingFailure(resolution.FailureReason
                        == PlayResolutionFailure.InvalidDecision
                            ? PendingPlayFailure.InvalidRequest
                            : PendingPlayFailure.DuplicateDestination);
                    return;
                }
                if (!TrySynchronizePendingPlay(
                        transaction, play, movements, resolution.Context,
                        out var synchronizedMovements))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }
                if (resolution.Status == PlayResolutionStatus.AutoComplete)
                {
                    var completed = CompletePendingPlay(
                        transaction, gameId, play.Result);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
                }
                if (play.Result == BatterResult.Out
                    && resolution.Status == PlayResolutionStatus.CommitImmediately)
                {
                    var completionContext = LoadCompletionContext(transaction, gameId);
                    var completed = CompleteImmediateOut(transaction, completionContext);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
                }
                RecordPendingCheckpoint(transaction, session.Id);
                response = new(new(play, synchronizedMovements), PendingPlayFailure.None);
            });
        }
        catch
        {
            return PendingFailure(PendingPlayFailure.Persistence);
        }
        return response;
    }

    public async Task<RunnerDestinationChoicesResult> GetValidRunnerDestinationsAsync(
        int gameId, int playerId)
    {
        var result = await GetValidRunnerOutcomeChoicesAsync(gameId, playerId);
        return new(result.Choices.Select(item => item.Destination).Distinct().ToArray(),
            result.FailureReason);
    }

    public async Task<RunnerOutcomeChoicesResult> GetValidRunnerOutcomeChoicesAsync(
        int gameId, int playerId)
    {
        if (gameId <= 0 || playerId <= 0)
            return new([], PendingPlayFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var session = await connection.Table<GameScoringSession>()
            .Where(item => item.GameId == gameId).FirstOrDefaultAsync();
        if (session is null)
            return new([], PendingPlayFailure.SessionNotFound);
        if (session.Status != GameScoringStatus.InProgress)
            return new([], PendingPlayFailure.GameCompleted);
        var play = await connection.Table<GameScoringPendingPlay>()
            .Where(item => item.GameScoringSessionId == session.Id).FirstOrDefaultAsync();
        if (play is null)
            return new([], PendingPlayFailure.InvalidScoringState);
        var movements = await connection.Table<GameScoringPendingRunnerMovement>()
            .Where(item => item.GameScoringPendingPlayId == play.Id).ToListAsync();
        if (movements.All(item => item.PlayerId != playerId)
            || play.BatterPlayerId <= 0)
            return new([], PendingPlayFailure.InvalidRequest);
        var baseRunners = await connection.Table<GameScoringBaseRunner>()
            .Where(item => item.GameScoringSessionId == session.Id).ToListAsync();
        if (!PlayContextFactory.TryRehydrate(
                session.CurrentOuts,
                play,
                movements,
                baseRunners,
                out var playContext)
            || playContext is null)
            return new([], PendingPlayFailure.InvalidScoringState);
        var participant = playContext.Participants.Single(item =>
            item.PlayerId == playerId);
        return new(
            playResolutionCoordinator.GetAvailableOutcomes(playContext, playerId),
            PendingPlayFailure.None,
            new RunnerOutcomeChoice(
                participant.Destination,
                participant.SafeDestination,
                participant.AttemptedDestination));
    }

    public async Task<bool> ClearPendingPlayAsync(int gameId)
    {
        if (gameId <= 0) return false;
        var connection = await database.GetConnectionAsync();
        var cleared = false;
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null || session.Status != GameScoringStatus.InProgress) return;
                RewindPendingHistory(transaction, session.Id);
                ClearPending(transaction, session.Id);
                cleared = true;
            });
        }
        catch
        {
            return false;
        }
        return cleared;
    }

    public async Task<RunRulingRequirementsResult> GetRunRulingRequirementsAsync(int gameId)
    {
        if (gameId <= 0)
            return new([], PendingPlayFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        try
        {
            var result = new RunRulingRequirementsResult(
                [], PendingPlayFailure.SessionNotFound);
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    result = new([], MapPendingFailure(context.Failure));
                    return;
                }
                var session = context.Session!;
                var pending = transaction.Table<GameScoringPendingPlay>()
                    .FirstOrDefault(item => item.GameScoringSessionId == session.Id);
                if (pending is null)
                {
                    result = new([], PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var movements = transaction.Table<GameScoringPendingRunnerMovement>()
                    .Where(item => item.GameScoringPendingPlayId == pending.Id).ToList();
                if (!IsValidPendingPlay(pending, movements, context.CurrentBatter!,
                        context.BaseRunners!, session.CurrentOuts))
                {
                    result = new([], PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var outsAfterPlay = session.CurrentOuts
                    + CountOutsAdded(pending, movements);
                if (outsAfterPlay > 3)
                {
                    result = new([], PendingPlayFailure.InvalidScoringState);
                    return;
                }
                if (!PlayContextFactory.TryRehydrate(
                        session.CurrentOuts,
                        pending,
                        movements,
                        context.BaseRunners!,
                        out var playContext)
                    || playContext is null)
                {
                    result = new([], PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var resolution = playResolutionCoordinator.Evaluate(playContext);
                if (!resolution.IsAccepted
                    || resolution.DecisionRequirement is
                        PlayDecisionRequirement.ParticipantOutcome
                        or PlayDecisionRequirement.RetirementSelection)
                {
                    result = new([], PendingPlayFailure.InvalidScoringState);
                    return;
                }
                var ids = outsAfterPlay == 3
                    && resolution.DecisionRequirement
                        == PlayDecisionRequirement.ThirdOutRuling
                    ? resolution.DecisionPlayerIds
                    : [];
                result = new(ids, PendingPlayFailure.None);
            });
            return result;
        }
        catch
        {
            return new([], PendingPlayFailure.Persistence);
        }
    }

    public async Task<CompletePlateAppearanceResult> CompletePlateAppearanceAsync(
        int gameId, IReadOnlyDictionary<int, bool>? runRulings = null)
    {
        if (gameId <= 0)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var result = CompleteFailure(CompletePlateAppearanceFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
                result = CompletePendingPlay(transaction, gameId, null, runRulings));
        }
        catch
        {
            return CompleteFailure(CompletePlateAppearanceFailure.Persistence);
        }

        return result;
    }

    public async Task<CompletePlateAppearanceResult> CompleteHomeRunAsync(int gameId)
    {
        if (gameId <= 0)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var result = CompleteFailure(CompletePlateAppearanceFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    result = CompleteFailure(context.Failure);
                    return;
                }
                if (!IsHomeRunLegal(transaction, context.Session!))
                {
                    result = CompleteFailure(
                        CompletePlateAppearanceFailure.InvalidScoringState);
                    return;
                }
                EnsureHistoryInitialized(transaction, context.Session!.Id);
                ClearPending(transaction, context.Session!.Id);
                InsertPendingPlay(transaction, context, BatterResult.HomeRun);
                result = CompletePendingPlay(transaction, gameId, BatterResult.HomeRun);
            });
        }
        catch
        {
            return CompleteFailure(CompletePlateAppearanceFailure.Persistence);
        }
        return result;
    }

    public async Task<PendingPlayResult> SelectOutAsync(int gameId)
    {
        if (gameId <= 0)
            return PendingFailure(PendingPlayFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var response = PendingFailure(PendingPlayFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    response = PendingFailure(MapPendingFailure(context.Failure));
                    return;
                }

                EnsureHistoryInitialized(transaction, context.Session!.Id);
                ClearPending(transaction, context.Session!.Id);
                if (context.BaseRunners!.Count > 0 && context.Session.CurrentOuts < 2)
                {
                    response = new(InsertPendingPlay(
                        transaction, context, BatterResult.Out), PendingPlayFailure.None);
                    RecordPendingCheckpoint(transaction, context.Session.Id);
                    return;
                }

                var completed = CompleteImmediateOut(transaction, context);
                response = completed.IsSuccess
                    ? new(null, PendingPlayFailure.None)
                    : PendingFailure(MapPendingFailure(completed.FailureReason));
            });
        }
        catch
        {
            return PendingFailure(PendingPlayFailure.Persistence);
        }
        return response;
    }

    public async Task<CompletePlateAppearanceResult> CompleteOutAsync(int gameId)
    {
        if (gameId <= 0)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);
        var connection = await database.GetConnectionAsync();
        var result = CompleteFailure(CompletePlateAppearanceFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var context = LoadCompletionContext(transaction, gameId);
                if (context.Failure != CompletePlateAppearanceFailure.None)
                {
                    result = CompleteFailure(context.Failure);
                    return;
                }

                result = CompleteImmediateOut(transaction, context);
            });
        }
        catch
        {
            return CompleteFailure(CompletePlateAppearanceFailure.Persistence);
        }
        return result;
    }

    public async Task<InitializeScoringSessionResult> InitializeAsync(
        int teamId,
        int gameId,
        TeamGameSide teamSide) => await InitializeAsync(teamId, gameId, teamSide,
            GameScoringSession.DefaultHomeRunLimit, false);

    public async Task<InitializeScoringSessionResult> InitializeAsync(
        int teamId,
        int gameId,
        TeamGameSide teamSide,
        int homeRunLimit,
        bool isOneUpEnabled)
    {
        if (teamId <= 0 || gameId <= 0 || !Enum.IsDefined(teamSide)
            || homeRunLimit < 0)
            return Failure(InitializeScoringSessionFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var result = Failure(InitializeScoringSessionFailure.InvalidTeamOrGame);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var existing = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(session => session.GameId == gameId);
                if (existing is not null)
                {
                    result = existing.Status == GameScoringStatus.Completed
                        ? new(existing, false, InitializeScoringSessionFailure.GameCompleted)
                        : new(existing, false, InitializeScoringSessionFailure.None);
                    return;
                }

                var team = transaction.Find<Team>(teamId);
                var game = transaction.Find<Game>(gameId);
                if (team is null || !team.IsActive || game is null || game.TeamId != teamId)
                    return;

                var lineup = transaction.Table<GameLineupPlayer>()
                    .Where(item => item.GameId == gameId)
                    .OrderBy(item => item.BattingOrder)
                    .ThenBy(item => item.Id)
                    .ToList();
                if (lineup.Count == 0
                    || lineup.Select(item => item.PlayerId).Distinct().Count() != lineup.Count
                    || !lineup.Select(item => item.BattingOrder)
                        .SequenceEqual(Enumerable.Range(1, lineup.Count)))
                {
                    result = Failure(InitializeScoringSessionFailure.InvalidLineup);
                    return;
                }

                var session = new GameScoringSession
                {
                    GameId = gameId,
                    Status = GameScoringStatus.InProgress,
                    TeamSide = teamSide,
                    HomeScore = 0,
                    VisitorScore = 0,
                    CurrentBatterPosition = 1,
                    CurrentInning = 1,
                    CurrentOuts = 0,
                    HomeRunLimit = homeRunLimit,
                    IsOneUpEnabled = isOneUpEnabled,
                };
                if (transaction.Insert(session) != 1)
                    throw new InvalidOperationException("Scoring session insertion did not affect one row.");

                foreach (var lineupPlayer in lineup)
                {
                    var sequencePlayer = new GameScoringLineupPlayer
                    {
                        GameScoringSessionId = session.Id,
                        PlayerId = lineupPlayer.PlayerId,
                        BattingPosition = lineupPlayer.BattingOrder,
                    };
                    if (transaction.Insert(sequencePlayer) != 1)
                        throw new InvalidOperationException(
                            "Live batting-sequence insertion did not affect one row.");
                }

                result = new(session, true, InitializeScoringSessionFailure.None);
            });
        }
        catch
        {
            var existing = await GetForGameAsync(gameId);
            if (existing?.Status == GameScoringStatus.InProgress)
                return new(existing, false, InitializeScoringSessionFailure.None);
            return Failure(InitializeScoringSessionFailure.Persistence);
        }

        return result;
    }

    private static InitializeScoringSessionResult Failure(InitializeScoringSessionFailure failure) =>
        new(null, false, failure);

    private static PendingScoringPlay InsertPendingPlay(SQLite.SQLiteConnection transaction,
        CompletionContext context, BatterResult result)
    {
        var play = new GameScoringPendingPlay
        {
            GameScoringSessionId = context.Session!.Id,
            BatterPlayerId = context.CurrentBatter!.PlayerId,
            BatterPosition = context.CurrentBatter.BattingPosition,
            Result = result,
        };
        if (transaction.Insert(play) != 1)
            throw new InvalidOperationException("Pending-play insertion did not affect one row.");
        var movements = CreateDefaultMovements(play.Id,
            context.CurrentBatter.PlayerId, context.BaseRunners!, result);
        foreach (var movement in movements)
            if (transaction.Insert(movement) != 1)
                throw new InvalidOperationException(
                    "Pending movement insertion did not affect one row.");
        return new(play, movements);
    }

    private CompletePlateAppearanceResult CompletePendingPlay(
        SQLite.SQLiteConnection transaction,
        int gameId,
        BatterResult? requiredResult = null,
        IReadOnlyDictionary<int, bool>? runRulings = null)
    {
        var context = LoadCompletionContext(transaction, gameId);
        if (context.Failure != CompletePlateAppearanceFailure.None)
            return CompleteFailure(context.Failure);
        var session = context.Session!;
        var sequence = context.Sequence!;
        var currentBatter = context.CurrentBatter!;
        var pending = transaction.Table<GameScoringPendingPlay>()
            .FirstOrDefault(item => item.GameScoringSessionId == session.Id);
        if (pending is null)
            return CompleteFailure(CompletePlateAppearanceFailure.PendingPlayNotFound);
        var movements = transaction.Table<GameScoringPendingRunnerMovement>()
            .Where(item => item.GameScoringPendingPlayId == pending.Id).ToList();
        if ((pending.Result is BatterResult.HomeRun or BatterResult.DeadBallOut)
                && IsHomeRunLegal(transaction, session)
                    != (pending.Result == BatterResult.HomeRun)
            || requiredResult is not null && pending.Result != requiredResult
            || pending.Result == BatterResult.Out && session.CurrentOuts >= 2
            || !IsValidPendingPlay(pending, movements, currentBatter,
                context.BaseRunners!, session.CurrentOuts))
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

        IReadOnlyList<GameScoringPendingRunnerMovement> committedMovements = movements;

        var outsAdded = CountOutsAdded(pending, committedMovements);
        var outsAfterPlay = session.CurrentOuts + outsAdded;
        if (!PlayContextFactory.TryRehydrate(
                session.CurrentOuts,
                pending,
                movements,
                context.BaseRunners!,
                out var playContext)
            || playContext is null)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);
        var resolution = playResolutionCoordinator.Evaluate(playContext);
        if (!resolution.IsAccepted
            || resolution.DecisionRequirement is
                PlayDecisionRequirement.ParticipantOutcome
                or PlayDecisionRequirement.RetirementSelection
            || resolution.Status == PlayResolutionStatus.AwaitingDecision
                && resolution.DecisionRequirement
                    != PlayDecisionRequirement.ThirdOutRuling)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);
        var runRulingRequirement = resolution.ThirdOutRulingRequirement;
        if (outsAfterPlay > 3
            || !TryResolveRunRulings(committedMovements, outsAfterPlay,
                runRulingRequirement, runRulings, out var resolvedRunRulings))
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

        PrepareCommittedHistory(transaction, session.Id);

        var previousAppearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id).ToList();
        var appearance = new GamePlateAppearance
        {
            GameScoringSessionId = session.Id,
            PlayerId = currentBatter.PlayerId,
            BattingPosition = currentBatter.BattingPosition,
            PlateAppearanceNumber = previousAppearances.Count == 0
                ? 1 : previousAppearances.Max(item => item.PlateAppearanceNumber) + 1,
            Inning = session.CurrentInning,
            Result = pending.Result,
        };
        if (transaction.Insert(appearance) != 1)
            throw new InvalidOperationException("Plate-appearance insertion did not affect one row.");
        foreach (var movement in committedMovements
                     .OrderBy(item => item.OutOrder ?? int.MaxValue)
                     .ThenByDescending(item => item.StartingLocation))
        {
            if (transaction.Insert(new GamePlateAppearanceRunnerMovement
            {
                GamePlateAppearanceId = appearance.Id,
                PlayerId = movement.PlayerId,
                StartingLocation = movement.StartingLocation,
                Destination = movement.Destination,
                SafeDestination = movement.SafeDestination,
                AttemptedDestination = movement.AttemptedDestination,
                OutOrder = movement.OutOrder,
                RunCounts = movement.Destination == RunnerDestination.Scores
                    ? resolvedRunRulings[movement.PlayerId]
                    : null,
            }) != 1)
                throw new InvalidOperationException(
                    "Runner-movement insertion did not affect one row.");
        }

        var runs = committedMovements.Count(item => item.Destination == RunnerDestination.Scores
            && resolvedRunRulings[item.PlayerId]);
        if (session.TeamSide == TeamGameSide.Home) session.HomeScore += runs;
        else if (session.TeamSide == TeamGameSide.Away) session.VisitorScore += runs;
        else throw new InvalidOperationException("Scoring session has an invalid Team side.");
        var nextIndex = (context.CurrentIndex + 1) % sequence.Count;
        session.CurrentBatterPosition = sequence[nextIndex].BattingPosition;
        session.CurrentOuts = outsAfterPlay;

        transaction.Execute(
            "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?", session.Id);
        if (outsAfterPlay == 3)
        {
            InsertCompletedHalfInning(transaction, session);
            session.CurrentInning++;
            session.CurrentOuts = 0;
        }
        else
        {
            foreach (var movement in committedMovements.Where(item =>
                item.Destination is RunnerDestination.First
                    or RunnerDestination.Second or RunnerDestination.Third))
            {
                if (transaction.Insert(new GameScoringBaseRunner
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = movement.PlayerId,
                    Base = (ScoringBase)movement.Destination,
                }) != 1)
                    throw new InvalidOperationException(
                        "Base-runner insertion did not affect one row.");
            }
        }

        if (transaction.Update(session) != 1)
            throw new InvalidOperationException(
                "Current-batter advancement did not affect one row.");
        ClearPending(transaction, session.Id);
        EvaluateAndPersistBeerConsequences(
            transaction, session, appearance,
            outsAfterPlay - outsAdded, outsAfterPlay, runs);
        AdvanceHistory(transaction, session.Id);
        return new(appearance, session.CurrentBatterPosition, runs,
            CompletePlateAppearanceFailure.None);
    }

    private static int CountOutsAdded(
        GameScoringPendingPlay pending,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements) =>
        (BatterRetirementEvaluator.AddsImplicitBatterOut(pending.Result) ? 1 : 0)
        + movements.Count(item => item.Destination == RunnerDestination.Out);

    private static bool IsHomeRunLegal(
        SQLite.SQLiteConnection transaction,
        GameScoringSession session)
    {
        var battingTeamHomeRuns = transaction.Table<GamePlateAppearance>()
            .Count(item => item.GameScoringSessionId == session.Id
                && item.Result == BatterResult.HomeRun);
        var opponentHomeRuns = transaction.Table<GameScoringHalfInning>()
            .Where(item => item.GameScoringSessionId == session.Id
                && item.OpponentEntryCompleted)
            .Sum(item => item.OpponentHomeRuns ?? 0);
        return HomeRunAvailabilityEvaluator.IsHomeRunLegal(
            session.HomeRunLimit,
            session.IsOneUpEnabled,
            battingTeamHomeRuns,
            opponentHomeRuns);
    }

    private static bool TryResolveRunRulings(
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int outsAfterPlay,
        ThirdOutRunRulingRequirement rulingRequirement,
        IReadOnlyDictionary<int, bool>? requestedRulings,
        out IReadOnlyDictionary<int, bool> resolvedRulings)
    {
        var scoringRunners = movements
            .Where(item => item.Destination == RunnerDestination.Scores)
            .OrderByDescending(item => item.StartingLocation)
            .ToArray();
        var result = new Dictionary<int, bool>();
        if (outsAfterPlay != 3)
        {
            foreach (var runner in scoringRunners) result[runner.PlayerId] = true;
            resolvedRulings = result;
            return true;
        }

        if (rulingRequirement == ThirdOutRunRulingRequirement.AutomaticNoRun)
        {
            foreach (var runner in scoringRunners) result[runner.PlayerId] = false;
            resolvedRulings = result;
            return true;
        }

        if (rulingRequirement != ThirdOutRunRulingRequirement.ScorerDecisionRequired)
        {
            resolvedRulings = new Dictionary<int, bool>();
            return scoringRunners.Length == 0;
        }

        var trailingRunsDoNotCount = false;
        foreach (var runner in scoringRunners)
        {
            if (trailingRunsDoNotCount)
            {
                result[runner.PlayerId] = false;
                continue;
            }
            if (requestedRulings is null
                || !requestedRulings.TryGetValue(runner.PlayerId, out var runCounts))
            {
                resolvedRulings = new Dictionary<int, bool>();
                return false;
            }
            result[runner.PlayerId] = runCounts;
            trailingRunsDoNotCount = !runCounts;
        }
        resolvedRulings = result;
        return true;
    }

    private CompletePlateAppearanceResult CompleteInningEndingMultiOut(
        SQLite.SQLiteConnection transaction,
        CompletionContext context,
        BatterResult result,
        int outsAdded,
        ThirdOutRunRulingRequirement rulingRequirement)
    {
        var session = context.Session!;
        var currentBatter = context.CurrentBatter!;
        if (session.CurrentOuts + outsAdded != 3
            || rulingRequirement != ThirdOutRunRulingRequirement.AutomaticNoRun)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

        PrepareCommittedHistory(transaction, session.Id);
        ClearPending(transaction, session.Id);
        var previousAppearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id).ToList();
        var appearance = new GamePlateAppearance
        {
            GameScoringSessionId = session.Id,
            PlayerId = currentBatter.PlayerId,
            BattingPosition = currentBatter.BattingPosition,
            PlateAppearanceNumber = previousAppearances.Count == 0
                ? 1 : previousAppearances.Max(item => item.PlateAppearanceNumber) + 1,
            Inning = session.CurrentInning,
            Result = result,
        };
        if (transaction.Insert(appearance) != 1)
            throw new InvalidOperationException(
                "Plate-appearance insertion did not affect one row.");

        var nextIndex = (context.CurrentIndex + 1) % context.Sequence!.Count;
        session.CurrentBatterPosition = context.Sequence[nextIndex].BattingPosition;
        InsertCompletedHalfInning(transaction, session);
        session.CurrentInning++;
        session.CurrentOuts = 0;
        transaction.Execute(
            "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?", session.Id);
        if (transaction.Update(session) != 1)
            throw new InvalidOperationException(
                "Scoring-session multi-out update did not affect one row.");

        EvaluateAndPersistBeerConsequences(
            transaction, session, appearance,
            3 - outsAdded, 3, 0);
        AdvanceHistory(transaction, session.Id);

        return new(appearance, session.CurrentBatterPosition, 0,
            CompletePlateAppearanceFailure.None);
    }

    private CompletePlateAppearanceResult CompleteImmediateOut(
        SQLite.SQLiteConnection transaction, CompletionContext context)
    {
        var session = context.Session!;
        if (context.BaseRunners!.Count > 0 && session.CurrentOuts < 2)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);
        PrepareCommittedHistory(transaction, session.Id);
        ClearPending(transaction, session.Id);
        var currentBatter = context.CurrentBatter!;
        var previousAppearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id).ToList();
        var appearance = new GamePlateAppearance
        {
            GameScoringSessionId = session.Id,
            PlayerId = currentBatter.PlayerId,
            BattingPosition = currentBatter.BattingPosition,
            PlateAppearanceNumber = previousAppearances.Count == 0
                ? 1 : previousAppearances.Max(item => item.PlateAppearanceNumber) + 1,
            Inning = session.CurrentInning,
            Result = BatterResult.Out,
        };
        if (transaction.Insert(appearance) != 1)
            throw new InvalidOperationException(
                "Plate-appearance insertion did not affect one row.");

        var sequence = context.Sequence!;
        var nextIndex = (context.CurrentIndex + 1) % sequence.Count;
        session.CurrentBatterPosition = sequence[nextIndex].BattingPosition;
        if (session.CurrentOuts == 2)
        {
            InsertCompletedHalfInning(transaction, session);
            session.CurrentInning++;
            session.CurrentOuts = 0;
            transaction.Execute(
                "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?",
                session.Id);
        }
        else
        {
            session.CurrentOuts++;
        }

        if (transaction.Update(session) != 1)
            throw new InvalidOperationException(
                "Scoring-session OUT update did not affect one row.");
        EvaluateAndPersistBeerConsequences(
            transaction, session, appearance,
            session.CurrentInning == appearance.Inning ? session.CurrentOuts - 1 : 2,
            session.CurrentInning == appearance.Inning ? session.CurrentOuts : 3,
            0);
        AdvanceHistory(transaction, session.Id);
        return new(appearance, session.CurrentBatterPosition, 0,
            CompletePlateAppearanceFailure.None);
    }

    private void EvaluateAndPersistBeerConsequences(
        SQLite.SQLiteConnection transaction,
        GameScoringSession session,
        GamePlateAppearance appearance,
        int outsBeforePlay,
        int outsAfterPlay,
        int runsScored)
    {
        var game = transaction.Find<Game>(session.GameId);
        var committedAppearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id
                && item.Result != null)
            .OrderBy(item => item.PlateAppearanceNumber)
            .ToList();
        var committedAppearanceIds = committedAppearances.Select(item => item.Id).ToHashSet();
        var committedMovements = transaction.Table<GamePlateAppearanceRunnerMovement>()
            .ToList()
            .Where(item => committedAppearanceIds.Contains(item.GamePlateAppearanceId))
            .GroupBy(item => item.GamePlateAppearanceId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id).ToArray());
        var beerAppearances = committedAppearances.Select(item =>
            CreateBeerPlateAppearance(item,
                committedMovements.GetValueOrDefault(item.Id, []))).ToArray();
        var beerAppearance = beerAppearances.Single(item => item.Id == appearance.Id);
        var battingSequence = transaction.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition)
            .Select(item => item.PlayerId)
            .ToArray();
        var consequences = beerRuleEvaluator.Evaluate(new BeerEvaluationContext(
            session.GameId,
            game?.TeamId,
            session.Id,
            session.TeamSide,
            beerAppearance,
            beerAppearances,
            battingSequence,
            outsBeforePlay,
            outsAfterPlay,
            runsScored)) ?? throw new InvalidOperationException(
                "Beer evaluation returned no consequence collection.");

        foreach (var consequence in consequences)
        {
            if (game is null
                || consequence.PlayerId <= 0
                || !Enum.IsDefined(consequence.RuleType)
                || consequence.Amount == 0
                || transaction.Find<Player>(consequence.PlayerId) is null)
                throw new InvalidOperationException(
                    "Beer evaluation returned an invalid consequence.");
            if (transaction.Table<BeerLedgerEntry>().Any(item =>
                    item.GameScoringSessionId == session.Id
                    && item.GamePlateAppearanceId == appearance.Id
                    && item.PlayerId == consequence.PlayerId
                    && item.RuleType == consequence.RuleType
                    && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring))
                continue;
            if (transaction.Insert(new BeerLedgerEntry
            {
                TeamId = game.TeamId,
                GameId = session.GameId,
                GameScoringSessionId = session.Id,
                PlayerId = consequence.PlayerId,
                RuleType = consequence.RuleType,
                Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                GamePlateAppearanceId = appearance.Id,
                Amount = consequence.Amount,
            }) != 1)
                throw new InvalidOperationException(
                    "Beer consequence insertion did not affect one row.");
        }
    }

    private static BeerCommittedPlateAppearance CreateBeerPlateAppearance(
        GamePlateAppearance appearance,
        IReadOnlyList<GamePlateAppearanceRunnerMovement> movements) =>
        new(
            appearance.Id,
            appearance.PlayerId,
            appearance.PlateAppearanceNumber,
            appearance.Inning,
            appearance.Result ?? throw new InvalidOperationException(
                "Beer evaluation requires final plate-appearance results."),
            movements.Select(item => new BeerRunnerMovement(
                item.PlayerId,
                item.StartingLocation,
                item.Destination,
                item.SafeDestination,
                item.AttemptedDestination,
                item.OutOrder,
                item.RunCounts)).ToArray());

    private static void InsertCompletedHalfInning(
        SQLite.SQLiteConnection transaction, GameScoringSession session)
    {
        var appearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == session.Id
                && item.Inning == session.CurrentInning)
            .ToList();
        var appearanceIds = appearances.Select(item => item.Id).ToHashSet();
        var runs = transaction.Table<GamePlateAppearanceRunnerMovement>()
            .ToList()
            .Count(item => appearanceIds.Contains(item.GamePlateAppearanceId)
                && item.Destination == RunnerDestination.Scores
                && item.RunCounts != false);
        var halfInning = new GameScoringHalfInning
        {
            GameScoringSessionId = session.Id,
            Inning = session.CurrentInning,
            TeamRuns = runs,
            TeamHomeRuns = appearances.Count(item => item.Result == BatterResult.HomeRun),
        };
        if (transaction.Insert(halfInning) != 1)
            throw new InvalidOperationException(
                "Completed half-inning insertion did not affect one row.");
    }

    private static CompletePlateAppearanceResult CompleteFailure(
        CompletePlateAppearanceFailure failure) =>
        new(null, 0, 0, failure);

    private static PendingPlayResult PendingFailure(PendingPlayFailure failure) =>
        new(null, failure);

    private static PendingPlayFailure MapPendingFailure(CompletePlateAppearanceFailure failure) =>
        failure switch
        {
            CompletePlateAppearanceFailure.SessionNotFound => PendingPlayFailure.SessionNotFound,
            CompletePlateAppearanceFailure.GameCompleted => PendingPlayFailure.GameCompleted,
            _ => PendingPlayFailure.InvalidScoringState,
        };

    private static CompletionContext LoadCompletionContext(SQLite.SQLiteConnection transaction,
        int gameId)
    {
        var session = transaction.Table<GameScoringSession>()
            .FirstOrDefault(item => item.GameId == gameId);
        if (session is null)
            return new(CompletePlateAppearanceFailure.SessionNotFound, null, null, null, null, -1);
        if (session.Status != GameScoringStatus.InProgress)
            return new(CompletePlateAppearanceFailure.GameCompleted, session, null, null, null, -1);
        if (session.CurrentInning < 1 || session.CurrentOuts is < 0 or > 2)
            return new(CompletePlateAppearanceFailure.InvalidScoringState,
                session, null, null, null, -1);
        var incompleteHalfInning = transaction.Table<GameScoringHalfInning>()
            .FirstOrDefault(item => item.GameScoringSessionId == session.Id
                && !item.OpponentEntryCompleted);
        if (incompleteHalfInning is not null)
            return new(CompletePlateAppearanceFailure.InvalidScoringState,
                session, null, null, null, -1);
        var sequence = transaction.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition).ThenBy(item => item.Id).ToList();
        var bases = transaction.Table<GameScoringBaseRunner>()
            .Where(item => item.GameScoringSessionId == session.Id).ToList();
        var currentIndex = sequence.FindIndex(item =>
            item.BattingPosition == session.CurrentBatterPosition);
        if (sequence.Count == 0 || currentIndex < 0
            || sequence.Any(item => item.BattingPosition <= 0 || item.PlayerId <= 0)
            || sequence.Select(item => item.BattingPosition).Distinct().Count() != sequence.Count
            || !IsValidBaseState(bases))
            return new(CompletePlateAppearanceFailure.InvalidScoringState,
                session, sequence, null, bases, currentIndex);
        return new(CompletePlateAppearanceFailure.None, session, sequence,
            sequence[currentIndex], bases, currentIndex);
    }

    private static bool IsValidBaseState(IReadOnlyList<GameScoringBaseRunner> bases) =>
        bases.All(item => item.PlayerId > 0 && Enum.IsDefined(item.Base))
        && bases.Select(item => item.Base).Distinct().Count() == bases.Count
        && bases.Select(item => item.PlayerId).Distinct().Count() == bases.Count;

    private static List<GameScoringPendingRunnerMovement> CreateDefaultMovements(
        int pendingPlayId, int batterPlayerId, IReadOnlyList<GameScoringBaseRunner> bases,
        BatterResult result)
    {
        var definition = PlateAppearanceResultDefinitions.Get(result);
        var occupiedStartingLocations = bases
            .Select(item => (RunnerStartingLocation)item.Base)
            .ToHashSet();
        var movements = bases.Select(runner => new GameScoringPendingRunnerMovement
        {
            GameScoringPendingPlayId = pendingPlayId,
            PlayerId = runner.PlayerId,
            StartingLocation = (RunnerStartingLocation)runner.Base,
            Destination = definition.GetExistingRunnerDefaultDestination(
                (RunnerStartingLocation)runner.Base, occupiedStartingLocations),
            RequiresOutcomeConfirmation = definition.RequiresOutcomeConfirmation(
                (RunnerStartingLocation)runner.Base),
        }).ToList();
        if (definition.IncludesBatterMovement)
        {
            movements.Add(new()
            {
                GameScoringPendingPlayId = pendingPlayId,
                PlayerId = batterPlayerId,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = definition.BatterDefaultDestination,
                RequiresOutcomeConfirmation = definition.RequiresOutcomeConfirmation(
                    RunnerStartingLocation.Home),
            });
        }
        return movements;
    }

    private static bool TrySynchronizePendingPlay(
        SQLite.SQLiteConnection transaction,
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> currentMovements,
        PlayContext context,
        out List<GameScoringPendingRunnerMovement> synchronizedMovements)
    {
        synchronizedMovements = [];
        if (context.BatterPlayerId != play.BatterPlayerId) return false;
        if (context.Result == play.Result)
        {
            synchronizedMovements = currentMovements.ToList();
            if (!PlayContextFactory.TryApplyToPendingMovements(
                    context, synchronizedMovements))
                return false;
            foreach (var movement in synchronizedMovements)
                if (transaction.Update(movement) != 1)
                    throw new InvalidOperationException(
                        "Pending movement update did not affect one row.");
            return true;
        }
        if (context.Result != BatterResult.Out
            || play.Result is not BatterResult.FielderChoice
                and not BatterResult.SacrificeFly)
            return false;
        var existing = context.Participants.Where(item =>
            item.StartingLocation != RunnerStartingLocation.Home).ToArray();
        if (existing.Any(item => item.OutOrder is not null
                || item.RequiresOutcomeConfirmation)
            || existing.Select(item => item.PlayerId).Distinct().Count() != existing.Length
            || existing.Select(item => item.StartingLocation).Distinct().Count()
                != existing.Length)
            return false;
        play.Result = BatterResult.Out;
        if (transaction.Update(play) != 1)
            throw new InvalidOperationException(
                "Pending-play reconciliation update did not affect one row.");
        transaction.Execute(
            "DELETE FROM GameScoringPendingRunnerMovement WHERE GameScoringPendingPlayId = ?",
            play.Id);
        synchronizedMovements = existing.Select(item =>
            new GameScoringPendingRunnerMovement
            {
                GameScoringPendingPlayId = play.Id,
                PlayerId = item.PlayerId,
                StartingLocation = item.StartingLocation,
                Destination = item.Destination,
                SafeDestination = item.SafeDestination,
                AttemptedDestination = item.AttemptedDestination,
                OutOrder = null,
                RequiresOutcomeConfirmation = false,
            }).ToList();
        foreach (var movement in synchronizedMovements)
            if (transaction.Insert(movement) != 1)
                throw new InvalidOperationException(
                    "Reconciled pending movement insertion did not affect one row.");
        return true;
    }

    private bool IsValidPendingPlay(GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        GameScoringLineupPlayer currentBatter,
        IReadOnlyList<GameScoringBaseRunner> bases,
        int currentOuts)
    {
        if (play.BatterPlayerId != currentBatter.PlayerId
            || play.BatterPosition != currentBatter.BattingPosition
            || !PlayContextFactory.TryRehydrate(
                currentOuts, play, movements, bases, out var playContext))
            return false;

        return playResolutionCoordinator.Evaluate(playContext).IsAccepted;
    }

    private static void ClearPending(SQLite.SQLiteConnection transaction, int sessionId)
    {
        var pending = transaction.Table<GameScoringPendingPlay>()
            .FirstOrDefault(item => item.GameScoringSessionId == sessionId);
        if (pending is null) return;
        transaction.Execute("DELETE FROM GameScoringPendingRunnerMovement WHERE GameScoringPendingPlayId = ?", pending.Id);
        transaction.Delete(pending);
    }

    private static ScoringUndoRedoAvailability GetUndoRedoAvailability(
        SQLite.SQLiteConnection transaction,
        GameScoringSession? session)
    {
        if (session is null || session.Status != GameScoringStatus.InProgress
            || HasPendingHalfInningTransition(transaction, session.Id))
            return new(false, false);
        var history = transaction.Find<GameScoringHistory>(session.Id);
        return history is null
            ? new(false, false)
            : new(history.CurrentPosition > 0,
                history.CurrentPosition < history.LatestPosition);
    }

    private static bool HasUnresolvedScoringState(
        SQLite.SQLiteConnection transaction, int sessionId) =>
        transaction.Table<GameScoringPendingPlay>()
            .Any(item => item.GameScoringSessionId == sessionId)
        || HasPendingHalfInningTransition(transaction, sessionId);

    private static bool HasPendingHalfInningTransition(
        SQLite.SQLiteConnection transaction, int sessionId) =>
        transaction.Table<GameScoringHalfInning>()
            .Any(item => item.GameScoringSessionId == sessionId
                && !item.OpponentEntryCompleted);

    private static void EnsureHistoryInitialized(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        if (transaction.Find<GameScoringHistory>(sessionId) is not null) return;
        var history = new GameScoringHistory
        {
            GameScoringSessionId = sessionId,
            CurrentPosition = 0,
            LatestPosition = 0,
            PendingStartPosition = null,
        };
        if (transaction.Insert(history) != 1)
            throw new InvalidOperationException(
                "Scoring-history initialization did not affect one row.");
        InsertSnapshot(transaction, sessionId, 0);
    }

    private static void AdvanceHistory(SQLite.SQLiteConnection transaction, int sessionId)
    {
        EnsureHistoryInitialized(transaction, sessionId);
        var history = transaction.Find<GameScoringHistory>(sessionId)
            ?? throw new InvalidOperationException("Scoring history was not initialized.");
        transaction.Execute(
            "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position > ?",
            sessionId, history.CurrentPosition);
        var nextPosition = checked(history.CurrentPosition + 1);
        InsertSnapshot(transaction, sessionId, nextPosition);
        history.CurrentPosition = nextPosition;
        history.LatestPosition = nextPosition;
        if (transaction.Update(history) != 1)
            throw new InvalidOperationException(
                "Scoring-history advancement did not affect one row.");
    }

    private static void RecordPendingCheckpoint(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        EnsureHistoryInitialized(transaction, sessionId);
        var history = transaction.Find<GameScoringHistory>(sessionId)
            ?? throw new InvalidOperationException("Scoring history was not initialized.");
        if (history.PendingStartPosition is null
            || history.CurrentPosition < history.PendingStartPosition)
            history.PendingStartPosition = history.CurrentPosition;

        if (history.CurrentPosition < history.LatestPosition)
        {
            transaction.Execute(
                "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position > ?",
                sessionId, history.CurrentPosition);
            history.LatestPosition = history.CurrentPosition;
        }

        var stateJson = JsonSerializer.Serialize(CaptureSnapshot(transaction, sessionId));
        var matchingPosition = transaction.Table<GameScoringHistorySnapshot>()
            .Where(item => item.GameScoringSessionId == sessionId
                && item.Position > history.PendingStartPosition.Value
                && item.Position <= history.CurrentPosition)
            .ToList()
            .Where(item => item.StateJson == stateJson)
            .Select(item => (int?)item.Position)
            .Max();
        if (matchingPosition is not null)
        {
            transaction.Execute(
                "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position > ?",
                sessionId, matchingPosition.Value);
            history.CurrentPosition = matchingPosition.Value;
            history.LatestPosition = matchingPosition.Value;
        }
        else
        {
            var nextPosition = checked(history.CurrentPosition + 1);
            InsertSnapshot(transaction, sessionId, nextPosition, stateJson);
            history.CurrentPosition = nextPosition;
            history.LatestPosition = nextPosition;
        }

        if (transaction.Update(history) != 1)
            throw new InvalidOperationException(
                "Pending scoring-history update did not affect one row.");
    }

    private static void PrepareCommittedHistory(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        EnsureHistoryInitialized(transaction, sessionId);
        var history = transaction.Find<GameScoringHistory>(sessionId)
            ?? throw new InvalidOperationException("Scoring history was not initialized.");
        if (history.PendingStartPosition is null) return;
        var committedStartPosition = Math.Min(
            history.CurrentPosition, history.PendingStartPosition.Value);
        transaction.Execute(
            "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position > ?",
            sessionId, committedStartPosition);
        history.CurrentPosition = committedStartPosition;
        history.LatestPosition = committedStartPosition;
        history.PendingStartPosition = null;
        if (transaction.Update(history) != 1)
            throw new InvalidOperationException(
                "Committed scoring-history preparation did not affect one row.");
    }

    private static void RewindPendingHistory(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        var history = transaction.Find<GameScoringHistory>(sessionId);
        if (history?.PendingStartPosition is null) return;
        transaction.Execute(
            "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position > ?",
            sessionId, history.PendingStartPosition.Value);
        history.CurrentPosition = history.PendingStartPosition.Value;
        history.LatestPosition = history.PendingStartPosition.Value;
        history.PendingStartPosition = null;
        if (transaction.Update(history) != 1)
            throw new InvalidOperationException(
                "Pending scoring-history rewind did not affect one row.");
    }

    private static void ReplaceCurrentHistorySnapshot(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        EnsureHistoryInitialized(transaction, sessionId);
        var history = transaction.Find<GameScoringHistory>(sessionId)
            ?? throw new InvalidOperationException("Scoring history was not initialized.");
        transaction.Execute(
            "DELETE FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ? AND Position = ?",
            sessionId, history.CurrentPosition);
        InsertSnapshot(transaction, sessionId, history.CurrentPosition);
    }

    private static void InsertSnapshot(
        SQLite.SQLiteConnection transaction, int sessionId, int position)
    {
        var stateJson = JsonSerializer.Serialize(CaptureSnapshot(transaction, sessionId));
        InsertSnapshot(transaction, sessionId, position, stateJson);
    }

    private static void InsertSnapshot(
        SQLite.SQLiteConnection transaction, int sessionId, int position,
        string stateJson)
    {
        if (transaction.Insert(new GameScoringHistorySnapshot
        {
            GameScoringSessionId = sessionId,
            Position = position,
            StateJson = stateJson,
        }) != 1)
            throw new InvalidOperationException(
                "Scoring-history snapshot insertion did not affect one row.");
    }

    private static PersistedScoringState CaptureSnapshot(
        SQLite.SQLiteConnection transaction, int sessionId)
    {
        var session = transaction.Find<GameScoringSession>(sessionId)
            ?? throw new InvalidOperationException("Scoring session was not found.");
        var appearances = transaction.Table<GamePlateAppearance>()
            .Where(item => item.GameScoringSessionId == sessionId)
            .OrderBy(item => item.PlateAppearanceNumber).ToList();
        var appearanceIds = appearances.Select(item => item.Id).ToHashSet();
        var state = new PersistedScoringState
        {
            Session = new PersistedSessionState
            {
                HomeScore = session.HomeScore,
                VisitorScore = session.VisitorScore,
                CurrentBatterPosition = session.CurrentBatterPosition,
                CurrentInning = session.CurrentInning,
                CurrentOuts = session.CurrentOuts,
            },
            PlateAppearances = appearances,
            BaseRunners = transaction.Table<GameScoringBaseRunner>()
                .Where(item => item.GameScoringSessionId == sessionId)
                .OrderBy(item => item.Base).ToList(),
            RunnerMovements = transaction.Table<GamePlateAppearanceRunnerMovement>()
                .ToList().Where(item => appearanceIds.Contains(item.GamePlateAppearanceId))
                .OrderBy(item => item.GamePlateAppearanceId).ThenBy(item => item.Id).ToList(),
            HalfInnings = transaction.Table<GameScoringHalfInning>()
                .Where(item => item.GameScoringSessionId == sessionId)
                .OrderBy(item => item.Inning).ToList(),
            PendingPlay = transaction.Table<GameScoringPendingPlay>()
                .FirstOrDefault(item => item.GameScoringSessionId == sessionId),
            AutomaticBeerEntries = transaction.Table<BeerLedgerEntry>()
                .Where(item => item.GameScoringSessionId == sessionId
                    && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
                .OrderBy(item => item.Id).ToList(),
        };
        if (state.PendingPlay is not null)
            state.PendingMovements = transaction.Table<GameScoringPendingRunnerMovement>()
                .Where(item => item.GameScoringPendingPlayId == state.PendingPlay.Id)
                .OrderBy(item => item.Id).ToList();
        return state;
    }

    private static void RestoreSnapshot(
        SQLite.SQLiteConnection transaction,
        GameScoringSession session,
        GameScoringHistorySnapshot snapshot)
    {
        var state = JsonSerializer.Deserialize<PersistedScoringState>(snapshot.StateJson)
            ?? throw new InvalidOperationException("Scoring-history snapshot is invalid.");
        var game = transaction.Find<Game>(session.GameId);
        if (state.Session is null
            || state.PlateAppearances.Any(item => item.GameScoringSessionId != session.Id)
            || state.BaseRunners.Any(item => item.GameScoringSessionId != session.Id)
            || state.HalfInnings.Any(item => item.GameScoringSessionId != session.Id)
            || state.PendingPlay is not null
                && state.PendingPlay.GameScoringSessionId != session.Id
            || state.PendingPlay is null && state.PendingMovements.Count > 0
            || state.PendingPlay is not null && state.PendingMovements.Any(item =>
                item.GameScoringPendingPlayId != state.PendingPlay.Id)
            || state.AutomaticBeerEntries.Any(item =>
                game is null
                || item.GameScoringSessionId != session.Id
                || item.GameId != session.GameId
                || item.TeamId != game.TeamId
                || item.Origin != BeerLedgerEntryOrigin.AutomaticScoring))
            throw new InvalidOperationException("Scoring-history snapshot has invalid ownership.");

        transaction.Execute(
            "DELETE FROM GameScoringPendingRunnerMovement WHERE GameScoringPendingPlayId IN (SELECT Id FROM GameScoringPendingPlay WHERE GameScoringSessionId = ?)",
            session.Id);
        transaction.Execute(
            "DELETE FROM GameScoringPendingPlay WHERE GameScoringSessionId = ?", session.Id);
        transaction.Execute(
            "DELETE FROM GamePlateAppearanceRunnerMovement WHERE GamePlateAppearanceId IN (SELECT Id FROM GamePlateAppearance WHERE GameScoringSessionId = ?)",
            session.Id);
        transaction.Execute(
            "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?", session.Id);
        transaction.Execute(
            "DELETE FROM GameScoringHalfInning WHERE GameScoringSessionId = ?", session.Id);
        transaction.Execute(
            "DELETE FROM GamePlateAppearance WHERE GameScoringSessionId = ?", session.Id);
        transaction.Execute(
            "DELETE FROM BeerLedgerEntry WHERE GameScoringSessionId = ? AND Origin = ?",
            session.Id, BeerLedgerEntryOrigin.AutomaticScoring);

        foreach (var appearance in state.PlateAppearances)
            InsertRestoredRow(transaction, appearance, "plate appearance");
        var appearanceIds = state.PlateAppearances.Select(item => item.Id).ToHashSet();
        if (state.RunnerMovements.Any(item =>
                !appearanceIds.Contains(item.GamePlateAppearanceId)))
            throw new InvalidOperationException(
                "Scoring-history runner movement has invalid ownership.");
        foreach (var movement in state.RunnerMovements)
            InsertRestoredRow(transaction, movement, "runner movement");
        if (state.AutomaticBeerEntries.Any(item =>
                item.GamePlateAppearanceId is null
                || !appearanceIds.Contains(item.GamePlateAppearanceId.Value)))
            throw new InvalidOperationException(
                "Scoring-history Beer entry has invalid source ownership.");
        foreach (var entry in state.AutomaticBeerEntries)
            InsertRestoredRow(transaction, entry, "Beer entry");
        foreach (var runner in state.BaseRunners)
            InsertRestoredRow(transaction, runner, "base runner");
        foreach (var halfInning in state.HalfInnings)
            InsertRestoredRow(transaction, halfInning, "half-inning");
        if (state.PendingPlay is not null)
        {
            InsertRestoredRow(transaction, state.PendingPlay, "pending play");
            foreach (var movement in state.PendingMovements)
                InsertRestoredRow(transaction, movement, "pending movement");
        }

        session.HomeScore = state.Session.HomeScore;
        session.VisitorScore = state.Session.VisitorScore;
        session.CurrentBatterPosition = state.Session.CurrentBatterPosition;
        session.CurrentInning = state.Session.CurrentInning;
        session.CurrentOuts = state.Session.CurrentOuts;
        if (transaction.Update(session) != 1)
            throw new InvalidOperationException(
                "Scoring-history session restoration did not affect one row.");
    }

    private static void InsertRestoredRow<T>(
        SQLite.SQLiteConnection transaction, T item, string description)
    {
        var affected = item switch
        {
            GamePlateAppearance appearance => transaction.Execute(
                "INSERT INTO GamePlateAppearance (Id, GameScoringSessionId, PlayerId, BattingPosition, PlateAppearanceNumber, Inning, Result) VALUES (?, ?, ?, ?, ?, ?, ?)",
                appearance.Id, appearance.GameScoringSessionId, appearance.PlayerId,
                appearance.BattingPosition, appearance.PlateAppearanceNumber,
                appearance.Inning, appearance.Result),
            GamePlateAppearanceRunnerMovement movement => transaction.Execute(
                "INSERT INTO GamePlateAppearanceRunnerMovement (Id, GamePlateAppearanceId, PlayerId, StartingLocation, Destination, SafeDestination, AttemptedDestination, OutOrder, RunCounts) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                movement.Id, movement.GamePlateAppearanceId, movement.PlayerId,
                movement.StartingLocation, movement.Destination,
                movement.SafeDestination, movement.AttemptedDestination,
                movement.OutOrder, movement.RunCounts),
            GameScoringBaseRunner runner => transaction.Execute(
                "INSERT INTO GameScoringBaseRunner (Id, GameScoringSessionId, PlayerId, Base) VALUES (?, ?, ?, ?)",
                runner.Id, runner.GameScoringSessionId, runner.PlayerId, runner.Base),
            GameScoringHalfInning halfInning => transaction.Execute(
                "INSERT INTO GameScoringHalfInning (Id, GameScoringSessionId, Inning, TeamRuns, TeamHomeRuns, TeamSummaryAcknowledged, OpponentRuns, OpponentHomeRuns, OpponentEntryCompleted) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                halfInning.Id, halfInning.GameScoringSessionId, halfInning.Inning,
                halfInning.TeamRuns, halfInning.TeamHomeRuns,
                halfInning.TeamSummaryAcknowledged, halfInning.OpponentRuns,
                halfInning.OpponentHomeRuns, halfInning.OpponentEntryCompleted),
            GameScoringPendingPlay pending => transaction.Execute(
                "INSERT INTO GameScoringPendingPlay (Id, GameScoringSessionId, BatterPlayerId, BatterPosition, Result) VALUES (?, ?, ?, ?, ?)",
                pending.Id, pending.GameScoringSessionId, pending.BatterPlayerId,
                pending.BatterPosition, pending.Result),
            GameScoringPendingRunnerMovement pendingMovement => transaction.Execute(
                "INSERT INTO GameScoringPendingRunnerMovement (Id, GameScoringPendingPlayId, PlayerId, StartingLocation, Destination, SafeDestination, AttemptedDestination, OutOrder, RequiresOutcomeConfirmation) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                pendingMovement.Id, pendingMovement.GameScoringPendingPlayId,
                pendingMovement.PlayerId, pendingMovement.StartingLocation,
                pendingMovement.Destination, pendingMovement.SafeDestination,
                pendingMovement.AttemptedDestination, pendingMovement.OutOrder,
                pendingMovement.RequiresOutcomeConfirmation),
            BeerLedgerEntry entry => transaction.Execute(
                "INSERT INTO BeerLedgerEntry (Id, TeamId, GameId, GameScoringSessionId, PlayerId, RuleType, Origin, GamePlateAppearanceId, Amount) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                entry.Id, entry.TeamId, entry.GameId, entry.GameScoringSessionId,
                entry.PlayerId, entry.RuleType, entry.Origin,
                entry.GamePlateAppearanceId, entry.Amount),
            _ => throw new InvalidOperationException(
                $"Unsupported scoring-history row type {typeof(T).Name}."),
        };
        if (affected != 1)
            throw new InvalidOperationException(
                $"Scoring-history {description} restoration did not affect one row.");
    }

    private sealed class PersistedScoringState
    {
        public PersistedSessionState? Session { get; set; }

        public List<GamePlateAppearance> PlateAppearances { get; set; } = [];

        public List<GameScoringBaseRunner> BaseRunners { get; set; } = [];

        public List<GamePlateAppearanceRunnerMovement> RunnerMovements { get; set; } = [];

        public List<GameScoringHalfInning> HalfInnings { get; set; } = [];

        public GameScoringPendingPlay? PendingPlay { get; set; }

        public List<GameScoringPendingRunnerMovement> PendingMovements { get; set; } = [];

        public List<BeerLedgerEntry> AutomaticBeerEntries { get; set; } = [];
    }

    private sealed class PersistedSessionState
    {
        public int HomeScore { get; set; }

        public int VisitorScore { get; set; }

        public int CurrentBatterPosition { get; set; }

        public int CurrentInning { get; set; }

        public int CurrentOuts { get; set; }
    }

    private sealed record CompletionContext(
        CompletePlateAppearanceFailure Failure,
        GameScoringSession? Session,
        List<GameScoringLineupPlayer>? Sequence,
        GameScoringLineupPlayer? CurrentBatter,
        List<GameScoringBaseRunner>? BaseRunners,
        int CurrentIndex);
}
