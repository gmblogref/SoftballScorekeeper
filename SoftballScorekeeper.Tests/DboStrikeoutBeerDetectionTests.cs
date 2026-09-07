using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Theory]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 1, 0)]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 2, 5)]
    public async Task Direct_rule_persists_exact_source_player_amount_and_neutral_stats(
        BatterResult result, BeerRuleType expectedRule,
        int expectedAmount, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        var session = (await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home,
            homeRunLimit, false)).Session!;

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        var appearance = Assert.Single(await db.GetGamePlateAppearancesAsync());
        var player = Assert.Single(await db.GetGameScoringLineupPlayersAsync());
        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal((context.Team.Id, context.Game.Id, session.Id,
                appearance.Id, player.PlayerId, expectedRule,
                BeerLedgerEntryOrigin.AutomaticScoring, expectedAmount),
            (entry.TeamId, entry.GameId, entry.GameScoringSessionId,
                entry.GamePlateAppearanceId, entry.PlayerId, entry.RuleType,
                entry.Origin, entry.Amount));
        Assert.Equal(result, appearance.Result);

        var stats = Assert.Single((await repository
            .GetInProgressBattingStatisticsAsync(context.Game.Id))!.Players)
            .BattingStatistics;
        Assert.Equal((1, 1, 0, 0, 0m),
            (stats.PlateAppearances, stats.AtBats, stats.Hits,
                stats.HomeRuns, stats.OnBasePercentage));
    }

    [Theory]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 0)]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 5)]
    public async Task Direct_rule_undo_and_redo_restore_exactly_one_entry(
        BatterResult result, BeerRuleType expectedRule, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);
        var committed = Assert.Single(await db.GetBeerLedgerEntriesAsync());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);

        var restored = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal((committed.Id, committed.GamePlateAppearanceId,
                expectedRule, committed.Amount),
            (restored.Id, restored.GamePlateAppearanceId,
                restored.RuleType, restored.Amount));
    }

    [Theory]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 1, 0)]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 2, 5)]
    public async Task Stacked_direct_and_two_out_entries_undo_and_redo_atomically(
        BatterResult result, BeerRuleType directRule,
        int directAmount, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);
        var committed = await db.GetBeerLedgerEntriesAsync();

        Assert.Equal(2, committed.Count);
        Assert.Contains(committed, item => item.RuleType == directRule
            && item.Amount == directAmount);
        Assert.Contains(committed,
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
                && item.Amount == 1);
        Assert.Single(committed.Select(item => item.GamePlateAppearanceId).Distinct());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        var restored = await db.GetBeerLedgerEntriesAsync();
        Assert.Equal(committed.Select(item => (item.Id, item.RuleType, item.Amount)),
            restored.Select(item => (item.Id, item.RuleType, item.Amount)));
    }

    [Theory]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 0)]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 5)]
    public async Task Duplicate_direct_evaluator_output_persists_once(
        BatterResult result, BeerRuleType expectedRule, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(
            db.Database, new DuplicateBeerRuleEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);

        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(expectedRule, entry.RuleType);
    }

    [Fact]
    public async Task Strikeout_divergence_discards_abandoned_automatic_entry()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Strikeout)).IsSuccess);
        Assert.Single(await db.GetBeerLedgerEntriesAsync());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(
            context.Game.Id)).CanRedo);
    }

    [Fact]
    public async Task Reset_removes_direct_entries_but_preserves_other_game_and_manual_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var reset = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var retained = await CreateReadyGameAsync(
            db, DateTime.Today.AddDays(1), null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        var resetSession = (await repository.InitializeAsync(
            reset.Team.Id, reset.Game.Id, TeamGameSide.Home, 0, false)).Session!;
        var retainedSession = (await repository.InitializeAsync(
            retained.Team.Id, retained.Game.Id, TeamGameSide.Away)).Session!;
        Assert.True((await repository.SelectBatterResultAsync(
            reset.Game.Id, BatterResult.DeadBallOut)).IsSuccess);
        Assert.True((await repository.SelectBatterResultAsync(
            retained.Game.Id, BatterResult.Strikeout)).IsSuccess);
        var resetEntry = (await db.GetBeerLedgerEntriesAsync())
            .Single(item => item.GameId == reset.Game.Id);
        await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
        {
            TeamId = reset.Team.Id,
            GameId = reset.Game.Id,
            PlayerId = resetEntry.PlayerId,
            Origin = BeerLedgerEntryOrigin.ManualAdjustment,
            Amount = -1,
        });

        Assert.True((await repository.ResetGameAsync(reset.Game.Id)).IsSuccess);

        var remaining = await db.GetBeerLedgerEntriesAsync();
        Assert.DoesNotContain(remaining,
            item => item.GameScoringSessionId == resetSession.Id);
        Assert.Contains(remaining,
            item => item.GameId == reset.Game.Id
                && item.Origin == BeerLedgerEntryOrigin.ManualAdjustment);
        Assert.Contains(remaining,
            item => item.GameScoringSessionId == retainedSession.Id
                && item.RuleType == BeerRuleType.Strikeout);
    }

    [Theory]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 1, 0)]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 2, 5)]
    public async Task Direct_entry_survives_resume_database_reopen_and_completion(
        BatterResult result, BeerRuleType expectedRule,
        int expectedAmount, int homeRunLimit)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id,
            TeamGameSide.Home, homeRunLimit, false);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, result)).IsSuccess);
        var committed = Assert.Single(await db.GetBeerLedgerEntriesAsync());

        repository = db.CreateGameScoringSessionRepository();
        Assert.NotNull(await repository.GetStateForGameAsync(context.Game.Id));
        Assert.Equal(committed.Id,
            Assert.Single(await db.GetBeerLedgerEntriesAsync()).Id);

        await db.Database.CloseAsync();
        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        try
        {
            var reopenedRepository = new GameScoringSessionRepository(reopenedDatabase);
            var reopened = Assert.Single(await new BeerLedgerRepository(reopenedDatabase)
                .GetForGameAsync(context.Game.Id));
            Assert.Equal((committed.Id, expectedRule, expectedAmount),
                (reopened.Id, reopened.RuleType, reopened.Amount));
            Assert.True((await reopenedRepository.CompleteGameAsync(
                context.Game.Id)).IsSuccess);
            var preserved = Assert.Single(await new BeerLedgerRepository(reopenedDatabase)
                .GetForGameAsync(context.Game.Id));
            Assert.Equal(reopened.Id, preserved.Id);
            var completedStats = Assert.Single((await reopenedRepository
                .GetCompletedGameAsync(context.Game.Id))!.Players)
                .BattingStatistics;
            Assert.Equal((1, 1, 0, 0, 0m),
                (completedStats.PlateAppearances, completedStats.AtBats,
                    completedStats.Hits, completedStats.HomeRuns,
                    completedStats.OnBasePercentage));
        }
        finally
        {
            await reopenedDatabase.CloseAsync();
        }
    }
}
