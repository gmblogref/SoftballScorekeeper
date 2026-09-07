using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Automatic_beer_entry_persists_across_repository_and_database_reopen()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var evaluator = new RecordingBeerEvaluator();
        var repository = CreateBeerAwareRepository(db.Database, evaluator);
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        var completed = await CompleteHitAsync(repository, context.Game.Id);
        var entry = Assert.Single(
            await db.CreateBeerLedgerRepository().GetForGameAsync(context.Game.Id)
        );
        Assert.Equal(completed.PlateAppearance!.Id, entry.GamePlateAppearanceId);
        Assert.Equal(completed.PlateAppearance.PlayerId, entry.PlayerId);
        Assert.Equal(BeerRuleType.Dbo, entry.RuleType);
        Assert.Equal(BeerLedgerEntryOrigin.AutomaticScoring, entry.Origin);
        Assert.Equal(1, entry.Amount);
        Assert.Equal(1, evaluator.EvaluationCount);

        repository = CreateBeerAwareRepository(db.Database, evaluator);
        Assert.NotNull(await repository.GetStateForGameAsync(context.Game.Id));
        Assert.Single(await db.CreateBeerLedgerRepository().GetForGameAsync(context.Game.Id));
        Assert.Equal(1, evaluator.EvaluationCount);

        await db.Database.CloseAsync();
        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        try
        {
            var reopenedEntries = await new BeerLedgerRepository(reopenedDatabase).GetForGameAsync(
                context.Game.Id
            );
            Assert.Equal(entry.Id, Assert.Single(reopenedEntries).Id);
        }
        finally
        {
            await reopenedDatabase.CloseAsync();
        }
    }

    [Fact]
    public async Task Beer_history_uses_stable_player_identity_across_player_and_membership_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);
        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        var player = Assert.Single(await db.GetPlayersAsync());

        player.FirstName = "Renamed";
        Assert.Equal(1, await db.CreatePlayerRepository().UpdateAsync(player));
        Assert.Equal(
            1,
            (
                await db.CreateTeamPlayerRepository()
                    .RemovePlayersFromTeamAsync(context.Team.Id, [player.Id])
            ).SuccessfulRemovalCount
        );
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([player.Id]));

        var persisted = Assert.Single(
            await db.CreateBeerLedgerRepository().GetForGameAsync(context.Game.Id)
        );
        Assert.Equal(entry.PlayerId, persisted.PlayerId);
        Assert.Equal(player.Id, persisted.PlayerId);
        var inactive = Assert.Single(await db.CreatePlayerRepository().GetInactiveAsync());
        Assert.Equal((player.Id, "Renamed"), (inactive.Id, inactive.FirstName));
    }

    [Fact]
    public async Task Beer_queries_are_isolated_by_game_and_team()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var second = await CreateReadyGameAsync(db, DateTime.Today.AddDays(1), null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        await repository.InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);
        await repository.InitializeAsync(second.Team.Id, second.Game.Id, TeamGameSide.Away);
        await CompleteHitAsync(repository, first.Game.Id);

        var ledger = db.CreateBeerLedgerRepository();
        Assert.Single(await ledger.GetForGameAsync(first.Game.Id));
        Assert.Empty(await ledger.GetForGameAsync(second.Game.Id));
        Assert.Single(await ledger.GetForTeamAsync(first.Team.Id));
        Assert.Empty(await ledger.GetForTeamAsync(second.Team.Id));
    }

    [Fact]
    public async Task Completion_preserves_automatic_beer_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteHitAsync(repository, context.Game.Id);

        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        Assert.Single(await db.CreateBeerLedgerRepository().GetForGameAsync(context.Game.Id));
        Assert.NotNull(await repository.GetCompletedGameAsync(context.Game.Id));
    }

    [Fact]
    public async Task Reset_removes_only_entries_owned_by_reset_scoring_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var reset = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var retained = await CreateReadyGameAsync(db, DateTime.Today.AddDays(1), null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        var resetSession = (
            await repository.InitializeAsync(reset.Team.Id, reset.Game.Id, TeamGameSide.Home)
        ).Session!;
        var retainedSession = (
            await repository.InitializeAsync(retained.Team.Id, retained.Game.Id, TeamGameSide.Home)
        ).Session!;
        await CompleteHitAsync(repository, reset.Game.Id);
        await CompleteHitAsync(repository, retained.Game.Id);
        var resetPlayerId = (await repository.GetStateForGameAsync(reset.Game.Id))!
            .PlateAppearances.Single()
            .PlayerId;
        var retainedPlayerId = (await repository.GetStateForGameAsync(retained.Game.Id))!
            .PlateAppearances.Single()
            .PlayerId;
        await db.SeedBeerLedgerEntryAsync(
            new BeerLedgerEntry
            {
                TeamId = reset.Team.Id,
                GameId = reset.Game.Id,
                GameScoringSessionId = null,
                PlayerId = resetPlayerId,
                Origin = BeerLedgerEntryOrigin.BroughtCredit,
                Amount = -2,
            }
        );

        Assert.True((await repository.ResetGameAsync(reset.Game.Id)).IsSuccess);

        Assert.Empty(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.GameScoringSessionId == resetSession.Id
            )
        );
        Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item => item.GameId == retained.Game.Id)
        );
        Assert.Contains(
            await db.GetBeerLedgerEntriesAsync(),
            item =>
                item.GameId == reset.Game.Id && item.Origin == BeerLedgerEntryOrigin.BroughtCredit
        );
        Assert.Contains(
            await db.GetBeerLedgerEntriesAsync(),
            item =>
                item.GameScoringSessionId == retainedSession.Id && item.PlayerId == retainedPlayerId
        );
    }

    [Fact]
    public async Task Automatic_beer_entries_follow_committed_undo_redo_and_divergence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);
        var committed = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(committed.Id, Assert.Single(await db.GetBeerLedgerEntriesAsync()).Id);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        var divergent = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.NotEqual(committed.GamePlateAppearanceId, divergent.GamePlateAppearanceId);
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
    }

    [Fact]
    public async Task Pending_checkpoints_create_no_beer_and_beer_does_not_change_scoring_or_statistics()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var evaluator = new RecordingBeerEvaluator();
        var repository = CreateBeerAwareRepository(db.Database, evaluator);
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(
            (
                await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single)
            ).IsSuccess
        );
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(0, evaluator.EvaluationCount);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());

        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var stats = (await repository.GetInProgressBattingStatisticsAsync(context.Game.Id))!;
        Assert.Equal(
            (0, 0, 1, 2, 1),
            (
                state.Session.HomeScore,
                state.Session.CurrentOuts,
                state.Session.CurrentInning,
                state.Session.CurrentBatterPosition,
                state.BaseRunners.Count
            )
        );
        var batterStats = stats
            .Players.Single(item => item.PlayerId == state.PlateAppearances.Single().PlayerId)
            .BattingStatistics;
        Assert.Equal(
            (1, 1, 1, 1),
            (
                batterStats.PlateAppearances,
                batterStats.AtBats,
                batterStats.Hits,
                batterStats.TotalBases
            )
        );
        Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(1, evaluator.EvaluationCount);
    }

    [Fact]
    public async Task Scoring_history_restore_preserves_manual_entries_outside_automatic_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new RecordingBeerEvaluator());
        var session = (
            await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var completed = await CompleteHitAsync(repository, context.Game.Id);
        await db.SeedBeerLedgerEntryAsync(
            new BeerLedgerEntry
            {
                TeamId = context.Team.Id,
                GameId = context.Game.Id,
                GameScoringSessionId = session.Id,
                PlayerId = completed.PlateAppearance!.PlayerId,
                Origin = BeerLedgerEntryOrigin.ManualAdjustment,
                Amount = -1,
            }
        );

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        var afterUndo = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerLedgerEntryOrigin.ManualAdjustment, afterUndo.Origin);
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(2, (await db.GetBeerLedgerEntriesAsync()).Count);
    }

    [Fact]
    public async Task Production_evaluator_does_not_invent_rule_consequences()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
    }

    private static GameScoringSessionRepository CreateBeerAwareRepository(
        AppDatabase database,
        IBeerRuleEvaluator evaluator
    ) => new(database, new PlayResolutionCoordinator(), evaluator);

    private sealed class RecordingBeerEvaluator : IBeerRuleEvaluator
    {
        public int EvaluationCount { get; private set; }

        public IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context)
        {
            EvaluationCount++;
            return [new(context.PlateAppearance.PlayerId, BeerRuleType.Dbo, 1)];
        }
    }
}
