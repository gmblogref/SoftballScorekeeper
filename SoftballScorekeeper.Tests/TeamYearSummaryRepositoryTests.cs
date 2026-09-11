using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Tests;

public sealed class TeamYearSummaryRepositoryTests
{
    [Fact]
    public async Task Result_uses_final_game_semantics_across_matching_seasons_only()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Year Results", 2026);
        var spring = await SeasonAsync(db, team.Id, "Spring", 2026);
        var summerOne = await SeasonAsync(db, team.Id, "Summer 1", 2026);
        var summerTwo = await SeasonAsync(db, team.Id, "Summer 2", 2026);
        var legacy = await SeasonAsync(db, team.Id, "Legacy", 2025);

        await GameAsync(db, team.Id, spring.Id, GameScoringStatus.Completed, 5, 3);
        await GameAsync(db, team.Id, summerOne.Id, GameScoringStatus.Completed, 2, 4);
        await GameAsync(db, team.Id, summerTwo.Id, GameScoringStatus.Completed, 3, 3);
        await GameAsync(db, team.Id, spring.Id, GameScoringStatus.InProgress, 8, 0);
        await GameAsync(db, team.Id, spring.Id, null, 0, 0);
        await GameAsync(db, team.Id, legacy.Id, GameScoringStatus.Completed, 9, 0);

        var summary = await db.CreateTeamYearSummaryRepository().GetAsync(team.Id);

