using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameLineupRepositoryTests
{
    [Fact]
    public async Task SaveInitialAsync_PreservesSelectionOrderAndNormalizesDuplicates()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var ids = setup.Players.Select(p => p.Id).ToArray();

        var result = await db.CreateGameLineupRepository().SaveInitialAsync(setup.Team.Id, setup.Game.Id,
            [ids[2], ids[0], ids[2], ids[1], ids[0]]);

        Assert.True(result.IsSuccess);
        Assert.Equal([ids[2], ids[0], ids[1]], result.SavedPlayerIds);
        var rows = await db.GetGameLineupPlayersAsync();
        Assert.Equal([ids[2], ids[0], ids[1]], rows.Select(row => row.PlayerId));
        Assert.Equal([1, 2, 3], rows.Select(row => row.BattingOrder));
    }

    [Fact]
    public async Task EligiblePlayers_RequiresActivePlayerAndActiveMembershipForGameTeam()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var inactivePlayer = new Player { FirstName = "Inactive", IsActive = false };
        await db.CreatePlayerRepository().CreateAsync(inactivePlayer);
        await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = setup.Team.Id, PlayerId = inactivePlayer.Id });
        var inactiveMembership = new Player { FirstName = "Former" };
        await db.CreatePlayerRepository().CreateAsync(inactiveMembership);
        await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = setup.Team.Id, PlayerId = inactiveMembership.Id, IsActive = false });

        var eligible = await db.CreateGameLineupRepository().GetEligiblePlayersAsync(setup.Team.Id, setup.Game.Id);

        Assert.Equal(setup.Players[0].Id, Assert.Single(eligible).Id);
    }

    [Fact]
    public async Task SaveInitialAsync_IneligiblePlayerRejectsEntireLineup()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var outsider = new Player { FirstName = "Outsider" };
        await db.CreatePlayerRepository().CreateAsync(outsider);

        var result = await db.CreateGameLineupRepository().SaveInitialAsync(setup.Team.Id, setup.Game.Id,
            [setup.Players[0].Id, outsider.Id]);

        Assert.Equal(SaveGameLineupFailure.IneligiblePlayers, result.FailureReason);
        Assert.Equal([outsider.Id], result.IneligiblePlayerIds);
        Assert.Empty(await db.GetGameLineupPlayersAsync());
    }

    [Fact]
    public async Task SaveInitialAsync_ProtectsGameOwnershipEmptyMissingAndExistingLineup()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var otherTeam = new Team { Name = "Other" }; await db.CreateTeamRepository().CreateAsync(otherTeam);
        var repository = db.CreateGameLineupRepository();
        Assert.Equal(SaveGameLineupFailure.EmptySelection, (await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [])).FailureReason);
        Assert.Equal(SaveGameLineupFailure.InvalidGame, (await repository.SaveInitialAsync(setup.Team.Id, int.MaxValue, [setup.Players[0].Id])).FailureReason);
        Assert.Equal(SaveGameLineupFailure.TeamMismatch, (await repository.SaveInitialAsync(otherTeam.Id, setup.Game.Id, [setup.Players[0].Id])).FailureReason);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).IsSuccess);
        Assert.Equal(SaveGameLineupFailure.LineupAlreadyExists, (await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).FailureReason);
        Assert.Single(await db.GetGameLineupPlayersAsync());
    }

    [Fact]
    public async Task PersistedLineupSurvivesRosterAndGlobalPlayerDeactivationAndIsGameIsolated()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var secondGame = new Game { TeamId = setup.Team.Id, SeasonId = setup.Game.SeasonId, ScheduledDate = setup.Game.ScheduledDate.AddDays(1), ScheduledTime = setup.Game.ScheduledTime };
        await db.SeedGameAsync(secondGame);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, secondGame.Id, [setup.Players[0].Id])).IsSuccess);
        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(setup.Team.Id, [setup.Players[0].Id]);
        await db.CreatePlayerRepository().DeactivateAsync([setup.Players[0].Id]);
        Assert.Single(await repository.GetForGameAsync(setup.Game.Id));
        Assert.Single(await repository.GetForGameAsync(secondGame.Id));
        Assert.Equal(setup.Players[0].Id, Assert.Single(await repository.GetPlayersForGameAsync(setup.Game.Id)).Id);
    }

    [Fact]
    public async Task RemoveGamesAsync_BlocksLineupGameButRemovesUnrelatedGame()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var removable = new Game { TeamId = setup.Team.Id, SeasonId = setup.Game.SeasonId, ScheduledDate = setup.Game.ScheduledDate.AddDays(1), ScheduledTime = setup.Game.ScheduledTime };
        await db.SeedGameAsync(removable);
        await db.CreateGameLineupRepository().SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id]);

        var result = await db.CreateGameRepository().RemoveGamesAsync(setup.Team.Id, [setup.Game.Id, removable.Id]);

        Assert.Equal([setup.Game.Id], result.BlockedGameIds);
        Assert.Equal([removable.Id], result.DeletedGameIds);
        Assert.Equal(setup.Game.Id, Assert.Single(await db.GetGamesAsync()).Id);
        Assert.Single(await db.GetGameLineupPlayersAsync());
    }

    [Fact]
    public async Task AppendAsync_PreservesExistingOrderAndAppendsNewPlayersInSuppliedOrder()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 5);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [ids[1], ids[0]])).IsSuccess);

        var result = await repository.AppendAsync(
            setup.Team.Id, setup.Game.Id, [ids[4], ids[2], ids[4], ids[3]]);

        Assert.True(result.IsSuccess);
        Assert.Equal([ids[4], ids[2], ids[3]], result.SavedPlayerIds);
        var lineup = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal([ids[1], ids[0], ids[4], ids[2], ids[3]], lineup.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3, 4, 5], lineup.Select(item => item.BattingOrder));
    }

    [Fact]
    public async Task AppendAsync_ExistingPlayerIsIgnoredAndInvalidAdditionDoesNotPartiallyAppend()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 2);
        var outsider = new Player { FirstName = "Outsider" };
        await db.CreatePlayerRepository().CreateAsync(outsider);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).IsSuccess);

        var noOp = await repository.AppendAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id, setup.Players[0].Id]);
        var failed = await repository.AppendAsync(setup.Team.Id, setup.Game.Id, [setup.Players[1].Id, outsider.Id]);

        Assert.True(noOp.IsSuccess);
        Assert.Empty(noOp.SavedPlayerIds);
        Assert.Equal(SaveGameLineupFailure.IneligiblePlayers, failed.FailureReason);
        var persisted = Assert.Single(await repository.GetForGameAsync(setup.Game.Id));
        Assert.Equal(setup.Players[0].Id, persisted.PlayerId);
        Assert.Equal(1, persisted.BattingOrder);
    }

    private static async Task<(Team Team, Game Game, List<Player> Players)> SetupAsync(TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = "Tigers" }; await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Summer", Year = 2026 }; await db.SeedSeasonAsync(season);
        var game = new Game { TeamId = team.Id, SeasonId = season.Id, ScheduledDate = new(2026, 8, 20), ScheduledTime = new(18, 30, 0) }; await db.SeedGameAsync(game);
        var players = new List<Player>();
        for (var i = 0; i < playerCount; i++)
        {
            var player = new Player { FirstName = $"Player{i}" }; await db.CreatePlayerRepository().CreateAsync(player);
            await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = team.Id, PlayerId = player.Id }); players.Add(player);
        }
        return (team, game, players);
    }
}
