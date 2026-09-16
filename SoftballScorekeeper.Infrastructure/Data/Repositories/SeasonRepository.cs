using SoftballScorekeeper.Core.Models;
using SQLite;

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

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection.QueryAsync<Season>(
            """
            SELECT Season.*
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ?
            ORDER BY Season.Year, Season.Id
            """,
            teamId
        );
    }

    public async Task<Season?> GetByIdForTeamAsync(int teamId, int seasonId)
    {
        if (teamId <= 0 || seasonId <= 0)
        {
            return null;
        }

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var seasons = await connection.QueryAsync<Season>(
            """
            SELECT Season.*
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ? AND Season.Id = ?
            LIMIT 1
            """,
            teamId,
            seasonId
        );

        return seasons.FirstOrDefault();
    }

    public async Task<bool> HasSeasonsForTeamAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return false;
        }

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM Season
            INNER JOIN Team ON Team.Id = Season.TeamId
            WHERE Season.TeamId = ?
            """,
            teamId
        );

        return count > 0;
    }

    public async Task<Season?> GetOrCreateForTeamAsync(int teamId, string? name, int year)
    {
        if (!HasValidSeasonValues(teamId, name, year))
        {
            return null;
        }

        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        Season? resolvedSeason = null;
        await connection.RunInTransactionAsync(transaction =>
        {
            resolvedSeason = ResolveOrCreateForActiveTeam(transaction, teamId, name!, year);
        });

        return resolvedSeason;
    }

    internal static Season? ResolveOrCreateForActiveTeam(
        SQLiteConnection connection,
        int teamId,
        string name,
        int year
    )
    {
        var normalizedName = name.Trim();
        var activeTeam = connection
            .Table<Team>()
            .FirstOrDefault(team => team.Id == teamId && team.IsActive);
        if (activeTeam is null || activeTeam.IsYearOver || activeTeam.Year != year)
        {
            return null;
        }

        var existingSeason = connection
            .Table<Season>()
            .Where(season => season.TeamId == teamId && season.Year == year)
            .ToList()
            .OrderBy(season => season.Id)
            .FirstOrDefault(season =>
                string.Equals(
                    season.Name.Trim(),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        if (existingSeason is not null)
        {
            return existingSeason;
        }

        var season = new Season
        {
            TeamId = teamId,
            Name = normalizedName,
            Year = year,
        };

        return connection.Insert(season) == 1 ? season : null;
    }

    internal static bool HasValidSeasonValues(int teamId, string? name, int year)
    {
        return teamId > 0 && !string.IsNullOrWhiteSpace(name) && year is >= 1 and <= 9999;
    }
}
