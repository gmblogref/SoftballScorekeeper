using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameScoringSessionRepository
{
    private readonly AppDatabase database;

    public GameScoringSessionRepository(AppDatabase database) => this.database = database;

    public async Task<GameScoringSession?> GetForGameAsync(int gameId)
    {
        if (gameId <= 0) return null;
        var connection = await database.GetConnectionAsync();
        return await connection.Table<GameScoringSession>()
            .Where(session => session.GameId == gameId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsForGameAsync(int gameId) =>
        await GetForGameAsync(gameId) is not null;

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
        if (pending is not null)
        {
            var pendingMovements = await connection.Table<GameScoringPendingRunnerMovement>()
                .Where(item => item.GameScoringPendingPlayId == pending.Id)
                .OrderBy(item => item.StartingLocation)
                .ToListAsync();
            pendingPlay = new(pending, pendingMovements);
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
                homeHomeRuns, visitorHomeRuns);

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
            visitorHomeRuns);
    }

    public async Task<PendingPlayResult> SelectBatterResultAsync(int gameId, BatterResult result)
    {
        if (gameId <= 0 || !Enum.IsDefined(result) || result == BatterResult.Out)
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
                foreach (var movement in movements)
                    if (transaction.Insert(movement) != 1)
                        throw new InvalidOperationException("Pending movement insertion did not affect one row.");
                response = new(new(play, movements), PendingPlayFailure.None);
            });
        }
        catch
        {
            return PendingFailure(PendingPlayFailure.Persistence);
        }
        return response;
    }

    public async Task<PendingPlayResult> AdjustPendingRunnerAsync(
        int gameId, int playerId, RunnerDestination destination)
    {
        if (gameId <= 0 || playerId <= 0 || !Enum.IsDefined(destination))
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
                if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                        play, movements, out var resolverState)
                    || resolverState is null)
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                var resolution = BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, destination);
                if (!resolution.IsSuccess
                    || !IsWithinOutLimit(session.CurrentOuts, resolution))
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                foreach (var item in movements)
                {
                    item.Destination = resolution.Participants
                        .Single(participant => participant.PlayerId == item.PlayerId)
                        .Destination;
                    if (transaction.Update(item) != 1)
                        throw new InvalidOperationException(
                            "Pending movement update did not affect one row.");
                }
                response = new(new(play, movements), PendingPlayFailure.None);
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
        if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                play, movements, out var resolverState)
            || resolverState is null)
            return new([], PendingPlayFailure.InvalidScoringState);
        var destinations = BaseRunnerResolver.GetAvailableDestinations(
                resolverState, playerId)
            .Where(destination => IsWithinOutLimit(session.CurrentOuts,
                BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, destination)))
            .ToArray();
        return new(destinations, PendingPlayFailure.None);
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
                    context.BaseRunners!))
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
                var ids = outsAfterPlay == 3
                    ? movements.Where(item => item.Destination == RunnerDestination.Scores)
                        .OrderByDescending(item => item.StartingLocation)
                        .Select(item => item.PlayerId).ToArray()
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

                ClearPending(transaction, context.Session!.Id);
                if (context.BaseRunners!.Count > 0 && context.Session.CurrentOuts < 2)
                {
                    response = new(InsertPendingPlay(
                        transaction, context, BatterResult.Out), PendingPlayFailure.None);
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

    private static CompletePlateAppearanceResult CompletePendingPlay(
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
        if (requiredResult is not null && pending.Result != requiredResult
            || pending.Result == BatterResult.Out && session.CurrentOuts >= 2
            || !IsValidPendingPlay(pending, movements, currentBatter,
                context.BaseRunners!))
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

        var outsAdded = CountOutsAdded(pending, movements);
        var outsAfterPlay = session.CurrentOuts + outsAdded;
        if (outsAfterPlay > 3
            || !TryResolveRunRulings(movements, outsAfterPlay, runRulings,
                out var resolvedRunRulings))
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

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
        foreach (var movement in movements)
        {
            if (transaction.Insert(new GamePlateAppearanceRunnerMovement
            {
                GamePlateAppearanceId = appearance.Id,
                PlayerId = movement.PlayerId,
                StartingLocation = movement.StartingLocation,
                Destination = movement.Destination,
                RunCounts = movement.Destination == RunnerDestination.Scores
                    ? resolvedRunRulings[movement.PlayerId]
                    : null,
            }) != 1)
                throw new InvalidOperationException(
                    "Runner-movement insertion did not affect one row.");
        }

        var runs = movements.Count(item => item.Destination == RunnerDestination.Scores
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
            foreach (var movement in movements.Where(item =>
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
        return new(appearance, session.CurrentBatterPosition, runs,
            CompletePlateAppearanceFailure.None);
    }

    private static int CountOutsAdded(
        GameScoringPendingPlay pending,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements) =>
        (pending.Result == BatterResult.Out ? 1 : 0)
        + movements.Count(item => item.Destination == RunnerDestination.Out);

    private static bool TryResolveRunRulings(
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int outsAfterPlay,
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

    private static CompletePlateAppearanceResult CompleteImmediateOut(
        SQLite.SQLiteConnection transaction, CompletionContext context)
    {
        var session = context.Session!;
        if (context.BaseRunners!.Count > 0 && session.CurrentOuts < 2)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);
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
        return new(appearance, session.CurrentBatterPosition, 0,
            CompletePlateAppearanceFailure.None);
    }

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

    private static bool IsWithinOutLimit(
        int currentOuts, BaseRunnerResolutionResult resolution) =>
        resolution.IsSuccess
        && currentOuts + resolution.Participants.Count(item =>
            item.Destination == RunnerDestination.Out) <= 3;

    private static List<GameScoringPendingRunnerMovement> CreateDefaultMovements(
        int pendingPlayId, int batterPlayerId, IReadOnlyList<GameScoringBaseRunner> bases,
        BatterResult result)
    {
        if (result == BatterResult.Out)
            return bases.Select(runner => new GameScoringPendingRunnerMovement
            {
                GameScoringPendingPlayId = pendingPlayId,
                PlayerId = runner.PlayerId,
                StartingLocation = (RunnerStartingLocation)runner.Base,
                Destination = (RunnerDestination)runner.Base,
            }).ToList();

        var advance = (int)result;
        var movements = bases.Select(runner => new GameScoringPendingRunnerMovement
        {
            GameScoringPendingPlayId = pendingPlayId,
            PlayerId = runner.PlayerId,
            StartingLocation = (RunnerStartingLocation)runner.Base,
            Destination = (int)runner.Base + advance > 3
                ? RunnerDestination.Scores
                : (RunnerDestination)((int)runner.Base + advance),
        }).ToList();
        movements.Add(new()
        {
            GameScoringPendingPlayId = pendingPlayId,
            PlayerId = batterPlayerId,
            StartingLocation = RunnerStartingLocation.Home,
            Destination = advance > 3 ? RunnerDestination.Scores : (RunnerDestination)advance,
        });
        return movements;
    }

    private static bool IsValidPendingPlay(GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        GameScoringLineupPlayer currentBatter,
        IReadOnlyList<GameScoringBaseRunner> bases)
    {
        var batterReachedBase = play.Result != BatterResult.Out;
        if (!Enum.IsDefined(play.Result) || play.BatterPlayerId != currentBatter.PlayerId
            || play.BatterPosition != currentBatter.BattingPosition
            || movements.Count != bases.Count + (batterReachedBase ? 1 : 0)
            || movements.Select(item => item.PlayerId).Distinct().Count() != movements.Count
            || movements.Any(item => !Enum.IsDefined(item.StartingLocation)
                || !Enum.IsDefined(item.Destination))) return false;
        var expectedStarts = bases.Select(item =>
            (item.PlayerId, (RunnerStartingLocation)item.Base)).ToHashSet();
        if (batterReachedBase)
            expectedStarts.Add((currentBatter.PlayerId, RunnerStartingLocation.Home));
        if (!movements.Select(item => (item.PlayerId, item.StartingLocation))
            .ToHashSet().SetEquals(expectedStarts)) return false;
        return movements.Where(item => item.Destination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third)
            .Select(item => item.Destination).Distinct().Count()
            == movements.Count(item => item.Destination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third);
    }

    private static void ClearPending(SQLite.SQLiteConnection transaction, int sessionId)
    {
        var pending = transaction.Table<GameScoringPendingPlay>()
            .FirstOrDefault(item => item.GameScoringSessionId == sessionId);
        if (pending is null) return;
        transaction.Execute("DELETE FROM GameScoringPendingRunnerMovement WHERE GameScoringPendingPlayId = ?", pending.Id);
        transaction.Delete(pending);
    }

    private sealed record CompletionContext(
        CompletePlateAppearanceFailure Failure,
        GameScoringSession? Session,
        List<GameScoringLineupPlayer>? Sequence,
        GameScoringLineupPlayer? CurrentBatter,
        List<GameScoringBaseRunner>? BaseRunners,
        int CurrentIndex);
}
