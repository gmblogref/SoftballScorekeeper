using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameRepository
{
    private readonly AppDatabase database;

    public GameRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
    }

    public async Task<List<Game>> GetForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return [];
        }

        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<Game>(
            """
            SELECT Game.*
            FROM Game
            INNER JOIN Team ON Team.Id = Game.TeamId
            WHERE Game.TeamId = ?
            ORDER BY Game.ScheduledDate, Game.ScheduledTime, Game.Id
            """,
            teamId);
    }

    public async Task<bool> HasGamesForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return false;
        }

        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM Game
            INNER JOIN Team ON Team.Id = Game.TeamId
            WHERE Game.TeamId = ?
            """,
            teamId);

        return count > 0;
    }
}
