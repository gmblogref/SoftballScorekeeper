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
        return await connection.Table<Player>()
            .Where(player => !player.IsActive)
            .OrderBy(player => player.FirstName)
            .ThenBy(player => player.LastName)
            .ToListAsync();
    }
}
