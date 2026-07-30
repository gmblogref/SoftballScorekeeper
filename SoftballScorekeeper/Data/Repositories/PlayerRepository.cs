using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.Data.Repositories;

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
        var connection = await database.GetConnectionAsync();
        return await connection.InsertAsync(player);
    }

    public async Task<int> UpdateAsync(Player player)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.ExecuteAsync(
            "UPDATE Player SET FirstName = ?, LastName = ? WHERE Id = ? AND IsActive = ?",
            player.FirstName,
            player.LastName,
            player.Id,
            true);
    }

    public async Task<int> DeactivateAsync(IReadOnlyCollection<int>? playerIds)
    {
        if (playerIds is null || playerIds.Count == 0)
        {
            return 0;
        }

        var validPlayerIds = playerIds
            .Where(playerId => playerId > 0)
            .Distinct()
            .ToArray();

        if (validPlayerIds.Length == 0)
        {
            return 0;
        }

        var placeholders = string.Join(", ", validPlayerIds.Select(_ => "?"));
        var parameters = new object[] { false, true }
            .Concat(validPlayerIds.Cast<object>())
            .ToArray();
        var connection = await database.GetConnectionAsync();

        return await connection.ExecuteAsync(
            $"UPDATE Player SET IsActive = ? WHERE IsActive = ? AND Id IN ({placeholders})",
            parameters);
    }

    public async Task<int> ReactivateAsync(IReadOnlyCollection<int>? playerIds)
    {
        if (playerIds is null || playerIds.Count == 0)
        {
            return 0;
        }

        var validPlayerIds = playerIds
            .Where(playerId => playerId > 0)
            .Distinct()
            .ToArray();

        if (validPlayerIds.Length == 0)
        {
            return 0;
        }

        var placeholders = string.Join(", ", validPlayerIds.Select(_ => "?"));
        var parameters = new object[] { true, false }
            .Concat(validPlayerIds.Cast<object>())
            .ToArray();
        var connection = await database.GetConnectionAsync();

        return await connection.ExecuteAsync(
            $"UPDATE Player SET IsActive = ? WHERE IsActive = ? AND Id IN ({placeholders})",
            parameters);
    }

    public async Task<List<Player>> GetActiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        var activePlayers = await connection.Table<Player>()
            .Where(player => player.IsActive)
            .ToListAsync();

        return activePlayers
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<Player>> GetInactiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        var inactivePlayers = await connection.Table<Player>()
            .Where(player => !player.IsActive)
            .ToListAsync();

        return inactivePlayers
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Player?> GetActiveByIdAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Player>()
            .Where(player => player.Id == id && player.IsActive)
            .FirstOrDefaultAsync();
    }
}
