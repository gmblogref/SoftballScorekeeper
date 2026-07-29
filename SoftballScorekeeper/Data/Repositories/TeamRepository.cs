using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.Data.Repositories;

public sealed class TeamRepository
{
    private readonly AppDatabase database;

    public TeamRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
    }

    public async Task<int> CreateAsync(Team team)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.InsertAsync(team);
    }

    public async Task<List<Team>> GetActiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Team>()
            .Where(team => team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync();
    }

    public async Task<bool> ActiveNameExistsAsync(string name)
    {
        var normalizedName = name.Trim();
        var connection = await database.GetConnectionAsync();
        var activeTeams = await connection.Table<Team>()
            .Where(team => team.IsActive)
            .ToListAsync();

        return activeTeams.Any(
            team => string.Equals(
                team.Name.Trim(),
                normalizedName,
                StringComparison.OrdinalIgnoreCase));
    }
}
