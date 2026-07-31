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

    public async Task<IReadOnlyList<Player>> GetEligiblePlayersForTeamAsync(int teamId)
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

        var activePlayers = await connection.Table<Player>()
            .Where(player => player.IsActive)
            .ToListAsync();
        var activeRelationships = await connection.Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId && item.IsActive)
            .ToListAsync();
        var activeMemberIds = activeRelationships
            .Select(item => item.PlayerId)
            .ToHashSet();

        return activePlayers
            .Where(player => !activeMemberIds.Contains(player.Id))
            .OrderBy(player => player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AddPlayersToTeamResult> AddPlayersToTeamAsync(
        int teamId,
        IReadOnlyCollection<int>? playerIds)
    {
        var requestedIds = playerIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (teamId <= 0 || requestedIds.Length == 0)
        {
            return EmptyAddResult(requestedIds);
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection.Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();

        if (activeTeam is null)
        {
            return EmptyAddResult(requestedIds);
        }

        var positiveIds = requestedIds.Where(id => id > 0).ToArray();
        var activePlayers = await connection.Table<Player>()
            .Where(player => player.IsActive)
            .ToListAsync();
        var activePlayerIds = activePlayers
            .Where(player => positiveIds.Contains(player.Id))
            .Select(player => player.Id)
            .ToHashSet();
        var relationships = await connection.Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId)
            .ToListAsync();

        var eligible = new List<int>();
        var inserted = new List<int>();
        var reactivated = new List<int>();
        var alreadyActive = new List<int>();
        var ignored = requestedIds.Where(id => !activePlayerIds.Contains(id)).ToList();

        foreach (var playerId in positiveIds.Where(activePlayerIds.Contains))
        {
            var matchingRelationships = relationships
                .Where(item => item.PlayerId == playerId)
                .OrderBy(item => item.Id)
                .ToList();
            if (matchingRelationships.Any(item => item.IsActive))
            {
                alreadyActive.Add(playerId);
                continue;
            }

            eligible.Add(playerId);
            var inactiveRelationship = matchingRelationships.FirstOrDefault();
            if (inactiveRelationship is not null)
            {
                int affectedRows;
                try
                {
                    affectedRows = await connection.ExecuteAsync(
                        "UPDATE TeamPlayer SET IsActive = ? WHERE Id = ? AND IsActive = ?",
                        true,
                        inactiveRelationship.Id,
                        false);
                }
                catch
                {
                    ignored.Add(playerId);
                    continue;
                }

                if (affectedRows == 1)
                {
                    inactiveRelationship.IsActive = true;
                    reactivated.Add(playerId);
                }
                else
                {
                    ignored.Add(playerId);
                }

                continue;
            }

            var relationship = new TeamPlayer
            {
                TeamId = teamId,
                PlayerId = playerId,
                IsActive = true,
            };
            int insertedRows;
            try
            {
                insertedRows = await connection.InsertAsync(relationship);
            }
            catch
            {
                ignored.Add(playerId);
                continue;
            }

            if (insertedRows == 1)
            {
                relationships.Add(relationship);
                inserted.Add(playerId);
            }
            else
            {
                ignored.Add(playerId);
            }
        }

        return new AddPlayersToTeamResult(
            requestedIds,
            eligible,
            inserted,
            reactivated,
            alreadyActive,
            ignored.Distinct().ToArray());
    }

    public async Task<IReadOnlyList<Team>> GetActiveTeamsForPlayerAsync(int playerId)
    {
        if (playerId <= 0)
        {
            return Array.Empty<Team>();
        }

        var connection = await database.GetConnectionAsync();
        var activePlayer = await connection.Table<Player>()
            .Where(player => player.Id == playerId && player.IsActive)
            .FirstOrDefaultAsync();

        if (activePlayer is null)
        {
            return Array.Empty<Team>();
        }

        var teams = await connection.QueryAsync<Team>(
            """
            SELECT Team.*
            FROM Team
            INNER JOIN TeamPlayer ON TeamPlayer.TeamId = Team.Id
            WHERE TeamPlayer.PlayerId = ?
                AND TeamPlayer.IsActive = ?
                AND Team.IsActive = ?
            """,
            playerId,
            true,
            true);

        return teams
            .DistinctBy(team => team.Id)
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AddPlayersToTeamResult EmptyAddResult(IReadOnlyList<int> requestedIds)
    {
        return new AddPlayersToTeamResult(
            requestedIds,
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            requestedIds);
    }
}
