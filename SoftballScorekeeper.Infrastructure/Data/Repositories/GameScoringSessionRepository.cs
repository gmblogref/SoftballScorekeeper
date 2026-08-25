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

                if (definition.RequiredPlayOuts > 0
                    && context.Session.CurrentOuts + definition.RequiredPlayOuts == 3)
                {
                    var completed = CompleteInningEndingMultiOut(
                        transaction, context, result, definition.RequiredPlayOuts);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
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
                var requiredMultiOutRetirements = definition.RequiredPlayOuts == 0
                    ? 0 : definition.RequiredPlayOuts;
                if (requiredMultiOutRetirements > 0
                    && movements.Count == requiredMultiOutRetirements)
                {
                    var order = 1;
                    foreach (var movement in movements
                                 .OrderByDescending(item => item.StartingLocation))
                    {
                        movement.Destination = RunnerDestination.Out;
                        movement.SafeDestination = null;
                        movement.AttemptedDestination = null;
                        movement.OutOrder = order++;
                        movement.RequiresOutcomeConfirmation = false;
                        if (transaction.Update(movement) != 1)
                            throw new InvalidOperationException(
                                "Pending movement update did not affect one row.");
                    }
                    var completed = CompletePendingPlay(transaction, gameId, result);
                    if (!completed.IsSuccess)
                    {
                        response = PendingFailure(MapPendingFailure(completed.FailureReason));
                        return;
                    }
                    response = new(null, PendingPlayFailure.None);
                    return;
                }
                if (result == BatterResult.Walk)
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
                    || definition is null
                    || definition.RequiredPlayOuts == 0)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidScoringState);
                    return;
                }

                var movements = transaction.Table<GameScoringPendingRunnerMovement>()
                    .Where(item => item.GameScoringPendingPlayId == play.Id).ToList();
                if (definition.RequiredPlayOuts > 3 - session.CurrentOuts
                    || retirements.Count != definition.RequiredPlayOuts
                    || retirements.Select(item => item.PlayerId).Distinct().Count()
                        != retirements.Count
                    || retirements.Any(item => item.PlayerId <= 0
                        || item.Outcome is null
                        || item.Outcome != RunnerOutcomeChoice.ForDestination(
                            RunnerDestination.Out))
                    || movements.Any(item => !item.RequiresOutcomeConfirmation
                        || item.OutOrder is not null))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                var selectedPlayerIds = retirements
                    .Select(item => item.PlayerId)
                    .ToHashSet();
                if (selectedPlayerIds.Any(playerId => movements.All(item =>
                        item.PlayerId != playerId)))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                var internalOrders = movements
                    .Where(item => selectedPlayerIds.Contains(item.PlayerId))
                    .OrderByDescending(item => item.StartingLocation)
                    .Select((item, index) => (item.PlayerId, Order: index + 1))
                    .ToDictionary(item => item.PlayerId, item => item.Order);
                foreach (var movement in movements)
                {
                    if (internalOrders.TryGetValue(movement.PlayerId, out var internalOrder))
                    {
                        movement.Destination = RunnerDestination.Out;
                        movement.SafeDestination = null;
                        movement.AttemptedDestination = null;
                        movement.OutOrder = internalOrder;
                    }
                    else
                    {
                        movement.OutOrder = null;
                    }
                    movement.RequiresOutcomeConfirmation = false;
                }

                ApplyMultiOutSurvivorDefaults(
                    play, movements, definition, session.CurrentOuts);

                if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                        play, movements, out var state)
                    || state is null
                    || !BaseRunnerResolver.IsLegalArrangement(state)
                    || !definition.IsCompatibleWithArrangement(state, session.CurrentOuts))
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }

                foreach (var movement in movements)
                    if (transaction.Update(movement) != 1)
                        throw new InvalidOperationException(
                            "Pending movement update did not affect one row.");

                if (definition.RequiredPlayOuts > 0
                    && movements.All(item => item.Destination == RunnerDestination.Out))
                {
                    var completed = CompletePendingPlay(transaction, gameId, play.Result);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
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
                if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                        play, movements, out var currentResolverState)
                    || currentResolverState is null)
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                var definition = PlateAppearanceResultDefinitions.Get(play.Result);
                if (definition.RequiredPlayOuts > 0 && movement.OutOrder is not null)
                {
                    response = PendingFailure(PendingPlayFailure.InvalidRequest);
                    return;
                }
                var usesGuidedResolution = definition.RequiresExplicitOutcomeConfirmation
                    && definition.RequiredPlayOuts == 0;
                if (outcome.IsOutAttemptingAdvance)
                {
                    var isAvailable = usesGuidedResolution
                        ? GetCompatibleGuidedAttemptedOutcomes(
                            play, movements, session.CurrentOuts, playerId)
                            .Contains(outcome)
                        : BaseRunnerResolver.TryGetOutAttemptingAdvance(
                            currentResolverState, playerId, out var availableOutcome)
                            && availableOutcome == outcome
                            && IsCompatibleWithDefinition(definition, session.CurrentOuts,
                                BaseRunnerResolver.ResolveCorrection(currentResolverState,
                                    playerId, RunnerDestination.Out));
                    var isWithinOutLimit = usesGuidedResolution
                        || IsWithinOutLimit(session.CurrentOuts,
                            BaseRunnerResolver.ResolveCorrection(currentResolverState,
                                playerId, RunnerDestination.Out));
                    if (!isAvailable || !isWithinOutLimit)
                    {
                        response = PendingFailure(PendingPlayFailure.InvalidRequest);
                        return;
                    }
                }
                if (usesGuidedResolution)
                {
                    if (play.Result == BatterResult.FielderChoice
                        && session.CurrentOuts == 2)
                    {
                        response = PendingFailure(PendingPlayFailure.InvalidRequest);
                        return;
                    }
                    var fixedOutcomes = movements
                        .Where(item => !item.RequiresOutcomeConfirmation
                            && item.PlayerId != playerId)
                        .ToDictionary(item => item.PlayerId, item => item.Destination);
                    fixedOutcomes[playerId] = outcome.Destination;
                    var arrangements = GetCompatibleGuidedArrangements(
                        play, movements, session.CurrentOuts, fixedOutcomes);
                    if (arrangements.Count == 0)
                    {
                        response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                        return;
                    }

                    var resolvedOutcomes = new Dictionary<int, RunnerDestination>(fixedOutcomes);
                    var establishesThirdOut = session.CurrentOuts
                        + fixedOutcomes.Count(item => item.Value == RunnerDestination.Out) == 3;
                    if (establishesThirdOut)
                    {
                        var thirdOutArrangement = arrangements
                            .OrderByDescending(arrangement => arrangement.Participants.Count(
                                participant => participant.Destination == RunnerDestination.Scores))
                            .First();
                        foreach (var participant in thirdOutArrangement.Participants)
                            resolvedOutcomes[participant.PlayerId] = participant.Destination;
                    }
                    else
                    {
                        foreach (var unresolved in movements.Where(item =>
                                     !resolvedOutcomes.ContainsKey(item.PlayerId)))
                        {
                            var outcomes = arrangements.Select(arrangement => arrangement.Participants
                                    .Single(participant => participant.PlayerId == unresolved.PlayerId)
                                    .Destination)
                                .Distinct()
                                .ToArray();
                            if (outcomes.Length == 1)
                                resolvedOutcomes[unresolved.PlayerId] = outcomes[0];
                        }
                    }

                    var representative = arrangements[0];
                    foreach (var item in movements)
                    {
                        item.Destination = resolvedOutcomes.TryGetValue(
                            item.PlayerId, out var resolvedDestination)
                            ? resolvedDestination
                            : representative.Participants.Single(participant =>
                                participant.PlayerId == item.PlayerId).Destination;
                        item.RequiresOutcomeConfirmation =
                            !resolvedOutcomes.ContainsKey(item.PlayerId);
                        ApplyOutcomeDetails(item, playerId, outcome);
                        if (transaction.Update(item) != 1)
                            throw new InvalidOperationException(
                                "Pending movement update did not affect one row.");
                    }
                    var completesHalfInning = movements.All(item =>
                            !item.RequiresOutcomeConfirmation)
                        && session.CurrentOuts + movements.Count(item =>
                            item.Destination == RunnerDestination.Out) == 3;
                    var canTerminalAutoComplete =
                        PendingFielderChoiceCompletion.CanAutoComplete(
                            play, movements, session.CurrentOuts);
                    if (canTerminalAutoComplete
                        || completesHalfInning && movements.All(item =>
                            item.Destination != RunnerDestination.Scores)
                            && play.Result == BatterResult.FielderChoice)
                    {
                        var completed = CompletePendingPlay(
                            transaction, gameId, BatterResult.FielderChoice);
                        if (!completed.IsSuccess)
                        {
                            response = PendingFailure(MapPendingFailure(completed.FailureReason));
                            return;
                        }
                        response = new(null, PendingPlayFailure.None);
                        return;
                    }
                    response = new(new(play, movements), PendingPlayFailure.None);
                    return;
                }
                var resolution = BaseRunnerResolver.ResolveCorrection(
                    currentResolverState, playerId, outcome.Destination);
                if (!resolution.IsSuccess
                    || !IsWithinOutLimit(session.CurrentOuts, resolution)
                    || !IsCompatibleWithDefinition(
                        definition, session.CurrentOuts, resolution))
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                var nextOutOrder = definition.RequiredPlayOuts > 0
                    && outcome.Destination == RunnerDestination.Out
                    && movement.OutOrder is null
                        ? movements.Where(item => item.OutOrder is not null)
                            .Max(item => item.OutOrder!.Value) + 1
                        : (int?)null;
                foreach (var item in movements)
                {
                    var resolvedDestination = resolution.Participants
                        .Single(participant => participant.PlayerId == item.PlayerId)
                        .Destination;
                    item.Destination = resolvedDestination;
                    ApplyOutcomeDetails(item, playerId, outcome);
                    if (item.PlayerId == playerId && nextOutOrder is not null)
                        item.OutOrder = nextOutOrder;
                    if (transaction.Update(item) != 1)
                        throw new InvalidOperationException(
                        "Pending movement update did not affect one row.");
                }
                if (definition.RequiredPlayOuts > 0
                    && movements.All(item => item.Destination == RunnerDestination.Out))
                {
                    var completed = CompletePendingPlay(
                        transaction, gameId, play.Result);
                    response = completed.IsSuccess
                        ? new(null, PendingPlayFailure.None)
                        : PendingFailure(MapPendingFailure(completed.FailureReason));
                    return;
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
        if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                play, movements, out var resolverState)
            || resolverState is null)
            return new([], PendingPlayFailure.InvalidScoringState);
        var definition = PlateAppearanceResultDefinitions.Get(play.Result);
        var selectedMovement = movements.Single(item => item.PlayerId == playerId);
        if (definition.RequiredPlayOuts > 0 && selectedMovement.OutOrder is not null)
            return new([], PendingPlayFailure.None);
        var usesGuidedResolution = definition.RequiresExplicitOutcomeConfirmation
            && definition.RequiredPlayOuts == 0;
        if (usesGuidedResolution)
        {
            if (play.Result == BatterResult.FielderChoice
                && session.CurrentOuts == 2)
                return new([], PendingPlayFailure.None);
            var fixedOutcomes = movements
                .Where(item => !item.RequiresOutcomeConfirmation
                    && item.PlayerId != playerId)
                .ToDictionary(item => item.PlayerId, item => item.Destination);
            var arrangements = GetCompatibleGuidedArrangements(
                play, movements, session.CurrentOuts, fixedOutcomes);
            var fielderChoiceDestinations = arrangements
                .Select(arrangement => arrangement.Participants.Single(item =>
                    item.PlayerId == playerId).Destination)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();
            var choices = fielderChoiceDestinations
                .Select(RunnerOutcomeChoice.ForDestination)
                .Concat(GetCompatibleGuidedAttemptedOutcomes(
                    play, movements, session.CurrentOuts, playerId))
                .ToArray();
            return new(choices,
                PendingPlayFailure.None);
        }
        var destinations = BaseRunnerResolver.GetAvailableDestinations(
                resolverState, playerId)
            .Where(destination => IsWithinOutLimit(session.CurrentOuts,
                BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, destination)))
            .Where(destination => IsCompatibleWithDefinition(
                definition, session.CurrentOuts,
                BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, destination)))
            .Select(RunnerOutcomeChoice.ForDestination)
            .ToList();
        var movement = movements.Single(item => item.PlayerId == playerId);
        if (!movement.RequiresOutcomeConfirmation
            && BaseRunnerResolver.TryGetOutAttemptingAdvance(
                resolverState, playerId, out var attemptedOutcome)
            && attemptedOutcome is not null
            && IsWithinOutLimit(session.CurrentOuts,
                BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, RunnerDestination.Out))
            && IsCompatibleWithDefinition(definition, session.CurrentOuts,
                BaseRunnerResolver.ResolveCorrection(
                    resolverState, playerId, RunnerDestination.Out)))
            destinations.Add(attemptedOutcome);
        return new(destinations.AsReadOnly(), PendingPlayFailure.None);
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
                if (movements.Any(item => item.RequiresOutcomeConfirmation)
                    || !IsValidPendingPlay(pending, movements, context.CurrentBatter!,
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
                var rulingRequirement = EvaluateThirdOutRunRuling(
                    pending, movements, session.CurrentOuts);
                var ids = outsAfterPlay == 3
                    && rulingRequirement
                        == ThirdOutRunRulingRequirement.ScorerDecisionRequired
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

    public async Task<CompletePlateAppearanceResult> CompleteFielderChoiceThirdOutAsync(
        int gameId, int outPlayerId)
    {
        if (gameId <= 0 || outPlayerId <= 0)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var result = CompleteFailure(CompletePlateAppearanceFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
                result = CompletePendingPlay(transaction, gameId,
                    BatterResult.FielderChoice, thirdOutPlayerId: outPlayerId));
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
        IReadOnlyDictionary<int, bool>? runRulings = null,
        int? thirdOutPlayerId = null)
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
        var isDedicatedFielderChoiceThirdOut = thirdOutPlayerId is not null
            && pending.Result == BatterResult.FielderChoice
            && session.CurrentOuts == 2
            && HasExpectedPendingParticipants(
                pending, movements, currentBatter, context.BaseRunners!);
        if (requiredResult is not null && pending.Result != requiredResult
            || pending.Result == BatterResult.Out && session.CurrentOuts >= 2
            || thirdOutPlayerId is not null && !isDedicatedFielderChoiceThirdOut
            || !isDedicatedFielderChoiceThirdOut
                && (movements.Any(item => item.RequiresOutcomeConfirmation)
                    || !IsValidPendingPlay(pending, movements, currentBatter,
                        context.BaseRunners!, session.CurrentOuts)))
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

        IReadOnlyList<GameScoringPendingRunnerMovement> committedMovements = movements;
        if (isDedicatedFielderChoiceThirdOut)
        {
            var selectedOut = movements.SingleOrDefault(item =>
                item.PlayerId == thirdOutPlayerId);
            if (selectedOut is null)
                return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);
            selectedOut.Destination = RunnerDestination.Out;
            selectedOut.SafeDestination = null;
            selectedOut.AttemptedDestination = null;
            selectedOut.RequiresOutcomeConfirmation = false;
            if (transaction.Update(selectedOut) != 1)
                throw new InvalidOperationException(
                    "Pending movement update did not affect one row.");
            committedMovements = [selectedOut];
        }

        var outsAdded = CountOutsAdded(pending, committedMovements);
        var outsAfterPlay = session.CurrentOuts + outsAdded;
        var runRulingRequirement = EvaluateThirdOutRunRuling(
            pending, committedMovements, session.CurrentOuts);
        if (outsAfterPlay > 3
            || !TryResolveRunRulings(committedMovements, outsAfterPlay,
                runRulingRequirement, runRulings, out var resolvedRunRulings))
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

    private static ThirdOutRunRulingRequirement EvaluateThirdOutRunRuling(
        GameScoringPendingPlay pending,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int currentOuts)
    {
        var definition = PlateAppearanceResultDefinitions.Get(pending.Result);
        var outcomes = movements.Select(item => new ResolvedPlayParticipantOutcome(
            item.PlayerId,
            item.StartingLocation,
            item.Destination,
            item.SafeDestination,
            item.AttemptedDestination,
            item.OutOrder,
            definition.RequiredPlayOuts > 0
                && item.OutOrder is not null
                && item.OutOrder <= definition.RequiredPlayOuts)).ToList();
        if (!definition.IncludesBatterMovement
            && outcomes.All(item => item.PlayerId != pending.BatterPlayerId))
        {
            outcomes.Add(new(
                pending.BatterPlayerId,
                RunnerStartingLocation.Home,
                RunnerDestination.Out,
                null,
                null,
                null));
        }

        return ThirdOutRunRulingEvaluator.Evaluate(
            currentOuts,
            pending.BatterPlayerId,
            outcomes,
            pending.Result);
    }

    private static CompletePlateAppearanceResult CompleteInningEndingMultiOut(
        SQLite.SQLiteConnection transaction,
        CompletionContext context,
        BatterResult result,
        int outsAdded)
    {
        var session = context.Session!;
        var currentBatter = context.CurrentBatter!;
        if (session.CurrentOuts + outsAdded != 3
            || ThirdOutRunRulingEvaluator.Evaluate(
                session.CurrentOuts, currentBatter.PlayerId, [], result)
                != ThirdOutRunRulingRequirement.AutomaticNoRun)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidScoringState);

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

        return new(appearance, session.CurrentBatterPosition, 0,
            CompletePlateAppearanceFailure.None);
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

    private static bool IsCompatibleWithDefinition(
        PlateAppearanceResultDefinition definition,
        int currentOuts,
        BaseRunnerResolutionResult resolution) =>
        resolution.IsSuccess
        && IsCompatibleResolvedMultiOutArrangement(
            definition,
            new BaseRunnerResolutionState(resolution.Participants),
            currentOuts);

    private static bool IsCompatibleResolvedMultiOutArrangement(
        PlateAppearanceResultDefinition definition,
        BaseRunnerResolutionState arrangement,
        int currentOuts)
    {
        if (definition.RequiredPlayOuts == 0)
            return definition.IsCompatibleWithArrangement(arrangement, currentOuts);

        var batter = arrangement.Participants.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        var outCount = arrangement.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        return arrangement.Participants.Count >= definition.MinimumParticipants
            && outCount >= definition.RequiredPlayOuts
            && outCount <= 3 - currentOuts
            && (definition.AllowedBatterDestinations is null
                || definition.AllowedBatterDestinations.Contains(batter.Destination));
    }

    private static IReadOnlyList<BaseRunnerResolutionState> GetCompatibleGuidedArrangements(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int currentOuts,
        IReadOnlyDictionary<int, RunnerDestination> fixedOutcomes)
    {
        if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                play, movements, out var seedState)
            || seedState is null
            || !PlateAppearanceResultDefinitions.TryGet(play.Result, out var definition)
            || definition is null)
            return [];

        return BaseRunnerResolver.GetLegalArrangements(seedState)
            .Where(arrangement => definition.IsCompatibleWithArrangement(
                arrangement, currentOuts))
            .Where(arrangement => currentOuts + arrangement.Participants.Count(item =>
                item.Destination == RunnerDestination.Out) <= 3)
            .Where(arrangement => fixedOutcomes.All(outcome =>
                arrangement.Participants.Single(item =>
                    item.PlayerId == outcome.Key).Destination == outcome.Value))
            .ToArray();
    }

    private static IReadOnlyList<RunnerOutcomeChoice> GetCompatibleGuidedAttemptedOutcomes(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int currentOuts,
        int playerId)
    {
        var movement = movements.SingleOrDefault(item => item.PlayerId == playerId);
        if (movement is null || movement.RequiresOutcomeConfirmation) return [];
        var attemptedDestination = RunnerOutcomeChoice.GetNextDestination(
            movement.Destination);
        var candidate = new RunnerOutcomeChoice(
            RunnerDestination.Out, movement.Destination, attemptedDestination);
        if (!candidate.IsValidFor(movement.StartingLocation)) return [];

        var fixedOthers = movements
            .Where(item => !item.RequiresOutcomeConfirmation && item.PlayerId != playerId)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        var fixedOut = new Dictionary<int, RunnerDestination>(fixedOthers)
        {
            [playerId] = RunnerDestination.Out,
        };
        if (GetCompatibleGuidedArrangements(
                play, movements, currentOuts, fixedOut).Count == 0)
            return [];
        return [candidate];
    }

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
            RequiresOutcomeConfirmation =
                definition.RequiresExplicitOutcomeConfirmation,
        }).ToList();
        if (definition.IncludesBatterMovement)
        {
            movements.Add(new()
            {
                GameScoringPendingPlayId = pendingPlayId,
                PlayerId = batterPlayerId,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = definition.BatterDefaultDestination,
                RequiresOutcomeConfirmation =
                    definition.RequiresExplicitOutcomeConfirmation,
            });
        }
        return movements;
    }

    private static void ApplyMultiOutSurvivorDefaults(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        PlateAppearanceResultDefinition definition,
        int currentOuts)
    {
        if (definition.RequiredPlayOuts == 0) return;

        var occupiedStarts = movements
            .Where(item => item.StartingLocation != RunnerStartingLocation.Home)
            .Select(item => item.StartingLocation)
            .ToHashSet();
        var firstIsForced = occupiedStarts.Contains(RunnerStartingLocation.First);
        var secondIsForced = firstIsForced
            && occupiedStarts.Contains(RunnerStartingLocation.Second);
        var thirdIsForced = secondIsForced
            && occupiedStarts.Contains(RunnerStartingLocation.Third);

        foreach (var survivor in movements.Where(item =>
                     item.Destination != RunnerDestination.Out
                     && item.StartingLocation != RunnerStartingLocation.Home))
        {
            var isForced = survivor.StartingLocation switch
            {
                RunnerStartingLocation.First => firstIsForced,
                RunnerStartingLocation.Second => secondIsForced,
                RunnerStartingLocation.Third => thirdIsForced,
                _ => false,
            };
            if (!isForced) continue;
            survivor.Destination = survivor.StartingLocation == RunnerStartingLocation.Third
                ? RunnerDestination.Scores
                : (RunnerDestination)((int)survivor.StartingLocation + 1);
            survivor.SafeDestination = null;
            survivor.AttemptedDestination = null;
        }

        if (PendingBaseRunnerResolutionAdapter.TryCreateState(
                play, movements, out var proposedState)
            && proposedState is not null
            && BaseRunnerResolver.IsLegalArrangement(proposedState)
            && definition.IsCompatibleWithArrangement(proposedState, currentOuts))
            return;

        if (proposedState is null)
            throw new InvalidOperationException(
                "Multi-out survivor defaults could not create a resolver state.");

        var selectedOutPlayerIds = movements
            .Where(item => item.OutOrder is not null)
            .Select(item => item.PlayerId)
            .ToHashSet();
        var fallback = BaseRunnerResolver.GetLegalArrangements(proposedState)
            .Where(arrangement => definition.IsCompatibleWithArrangement(
                arrangement, currentOuts))
            .Where(arrangement => selectedOutPlayerIds.All(playerId =>
                arrangement.Participants.Single(item => item.PlayerId == playerId)
                    .Destination == RunnerDestination.Out))
            .OrderBy(arrangement => arrangement.Participants.Count(participant =>
                participant.Destination != proposedState.Participants.Single(item =>
                    item.PlayerId == participant.PlayerId).Destination))
            .FirstOrDefault();
        if (fallback is null)
            throw new InvalidOperationException(
                "Multi-out survivor defaults have no legal resolver arrangement.");

        foreach (var survivor in movements.Where(item => item.OutOrder is null))
        {
            survivor.Destination = fallback.Participants.Single(item =>
                item.PlayerId == survivor.PlayerId).Destination;
            survivor.SafeDestination = null;
            survivor.AttemptedDestination = null;
        }
    }

    private static bool HasExpectedPendingParticipants(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        GameScoringLineupPlayer currentBatter,
        IReadOnlyList<GameScoringBaseRunner> bases)
    {
        if (play.BatterPlayerId != currentBatter.PlayerId
            || play.BatterPosition != currentBatter.BattingPosition
            || movements.Count != bases.Count + 1
            || movements.Select(item => item.PlayerId).Distinct().Count() != movements.Count)
            return false;

        var expectedStarts = bases.Select(item =>
            (item.PlayerId, (RunnerStartingLocation)item.Base)).ToHashSet();
        expectedStarts.Add((currentBatter.PlayerId, RunnerStartingLocation.Home));
        return movements.Select(item => (item.PlayerId, item.StartingLocation))
            .ToHashSet().SetEquals(expectedStarts);
    }

    private static bool IsValidPendingPlay(GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        GameScoringLineupPlayer currentBatter,
        IReadOnlyList<GameScoringBaseRunner> bases,
        int currentOuts)
    {
        if (!PlateAppearanceResultDefinitions.TryGet(play.Result, out var definition)
            || definition is null) return false;
        var includesBatterMovement = definition.IncludesBatterMovement;
        if (definition.RequiresExistingRunner && bases.Count == 0
            || play.BatterPlayerId != currentBatter.PlayerId
            || play.BatterPosition != currentBatter.BattingPosition
            || movements.Count != bases.Count + (includesBatterMovement ? 1 : 0)
            || movements.Select(item => item.PlayerId).Distinct().Count() != movements.Count
            || movements.Any(item => !Enum.IsDefined(item.StartingLocation)
                || !Enum.IsDefined(item.Destination)
                || !new RunnerOutcomeChoice(item.Destination,
                    item.SafeDestination, item.AttemptedDestination)
                    .IsValidFor(item.StartingLocation)
                || item.RequiresOutcomeConfirmation
                    && (item.SafeDestination is not null
                        || item.AttemptedDestination is not null))) return false;
        var expectedStarts = bases.Select(item =>
            (item.PlayerId, (RunnerStartingLocation)item.Base)).ToHashSet();
        if (includesBatterMovement)
            expectedStarts.Add((currentBatter.PlayerId, RunnerStartingLocation.Home));
        if (!movements.Select(item => (item.PlayerId, item.StartingLocation))
            .ToHashSet().SetEquals(expectedStarts)) return false;
        if (definition.RequiresExplicitOutcomeConfirmation)
        {
            if (!PendingBaseRunnerResolutionAdapter.TryCreateState(
                    play, movements, out var resolvedState)
                || resolvedState is null
                || !IsCompatibleResolvedMultiOutArrangement(
                    definition, resolvedState, currentOuts))
                return false;
            var participant = resolvedState.Participants[0];
            if (!BaseRunnerResolver.ResolveCorrection(resolvedState,
                    participant.PlayerId, participant.Destination).IsSuccess)
                return false;
        }
        if (definition.RequiredPlayOuts > 0)
        {
            var orderedOuts = movements
                .Where(item => item.OutOrder is not null)
                .OrderBy(item => item.OutOrder)
                .ToArray();
            var totalOuts = movements.Count(item =>
                item.Destination == RunnerDestination.Out);
            if (definition.RequiredPlayOuts > 3 - currentOuts
                || orderedOuts.Length < definition.RequiredPlayOuts
                || totalOuts != orderedOuts.Length
                || totalOuts > 3 - currentOuts
                || !orderedOuts.Select(item => item.OutOrder)
                    .SequenceEqual(Enumerable.Range(1, orderedOuts.Length)
                        .Select(value => (int?)value))
                || movements.Any(item => item.Destination != RunnerDestination.Out
                    && item.OutOrder is not null))
                return false;
        }
        else if (movements.Any(item => item.OutOrder is not null)) return false;

        return movements.Where(item => item.Destination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third)
            .Select(item => item.Destination).Distinct().Count()
            == movements.Count(item => item.Destination is RunnerDestination.First
                or RunnerDestination.Second or RunnerDestination.Third);
    }

    private static void ApplyOutcomeDetails(
        GameScoringPendingRunnerMovement movement,
        int selectedPlayerId,
        RunnerOutcomeChoice selectedOutcome)
    {
        if (movement.PlayerId == selectedPlayerId)
        {
            movement.SafeDestination = selectedOutcome.SafeDestination;
            movement.AttemptedDestination = selectedOutcome.AttemptedDestination;
            return;
        }

        if (movement.Destination != RunnerDestination.Out)
        {
            movement.SafeDestination = null;
            movement.AttemptedDestination = null;
        }
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
