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
    public async Task GetGameIdsWithLineupsAsync_ReportsPersistedRowsPerGameAndTracksRemoval()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var secondGame = new Game { TeamId = setup.Team.Id, SeasonId = setup.Game.SeasonId, ScheduledDate = setup.Game.ScheduledDate, ScheduledTime = setup.Game.ScheduledTime + TimeSpan.FromHours(1) };
        var thirdGame = new Game { TeamId = setup.Team.Id, SeasonId = setup.Game.SeasonId, ScheduledDate = setup.Game.ScheduledDate, ScheduledTime = setup.Game.ScheduledTime + TimeSpan.FromHours(2) };
        await db.SeedGameAsync(secondGame);
        await db.SeedGameAsync(thirdGame);
        var repository = db.CreateGameLineupRepository();

        Assert.Empty(await repository.GetGameIdsWithLineupsAsync(null));
        Assert.Empty(await repository.GetGameIdsWithLineupsAsync([0, -1, setup.Game.Id]));

        Assert.True((await repository.SaveInitialAsync(
            setup.Team.Id,
            setup.Game.Id,
            [setup.Players[0].Id, setup.Players[1].Id])).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(
            setup.Team.Id,
            secondGame.Id,
            [setup.Players[2].Id])).IsSuccess);

        var withLineups = await repository.GetGameIdsWithLineupsAsync(
            [setup.Game.Id, secondGame.Id, thirdGame.Id, setup.Game.Id, 0]);
        Assert.Equal([setup.Game.Id, secondGame.Id], withLineups.Order());

        await repository.RemovePlayersAsync(setup.Game.Id, [setup.Players[0].Id]);
        Assert.Contains(setup.Game.Id, await repository.GetGameIdsWithLineupsAsync([setup.Game.Id]));

        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(
            setup.Team.Id,
            [setup.Players[1].Id]);
        await db.CreatePlayerRepository().DeactivateAsync([setup.Players[1].Id]);
        Assert.Contains(setup.Game.Id, await repository.GetGameIdsWithLineupsAsync([setup.Game.Id]));

        await repository.RemovePlayersAsync(setup.Game.Id, [setup.Players[1].Id]);
        Assert.DoesNotContain(setup.Game.Id, await repository.GetGameIdsWithLineupsAsync([setup.Game.Id]));
        Assert.Contains(secondGame.Id, await repository.GetGameIdsWithLineupsAsync([secondGame.Id]));
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

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task RemovePlayersAsync_RemovesOneAndResequencesSurvivors(int removedIndex)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 5);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);

        var result = await repository.RemovePlayersAsync(setup.Game.Id, [ids[removedIndex]]);

        Assert.True(result.GameExists);
        Assert.Equal([ids[removedIndex]], result.RemovedPlayerIds);
        Assert.Empty(result.MissingPlayerIds);
        var lineup = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(ids.Where((_, index) => index != removedIndex), lineup.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3, 4], lineup.Select(item => item.BattingOrder));
        Assert.Contains((await db.GetPlayersAsync()), player => player.Id == ids[removedIndex]);
        Assert.Contains((await db.GetTeamPlayersAsync()), membership => membership.PlayerId == ids[removedIndex] && membership.IsActive);
    }

    [Fact]
    public async Task RemovePlayersAsync_NormalizesInputReportsMissingAndPreservesRelativeOrder()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 5);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);

        var result = await repository.RemovePlayersAsync(
            setup.Game.Id, [ids[1], ids[3], ids[1], 0, -4, int.MaxValue]);

        Assert.Equal([ids[1], ids[3], 0, -4, int.MaxValue], result.RequestedPlayerIds);
        Assert.Equal([ids[1], ids[3]], result.RemovedPlayerIds);
        Assert.Equal([int.MaxValue], result.MissingPlayerIds);
        Assert.Equal([0, -4], result.IgnoredPlayerIds);
        var lineup = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal([ids[0], ids[2], ids[4]], lineup.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3], lineup.Select(item => item.BattingOrder));
        Assert.Equal(lineup.Count, lineup.Select(item => item.BattingOrder).Distinct().Count());
    }

    [Fact]
    public async Task RemovePlayersAsync_NullEmptyInvalidGameAndNonLineupRequestsAreSafeNoOps()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).IsSuccess);

        Assert.Empty((await repository.RemovePlayersAsync(setup.Game.Id, null)).RemovedPlayerIds);
        Assert.Empty((await repository.RemovePlayersAsync(setup.Game.Id, [])).RemovedPlayerIds);
        Assert.Empty((await repository.RemovePlayersAsync(0, [setup.Players[0].Id])).RemovedPlayerIds);
        var missing = await repository.RemovePlayersAsync(setup.Game.Id, [int.MaxValue]);

        Assert.True(missing.GameExists);
        Assert.Equal([int.MaxValue], missing.MissingPlayerIds);
        Assert.Single(await repository.GetForGameAsync(setup.Game.Id));
    }

    [Fact]
    public async Task RemovePlayersAsync_IsGameIsolatedEvenWhenPlayerIsShared()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var secondGame = new Game { TeamId = setup.Team.Id, SeasonId = setup.Game.SeasonId, ScheduledDate = setup.Game.ScheduledDate.AddDays(1), ScheduledTime = setup.Game.ScheduledTime };
        await db.SeedGameAsync(secondGame);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, secondGame.Id, [ids[1], ids[2]])).IsSuccess);

        await repository.RemovePlayersAsync(setup.Game.Id, [ids[1]]);

        Assert.Equal([ids[0], ids[2]], (await repository.GetForGameAsync(setup.Game.Id)).Select(item => item.PlayerId));
        var otherLineup = await repository.GetForGameAsync(secondGame.Id);
        Assert.Equal([ids[1], ids[2]], otherLineup.Select(item => item.PlayerId));
        Assert.Equal([1, 2], otherLineup.Select(item => item.BattingOrder));
    }

    [Fact]
    public async Task RemovePlayersAsync_AllowsEntireLineupRemovalAndGameDeletion()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 2);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);

        var result = await repository.RemovePlayersAsync(setup.Game.Id, ids);

        Assert.Equal(2, result.SuccessfulRemovalCount);
        Assert.Empty(await repository.GetForGameAsync(setup.Game.Id));
        Assert.False(await repository.HasLineupAsync(setup.Game.Id));
        var gameRemoval = await db.CreateGameRepository().RemoveGamesAsync(setup.Team.Id, [setup.Game.Id]);
        Assert.Equal([setup.Game.Id], gameRemoval.DeletedGameIds);
    }

    [Fact]
    public async Task RemovedEligiblePlayerCanBeReaddedAtEndWithoutRestoringOldPosition()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 4);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        await repository.RemovePlayersAsync(setup.Game.Id, [ids[1]]);

        Assert.Contains(await repository.GetEligiblePlayersAsync(setup.Team.Id, setup.Game.Id), player => player.Id == ids[1]);
        var append = await repository.AppendAsync(setup.Team.Id, setup.Game.Id, [ids[1], ids[1]]);

        Assert.Equal([ids[1]], append.SavedPlayerIds);
        var lineup = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal([ids[0], ids[2], ids[3], ids[1]], lineup.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3, 4], lineup.Select(item => item.BattingOrder));
    }

    [Fact]
    public async Task RemovalPreservesInactivePlayerAndRosterState()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 2);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, setup.Players.Select(player => player.Id).ToArray())).IsSuccess);
        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(setup.Team.Id, [setup.Players[0].Id]);
        await db.CreatePlayerRepository().DeactivateAsync([setup.Players[1].Id]);

        var result = await repository.RemovePlayersAsync(setup.Game.Id, setup.Players.Select(player => player.Id).ToArray());

        Assert.Equal(2, result.SuccessfulRemovalCount);
        var memberships = await db.GetTeamPlayersAsync();
        Assert.False(memberships.Single(item => item.PlayerId == setup.Players[0].Id).IsActive);
        var players = await db.GetPlayersAsync();
        Assert.False(players.Single(player => player.Id == setup.Players[1].Id).IsActive);
        Assert.DoesNotContain(await repository.GetEligiblePlayersAsync(setup.Team.Id, setup.Game.Id), player => player.Id == setup.Players[0].Id || player.Id == setup.Players[1].Id);
    }

    [Fact]
    public async Task ReorderAsync_TwoPlayers_PersistsContiguousOrderAndPreservesIdentifiers()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 2);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        var before = await repository.GetForGameAsync(setup.Game.Id);

        var result = await repository.ReorderAsync(setup.Game.Id, [ids[1], ids[0]]);

        Assert.True(result.IsSuccess);
        Assert.Equal([ids[1], ids[0]], result.OrderedPlayerIds);
        var after = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal([ids[1], ids[0]], after.Select(item => item.PlayerId));
        Assert.Equal([1, 2], after.Select(item => item.BattingOrder));
        Assert.Equal(before.Select(item => item.Id).Order(), after.Select(item => item.Id).Order());
        Assert.All(after, item => Assert.Equal(setup.Game.Id, item.GameId));
    }

    [Fact]
    public async Task ReorderAsync_LastToFirstAndFirstToLast_IsGameIsolated()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 5);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var secondGame = new Game
        {
            TeamId = setup.Team.Id,
            SeasonId = setup.Game.SeasonId,
            ScheduledDate = setup.Game.ScheduledDate.AddDays(1),
            ScheduledTime = setup.Game.ScheduledTime,
        };
        await db.SeedGameAsync(secondGame);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, secondGame.Id, [ids[1], ids[3]])).IsSuccess);
        var requestedOrder = new[] { ids[4], ids[1], ids[2], ids[3], ids[0] };

        var result = await repository.ReorderAsync(setup.Game.Id, requestedOrder);

        Assert.True(result.IsSuccess);
        var reordered = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(requestedOrder, reordered.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3, 4, 5], reordered.Select(item => item.BattingOrder));
        var other = await repository.GetForGameAsync(secondGame.Id);
        Assert.Equal([ids[1], ids[3]], other.Select(item => item.PlayerId));
        Assert.Equal([1, 2], other.Select(item => item.BattingOrder));
    }

    [Fact]
    public async Task ReorderAsync_InvalidOrMismatchedRequests_LeaveOriginalOrderUnchanged()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var otherGame = new Game
        {
            TeamId = setup.Team.Id,
            SeasonId = setup.Game.SeasonId,
            ScheduledDate = setup.Game.ScheduledDate.AddDays(1),
            ScheduledTime = setup.Game.ScheduledTime,
        };
        await db.SeedGameAsync(otherGame);
        var otherPlayer = new Player { FirstName = "Other" };
        await db.CreatePlayerRepository().CreateAsync(otherPlayer);
        await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = setup.Team.Id, PlayerId = otherPlayer.Id });
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, otherGame.Id, [otherPlayer.Id])).IsSuccess);

        Assert.Equal(ReorderGameLineupFailure.InvalidRequest,
            (await repository.ReorderAsync(setup.Game.Id, null)).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.InvalidRequest,
            (await repository.ReorderAsync(setup.Game.Id, [ids[0], ids[0], ids[2]])).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.InvalidRequest,
            (await repository.ReorderAsync(setup.Game.Id, [ids[0], 0, ids[2]])).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.LineupMismatch,
            (await repository.ReorderAsync(setup.Game.Id, [])).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.LineupMismatch,
            (await repository.ReorderAsync(setup.Game.Id, [ids[0], ids[1]])).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.LineupMismatch,
            (await repository.ReorderAsync(setup.Game.Id, [ids[0], ids[1], otherPlayer.Id])).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.InvalidRequest,
            (await repository.ReorderAsync(0, ids)).FailureReason);
        Assert.Equal(ReorderGameLineupFailure.InvalidGame,
            (await repository.ReorderAsync(int.MaxValue, ids)).FailureReason);

        var unchanged = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(ids, unchanged.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3], unchanged.Select(item => item.BattingOrder));
        Assert.Equal(otherPlayer.Id, Assert.Single(await repository.GetForGameAsync(otherGame.Id)).PlayerId);
    }

    [Fact]
    public async Task ReorderAsync_HistoricalPlayersRemainReorderable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, ids)).IsSuccess);
        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(setup.Team.Id, [ids[0]]);
        await db.CreatePlayerRepository().DeactivateAsync([ids[2]]);

        var result = await repository.ReorderAsync(setup.Game.Id, [ids[2], ids[1], ids[0]]);

        Assert.True(result.IsSuccess);
        Assert.Equal([ids[2], ids[1], ids[0]],
            (await repository.GetForGameAsync(setup.Game.Id)).Select(item => item.PlayerId));
    }

    [Fact]
    public async Task UsePreviousAsync_CopiesLatestPriorLineupAndTransactionallyReplacesTarget()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 5);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var emptyPrior = await AddGameAsync(db, setup, setup.Game.ScheduledDate.AddDays(1), setup.Game.ScheduledTime);
        var target = await AddGameAsync(db, setup, setup.Game.ScheduledDate.AddDays(2), setup.Game.ScheduledTime);
        var future = await AddGameAsync(db, setup, setup.Game.ScheduledDate.AddDays(3), setup.Game.ScheduledTime);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [ids[2], ids[0], ids[1]])).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, target.Id, [ids[4]])).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, future.Id, [ids[3]])).IsSuccess);
        var sourceBefore = await repository.GetForGameAsync(setup.Game.Id);
        var targetBefore = await repository.GetForGameAsync(target.Id);

        var result = await repository.UsePreviousAsync(setup.Team.Id, target.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(setup.Game.Id, result.SourceGameId);
        Assert.Equal([ids[2], ids[0], ids[1]], result.CopiedPlayerIds);
        Assert.Empty(result.SkippedPlayerIds);
        var targetAfter = await repository.GetForGameAsync(target.Id);
        Assert.Equal([ids[2], ids[0], ids[1]], targetAfter.Select(item => item.PlayerId));
        Assert.Equal([1, 2, 3], targetAfter.Select(item => item.BattingOrder));
        Assert.All(targetAfter, item => Assert.Equal(target.Id, item.GameId));
        Assert.DoesNotContain(targetAfter, after => sourceBefore.Any(source => source.Id == after.Id));
        Assert.DoesNotContain(targetAfter, after => targetBefore.Any(before => before.Id == after.Id));
        var sourceAfter = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(sourceBefore.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)),
            sourceAfter.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
        Assert.Empty(await repository.GetForGameAsync(emptyPrior.Id));
        Assert.Equal(ids[3], Assert.Single(await repository.GetForGameAsync(future.Id)).PlayerId);
        Assert.Equal(targetAfter.Count, targetAfter.Select(item => item.PlayerId).Distinct().Count());
    }

    [Fact]
    public async Task CanUsePreviousAsync_ReturnsFalseWhenNoPriorValidLineupExists()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var target = await AddGameAsync(
            db,
            setup,
            setup.Game.ScheduledDate.AddDays(1),
            setup.Game.ScheduledTime);
        var repository = db.CreateGameLineupRepository();

        Assert.False(await repository.CanUsePreviousAsync(setup.Team.Id, target.Id));

        Assert.True((await repository.SaveInitialAsync(
            setup.Team.Id,
            setup.Game.Id,
            [setup.Players[0].Id])).IsSuccess);
        await db.CreatePlayerRepository().DeactivateAsync([setup.Players[0].Id]);

        Assert.False(await repository.CanUsePreviousAsync(setup.Team.Id, target.Id));
    }

    [Fact]
    public async Task CanUsePreviousAsync_ReturnsTrueWhenPriorValidLineupExists()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var target = await AddGameAsync(
            db,
            setup,
            setup.Game.ScheduledDate.AddDays(1),
            setup.Game.ScheduledTime);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(
            setup.Team.Id,
            setup.Game.Id,
            [setup.Players[0].Id])).IsSuccess);

        Assert.True(await repository.CanUsePreviousAsync(setup.Team.Id, target.Id));
    }

    [Fact]
    public async Task UsePreviousAsync_UsesDateTimeIdOrderAndNeverAnotherTeam()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 3);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var sameTimeTarget = await AddGameAsync(db, setup, setup.Game.ScheduledDate, setup.Game.ScheduledTime);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [ids[1], ids[0]])).IsSuccess);

        var otherTeam = new Team { Name = "Other" };
        await db.CreateTeamRepository().CreateAsync(otherTeam);
        var otherSeason = new Season { TeamId = otherTeam.Id, Name = "Other", Year = 2026 };
        await db.SeedSeasonAsync(otherSeason);
        var otherGame = new Game { TeamId = otherTeam.Id, SeasonId = otherSeason.Id, ScheduledDate = setup.Game.ScheduledDate, ScheduledTime = setup.Game.ScheduledTime };
        await db.SeedGameAsync(otherGame);
        await db.SeedGameLineupPlayerAsync(new GameLineupPlayer { GameId = otherGame.Id, PlayerId = ids[2], BattingOrder = 1 });

        var result = await repository.UsePreviousAsync(setup.Team.Id, sameTimeTarget.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(setup.Game.Id, result.SourceGameId);
        Assert.Equal([ids[1], ids[0]], (await repository.GetForGameAsync(sameTimeTarget.Id)).Select(item => item.PlayerId));
        Assert.Equal(ids[2], Assert.Single(await repository.GetForGameAsync(otherGame.Id)).PlayerId);

        var reverse = await repository.UsePreviousAsync(setup.Team.Id, setup.Game.Id);
        Assert.Equal(UsePreviousLineupFailure.NoPreviousLineup, reverse.FailureReason);
        Assert.Equal([ids[1], ids[0]], (await repository.GetForGameAsync(setup.Game.Id)).Select(item => item.PlayerId));
    }

    [Fact]
    public async Task UsePreviousAsync_FiltersCurrentEligibilityWithoutChangingHistoricalState()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 4);
        var ids = setup.Players.Select(player => player.Id).ToArray();
        var target = await AddGameAsync(db, setup, setup.Game.ScheduledDate.AddDays(1), setup.Game.ScheduledTime);
        var noEligibleTarget = await AddGameAsync(db, setup, setup.Game.ScheduledDate.AddDays(2), setup.Game.ScheduledTime);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [ids[0], ids[1], ids[2]])).IsSuccess);
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, noEligibleTarget.Id, [ids[3]])).IsSuccess);
        var sourceBefore = await repository.GetForGameAsync(setup.Game.Id);
        await db.CreatePlayerRepository().DeactivateAsync([ids[1]]);
        await db.CreateTeamPlayerRepository().RemovePlayersFromTeamAsync(setup.Team.Id, [ids[2]]);

        var partial = await repository.UsePreviousAsync(setup.Team.Id, target.Id);

        Assert.True(partial.IsSuccess);
        Assert.Equal([ids[0]], partial.CopiedPlayerIds);
        Assert.Equal([ids[1], ids[2]], partial.SkippedPlayerIds);
        Assert.Equal(ids[0], Assert.Single(await repository.GetForGameAsync(target.Id)).PlayerId);
        var sourceAfterPartial = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(sourceBefore.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)),
            sourceAfterPartial.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
        Assert.False((await db.GetPlayersAsync()).Single(player => player.Id == ids[1]).IsActive);
        Assert.False((await db.GetTeamPlayersAsync()).Single(item => item.TeamId == setup.Team.Id && item.PlayerId == ids[2]).IsActive);

        await db.CreatePlayerRepository().DeactivateAsync([ids[0]]);
        var existingBefore = await repository.GetForGameAsync(noEligibleTarget.Id);
        var noEligible = await repository.UsePreviousAsync(setup.Team.Id, noEligibleTarget.Id);

        Assert.Equal(UsePreviousLineupFailure.NoEligiblePlayers, noEligible.FailureReason);
        Assert.Equal(target.Id, noEligible.SourceGameId);
        var existingAfter = await repository.GetForGameAsync(noEligibleTarget.Id);
        Assert.Equal(existingBefore.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)),
            existingAfter.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
        var sourceAfterNoOp = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(sourceBefore.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)),
            sourceAfterNoOp.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
    }

    [Fact]
    public async Task UsePreviousAsync_NoSourceAndInvalidOwnershipPreserveTarget()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var setup = await SetupAsync(db, 1);
        var repository = db.CreateGameLineupRepository();
        Assert.True((await repository.SaveInitialAsync(setup.Team.Id, setup.Game.Id, [setup.Players[0].Id])).IsSuccess);
        var before = await repository.GetForGameAsync(setup.Game.Id);
        var otherTeam = new Team { Name = "Other" };
        await db.CreateTeamRepository().CreateAsync(otherTeam);

        Assert.Equal(UsePreviousLineupFailure.NoPreviousLineup,
            (await repository.UsePreviousAsync(setup.Team.Id, setup.Game.Id)).FailureReason);
        Assert.Equal(UsePreviousLineupFailure.InvalidContext,
            (await repository.UsePreviousAsync(otherTeam.Id, setup.Game.Id)).FailureReason);
        Assert.Equal(UsePreviousLineupFailure.InvalidContext,
            (await repository.UsePreviousAsync(setup.Team.Id, int.MaxValue)).FailureReason);
        Assert.Equal(UsePreviousLineupFailure.InvalidContext,
            (await repository.UsePreviousAsync(0, setup.Game.Id)).FailureReason);
        var after = await repository.GetForGameAsync(setup.Game.Id);
        Assert.Equal(before.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)),
            after.Select(item => (item.Id, item.GameId, item.PlayerId, item.BattingOrder)));
    }

    private static async Task<Game> AddGameAsync(
        TemporaryTestDatabase db,
        (Team Team, Game Game, List<Player> Players) setup,
        DateTime date,
        TimeSpan time)
    {
        var game = new Game
        {
            TeamId = setup.Team.Id,
            SeasonId = setup.Game.SeasonId,
            ScheduledDate = date,
            ScheduledTime = time,
        };
        await db.SeedGameAsync(game);
        return game;
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
