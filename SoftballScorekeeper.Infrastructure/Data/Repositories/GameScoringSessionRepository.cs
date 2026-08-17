using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class GameScoringSessionRepository
{
    private readonly AppDatabase database;

    public GameScoringSessionRepository(AppDatabase database) => this.database = database;

    public async Task<GameScoringSession?> GetForGameAsync(int gameId)
    {
        if (gameId <= 0) return null;
        var connection = await database.GetConnectionAsync();
        return await connection.Table<GameScoringSession>()
            .Where(session => session.GameId == gameId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsForGameAsync(int gameId) =>
        await GetForGameAsync(gameId) is not null;

    public async Task<GameScoringState?> GetStateForGameAsync(int gameId)
    {
        var session = await GetForGameAsync(gameId);
        if (session is null) return null;

        var connection = await database.GetConnectionAsync();
        var sequence = await connection.Table<GameScoringLineupPlayer>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition)
            .ToListAsync();
        if (sequence.Count == 0)
            return new(session, sequence, new Dictionary<int, Player>());

        var playerIds = sequence.Select(item => item.PlayerId).Distinct().ToArray();
        var placeholders = string.Join(",", playerIds.Select(_ => "?"));
        var players = await connection.QueryAsync<Player>(
            $"SELECT * FROM Player WHERE Id IN ({placeholders})",
            playerIds.Cast<object>().ToArray());
        return new(session, sequence, players.ToDictionary(player => player.Id));
    }

    public async Task<InitializeScoringSessionResult> InitializeAsync(
        int teamId,
        int gameId,
        TeamGameSide teamSide)
    {
        if (teamId <= 0 || gameId <= 0 || !Enum.IsDefined(teamSide))
            return Failure(InitializeScoringSessionFailure.InvalidRequest);

        var connection = await database.GetConnectionAsync();
        var result = Failure(InitializeScoringSessionFailure.InvalidTeamOrGame);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var existing = transaction.Table<GameScoringSession>()
                    .FirstOrDefault(session => session.GameId == gameId);
                if (existing is not null)
                {
                    result = existing.Status == GameScoringStatus.Completed
                        ? new(existing, false, InitializeScoringSessionFailure.GameCompleted)
                        : new(existing, false, InitializeScoringSessionFailure.None);
                    return;
                }

                var team = transaction.Find<Team>(teamId);
                var game = transaction.Find<Game>(gameId);
                if (team is null || !team.IsActive || game is null || game.TeamId != teamId)
                    return;

                var lineup = transaction.Table<GameLineupPlayer>()
                    .Where(item => item.GameId == gameId)
                    .OrderBy(item => item.BattingOrder)
                    .ThenBy(item => item.Id)
                    .ToList();
                if (lineup.Count == 0
                    || lineup.Select(item => item.PlayerId).Distinct().Count() != lineup.Count
                    || !lineup.Select(item => item.BattingOrder)
                        .SequenceEqual(Enumerable.Range(1, lineup.Count)))
                {
                    result = Failure(InitializeScoringSessionFailure.InvalidLineup);
                    return;
                }

                var session = new GameScoringSession
                {
                    GameId = gameId,
                    Status = GameScoringStatus.InProgress,
                    TeamSide = teamSide,
                    HomeScore = 0,
                    VisitorScore = 0,
                    CurrentBatterPosition = 1,
                };
                if (transaction.Insert(session) != 1)
                    throw new InvalidOperationException("Scoring session insertion did not affect one row.");

                foreach (var lineupPlayer in lineup)
                {
                    var sequencePlayer = new GameScoringLineupPlayer
                    {
                        GameScoringSessionId = session.Id,
                        PlayerId = lineupPlayer.PlayerId,
                        BattingPosition = lineupPlayer.BattingOrder,
                    };
                    if (transaction.Insert(sequencePlayer) != 1)
                        throw new InvalidOperationException(
                            "Live batting-sequence insertion did not affect one row.");
                }

                result = new(session, true, InitializeScoringSessionFailure.None);
            });
        }
        catch
        {
            var existing = await GetForGameAsync(gameId);
            if (existing?.Status == GameScoringStatus.InProgress)
                return new(existing, false, InitializeScoringSessionFailure.None);
            return Failure(InitializeScoringSessionFailure.Persistence);
        }

        return result;
    }

    private static InitializeScoringSessionResult Failure(InitializeScoringSessionFailure failure) =>
        new(null, false, failure);
}
