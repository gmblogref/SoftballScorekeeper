using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Reading_live_lineup_and_upcoming_batters_does_not_mutate_scoring_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozenBefore = (await db.GetGameLineupPlayersAsync())
            .Select(item => (item.PlayerId, item.BattingOrder))
            .ToArray();
        var replacement = await CreateEligiblePlayerAsync(db, context.Team.Id, "Replacement");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    frozenBefore[1].PlayerId,
                    replacement.Id
                )
            ).IsSuccess
        );
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        var before = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var sessionBefore = (
            before.Session.Status,
            before.Session.TeamSide,
            before.Session.HomeScore,
            before.Session.VisitorScore,
            before.Session.CurrentBatterPosition,
            before.Session.CurrentInning,
            before.Session.CurrentOuts
        );
        var availabilityBefore = await repository.GetUndoRedoAvailabilityAsync(context.Game.Id);
        var historyBefore = await CountHistoryRowsAsync(db, before.Session.Id);
        var snapshotsBefore = await CountHistorySnapshotsAsync(db, before.Session.Id);

        var viewed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var upcoming = viewed.GetUpcomingPlayerIds();

        Assert.Equal(
            frozenBefore.Select(item => item.PlayerId),
            viewed.BattingSequence.Select(item => item.PlayerId)
        );
        Assert.Equal(frozenBefore[0].PlayerId, viewed.BattingSequence[0].PlayerId);
        Assert.Equal(frozenBefore[1].PlayerId, upcoming!.Value.OnDeckPlayerId);
        Assert.Equal(frozenBefore[2].PlayerId, upcoming.Value.InTheHolePlayerId);
        Assert.Equal(
            availabilityBefore,
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)
        );
        Assert.Equal(historyBefore, await CountHistoryRowsAsync(db, before.Session.Id));
        Assert.Equal(snapshotsBefore, await CountHistorySnapshotsAsync(db, before.Session.Id));
        Assert.Equal(
            frozenBefore,
            (await db.GetGameLineupPlayersAsync())
                .Select(item => (item.PlayerId, item.BattingOrder))
                .ToArray()
        );
        Assert.Empty(await db.GetGameScoringBaseRunnersAsync());
        Assert.Equal(
            sessionBefore,
            (
                viewed.Session.Status,
                viewed.Session.TeamSide,
                viewed.Session.HomeScore,
                viewed.Session.VisitorScore,
                viewed.Session.CurrentBatterPosition,
                viewed.Session.CurrentInning,
                viewed.Session.CurrentOuts
            )
        );

        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        var wrapped = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(3, wrapped.Session.CurrentBatterPosition);
        Assert.Equal(
            frozenBefore[0].PlayerId,
            wrapped.GetUpcomingPlayerIds()!.Value.OnDeckPlayerId
        );

        Assert.True(
            (await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single))
                .IsSuccess
        );
        var pendingBefore = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var pendingJson = System.Text.Json.JsonSerializer.Serialize(pendingBefore.PendingPlay);
        var pendingHistoryCount = await CountHistoryRowsAsync(db, pendingBefore.Session.Id);
        var pendingSnapshotCount = await CountHistorySnapshotsAsync(db, pendingBefore.Session.Id);

        var pendingViewed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(
            pendingJson,
            System.Text.Json.JsonSerializer.Serialize(pendingViewed.PendingPlay)
        );
        Assert.Equal(
            pendingHistoryCount,
            await CountHistoryRowsAsync(db, pendingBefore.Session.Id)
        );
        Assert.Equal(
            pendingSnapshotCount,
            await CountHistorySnapshotsAsync(db, pendingBefore.Session.Id)
        );
        Assert.Equal(
            frozenBefore,
            (await db.GetGameLineupPlayersAsync())
                .Select(item => (item.PlayerId, item.BattingOrder))
                .ToArray()
        );
    }

    private static Task<int> CountHistoryRowsAsync(TemporaryTestDatabase db, int sessionId) =>
        db.UseConnectionAsync(connection =>
            connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GameScoringHistory WHERE GameScoringSessionId = ?",
                sessionId
            )
        );

    private static Task<int> CountHistorySnapshotsAsync(
        TemporaryTestDatabase db,
        int sessionId
    ) =>
        db.UseConnectionAsync(connection =>
            connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
                sessionId
            )
        );

    [Fact]
    public async Task Existing_global_player_joins_team_once_then_live_lineup_preserves_frozen_and_undo_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = (await db.GetGameLineupPlayersAsync()).ToArray();
        var globalPlayer = new Player
        {
            FirstName = "Existing",
            LastName = "Global",
            IsActive = true,
        };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(globalPlayer));
        var teamPlayers = db.CreateTeamPlayerRepository();
        Assert.DoesNotContain(
            await teamPlayers.GetActivePlayersForTeamAsync(context.Team.Id),
            player => player.Id == globalPlayer.Id
        );

        var membership = await teamPlayers.AddPlayersToTeamAsync(
            context.Team.Id,
            [globalPlayer.Id]
        );
        Assert.Equal([globalPlayer.Id], membership.InsertedPlayerIds);
        Assert.Empty(membership.ReactivatedPlayerIds);
        Assert.Empty(membership.AlreadyActivePlayerIds);

        var scoring = db.CreateGameScoringSessionRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var added = await scoring.AddLiveLineupPlayerAsync(
            context.Game.Id,
            globalPlayer.Id,
            2
        );
        Assert.True(added.IsSuccess);
        Assert.Equal(
            LiveLineupChangeFailure.PlayerAlreadyActive,
            (
                await scoring.AddLiveLineupPlayerAsync(
                    context.Game.Id,
                    globalPlayer.Id,
                    3
                )
            ).FailureReason
        );

        Assert.Equal(
            frozen.Select(item => (item.PlayerId, item.BattingOrder)),
            (await db.GetGameLineupPlayersAsync()).Select(item =>
                (item.PlayerId, item.BattingOrder)
            )
        );
        Assert.Equal(
            globalPlayer.Id,
            (await scoring.GetStateForGameAsync(context.Game.Id))!
                .BattingSequence.Single(item => item.BattingPosition == 2)
                .PlayerId
        );

        Assert.True((await scoring.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.DoesNotContain(
            (await scoring.GetStateForGameAsync(context.Game.Id))!.BattingSequence,
            item => item.PlayerId == globalPlayer.Id
        );
        Assert.Contains(
            await teamPlayers.GetActivePlayersForTeamAsync(context.Team.Id),
            player => player.Id == globalPlayer.Id
        );
        Assert.True((await scoring.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Contains(
            (await scoring.GetStateForGameAsync(context.Game.Id))!.BattingSequence,
            item => item.PlayerId == globalPlayer.Id
        );

        var repeatMembership = await teamPlayers.AddPlayersToTeamAsync(
            context.Team.Id,
            [globalPlayer.Id]
        );
        Assert.Empty(repeatMembership.InsertedPlayerIds);
        Assert.Equal([globalPlayer.Id], repeatMembership.AlreadyActivePlayerIds);
        Assert.Single(
            (await db.GetTeamPlayersAsync()).Where(item =>
                item.TeamId == context.Team.Id && item.PlayerId == globalPlayer.Id
            )
        );
        Assert.Single(
            (await db.CreatePlayerRepository().GetActiveAsync()).Where(player =>
                player.Id == globalPlayer.Id
            )
        );
    }

    [Fact]
    public async Task Replace_is_chronological_preserves_frozen_lineup_and_player_statistics()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = await db.GetGameLineupPlayersAsync();
        var replacement = await CreateEligiblePlayerAsync(db, context.Team.Id, "Replacement");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        var replaced = await repository.ReplaceLiveLineupPlayerAsync(
            context.Game.Id,
            frozen[1].PlayerId,
            replacement.Id
        );
        var replacementAppearance = await CompleteHitAsync(
            repository,
            context.Game.Id,
            BatterResult.HomeRun
        );

        Assert.True(replaced.IsSuccess);
        Assert.Equal(replacement.Id, replacementAppearance.PlateAppearance!.PlayerId);
        Assert.Equal(2, replacementAppearance.PlateAppearance.BattingPosition);
        Assert.Equal(
            frozen.Select(item => (item.PlayerId, item.BattingOrder)),
            (await db.GetGameLineupPlayersAsync()).Select(item =>
                (item.PlayerId, item.BattingOrder)
            )
        );

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        var afterPaUndo = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(replacement.Id, afterPaUndo!.BattingSequence[1].PlayerId);
        Assert.Equal(3, afterPaUndo.PlateAppearances.Count);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            frozen[1].PlayerId,
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence[1].PlayerId
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            replacement.Id,
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence[1].PlayerId
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);
        var stats = await repository.GetCompletedGameAsync(context.Game.Id);
        Assert.Equal(
            2,
            stats!
                .Players.Single(item => item.PlayerId == frozen[0].PlayerId)
                .BattingStatistics.PlateAppearances
        );
        Assert.Equal(
            1,
            stats
                .Players.Single(item => item.PlayerId == frozen[1].PlayerId)
                .BattingStatistics.PlateAppearances
        );
        Assert.Equal(
            1,
            stats
                .Players.Single(item => item.PlayerId == replacement.Id)
                .BattingStatistics.PlateAppearances
        );

        var plays = await repository.GetCompletedGamePlaysAsync(context.Game.Id);
        var timeline = Assert.Single(plays!.HalfInnings).Timeline!;
        Assert.Equal(5, timeline.Count);
        Assert.Equal(GameScoringLineupChangeType.Replace, timeline[3].LineupChange!.ChangeType);
        Assert.Equal("Replacement Player", timeline[3].LineupChange!.PlayerName);
    }

    [Fact]
    public async Task Remove_reconciles_current_batter_and_add_reentry_uses_selected_position()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(
            (
                await repository.RemoveLiveLineupPlayerAsync(context.Game.Id, frozen[0].PlayerId)
            ).IsSuccess
        );
        var removedCurrent = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(frozen[1].PlayerId, removedCurrent!.BattingSequence[0].PlayerId);
        Assert.Equal(1, removedCurrent.Session.CurrentBatterPosition);

        Assert.True(
            (
                await repository.AddLiveLineupPlayerAsync(context.Game.Id, frozen[0].PlayerId, 2)
            ).IsSuccess
        );
        var reentered = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(frozen[0].PlayerId, reentered!.BattingSequence[1].PlayerId);
        Assert.Equal(2, reentered.BattingSequence[1].BattingPosition);
        Assert.Equal(
            LiveLineupChangeFailure.PlayerAlreadyActive,
            (
                await repository.AddLiveLineupPlayerAsync(context.Game.Id, frozen[0].PlayerId, 3)
            ).FailureReason
        );

        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        Assert.Equal(
            frozen[1].PlayerId,
            (await repository.GetStateForGameAsync(context.Game.Id))!
                .BattingSequence.Single(item => item.BattingPosition == 1)
                .PlayerId
        );
        Assert.Equal(
            1,
            (await repository.GetStateForGameAsync(context.Game.Id))!.Session.CurrentBatterPosition
        );
    }

    [Fact]
    public async Task Remove_before_current_shifts_position_without_skipping_same_batter()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);

        Assert.Equal(
            3,
            (await repository.GetStateForGameAsync(context.Game.Id))!.Session.CurrentBatterPosition
        );
        Assert.True(
            (
                await repository.RemoveLiveLineupPlayerAsync(context.Game.Id, frozen[0].PlayerId)
            ).IsSuccess
        );
        var state = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(2, state!.Session.CurrentBatterPosition);
        Assert.Equal(
            frozen[2].PlayerId,
            state.BattingSequence.Single(item => item.BattingPosition == 2).PlayerId
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Add_inserts_at_selected_position_and_shifts_every_existing_player(
        int selectedPosition
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var added = await CreateEligiblePlayerAsync(db, context.Team.Id, "Added");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        var result = await repository.AddLiveLineupPlayerAsync(
            context.Game.Id,
            added.Id,
            selectedPosition
        );
        var state = await repository.GetStateForGameAsync(context.Game.Id);
        var expected = frozen.Select(item => item.PlayerId).ToList();
        expected.Insert(selectedPosition - 1, added.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, state!.BattingSequence.Select(item => item.PlayerId));
        Assert.Equal(
            Enumerable.Range(1, expected.Count),
            state.BattingSequence.Select(item => item.BattingPosition)
        );
        Assert.All(
            frozen,
            original =>
                Assert.Contains(
                    state.BattingSequence,
                    current => current.PlayerId == original.PlayerId
                )
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Add_before_after_or_at_end_preserves_current_batter_identity(
        int selectedPosition
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 4);
        var frozen = await db.GetGameLineupPlayersAsync();
        var added = await CreateEligiblePlayerAsync(db, context.Team.Id, "Added");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);

        Assert.True(
            (
                await repository.AddLiveLineupPlayerAsync(
                    context.Game.Id,
                    added.Id,
                    selectedPosition
                )
            ).IsSuccess
        );
        var state = await repository.GetStateForGameAsync(context.Game.Id);
        var current = state!.BattingSequence.Single(item =>
            item.BattingPosition == state.Session.CurrentBatterPosition
        );

        Assert.Equal(frozen[2].PlayerId, current.PlayerId);
        if (selectedPosition == 4)
            Assert.Equal(added.Id, state.GetUpcomingPlayerIds()!.Value.OnDeckPlayerId);
    }

    [Fact]
    public async Task Selected_insertion_position_survives_undo_redo_and_repository_reload()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var added = await CreateEligiblePlayerAsync(db, context.Team.Id, "Added");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(
            (await repository.AddLiveLineupPlayerAsync(context.Game.Id, added.Id, 2)).IsSuccess
        );
        Assert.Equal(
            new[] { frozen[0].PlayerId, added.Id, frozen[1].PlayerId, frozen[2].PlayerId },
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence.Select(item =>
                item.PlayerId
            )
        );
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            frozen.Select(item => item.PlayerId),
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence.Select(item =>
                item.PlayerId
            )
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);

        var reloaded = await db.CreateGameScoringSessionRepository()
            .GetStateForGameAsync(context.Game.Id);
        Assert.Equal(
            new[] { frozen[0].PlayerId, added.Id, frozen[1].PlayerId, frozen[2].PlayerId },
            reloaded!.BattingSequence.Select(item => item.PlayerId)
        );
        var change = Assert.Single(await db.GetGameScoringLineupChangesAsync());
        Assert.Equal(GameScoringLineupChangeType.Add, change.ChangeType);
        Assert.Equal(2, change.BattingPosition);
    }

    [Fact]
    public async Task Add_can_reenter_replaced_player_at_selected_position()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var replacement = await CreateEligiblePlayerAsync(db, context.Team.Id, "Replacement");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    frozen[1].PlayerId,
                    replacement.Id
                )
            ).IsSuccess
        );
        Assert.True(
            (
                await repository.AddLiveLineupPlayerAsync(context.Game.Id, frozen[1].PlayerId, 1)
            ).IsSuccess
        );

        Assert.Equal(
            new[] { frozen[1].PlayerId, frozen[0].PlayerId, replacement.Id, frozen[2].PlayerId },
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence.Select(item =>
                item.PlayerId
            )
        );
    }

    [Fact]
    public async Task Insertion_does_not_reassign_committed_plate_appearance_or_base_runner()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var added = await CreateEligiblePlayerAsync(db, context.Team.Id, "Added");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var committed = await CompleteHitAsync(repository, context.Game.Id, BatterResult.Single);

        Assert.True(
            (await repository.AddLiveLineupPlayerAsync(context.Game.Id, added.Id, 1)).IsSuccess
        );
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(frozen[0].PlayerId, committed.PlateAppearance!.PlayerId);
        Assert.Equal(frozen[0].PlayerId, Assert.Single(state!.BaseRunners).PlayerId);
        Assert.Equal(
            frozen[1].PlayerId,
            state
                .BattingSequence.Single(item =>
                    item.BattingPosition == state.Session.CurrentBatterPosition
                )
                .PlayerId
        );
    }

    [Fact]
    public async Task Middle_add_appears_at_the_correct_chronological_point_in_game_plays()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var added = await CreateEligiblePlayerAsync(db, context.Team.Id, "Added");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);

        Assert.True(
            (await repository.AddLiveLineupPlayerAsync(context.Game.Id, added.Id, 2)).IsSuccess
        );
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var plays = await repository.GetCompletedGamePlaysAsync(context.Game.Id);
        var timeline = Assert.Single(plays!.HalfInnings).Timeline!;
        Assert.Collection(
            timeline,
            item => Assert.NotNull(item.Play),
            item =>
            {
                Assert.Equal(GameScoringLineupChangeType.Add, item.LineupChange!.ChangeType);
                Assert.Equal(2, item.LineupChange.BattingPosition);
                Assert.Equal("Added Player", item.LineupChange.PlayerName);
            },
            item => Assert.NotNull(item.Play)
        );
    }

    [Fact]
    public async Task Divergent_lineup_action_after_undo_clears_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = await db.GetGameLineupPlayersAsync();
        var firstBench = await CreateEligiblePlayerAsync(db, context.Team.Id, "FirstBench");
        var secondBench = await CreateEligiblePlayerAsync(db, context.Team.Id, "SecondBench");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    frozen[0].PlayerId,
                    firstBench.Id
                )
            ).IsSuccess
        );
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    frozen[0].PlayerId,
                    secondBench.Id
                )
            ).IsSuccess
        );

        var availability = await repository.GetUndoRedoAvailabilityAsync(context.Game.Id);
        Assert.True(availability.CanUndo);
        Assert.False(availability.CanRedo);
    }

    [Fact]
    public async Task On_deck_last_batter_replacement_removal_and_reentry_are_deterministic()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var frozen = await db.GetGameLineupPlayersAsync();
        var bench = await CreateEligiblePlayerAsync(db, context.Team.Id, "Bench");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    frozen[1].PlayerId,
                    bench.Id
                )
            ).IsSuccess
        );
        Assert.Equal(
            1,
            (await repository.GetStateForGameAsync(context.Game.Id))!.Session.CurrentBatterPosition
        );
        Assert.True(
            (
                await repository.ReplaceLiveLineupPlayerAsync(
                    context.Game.Id,
                    bench.Id,
                    frozen[1].PlayerId
                )
            ).IsSuccess
        );

        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.HomeRun);
        Assert.Equal(
            3,
            (await repository.GetStateForGameAsync(context.Game.Id))!.Session.CurrentBatterPosition
        );
        Assert.True(
            (
                await repository.RemoveLiveLineupPlayerAsync(context.Game.Id, frozen[2].PlayerId)
            ).IsSuccess
        );
        var wrapped = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(1, wrapped!.Session.CurrentBatterPosition);
        Assert.Equal(
            frozen[0].PlayerId,
            wrapped.BattingSequence.Single(item => item.BattingPosition == 1).PlayerId
        );
    }

    [Fact]
    public async Task Pending_play_blocks_lineup_change_without_mutating_sequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = await db.GetGameLineupPlayersAsync();
        var bench = await CreateEligiblePlayerAsync(db, context.Team.Id, "Bench");
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True(
            (
                await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single)
            ).IsSuccess
        );

        var result = await repository.ReplaceLiveLineupPlayerAsync(
            context.Game.Id,
            frozen[0].PlayerId,
            bench.Id
        );

        Assert.Equal(LiveLineupChangeFailure.UnresolvedScoringState, result.FailureReason);
        Assert.Equal(
            frozen.Select(item => item.PlayerId),
            (await repository.GetStateForGameAsync(context.Game.Id))!.BattingSequence.Select(item =>
                item.PlayerId
            )
        );
    }

    [Fact]
    public async Task New_player_created_during_game_is_normal_roster_data_and_reset_preserves_it()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = await db.GetGameLineupPlayersAsync();
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var player = new Player { FirstName = "New", LastName = "Player" };

        var created = await db.CreatePlayerRepository()
            .CreateForGameLineupAsync(player, context.Team.Id, context.Game.Id);
        Assert.True(created.IsSuccess);
        Assert.Contains(
            await db.GetTeamPlayersAsync(),
            item => item.TeamId == context.Team.Id && item.PlayerId == player.Id && item.IsActive
        );
        Assert.DoesNotContain(
            await db.GetGameLineupPlayersAsync(),
            item => item.PlayerId == player.Id
        );
        Assert.True(
            (await repository.AddLiveLineupPlayerAsync(context.Game.Id, player.Id, 1)).IsSuccess
        );
        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);

        Assert.Contains(await db.GetPlayersAsync(), item => item.Id == player.Id && item.IsActive);
        Assert.Contains(
            await db.GetTeamPlayersAsync(),
            item => item.TeamId == context.Team.Id && item.PlayerId == player.Id && item.IsActive
        );
        Assert.Equal(
            frozen.Select(item => item.PlayerId),
            (await db.GetGameLineupPlayersAsync()).Select(item => item.PlayerId)
        );
        Assert.Empty(await db.GetGameScoringLineupPlayersAsync());
    }

    [Fact]
    public async Task Version_three_active_game_migrates_without_rewriting_saved_lineup()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var before = (await repository.GetStateForGameAsync(context.Game.Id))!
            .BattingSequence.Select(item => (item.PlayerId, item.BattingPosition))
            .ToArray();
        await db.Database.CloseAsync();
        await db.UseConnectionAsync(async connection =>
        {
            await connection.ExecuteAsync("DROP TABLE GameScoringLineupChange");
            return await connection.ExecuteAsync("PRAGMA user_version = 3");
        });

        var migrated = new AppDatabase(db.DatabasePath);
        try
        {
            await migrated.InitializeAsync();
            var migratedRepository = new GameScoringSessionRepository(migrated);
            var state = await migratedRepository.GetStateForGameAsync(context.Game.Id);
            Assert.Equal(AppDatabase.CurrentSchemaVersion, await migrated.GetSchemaVersionAsync());
            Assert.Equal(
                before,
                state!.BattingSequence.Select(item => (item.PlayerId, item.BattingPosition))
            );
            await using var operation = await migrated.BeginOperationAsync();
            Assert.Empty(await operation.Connection.Table<GameScoringLineupChange>().ToListAsync());
        }
        finally
        {
            await migrated.CloseAsync();
        }
    }
}
