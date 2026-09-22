#if DEBUG
using SQLite;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Testing;

public sealed class AndroidUiTestDataInitializer
{
    private static int initializationInProgress;

    internal const string ModeFileName = "android-ui-test-mode";
    internal const string RequestFileName = "android-ui-test-seed";
    internal const string MissingGameRequestFileName = "android-ui-test-missing-game";
    internal const string CompletedStatisticsSortingRequestFileName =
        "android-ui-test-completed-statistics-sorting";
    internal const string TeamRecordRequestFileName = "android-ui-test-team-record";
    internal const string BeerTotalsRequestFileName = "android-ui-test-beer-totals";
    internal const string TeamStatsRequestFileName = "android-ui-test-team-stats";
    internal const string TeamYearCopyRequestFileName = "android-ui-test-team-year-copy";
    internal const string TeamYearSummaryRequestFileName =
        "android-ui-test-team-year-summary";
    internal const string ManualRestoreRequestFileName =
        "android-ui-test-manual-restore";
    internal const string DeviceMigrationRequestFileName =
        "android-ui-test-device-migration";
    internal const string ManualFindingsReproductionRequestFileName =
        "android-ui-test-manual-findings-reproduction";
    internal const string ScheduleGamesRequestFileName = "android-ui-test-schedule-games";
    internal const string SeasonBackupReminderRequestFileName =
        "android-ui-test-season-backup-reminder";
    private static readonly string[] RequestFileNames =
    [
        RequestFileName,
        MissingGameRequestFileName,
        CompletedStatisticsSortingRequestFileName,
        TeamRecordRequestFileName,
        BeerTotalsRequestFileName,
        TeamStatsRequestFileName,
        TeamYearCopyRequestFileName,
        TeamYearSummaryRequestFileName,
        ManualRestoreRequestFileName,
        DeviceMigrationRequestFileName,
        ManualFindingsReproductionRequestFileName,
        ScheduleGamesRequestFileName,
        SeasonBackupReminderRequestFileName,
    ];

    private readonly AppDatabase database;
    private readonly TeamRepository teamRepository;
    private readonly PlayerRepository playerRepository;
    private readonly GameRepository gameRepository;
    private readonly GameLineupRepository lineupRepository;
    private readonly GameScoringSessionRepository scoringRepository;
    private readonly BeerAccountingRepository beerAccountingRepository;

    public AndroidUiTestDataInitializer(
        AppDatabase database,
        TeamRepository teamRepository,
        PlayerRepository playerRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository,
        GameScoringSessionRepository scoringRepository,
        BeerAccountingRepository beerAccountingRepository)
    {
        this.database = database;
        this.teamRepository = teamRepository;
        this.playerRepository = playerRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
        this.scoringRepository = scoringRepository;
        this.beerAccountingRepository = beerAccountingRepository;
    }

    internal static bool IsInitializationInProgress =>
        Volatile.Read(ref initializationInProgress) == 1;

    internal async Task<AndroidUiTestContext?> InitializeIfRequestedAsync()
    {
        if (!IsInitializationRequested(FileSystem.AppDataDirectory)) return null;

        Interlocked.Exchange(ref initializationInProgress, 1);
        try
        {
            return await InitializeRequestedAsync();
        }
        finally
        {
            Interlocked.Exchange(ref initializationInProgress, 0);
        }
    }

