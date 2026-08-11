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

    public async Task<int?> GetMostRecentlyUsedSeasonIdAsync(int teamId)
    {
        if (teamId <= 0)
        {
            return null;
        }

        var connection = await database.GetConnectionAsync();
        var games = await connection.QueryAsync<Game>(
            """
            SELECT Game.*
            FROM Game
            INNER JOIN Team ON Team.Id = Game.TeamId
            WHERE Game.TeamId = ?
            ORDER BY Game.Id DESC
            LIMIT 1
            """,
            teamId);

        return games.FirstOrDefault()?.SeasonId;
    }

    public async Task<CreateGamesResult> CreateForTeamAsync(
        int teamId,
        Season? season,
        IReadOnlyCollection<Game>? games)
    {
        var requestedGames = games?.ToArray() ?? Array.Empty<Game>();
        if (!HasValidRequest(teamId, season, requestedGames))
        {
            return FailedResult(requestedGames.Length, CreateGamesFailure.InvalidRequest);
        }

        if (HasDuplicateRequestedSchedule(requestedGames))
        {
            return FailedResult(requestedGames.Length, CreateGamesFailure.DuplicateSchedule);
        }

        var connection = await database.GetConnectionAsync();
        var result = FailedResult(requestedGames.Length, CreateGamesFailure.InvalidTeamOrSeason);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var activeTeam = transaction.Table<Team>()
                    .FirstOrDefault(team => team.Id == teamId && team.IsActive);
                if (activeTeam is null)
                {
                    return;
                }

                Season? persistedSeason;
                if (season!.Id > 0)
                {
                    persistedSeason = transaction.Find<Season>(season.Id);
                    if (persistedSeason?.TeamId != teamId)
                    {
                        return;
                    }
                }
                else
                {
                    persistedSeason = SeasonRepository.ResolveOrCreateForActiveTeam(
                        transaction,
                        teamId,
                        season.Name,
                        season.Year);
                    if (persistedSeason is null)
                    {
                        return;
                    }
                }

                var existingGames = transaction.Table<Game>()
                    .Where(game => game.TeamId == teamId && game.SeasonId == persistedSeason.Id)
                    .ToList();
                if (requestedGames.Any(requested => existingGames.Any(existing =>
                        existing.ScheduledDate.Date == requested.ScheduledDate.Date
                        && existing.ScheduledTime == requested.ScheduledTime)))
                {
                    throw new DuplicateScheduledGameException();
                }

                var createdIds = new List<int>(requestedGames.Length);
                foreach (var requestedGame in requestedGames)
                {
                    var game = new Game
                    {
                        TeamId = teamId,
                        SeasonId = persistedSeason.Id,
                        ScheduledDate = requestedGame.ScheduledDate.Date,
                        ScheduledTime = requestedGame.ScheduledTime,
                        Opponent = NormalizeOptionalValue(requestedGame.Opponent),
                        Field = NormalizeOptionalValue(requestedGame.Field),
                    };

                    if (transaction.Insert(game) != 1)
                    {
                        throw new InvalidOperationException("Game insertion did not affect one row.");
                    }

                    createdIds.Add(game.Id);
                }

                result = new CreateGamesResult(
                    requestedGames.Length,
                    persistedSeason.Id,
                    createdIds,
                    CreateGamesFailure.None);
            });
        }
        catch (DuplicateScheduledGameException)
        {
            return FailedResult(requestedGames.Length, CreateGamesFailure.DuplicateSchedule);
        }
        catch
        {
            return FailedResult(requestedGames.Length, CreateGamesFailure.Persistence);
        }

        return result;
    }

    private static bool HasValidRequest(int teamId, Season? season, IReadOnlyList<Game> games)
    {
        if (teamId <= 0
            || season is null
            || games.Count is < 1 or > 2
            || games.Any(game => game is null
                || game.ScheduledDate == default
                || game.ScheduledTime < TimeSpan.Zero
                || game.ScheduledTime >= TimeSpan.FromDays(1)))
        {
            return false;
        }

        return season.Id > 0
            || SeasonRepository.HasValidSeasonValues(teamId, season.Name, season.Year);
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasDuplicateRequestedSchedule(IReadOnlyList<Game> games)
    {
        return games
            .Select(game => (game.ScheduledDate.Date, game.ScheduledTime))
            .Distinct()
            .Count() != games.Count;
    }

    private static CreateGamesResult FailedResult(
        int requestedGameCount,
        CreateGamesFailure failureReason)
    {
        return new CreateGamesResult(
            requestedGameCount,
            0,
            Array.Empty<int>(),
            failureReason);
    }

    private sealed class DuplicateScheduledGameException : Exception;
}
