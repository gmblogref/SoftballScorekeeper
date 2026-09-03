using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class TeamRecordRepositoryTests
{
    [Fact]
    public async Task Team_without_seasons_has_no_year_and_zero_record()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "No Seasons");

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(null, 0, 0), records[team.Id]);
    }

    [Fact]
    public async Task Latest_year_with_no_completed_games_has_zero_record()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "No Finals");
        var season = await CreateSeasonAsync(db, team.Id, "Spring", 2026);
        await CreateGameAsync(db, team.Id, season.Id);

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, 0, 0), records[team.Id]);
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 8, 3, 1, 0)]
    [InlineData(TeamGameSide.Home, 3, 8, 0, 1)]
    [InlineData(TeamGameSide.Away, 3, 8, 1, 0)]
    [InlineData(TeamGameSide.Away, 8, 3, 0, 1)]
    public async Task Completed_game_uses_team_side_for_win_or_loss(
        TeamGameSide side,
        int homeScore,
        int visitorScore,
        int expectedWins,
        int expectedLosses
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Perspective");
        var season = await CreateSeasonAsync(db, team.Id, "Summer", 2026);
        var game = await CreateGameAsync(db, team.Id, season.Id);
        await CreateSessionAsync(
            db,
            game.Id,
            GameScoringStatus.Completed,
            side,
            homeScore,
            visitorScore
        );

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, expectedWins, expectedLosses), records[team.Id]);
    }

    [Fact]
    public async Task Only_completed_games_affect_record_and_tie_affects_neither_total()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Lifecycle");
        var season = await CreateSeasonAsync(db, team.Id, "Fall", 2026);
        await CreateGameAsync(db, team.Id, season.Id);
        var inProgress = await CreateGameAsync(db, team.Id, season.Id);
        var win = await CreateGameAsync(db, team.Id, season.Id);
        var loss = await CreateGameAsync(db, team.Id, season.Id);
        var tie = await CreateGameAsync(db, team.Id, season.Id);
        await CreateSessionAsync(
            db,
            inProgress.Id,
            GameScoringStatus.InProgress,
            TeamGameSide.Home,
            99,
            0
        );
        await CreateSessionAsync(db, win.Id, GameScoringStatus.Completed, TeamGameSide.Home, 9, 2);
        await CreateSessionAsync(db, loss.Id, GameScoringStatus.Completed, TeamGameSide.Away, 9, 2);
        await CreateSessionAsync(db, tie.Id, GameScoringStatus.Completed, TeamGameSide.Home, 7, 7);

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, 1, 1), records[team.Id]);
    }

    [Fact]
    public async Task Latest_year_combines_all_its_seasons_and_excludes_older_year()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Multi Season");
        var oldSeason = await CreateSeasonAsync(db, team.Id, "Fall", 2025);
        var spring = await CreateSeasonAsync(db, team.Id, "Spring", 2026);
        var summer = await CreateSeasonAsync(db, team.Id, "Summer", 2026);
        var oldGame = await CreateGameAsync(db, team.Id, oldSeason.Id);
        var springWin = await CreateGameAsync(db, team.Id, spring.Id);
        var summerWin = await CreateGameAsync(db, team.Id, summer.Id);
        var summerLoss = await CreateGameAsync(db, team.Id, summer.Id);
        await CreateSessionAsync(
            db,
            oldGame.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            1,
            10
        );
        await CreateSessionAsync(
            db,
            springWin.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            10,
            1
        );
        await CreateSessionAsync(
            db,
            summerWin.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Away,
            1,
            10
        );
        await CreateSessionAsync(
            db,
            summerLoss.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            1,
            10
        );

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, 2, 1), records[team.Id]);
    }

    [Fact]
    public async Task Newer_season_changes_card_year_even_before_it_has_completed_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "New Year");
        var oldSeason = await CreateSeasonAsync(db, team.Id, "Fall", 2025);
        var oldGame = await CreateGameAsync(db, team.Id, oldSeason.Id);
        await CreateSessionAsync(
            db,
            oldGame.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            8,
            2
        );
        await CreateSeasonAsync(db, team.Id, "Spring", 2026);

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, 0, 0), records[team.Id]);
    }

    [Fact]
    public async Task Mixed_latest_year_history_aggregates_across_seasons()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Mixed History");
        var spring = await CreateSeasonAsync(db, team.Id, "Spring", 2026);
        var summer = await CreateSeasonAsync(db, team.Id, "Summer", 2026);
        await CreateGameAsync(db, team.Id, spring.Id);
        var inProgress = await CreateGameAsync(db, team.Id, spring.Id);
        var win = await CreateGameAsync(db, team.Id, spring.Id);
        var loss = await CreateGameAsync(db, team.Id, summer.Id);
        var tie = await CreateGameAsync(db, team.Id, summer.Id);
        await CreateSessionAsync(
            db,
            inProgress.Id,
            GameScoringStatus.InProgress,
            TeamGameSide.Home,
            20,
            1
        );
        await CreateSessionAsync(db, win.Id, GameScoringStatus.Completed, TeamGameSide.Home, 9, 3);
        await CreateSessionAsync(db, loss.Id, GameScoringStatus.Completed, TeamGameSide.Away, 9, 3);
        await CreateSessionAsync(db, tie.Id, GameScoringStatus.Completed, TeamGameSide.Away, 6, 6);

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        Assert.Equal(new TeamYearRecord(2026, 1, 1), records[team.Id]);
    }

    [Fact]
    public async Task Batched_records_keep_teams_isolated()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateTeamAsync(db, "First");
        var second = await CreateTeamAsync(db, "Second");
        var firstSeason = await CreateSeasonAsync(db, first.Id, "Spring", 2026);
        var secondSeason = await CreateSeasonAsync(db, second.Id, "Fall", 2025);
        var firstGame = await CreateGameAsync(db, first.Id, firstSeason.Id);
        var secondGame = await CreateGameAsync(db, second.Id, secondSeason.Id);
        await CreateSessionAsync(
            db,
            firstGame.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            6,
            2
        );
        await CreateSessionAsync(
            db,
            secondGame.Id,
            GameScoringStatus.Completed,
            TeamGameSide.Home,
            2,
            6
        );

        var records = await db.CreateTeamRecordRepository().GetForTeamsAsync([first.Id, second.Id]);

        Assert.Equal(new TeamYearRecord(2026, 1, 0), records[first.Id]);
        Assert.Equal(new TeamYearRecord(2025, 0, 1), records[second.Id]);
    }

    [Fact]
    public async Task Database_reopen_regenerates_same_derived_record()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Reload");
        var season = await CreateSeasonAsync(db, team.Id, "Summer", 2026);
        var game = await CreateGameAsync(db, team.Id, season.Id);
        await CreateSessionAsync(db, game.Id, GameScoringStatus.Completed, TeamGameSide.Away, 3, 8);
        var before = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);

        await db.Database.CloseAsync();

        var after = await db.CreateTeamRecordRepository().GetForTeamsAsync([team.Id]);
        Assert.Equal(before[team.Id], after[team.Id]);
        Assert.Equal(new TeamYearRecord(2026, 1, 0), after[team.Id]);
    }

    private static async Task<Team> CreateTeamAsync(TemporaryTestDatabase db, string name)
    {
        var team = new Team { Name = name };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<Season> CreateSeasonAsync(
        TemporaryTestDatabase db,
        int teamId,
        string name,
        int year
    )
    {
        var season = new Season
        {
            TeamId = teamId,
            Name = name,
            Year = year,
        };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        return season;
    }

    private static async Task<Game> CreateGameAsync(
        TemporaryTestDatabase db,
        int teamId,
        int seasonId
    )
    {
        var game = new Game
        {
            TeamId = teamId,
            SeasonId = seasonId,
            ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18),
        };
        Assert.Equal(1, await db.SeedGameAsync(game));
        return game;
    }

    private static async Task CreateSessionAsync(
        TemporaryTestDatabase db,
        int gameId,
        GameScoringStatus status,
        TeamGameSide side,
        int homeScore,
        int visitorScore
    )
    {
        var session = new GameScoringSession
        {
            GameId = gameId,
            Status = status,
            TeamSide = side,
            HomeScore = homeScore,
            VisitorScore = visitorScore,
            CurrentBatterPosition = 1,
            CurrentInning = 1,
        };
        Assert.Equal(1, await db.UseConnectionAsync(connection => connection.InsertAsync(session)));
    }
}
