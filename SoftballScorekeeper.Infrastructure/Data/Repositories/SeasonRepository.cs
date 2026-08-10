using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class SeasonRepository
{
    private readonly AppDatabase database;

    public SeasonRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
    }

    public async Task<List<Season>> GetForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return [];
        }

        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<Season>(
            """
            SELECT Season.*
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ?
            ORDER BY Season.Year, Season.Name, Season.Id
            """,
            teamId);
    }

    public async Task<Season?> GetByIdForTeamAsync(int teamId, int seasonId)
    {
        if (teamId <= 0 || seasonId <= 0)
        {
            return null;
        }

        var connection = await database.GetConnectionAsync();
        var seasons = await connection.QueryAsync<Season>(
            """
            SELECT Season.*
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ? AND Season.Id = ?
            LIMIT 1
            """,
            teamId,
            seasonId);

        return seasons.FirstOrDefault();
    }

    public async Task<bool> HasSeasonsForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return false;
        }

        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ?
            """,
            teamId);

        return count > 0;
    }
}