    private async Task<AndroidUiTestContext?> InitializeRequestedAsync()
    {
        var requestPath = Path.Combine(FileSystem.AppDataDirectory, RequestFileName);
        var missingGameRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, MissingGameRequestFileName);
        var completedStatisticsSortingRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, CompletedStatisticsSortingRequestFileName);
        var teamRecordRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, TeamRecordRequestFileName);
        var beerTotalsRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, BeerTotalsRequestFileName);
        var teamStatsRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, TeamStatsRequestFileName);
        var teamYearCopyRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, TeamYearCopyRequestFileName);
        var teamYearSummaryRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, TeamYearSummaryRequestFileName);
        var manualRestoreRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, ManualRestoreRequestFileName);
        var deviceMigrationRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, DeviceMigrationRequestFileName);
        var manualFindingsReproductionRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, ManualFindingsReproductionRequestFileName);
        var scheduleGamesRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, ScheduleGamesRequestFileName);
        var seasonBackupReminderRequestPath = Path.Combine(
            FileSystem.AppDataDirectory, SeasonBackupReminderRequestFileName);
        var useMissingGame = File.Exists(missingGameRequestPath);
        var useCompletedStatisticsSorting = File.Exists(
            completedStatisticsSortingRequestPath);
        var useTeamRecord = File.Exists(teamRecordRequestPath);
        var useBeerTotals = File.Exists(beerTotalsRequestPath);
        var useTeamStats = File.Exists(teamStatsRequestPath);
        var useTeamYearCopy = File.Exists(teamYearCopyRequestPath);
        var useTeamYearSummary = File.Exists(teamYearSummaryRequestPath);
        var useManualRestore = File.Exists(manualRestoreRequestPath);
        var useDeviceMigration = File.Exists(deviceMigrationRequestPath);
        var useManualFindingsReproduction = File.Exists(
            manualFindingsReproductionRequestPath);
        var useScheduleGames = File.Exists(scheduleGamesRequestPath);
        var useSeasonBackupReminder = File.Exists(seasonBackupReminderRequestPath);
        if (!File.Exists(requestPath) && !useMissingGame
            && !useCompletedStatisticsSorting && !useTeamRecord && !useBeerTotals
            && !useTeamStats && !useTeamYearCopy && !useTeamYearSummary
            && !useManualRestore && !useDeviceMigration
            && !useManualFindingsReproduction && !useScheduleGames
            && !useSeasonBackupReminder) return null;

        if (File.Exists(requestPath)) File.Delete(requestPath);
        if (useMissingGame) File.Delete(missingGameRequestPath);
        if (useCompletedStatisticsSorting)
            File.Delete(completedStatisticsSortingRequestPath);
        if (useTeamRecord) File.Delete(teamRecordRequestPath);
        if (useBeerTotals) File.Delete(beerTotalsRequestPath);
        if (useTeamStats) File.Delete(teamStatsRequestPath);
        if (useTeamYearCopy) File.Delete(teamYearCopyRequestPath);
        if (useTeamYearSummary) File.Delete(teamYearSummaryRequestPath);
        if (useManualRestore) File.Delete(manualRestoreRequestPath);
        if (useDeviceMigration) File.Delete(deviceMigrationRequestPath);
        if (useManualFindingsReproduction)
            File.Delete(manualFindingsReproductionRequestPath);
        if (useScheduleGames) File.Delete(scheduleGamesRequestPath);
        if (useSeasonBackupReminder) File.Delete(seasonBackupReminderRequestPath);

        if (useSeasonBackupReminder) return await SeedSeasonBackupReminderAsync();

        if (useScheduleGames) return await SeedScheduleGamesAsync();

        if (useManualFindingsReproduction)
            return await SeedManualFindingsReproductionAsync();

        if (useDeviceMigration) return await SeedDeviceMigrationAsync();

        if (useManualRestore) return await SeedManualRestoreAsync();

        if (useTeamYearSummary) return await SeedTeamYearSummaryAsync();

        if (useTeamYearCopy) return await SeedTeamYearCopyAsync();

        if (useTeamStats) return await SeedTeamStatsAsync();

        if (useBeerTotals) return await SeedBeerTotalsAsync();

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

    private async Task<AndroidUiTestContext> SeedManualFindingsReproductionAsync()
    {
        var team = new Team
        {
            Name = "MANUAL FINDINGS REPRODUCTION",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "manual-findings Team");

        var playerIds = new List<int>();
        foreach (var firstName in new[] { "First", "Second", "Third" })
        {
            var result = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Reproduction",
                    IsActive = true,
                },
                team.Id);
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    $"Could not seed manual-findings Player {firstName}.");
            playerIds.Add(result.PlayerId);
        }

        var gameResult = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Reproduction", Year = team.Year },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 9, 18),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Reproduction Opponent",
                Field = "Reproduction Field",
            }]);
        if (!gameResult.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed the manual-findings Game.");

        var gameId = gameResult.CreatedGameIds.Single();
        var lineupResult = await lineupRepository.SaveInitialAsync(
            team.Id, gameId, playerIds);
        if (!lineupResult.IsSuccess)
            throw new InvalidOperationException(
                "Could not seed the manual-findings lineup.");

        return new AndroidUiTestContext(team.Id, gameId, false);
    }

    private async Task<AndroidUiTestContext> SeedScheduleGamesAsync()
    {
        var team = new Team
        {
            Name = "SCHEDULE GAMES REPRODUCTION",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "schedule-games Team");

        var seasonResult = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Summer 2", Year = team.Year },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 9, 11),
                ScheduledTime = new TimeSpan(17, 0, 0),
            }]);
        if (!seasonResult.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed the schedule-games Season.");

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedSeasonBackupReminderAsync()
    {
        var latestScheduledDate = DateTime.Today.AddDays(-1);
        var team = new Team
        {
            Name = "BACKUP REMINDER TEAM",
            Year = latestScheduledDate.Year,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "backup-reminder Team");

        var player = await playerRepository.CreateForTeamAsync(
            new Player
            {
                FirstName = "Backup",
                LastName = "Reminder",
                IsActive = true,
            },
            team.Id);
        if (!player.IsSuccess)
            throw new InvalidOperationException(
                "Could not seed the backup-reminder Player.");

        var gameResult = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Completed Season", Year = team.Year },
            [new Game
            {
                ScheduledDate = latestScheduledDate,
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Backup Reminder Opponent",
            }]);
        if (!gameResult.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed the backup-reminder Game.");

        await using var operation = await database.BeginOperationAsync();
        await InsertCompletedSessionAsync(
            operation.Connection,
            gameResult.CreatedGameIds.Single(),
            player.PlayerId,
            TeamGameSide.Home,
            homeScore: 1,
            visitorScore: 0);

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    internal static bool IsInitializationRequested(string appDataDirectory) =>
        RequestFileNames.Any(fileName => File.Exists(Path.Combine(appDataDirectory, fileName)));

    internal static bool IsUiTestModeRequested(string appDataDirectory) =>
        File.Exists(Path.Combine(appDataDirectory, ModeFileName));

    private async Task<AndroidUiTestContext> SeedManualRestoreAsync()
    {
        var team = new Team
        {
            Name = "RESTORE DESTINATION ONLY",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "manual-restore destination Team");
        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedDeviceMigrationAsync()
    {
        var sourceTeam = new Team
        {
            Name = "MIGRATION HISTORY",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(sourceTeam),
            "device-migration source Team");

        var currentTeam = new Team
        {
            Name = "MIGRATION CURRENT",
            Year = 2027,
            TeamLineageId = sourceTeam.TeamLineageId,
            PreviousTeamId = sourceTeam.Id,
            IsActive = true,
        };
        var finalizedTeam = new Team
        {
            Name = "MIGRATION FINALIZED",
            Year = 2025,
            TeamLineageId = Guid.NewGuid(),
            IsYearOver = true,
            IsActive = true,
        };

        var playerIds = new List<int>();
        foreach (var firstName in new[] { "Alex", "Blake", "Casey", "Drew", "Evan", "Flynn" })
        {
            var result = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Migration",
                    IsActive = true,
                },
                sourceTeam.Id);
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    "Could not seed a device-migration Player.");
            playerIds.Add(result.PlayerId);
        }

        await using (var operation = await database.BeginOperationAsync())
        {
            var connection = operation.Connection;
            EnsureSingleRow(await connection.InsertAsync(currentTeam),
                "device-migration current Team");
            EnsureSingleRow(await connection.InsertAsync(finalizedTeam),
                "device-migration finalized Team");
            for (var index = 0; index < playerIds.Count; index++)
            {
                EnsureSingleRow(await connection.InsertAsync(new TeamPlayer
                {
                    TeamId = currentTeam.Id,
                    PlayerId = playerIds[index],
                    JerseyNumber = (index + 1) * 11,
                    IsActive = true,
                }), "device-migration current roster Player");
            }
        }

        var completedGames = await gameRepository.CreateForTeamAsync(
            sourceTeam.Id,
            new Season { Name = "Spring Migration", Year = sourceTeam.Year },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 5, 12),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Completed Migration Opponent",
                Field = "History Field",
            }]);
        var currentGames = await gameRepository.CreateForTeamAsync(
            currentTeam.Id,
            new Season { Name = "Summer Migration", Year = currentTeam.Year },
            [new Game
            {
                ScheduledDate = new DateTime(2027, 7, 20),
                ScheduledTime = new TimeSpan(19, 0, 0),
                Opponent = "Resume Migration Opponent",
                Field = "Migration Field",
            }]);
        if (!completedGames.IsCompleteSuccess || !currentGames.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed device-migration Seasons and Games.");

        await using (var operation = await database.BeginOperationAsync())
        {
            var connection = operation.Connection;
            var completedSession = new GameScoringSession
            {
                GameId = completedGames.CreatedGameIds.Single(),
                Status = GameScoringStatus.Completed,
                TeamSide = TeamGameSide.Home,
                HomeScore = 8,
                VisitorScore = 3,
                CurrentBatterPosition = 1,
                CurrentInning = 7,
                CompletedAtUtc = new DateTime(2026, 5, 13, 1, 0, 0, DateTimeKind.Utc),
            };
            EnsureSingleRow(await connection.InsertAsync(completedSession),
                "device-migration completed scoring session");
            for (var index = 0; index < playerIds.Count; index++)
            {
                EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
                {
                    GameScoringSessionId = completedSession.Id,
                    PlayerId = playerIds[index],
                    BattingPosition = index + 1,
                }), "device-migration completed frozen lineup Player");
                EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
                {
                    GameScoringSessionId = completedSession.Id,
                    PlayerId = playerIds[index],
                    BattingPosition = index + 1,
                    PlateAppearanceNumber = index + 1,
                    Inning = 1,
                    Result = index switch
                    {
                        0 => BatterResult.Single,
                        1 => BatterResult.Double,
                        2 => BatterResult.Walk,
                        _ => BatterResult.Out,
                    },
                }), "device-migration completed plate appearance");
            }

            var currentSession = new GameScoringSession
            {
                GameId = currentGames.CreatedGameIds.Single(),
                Status = GameScoringStatus.InProgress,
                TeamSide = TeamGameSide.Away,
                HomeScore = 2,
                VisitorScore = 4,
                CurrentBatterPosition = 4,
                CurrentInning = 3,
                CurrentOuts = 1,
            };
            EnsureSingleRow(await connection.InsertAsync(currentSession),
                "device-migration in-progress scoring session");
            for (var index = 0; index < playerIds.Count; index++)
            {
                EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
                {
                    GameScoringSessionId = currentSession.Id,
                    PlayerId = playerIds[index],
                    BattingPosition = index + 1,
                }), "device-migration in-progress frozen lineup Player");
            }
            EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = currentSession.Id,
                PlayerId = playerIds[0],
                BattingPosition = 1,
                PlateAppearanceNumber = 1,
                Inning = 3,
                Result = BatterResult.Single,
            }), "device-migration committed first plate appearance");
            EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = currentSession.Id,
                PlayerId = playerIds[1],
                BattingPosition = 2,
                PlateAppearanceNumber = 2,
                Inning = 3,
                Result = BatterResult.Single,
            }), "device-migration committed second plate appearance");
            EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = currentSession.Id,
                PlayerId = playerIds[2],
                BattingPosition = 3,
                PlateAppearanceNumber = 3,
                Inning = 3,
                Result = BatterResult.Out,
            }), "device-migration committed third plate appearance");
            EnsureSingleRow(await connection.InsertAsync(new GameScoringBaseRunner
            {
                GameScoringSessionId = currentSession.Id,
                PlayerId = playerIds[1],
                Base = ScoringBase.First,
            }), "device-migration first-base runner");
            EnsureSingleRow(await connection.InsertAsync(new GameScoringBaseRunner
            {
                GameScoringSessionId = currentSession.Id,
                PlayerId = playerIds[0],
                Base = ScoringBase.Second,
            }), "device-migration second-base runner");

            var beerRows = new[]
            {
                new BeerLedgerEntry
                {
                    TeamId = currentTeam.Id, PlayerId = playerIds[0],
                    SourceTeamId = sourceTeam.Id,
                    Origin = BeerLedgerEntryOrigin.CarryForward, Amount = 3,
                },
                new BeerLedgerEntry
                {
                    TeamId = currentTeam.Id, PlayerId = playerIds[0],
                    RuleType = BeerRuleType.Dbo,
                    Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                    RuleCountAdjustment = 1, Amount = 1,
                },
                new BeerLedgerEntry
                {
                    TeamId = currentTeam.Id, PlayerId = playerIds[0],
                    RuleType = BeerRuleType.LowMan,
                    Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                    PlayingDate = new DateTime(2027, 7, 20),
                    Amount = 2,
                },
                new BeerLedgerEntry
                {
                    TeamId = currentTeam.Id, PlayerId = playerIds[0],
                    Origin = BeerLedgerEntryOrigin.BroughtCredit, Amount = 2,
                },
                new BeerLedgerEntry
                {
                    TeamId = currentTeam.Id, PlayerId = playerIds[1],
                    SourceTeamId = sourceTeam.Id,
                    Origin = BeerLedgerEntryOrigin.CarryForward, Amount = -2,
                },
            };
            foreach (var beerRow in beerRows)
                EnsureSingleRow(await connection.InsertAsync(beerRow),
                    "device-migration Beer ledger row");
        }

        return new AndroidUiTestContext(currentTeam.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedBeerTotalsAsync()
    {
        var team = new Team { Name = "Automation Beer Team", IsActive = true };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "Beer-totals team");
        var playerIds = new List<int>();
        foreach (var firstName in new[] { "Alex", "Blake" })
        {
            var player = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Automation",
                    IsActive = true,
                },
                team.Id);
            if (!player.IsSuccess)
                throw new InvalidOperationException("Could not seed a Beer-totals Player.");
            playerIds.Add(player.PlayerId);

            if (firstName == "Alex")
            {
                EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
                    team.Id, player.PlayerId, BeerRuleType.TriplePlay, 1));
                EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
                    team.Id, player.PlayerId, BeerRuleType.LowMan, 2));
                EnsureBeerUpdate(await beerAccountingRepository.SetBroughtAsync(
                    team.Id, player.PlayerId, 5));
            }
        }

        await SeedBeerTotalsLowManGameAsync(team.Id, playerIds);

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedTeamYearSummaryAsync()
    {
        var team = new Team
        {
            Name = "Automation Year Summary",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "Team-year-summary Team");

        var playerIds = new List<int>();
        foreach (var firstName in new[] { "Alex", "Blake" })
        {
            var player = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Automation",
                    IsActive = true,
                },
                team.Id);
            if (!player.IsSuccess)
                throw new InvalidOperationException(
                    "Could not seed a Team-year-summary Player.");
            playerIds.Add(player.PlayerId);
        }

        var springGames = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Spring", Year = team.Year },
            [
                new Game
                {
                    ScheduledDate = new DateTime(2026, 4, 1),
                    ScheduledTime = new TimeSpan(18, 30, 0),
                    Opponent = "Automation Win",
                },
                new Game
                {
                    ScheduledDate = new DateTime(2026, 4, 8),
                    ScheduledTime = new TimeSpan(18, 30, 0),
                    Opponent = "Automation Loss",
                },
            ]);
        var summerGames = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Summer", Year = team.Year },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 7, 1),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Automation Tie",
            }]);
        if (!springGames.IsCompleteSuccess || !summerGames.IsCompleteSuccess)
            throw new InvalidOperationException(
                "Could not seed Team-year-summary Games.");

        var games = springGames.CreatedGameIds
            .Concat(summerGames.CreatedGameIds)
            .ToArray();
        var alexResults = new[]
        {
            BatterResult.Single,
            BatterResult.HomeRun,
            BatterResult.Walk,
        };
        var scores = new[] { (8, 3), (2, 5), (6, 6) };
        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
            for (var index = 0; index < games.Length; index++)
            {
                var session = new GameScoringSession
                {
                    GameId = games[index],
                    Status = GameScoringStatus.Completed,
                    TeamSide = TeamGameSide.Home,
                    HomeScore = scores[index].Item1,
                    VisitorScore = scores[index].Item2,
                    CurrentBatterPosition = 1,
                    CurrentInning = 1,
                    CompletedAtUtc = DateTime.UtcNow,
                };
                EnsureSingleRow(await connection.InsertAsync(session),
                    "Team-year-summary scoring session");
                for (var playerIndex = 0; playerIndex < playerIds.Count; playerIndex++)
                {
                    EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
                    {
                        GameScoringSessionId = session.Id,
                        PlayerId = playerIds[playerIndex],
                        BattingPosition = playerIndex + 1,
                    }), "Team-year-summary frozen lineup Player");
                    EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
                    {
                        GameScoringSessionId = session.Id,
                        PlayerId = playerIds[playerIndex],
                        BattingPosition = playerIndex + 1,
                        PlateAppearanceNumber = playerIndex + 1,
                        Inning = 1,
                        Result = playerIndex == 0
                            ? alexResults[index]
                            : BatterResult.Out,
                    }), "Team-year-summary plate appearance");
                }
            }

            EnsureSingleRow(await connection.InsertAsync(new Team
            {
                Name = team.Name,
                Year = team.Year + 1,
                TeamLineageId = team.TeamLineageId,
                PreviousTeamId = team.Id,
                IsActive = true,
            }), "Team-year-summary successor Team");

        }
        EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
            team.Id, playerIds[0], BeerRuleType.Dbo, 1));
        EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
            team.Id, playerIds[0], BeerRuleType.TriplePlay, 2));
        EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
            team.Id, playerIds[0], BeerRuleType.Strikeout, 3));
        EnsureBeerUpdate(await beerAccountingRepository.SetEffectiveRuleCountAsync(
            team.Id, playerIds[0], BeerRuleType.LowMan, 4));
        EnsureBeerUpdate(await beerAccountingRepository.SetBroughtAsync(
            team.Id, playerIds[0], 2));

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedTeamYearCopyAsync()
    {
        var team = new Team
        {
            Name = "Automation Copy Team",
            Year = 2026,
            IsActive = true,
        };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "Team-year-copy source Team");

        var rosterPlayers = new[]
        {
            (FirstName: "Mike", LastName: "Current", JerseyNumber: 12),
            (FirstName: "Zulu", LastName: "Current", JerseyNumber: 34),
        };
        foreach (var playerData in rosterPlayers)
        {
            var result = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = playerData.FirstName,
                    LastName = playerData.LastName,
                    IsActive = true,
                },
                team.Id);
            if (!result.IsSuccess)
                throw new InvalidOperationException(
                    "Could not seed a Team-year-copy roster Player.");
        }

        var nonRosterPlayers = new[]
        {
            new Player { FirstName = "Aaron", LastName = "Owed", IsActive = false },
            new Player { FirstName = "Beta", LastName = "Credit", IsActive = true },
            new Player { FirstName = "Zero", LastName = "Balance", IsActive = false },
        };
        foreach (var player in nonRosterPlayers)
            EnsureSingleRow(await playerRepository.CreateAsync(player),
                "Team-year-copy non-roster Player");

        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
            var memberships = await connection.Table<TeamPlayer>()
                .Where(item => item.TeamId == team.Id)
                .OrderBy(item => item.Id)
                .ToListAsync();
            for (var index = 0; index < memberships.Count; index++)
            {
                memberships[index].JerseyNumber = rosterPlayers[index].JerseyNumber;
                EnsureSingleRow(await connection.UpdateAsync(memberships[index]),
                    "Team-year-copy jersey number");
            }

            var spring = new Season { TeamId = team.Id, Name = "Spring", Year = 2026 };
            var summer = new Season { TeamId = team.Id, Name = "Summer", Year = 2026 };
            var legacy = new Season { TeamId = team.Id, Name = "Legacy", Year = 2025 };
            EnsureSingleRow(await connection.InsertAsync(spring), "Team-year-copy Spring Season");
            EnsureSingleRow(await connection.InsertAsync(summer), "Team-year-copy Summer Season");
            EnsureSingleRow(await connection.InsertAsync(legacy), "Team-year-copy legacy Season");
            EnsureSingleRow(await connection.InsertAsync(new Game
            {
                TeamId = team.Id,
                SeasonId = spring.Id,
                ScheduledDate = new DateTime(2026, 5, 4),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Historical Opponent",
            }), "Team-year-copy historical Game");

            foreach (var setting in BeerRuleConfigurationDefaults.TeamRules)
            {
                EnsureSingleRow(await connection.InsertAsync(new TeamBeerRuleConfiguration
                {
                    TeamId = team.Id,
                    RuleType = setting.RuleType,
                    IsEnabled = setting.RuleType != BeerRuleType.Dbo,
                    Amount = setting.RuleType == BeerRuleType.Strikeout ? 4 : setting.Amount,
                }), "Team-year-copy Beer Rule configuration");
            }

            EnsureSingleRow(await connection.InsertAsync(new BeerLedgerEntry
            {
                TeamId = team.Id,
                PlayerId = nonRosterPlayers[0].Id,
                RuleType = BeerRuleType.Dbo,
                Origin = BeerLedgerEntryOrigin.ManualAdjustment,
                RuleCountAdjustment = 3,
                Amount = 3,
            }), "Team-year-copy positive Beer balance");
            EnsureSingleRow(await connection.InsertAsync(new BeerLedgerEntry
            {
                TeamId = team.Id,
                PlayerId = nonRosterPlayers[1].Id,
                Origin = BeerLedgerEntryOrigin.BroughtCredit,
                Amount = 2,
            }), "Team-year-copy negative Beer balance");

        }

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<AndroidUiTestContext> SeedTeamStatsAsync()
    {
        var team = new Team { Name = "Automation Stats Team", IsActive = true };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "Team-stats team");

        var playerIds = new List<int>();
        foreach (var firstName in new[] { "Alex", "Blake" })
        {
            var player = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Automation",
                    IsActive = true,
                },
                team.Id);
            if (!player.IsSuccess)
                throw new InvalidOperationException("Could not seed a Team-stats Player.");
            playerIds.Add(player.PlayerId);
        }

        var springGameIds = await CreateTeamStatsGamesAsync(
            team.Id,
            "Spring",
            [
                (new DateTime(2026, 4, 1), new TimeSpan(18, 30, 0), "Spring One"),
                (new DateTime(2026, 4, 8), new TimeSpan(19, 0, 0), "Spring Two"),
            ]);
        var summerGameIds = await CreateTeamStatsGamesAsync(
            team.Id,
            "Summer 1",
            [
                (new DateTime(2026, 7, 1), new TimeSpan(18, 0, 0), "Summer One"),
                (new DateTime(2026, 7, 8), new TimeSpan(18, 30, 0), "Summer Two"),
                (new DateTime(2026, 7, 15), new TimeSpan(19, 0, 0), "Summer Three"),
            ]);
        _ = await CreateTeamStatsGamesAsync(
            team.Id,
            "Summer 2",
            [(new DateTime(2026, 8, 1), new TimeSpan(18, 30, 0), "Future Summer")]);
        var fallGameId = (await CreateTeamStatsGamesAsync(
            team.Id,
            "Fall",
            [(new DateTime(2026, 9, 1), new TimeSpan(18, 30, 0), "In Progress Fall")]))[0];

        var completedGameIds = springGameIds.Concat(summerGameIds).ToArray();
        var alexResults = new[]
        {
            BatterResult.Single,
            BatterResult.Double,
            BatterResult.Triple,
            BatterResult.HomeRun,
            BatterResult.Walk,
        };
        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
            for (var index = 0; index < completedGameIds.Length; index++)
            {
                await InsertTeamStatsSessionAsync(
                    connection,
                    completedGameIds[index],
                    playerIds,
                    GameScoringStatus.Completed,
                    alexResults[index]);
            }
            await InsertTeamStatsSessionAsync(
                connection,
                fallGameId,
                playerIds,
                GameScoringStatus.InProgress,
                BatterResult.HomeRun);

        }

        return new AndroidUiTestContext(team.Id, 0, true);
    }

    private async Task<IReadOnlyList<int>> CreateTeamStatsGamesAsync(
        int teamId,
        string leagueSeasonName,
        IReadOnlyList<(DateTime Date, TimeSpan Time, string Opponent)> games)
    {
        var createdIds = new List<int>(games.Count);
        foreach (var batch in games.Chunk(2))
        {
            var result = await gameRepository.CreateForTeamAsync(
                teamId,
                new Season { Name = leagueSeasonName, Year = 2026 },
                batch.Select(game => new Game
                {
                    ScheduledDate = game.Date,
                    ScheduledTime = game.Time,
                    Opponent = game.Opponent,
                }).ToArray());
            if (!result.IsCompleteSuccess)
                throw new InvalidOperationException(
                    $"Could not seed Android Team-stats {leagueSeasonName} Games."
                    + $" Failure: {result.FailureReason}.");
            createdIds.AddRange(result.CreatedGameIds);
        }
        return createdIds;
    }

    private static async Task InsertTeamStatsSessionAsync(
        SQLiteAsyncConnection connection,
        int gameId,
        IReadOnlyList<int> playerIds,
        GameScoringStatus status,
        BatterResult firstPlayerResult)
    {
        var session = new GameScoringSession
        {
            GameId = gameId,
            Status = status,
            TeamSide = TeamGameSide.Home,
            CurrentBatterPosition = 1,
            CurrentInning = 1,
            CompletedAtUtc = status == GameScoringStatus.Completed ? DateTime.UtcNow : null,
        };
        EnsureSingleRow(await connection.InsertAsync(session), "Team-stats scoring session");
        for (var index = 0; index < playerIds.Count; index++)
        {
            EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id,
                PlayerId = playerIds[index],
                BattingPosition = index + 1,
            }), "Team-stats frozen lineup Player");
            EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = session.Id,
                PlayerId = playerIds[index],
                BattingPosition = index + 1,
                PlateAppearanceNumber = index + 1,
                Inning = 1,
                Result = index == 0 ? firstPlayerResult : BatterResult.Out,
            }), "Team-stats plate appearance");
        }
    }

    private async Task SeedBeerTotalsLowManGameAsync(
        int teamId,
        IReadOnlyList<int> playerIds)
    {
        var gameResult = await gameRepository.CreateForTeamAsync(
            teamId,
            new Season { Name = "Automation", Year = 2026 },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 8, 15),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Automation Low Man Opponent",
            }]);
        if (!gameResult.IsCompleteSuccess)
            throw new InvalidOperationException("Could not seed the Low Man Game Night.");

        var gameId = gameResult.CreatedGameIds.Single();
        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
            var session = new GameScoringSession
            {
                GameId = gameId,
                Status = GameScoringStatus.Completed,
                TeamSide = TeamGameSide.Home,
                CurrentBatterPosition = 1,
                CurrentInning = 1,
                CompletedAtUtc = DateTime.UtcNow,
            };
            EnsureSingleRow(await connection.InsertAsync(session),
                "Low Man completed scoring session");
            for (var index = 0; index < playerIds.Count; index++)
            {
                EnsureSingleRow(await connection.InsertAsync(new GameScoringLineupPlayer
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = playerIds[index],
                    BattingPosition = index + 1,
                }), "Low Man frozen lineup Player");
                EnsureSingleRow(await connection.InsertAsync(new GamePlateAppearance
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = playerIds[index],
                    BattingPosition = index + 1,
                    PlateAppearanceNumber = index + 1,
                    Inning = 1,
                    Result = index == 0 ? BatterResult.Out : BatterResult.Single,
                }), "Low Man plate appearance");
            }
        }
    }

    private static void EnsureBeerUpdate(BeerAccountingUpdateResult result)
    {
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"Could not seed Beer totals: {result.FailureReason}.");
    }

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

        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
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
        {
            await using var operation = await database.BeginOperationAsync();
            var connection = operation.Connection;
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
