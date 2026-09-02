using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class CompletedGameProjectionTests
{
    [Theory]
    [InlineData(TeamGameSide.Home, "Opponent Club", "Scored Team")]
    [InlineData(TeamGameSide.Away, "Scored Team", "Opponent Club")]
    public async Task Completed_projection_orients_score_and_projects_all_canonical_stats(
        TeamGameSide teamSide,
        string expectedVisitorName,
        string expectedHomeName)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, teamSide, 10, duplicateFinalNames: true);
        var repository = db.CreateGameScoringSessionRepository();
        var appearances = new List<GamePlateAppearance>();

        foreach (var result in new[]
        {
            BatterResult.Single,
            BatterResult.Double,
            BatterResult.Walk,
            BatterResult.SacrificeFly,
            BatterResult.FielderChoice,
            BatterResult.Out,
        })
            appearances.Add(await SeedAppearanceAsync(db, context,
                context.Players[0], result, appearances.Count + 1));
        for (var index = 0; index < 3; index++)
            appearances.Add(await SeedAppearanceAsync(db, context,
                context.Players[1], BatterResult.Out, appearances.Count + 1));
        foreach (var (player, result) in new[]
        {
            (context.Players[2], BatterResult.Walk),
            (context.Players[3], BatterResult.SacrificeFly),
            (context.Players[4], BatterResult.FielderChoice),
            (context.Players[5], BatterResult.DoublePlay),
            (context.Players[6], BatterResult.TriplePlay),
            (context.Players[8], BatterResult.Single),
            (context.Players[9], BatterResult.Out),
        })
            appearances.Add(await SeedAppearanceAsync(db, context,
                player, result, appearances.Count + 1));

        var extraPlayer = await CreatePlayerAsync(db, context.Team,
            "Extra", "Participant");
        appearances.Add(await SeedAppearanceAsync(db, context,
            extraPlayer, BatterResult.Triple, appearances.Count + 1,
            battingPosition: 11));
        await db.UseConnectionAsync(connection => connection.InsertAsync(
            new GamePlateAppearanceRunnerMovement
            {
                GamePlateAppearanceId = appearances[0].Id,
                PlayerId = context.Players[0].Id,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = RunnerDestination.Out,
                SafeDestination = RunnerDestination.First,
                AttemptedDestination = RunnerDestination.Second,
                OutOrder = 1,
            }));
        await db.CreatePlayerRepository().DeactivateAsync([context.Players[7].Id]);
        await db.UpdateScoringStateAsync(context.Session.Id,
            homeScore: 7, visitorScore: 4, currentBatterPosition: 1);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var projection = Assert.IsType<CompletedGameProjection>(
            await repository.GetCompletedGameAsync(context.Game.Id));

        Assert.Equal((context.Game.Id, context.Session.Id, GameScoringStatus.Completed,
                teamSide),
            (projection.GameId, projection.ScoringSessionId, projection.Status,
                projection.TeamSide));
        Assert.NotNull(projection.CompletedAtUtc);
        Assert.Equal((expectedVisitorName, 4, expectedHomeName, 7),
            (projection.VisitorName, projection.VisitorScore,
                projection.HomeName, projection.HomeScore));
        Assert.Equal(context.Players.Select(item => item.Id).Append(extraPlayer.Id),
            projection.Players.Select(item => item.PlayerId));

        var mixed = Find(projection, context.Players[0].Id);
        AssertLine(mixed, pa: 6, ab: 4, hits: 2, totalBases: 3,
            average: ".500", onBase: ".500", slugging: ".750");
        Assert.Equal((1, 1, 1, 1, 1),
            (mixed.Singles, mixed.Doubles, mixed.Walks,
                mixed.FielderChoices, mixed.SacrificeFlies));
        var zeroForThree = Find(projection, context.Players[1].Id);
        AssertLine(zeroForThree, 3, 3, 0, 0, ".000", ".000", ".000");
        AssertLine(Find(projection, context.Players[2].Id),
            1, 0, 0, 0, ".000", "1.000", ".000");
        AssertLine(Find(projection, context.Players[3].Id),
            1, 0, 0, 0, ".000", ".000", ".000");
        var fielderChoice = Find(projection, context.Players[4].Id);
        AssertLine(fielderChoice, 1, 1, 0, 0, ".000", ".000", ".000");
        Assert.Equal(1, fielderChoice.FielderChoices);
        AssertLine(Find(projection, context.Players[5].Id),
            1, 1, 0, 0, ".000", ".000", ".000");
        AssertLine(Find(projection, context.Players[6].Id),
            1, 1, 0, 0, ".000", ".000", ".000");
        AssertLine(Find(projection, context.Players[7].Id),
            0, 0, 0, 0, ".000", ".000", ".000");
        Assert.Equal("Same Name", projection.Players[8].DisplayName);
        Assert.Equal("Same Name", projection.Players[9].DisplayName);
        Assert.NotEqual(projection.Players[8].PlayerId, projection.Players[9].PlayerId);
        Assert.Equal(1, projection.Players[8].BattingStatistics.Hits);
        Assert.Equal(0, projection.Players[9].BattingStatistics.Hits);
        AssertLine(Find(projection, extraPlayer.Id),
            1, 1, 1, 3, "1.000", "1.000", "3.000");
    }

    [Fact]
    public async Task In_progress_game_has_no_completed_projection()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 1);

        Assert.Null(await db.CreateGameScoringSessionRepository()
            .GetCompletedGameAsync(context.Game.Id));
    }

    [Fact]
    public async Task Abandoned_redo_branch_is_absent_from_completed_projection()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 1);
        var repository = db.CreateGameScoringSessionRepository();
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Single)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var line = Find((await repository.GetCompletedGameAsync(context.Game.Id))!,
            context.Players[0].Id);

        AssertLine(line, 1, 1, 1, 4, "1.000", "1.000", "4.000");
        Assert.Equal((0, 1), (line.Singles, line.HomeRuns));
    }

    [Fact]
    public async Task Reconciled_fielder_choice_is_projected_as_ordinary_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await SeedBaseAsync(db, context.Session.Id, context.Players[1], ScoringBase.First);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.FielderChoice)).IsSuccess);
        Assert.True((await repository.SelectMultiOutRetirementsAsync(
            context.Game.Id,
            [new(context.Players[0].Id,
                RunnerOutcomeChoice.ForDestination(RunnerDestination.Out))])).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(BatterResult.Out,
            Assert.Single((await repository.GetStateForGameAsync(
                context.Game.Id))!.PlateAppearances).Result);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var line = Find((await repository.GetCompletedGameAsync(context.Game.Id))!,
            context.Players[0].Id);

        Assert.Equal(0, line.FielderChoices);
        AssertLine(line, 1, 1, 0, 0, ".000", ".000", ".000");
    }

    [Fact]
    public async Task Reconciled_sacrifice_fly_is_projected_as_ordinary_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 4);
        var repository = db.CreateGameScoringSessionRepository();
        await SeedBaseAsync(db, context.Session.Id, context.Players[1], ScoringBase.First);
        await SeedBaseAsync(db, context.Session.Id, context.Players[3], ScoringBase.Third);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[1].Id, RunnerDestination.Second);
        await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id,
            context.Players[1].Id,
            new(RunnerDestination.Out,
                RunnerDestination.Second, RunnerDestination.Third));
        Assert.True((await repository.AdjustPendingRunnerAsync(
            context.Game.Id, context.Players[3].Id, RunnerDestination.Third)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(BatterResult.Out,
            Assert.Single((await repository.GetStateForGameAsync(
                context.Game.Id))!.PlateAppearances).Result);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var line = Find((await repository.GetCompletedGameAsync(context.Game.Id))!,
            context.Players[0].Id);

        Assert.Equal(0, line.SacrificeFlies);
        AssertLine(line, 1, 1, 0, 0, ".000", ".000", ".000");
    }

    [Fact]
    public async Task Completed_projection_survives_database_close_and_repository_recreation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Away, 2);
        await SeedAppearanceAsync(db, context, context.Players[0],
            BatterResult.Double, 1);
        await SeedAppearanceAsync(db, context, context.Players[1],
            BatterResult.Walk, 2);
        await db.UpdateScoringStateAsync(context.Session.Id,
            homeScore: 9, visitorScore: 6, currentBatterPosition: 1);
        Assert.True((await db.CreateGameScoringSessionRepository()
            .CompleteGameAsync(context.Game.Id)).IsSuccess);
        var before = (await db.CreateGameScoringSessionRepository()
            .GetCompletedGameAsync(context.Game.Id))!;

        await db.Database.CloseAsync();
        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        try
        {
            var after = (await new GameScoringSessionRepository(reopenedDatabase)
                .GetCompletedGameAsync(context.Game.Id))!;

            Assert.Equal((before.VisitorName, before.VisitorScore,
                    before.HomeName, before.HomeScore),
                (after.VisitorName, after.VisitorScore,
                    after.HomeName, after.HomeScore));
            Assert.Equal(
                before.Players.Select(StatValues),
                after.Players.Select(StatValues));
        }
        finally
        {
            await reopenedDatabase.CloseAsync();
        }
    }

    [Fact]
    public async Task Missing_historical_player_identity_is_rejected_instead_of_dropped()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 1);
        await SeedAppearanceAsync(db, context, context.Players[0], BatterResult.Single, 1);
        var repository = db.CreateGameScoringSessionRepository();
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);
        await db.ExecuteAsync("DELETE FROM Player WHERE Id = ?", context.Players[0].Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetCompletedGameAsync(context.Game.Id));

        Assert.Contains(context.Players[0].Id.ToString(), exception.Message);
    }

    [Fact]
    public async Task Null_result_in_completed_canonical_history_is_rejected()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, TeamGameSide.Home, 1);
        await SeedAppearanceAsync(db, context, context.Players[0], null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetCompletedGameAsync(context.Game.Id));
    }

    private static PlayerBattingStatLine Find(
        CompletedGameProjection projection, int playerId) =>
        projection.Players.Single(item => item.PlayerId == playerId).BattingStatistics;

    private static void AssertLine(
        PlayerBattingStatLine line,
        int pa,
        int ab,
        int hits,
        int totalBases,
        string average,
        string onBase,
        string slugging)
    {
        Assert.Equal((pa, ab, hits, totalBases),
            (line.PlateAppearances, line.AtBats, line.Hits, line.TotalBases));
        Assert.Equal((average, onBase, slugging),
            (BattingRateFormatter.Format(line.BattingAverage),
                BattingRateFormatter.Format(line.OnBasePercentage),
                BattingRateFormatter.Format(line.SluggingPercentage)));
    }

    private static object StatValues(CompletedGamePlayerProjection player) => new
    {
        player.PlayerId,
        player.DisplayName,
        player.BattingPosition,
        player.BattingStatistics.PlateAppearances,
        player.BattingStatistics.AtBats,
        player.BattingStatistics.Hits,
        player.BattingStatistics.TotalBases,
        player.BattingStatistics.BattingAverage,
        player.BattingStatistics.OnBasePercentage,
        player.BattingStatistics.SluggingPercentage,
    };

    private static async Task<TestGameContext> CreateGameAsync(
        TemporaryTestDatabase db,
        TeamGameSide teamSide,
        int playerCount,
        bool duplicateFinalNames = false)
    {
        var team = new Team { Name = "Scored Team", IsActive = true };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        var season = new Season
        {
            TeamId = team.Id,
            Name = "Season",
            Year = 2026,
        };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        var game = new Game
        {
            TeamId = team.Id,
            SeasonId = season.Id,
            ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18),
            Opponent = "Opponent Club",
            Field = "Field",
        };
        Assert.Equal(1, await db.SeedGameAsync(game));
        var players = new List<Player>();
        for (var index = 0; index < playerCount; index++)
        {
            var duplicateName = duplicateFinalNames && index >= playerCount - 2;
            var player = await CreatePlayerAsync(db, team,
                duplicateName ? "Same" : $"Player{index + 1}",
                duplicateName ? "Name" : "Test");
            Assert.Equal(1, await db.SeedGameLineupPlayerAsync(new()
            {
                GameId = game.Id,
                PlayerId = player.Id,
                BattingOrder = index + 1,
            }));
            players.Add(player);
        }
        var initialized = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(team.Id, game.Id, teamSide);
        Assert.True(initialized.IsSuccess);
        return new(team, game, players, initialized.Session!);
    }

    private static async Task<Player> CreatePlayerAsync(
        TemporaryTestDatabase db,
        Team team,
        string firstName,
        string lastName)
    {
        var player = new Player
        {
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
        };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        Assert.Equal(1, await db.SeedTeamPlayerAsync(new()
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            IsActive = true,
        }));
        return player;
    }

    private static async Task<GamePlateAppearance> SeedAppearanceAsync(
        TemporaryTestDatabase db,
        TestGameContext context,
        Player player,
        BatterResult? result,
        int plateAppearanceNumber,
        int? battingPosition = null)
    {
        var appearance = new GamePlateAppearance
        {
            GameScoringSessionId = context.Session.Id,
            PlayerId = player.Id,
            BattingPosition = battingPosition
                ?? context.Players.Select((item, index) => (item, index))
                    .Single(item => item.item.Id == player.Id).index + 1,
            PlateAppearanceNumber = plateAppearanceNumber,
            Inning = 1,
            Result = result,
        };
        Assert.Equal(1, await db.UseConnectionAsync(
            connection => connection.InsertAsync(appearance)));
        return appearance;
    }

    private static async Task SeedBaseAsync(
        TemporaryTestDatabase db,
        int sessionId,
        Player player,
        ScoringBase scoringBase) =>
        Assert.Equal(1, await db.SeedBaseRunnerAsync(new()
        {
            GameScoringSessionId = sessionId,
            PlayerId = player.Id,
            Base = scoringBase,
        }));

    private sealed record TestGameContext(
        Team Team,
        Game Game,
        IReadOnlyList<Player> Players,
        GameScoringSession Session);
}
