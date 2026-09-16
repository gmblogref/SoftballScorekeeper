using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameLineupRepository
{
    private readonly AppDatabase database;

    public GameLineupRepository(AppDatabase database) => this.database = database;

    public async Task<List<Player>> GetEligiblePlayersAsync(int teamId, int gameId)
    {
        if (teamId <= 0 || gameId <= 0) return [];
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.QueryAsync<Player>(
            """
            SELECT Player.* FROM Player
            INNER JOIN TeamPlayer ON TeamPlayer.PlayerId = Player.Id
            INNER JOIN Game ON Game.TeamId = TeamPlayer.TeamId
            WHERE Game.Id = ? AND Game.TeamId = ? AND Player.IsActive = 1 AND TeamPlayer.IsActive = 1
            ORDER BY Player.LastName COLLATE NOCASE, Player.FirstName COLLATE NOCASE, Player.Id
            """, gameId, teamId);
    }

    public async Task<List<GameLineupPlayer>> GetForGameAsync(int gameId)
    {
        if (gameId <= 0) return [];
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.Table<GameLineupPlayer>()
            .Where(item => item.GameId == gameId)
            .OrderBy(item => item.BattingOrder).ToListAsync();
    }

    public async Task<List<Player>> GetPlayersForGameAsync(int gameId)
    {
        if (gameId <= 0) return [];
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.QueryAsync<Player>(
            """
            SELECT Player.* FROM Player
            INNER JOIN GameLineupPlayer ON GameLineupPlayer.PlayerId = Player.Id
            WHERE GameLineupPlayer.GameId = ?
            ORDER BY GameLineupPlayer.BattingOrder, GameLineupPlayer.Id
            """, gameId);
    }

    public async Task<bool> HasLineupAsync(int gameId) => (await GetForGameAsync(gameId)).Count > 0;

    public async Task<IReadOnlySet<int>> GetGameIdsWithLineupsAsync(IEnumerable<int>? gameIds)
    {
        var requestedIds = (gameIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (requestedIds.Length == 0) return new HashSet<int>();

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var placeholders = string.Join(",", requestedIds.Select(_ => "?"));
        var rows = await connection.QueryAsync<GameLineupPlayer>(
            $"SELECT DISTINCT GameId FROM GameLineupPlayer WHERE GameId IN ({placeholders})",
            requestedIds.Cast<object>().ToArray());
        return rows.Select(item => item.GameId).ToHashSet();
    }

    public async Task<SaveGameLineupResult> SaveInitialAsync(int teamId, int gameId, IEnumerable<int>? playerIds)
        => await SaveAsync(teamId, gameId, playerIds, allowExistingLineup: false);

    public async Task<SaveGameLineupResult> AppendAsync(int teamId, int gameId, IEnumerable<int>? playerIds)
        => await SaveAsync(teamId, gameId, playerIds, allowExistingLineup: true);

    public async Task<RemoveGameLineupPlayersResult> RemovePlayersAsync(
        int gameId,
        IReadOnlyCollection<int>? playerIds)
    {
        var requestedIds = playerIds?.Distinct().ToArray() ?? Array.Empty<int>();
        var ignoredIds = requestedIds.Where(id => id <= 0).ToArray();
        var positiveIds = requestedIds.Where(id => id > 0).ToArray();
        if (gameId <= 0 || positiveIds.Length == 0)
            return new(gameId, requestedIds, [], positiveIds, ignoredIds, false);

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = new RemoveGameLineupPlayersResult(
            gameId, requestedIds, [], positiveIds, ignoredIds, false);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                if (transaction.Find<Game>(gameId) is null) return;
                if (TeamYearWriteGuard.IsGameFinalized(transaction, gameId))
                {
                    result = new(gameId, requestedIds, [], positiveIds, ignoredIds, true);
                    return;
                }
                if (HasScoringSession(transaction, gameId))
                {
                    result = new(gameId, requestedIds, [], positiveIds, ignoredIds, true);
                    return;
                }

                var lineup = transaction.Table<GameLineupPlayer>()
                    .Where(item => item.GameId == gameId)
                    .OrderBy(item => item.BattingOrder)
                    .ThenBy(item => item.Id)
                    .ToList();
                var requestedSet = positiveIds.ToHashSet();
                var removedIds = lineup
                    .Where(item => requestedSet.Contains(item.PlayerId))
                    .Select(item => item.PlayerId)
                    .ToArray();
                var removedSet = removedIds.ToHashSet();
                var missingIds = positiveIds.Where(id => !removedSet.Contains(id)).ToArray();

                foreach (var item in lineup.Where(item => removedSet.Contains(item.PlayerId)))
                    transaction.Delete(item);

                var survivors = lineup.Where(item => !removedSet.Contains(item.PlayerId)).ToArray();
                for (var index = 0; index < survivors.Length; index++)
                {
                    survivors[index].BattingOrder = index + 1;
                    transaction.Update(survivors[index]);
                }

                result = new(gameId, requestedIds, removedIds, missingIds, ignoredIds, true);
            });
        }
        catch
        {
            return new(gameId, requestedIds, [], positiveIds, ignoredIds, false);
        }

        return result;
    }

    public async Task<ReorderGameLineupResult> ReorderAsync(
        int gameId,
        IReadOnlyCollection<int>? orderedPlayerIds)
    {
        if (gameId <= 0 || orderedPlayerIds is null)
            return ReorderFailure(gameId, ReorderGameLineupFailure.InvalidRequest);

        var requestedIds = orderedPlayerIds.ToArray();
        if (requestedIds.Any(id => id <= 0)
            || requestedIds.Distinct().Count() != requestedIds.Length)
            return ReorderFailure(gameId, ReorderGameLineupFailure.InvalidRequest);

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = ReorderFailure(gameId, ReorderGameLineupFailure.InvalidGame);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                if (transaction.Find<Game>(gameId) is null
                    || TeamYearWriteGuard.IsGameFinalized(transaction, gameId))
                    return;
                if (HasScoringSession(transaction, gameId))
                {
                    result = ReorderFailure(gameId, ReorderGameLineupFailure.ScoringStarted);
                    return;
                }

                var lineup = transaction.Table<GameLineupPlayer>()
                    .Where(item => item.GameId == gameId)
                    .OrderBy(item => item.BattingOrder)
                    .ThenBy(item => item.Id)
                    .ToList();
                var persistedIds = lineup.Select(item => item.PlayerId).ToHashSet();
                if (requestedIds.Length != lineup.Count
                    || requestedIds.Any(id => !persistedIds.Contains(id)))
                {
                    result = ReorderFailure(gameId, ReorderGameLineupFailure.LineupMismatch);
                    return;
                }

                var rowsByPlayerId = lineup.ToDictionary(item => item.PlayerId);
                for (var index = 0; index < requestedIds.Length; index++)
                {
                    var row = rowsByPlayerId[requestedIds[index]];
                    row.BattingOrder = index + 1;
                    if (transaction.Update(row) != 1)
                        throw new InvalidOperationException("Lineup reorder did not update one row.");
                }

                result = new(gameId, requestedIds, ReorderGameLineupFailure.None);
            });
        }
        catch
        {
            return ReorderFailure(gameId, ReorderGameLineupFailure.Persistence);
        }

        return result;
    }

    public async Task<UsePreviousLineupResult> UsePreviousAsync(int teamId, int targetGameId)
    {
        if (teamId <= 0 || targetGameId <= 0)
            return PreviousFailure(targetGameId, UsePreviousLineupFailure.InvalidContext);

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = PreviousFailure(targetGameId, UsePreviousLineupFailure.InvalidContext);
        await connection.RunInTransactionAsync(transaction =>
        {
            result = ResolvePreviousLineup(transaction, teamId, targetGameId);
            if (!result.IsSuccess) return;

            foreach (var existing in transaction.Table<GameLineupPlayer>()
                .Where(item => item.GameId == targetGameId)
                .ToList())
            {
                transaction.Delete(existing);
            }

            for (var index = 0; index < result.CopiedPlayerIds.Count; index++)
            {
                transaction.Insert(new GameLineupPlayer
                {
                    GameId = targetGameId,
                    PlayerId = result.CopiedPlayerIds[index],
                    BattingOrder = index + 1,
                });
            }
        });

        return result;
    }

    public async Task<bool> CanUsePreviousAsync(int teamId, int targetGameId)
    {
        if (teamId <= 0 || targetGameId <= 0) return false;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var canUsePrevious = false;
        await connection.RunInTransactionAsync(transaction =>
            canUsePrevious = ResolvePreviousLineup(transaction, teamId, targetGameId).IsSuccess);
        return canUsePrevious;
    }

    private async Task<SaveGameLineupResult> SaveAsync(
        int teamId,
        int gameId,
        IEnumerable<int>? playerIds,
        bool allowExistingLineup)
    {
        var orderedIds = (playerIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (orderedIds.Length == 0) return Failure(gameId, SaveGameLineupFailure.EmptySelection);
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = Failure(gameId, SaveGameLineupFailure.InvalidGame);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var game = transaction.Find<Game>(gameId);
                if (game is null) return;
                if (game.TeamId != teamId)
                {
                    result = Failure(gameId, SaveGameLineupFailure.TeamMismatch);
                    return;
                }
                if (!TeamYearWriteGuard.IsOpen(transaction, teamId))
                    return;
                if (HasScoringSession(transaction, gameId))
                {
                    result = Failure(gameId, SaveGameLineupFailure.ScoringStarted);
                    return;
                }
                var existingLineup = transaction.Table<GameLineupPlayer>()
                    .Where(item => item.GameId == gameId).ToList();
                if (!allowExistingLineup && existingLineup.Count > 0)
                {
                    result = Failure(gameId, SaveGameLineupFailure.LineupAlreadyExists);
                    return;
                }
                var eligible = (from player in transaction.Table<Player>().ToList()
                                join membership in transaction.Table<TeamPlayer>().ToList() on player.Id equals membership.PlayerId
                                where player.IsActive && membership.IsActive && membership.TeamId == game.TeamId
                                select player.Id).ToHashSet();
                var ineligible = orderedIds.Where(id => !eligible.Contains(id)).ToArray();
                if (ineligible.Length > 0)
                {
                    result = new(gameId, [], ineligible, SaveGameLineupFailure.IneligiblePlayers);
                    return;
                }
                var existingPlayerIds = existingLineup.Select(item => item.PlayerId).ToHashSet();
                var newPlayerIds = orderedIds.Where(id => !existingPlayerIds.Contains(id)).ToArray();
                var nextBattingOrder = existingLineup.Count == 0 ? 1 : existingLineup.Max(item => item.BattingOrder) + 1;
                for (var index = 0; index < newPlayerIds.Length; index++)
                    transaction.Insert(new GameLineupPlayer { GameId = gameId, PlayerId = newPlayerIds[index], BattingOrder = nextBattingOrder + index });
                result = new(gameId, newPlayerIds, [], SaveGameLineupFailure.None);
            });
        }
        catch { return Failure(gameId, SaveGameLineupFailure.Persistence); }
        return result;
    }

    private static SaveGameLineupResult Failure(int gameId, SaveGameLineupFailure failure) => new(gameId, [], [], failure);

    private static ReorderGameLineupResult ReorderFailure(
        int gameId,
        ReorderGameLineupFailure failure) => new(gameId, [], failure);

    private static UsePreviousLineupResult PreviousFailure(
        int targetGameId,
        UsePreviousLineupFailure failure) => new(targetGameId, null, [], [], failure);

    private static UsePreviousLineupResult ResolvePreviousLineup(
        SQLite.SQLiteConnection transaction,
        int teamId,
        int targetGameId)
    {
        if (teamId <= 0 || targetGameId <= 0)
            return PreviousFailure(targetGameId, UsePreviousLineupFailure.InvalidContext);

        var team = transaction.Find<Team>(teamId);
        var targetGame = transaction.Find<Game>(targetGameId);
        if (team is null || !team.IsActive || team.IsYearOver
            || targetGame is null || targetGame.TeamId != teamId)
            return PreviousFailure(targetGameId, UsePreviousLineupFailure.InvalidContext);
        if (HasScoringSession(transaction, targetGameId))
            return PreviousFailure(targetGameId, UsePreviousLineupFailure.ScoringStarted);

        var priorGames = transaction.Table<Game>()
            .Where(game => game.TeamId == teamId)
            .ToList()
            .Where(game => IsBefore(game, targetGame))
            .OrderByDescending(game => game.ScheduledDate)
            .ThenByDescending(game => game.ScheduledTime)
            .ThenByDescending(game => game.Id)
            .ToArray();
        var priorGameIds = priorGames.Select(game => game.Id).ToHashSet();
        var allPriorRows = transaction.Table<GameLineupPlayer>()
            .ToList()
            .Where(item => priorGameIds.Contains(item.GameId))
            .ToArray();
        var gamesWithLineups = allPriorRows.Select(item => item.GameId).ToHashSet();
        var sourceGame = priorGames.FirstOrDefault(game => gamesWithLineups.Contains(game.Id));
        if (sourceGame is null)
            return PreviousFailure(targetGameId, UsePreviousLineupFailure.NoPreviousLineup);

        var sourceRows = allPriorRows
            .Where(item => item.GameId == sourceGame.Id)
            .OrderBy(item => item.BattingOrder)
            .ThenBy(item => item.Id)
            .ToArray();
        var activePlayerIds = transaction.Table<Player>()
            .Where(player => player.IsActive)
            .ToList()
            .Select(player => player.Id)
            .ToHashSet();
        var activeMemberIds = transaction.Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId && item.IsActive)
            .ToList()
            .Select(item => item.PlayerId)
            .ToHashSet();
        var copiedIds = sourceRows
            .Where(item => activePlayerIds.Contains(item.PlayerId)
                && activeMemberIds.Contains(item.PlayerId))
            .Select(item => item.PlayerId)
            .ToArray();
        var copiedSet = copiedIds.ToHashSet();
        var skippedIds = sourceRows
            .Select(item => item.PlayerId)
            .Where(id => !copiedSet.Contains(id))
            .ToArray();
        return copiedIds.Length == 0
            ? new(targetGameId, sourceGame.Id, [], skippedIds,
                UsePreviousLineupFailure.NoEligiblePlayers)
            : new(targetGameId, sourceGame.Id, copiedIds, skippedIds,
                UsePreviousLineupFailure.None);
    }

    private static bool IsBefore(Game candidate, Game target) =>
        candidate.ScheduledDate < target.ScheduledDate
        || candidate.ScheduledDate == target.ScheduledDate
            && (candidate.ScheduledTime < target.ScheduledTime
                || candidate.ScheduledTime == target.ScheduledTime && candidate.Id < target.Id);

    private static bool HasScoringSession(SQLite.SQLiteConnection transaction, int gameId) =>
        transaction.Table<GameScoringSession>().Any(session => session.GameId == gameId);
}
