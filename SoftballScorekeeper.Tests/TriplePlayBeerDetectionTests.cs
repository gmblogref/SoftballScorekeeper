using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Triple_play_persists_exact_automatic_batter_consequence_and_neutral_stats()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();

        var (session, batter, appearance) = await CommitTriplePlayAsync(
            db, repository, context.Team.Id, context.Game.Id);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            (context.Team.Id, context.Game.Id, session.Id, appearance.Id,
                batter.PlayerId, BeerRuleType.TriplePlay,
                BeerLedgerEntryOrigin.AutomaticScoring, 1),
            (entry.TeamId, entry.GameId, entry.GameScoringSessionId,
                entry.GamePlateAppearanceId, entry.PlayerId, entry.RuleType,
                entry.Origin, entry.Amount));
        Assert.DoesNotContain(await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType is BeerRuleType.DoublePlay or BeerRuleType.Dbo
                or BeerRuleType.Strikeout or BeerRuleType.SoloHomeRun);

        var stats = (await repository
            .GetInProgressBattingStatisticsAsync(context.Game.Id))!.Players
            .Single(item => item.PlayerId == batter.PlayerId).BattingStatistics;
        Assert.Equal((1, 1, 0, 0, 0m),
            (stats.PlateAppearances, stats.AtBats, stats.Hits,
                stats.TotalBases, stats.OnBasePercentage));
    }

    [Fact]
    public async Task Duplicate_triple_play_evaluator_output_persists_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = CreateBeerAwareRepository(
            db.Database, new DuplicateBeerRuleEvaluator());

        await CommitTriplePlayAsync(db, repository, context.Team.Id, context.Game.Id);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerRuleType.TriplePlay, entry.RuleType);
    }

    [Fact]
    public async Task Triple_play_undo_and_redo_restore_exactly_one_entry()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await CommitTriplePlayAsync(db, repository, context.Team.Id, context.Game.Id);
        await CompleteTriplePlayTransitionAsync(repository, context.Game.Id);
        var committed = Assert.Single(await db.GetBeerLedgerEntriesAsync());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);

        var restored = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            (committed.Id, committed.GamePlateAppearanceId,
                BeerRuleType.TriplePlay, 1),
            (restored.Id, restored.GamePlateAppearanceId,
                restored.RuleType, restored.Amount));
    }

    [Fact]
    public async Task Triple_play_divergence_discards_abandoned_automatic_entry()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await CommitTriplePlayAsync(db, repository, context.Team.Id, context.Game.Id);
        await CompleteTriplePlayTransitionAsync(repository, context.Game.Id);

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        Assert.DoesNotContain(await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType == BeerRuleType.TriplePlay);
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(
            context.Game.Id)).CanRedo);
    }

    [Fact]
    public async Task Reset_removes_triple_play_but_preserves_other_game_and_manual_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var reset = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var retained = await CreateReadyGameAsync(
            db, DateTime.Today.AddDays(1), null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var (resetSession, _, _) = await CommitTriplePlayAsync(
            db, repository, reset.Team.Id, reset.Game.Id);
        var (retainedSession, _, _) = await CommitTriplePlayAsync(
            db, repository, retained.Team.Id, retained.Game.Id);
        var resetEntry = (await db.GetBeerLedgerEntriesAsync())
            .Single(item => item.GameId == reset.Game.Id);
        Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(new BeerLedgerEntry
        {
            TeamId = reset.Team.Id,
            GameId = reset.Game.Id,
            PlayerId = resetEntry.PlayerId,
            Origin = BeerLedgerEntryOrigin.ManualAdjustment,
            Amount = -1,
        }));

        Assert.True((await repository.ResetGameAsync(reset.Game.Id)).IsSuccess);

        var remaining = await db.GetBeerLedgerEntriesAsync();
        Assert.DoesNotContain(remaining,
            item => item.GameScoringSessionId == resetSession.Id);
        Assert.Contains(remaining,
            item => item.GameId == reset.Game.Id
                && item.Origin == BeerLedgerEntryOrigin.ManualAdjustment);
        Assert.Contains(remaining,
            item => item.GameScoringSessionId == retainedSession.Id
                && item.RuleType == BeerRuleType.TriplePlay);
    }

    [Fact]
    public async Task Triple_play_survives_resume_database_reopen_and_completion_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await CommitTriplePlayAsync(db, repository, context.Team.Id, context.Game.Id);
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
            var ledger = new BeerLedgerRepository(reopenedDatabase);
            var reopened = Assert.Single(await ledger.GetForGameAsync(context.Game.Id));
            Assert.Equal((committed.Id, BeerRuleType.TriplePlay, 1),
                (reopened.Id, reopened.RuleType, reopened.Amount));

            await CompleteTriplePlayTransitionAsync(
                reopenedRepository, context.Game.Id);
            Assert.True((await reopenedRepository.CompleteGameAsync(
                context.Game.Id)).IsSuccess);

            var preserved = Assert.Single(await ledger.GetForGameAsync(context.Game.Id));
            Assert.Equal(reopened.Id, preserved.Id);
            var completedStats = (await reopenedRepository
                .GetCompletedGameAsync(context.Game.Id))!.Players
                .Single(item => item.PlayerId == committed.PlayerId)
                .BattingStatistics;
            Assert.Equal((1, 1, 0, 0, 0m),
                (completedStats.PlateAppearances, completedStats.AtBats,
                    completedStats.Hits, completedStats.TotalBases,
                    completedStats.OnBasePercentage));
        }
        finally
        {
            await reopenedDatabase.CloseAsync();
        }
    }

    private static async Task<(
        GameScoringSession Session,
        GameScoringLineupPlayer Batter,
        GamePlateAppearance Appearance)> CommitTriplePlayAsync(
        TemporaryTestDatabase db,
        GameScoringSessionRepository repository,
        int teamId,
        int gameId)
    {
        var session = (await repository.InitializeAsync(
            teamId, gameId, TeamGameSide.Home)).Session!;
        var sequence = (await db.GetGameScoringLineupPlayersAsync())
            .Where(item => item.GameScoringSessionId == session.Id)
            .OrderBy(item => item.BattingPosition)
            .ToArray();
        Assert.True(sequence.Length >= 3);
        await db.SeedBaseRunnerAsync(new GameScoringBaseRunner
        {
            GameScoringSessionId = session.Id,
            PlayerId = sequence[1].PlayerId,
            Base = ScoringBase.First,
        });
        await db.SeedBaseRunnerAsync(new GameScoringBaseRunner
        {
            GameScoringSessionId = session.Id,
            PlayerId = sequence[2].PlayerId,
            Base = ScoringBase.Second,
        });

        Assert.True((await repository.SelectBatterResultAsync(
            gameId, BatterResult.TriplePlay)).IsSuccess);

        var appearance = (await db.GetGamePlateAppearancesAsync())
            .Single(item => item.GameScoringSessionId == session.Id);
        return (session, sequence[0], appearance);
    }

    private static async Task CompleteTriplePlayTransitionAsync(
        GameScoringSessionRepository repository,
        int gameId)
    {
        var transition = await repository.GetPendingHalfInningTransitionAsync(gameId);
        Assert.NotNull(transition);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            gameId, transition.Id));
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            gameId, transition.Id, 0, 0)).IsSuccess);
    }
}
