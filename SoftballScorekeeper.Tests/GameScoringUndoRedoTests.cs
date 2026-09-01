using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task New_session_has_no_undo_or_redo()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.Equal(
            new(false, false),
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)
        );
        Assert.Equal(
            ScoringUndoRedoFailure.NothingToUndo,
            (await repository.UndoAsync(context.Game.Id)).FailureReason
        );
        Assert.Equal(
            ScoringUndoRedoFailure.NothingToRedo,
            (await repository.RedoAsync(context.Game.Id)).FailureReason
        );
    }

    [Fact]
    public async Task Undo_and_redo_committed_single_restore_exact_authoritative_states()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var before = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        var undo = await repository.UndoAsync(context.Game.Id);
        Assert.True(undo.IsSuccess, undo.FailureReason.ToString());
        Assert.Equal(
            ScoringStateValues(before!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(committed!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Fact]
    public async Task Multiple_undo_and_redo_steps_follow_committed_action_order()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 4);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var states = new List<string>
        {
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!),
        };
        foreach (
            var result in new[] { BatterResult.Single, BatterResult.Double, BatterResult.Triple }
        )
        {
            Assert.True((await CompleteHitAsync(repository, context.Game.Id, result)).IsSuccess);
            states.Add(
                ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
            );
        }

        for (var index = 2; index >= 0; index--)
        {
            Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
            Assert.Equal(
                states[index],
                ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
            );
        }
        for (var index = 1; index <= 3; index++)
        {
            Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
            Assert.Equal(
                states[index],
                ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
            );
        }
    }

    [Fact]
    public async Task New_commit_after_undo_invalidates_redo_chain()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id, BatterResult.Single);
        await repository.UndoAsync(context.Game.Id);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        var availability = await repository.GetUndoRedoAvailabilityAsync(context.Game.Id);
        Assert.True(availability.CanUndo);
        Assert.False(availability.CanRedo);
        Assert.Equal(
            ScoringUndoRedoFailure.NothingToRedo,
            (await repository.RedoAsync(context.Game.Id)).FailureReason
        );
    }

    [Fact]
    public async Task Home_run_undo_and_redo_restore_runners_score_home_runs_and_row_identities()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        var players = await db.GetGameScoringLineupPlayersAsync();
        await db.SeedBaseRunnerAsync(
            new()
            {
                GameScoringSessionId = initialized.Session!.Id,
                PlayerId = players[2].PlayerId,
                Base = ScoringBase.Third,
            }
        );
        var before = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(1, committed!.HomeHomeRuns);
        Assert.Equal(2, committed.Session.HomeScore);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(before!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(committed),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Fact]
    public async Task Pending_play_undo_preserves_previous_committed_action()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        var result = await repository.UndoAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        var restored = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Null(restored.PendingPlay);
        Assert.Single(restored.PlateAppearances);
        Assert.True((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanUndo);
    }

    [Fact]
    public async Task Third_out_transition_and_opponent_entry_undo_and_redo_as_one_action()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 4, 2);
        var before = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        var transition = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);
        Assert.NotNull(transition);
        Assert.True(
            await repository.AcknowledgeHalfInningSummaryAsync(context.Game.Id, transition!.Id)
        );
        Assert.True(
            (
                await repository.CompleteOpponentHalfInningAsync(
                    context.Game.Id,
                    transition.Id,
                    3,
                    2
                )
            ).IsSuccess
        );
        var committed = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(before!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(committed!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Fact]
    public async Task Undo_and_redo_cursor_survive_repository_recreation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        var committed = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        repository = db.CreateGameScoringSessionRepository();

        Assert.True((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringStateValues(committed!),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
    }

    [Fact]
    public async Task Undo_and_redo_cursor_survive_database_close_and_reopen()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        await db.Database.CloseAsync();

        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        var reopened = new GameScoringSessionRepository(reopenedDatabase);
        Assert.Equal(
            new(false, true),
            await reopened.GetUndoRedoAvailabilityAsync(context.Game.Id)
        );
        Assert.True((await reopened.RedoAsync(context.Game.Id)).IsSuccess);
        await reopenedDatabase.CloseAsync();
    }

    [Fact]
    public async Task Failed_undo_rolls_back_the_entire_authoritative_restore()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        await db.ExecuteAsync(
            "CREATE TRIGGER FailUndoSessionRestore BEFORE UPDATE OF CurrentBatterPosition ON GameScoringSession BEGIN SELECT RAISE(ABORT, 'test failure'); END"
        );

        var result = await repository.UndoAsync(context.Game.Id);

        Assert.Equal(ScoringUndoRedoFailure.Persistence, result.FailureReason);
        Assert.Equal(
            ScoringStateValues(committed),
            ScoringStateValues((await repository.GetStateForGameAsync(context.Game.Id))!)
        );
        Assert.True((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanUndo);
    }

    [Fact]
    public async Task Completed_game_blocks_live_undo_and_reset_removes_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(
            ScoringUndoRedoFailure.GameCompleted,
            (await repository.UndoAsync(context.Game.Id)).FailureReason
        );
        Assert.Equal(
            new(false, false),
            await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)
        );

        var resetContext = await CreateReadyGameAsync(db, DateTime.Today.AddDays(1), null, null, 3);
        await repository.InitializeAsync(
            resetContext.Team.Id,
            resetContext.Game.Id,
            TeamGameSide.Home
        );
        await CompleteHitAsync(repository, resetContext.Game.Id);
        Assert.True((await repository.ResetGameAsync(resetContext.Game.Id)).IsSuccess);
        Assert.Equal(
            new(false, false),
            await repository.GetUndoRedoAvailabilityAsync(resetContext.Game.Id)
        );
    }

    [Fact]
    public async Task Database_startup_removes_only_expired_completed_game_recovery_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var oldContext = await CreateReadyGameAsync(
            db, DateTime.Today.AddDays(-40), null, null, 3);
        var recentContext = await CreateReadyGameAsync(
            db, DateTime.Today.AddDays(-5), null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();

        foreach (var context in new[] { oldContext, recentContext })
        {
            await repository.InitializeAsync(
                context.Team.Id, context.Game.Id, TeamGameSide.Home);
            Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);
            Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);
        }
        var sessions = await db.GetGameScoringSessionsAsync();
        var oldSession = sessions.Single(item => item.GameId == oldContext.Game.Id);
        var recentSession = sessions.Single(item => item.GameId == recentContext.Game.Id);
        await db.ExecuteAsync(
            "UPDATE GameScoringSession SET CompletedAtUtc = ? WHERE Id = ?",
            DateTime.UtcNow.AddDays(-22), oldSession.Id);
        await db.ExecuteAsync(
            "UPDATE GameScoringSession SET CompletedAtUtc = ? WHERE Id = ?",
            DateTime.UtcNow.AddDays(-20), recentSession.Id);
        await db.Database.CloseAsync();

        var reopened = new SoftballScorekeeper.Infrastructure.Data.AppDatabase(db.DatabasePath);
        await reopened.InitializeAsync();
        await reopened.CloseAsync();
        var verification = new SQLite.SQLiteAsyncConnection(db.DatabasePath);

        Assert.Equal(0, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GameScoringHistory WHERE GameScoringSessionId = ?",
            oldSession.Id));
        Assert.Equal(0, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GameScoringHistorySnapshot WHERE GameScoringSessionId = ?",
            oldSession.Id));
        Assert.Equal(1, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GameScoringHistory WHERE GameScoringSessionId = ?",
            recentSession.Id));
        Assert.Equal(2, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GameScoringSession WHERE Id IN (?, ?)",
            oldSession.Id, recentSession.Id));
        Assert.Equal(2, await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GamePlateAppearance WHERE GameScoringSessionId IN (?, ?)",
            oldSession.Id, recentSession.Id));
        Assert.True(await verification.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GamePlateAppearanceRunnerMovement WHERE GamePlateAppearanceId IN (SELECT Id FROM GamePlateAppearance WHERE GameScoringSessionId IN (?, ?))",
            oldSession.Id, recentSession.Id) > 0);
        await verification.CloseAsync();
    }

    private static string ScoringStateValues(GameScoringState state) =>
        string.Join(
            '|',
            state.Session.HomeScore,
            state.Session.VisitorScore,
            state.Session.CurrentBatterPosition,
            state.Session.CurrentInning,
            state.Session.CurrentOuts,
            state.HomeHomeRuns,
            state.VisitorHomeRuns,
            string.Join(
                ';',
                state.PlateAppearances.Select(item =>
                    $"{item.Id},{item.PlayerId},{item.BattingPosition},{item.PlateAppearanceNumber},{item.Inning},{item.Result}"
                )
            ),
            string.Join(
                ';',
                state.BaseRunners.Select(item => $"{item.Id},{item.PlayerId},{item.Base}")
            ),
            string.Join(
                ';',
                state.RunnerMovements.Select(item =>
                    $"{item.Id},{item.GamePlateAppearanceId},{item.PlayerId},{item.StartingLocation},{item.Destination},{item.SafeDestination},{item.AttemptedDestination},{item.OutOrder},{item.RunCounts}"
                )
            ),
            string.Join(
                ';',
                state.HalfInnings.Select(item =>
                    $"{item.Id},{item.Inning},{item.TeamRuns},{item.TeamHomeRuns},{item.TeamSummaryAcknowledged},{item.OpponentRuns},{item.OpponentHomeRuns},{item.OpponentEntryCompleted}"
                )
            ),
            state.PendingPlay is null ? "none" : state.PendingPlay.Play.Result.ToString()
        );
}
