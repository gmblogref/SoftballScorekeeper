using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class BeerRuleEvaluatorTests
{
    private readonly BeerRuleEvaluator evaluator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData(RunnerStartingLocation.First, false)]
    [InlineData(RunnerStartingLocation.Second, false)]
    [InlineData(RunnerStartingLocation.Third, false)]
    public void Home_run_is_solo_only_when_no_runner_started_on_a_base(
        RunnerStartingLocation? runnerLocation,
        bool expectedSoloHomeRun
    )
    {
        var movements = new List<BeerRunnerMovement>
        {
            Movement(7, RunnerStartingLocation.Home, RunnerDestination.Scores),
        };
        if (runnerLocation is not null)
            movements.Add(Movement(8, runnerLocation.Value, RunnerDestination.Scores));
        var context = Context(BatterResult.HomeRun, movements);
        var factsBefore = FactValues(context).ToArray();

        var consequences = evaluator.Evaluate(context);

        if (expectedSoloHomeRun)
        {
            var consequence = Assert.Single(consequences);
            Assert.Equal(
                (7, BeerRuleType.SoloHomeRun, 1),
                (consequence.PlayerId, consequence.RuleType, consequence.Amount)
            );
        }
        else
        {
            Assert.Empty(consequences);
        }
        Assert.Equal(factsBefore, FactValues(context));
    }

    [Fact]
    public void Home_run_with_multiple_pre_play_runners_is_not_solo()
    {
        var context = Context(
            BatterResult.HomeRun,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.Scores),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Scores),
                Movement(9, RunnerStartingLocation.Third, RunnerDestination.Scores),
            ]
        );

        Assert.Empty(evaluator.Evaluate(context));
    }

    [Theory]
    [InlineData(BatterResult.Single)]
    [InlineData(BatterResult.Out)]
    [InlineData(BatterResult.Walk)]
    public void Non_home_run_on_empty_bases_is_not_solo(BatterResult result)
    {
        var destination =
            result == BatterResult.Out ? RunnerDestination.Out : RunnerDestination.First;

        Assert.Empty(
            evaluator.Evaluate(
                Context(result, [Movement(7, RunnerStartingLocation.Home, destination)])
            )
        );
    }

    [Fact]
    public void Authoritative_double_play_charges_batter_even_when_batter_is_safe()
    {
        var context = Context(
            BatterResult.DoublePlay,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.First),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
                Movement(9, RunnerStartingLocation.Second, RunnerDestination.Out),
            ]
        );

        var consequence = Assert.Single(evaluator.Evaluate(context));

        Assert.Equal(
            (7, BeerRuleType.DoublePlay, 1),
            (consequence.PlayerId, consequence.RuleType, consequence.Amount)
        );
    }

    [Theory]
    [InlineData(BatterResult.Out)]
    [InlineData(BatterResult.FielderChoice)]
    [InlineData(BatterResult.TriplePlay)]
    public void Other_results_do_not_masquerade_as_double_play(BatterResult result)
    {
        var context = Context(
            result,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.First),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
                Movement(9, RunnerStartingLocation.Second, RunnerDestination.Out),
            ]
        );

        Assert.Empty(evaluator.Evaluate(context));
    }

    [Fact]
    public void Same_batter_second_retired_plate_appearance_in_inning_triggers_rule()
    {
        var first = Appearance(10, 7, 2, BatterResult.Out);
        var second = Appearance(11, 7, 2, BatterResult.Out);

        var consequence = Assert.Single(evaluator.Evaluate(Context(second, first)));

        Assert.Equal(
            (7, BeerRuleType.TwoOutsInOneInning, 1),
            (consequence.PlayerId, consequence.RuleType, consequence.Amount)
        );
    }

    [Fact]
    public void Batter_outs_in_different_innings_do_not_combine()
    {
        var first = Appearance(10, 7, 2, BatterResult.Out);
        var second = Appearance(11, 7, 3, BatterResult.Out);

        Assert.Empty(evaluator.Evaluate(Context(second, first)));
    }

    [Fact]
    public void Different_batters_outs_in_same_inning_do_not_combine()
    {
        var first = Appearance(10, 8, 2, BatterResult.Out);
        var second = Appearance(11, 7, 2, BatterResult.Out);

        Assert.Empty(evaluator.Evaluate(Context(second, first)));
    }

    [Theory]
    [InlineData(BatterResult.Single)]
    [InlineData(BatterResult.Double)]
    [InlineData(BatterResult.Triple)]
    [InlineData(BatterResult.HomeRun)]
    [InlineData(BatterResult.Walk)]
    public void Successful_result_with_runner_out_does_not_count_as_batter_out(BatterResult result)
    {
        var successful = Appearance(
            10,
            7,
            2,
            result,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.First),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
            ]
        );
        var batterOut = Appearance(11, 7, 2, BatterResult.Out);

        Assert.DoesNotContain(
            evaluator.Evaluate(Context(batterOut, successful)),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public void Fielder_choice_runner_out_with_safe_batter_does_not_count()
    {
        var fielderChoice = Appearance(
            10,
            7,
            2,
            BatterResult.FielderChoice,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.First),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
            ]
        );
        var batterOut = Appearance(11, 7, 2, BatterResult.Out);

        Assert.DoesNotContain(
            evaluator.Evaluate(Context(batterOut, fielderChoice)),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public void Fielder_choice_reconciled_to_batter_out_counts_final_out_result()
    {
        var reconciledOut = Appearance(
            10,
            7,
            2,
            BatterResult.Out,
            [Movement(7, RunnerStartingLocation.Home, RunnerDestination.Out)]
        );
        var secondOut = Appearance(11, 7, 2, BatterResult.Out);

        Assert.Contains(
            evaluator.Evaluate(Context(secondOut, reconciledOut)),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public void Sacrifice_fly_and_later_batter_out_trigger_rule()
    {
        var sacrificeFly = Appearance(
            10,
            7,
            2,
            BatterResult.SacrificeFly,
            [Movement(7, RunnerStartingLocation.Home, RunnerDestination.Out)]
        );
        var secondOut = Appearance(11, 7, 2, BatterResult.Out);

        Assert.Contains(
            evaluator.Evaluate(Context(secondOut, sacrificeFly)),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Theory]
    [InlineData(BatterResult.DoublePlay)]
    [InlineData(BatterResult.TriplePlay)]
    public void Multi_out_play_counts_once_only_when_batter_is_retired(BatterResult result)
    {
        var firstOut = Appearance(10, 7, 2, BatterResult.Out);
        var batterRetired = Appearance(
            11,
            7,
            2,
            result,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.Out),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
                Movement(9, RunnerStartingLocation.Second, RunnerDestination.Out),
            ]
        );

        var consequences = evaluator.Evaluate(Context(batterRetired, firstOut));

        Assert.Single(consequences.Where(item => item.RuleType == BeerRuleType.TwoOutsInOneInning));
    }

    [Theory]
    [InlineData(BatterResult.DoublePlay)]
    [InlineData(BatterResult.TriplePlay)]
    public void Multi_out_play_with_safe_batter_does_not_count_batter_out(BatterResult result)
    {
        var multiOut = Appearance(
            10,
            7,
            2,
            result,
            [
                Movement(7, RunnerStartingLocation.Home, RunnerDestination.First),
                Movement(8, RunnerStartingLocation.First, RunnerDestination.Out),
                Movement(9, RunnerStartingLocation.Second, RunnerDestination.Out),
                Movement(10, RunnerStartingLocation.Third, RunnerDestination.Out),
            ]
        );
        var batterOut = Appearance(11, 7, 2, BatterResult.Out);

        Assert.DoesNotContain(
            evaluator.Evaluate(Context(batterOut, multiOut)),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public void Third_batter_out_in_same_inning_does_not_add_another_consequence()
    {
        var first = Appearance(9, 7, 2, BatterResult.Out);
        var second = Appearance(10, 7, 2, BatterResult.Out);
        var third = Appearance(11, 7, 2, BatterResult.Out);

        Assert.Empty(evaluator.Evaluate(Context(third, first, second)));
    }

    private static BeerEvaluationContext Context(
        BatterResult result,
        IReadOnlyList<BeerRunnerMovement> movements
    )
    {
        var appearance = Appearance(11, 7, 2, result, movements);
        return Context(appearance);
    }

    private static BeerEvaluationContext Context(
        BeerCommittedPlateAppearance appearance,
        params BeerCommittedPlateAppearance[] earlierAppearances
    )
    {
        var committed = earlierAppearances.Append(appearance).ToArray();
        return new(
            5,
            4,
            3,
            TeamGameSide.Home,
            appearance,
            committed,
            [7, 8, 9],
            0,
            appearance.RunnerMovements.Count(item => item.Destination == RunnerDestination.Out),
            appearance.RunnerMovements.Count(item => item.Destination == RunnerDestination.Scores)
        );
    }

    private static BeerCommittedPlateAppearance Appearance(
        int id,
        int playerId,
        int inning,
        BatterResult result,
        IReadOnlyList<BeerRunnerMovement>? movements = null
    ) => new(id, playerId, id, inning, result, movements ?? []);

    private static BeerRunnerMovement Movement(
        int playerId,
        RunnerStartingLocation startingLocation,
        RunnerDestination destination
    ) =>
        new(
            playerId,
            startingLocation,
            destination,
            null,
            null,
            null,
            destination == RunnerDestination.Scores ? true : null
        );

    private static IEnumerable<string> FactValues(BeerEvaluationContext context) =>
        context.CommittedPlateAppearances.SelectMany(appearance =>
            appearance.RunnerMovements.Select(movement =>
                $"{appearance.Id}|{appearance.PlayerId}|{appearance.Result}|"
                + $"{movement.PlayerId}|{movement.StartingLocation}|{movement.Destination}"
            )
        );
}

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Qualifying_solo_home_run_persists_one_automatic_consequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        var session = (
            await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;

        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            (
                context.Team.Id,
                context.Game.Id,
                session.Id,
                completed.PlateAppearance!.Id,
                completed.PlateAppearance.PlayerId,
                BeerRuleType.SoloHomeRun,
                BeerLedgerEntryOrigin.AutomaticScoring,
                1
            ),
            (
                entry.TeamId,
                entry.GameId,
                entry.GameScoringSessionId,
                entry.GamePlateAppearanceId,
                entry.PlayerId,
                entry.RuleType,
                entry.Origin,
                entry.Amount
            )
        );
    }

    [Fact]
    public async Task Home_run_with_committed_runner_on_base_creates_no_solo_consequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var session = (
            await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var sequence = await db.GetGameScoringLineupPlayersAsync();
        await db.SeedBaseRunnerAsync(
            new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id,
                PlayerId = sequence[1].PlayerId,
                Base = ScoringBase.First,
            }
        );

        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
    }

    [Fact]
    public async Task Batter_safe_double_play_persists_batter_consequence_and_unchanged_stats()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var session = (
            await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var sequence = await db.GetGameScoringLineupPlayersAsync();
        await db.SeedBaseRunnerAsync(
            new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id,
                PlayerId = sequence[1].PlayerId,
                Base = ScoringBase.First,
            }
        );
        await db.SeedBaseRunnerAsync(
            new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id,
                PlayerId = sequence[2].PlayerId,
                Base = ScoringBase.Second,
            }
        );
        Assert.True(
            (
                await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.DoublePlay)
            ).IsSuccess
        );
        Assert.True(
            (
                await repository.SelectMultiOutRetirementsAsync(
                    context.Game.Id,
                    [Out(sequence[2].PlayerId), Out(sequence[1].PlayerId)]
                )
            ).IsSuccess
        );

        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            (sequence[0].PlayerId, BeerRuleType.DoublePlay, 1),
            (entry.PlayerId, entry.RuleType, entry.Amount)
        );
        var state = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(sequence[0].PlayerId, Assert.Single(state.BaseRunners).PlayerId);
        var stats = (await repository.GetInProgressBattingStatisticsAsync(context.Game.Id))!;
        var batter = stats
            .Players.Single(item => item.PlayerId == sequence[0].PlayerId)
            .BattingStatistics;
        Assert.Equal(
            (1, 1, 0, 0),
            (batter.PlateAppearances, batter.AtBats, batter.Hits, batter.TotalBases)
        );
    }

    [Fact]
    public async Task Duplicate_evaluator_output_persists_once_without_restricting_nonautomatic_entries()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new DuplicateBeerRuleEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var completed = await repository.CompleteHomeRunAsync(context.Game.Id);
        var automatic = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        foreach (
            var origin in new[]
            {
                BeerLedgerEntryOrigin.ManualAdjustment,
                BeerLedgerEntryOrigin.ManualAdjustment,
                BeerLedgerEntryOrigin.BroughtCredit,
            }
        )
            Assert.Equal(
                1,
                await db.SeedBeerLedgerEntryAsync(
                    new BeerLedgerEntry
                    {
                        TeamId = automatic.TeamId,
                        GameId = automatic.GameId,
                        GameScoringSessionId = automatic.GameScoringSessionId,
                        PlayerId = automatic.PlayerId,
                        RuleType = automatic.RuleType,
                        Origin = origin,
                        GamePlateAppearanceId = completed.PlateAppearance!.Id,
                        Amount = 1,
                    }
                )
            );

        Assert.Equal(4, (await db.GetBeerLedgerEntriesAsync()).Count);
        Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
            )
        );
    }

    [Fact]
    public async Task Separate_solo_home_runs_create_separate_attributed_consequences()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        var entries = await db.GetBeerLedgerEntriesAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(2, entries.Select(item => item.GamePlateAppearanceId).Distinct().Count());
        Assert.Equal(
            (await db.GetGameScoringLineupPlayersAsync()).Select(item => item.PlayerId),
            entries.Select(item => item.PlayerId)
        );
    }

    [Fact]
    public async Task Solo_home_run_undo_redo_and_divergence_restore_exact_beer_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        var committed = Assert.Single(await db.GetBeerLedgerEntriesAsync());

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(committed.Id, Assert.Single(await db.GetBeerLedgerEntriesAsync()).Id);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
    }

    [Fact]
    public async Task Solo_home_run_survives_resume_and_completion_without_changing_statistics()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        repository = db.CreateGameScoringSessionRepository();
        Assert.NotNull(await repository.GetStateForGameAsync(context.Game.Id));
        Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerRuleType.SoloHomeRun, entry.RuleType);
        var completed = (await repository.GetCompletedGameAsync(context.Game.Id))!;
        var stats = Assert.Single(completed.Players).BattingStatistics;
        Assert.Equal(
            (1, 1, 1, 1, 4),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.HomeRuns, stats.TotalBases)
        );
    }

    [Fact]
    public async Task Reset_removes_solo_home_run_but_preserves_unrelated_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        var automatic = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            1,
            await db.SeedBeerLedgerEntryAsync(
                new BeerLedgerEntry
                {
                    TeamId = automatic.TeamId,
                    PlayerId = automatic.PlayerId,
                    Origin = BeerLedgerEntryOrigin.BroughtCredit,
                    Amount = -1,
                }
            )
        );

        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);

        var retained = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerLedgerEntryOrigin.BroughtCredit, retained.Origin);
    }

    [Fact]
    public async Task Same_player_second_batter_out_in_inning_persists_one_consequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        var session = (
            await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var player = Assert.Single(await db.GetGameScoringLineupPlayersAsync());

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        var entry = Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.RuleType == BeerRuleType.TwoOutsInOneInning
            )
        );
        Assert.Equal(
            (
                context.Team.Id,
                context.Game.Id,
                session.Id,
                player.PlayerId,
                BeerLedgerEntryOrigin.AutomaticScoring,
                1
            ),
            (
                entry.TeamId,
                entry.GameId,
                entry.GameScoringSessionId,
                entry.PlayerId,
                entry.Origin,
                entry.Amount
            )
        );
    }

    [Fact]
    public async Task Batter_outs_in_separate_innings_do_not_create_consequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        var transition = (await repository.GetPendingHalfInningTransitionAsync(context.Game.Id))!;
        Assert.True(
            await repository.AcknowledgeHalfInningSummaryAsync(context.Game.Id, transition.Id)
        );
        Assert.True(
            (
                await repository.CompleteOpponentHalfInningAsync(
                    context.Game.Id,
                    transition.Id,
                    0,
                    0
                )
            ).IsSuccess
        );
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public async Task Different_players_single_outs_in_same_inning_do_not_create_consequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public async Task Duplicate_two_out_rule_evaluation_persists_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = CreateBeerAwareRepository(db.Database, new DuplicateBeerRuleEvaluator());
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.RuleType == BeerRuleType.TwoOutsInOneInning
            )
        );
    }

    [Fact]
    public async Task Two_out_rule_is_isolated_by_game_and_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var second = await CreateReadyGameAsync(db, DateTime.Today.AddDays(1), null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);
        await repository.InitializeAsync(second.Team.Id, second.Game.Id, TeamGameSide.Away);

        Assert.True((await repository.SelectOutAsync(first.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(first.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(second.Game.Id)).IsSuccess);

        var ledger = db.CreateBeerLedgerRepository();
        Assert.Single(
            (await ledger.GetForGameAsync(first.Game.Id)).Where(item =>
                item.RuleType == BeerRuleType.TwoOutsInOneInning
            )
        );
        Assert.DoesNotContain(
            await ledger.GetForGameAsync(second.Game.Id),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public async Task Two_out_rule_undo_redo_and_divergence_restore_exact_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        var committed = Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.RuleType == BeerRuleType.TwoOutsInOneInning
            )
        );

        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.RedoAsync(context.Game.Id)).IsSuccess);
        Assert.Equal(committed.Id, Assert.Single(await db.GetBeerLedgerEntriesAsync()).Id);
        Assert.True((await repository.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.True((await CompleteHitAsync(repository, context.Game.Id)).IsSuccess);

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.False((await repository.GetUndoRedoAvailabilityAsync(context.Game.Id)).CanRedo);
    }

    [Fact]
    public async Task Reset_removes_two_out_rule_but_preserves_unrelated_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        var automatic = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            1,
            await db.SeedBeerLedgerEntryAsync(
                new BeerLedgerEntry
                {
                    TeamId = automatic.TeamId,
                    PlayerId = automatic.PlayerId,
                    Origin = BeerLedgerEntryOrigin.BroughtCredit,
                    Amount = -1,
                }
            )
        );

        Assert.True((await repository.ResetGameAsync(context.Game.Id)).IsSuccess);

        var retained = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerLedgerEntryOrigin.BroughtCredit, retained.Origin);
    }

    [Fact]
    public async Task Two_out_rule_survives_resume_and_completion_with_normal_statistics()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        repository = db.CreateGameScoringSessionRepository();
        Assert.NotNull(await repository.GetStateForGameAsync(context.Game.Id));
        Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await repository.CompleteGameAsync(context.Game.Id)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(BeerRuleType.TwoOutsInOneInning, entry.RuleType);
        var completed = (await repository.GetCompletedGameAsync(context.Game.Id))!;
        var stats = Assert.Single(completed.Players).BattingStatistics;
        Assert.Equal(
            (2, 2, 0, 0),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.TotalBases)
        );
    }

    private static MultiOutRetirementSelection Out(int playerId) =>
        new(playerId, RunnerOutcomeChoice.ForDestination(RunnerDestination.Out));

    private sealed class DuplicateBeerRuleEvaluator : IBeerRuleEvaluator
    {
        private readonly BeerRuleEvaluator evaluator = new();

        public IReadOnlyList<BeerConsequence> Evaluate(BeerEvaluationContext context)
        {
            var consequences = evaluator.Evaluate(context);
            return [.. consequences, .. consequences];
        }
    }
}
