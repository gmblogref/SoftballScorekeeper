using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameLineupRepository
{
    private readonly AppDatabase database;

    public GameLineupRepository(AppDatabase database) => this.database = database;

    public async Task<List<Player>> GetEligiblePlayersAsync(int teamId, int gameId)
    {
        if (teamId <= 0 || gameId <= 0) return [];
        var connection = await database.GetConnectionAsync();
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
        var connection = await database.GetConnectionAsync();
        return await connection.Table<GameLineupPlayer>()
            .Where(item => item.GameId == gameId)
            .OrderBy(item => item.BattingOrder).ToListAsync();
    }

    public async Task<List<Player>> GetPlayersForGameAsync(int gameId)
    {
        if (gameId <= 0) return [];
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<Player>(
            """
            SELECT Player.* FROM Player
            INNER JOIN GameLineupPlayer ON GameLineupPlayer.PlayerId = Player.Id
            WHERE GameLineupPlayer.GameId = ?
            ORDER BY GameLineupPlayer.BattingOrder, GameLineupPlayer.Id
            """, gameId);
    }

    public async Task<bool> HasLineupAsync(int gameId) => (await GetForGameAsync(gameId)).Count > 0;

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

        var connection = await database.GetConnectionAsync();
        var result = new RemoveGameLineupPlayersResult(
            gameId, requestedIds, [], positiveIds, ignoredIds, false);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                if (transaction.Find<Game>(gameId) is null) return;

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

    private async Task<SaveGameLineupResult> SaveAsync(
        int teamId,
        int gameId,
        IEnumerable<int>? playerIds,
        bool allowExistingLineup)
    {
        var orderedIds = (playerIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (orderedIds.Length == 0) return Failure(gameId, SaveGameLineupFailure.EmptySelection);
        var connection = await database.GetConnectionAsync();
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
}
