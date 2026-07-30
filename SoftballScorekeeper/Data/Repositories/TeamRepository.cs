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

    public async Task<int> UpdateAsync(Team team)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.UpdateAsync(team);
    }

    public async Task<int> DeactivateAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.ExecuteAsync(
            "UPDATE Team SET IsActive = ? WHERE Id = ? AND IsActive = ?",
            false,
            id,
            true);
    }

    public async Task<int> ReactivateAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.ExecuteAsync(
            "UPDATE Team SET IsActive = ? WHERE Id = ? AND IsActive = ?",
            true,
            id,
            false);
    }

    public async Task<List<Team>> GetActiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Team>()
            .Where(team => team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync();
    }

    public async Task<List<Team>> GetInactiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Team>()
            .Where(team => !team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync();
    }

    public async Task<Team?> GetActiveByIdAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Team>()
            .Where(team => team.Id == id && team.IsActive)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ActiveNameExistsAsync(string name)
    {
        return ActiveNameExistsAsync(name, excludedTeamId: null);
    }

    public Task<bool> ActiveNameExistsAsync(string name, int excludedTeamId)
    {
        return ActiveNameExistsAsync(name, (int?)excludedTeamId);
    }

    public Task<bool> InactiveNameExistsAsync(string name)
    {
        return InactiveNameExistsAsync(name, excludedTeamId: null);
    }

    public Task<bool> InactiveNameExistsAsync(string name, int excludedTeamId)
    {
        return InactiveNameExistsAsync(name, (int?)excludedTeamId);
    }

    private async Task<bool> ActiveNameExistsAsync(string name, int? excludedTeamId)
    {
        var normalizedName = name.Trim();
        var connection = await database.GetConnectionAsync();
        var activeTeams = await connection.Table<Team>()
            .Where(team => team.IsActive)
            .ToListAsync();

        return activeTeams.Any(
            team => team.Id != excludedTeamId
                && string.Equals(
                    team.Name.Trim(),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> InactiveNameExistsAsync(string name, int? excludedTeamId)
    {
        var normalizedName = name.Trim();
        var connection = await database.GetConnectionAsync();
        var inactiveTeams = await connection.Table<Team>()
            .Where(team => !team.IsActive)
            .ToListAsync();

        return inactiveTeams.Any(
            team => team.Id != excludedTeamId
                && string.Equals(
                    team.Name.Trim(),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase));
    }
}
