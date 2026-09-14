using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class TeamYearFinalizationTests
{
    [Fact]
    public async Task Finalized_team_year_rejects_team_roster_game_and_lineup_writes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Finalized");
        var player = await CreatePlayerAsync(db, "First");
        var otherPlayer = await CreatePlayerAsync(db, "Other");
        var roster = db.CreateTeamPlayerRepository();
        Assert.Equal(1, (await roster.AddPlayersToTeamAsync(team.Id, [player.Id]))
            .SuccessfullyActiveMembershipCount);
        var membership = Assert.Single(await db.GetTeamPlayersAsync());
        var season = Assert.IsType<Season>(await db.CreateSeasonRepository()
            .GetOrCreateForTeamAsync(team.Id, "Spring", team.Year));
        var gameRepository = db.CreateGameRepository();
        var created = await gameRepository.CreateForTeamAsync(team.Id, season,
            [NewGame(new DateTime(2026, 5, 1))]);
        Assert.True(created.IsCompleteSuccess);
        var gameId = Assert.Single(created.CreatedGameIds);
        var lineup = db.CreateGameLineupRepository();
        Assert.True((await lineup.SaveInitialAsync(team.Id, gameId, [player.Id])).IsSuccess);

        Assert.True(await db.CreateTeamRepository().MarkYearOverAsync(team.Id));
        var beforeGames = await db.GetGamesAsync();
        var beforeLineup = await db.GetGameLineupPlayersAsync();

        team.Name = "Changed";
        Assert.Equal(0, await db.CreateTeamRepository().UpdateAsync(team));
        Assert.Equal(0, await roster.UpdateJerseyNumberAsync(team.Id, membership.Id, 42));
        Assert.Equal(0, (await roster.AddPlayersToTeamAsync(team.Id, [otherPlayer.Id]))
            .SuccessfullyActiveMembershipCount);
        var createdForTeam = new Player { FirstName = "Blocked" };
        Assert.Equal(CreatePlayerFailure.InvalidTeam,
            (await db.CreatePlayerRepository().CreateForTeamAsync(createdForTeam, team.Id))
                .Failure);
        Assert.Equal(0, createdForTeam.Id);
        Assert.Empty((await roster.RemovePlayersFromTeamAsync(team.Id, [player.Id]))
            .RemovedPlayerIds);
        Assert.Null(await db.CreateSeasonRepository()
            .GetOrCreateForTeamAsync(team.Id, "Fall", team.Year));
        Assert.False((await gameRepository.CreateForTeamAsync(team.Id, season,
            [NewGame(new DateTime(2026, 5, 8))])).IsCompleteSuccess);
        Assert.False((await gameRepository.UpdateForTeamAsync(team.Id, gameId, season,
            NewGame(new DateTime(2026, 5, 2)))).IsSuccess);
        Assert.Empty((await gameRepository.RemoveGamesAsync(team.Id, [gameId])).DeletedGameIds);
        Assert.False((await lineup.AppendAsync(team.Id, gameId, [otherPlayer.Id])).IsSuccess);
        Assert.False((await lineup.ReorderAsync(gameId, [player.Id])).IsSuccess);
        Assert.Empty((await lineup.RemovePlayersAsync(gameId, [player.Id])).RemovedPlayerIds);

        Assert.Equal(beforeGames.Select(GameValues),
            (await db.GetGamesAsync()).Select(GameValues));
        Assert.Equal(beforeLineup.Select(LineupValues),
            (await db.GetGameLineupPlayersAsync()).Select(LineupValues));
        Assert.Equal("Finalized", (await db.CreateTeamRepository()
            .GetActiveByIdAsync(team.Id))!.Name);
        Assert.Single(await roster.GetActiveRosterForTeamAsync(team.Id));
    }

    [Fact]
    public async Task Finalized_team_year_rejects_scoring_and_beer_writes_but_remains_readable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Read Only");
        var player = await CreatePlayerAsync(db, "Batter");
        Assert.Equal(1, (await db.CreateTeamPlayerRepository()
            .AddPlayersToTeamAsync(team.Id, [player.Id])).SuccessfullyActiveMembershipCount);
        var season = Assert.IsType<Season>(await db.CreateSeasonRepository()
            .GetOrCreateForTeamAsync(team.Id, "Summer", team.Year));
        var games = db.CreateGameRepository();
        var first = await games.CreateForTeamAsync(team.Id, season,
            [NewGame(new DateTime(2026, 6, 1))]);
        var second = await games.CreateForTeamAsync(team.Id, season,
            [NewGame(new DateTime(2026, 6, 8))]);
        var gameId = Assert.Single(first.CreatedGameIds);
        var unstartedGameId = Assert.Single(second.CreatedGameIds);
        var lineup = db.CreateGameLineupRepository();
        Assert.True((await lineup.SaveInitialAsync(team.Id, gameId, [player.Id])).IsSuccess);
        Assert.True((await lineup.SaveInitialAsync(team.Id, unstartedGameId, [player.Id])).IsSuccess);
        var scoring = db.CreateGameScoringSessionRepository();
        Assert.True((await scoring.InitializeAsync(team.Id, gameId, TeamGameSide.Home)).IsSuccess);
        var beerSettings = await db.CreateBeerRuleConfigurationRepository().GetTeamAsync(team.Id);
        Assert.NotNull(beerSettings);

        Assert.True(await db.CreateTeamRepository().MarkYearOverAsync(team.Id));
        var beforeSessions = await db.GetGameScoringSessionsAsync();
        var beforeAppearances = await db.GetGamePlateAppearancesAsync();
        var beforeLedger = await db.GetBeerLedgerEntriesAsync();

        Assert.Equal(InitializeScoringSessionFailure.TeamYearFinalized,
            (await scoring.InitializeAsync(team.Id, unstartedGameId, TeamGameSide.Home))
                .FailureReason);
        Assert.Equal(PendingPlayFailure.TeamYearFinalized,
            (await scoring.SelectBatterResultAsync(gameId, BatterResult.Single)).FailureReason);
        Assert.Equal(ScoringUndoRedoFailure.TeamYearFinalized,
            (await scoring.UndoAsync(gameId)).FailureReason);
        Assert.Equal(GameScoringLifecycleFailure.TeamYearFinalized,
            (await scoring.UpdateGameRulesAsync(gameId, 4, true)).FailureReason);
        Assert.Equal(GameScoringLifecycleFailure.TeamYearFinalized,
            (await scoring.ResetGameAsync(gameId)).FailureReason);
        Assert.False(await db.CreateBeerRuleConfigurationRepository()
            .SaveTeamAsync(team.Id, beerSettings!));
        Assert.Equal(BeerAccountingUpdateFailure.TeamYearFinalized,
            (await db.CreateBeerAccountingRepository()
                .SetBroughtAsync(team.Id, player.Id, 1)).FailureReason);
        Assert.Equal(LowManCalculationFailure.TeamYearFinalized,
            (await db.CreateLowManCalculationRepository()
                .CalculateAsync(team.Id, new DateTime(2026, 6, 1))).FailureReason);

        Assert.Equal(beforeSessions.Select(SessionValues),
            (await db.GetGameScoringSessionsAsync()).Select(SessionValues));
        Assert.Equal(beforeAppearances.Select(item => (item.Id, item.Result)),
            (await db.GetGamePlateAppearancesAsync()).Select(item => (item.Id, item.Result)));
        Assert.Equal(beforeLedger.Select(item => item.Id),
            (await db.GetBeerLedgerEntriesAsync()).Select(item => item.Id));
        Assert.Equal(2, (await games.GetForTeamAsync(team.Id)).Count);
        Assert.NotNull(await scoring.GetStateForGameAsync(gameId));
        Assert.NotNull(await db.CreateTeamBattingStatisticsRepository().GetAsync(team.Id));
        Assert.NotNull(await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(team.Id));
        Assert.NotNull(await db.CreateTeamYearSummaryRepository().GetAsync(team.Id));
    }

    [Fact]
    public async Task Finalization_is_idempotent_and_team_activity_remains_independent()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var team = await CreateTeamAsync(db, "Lifecycle");
        var repository = db.CreateTeamRepository();

        Assert.True(await repository.MarkYearOverAsync(team.Id));
        Assert.True(await repository.MarkYearOverAsync(team.Id));
        Assert.True((await repository.GetActiveByIdAsync(team.Id))!.IsActive);
        Assert.Equal(1, await repository.DeactivateAsync(team.Id));
        Assert.True((await repository.GetInactiveAsync()).Single(item => item.Id == team.Id)
            .IsYearOver);
        Assert.Equal(1, await repository.ReactivateAsync(team.Id));
        var reloaded = Assert.IsType<Team>(await repository.GetActiveByIdAsync(team.Id));
        Assert.True(reloaded.IsActive);
        Assert.True(reloaded.IsYearOver);
    }

    private static async Task<Team> CreateTeamAsync(
        TemporaryTestDatabase db,
        string name)
    {
        var team = new Team { Name = name, Year = 2026, IsActive = true };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<Player> CreatePlayerAsync(
        TemporaryTestDatabase db,
        string firstName)
    {
        var player = new Player { FirstName = firstName, IsActive = true };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        return player;
    }

    private static Game NewGame(DateTime date) => new()
    {
        ScheduledDate = date,
        ScheduledTime = TimeSpan.FromHours(18),
        Opponent = "Opponent",
    };

    private static object GameValues(Game item) => new
    {
        item.Id,
        item.TeamId,
        item.SeasonId,
        item.ScheduledDate,
        item.ScheduledTime,
        item.Opponent,
        item.Field,
    };

    private static object LineupValues(GameLineupPlayer item) => new
    {
        item.Id,
        item.GameId,
        item.PlayerId,
        item.BattingOrder,
    };

    private static object SessionValues(GameScoringSession item) => new
    {
        item.Id,
        item.GameId,
        item.Status,
        item.HomeScore,
        item.VisitorScore,
        item.CurrentBatterPosition,
        item.CurrentInning,
        item.CurrentOuts,
        item.HomeRunLimit,
        item.IsOneUpEnabled,
    };
}
