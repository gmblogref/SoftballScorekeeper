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
        return (await GetActiveRosterForTeamAsync(teamId))
            .Select(item => item.Player)
            .ToList();
    }

    public async Task<IReadOnlyList<TeamRosterMember>> GetActiveRosterForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return Array.Empty<TeamRosterMember>();
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection.Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();

        if (activeTeam is null)
        {
            return Array.Empty<TeamRosterMember>();
        }

        var relationships = await connection.Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId && item.IsActive)
            .ToListAsync();
        var players = await connection.Table<Player>()
            .Where(player => player.IsActive)
            .ToListAsync();
        var playersById = players.ToDictionary(player => player.Id);

        return relationships
            .OrderBy(item => item.Id)
            .Where(item => playersById.ContainsKey(item.PlayerId))
            .DistinctBy(item => item.PlayerId)
            .Select(item => new TeamRosterMember(
                item.Id,
                item.TeamId,
                playersById[item.PlayerId],
                item.JerseyNumber))
            .OrderBy(item => item.Player.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Player.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> UpdateJerseyNumberAsync(
        int teamId,
        int teamPlayerId,
        int? jerseyNumber)
    {
        if (teamId <= 0 || teamPlayerId <= 0 || jerseyNumber < 0)
        {
            return 0;
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection.Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();
        if (activeTeam is null || activeTeam.IsYearOver)
        {
            return 0;
        }

        var relationship = await connection.Table<TeamPlayer>()
            .Where(item => item.Id == teamPlayerId
                && item.TeamId == teamId
                && item.IsActive)
            .FirstOrDefaultAsync();
        if (relationship is null)
        {
            return 0;
        }

        var activePlayer = await connection.Table<Player>()
            .Where(player => player.Id == relationship.PlayerId && player.IsActive)
            .FirstOrDefaultAsync();
        if (activePlayer is null)
        {
            return 0;
        }

        return await connection.ExecuteAsync(
            "UPDATE TeamPlayer SET JerseyNumber = ? WHERE Id = ? AND TeamId = ? AND IsActive = ?",
            jerseyNumber,
            teamPlayerId,
            teamId,
            true);
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

        if (activeTeam is null || activeTeam.IsYearOver)
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

        if (activeTeam is null || activeTeam.IsYearOver)
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

    public async Task<RemovePlayersFromTeamResult> RemovePlayersFromTeamAsync(
        int teamId,
        IReadOnlyCollection<int>? playerIds)
    {
        var requestedIds = playerIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (teamId <= 0 || requestedIds.Length == 0)
        {
            return EmptyRemoveResult(requestedIds);
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection.Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();
        if (activeTeam is null || activeTeam.IsYearOver)
        {
            return EmptyRemoveResult(requestedIds);
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

        var ignored = requestedIds.Where(id => !activePlayerIds.Contains(id)).ToList();
        var alreadyInactive = new List<int>();
        var notOnTeam = new List<int>();
        var activeMembershipIds = new List<int>();

        foreach (var playerId in positiveIds.Where(activePlayerIds.Contains))
        {
            var matching = relationships.Where(item => item.PlayerId == playerId).ToList();
            if (matching.Any(item => item.IsActive))
            {
                activeMembershipIds.Add(playerId);
            }
            else if (matching.Count > 0)
            {
                alreadyInactive.Add(playerId);
            }
            else
            {
                notOnTeam.Add(playerId);
            }
        }

        if (activeMembershipIds.Count == 0)
        {
            return new RemovePlayersFromTeamResult(
                requestedIds,
                Array.Empty<int>(),
                alreadyInactive,
                notOnTeam,
                ignored);
        }

        var placeholders = string.Join(", ", activeMembershipIds.Select(_ => "?"));
        var parameters = new object[] { false, teamId, true }
            .Concat(activeMembershipIds.Cast<object>())
            .ToArray();
        await connection.ExecuteAsync(
            $"UPDATE TeamPlayer SET IsActive = ? WHERE TeamId = ? AND IsActive = ? AND PlayerId IN ({placeholders})",
            parameters);

        var remainingActiveRelationships = await connection.Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId && item.IsActive)
            .ToListAsync();
        var remainingActivePlayerIds = remainingActiveRelationships
            .Select(item => item.PlayerId)
            .ToHashSet();
        var removed = activeMembershipIds
            .Where(id => !remainingActivePlayerIds.Contains(id))
            .ToArray();
        ignored.AddRange(activeMembershipIds.Where(id => remainingActivePlayerIds.Contains(id)));

        return new RemovePlayersFromTeamResult(
            requestedIds,
            removed,
            alreadyInactive,
            notOnTeam,
            ignored.Distinct().ToArray());
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

    private static RemovePlayersFromTeamResult EmptyRemoveResult(IReadOnlyList<int> requestedIds)
    {
        return new RemovePlayersFromTeamResult(
            requestedIds,
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            requestedIds);
    }
}
