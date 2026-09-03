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
    internal const string TeamRecordRequestFileName = "android-ui-test-team-record";
    private static readonly string[] RequestFileNames =
    [
        RequestFileName,
        MissingGameRequestFileName,
        CompletedStatisticsSortingRequestFileName,
        TeamRecordRequestFileName,
    ];

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
        var teamRecordRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, TeamRecordRequestFileName);
        var useMissingGame = File.Exists(missingGameRequestPath);
        var useCompletedStatisticsSorting = File.Exists(
            completedStatisticsSortingRequestPath);
        var useTeamRecord = File.Exists(teamRecordRequestPath);
        if (!File.Exists(requestPath) && !useMissingGame
            && !useCompletedStatisticsSorting && !useTeamRecord) return null;

        if (File.Exists(requestPath)) File.Delete(requestPath);
        if (useMissingGame) File.Delete(missingGameRequestPath);
        if (useCompletedStatisticsSorting)
            File.Delete(completedStatisticsSortingRequestPath);
        if (useTeamRecord) File.Delete(teamRecordRequestPath);

        if (useTeamRecord) return await SeedTeamRecordAsync();

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

        return new AndroidUiTestContext(
            team.Id, useMissingGame ? gameId + 1 : gameId, false);
    }

    internal static bool IsInitializationRequested(string appDataDirectory) =>
        RequestFileNames.Any(fileName => File.Exists(Path.Combine(appDataDirectory, fileName)));

    private async Task<AndroidUiTestContext> SeedTeamRecordAsync()
    {
        var team = new Team { Name = "Automation Record Team", IsActive = true };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "Team-record team");
        var playerResult = await playerRepository.CreateForTeamAsync(
            new Player
            {
                FirstName = "Record",
                LastName = "Automation",
                IsActive = true,
            },
            team.Id);
        if (!playerResult.IsSuccess)
            throw new InvalidOperationException(
                "Could not seed the Android Team-record Player.");

        var oldGameId = await CreateTeamRecordGameAsync(
            team.Id, "Fall", 2025, new DateTime(2025, 9, 1));
        var winGameId = await CreateTeamRecordGameAsync(
            team.Id, "Spring", 2026, new DateTime(2026, 4, 1));
        var latestGames = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Summer", Year = 2026 },
            [
                new Game
                {
                    ScheduledDate = new DateTime(2026, 7, 1),
                    ScheduledTime = new TimeSpan(18, 30, 0),
                    Opponent = "Automation Loss Opponent",
                },
                new Game
                {
                    ScheduledDate = new DateTime(2026, 7, 8),
                    ScheduledTime = new TimeSpan(18, 30, 0),
                    Opponent = "Automation Tie Opponent",
                },
            ]);
        if (!latestGames.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed the Android Team-record Summer Games.");

        var allGameIds = new[]
        {
            oldGameId,
            winGameId,
            latestGames.CreatedGameIds[0],
            latestGames.CreatedGameIds[1],
        };
        foreach (var gameId in allGameIds)
        {
            var lineupResult = await lineupRepository.SaveInitialAsync(
                team.Id, gameId, [playerResult.PlayerId]);
            if (!lineupResult.IsSuccess)
                throw new InvalidOperationException(
                    "Could not seed an Android Team-record Game lineup.");
        }

        var connection = new SQLiteAsyncConnection(
            database.DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);
        try
        {
            await InsertCompletedSessionAsync(
                connection, oldGameId, playerResult.PlayerId, TeamGameSide.Home, 12, 1);
            await InsertCompletedSessionAsync(
                connection, winGameId, playerResult.PlayerId, TeamGameSide.Home, 8, 3);
            await InsertCompletedSessionAsync(
                connection,
                latestGames.CreatedGameIds[0],
                playerResult.PlayerId,
                TeamGameSide.Away,
                8,
                3);
            await InsertCompletedSessionAsync(
                connection,
                latestGames.CreatedGameIds[1],
                playerResult.PlayerId,
                TeamGameSide.Home,
                6,
                6);
        }
        finally
        {
            await connection.CloseAsync();
        }

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<int> CreateTeamRecordGameAsync(
        int teamId,
        string seasonName,
        int seasonYear,
        DateTime date)
    {
        var result = await gameRepository.CreateForTeamAsync(
            teamId,
            new Season { Name = seasonName, Year = seasonYear },
            [new Game
            {
                ScheduledDate = date,
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = $"Automation {seasonName} Opponent",
            }]);
        if (!result.IsCompleteSuccess)
            throw new InvalidOperationException(
                $"Could not seed the Android Team-record {seasonName} Game.");
        return result.CreatedGameIds.Single();
    }

    private static async Task InsertCompletedSessionAsync(
        SQLiteAsyncConnection connection,
        int gameId,
        int playerId,
        TeamGameSide side,
        int homeScore,
        int visitorScore)
    {
        var session = new GameScoringSession
        {
            GameId = gameId,
            Status = GameScoringStatus.Completed,
            TeamSide = side,
            HomeScore = homeScore,
            VisitorScore = visitorScore,
            CurrentBatterPosition = 1,
            CurrentInning = 1,
            CompletedAtUtc = DateTime.UtcNow,
        };
        EnsureSingleRow(
            await connection.InsertAsync(session), "Team-record scoring session");
        EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
        {
            GameScoringSessionId = session.Id,
            PlayerId = playerId,
            BattingPosition = 1,
        }), "Team-record frozen lineup Player");
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

internal sealed record AndroidUiTestContext(int TeamId, int GameId, bool OpenTeamManager);
#endif
