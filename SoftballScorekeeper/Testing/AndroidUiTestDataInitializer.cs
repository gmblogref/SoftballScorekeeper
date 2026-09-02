#if DEBUG
using SQLite;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Testing;

public sealed class AndroidUiTestDataInitializer
{
    internal const string RequestFileName = "android-ui-test-seed";
    internal const string MissingGameRequestFileName = "android-ui-test-missing-game";
    internal const string CompletedStatisticsSortingRequestFileName =
        "android-ui-test-completed-statistics-sorting";

    private readonly AppDatabase database;
    private readonly TeamRepository teamRepository;
    private readonly PlayerRepository playerRepository;
    private readonly GameRepository gameRepository;
    private readonly GameLineupRepository lineupRepository;
    private readonly GameScoringSessionRepository scoringRepository;

    public AndroidUiTestDataInitializer(
        AppDatabase database,
        TeamRepository teamRepository,
        PlayerRepository playerRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository,
        GameScoringSessionRepository scoringRepository)
    {
        this.database = database;
        this.teamRepository = teamRepository;
        this.playerRepository = playerRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
        this.scoringRepository = scoringRepository;
    }

    internal async Task<AndroidUiTestContext?> InitializeIfRequestedAsync()
    {
        var requestPath = Path.Combine(FileSystem.AppDataDirectory, RequestFileName);
        var missingGameRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, MissingGameRequestFileName);
        var completedStatisticsSortingRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, CompletedStatisticsSortingRequestFileName);
        var useMissingGame = File.Exists(missingGameRequestPath);
        var useCompletedStatisticsSorting = File.Exists(
            completedStatisticsSortingRequestPath);
        if (!File.Exists(requestPath) && !useMissingGame
            && !useCompletedStatisticsSorting) return null;

        if (File.Exists(requestPath)) File.Delete(requestPath);
        if (useMissingGame) File.Delete(missingGameRequestPath);
        if (useCompletedStatisticsSorting)
            File.Delete(completedStatisticsSortingRequestPath);

        var team = new Team { Name = "Automation Team", IsActive = true };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "team");

        var playerIds = new List<int>();
        var firstNames = useCompletedStatisticsSorting
            ? new[] { "Alex", "Blake", "Casey" }
            : new[]
            {
                "Alex",
                "Blake",
                "Casey",
                "Drew",
                "Evan",
                "Finley",
                "Gray",
                "Harper",
                "Jordan",
                "Morgan",
            };
        foreach (var firstName in firstNames)
        {
            var result = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Automation",
                    IsActive = true,
                },
                team.Id);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Could not seed UI-test Player {firstName}.");
            playerIds.Add(result.PlayerId);
        }

        var gameResult = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Automation", Year = 2026 },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 8, 27),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Automation Opponent",
                Field = "Automation Field",
            }]);
        if (!gameResult.IsCompleteSuccess)
            throw new InvalidOperationException("Could not seed the Android UI-test Game.");

        var gameId = gameResult.CreatedGameIds.Single();
        var lineupResult = await lineupRepository.SaveInitialAsync(team.Id, gameId, playerIds);
        if (!lineupResult.IsSuccess)
            throw new InvalidOperationException("Could not seed the Android UI-test lineup.");

        if (useCompletedStatisticsSorting)
            await SeedCompletedStatisticsSortingGameAsync(team.Id, gameId, playerIds);

        return new AndroidUiTestContext(team.Id, useMissingGame ? gameId + 1 : gameId);
    }

    private async Task SeedCompletedStatisticsSortingGameAsync(
        int teamId,
        int gameId,
        IReadOnlyList<int> playerIds)
    {
        var initialized = await scoringRepository.InitializeAsync(
            teamId, gameId, TeamGameSide.Away);
        if (!initialized.IsSuccess)
            throw new InvalidOperationException(
                "Could not initialize the Android statistics-sorting Game.");

        var appearances = new[]
        {
            (playerIds[0], 1, BatterResult.Single),
            (playerIds[0], 1, BatterResult.Double),
            (playerIds[0], 1, BatterResult.HomeRun),
            (playerIds[0], 1, BatterResult.Out),
            (playerIds[0], 1, BatterResult.Walk),
            (playerIds[1], 2, BatterResult.Single),
            (playerIds[1], 2, BatterResult.Single),
            (playerIds[1], 2, BatterResult.Out),
            (playerIds[1], 2, BatterResult.Out),
            (playerIds[1], 2, BatterResult.FielderChoice),
            (playerIds[1], 2, BatterResult.SacrificeFly),
            (playerIds[2], 3, BatterResult.Triple),
            (playerIds[2], 3, BatterResult.Out),
            (playerIds[2], 3, BatterResult.Walk),
            (playerIds[2], 3, BatterResult.Walk),
        };
        var connection = new SQLiteAsyncConnection(
            database.DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);
        try
        {
            for (var index = 0; index < appearances.Length; index++)
            {
                var (playerId, battingPosition, result) = appearances[index];
                EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
                {
                    GameScoringSessionId = initialized.Session!.Id,
                    PlayerId = playerId,
                    BattingPosition = battingPosition,
                    PlateAppearanceNumber = index + 1,
                    Inning = 1,
                    Result = result,
                }), "statistics-sorting plate appearance");
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        if (!(await scoringRepository.CompleteGameAsync(gameId)).IsSuccess)
            throw new InvalidOperationException(
                "Could not complete the Android statistics-sorting Game.");
    }

    private static void EnsureSingleRow(int affectedRows, string entityName)
    {
        if (affectedRows != 1)
            throw new InvalidOperationException($"Could not seed the Android UI-test {entityName}.");
    }
}

internal sealed record AndroidUiTestContext(int TeamId, int GameId);
#endif
