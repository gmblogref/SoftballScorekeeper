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
            teamId
        );
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
            teamId
        );

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
            teamId
        );

        return games.FirstOrDefault()?.SeasonId;
    }

    public async Task<Game?> GetByIdForTeamAsync(int teamId, int gameId)
    {
        if (teamId <= 0 || gameId <= 0)
        {
            return null;
        }

        var connection = await database.GetConnectionAsync();
        return await connection
            .Table<Game>()
            .Where(game => game.Id == gameId && game.TeamId == teamId)
            .FirstOrDefaultAsync();
    }

    public async Task<CreateGamesResult> CreateForTeamAsync(
        int teamId,
        Season? season,
        IReadOnlyCollection<Game>? games
    )
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
                var activeTeam = transaction
                    .Table<Team>()
                    .FirstOrDefault(team =>
                        team.Id == teamId && team.IsActive && !team.IsYearOver);
                if (activeTeam is null)
                {
                    return;
                }

                Season? persistedSeason;
                if (season!.Id > 0)
                {
                    persistedSeason = transaction.Find<Season>(season.Id);
                    if (
                        persistedSeason?.TeamId != teamId
                        || persistedSeason.Year != activeTeam.Year
                    )
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
                        season.Year
                    );
                    if (persistedSeason is null)
                    {
                        return;
                    }
                }

                var existingGames = transaction
                    .Table<Game>()
                    .Where(game => game.TeamId == teamId && game.SeasonId == persistedSeason.Id)
                    .ToList();
                if (
                    requestedGames.Any(requested =>
                        existingGames.Any(existing =>
                            existing.ScheduledDate.Date == requested.ScheduledDate.Date
                            && existing.ScheduledTime == requested.ScheduledTime
                        )
                    )
                )
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
                        throw new InvalidOperationException(
                            "Game insertion did not affect one row."
                        );
                    }

                    createdIds.Add(game.Id);
                }

                result = new CreateGamesResult(
                    requestedGames.Length,
                    persistedSeason.Id,
                    createdIds,
                    CreateGamesFailure.None
                );
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

    public async Task<RemoveGamesResult> RemoveGamesAsync(
        int teamId,
        IReadOnlyCollection<int>? gameIds
    )
    {
        var requestedIds = gameIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (teamId <= 0 || requestedIds.Length == 0)
        {
            return EmptyRemovalResult(requestedIds);
        }

        var connection = await database.GetConnectionAsync();
        var activeTeam = await connection
            .Table<Team>()
            .Where(team => team.Id == teamId && team.IsActive)
            .FirstOrDefaultAsync();
        if (activeTeam is null || activeTeam.IsYearOver)
        {
            return EmptyRemovalResult(requestedIds);
        }

        var ignoredIds = requestedIds.Where(id => id <= 0).ToArray();
        var positiveIds = requestedIds.Where(id => id > 0).ToArray();
        var persistedGames = await connection.Table<Game>().ToListAsync();
        var gamesById = persistedGames
            .Where(game => positiveIds.Contains(game.Id))
            .ToDictionary(game => game.Id);
        var missingIds = positiveIds.Where(id => !gamesById.ContainsKey(id)).ToArray();
        var wrongTeamIds = positiveIds
            .Where(id => gamesById.TryGetValue(id, out var game) && game.TeamId != teamId)
            .ToArray();
        var eligibleIds = positiveIds
            .Where(id => gamesById.TryGetValue(id, out var game) && game.TeamId == teamId)
            .ToArray();
        var deletedIds = new List<int>();
        var blockedIds = new List<int>();

        await connection.RunInTransactionAsync(transaction =>
        {
            if (!TeamYearWriteGuard.IsOpen(transaction, teamId))
                return;
            foreach (var gameId in eligibleIds)
            {
                if (HasDependentHistory(transaction, gameId))
                {
                    blockedIds.Add(gameId);
                    continue;
                }

                if (
                    transaction.Execute(
                        "DELETE FROM Game WHERE Id = ? AND TeamId = ?",
                        gameId,
                        teamId
                    ) == 1
                )
                {
                    deletedIds.Add(gameId);
                }
                else
                {
                    blockedIds.Add(gameId);
                }
            }
        });

        return new RemoveGamesResult(
            requestedIds,
            deletedIds,
            missingIds,
            wrongTeamIds,
            blockedIds,
            ignoredIds
        );
    }

    public async Task<UpdateGameResult> UpdateForTeamAsync(
        int teamId,
        int gameId,
        Season? season,
        Game? requestedGame
    )
    {
        if (
            teamId <= 0
            || gameId <= 0
            || season is null
            || requestedGame is null
            || requestedGame.ScheduledDate == default
            || requestedGame.ScheduledTime < TimeSpan.Zero
            || requestedGame.ScheduledTime >= TimeSpan.FromDays(1)
            || (
                season.Id <= 0
                && !SeasonRepository.HasValidSeasonValues(teamId, season.Name, season.Year)
            )
        )
        {
            return new UpdateGameResult(gameId, 0, UpdateGameFailure.InvalidRequest);
        }

        var connection = await database.GetConnectionAsync();
        var result = new UpdateGameResult(gameId, 0, UpdateGameFailure.InvalidTeamOrGame);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var activeTeam = transaction
                    .Table<Team>()
                    .FirstOrDefault(team =>
                        team.Id == teamId && team.IsActive && !team.IsYearOver);
                if (activeTeam is null)
                {
                    return;
                }

                var persistedGame = transaction.Find<Game>(gameId);
                if (persistedGame?.TeamId != teamId)
                {
                    return;
                }

                Season? persistedSeason;
                if (season.Id > 0)
                {
                    persistedSeason = transaction.Find<Season>(season.Id);
                    if (
                        persistedSeason?.TeamId != teamId
                        || persistedSeason.Year != activeTeam.Year
                    )
                    {
                        result = new UpdateGameResult(gameId, 0, UpdateGameFailure.InvalidSeason);
                        return;
                    }
                }
                else
                {
                    persistedSeason = SeasonRepository.ResolveOrCreateForActiveTeam(
                        transaction,
                        teamId,
                        season.Name,
                        season.Year
                    );
                    if (persistedSeason is null)
                    {
                        result = new UpdateGameResult(gameId, 0, UpdateGameFailure.InvalidSeason);
                        return;
                    }
                }

                var conflict = transaction
                    .Table<Game>()
                    .FirstOrDefault(game =>
                        game.Id != gameId
                        && game.TeamId == teamId
                        && game.SeasonId == persistedSeason.Id
                        && game.ScheduledDate == requestedGame.ScheduledDate.Date
                        && game.ScheduledTime == requestedGame.ScheduledTime
                    );
                if (conflict is not null)
                {
                    throw new DuplicateScheduledGameException();
                }

                persistedGame.SeasonId = persistedSeason.Id;
                persistedGame.ScheduledDate = requestedGame.ScheduledDate.Date;
                persistedGame.ScheduledTime = requestedGame.ScheduledTime;
                persistedGame.Opponent = NormalizeOptionalValue(requestedGame.Opponent);
                persistedGame.Field = NormalizeOptionalValue(requestedGame.Field);
                if (transaction.Update(persistedGame) != 1)
                {
                    throw new InvalidOperationException("Game update did not affect one row.");
                }

                result = new UpdateGameResult(gameId, persistedSeason.Id, UpdateGameFailure.None);
            });
        }
        catch (DuplicateScheduledGameException)
        {
            return new UpdateGameResult(gameId, 0, UpdateGameFailure.DuplicateSchedule);
        }
        catch
        {
            return new UpdateGameResult(gameId, 0, UpdateGameFailure.Persistence);
        }

        return result;
    }

    private static bool HasValidRequest(int teamId, Season? season, IReadOnlyList<Game> games)
    {
        if (
            teamId <= 0
            || season is null
            || games.Count is < 1 or > 2
            || games.Any(game =>
                game is null
                || game.ScheduledDate == default
                || game.ScheduledTime < TimeSpan.Zero
                || game.ScheduledTime >= TimeSpan.FromDays(1)
            )
        )
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
        CreateGamesFailure failureReason
    )
    {
        return new CreateGamesResult(requestedGameCount, 0, Array.Empty<int>(), failureReason);
    }

    private static RemoveGamesResult EmptyRemovalResult(IReadOnlyList<int> requestedIds)
    {
        return new RemoveGamesResult(
            requestedIds,
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            requestedIds
        );
    }

    private static bool HasDependentHistory(SQLite.SQLiteConnection transaction, int gameId)
    {
        return transaction.Table<GameLineupPlayer>().Any(item => item.GameId == gameId)
            || transaction.Table<GameScoringSession>().Any(session => session.GameId == gameId);
    }

    private sealed class DuplicateScheduledGameException : Exception;
}
