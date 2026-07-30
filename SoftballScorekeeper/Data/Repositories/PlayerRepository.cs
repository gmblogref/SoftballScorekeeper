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

    public async Task<List<Player>> GetActiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Player>()
            .Where(player => player.IsActive)
            .OrderBy(player => player.FirstName)
            .ThenBy(player => player.LastName)
            .ToListAsync();
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
