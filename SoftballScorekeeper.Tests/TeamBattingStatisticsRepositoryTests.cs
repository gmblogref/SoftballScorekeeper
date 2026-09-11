using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class TeamBattingStatisticsRepositoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Last_game_night_includes_every_completed_game_on_latest_date(int gameCount)
    {
        var earlier = CompletedGame(1, new(2026, 6, 1), 18);
        var latest = Enumerable.Range(0, gameCount)
            .Select(index => CompletedGame(index + 2, new(2026, 6, 8), 10 + index))
            .ToArray();

        var selected = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.LastGameNight, latest.Prepend(earlier).ToArray());

        Assert.Equal(gameCount, selected.Count);
        Assert.DoesNotContain(earlier.GameId, selected);
    }

    [Fact]
    public async Task All_games_uses_every_completed_game_owned_by_the_team_year()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Team");
        var player = await PlayerAsync(db, "Historical");
        var old = await SeasonAsync(db, team.Id, "Fall", 2025);
        var spring = await SeasonAsync(db, team.Id, "Spring", 2026);
        var summer = await SeasonAsync(db, team.Id, "Summer", 2026);
        await GameAsync(db, team.Id, old.Id, new(2025, 9, 1), GameScoringStatus.Completed,
            player, BatterResult.HomeRun);
        await GameAsync(db, team.Id, spring.Id, new(2026, 5, 1), GameScoringStatus.Completed,
            player, BatterResult.Single);
        await GameAsync(db, team.Id, summer.Id, new(2026, 7, 1), GameScoringStatus.Completed,
            player, BatterResult.Out);
        await GameAsync(db, team.Id, summer.Id, new(2026, 7, 2), GameScoringStatus.InProgress,
            player, BatterResult.HomeRun);
        await GameAsync(db, team.Id, summer.Id, new(2026, 7, 3), null, player, BatterResult.HomeRun);
        var other = await TeamAsync(db, "Other");
        var otherSeason = await SeasonAsync(db, other.Id, "Summer", 2026);
        await GameAsync(db, other.Id, otherSeason.Id, new(2026, 7, 4),
            GameScoringStatus.Completed, player, BatterResult.HomeRun);
        await db.ExecuteAsync("UPDATE Player SET IsActive = 0 WHERE Id = ?", player.Id);

        var projection = await db.CreateTeamBattingStatisticsRepository().GetAsync(team.Id);

        Assert.NotNull(projection);
        Assert.Equal(2, projection.CompletedGames.Count);
        var stats = Assert.Single(projection.Players).BattingStatistics;
        Assert.Equal((2, 2, 1, 0),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.HomeRuns));
        var teamTotal = BattingStatisticsCalculator.CalculateTeam(
            projection.Players.Select(item => item.BattingStatistics));
        Assert.Equal((2, 1), (teamTotal.PlateAppearances, teamTotal.Hits));
    }

    [Fact]
    public async Task Last_game_night_tournament_aggregates_every_completed_game_on_latest_date()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Tournament");
        var season = await SeasonAsync(db, team.Id, "Summer", 2026);
        var player = await PlayerAsync(db, "Player");
        await GameAsync(db, team.Id, season.Id, new(2026, 6, 1),
            GameScoringStatus.Completed, player, BatterResult.Out, 18);
        await GameAsync(db, team.Id, season.Id, new(2026, 6, 8),
            GameScoringStatus.Completed, player, BatterResult.Single, 10);
        await GameAsync(db, team.Id, season.Id, new(2026, 6, 8),
            GameScoringStatus.Completed, player, BatterResult.Double, 12);
        await GameAsync(db, team.Id, season.Id, new(2026, 6, 8),
            GameScoringStatus.Completed, player, BatterResult.Triple, 14);
        var repository = db.CreateTeamBattingStatisticsRepository();
        var all = (await repository.GetAsync(team.Id))!;

        var nightIds = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.LastGameNight, all.CompletedGames);
        var night = (await repository.GetAsync(team.Id, nightIds))!;

        Assert.Equal(3, nightIds.Count);
        var stats = Assert.Single(night.Players).BattingStatistics;
        Assert.Equal((3, 3, 1, 1, 1),
            (stats.PlateAppearances, stats.Hits, stats.Singles, stats.Doubles, stats.Triples));
    }

    [Fact]
    public async Task Custom_supports_cross_season_subset_and_valid_empty_selection()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Custom");
        var spring = await SeasonAsync(db, team.Id, "Spring", 2026);
        var fall = await SeasonAsync(db, team.Id, "Fall", 2026);
        var player = await PlayerAsync(db, "Player");
        var first = await GameAsync(db, team.Id, spring.Id, new(2026, 4, 1),
            GameScoringStatus.Completed, player, BatterResult.Single);
        await GameAsync(db, team.Id, spring.Id, new(2026, 4, 2),
            GameScoringStatus.Completed, player, BatterResult.Out);
        var third = await GameAsync(db, team.Id, fall.Id, new(2026, 9, 1),
            GameScoringStatus.Completed, player, BatterResult.HomeRun);
        var repository = db.CreateTeamBattingStatisticsRepository();
        var available = (await repository.GetAsync(team.Id))!.CompletedGames;
        var customIds = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.Custom, available, [first.Id, third.Id]);

        var custom = (await repository.GetAsync(team.Id, customIds))!;
        var empty = (await repository.GetAsync(team.Id, []))!;

        var stats = Assert.Single(custom.Players).BattingStatistics;
        Assert.Equal((2, 2, 2, 1),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.HomeRuns));
        Assert.Empty(empty.Players);
        Assert.Equal(3, empty.CompletedGames.Count);
    }

    [Fact]
    public async Task Current_league_season_uses_latest_completed_game_and_ignores_later_incomplete_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "Current League Season");
        var spring = await SeasonAsync(db, team.Id, "Spring", 2026);
        var summerOne = await SeasonAsync(db, team.Id, "Summer 1", 2026);
        var summerTwo = await SeasonAsync(db, team.Id, "Summer 2", 2026);
        var fall = await SeasonAsync(db, team.Id, "Fall", 2026);
        var player = await PlayerAsync(db, "Player");
        await GameAsync(db, team.Id, spring.Id, new(2026, 4, 1),
            GameScoringStatus.Completed, player, BatterResult.Out);
        await GameAsync(db, team.Id, summerOne.Id, new(2026, 6, 1),
            GameScoringStatus.Completed, player, BatterResult.Single);
        await GameAsync(db, team.Id, summerOne.Id, new(2026, 6, 8),
            GameScoringStatus.Completed, player, BatterResult.Double);
        await GameAsync(db, team.Id, summerTwo.Id, new(2026, 8, 1),
            null, player, BatterResult.HomeRun);
        await GameAsync(db, team.Id, fall.Id, new(2026, 9, 1),
            GameScoringStatus.InProgress, player, BatterResult.HomeRun);
        var repository = db.CreateTeamBattingStatisticsRepository();

        var beforeFallCompletion = (await repository.GetAsync(team.Id))!;
        var currentSummerIds = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.CurrentLeagueSeason,
            beforeFallCompletion.CompletedGames);
        var currentSummer = (await repository.GetAsync(team.Id, currentSummerIds))!;

        Assert.Equal(3, beforeFallCompletion.CompletedGames.Count);
        Assert.Equal(2, currentSummerIds.Count);
        var summerStats = Assert.Single(currentSummer.Players).BattingStatistics;
        Assert.Equal((2, 2, 3),
            (summerStats.PlateAppearances, summerStats.Hits, summerStats.TotalBases));

        var completedFall = await GameAsync(db, team.Id, fall.Id, new(2026, 9, 8),
            GameScoringStatus.Completed, player, BatterResult.HomeRun);
        var afterFallCompletion = (await repository.GetAsync(team.Id))!;
        var currentFallIds = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.CurrentLeagueSeason,
            afterFallCompletion.CompletedGames);

        Assert.Equal([completedFall.Id], currentFallIds);
        Assert.Equal(4, TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.AllGames, afterFallCompletion.CompletedGames).Count);
    }

    [Fact]
    public async Task Current_league_season_is_empty_when_team_year_has_no_completed_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await TeamAsync(db, "No Completed Games");
        var spring = await SeasonAsync(db, team.Id, "Spring", 2026);
        var player = await PlayerAsync(db, "Player");
        await GameAsync(db, team.Id, spring.Id, new(2026, 4, 1),
            null, player, BatterResult.Out);
        await GameAsync(db, team.Id, spring.Id, new(2026, 4, 2),
            GameScoringStatus.InProgress, player, BatterResult.Out);

        var projection = (await db.CreateTeamBattingStatisticsRepository()
            .GetAsync(team.Id))!;
        var selected = TeamStatsGameSelection.Resolve(
            TeamStatsGameFilter.CurrentLeagueSeason, projection.CompletedGames);

        Assert.Empty(projection.CompletedGames);
        Assert.Empty(selected);
        Assert.Empty((await db.CreateTeamBattingStatisticsRepository()
            .GetAsync(team.Id, selected))!.Players);
    }

    private static async Task<Team> TeamAsync(TemporaryTestDatabase db, string name)
    {
        var team = new Team { Name = name, IsActive = true };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static TeamCompletedGame CompletedGame(int id, DateTime date, int hour) =>
        new(id, 1, id, date, TimeSpan.FromHours(hour), "Opponent", "Summer", 2026);

    private static async Task<Season> SeasonAsync(
        TemporaryTestDatabase db, int teamId, string name, int year)
    {
        var season = new Season { TeamId = teamId, Name = name, Year = year };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        return season;
    }

    private static async Task<Player> PlayerAsync(TemporaryTestDatabase db, string name)
    {
        var player = new Player { FirstName = name, IsActive = true };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        return player;
    }

    private static async Task<Game> GameAsync(
        TemporaryTestDatabase db,
        int teamId,
        int seasonId,
        DateTime date,
        GameScoringStatus? status,
        Player player,
        BatterResult result,
        int hour = 18)
    {
        var game = new Game
        {
            TeamId = teamId,
            SeasonId = seasonId,
            ScheduledDate = date,
            ScheduledTime = TimeSpan.FromHours(hour),
            Opponent = $"Opponent {date:MMdd}",
        };
        Assert.Equal(1, await db.SeedGameAsync(game));
        if (status is null)
            return game;

        await db.UseConnectionAsync(async connection =>
        {
            var session = new GameScoringSession
            {
                GameId = game.Id,
                Status = status.Value,
                TeamSide = TeamGameSide.Home,
                CurrentBatterPosition = 1,
                CurrentInning = 1,
            };
            Assert.Equal(1, await connection.InsertAsync(session));
            Assert.Equal(1, await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id,
                PlayerId = player.Id,
                BattingPosition = 1,
            }));
            Assert.Equal(1, await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = session.Id,
                PlayerId = player.Id,
                BattingPosition = 1,
                PlateAppearanceNumber = 1,
                Inning = 1,
                Result = result,
            }));
            return 0;
        });
        return game;
    }
}