        Assert.NotNull(summary);
        Assert.Equal((2026, 3, 1, 1, 1),
            (summary.Team.Year, summary.Record.GamesPlayed, summary.Record.Wins,
                summary.Record.Losses, summary.Record.Ties));
        Assert.Equal(3, summary.Batting.CompletedGames.Count);
    }

    [Fact]
    public async Task Summary_isolated_by_team_record_and_matches_all_games_stats()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var lineage = Guid.NewGuid();
        var prior = await TeamAsync(db, "Continuation", 2026, lineage);
        var current = await TeamAsync(db, "Continuation", 2027, lineage, prior.Id);
        var unrelated = await TeamAsync(db, "Other", 2027);
        var priorSeason = await SeasonAsync(db, prior.Id, "Spring", 2026);
        var currentSpring = await SeasonAsync(db, current.Id, "Spring", 2027);
        var currentSummer = await SeasonAsync(db, current.Id, "Summer", 2027);
        var unrelatedSeason = await SeasonAsync(db, unrelated.Id, "Spring", 2027);
        var player = await PlayerAsync(db, "Historical", "Hitter");

        await GameAsync(db, prior.Id, priorSeason.Id, GameScoringStatus.Completed, 1, 0,
            player, BatterResult.Out);
        await GameAsync(db, current.Id, currentSpring.Id, GameScoringStatus.Completed, 1, 0,
            player, BatterResult.Single);
        await GameAsync(db, current.Id, currentSummer.Id, GameScoringStatus.Completed, 0, 1,
            player, BatterResult.Walk);
        await GameAsync(db, unrelated.Id, unrelatedSeason.Id, GameScoringStatus.Completed, 8, 0,
            player, BatterResult.HomeRun);
        player.IsActive = false;
        await db.UseConnectionAsync(connection => connection.UpdateAsync(player));

        var summary = (await db.CreateTeamYearSummaryRepository().GetAsync(current.Id))!;
        var teamStats = (await db.CreateTeamBattingStatisticsRepository().GetAsync(current.Id))!;

        Assert.Equal([.. teamStats.CompletedGames.Select(item => item.GameId)],
            [.. summary.Batting.CompletedGames.Select(item => item.GameId)]);
        var line = Assert.Single(summary.Batting.Players).BattingStatistics;
        Assert.Equal(player.Id, line.PlayerId);
        Assert.Equal((2, 1, 1, 1),
            (line.PlateAppearances, line.AtBats, line.Hits, line.Walks));
        var teamTotal = BattingStatisticsCalculator.CalculateTeam(
            summary.Batting.Players.Select(item => item.BattingStatistics));
        Assert.Equal((2, 1, 1),
            (teamTotal.PlateAppearances, teamTotal.Hits, teamTotal.Walks));
        Assert.Equal((2, 1, 1, 0),
            (summary.Record.GamesPlayed, summary.Record.Wins,
                summary.Record.Losses, summary.Record.Ties));
    }

    [Fact]
    public async Task Beer_uses_authoritative_counts_brought_signed_carry_and_non_roster_rows()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Beer Summary", 2027);
        var rosterPlayer = await PlayerAsync(db, "Roster", "Player");
        var nonRosterPlayer = await PlayerAsync(db, "Balance", "Holder");
        Assert.Equal(1, await db.SeedTeamPlayerAsync(new TeamPlayer
        {
            TeamId = team.Id, PlayerId = rosterPlayer.Id, IsActive = true,
        }));

        foreach (var rule in Enum.GetValues<BeerRuleType>())
        {
            Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
            {
                TeamId = team.Id,
                PlayerId = rosterPlayer.Id,
                RuleType = rule,
                Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                Amount = 2,
                PlayingDate = rule == BeerRuleType.LowMan ? new DateTime(2027, 6, 1) : null,
            }));
        }
        Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
        {
            TeamId = team.Id,
            PlayerId = rosterPlayer.Id,
            Origin = BeerLedgerEntryOrigin.BroughtCredit,
            Amount = 3,
        }));
        Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
        {
            TeamId = team.Id,
            PlayerId = nonRosterPlayer.Id,
            Origin = BeerLedgerEntryOrigin.CarryForward,
            SourceTeamId = 42,
            Amount = -4,
        }));

        var summary = (await db.CreateTeamYearSummaryRepository().GetAsync(team.Id))!;

        Assert.Equal(2, summary.Beer.Count);
        var roster = summary.Beer[0];
        Assert.Equal((1, 1, 1, 1, 1, 1, 1),
            (roster.DboCount, roster.SoloHomeRunCount, roster.DoublePlayCount,
                roster.TriplePlayCount, roster.TwoOutsInOneInningCount,
                roster.StrikeoutCount, roster.LowManCount));
        Assert.Equal(3, roster.Brought);
        Assert.Equal(11, roster.Owed);
        var carried = summary.Beer[1];
        Assert.Equal(nonRosterPlayer.Id, carried.PlayerId);
        Assert.Equal((0, 0, 0, 0, 0, 0, 0, 0L, -4L),
            (carried.DboCount, carried.SoloHomeRunCount, carried.DoublePlayCount,
                carried.TriplePlayCount, carried.TwoOutsInOneInningCount,
                carried.StrikeoutCount, carried.LowManCount, carried.Brought, carried.Owed));
    }

    [Fact]
    public async Task Empty_and_carry_forward_only_teams_are_safe_and_reload_live_data()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var empty = await TeamAsync(db, "Empty", 2027);
        var emptySummary = (await db.CreateTeamYearSummaryRepository().GetAsync(empty.Id))!;
        Assert.Equal((0, 0, 0, 0),
            (emptySummary.Record.GamesPlayed, emptySummary.Record.Wins,
                emptySummary.Record.Losses, emptySummary.Record.Ties));
        Assert.Empty(emptySummary.Batting.Players);
        Assert.Empty(emptySummary.Beer);

        var player = await PlayerAsync(db, "Carry", "Only");
        Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
        {
            TeamId = empty.Id,
            PlayerId = player.Id,
            Origin = BeerLedgerEntryOrigin.CarryForward,
            SourceTeamId = 1,
            Amount = 5,
        }));
        var carried = (await db.CreateTeamYearSummaryRepository().GetAsync(empty.Id))!;
        Assert.Empty(carried.Batting.Players);
        Assert.Equal(5, Assert.Single(carried.Beer).Owed);

        var season = await SeasonAsync(db, empty.Id, "Spring", 2027);
        var (_, appearanceId) = await GameAsync(db, empty.Id, season.Id,
            GameScoringStatus.Completed, 1, 0, player, BatterResult.Out);
        var firstLoad = (await db.CreateTeamYearSummaryRepository().GetAsync(empty.Id))!;
        Assert.Equal(0, Assert.Single(firstLoad.Batting.Players).BattingStatistics.Hits);
        Assert.Equal(1, await db.ExecuteAsync(
            "UPDATE GamePlateAppearance SET Result = ? WHERE Id = ?",
            BatterResult.HomeRun, appearanceId));
        Assert.True((await db.CreateBeerAccountingRepository()
            .SetBroughtAsync(empty.Id, player.Id, 2)).IsSuccess);

        var reloaded = (await db.CreateTeamYearSummaryRepository().GetAsync(empty.Id))!;
        Assert.Equal(1, Assert.Single(reloaded.Batting.Players).BattingStatistics.HomeRuns);
        Assert.Equal(3, Assert.Single(reloaded.Beer).Owed);
    }

    private static async Task<Team> TeamAsync(
        TemporaryTestDatabase db,
        string name,
        int year,
        Guid? lineage = null,
        int? previousTeamId = null)
    {
        var team = new Team
        {
            Name = name,
            Year = year,
            TeamLineageId = lineage ?? Guid.NewGuid(),
            PreviousTeamId = previousTeamId,
            IsActive = true,
        };
        if (previousTeamId is null)
            Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        else
            Assert.Equal(1, await db.UseConnectionAsync(connection => connection.InsertAsync(team)));
        return team;
    }

    private static async Task<Season> SeasonAsync(
        TemporaryTestDatabase db, int teamId, string name, int year)
    {
        var season = new Season { TeamId = teamId, Name = name, Year = year };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        return season;
    }

    private static async Task<Player> PlayerAsync(
        TemporaryTestDatabase db, string firstName, string lastName)
    {
        var player = new Player
        {
            FirstName = firstName, LastName = lastName, IsActive = true,
        };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        return player;
    }

    private static async Task<(Game Game, int AppearanceId)> GameAsync(
        TemporaryTestDatabase db,
        int teamId,
        int seasonId,
        GameScoringStatus? status,
        int homeScore,
        int visitorScore,
        Player? player = null,
        BatterResult result = BatterResult.Out)
    {
        var game = new Game
        {
            TeamId = teamId,
            SeasonId = seasonId,
            ScheduledDate = new DateTime(2027, 6, 1),
            ScheduledTime = TimeSpan.FromHours(18),
            Opponent = "Opponent",
        };
        Assert.Equal(1, await db.SeedGameAsync(game));
        if (status is null)
            return (game, 0);

        var appearanceId = await db.UseConnectionAsync(async connection =>
        {
            var session = new GameScoringSession
            {
                GameId = game.Id,
                Status = status.Value,
                TeamSide = TeamGameSide.Home,
                HomeScore = homeScore,
                VisitorScore = visitorScore,
                CurrentBatterPosition = 1,
                CurrentInning = 1,
            };
            Assert.Equal(1, await connection.InsertAsync(session));
            if (player is null)
                return 0;
            Assert.Equal(1, await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id,
                PlayerId = player.Id,
                BattingPosition = 1,
            }));
            var appearance = new GamePlateAppearance
            {
                GameScoringSessionId = session.Id,
                PlayerId = player.Id,
                BattingPosition = 1,
                PlateAppearanceNumber = 1,
                Inning = 1,
                Result = result,
            };
            Assert.Equal(1, await connection.InsertAsync(appearance));
            return appearance.Id;
        });
        return (game, appearanceId);
    }
}
