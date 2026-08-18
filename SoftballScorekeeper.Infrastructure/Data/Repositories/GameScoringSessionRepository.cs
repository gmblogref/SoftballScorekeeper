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
        if (sequence.Count == 0)
            return new(session, sequence, new Dictionary<int, Player>(), plateAppearances,
                baseRunners, runnerMovements, pendingPlay);

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
            pendingPlay);
    }

    public async Task<PendingPlayResult> SelectBatterResultAsync(int gameId, BatterResult result)
    {
        if (gameId <= 0 || !Enum.IsDefined(result))
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
                if (play.BatterPlayerId <= 0
                    || !PendingRunnerCorrection.TryResolve(movements,
                        play.BatterPlayerId, playerId, destination, out var resolved))
                {
                    response = PendingFailure(PendingPlayFailure.DuplicateDestination);
                    return;
                }
                foreach (var item in movements)
                {
                    item.Destination = resolved[item.PlayerId];
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
        var destinations = Enum.GetValues<RunnerDestination>()
            .Where(destination => PendingRunnerCorrection.TryResolve(
                movements, play.BatterPlayerId, playerId, destination, out _))
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

    public async Task<CompletePlateAppearanceResult> CompletePlateAppearanceAsync(int gameId)
    {
        if (gameId <= 0)
            return CompleteFailure(CompletePlateAppearanceFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var result = CompleteFailure(CompletePlateAppearanceFailure.SessionNotFound);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
                result = CompletePendingPlay(transaction, gameId));
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

    public async Task<InitializeScoringSessionResult> InitializeAsync(
        int teamId,
        int gameId,
        TeamGameSide teamSide)
    {
        if (teamId <= 0 || gameId <= 0 || !Enum.IsDefined(teamSide))
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

    private static void InsertPendingPlay(SQLite.SQLiteConnection transaction,
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
        foreach (var movement in CreateDefaultMovements(play.Id,
            context.CurrentBatter.PlayerId, context.BaseRunners!, result))
            if (transaction.Insert(movement) != 1)
                throw new InvalidOperationException(
                    "Pending movement insertion did not affect one row.");
    }

    private static CompletePlateAppearanceResult CompletePendingPlay(
        SQLite.SQLiteConnection transaction, int gameId, BatterResult? requiredResult = null)
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
            || !IsValidPendingPlay(pending, movements, currentBatter,
                context.BaseRunners!))
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
            }) != 1)
                throw new InvalidOperationException(
                    "Runner-movement insertion did not affect one row.");
        }

        transaction.Execute(
            "DELETE FROM GameScoringBaseRunner WHERE GameScoringSessionId = ?", session.Id);
        foreach (var movement in movements.Where(item =>
            item.Destination != RunnerDestination.Scores))
        {
            if (transaction.Insert(new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id,
                PlayerId = movement.PlayerId,
                Base = (ScoringBase)movement.Destination,
            }) != 1)
                throw new InvalidOperationException("Base-runner insertion did not affect one row.");
        }

        var runs = movements.Count(item => item.Destination == RunnerDestination.Scores);
        if (session.TeamSide == TeamGameSide.Home) session.HomeScore += runs;
        else if (session.TeamSide == TeamGameSide.Away) session.VisitorScore += runs;
        else throw new InvalidOperationException("Scoring session has an invalid Team side.");
        var nextIndex = (context.CurrentIndex + 1) % sequence.Count;
        session.CurrentBatterPosition = sequence[nextIndex].BattingPosition;
        if (transaction.Update(session) != 1)
            throw new InvalidOperationException(
                "Current-batter advancement did not affect one row.");
        ClearPending(transaction, session.Id);
        return new(appearance, session.CurrentBatterPosition, runs,
            CompletePlateAppearanceFailure.None);
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
        if (!Enum.IsDefined(play.Result) || play.BatterPlayerId != currentBatter.PlayerId
            || play.BatterPosition != currentBatter.BattingPosition
            || movements.Count != bases.Count + 1
            || movements.Select(item => item.PlayerId).Distinct().Count() != movements.Count
            || movements.Any(item => !Enum.IsDefined(item.StartingLocation)
                || !Enum.IsDefined(item.Destination))) return false;
        var expectedStarts = bases.Select(item => (item.PlayerId, (RunnerStartingLocation)item.Base))
            .Append((currentBatter.PlayerId, RunnerStartingLocation.Home)).ToHashSet();
        if (!movements.Select(item => (item.PlayerId, item.StartingLocation))
            .ToHashSet().SetEquals(expectedStarts)) return false;
        return movements.Where(item => item.Destination != RunnerDestination.Scores)
            .Select(item => item.Destination).Distinct().Count()
            == movements.Count(item => item.Destination != RunnerDestination.Scores);
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
