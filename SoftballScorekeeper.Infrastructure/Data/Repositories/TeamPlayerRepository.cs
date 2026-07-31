using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class TeamPlayerRepository
{
    private readonly AppDatabase database;

    public TeamPlayerRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
    }

    public async Task<IReadOnlyList<Player>> GetActivePlayersForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return Array.Empty<Player>();
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection.Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();

        if (activeTeam is null)
        {
            return Array.Empty<Player>();
        }

        var players = await connection.QueryAsync<Player>(
            """
            SELECT Player.*
            FROM Player
            INNER JOIN TeamPlayer ON TeamPlayer.PlayerId = Player.Id
            WHERE TeamPlayer.TeamId = ?
                AND TeamPlayer.IsActive = ?
                AND Player.IsActive = ?
            """,
            teamId,
            true,
            true);

        return players
            .DistinctBy(player => player.Id)
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
