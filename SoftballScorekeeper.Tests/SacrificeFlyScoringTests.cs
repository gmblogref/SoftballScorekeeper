using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class SacrificeFlyScoringTests
{
    [Theory]
    [InlineData("", 0, false)]
    [InlineData("Third", 0, true)]
    [InlineData("Third", 1, true)]
    [InlineData("Third", 2, false)]
    [InlineData("First", 0, false)]
    [InlineData("Second", 0, false)]
    [InlineData("First,Second", 0, false)]
    [InlineData("First,Third", 0, true)]
    [InlineData("Second,Third", 0, true)]
    [InlineData("First,Second,Third", 1, true)]
    public void Core_availability_requires_third_base_and_fewer_than_two_outs(
        string occupiedStartingLocations,
        int currentOuts,
        bool expected
    )
    {
        IReadOnlySet<RunnerStartingLocation> occupied =
            occupiedStartingLocations.Length == 0
            ? new HashSet<RunnerStartingLocation>()
            : occupiedStartingLocations.Split(',')
                .Select(Enum.Parse<RunnerStartingLocation>)
                .ToHashSet();
        var available = PlateAppearanceResultDefinitions.GetSelectableResults(
            occupied,
            currentOuts
        );

        Assert.Equal(expected, available.Contains(BatterResult.SacrificeFly));
    }

    [Fact]
    public void Only_a_pre_play_existing_runner_can_satisfy_sacrifice_fly_completion()
    {
        Assert.False(SacrificeFlyCompletionEvaluator.IsSatisfied(
        [
            new(1, RunnerStartingLocation.Home, RunnerDestination.Scores,
                null, null, null),
            new(2, RunnerStartingLocation.Third, RunnerDestination.Third,
                null, null, null),
        ]));
        Assert.True(SacrificeFlyCompletionEvaluator.IsSatisfied(
        [
            new(1, RunnerStartingLocation.Home, RunnerDestination.Out,
                null, null, 1),
            new(2, RunnerStartingLocation.Third, RunnerDestination.Scores,
                null, null, null),
        ]));
    }

    [Fact]
    public async Task Persisted_batter_result_values_remain_additive_and_compatible()
    {
        var expected = new Dictionary<BatterResult, int>
        {
            [BatterResult.Single] = 1,
            [BatterResult.Double] = 2,
            [BatterResult.Triple] = 3,
            [BatterResult.HomeRun] = 4,
            [BatterResult.Out] = 5,
            [BatterResult.Walk] = 6,
            [BatterResult.FielderChoice] = 7,
            [BatterResult.DoublePlay] = 8,
            [BatterResult.TriplePlay] = 9,
            [BatterResult.SacrificeFly] = 10,
        };
        Assert.All(expected, item => Assert.Equal(item.Value, (int)item.Key));

        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var initialized = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        foreach (var item in expected)
        {
            await db.ExecuteAsync(
                "INSERT INTO GamePlateAppearance "
                    + "(GameScoringSessionId, PlayerId, BattingPosition, "
                    + "PlateAppearanceNumber, Inning, Result) VALUES (?, ?, ?, ?, ?, ?)",
                initialized.Session!.Id,
                context.Players[0].Id,
                1,
                item.Value,
                1,
                item.Value
            );
        }

        var reloaded = await db.GetGamePlateAppearancesAsync();

        Assert.Equal(expected.Keys, reloaded.Select(item => item.Result!.Value));
    }

    [Theory]
    [InlineData(ScoringBase.First, 0, false)]
    [InlineData(ScoringBase.Second, 0, false)]
    [InlineData(ScoringBase.Third, 0, true)]
    [InlineData(ScoringBase.Third, 1, true)]
    [InlineData(ScoringBase.Third, 2, false)]
    public async Task Sacrifice_fly_selection_requires_third_base_and_fewer_than_two_outs(
        ScoringBase occupiedBase,
        int currentOuts,
        bool expectedSuccess
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1].Id, occupiedBase);
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, currentOuts);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id,
            BatterResult.SacrificeFly
        );

        Assert.Equal(expectedSuccess, selected.IsSuccess);
        Assert.Equal(expectedSuccess,
            (await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay is not null);
    }

    [Fact]
    public async Task Initial_sacrifice_fly_scores_third_holds_other_runners_and_keeps_guard()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id,
            context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id,
            context.Players[3].Id, ScoringBase.Third);
        var lineupBefore = await db.GetGameLineupPlayersAsync();
        var liveBefore = await db.GetGameScoringLineupPlayersAsync();

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id,
            BatterResult.SacrificeFly
        );
        var pendingState = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var pending = pendingState.PendingPlay!;
        var batter = pending.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home
        );
        var runner = pending.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Third
        );
        var firstRunner = pending.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.First
        );

        Assert.True(selected.IsSuccess);
        Assert.Equal(BatterResult.SacrificeFly, pending.Play.Result);
        Assert.Equal(RunnerDestination.Out, batter.Destination);
        Assert.Equal(1, batter.OutOrder);
        Assert.Equal(context.Players[3].Id, runner.PlayerId);
        Assert.Equal(RunnerDestination.Scores, runner.Destination);
        Assert.Equal(RunnerDestination.First, firstRunner.Destination);
        Assert.DoesNotContain(pending.Movements,
            item => item.Destination == RunnerDestination.Third);
        Assert.Equal(0, pendingState.Session.HomeScore);
        Assert.Equal(1, pendingState.Session.CurrentBatterPosition);
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter,
            pendingState.PlayResolution!.Status);
        Assert.Equal(PlayDecisionRequirement.None, pendingState.PlayResolution.DecisionRequirement);

        var batterChoices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id,
            context.Players[0].Id
        );
        Assert.Equal(
            [RunnerDestination.Out],
            batterChoices.Choices.Select(item => item.Destination)
        );
        Assert.False(
            (
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id,
                    context.Players[0].Id,
                    RunnerDestination.First
                )
            ).IsSuccess
        );

        Assert.Equal(1, await db.ExecuteAsync(
            "UPDATE GameScoringPendingRunnerMovement SET Destination = ? "
                + "WHERE GameScoringPendingPlayId = ? AND StartingLocation = ?",
            (int)RunnerDestination.Third,
            pending.Play.Id,
            (int)RunnerStartingLocation.Third));

        var completed = await db.CreateGameScoringSessionRepository()
            .CompletePlateAppearanceAsync(context.Game.Id);
        Assert.Equal(CompletePlateAppearanceFailure.InvalidScoringState, completed.FailureReason);
        Assert.NotNull((await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay);
        Assert.Equal(
            LineupValues(lineupBefore),
            LineupValues(await db.GetGameLineupPlayersAsync())
        );
        Assert.Equal(
            LiveLineupValues(liveBefore),
            LiveLineupValues(await db.GetGameScoringLineupPlayersAsync())
        );
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 0)]
    [InlineData(TeamGameSide.Home, 1)]
    [InlineData(TeamGameSide.Away, 0)]
    [InlineData(TeamGameSide.Away, 1)]
    public async Task Scoring_sacrifice_fly_commits_classification_score_and_one_batter_out(
        TeamGameSide side,
        int currentOuts
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, currentOuts);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);
        var lineupBefore = await db.GetGameLineupPlayersAsync();
        var liveBefore = await db.GetGameScoringLineupPlayersAsync();

        Assert.True(
            (
                await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly)
            ).IsSuccess
        );
        var ready = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, ready.PlayResolution!.Status);

        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var committed = (
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id)
        )!;

        Assert.Equal(BatterResult.SacrificeFly, Assert.Single(committed.PlateAppearances).Result);
        Assert.Equal(currentOuts + 1, committed.Session.CurrentOuts);
        Assert.Equal(side == TeamGameSide.Home ? 1 : 0, committed.Session.HomeScore);
        Assert.Equal(side == TeamGameSide.Away ? 1 : 0, committed.Session.VisitorScore);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
        Assert.Equal(1, committed.Session.CurrentInning);
        Assert.Single(
            committed.RunnerMovements,
            item =>
                item.PlayerId == context.Players[0].Id
                && item.StartingLocation == RunnerStartingLocation.Home
                && item.Destination == RunnerDestination.Out
                && item.OutOrder == 1
        );
        Assert.Single(
            committed.RunnerMovements,
            item =>
                item.PlayerId == context.Players[3].Id
                && item.Destination == RunnerDestination.Scores
                && item.RunCounts == true
        );
        Assert.Equal(LineupValues(lineupBefore),
            LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.Equal(LiveLineupValues(liveBefore),
            LiveLineupValues(await db.GetGameScoringLineupPlayersAsync()));
    }

    [Fact]
    public async Task Runner_only_on_second_cannot_select_sacrifice_fly()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[2].Id, ScoringBase.Second);

        var selected = await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.SacrificeFly);

        Assert.Equal(PendingPlayFailure.InvalidRequest, selected.FailureReason);
        Assert.Null((await repository.GetStateForGameAsync(context.Game.Id))!.PendingPlay);
    }

    [Fact]
    public async Task Multiple_sacrifice_fly_runners_use_normal_resolver_and_final_bases()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id, context.Players[1].Id);
        Assert.Contains(choices.Choices,
            item => item.Destination == RunnerDestination.Second);
        Assert.Contains(choices.Choices,
            item => item.Destination == RunnerDestination.Third);
        Assert.Contains(choices.Choices,
            item => item.Destination == RunnerDestination.Scores);
        Assert.Contains(choices.Choices,
            item => item.Destination == RunnerDestination.Out
                && !item.IsOutAttemptingAdvance);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Second
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var remaining = Assert.Single(committed.BaseRunners);
        Assert.Equal(context.Players[1].Id, remaining.PlayerId);
        Assert.Equal(ScoringBase.Second, remaining.Base);
        Assert.Equal(1, committed.Session.HomeScore);
    }

    [Fact]
    public async Task Changing_sacrifice_fly_to_out_rebuilds_from_committed_bases()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);

        var replaced = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.Out, replaced.PendingPlay!.Play.Result);
        var runner = Assert.Single(replaced.PendingPlay.Movements);
        Assert.Equal(RunnerStartingLocation.Third, runner.StartingLocation);
        Assert.Equal(RunnerDestination.Third, runner.Destination);
        Assert.DoesNotContain(
            replaced.PendingPlay.Movements,
            item => item.StartingLocation == RunnerStartingLocation.Home
        );
        Assert.Equal(0, replaced.Session.HomeScore);
    }

    [Fact]
    public async Task Additional_runner_out_counts_once_and_preserves_sacrifice_fly()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Out
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(BatterResult.SacrificeFly, Assert.Single(committed.PlateAppearances).Result);
        Assert.NotEqual(BatterResult.DoublePlay, committed.PlateAppearances[0].Result);
        Assert.Equal(2, committed.Session.CurrentOuts);
        var outs = committed
            .RunnerMovements.Where(item => item.Destination == RunnerDestination.Out)
            .OrderBy(item => item.OutOrder)
            .ToArray();
        Assert.Equal(
            [context.Players[0].Id, context.Players[1].Id],
            outs.Select(item => item.PlayerId)
        );
        Assert.Equal([1, 2], outs.Select(item => item.OutOrder));
    }

    [Fact]
    public async Task Out_attempting_metadata_and_identity_survive_sacrifice_fly_commit()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Second
        );
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id,
            context.Players[1].Id
        );
        var attemptedThird = Assert.Single(
            choices.Choices,
            item =>
                item.Destination == RunnerDestination.Out
                && item.SafeDestination == RunnerDestination.Second
                && item.AttemptedDestination == RunnerDestination.Third
        );
        Assert.True(
            (
                await repository.AdjustPendingRunnerOutcomeAsync(
                    context.Game.Id,
                    context.Players[1].Id,
                    attemptedThird
                )
            ).IsSuccess
        );
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);

        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;
        var retired = Assert.Single(
            committed.RunnerMovements,
            item => item.PlayerId == context.Players[1].Id
        );
        Assert.Equal(RunnerDestination.Out, retired.Destination);
        Assert.Equal(RunnerDestination.Second, retired.SafeDestination);
        Assert.Equal(RunnerDestination.Third, retired.AttemptedDestination);
        Assert.Equal(2, retired.OutOrder);
        Assert.Equal(BatterResult.SacrificeFly, Assert.Single(committed.PlateAppearances).Result);
    }

    [Fact]
    public async Task Runner_third_out_uses_scorer_ruling_and_transitions_once()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Out
        );
        var pending = (await repository.GetStateForGameAsync(context.Game.Id))!;
        Assert.Equal(
            PlayDecisionRequirement.ThirdOutRuling,
            pending.PlayResolution!.DecisionRequirement
        );
        Assert.Equal([context.Players[3].Id], pending.PlayResolution.DecisionPlayerIds);
        var requirements = await repository.GetRunRulingRequirementsAsync(context.Game.Id);
        Assert.Equal([context.Players[3].Id], requirements.PlayerIds);

        Assert.True(
            (
                await repository.CompletePlateAppearanceAsync(
                    context.Game.Id,
                    new Dictionary<int, bool> { [context.Players[3].Id] = true }
                )
            ).IsSuccess
        );
        var committed = (await repository.GetStateForGameAsync(context.Game.Id))!;

        Assert.Equal(2, committed.Session.CurrentInning);
        Assert.Equal(0, committed.Session.CurrentOuts);
        Assert.Equal(1, committed.Session.HomeScore);
        Assert.Single(committed.HalfInnings);
        Assert.Equal(2, committed.Session.CurrentBatterPosition);
        Assert.True(
            committed
                .RunnerMovements.Single(item => item.PlayerId == context.Players[3].Id)
                .RunCounts
        );
        Assert.Equal(
            2,
            committed
                .RunnerMovements.Single(item => item.PlayerId == context.Players[1].Id)
                .OutOrder
        );
        Assert.False((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        Assert.Single((await repository.GetStateForGameAsync(context.Game.Id))!.HalfInnings);
    }

    [Fact]
    public async Task Sacrifice_fly_never_allows_a_fourth_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 1);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[2].Id, ScoringBase.Second);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Out
        );
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id,
            context.Players[2].Id
        );

        Assert.DoesNotContain(choices.Choices, item => item.Destination == RunnerDestination.Out);
        Assert.Equal(
            PendingPlayFailure.DuplicateDestination,
            (
                await repository.AdjustPendingRunnerAsync(
                    context.Game.Id,
                    context.Players[2].Id,
                    RunnerDestination.Out
                )
            ).FailureReason
        );
    }

    [Fact]
    public async Task Pending_sacrifice_fly_rehydrates_exact_persisted_out_attempting_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Away
        );
        await SeedBaseAsync(db, initialized.Session!.Id, context.Players[1].Id, ScoringBase.First);
        await SeedBaseAsync(db, initialized.Session.Id, context.Players[3].Id, ScoringBase.Third);

        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.SacrificeFly);
        await repository.AdjustPendingRunnerAsync(
            context.Game.Id,
            context.Players[1].Id,
            RunnerDestination.Second
        );
        var choices = await repository.GetValidRunnerOutcomeChoicesAsync(
            context.Game.Id,
            context.Players[1].Id
        );
        var attempted = Assert.Single(
            choices.Choices,
            item =>
                item.SafeDestination == RunnerDestination.Second
                && item.AttemptedDestination == RunnerDestination.Third
        );
        await repository.AdjustPendingRunnerOutcomeAsync(
            context.Game.Id,
            context.Players[1].Id,
            attempted
        );

        var resumed = (
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id)
        )!;
        var pending = resumed.PendingPlay!;
        var batter = pending.Movements.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home
        );
        var scored = pending.Movements.Single(item => item.PlayerId == context.Players[3].Id);
        var retired = pending.Movements.Single(item => item.PlayerId == context.Players[1].Id);

        Assert.Equal(BatterResult.SacrificeFly, pending.Play.Result);
        Assert.Equal((RunnerDestination.Out, 1), (batter.Destination, batter.OutOrder));
        Assert.Equal(RunnerDestination.Scores, scored.Destination);
        Assert.Equal(
            (RunnerDestination.Out, RunnerDestination.Second, RunnerDestination.Third, 2),
            (
                retired.Destination,
                retired.SafeDestination,
                retired.AttemptedDestination,
                retired.OutOrder
            )
        );
        Assert.Equal(PlayResolutionStatus.ReadyForNextBatter, resumed.PlayResolution!.Status);
        Assert.Equal(
            (0, 0, 0, 1, 1),
            (
                resumed.Session.HomeScore,
                resumed.Session.VisitorScore,
                resumed.Session.CurrentOuts,
                resumed.Session.CurrentInning,
                resumed.Session.CurrentBatterPosition
            )
        );
    }

    private static async Task SeedBaseAsync(
        TemporaryTestDatabase db,
        int sessionId,
        int playerId,
        ScoringBase scoringBase
    ) =>
        Assert.Equal(
            1,
            await db.SeedBaseRunnerAsync(
                new()
                {
                    GameScoringSessionId = sessionId,
                    PlayerId = playerId,
                    Base = scoringBase,
                }
            )
        );

    private static IReadOnlyList<(int GameId, int PlayerId, int Order)> LineupValues(
        IEnumerable<GameLineupPlayer> items
    ) => items.Select(item => (item.GameId, item.PlayerId, item.BattingOrder)).ToArray();

    private static IReadOnlyList<(int SessionId, int PlayerId, int Position)> LiveLineupValues(
        IEnumerable<GameScoringLineupPlayer> items
    ) =>
        items
            .Select(item => (item.GameScoringSessionId, item.PlayerId, item.BattingPosition))
            .ToArray();

    private static async Task<(
        Team Team,
        Game Game,
        IReadOnlyList<Player> Players
    )> CreateReadyGameAsync(TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season
        {
            TeamId = team.Id,
            Name = "Season",
            Year = 2026,
        };
        await db.SeedSeasonAsync(season);
        var game = new Game
        {
            TeamId = team.Id,
            SeasonId = season.Id,
            ScheduledDate = DateTime.Today,
            ScheduledTime = TimeSpan.FromHours(18),
        };
        await db.SeedGameAsync(game);
        var players = new List<Player>();
        for (var index = 0; index < playerCount; index++)
        {
            var player = new Player
            {
                FirstName = $"Player{index + 1}",
                LastName = "Test",
                IsActive = true,
            };
            await db.CreatePlayerRepository().CreateAsync(player);
            await db.SeedTeamPlayerAsync(
                new()
                {
                    TeamId = team.Id,
                    PlayerId = player.Id,
                    IsActive = true,
                }
            );
            await db.SeedGameLineupPlayerAsync(
                new()
                {
                    GameId = game.Id,
                    PlayerId = player.Id,
                    BattingOrder = index + 1,
                }
            );
            players.Add(player);
        }
        return (team, game, players);
    }
}
