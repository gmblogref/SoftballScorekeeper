using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class PlayerRepository
{
    private readonly AppDatabase database;

    public PlayerRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
    }

    public async Task<int> CreateAsync(Player player)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.InsertAsync(player);
    }

    public Task<CreatePlayerResult> CreateForTeamAsync(Player player, int teamId) =>
        CreateForContextAsync(player, teamId, null);

    public Task<CreatePlayerResult> CreateForGameLineupAsync(
        Player player,
        int teamId,
        int gameId
    ) => CreateForContextAsync(player, teamId, gameId);

    private async Task<CreatePlayerResult> CreateForContextAsync(
        Player player,
        int teamId,
        int? gameId
    )
    {
        if (teamId <= 0)
            return new(0, CreatePlayerFailure.InvalidTeam);
        if (gameId is <= 0)
            return new(0, CreatePlayerFailure.InvalidGame);

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var result = new CreatePlayerResult(0, CreatePlayerFailure.Persistence);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var team = transaction.Find<Team>(teamId);
                if (team is null || !team.IsActive || team.IsYearOver)
                {
                    result = new(0, CreatePlayerFailure.InvalidTeam);
                    return;
                }

                Game? game = null;
                if (gameId.HasValue)
                {
                    game = transaction.Find<Game>(gameId.Value);
                    if (game is null)
                    {
                        result = new(0, CreatePlayerFailure.InvalidGame);
                        return;
                    }

                    if (game.TeamId != teamId)
                    {
                        result = new(0, CreatePlayerFailure.TeamMismatch);
                        return;
                    }

                    var scoringSession = transaction
                        .Table<GameScoringSession>()
                        .FirstOrDefault(session => session.GameId == game.Id);
                    if (scoringSession?.Status == GameScoringStatus.Completed)
                    {
                        result = new(0, CreatePlayerFailure.ScoringStarted);
                        return;
                    }
                }

                player.Id = 0;
                player.IsActive = true;
                if (transaction.Insert(player) != 1)
                    return;

                if (
                    transaction.Insert(
                        new TeamPlayer
                    {
                        TeamId = teamId,
                        PlayerId = player.Id,
                        IsActive = true,
                        }
                    ) != 1
                )
                {
                    throw new InvalidOperationException();
                }

                if (
                    game is not null
                    && !transaction
                        .Table<GameScoringSession>()
                        .Any(session => session.GameId == game.Id)
                )
                {
                    var existingLineup = transaction
                        .Table<GameLineupPlayer>()
                        .Where(item => item.GameId == game.Id)
                        .ToList();
                    var nextBattingOrder =
                        existingLineup.Count == 0
                        ? 1
                        : existingLineup.Max(item => item.BattingOrder) + 1;
                    if (
                        transaction.Insert(
                            new GameLineupPlayer
                        {
                            GameId = game.Id,
                            PlayerId = player.Id,
                            BattingOrder = nextBattingOrder,
                            }
                        ) != 1
                    )
                    {
                        throw new InvalidOperationException();
                    }
                }

                result = new(player.Id, CreatePlayerFailure.None);
            });
        }
        catch
        {
            player.Id = 0;
            return new(0, CreatePlayerFailure.Persistence);
        }

        return result;
    }

    public async Task<int> UpdateAsync(Player player)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.ExecuteAsync(
            "UPDATE Player SET FirstName = ?, LastName = ? WHERE Id = ? AND IsActive = ?",
            player.FirstName,
            player.LastName,
            player.Id,
            true
        );
    }

    public async Task<int> DeactivateAsync(IReadOnlyCollection<int>? playerIds)
    {
        if (playerIds is null || playerIds.Count == 0)
        {
            return 0;
        }

        var validPlayerIds = playerIds.Where(playerId => playerId > 0).Distinct().ToArray();

        if (validPlayerIds.Length == 0)
        {
            return 0;
        }

        var placeholders = string.Join(", ", validPlayerIds.Select(_ => "?"));
        var parameters = new object[] { false, true }
            .Concat(validPlayerIds.Cast<object>())
            .ToArray();
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;

        return await connection.ExecuteAsync(
            $"UPDATE Player SET IsActive = ? WHERE IsActive = ? AND Id IN ({placeholders})",
            parameters
        );
    }

    public async Task<int> ReactivateAsync(IReadOnlyCollection<int>? playerIds)
    {
        if (playerIds is null || playerIds.Count == 0)
        {
            return 0;
        }

        var validPlayerIds = playerIds.Where(playerId => playerId > 0).Distinct().ToArray();

        if (validPlayerIds.Length == 0)
        {
            return 0;
        }

        var placeholders = string.Join(", ", validPlayerIds.Select(_ => "?"));
        var parameters = new object[] { true, false }
            .Concat(validPlayerIds.Cast<object>())
            .ToArray();
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;

        return await connection.ExecuteAsync(
            $"UPDATE Player SET IsActive = ? WHERE IsActive = ? AND Id IN ({placeholders})",
            parameters
        );
    }

    public async Task<List<Player>> GetActiveAsync()
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var activePlayers = await connection
            .Table<Player>()
            .Where(player => player.IsActive)
            .ToListAsync();

        return activePlayers
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<Player>> GetInactiveAsync()
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var inactivePlayers = await connection
            .Table<Player>()
            .Where(player => !player.IsActive)
            .ToListAsync();

        return inactivePlayers
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Player?> GetActiveByIdAsync(int id)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<Player>()
            .Where(player => player.Id == id && player.IsActive)
            .FirstOrDefaultAsync();
    }
}
