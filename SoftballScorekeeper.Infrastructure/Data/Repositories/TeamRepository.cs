using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

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
        if (!HasValidIdentity(team.Name, team.Year) || team.PreviousTeamId is not null)
            return 0;
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        if (await NameYearExistsAsync(connection, team.Name, team.Year, null))
            return 0;
        team.Name = team.Name.Trim();
        if (team.TeamLineageId == Guid.Empty)
            team.TeamLineageId = Guid.NewGuid();
        return await connection.InsertAsync(team);
    }

    public async Task<int> UpdateAsync(Team team)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var persisted = await connection.FindAsync<Team>(team.Id);
        if (
            persisted is null
            || !HasValidIdentity(team.Name, team.Year)
            || team.Year != persisted.Year
            || team.TeamLineageId != persisted.TeamLineageId
            || team.PreviousTeamId != persisted.PreviousTeamId
            || team.IsYearOver != persisted.IsYearOver
            || persisted.IsYearOver
            || await NameYearExistsAsync(connection, team.Name, team.Year, team.Id)
        )
            return 0;
        team.Name = team.Name.Trim();
        return await connection.UpdateAsync(team);
    }

    public async Task<bool> NameYearExistsAsync(string name, int year, int? excludedTeamId = null)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await NameYearExistsAsync(connection, name, year, excludedTeamId);
    }

    public async Task<int> DeactivateAsync(int id)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.ExecuteAsync(
            "UPDATE Team SET IsActive = ? WHERE Id = ? AND IsActive = ?",
            false,
            id,
            true
        );
    }

    public async Task<int> ReactivateAsync(int id)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.ExecuteAsync(
            "UPDATE Team SET IsActive = ? WHERE Id = ? AND IsActive = ?",
            true,
            id,
            false
        );
    }

    public async Task<List<Team>> GetActiveAsync()
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<Team>()
            .Where(team => team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync();
    }

    public async Task<List<Team>> GetInactiveAsync()
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<Team>()
            .Where(team => !team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync();
    }

    public async Task<Team?> GetActiveByIdAsync(int id)
    {
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<Team>()
            .Where(team => team.Id == id && team.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasSuccessorAsync(int teamId)
    {
        if (teamId <= 0)
            return false;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<Team>()
            .Where(team => team.PreviousTeamId == teamId)
            .CountAsync() > 0;
    }

    public async Task<TeamYearSummaryEligibility?> GetYearSummaryEligibilityAsync(int teamId)
    {
        if (teamId <= 0)
            return null;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var team = await connection.FindAsync<Team>(teamId);
        if (team is null)
            return null;

        var hasSuccessor = await connection
            .Table<Team>()
            .Where(item => item.PreviousTeamId == teamId)
            .CountAsync() > 0;
        return new(hasSuccessor, team.IsYearOver);
    }

    public async Task<bool> MarkYearOverAsync(int teamId)
    {
        if (teamId <= 0)
            return false;

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var marked = false;
        await connection.RunInTransactionAsync(transaction =>
        {
            var team = transaction.Find<Team>(teamId);
            if (team is null)
                return;
            if (team.IsYearOver)
            {
                marked = true;
                return;
            }
            if (transaction.Table<Team>().Any(item => item.PreviousTeamId == teamId))
                return;

            marked = transaction.Execute(
                "UPDATE Team SET IsYearOver = ? WHERE Id = ? AND IsYearOver = ?",
                true,
                teamId,
                false
            ) == 1;
        });
        return marked;
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
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var activeTeams = await connection.Table<Team>().Where(team => team.IsActive).ToListAsync();

        return activeTeams.Any(team =>
            team.Id != excludedTeamId
            && string.Equals(team.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task<bool> InactiveNameExistsAsync(string name, int? excludedTeamId)
    {
        var normalizedName = name.Trim();
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var inactiveTeams = await connection
            .Table<Team>()
            .Where(team => !team.IsActive)
            .ToListAsync();

        return inactiveTeams.Any(team =>
            team.Id != excludedTeamId
            && string.Equals(team.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static async Task<bool> NameYearExistsAsync(
        SQLite.SQLiteAsyncConnection connection,
        string name,
        int year,
        int? excludedTeamId
    )
    {
        if (!HasValidIdentity(name, year))
            return false;
        var normalizedName = name.Trim();
        var teams = await connection.Table<Team>().Where(item => item.Year == year).ToListAsync();
        return teams.Any(item =>
            item.Id != excludedTeamId
            && string.Equals(item.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool HasValidIdentity(string? name, int year) =>
        !string.IsNullOrWhiteSpace(name) && year is >= 1 and <= 9999;
}
