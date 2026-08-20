using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringOutRepositoryTests
{
    [Fact]
    public void Opponent_half_inning_dialog_static_color_resources_exist()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dialogPath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "OpponentHalfInningDialog.xaml");
        var colorsPath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Resources",
            "Styles", "Colors.xaml");
        var dialog = File.ReadAllText(dialogPath);
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var colorKeys = XDocument.Load(colorsPath).Descendants()
            .Select(element => element.Attribute(xNamespace + "Key")?.Value)
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);
        var referencedColors = Regex.Matches(dialog,
                @"StaticResource\s+([A-Za-z][A-Za-z0-9_]*)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

        Assert.All(referencedColors, key => Assert.Contains(key, colorKeys));
    }

    [Fact]
    public void Scoring_view_model_preserves_context_for_empty_modal_navigation_query()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var viewModelPath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "ViewModels",
            "ScoringViewModel.cs");
        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Matches(
            @"if\s*\(\s*!hasTeamId\s*&&\s*!hasGameId\s*\)\s*return\s*;",
            viewModel);
    }

    [Fact]
    public void Scored_runner_buttons_display_player_names_in_both_layouts()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var pagePath = Path.Combine(repositoryRoot, "SoftballScorekeeper", "Views",
            "ScoringPage.xaml");
        var page = XDocument.Load(pagePath);
        var presentationNamespace = page.Root!.Name.Namespace;
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var templates = page.Descendants(presentationNamespace + "DataTemplate")
            .Where(element => element.Attribute(xNamespace + "DataType")?.Value
                == "viewModels:ScoredPlayerViewModel")
            .ToList();

        Assert.Equal(2, templates.Count);
        Assert.All(templates, template =>
        {
            var button = Assert.Single(
                template.Descendants(presentationNamespace + "Button"));
            Assert.Equal("{Binding PlayerName}", button.Attribute("Text")?.Value);
            Assert.Equal("OnRunnerButtonClicked", button.Attribute("Clicked")?.Value);
        });
    }

    [Theory]
    [InlineData(TeamGameSide.Away)]
    [InlineData(TeamGameSide.Home)]
    public async Task New_session_starts_first_inning_with_no_outs_and_preserves_team_side(
        TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var initialized = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, side);

        Assert.True(initialized.IsSuccess);
        Assert.Equal((side, 1, 0), (initialized.Session!.TeamSide,
            initialized.Session.CurrentInning, initialized.Session.CurrentOuts));
    }

    [Fact]
    public async Task Out_with_empty_bases_completes_immediately()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        var selected = await repository.SelectOutAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Out, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal((1, 1, 2), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 1, 0)]
    [InlineData(TeamGameSide.Away, 0, 1)]
    public async Task Runner_may_score_on_pending_out_with_authoritative_score_mapping(
        TeamGameSide side, int expectedHomeScore, int expectedVisitorScore)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, side);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "3");

        var selected = await repository.SelectOutAsync(context.Game.Id);
        Assert.True(selected.IsSuccess);
        Assert.Equal(RunnerDestination.Third,
            Assert.Single(selected.PendingPlay!.Movements).Destination);
        Assert.True((await repository.AdjustPendingRunnerAsync(context.Game.Id,
            context.Players[3].Id, RunnerDestination.Scores)).IsSuccess);

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Out, completed.PlateAppearance!.Result);
        Assert.Equal((1, 2, expectedHomeScore, expectedVisitorScore),
            (state!.Session.CurrentOuts, state.Session.CurrentBatterPosition,
                state.Session.HomeScore, state.Session.VisitorScore));
        var movement = Assert.Single(state.RunnerMovements);
        Assert.Equal((RunnerStartingLocation.Third, RunnerDestination.Scores),
            (movement.StartingLocation, movement.Destination));
        Assert.Empty(state.BaseRunners);
    }

    [Fact]
    public async Task Runner_may_advance_on_pending_out_and_movement_persists()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "2");

        Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
        Assert.True((await repository.AdjustPendingRunnerAsync(context.Game.Id,
            context.Players[2].Id, RunnerDestination.Third)).IsSuccess);
        Assert.True((await repository.CompletePlateAppearanceAsync(context.Game.Id)).IsSuccess);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        var runner = Assert.Single(state!.BaseRunners);
        Assert.Equal((ScoringBase.Third, context.Players[2].Id),
            (runner.Base, runner.PlayerId));
        var movement = Assert.Single(state.RunnerMovements);
        Assert.Equal((RunnerStartingLocation.Second, RunnerDestination.Third),
            (movement.StartingLocation, movement.Destination));
    }

    [Theory]
    [InlineData(0, 1, "1")]
    [InlineData(1, 2, "2,3")]
    public async Task Out_with_runners_is_pending_then_persists_PA_advances_batter_and_preserves_bases(
        int startingOuts, int expectedOuts, string occupiedBases)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 5, 7, 1);
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 4, startingOuts);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, occupiedBases);

        var selected = await repository.SelectOutAsync(context.Game.Id);
        var pendingState = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.NotNull(selected.PendingPlay);
        Assert.Empty(pendingState!.PlateAppearances);
        Assert.Equal((startingOuts, 1), (pendingState.Session.CurrentOuts,
            pendingState.Session.CurrentBatterPosition));
        Assert.Equal(BaseValues(occupiedBases, context.Players),
            BaseValues(pendingState.BaseRunners));
        Assert.All(pendingState.PendingPlay!.Movements, movement =>
            Assert.Equal((int)movement.StartingLocation, (int)movement.Destination));

        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Out, completed.PlateAppearance!.Result);
        Assert.Equal(context.Players[0].Id, completed.PlateAppearance.PlayerId);
        Assert.Equal(occupiedBases.Split(',').Length, state!.RunnerMovements.Count);
        Assert.Equal((4, expectedOuts, 2, 5, 7), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition,
            state.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(BaseValues(occupiedBases, context.Players),
            BaseValues(state.BaseRunners));
    }

    [Theory]
    [InlineData(TeamGameSide.Away)]
    [InlineData(TeamGameSide.Home)]
    public async Task Third_out_advances_our_next_inning_clears_bases_and_continues_batting_order(
        TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 3, 6, 3);
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 1, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, "1,2,3");
        var frozen = (await db.GetGameLineupPlayersAsync()).Select(FrozenRow).ToArray();
        var live = (await db.GetGameScoringLineupPlayersAsync()).Select(LiveRow).ToArray();

        var selected = await repository.SelectOutAsync(context.Game.Id);
        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        var reloaded = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(BatterResult.Out, Assert.Single(reloaded!.PlateAppearances).Result);
        Assert.Equal((2, 0, 4, 3, 6), (reloaded!.Session.CurrentInning,
            reloaded.Session.CurrentOuts, reloaded.Session.CurrentBatterPosition,
            reloaded.Session.HomeScore, reloaded.Session.VisitorScore));
        Assert.Empty(reloaded.BaseRunners);
        Assert.Equal(frozen, (await db.GetGameLineupPlayersAsync()).Select(FrozenRow));
        Assert.Equal(live, (await db.GetGameScoringLineupPlayersAsync()).Select(LiveRow));
    }

    [Fact]
    public async Task One_player_lineup_wraps_each_out_and_third_out_advances_inning()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        await repository.CompleteOutAsync(context.Game.Id);
        await repository.CompleteOutAsync(context.Game.Id);
        await repository.CompleteOutAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal((2, 0, 1), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Equal(3, state.PlateAppearances.Count);
        Assert.All(state.PlateAppearances, item => Assert.Equal(BatterResult.Out, item.Result));
    }

    [Theory]
    [InlineData(BatterResult.Single)]
    [InlineData(BatterResult.Double)]
    [InlineData(BatterResult.Triple)]
    [InlineData(BatterResult.HomeRun)]
    public async Task Existing_hit_results_do_not_change_inning_or_outs(BatterResult result)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 5);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 4, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, "1,2,3");

        var completed = result == BatterResult.HomeRun
            ? await repository.CompleteHomeRunAsync(context.Game.Id)
            : await CompleteHitAsync(repository, context.Game.Id, result);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal((4, 2), (state!.Session.CurrentInning, state.Session.CurrentOuts));
        if (result == BatterResult.HomeRun)
        {
            Assert.Equal(4, completed.RunsScored);
            Assert.Empty(state.BaseRunners);
        }
    }

    [Theory]
    [InlineData(BatterResult.Single, false)]
    [InlineData(BatterResult.Double, false)]
    [InlineData(BatterResult.Triple, false)]
    [InlineData(BatterResult.Single, true)]
    public async Task Out_replaces_pending_hit_and_corrections_from_committed_base_truth(
        BatterResult pendingResult, bool correctPendingRunner)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await SeedBasesAsync(db, initialized.Session!.Id, context.Players, "1");
        await repository.SelectBatterResultAsync(context.Game.Id, pendingResult);
        if (correctPendingRunner)
            await repository.AdjustPendingRunnerAsync(
                context.Game.Id, context.Players[1].Id, RunnerDestination.Scores);

        var selected = await repository.SelectOutAsync(context.Game.Id);
        Assert.True(selected.IsSuccess);
        Assert.NotNull(selected.PendingPlay);
        var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Equal(BatterResult.Out, Assert.Single(state!.PlateAppearances).Result);
        Assert.Null(state.PendingPlay);
        Assert.Equal(BaseValues("1", context.Players), BaseValues(state.BaseRunners));
        Assert.Equal((1, 0), (state.Session.CurrentOuts, state.Session.HomeScore));
    }

    [Fact]
    public async Task Pending_hit_replaced_by_out_with_empty_bases_completes_immediately()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Double)).IsSuccess);

        var selected = await repository.SelectOutAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Out, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal((1, 2), (state.Session.CurrentOuts,
            state.Session.CurrentBatterPosition));
    }

    [Fact]
    public async Task Pending_hit_replaced_by_out_with_two_outs_completes_third_out()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 1, 2);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, "1");
        Assert.True((await repository.SelectBatterResultAsync(
            context.Game.Id, BatterResult.Triple)).IsSuccess);

        var selected = await repository.SelectOutAsync(context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(selected.IsSuccess);
        Assert.Null(selected.PendingPlay);
        Assert.Null(state!.PendingPlay);
        Assert.Equal(BatterResult.Out, Assert.Single(state.PlateAppearances).Result);
        Assert.Equal((2, 0, 2), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Empty(state.BaseRunners);
        Assert.Empty(state.RunnerMovements);
        Assert.Single(state.HalfInnings);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Failed_out_rolls_back_PA_outs_inning_bases_batter_score_and_pending(int outs)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await db.UpdateScoringStateAsync(initialized.Session!.Id, 4, 8, 1);
        await db.UpdateInningOutStateAsync(initialized.Session.Id, 7, outs);
        await SeedBasesAsync(db, initialized.Session.Id, context.Players, "2");
        await repository.SelectBatterResultAsync(context.Game.Id, BatterResult.Single);
        await db.ExecuteAsync(
            "CREATE TRIGGER FailOutUpdate BEFORE UPDATE OF CurrentOuts ON GameScoringSession BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        bool failedAsExpected;
        if (outs == 1)
        {
            Assert.True((await repository.SelectOutAsync(context.Game.Id)).IsSuccess);
            var completed = await repository.CompletePlateAppearanceAsync(context.Game.Id);
            failedAsExpected = completed.FailureReason
                == CompletePlateAppearanceFailure.Persistence;
        }
        else
        {
            var selected = await repository.SelectOutAsync(context.Game.Id);
            failedAsExpected = selected.FailureReason == PendingPlayFailure.Persistence;
        }
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(failedAsExpected);
        Assert.Empty(state!.PlateAppearances);
        Assert.Equal((7, outs, 1, 4, 8), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition,
            state.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(BaseValues("2", context.Players), BaseValues(state.BaseRunners));
        Assert.NotNull(state.PendingPlay);
        Assert.Equal(outs == 1 ? BatterResult.Out : BatterResult.Single,
            state.PendingPlay.Play.Result);
        Assert.Empty(state.HalfInnings);
    }

    [Fact]
    public async Task Extra_innings_have_no_maximum_and_reload_does_not_repeat_transition()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var initialized = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(initialized.Session!.Id, 10, 2);

        await repository.CompleteOutAsync(context.Game.Id);
        var firstLoad = await repository.GetStateForGameAsync(context.Game.Id);
        var secondLoad = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal((11, 0, 2), (firstLoad!.Session.CurrentInning,
            firstLoad.Session.CurrentOuts, firstLoad.Session.CurrentBatterPosition));
        Assert.Equal((11, 0, 2), (secondLoad!.Session.CurrentInning,
            secondLoad.Session.CurrentOuts, secondLoad.Session.CurrentBatterPosition));
    }

    [Fact]
    public async Task Out_and_third_out_state_are_isolated_between_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, 2);
        var second = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        var firstSession = await repository.InitializeAsync(
            first.Team.Id, first.Game.Id, TeamGameSide.Away);
        await repository.InitializeAsync(second.Team.Id, second.Game.Id, TeamGameSide.Home);
        await db.UpdateInningOutStateAsync(firstSession.Session!.Id, 3, 2);
        await SeedBasesAsync(db, firstSession.Session.Id, first.Players, "1");

        await repository.CompleteOutAsync(first.Game.Id);
        var firstState = await repository.GetStateForGameAsync(first.Game.Id);
        var secondState = await repository.GetStateForGameAsync(second.Game.Id);

        Assert.Equal((4, 0), (firstState!.Session.CurrentInning, firstState.Session.CurrentOuts));
        Assert.Empty(firstState.BaseRunners);
        Assert.Equal((1, 0, 1), (secondState!.Session.CurrentInning,
            secondState.Session.CurrentOuts, secondState.Session.CurrentBatterPosition));
    }

    [Theory]
    [InlineData(TeamGameSide.Away)]
    [InlineData(TeamGameSide.Home)]
    public async Task Half_inning_summary_uses_only_completed_half_PA_history(TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 4);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);

        Assert.True((await repository.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var first = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);

        Assert.NotNull(first);
        Assert.Equal((1, 1, 1), (first.Inning, first.TeamRuns, first.TeamHomeRuns));
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            context.Game.Id, first.Id));
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, first.Id, null, null)).IsSuccess);

        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var state = await repository.GetStateForGameAsync(context.Game.Id);
        var second = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);

        Assert.NotNull(second);
        Assert.Equal((2, 0, 0), (second.Inning, second.TeamRuns, second.TeamHomeRuns));
        Assert.Equal(new[] { 1, 1, 1, 1 }, state!.PlateAppearances
            .Where(item => item.Inning == 1).Select(item => item.Inning).ToArray());
        Assert.Equal(3, state.PlateAppearances.Count(item => item.Inning == 2));
    }

    [Fact]
    public async Task Blank_opponent_entry_completes_transition_without_score_change()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var half = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);

        var blocked = await repository.SelectOutAsync(context.Game.Id);
        Assert.Equal(PendingPlayFailure.InvalidScoringState, blocked.FailureReason);

        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            context.Game.Id, half!.Id));
        var completed = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id, null, null);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Null(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Equal((0, 0), (state!.Session.HomeScore, state.Session.VisitorScore));
        var saved = Assert.Single(state.HalfInnings);
        Assert.True(saved.TeamSummaryAcknowledged);
        Assert.True(saved.OpponentEntryCompleted);
        Assert.Null(saved.OpponentRuns);
        Assert.Null(saved.OpponentHomeRuns);
    }

    [Theory]
    [InlineData(null, null, null, null, 0)]
    [InlineData(3, null, 3, null, 3)]
    [InlineData(null, 2, null, 2, 0)]
    [InlineData(3, 2, 3, 2, 3)]
    [InlineData(0, 0, 0, 0, 0)]
    public async Task Opponent_entry_preserves_optional_values_and_reloads_completed_transition(
        int? runs, int? homeRuns, int? expectedRuns, int? expectedHomeRuns,
        int expectedVisitorScore)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var half = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);
        var beforeSubmission = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(2, beforeSubmission!.Session.CurrentInning);
        Assert.Equal(1, half!.Inning);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            context.Game.Id, half.Id));

        var completed = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id, runs, homeRuns);
        var reloaded = await new GameScoringSessionRepository(db.Database)
            .GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.Null(await repository.GetPendingHalfInningTransitionAsync(context.Game.Id));
        Assert.Equal((0, expectedVisitorScore),
            (reloaded!.Session.HomeScore, reloaded.Session.VisitorScore));
        var saved = Assert.Single(reloaded.HalfInnings);
        Assert.Equal((expectedRuns, expectedHomeRuns, true),
            (saved.OpponentRuns, saved.OpponentHomeRuns, saved.OpponentEntryCompleted));
    }

    [Theory]
    [InlineData(OpponentEntryMode.Total, OpponentEntryMode.Total)]
    [InlineData(OpponentEntryMode.ThisHalf, OpponentEntryMode.ThisHalf)]
    [InlineData(OpponentEntryMode.Total, OpponentEntryMode.ThisHalf)]
    [InlineData(OpponentEntryMode.ThisHalf, OpponentEntryMode.Total)]
    public async Task Opponent_entry_modes_derive_identical_half_and_cumulative_values(
        OpponentEntryMode runsMode, OpponentEntryMode homeRunsMode)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            runsMode == OpponentEntryMode.Total ? 6 : 4, runsMode,
            homeRunsMode == OpponentEntryMode.Total ? 3 : 1, homeRunsMode);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.Equal((4, 1), (saved.OpponentRuns, saved.OpponentHomeRuns));
        Assert.Equal(3, state.HalfInnings
            .Where(item => item.OpponentEntryCompleted)
            .Sum(item => item.OpponentHomeRuns ?? 0));
    }

    [Fact]
    public async Task Opponent_total_equal_to_current_persists_explicit_zero_half_values()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            2, OpponentEntryMode.Total,
            2, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.Equal((0, 0), (saved.OpponentRuns, saved.OpponentHomeRuns));
    }

    [Fact]
    public async Task Opponent_this_half_zero_persists_explicit_zero_without_changing_totals()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            0, OpponentEntryMode.ThisHalf,
            0, OpponentEntryMode.ThisHalf);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.Equal((0, 0), (saved.OpponentRuns, saved.OpponentHomeRuns));
        Assert.Equal(2, state.HalfInnings.Sum(item => item.OpponentHomeRuns ?? 0));
    }

    [Fact]
    public async Task Opponent_blank_values_remain_null_without_changing_totals()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            null, OpponentEntryMode.Total,
            null, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.Equal((null, null), (saved.OpponentRuns, saved.OpponentHomeRuns));
    }

    [Theory]
    [InlineData(null, OpponentEntryMode.Total, 3, OpponentEntryMode.Total, 2, null, 1)]
    [InlineData(6, OpponentEntryMode.Total, null, OpponentEntryMode.ThisHalf, 6, 4, null)]
    public async Task Opponent_runs_and_home_runs_remain_independently_optional(
        int? runs, OpponentEntryMode runsMode,
        int? homeRuns, OpponentEntryMode homeRunsMode,
        int expectedScore, int? expectedHalfRuns, int? expectedHalfHomeRuns)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id, runs, runsMode, homeRuns, homeRunsMode);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedScore, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.Equal((expectedHalfRuns, expectedHalfHomeRuns),
            (saved.OpponentRuns, saved.OpponentHomeRuns));
    }

    [Fact]
    public async Task Opponent_total_score_below_current_is_rejected_without_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            1, OpponentEntryMode.Total,
            3, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(OpponentHalfInningEntryFailure.TotalRunsBelowCurrent,
            result.FailureReason);
        Assert.Equal(2, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.False(saved.OpponentEntryCompleted);
        Assert.Equal((null, null), (saved.OpponentRuns, saved.OpponentHomeRuns));
        Assert.Equal(half.Id,
            (await repository.GetPendingHalfInningTransitionAsync(context.Game.Id))!.Id);
    }

    [Fact]
    public async Task Opponent_total_home_runs_below_current_is_rejected_without_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            6, OpponentEntryMode.Total,
            1, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.Equal(OpponentHalfInningEntryFailure.TotalHomeRunsBelowCurrent,
            result.FailureReason);
        Assert.Equal(2, state!.Session.VisitorScore);
        var saved = state.HalfInnings.Single(item => item.Id == half.Id);
        Assert.False(saved.OpponentEntryCompleted);
        Assert.Equal((null, null), (saved.OpponentRuns, saved.OpponentHomeRuns));
        Assert.Equal(half.Id,
            (await repository.GetPendingHalfInningTransitionAsync(context.Game.Id))!.Id);
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 0, 6)]
    [InlineData(TeamGameSide.Away, 6, 0)]
    public async Task Opponent_total_score_updates_the_correct_home_or_visitor_score(
        TeamGameSide side, int expectedHomeScore, int expectedVisitorScore)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        var half = await PrepareSecondOpponentHalfAsync(repository, context.Game.Id);

        var result = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            6, OpponentEntryMode.Total,
            null, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal((expectedHomeScore, expectedVisitorScore),
            (state!.Session.HomeScore, state.Session.VisitorScore));
        Assert.Equal(4, state.HalfInnings.Single(item => item.Id == half.Id).OpponentRuns);
    }

    [Theory]
    [InlineData(TeamGameSide.Home, 0, 3)]
    [InlineData(TeamGameSide.Away, 3, 0)]
    public async Task Opponent_runs_and_home_runs_persist_with_score_mapping_and_inning_isolation(
        TeamGameSide side, int expectedHomeScore, int expectedVisitorScore)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, side);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var first = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            context.Game.Id, first!.Id));

        var completed = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, first.Id, 3, 2);
        var repeated = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, first.Id, 9, 7);
        var resumed = await new GameScoringSessionRepository(db.Database)
            .GetStateForGameAsync(context.Game.Id);

        Assert.True(completed.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal((expectedHomeScore, expectedVisitorScore),
            (resumed!.Session.HomeScore, resumed.Session.VisitorScore));
        var savedFirst = Assert.Single(resumed.HalfInnings);
        Assert.Equal((1, 3, 2, true), (savedFirst.Inning,
            savedFirst.OpponentRuns, savedFirst.OpponentHomeRuns,
            savedFirst.OpponentEntryCompleted));

        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var withSecond = await repository.GetStateForGameAsync(context.Game.Id);
        Assert.Equal(2, withSecond!.HalfInnings.Count);
        var savedSecond = withSecond.HalfInnings.Single(item => item.Inning == 2);
        Assert.Null(savedSecond.OpponentRuns);
        Assert.Null(savedSecond.OpponentHomeRuns);
        Assert.False(savedSecond.OpponentEntryCompleted);
    }

    [Fact]
    public async Task Failed_opponent_entry_rolls_back_score_and_half_inning_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 2);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var half = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(
            context.Game.Id, half!.Id));
        await db.ExecuteAsync(
            "CREATE TRIGGER FailOpponentEntry BEFORE UPDATE OF OpponentEntryCompleted ON GameScoringHalfInning BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var completed = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half.Id,
            4, OpponentEntryMode.Total,
            2, OpponentEntryMode.Total);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.False(completed.IsSuccess);
        Assert.Equal((0, 0), (state!.Session.HomeScore, state.Session.VisitorScore));
        var saved = Assert.Single(state.HalfInnings);
        Assert.False(saved.OpponentEntryCompleted);
        Assert.Null(saved.OpponentRuns);
        Assert.Null(saved.OpponentHomeRuns);
        Assert.Equal(half.Id,
            (await repository.GetPendingHalfInningTransitionAsync(context.Game.Id))!.Id);
    }

    [Fact]
    public async Task Negative_opponent_values_are_rejected_without_changes()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, 1);
        var repository = db.CreateGameScoringSessionRepository();
        await repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await CompleteThreeEmptyBaseOutsAsync(repository, context.Game.Id);
        var half = await repository.GetPendingHalfInningTransitionAsync(context.Game.Id);

        var completed = await repository.CompleteOpponentHalfInningAsync(
            context.Game.Id, half!.Id, -1, 0);
        var state = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.False(completed.IsSuccess);
        Assert.Equal((0, 0), (state!.Session.HomeScore, state.Session.VisitorScore));
        Assert.False(Assert.Single(state.HalfInnings).OpponentEntryCompleted);
    }

    [Fact]
    public async Task Story63_session_upgrade_defaults_inning_outs_and_preserves_scoring_state()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        const int gameId = 77;
        await db.UseConnectionAsync(async connection =>
        {
            await connection.CreateTableAsync<LegacyStory63ScoringSession>();
            await connection.CreateTableAsync<Player>();
            await connection.CreateTableAsync<GameScoringLineupPlayer>();
            await connection.CreateTableAsync<GamePlateAppearance>();
            await connection.CreateTableAsync<GameScoringBaseRunner>();
            await connection.CreateTableAsync<GameScoringPendingPlay>();
            await connection.CreateTableAsync<GameScoringPendingRunnerMovement>();
            var first = new Player { FirstName = "Legacy", LastName = "One", IsActive = true };
            var second = new Player { FirstName = "Legacy", LastName = "Two", IsActive = true };
            await connection.InsertAsync(first);
            await connection.InsertAsync(second);
            var session = new LegacyStory63ScoringSession
            {
                GameId = gameId,
                Status = GameScoringStatus.InProgress,
                TeamSide = TeamGameSide.Away,
                HomeScore = 4,
                VisitorScore = 6,
                CurrentBatterPosition = 2,
            };
            await connection.InsertAsync(session);
            await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id, PlayerId = first.Id, BattingPosition = 1,
            });
            await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id, PlayerId = second.Id, BattingPosition = 2,
            });
            await connection.InsertAsync(new GameScoringBaseRunner
            {
                GameScoringSessionId = session.Id, PlayerId = first.Id, Base = ScoringBase.First,
            });
            await connection.InsertAsync(new GamePlateAppearance
            {
                GameScoringSessionId = session.Id, PlayerId = first.Id,
                BattingPosition = 1, PlateAppearanceNumber = 1, Result = BatterResult.Single,
            });
            var pending = new GameScoringPendingPlay
            {
                GameScoringSessionId = session.Id, BatterPlayerId = second.Id,
                BatterPosition = 2, Result = BatterResult.Double,
            };
            await connection.InsertAsync(pending);
            await connection.InsertAsync(new GameScoringPendingRunnerMovement
            {
                GameScoringPendingPlayId = pending.Id, PlayerId = first.Id,
                StartingLocation = RunnerStartingLocation.First,
                Destination = RunnerDestination.Third,
            });
            await connection.InsertAsync(new GameScoringPendingRunnerMovement
            {
                GameScoringPendingPlayId = pending.Id, PlayerId = second.Id,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = RunnerDestination.Second,
            });
            return 0;
        });

        await db.Database.InitializeAsync();
        await db.Database.CloseAsync();
        var reopenedDatabase = new AppDatabase(db.DatabasePath);
        await reopenedDatabase.InitializeAsync();
        var state = await new GameScoringSessionRepository(reopenedDatabase)
            .GetStateForGameAsync(gameId);
        await reopenedDatabase.CloseAsync();

        Assert.NotNull(state);
        Assert.Equal((1, 0, 4, 6, 2), (state.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.HomeScore, state.Session.VisitorScore,
            state.Session.CurrentBatterPosition));
        Assert.Single(state.BaseRunners);
        Assert.Single(state.PlateAppearances);
        Assert.Equal(BatterResult.Single, state.PlateAppearances[0].Result);
        Assert.Equal(1, state.PlateAppearances[0].Inning);
        Assert.Empty(state.HalfInnings);
        Assert.NotNull(state.PendingPlay);
        Assert.Equal(BatterResult.Double, state.PendingPlay.Play.Result);
        Assert.Equal(2, state.PendingPlay.Movements.Count);
    }

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(3, 2, 3)]
    public async Task Earlier_schema_upgrade_can_complete_third_out_summary_and_opponent_transition(
        int? opponentRuns, int? opponentHomeRuns, int expectedHomeScore)
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        const int gameId = 91;
        await db.UseConnectionAsync(async connection =>
        {
            await connection.CreateTableAsync<LegacyEarly64ScoringSession>();
            await connection.CreateTableAsync<Player>();
            await connection.CreateTableAsync<GameScoringLineupPlayer>();
            await connection.CreateTableAsync<LegacyEarly64PlateAppearance>();
            await connection.CreateTableAsync<LegacyEarly64RunnerMovement>();
            await connection.CreateTableAsync<LegacyEarly64HalfInning>();
            await connection.CreateTableAsync<GameScoringBaseRunner>();
            await connection.CreateTableAsync<GameScoringPendingPlay>();
            await connection.CreateTableAsync<GameScoringPendingRunnerMovement>();
            var first = new Player { FirstName = "Upgrade", LastName = "One", IsActive = true };
            var second = new Player { FirstName = "Upgrade", LastName = "Two", IsActive = true };
            await connection.InsertAsync(first);
            await connection.InsertAsync(second);
            var session = new LegacyEarly64ScoringSession
            {
                GameId = gameId,
                Status = GameScoringStatus.InProgress,
                TeamSide = TeamGameSide.Away,
                VisitorScore = 1,
                CurrentBatterPosition = 2,
                CurrentInning = 1,
                CurrentOuts = 2,
            };
            await connection.InsertAsync(session);
            await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id, PlayerId = first.Id, BattingPosition = 1,
            });
            await connection.InsertAsync(new GameScoringLineupPlayer
            {
                GameScoringSessionId = session.Id, PlayerId = second.Id, BattingPosition = 2,
            });
            var homeRun = new LegacyEarly64PlateAppearance
            {
                GameScoringSessionId = session.Id,
                PlayerId = first.Id,
                BattingPosition = 1,
                PlateAppearanceNumber = 1,
                Result = BatterResult.HomeRun,
            };
            await connection.InsertAsync(homeRun);
            await connection.InsertAsync(new LegacyEarly64RunnerMovement
            {
                GamePlateAppearanceId = homeRun.Id,
                PlayerId = first.Id,
                StartingLocation = RunnerStartingLocation.Home,
                Destination = RunnerDestination.Scores,
            });
            return 0;
        });

        await db.Database.InitializeAsync();
        await db.Database.CloseAsync();
        var upgradedDatabase = new AppDatabase(db.DatabasePath);
        await upgradedDatabase.InitializeAsync();
        var repository = new GameScoringSessionRepository(upgradedDatabase);
        var selected = await repository.SelectOutAsync(gameId);
        var half = await repository.GetPendingHalfInningTransitionAsync(gameId);

        Assert.True(selected.IsSuccess);
        Assert.NotNull(half);
        Assert.Equal((1, 1, 1), (half.Inning, half.TeamRuns, half.TeamHomeRuns));
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(gameId, half.Id));
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            gameId, half.Id, opponentRuns, opponentHomeRuns)).IsSuccess);

        await upgradedDatabase.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        await reopened.InitializeAsync();
        var state = await new GameScoringSessionRepository(reopened)
            .GetStateForGameAsync(gameId);
        await reopened.CloseAsync();

        Assert.Equal((2, 0, 1), (state!.Session.CurrentInning,
            state.Session.CurrentOuts, state.Session.CurrentBatterPosition));
        Assert.Equal((expectedHomeScore, 1),
            (state.Session.HomeScore, state.Session.VisitorScore));
        var saved = Assert.Single(state.HalfInnings);
        Assert.True(saved.TeamSummaryAcknowledged);
        Assert.True(saved.OpponentEntryCompleted);
        Assert.Equal((opponentRuns, opponentHomeRuns),
            (saved.OpponentRuns, saved.OpponentHomeRuns));
        Assert.Equal(1, state.PlateAppearances[0].Inning);
        Assert.Equal(true, state.RunnerMovements[0].RunCounts);
    }

    private static async Task<CompletePlateAppearanceResult> CompleteHitAsync(
        GameScoringSessionRepository repository, int gameId, BatterResult result)
    {
        Assert.True((await repository.SelectBatterResultAsync(gameId, result)).IsSuccess);
        return await repository.CompletePlateAppearanceAsync(gameId);
    }

    private static async Task CompleteThreeEmptyBaseOutsAsync(
        GameScoringSessionRepository repository, int gameId)
    {
        for (var index = 0; index < 3; index++)
            Assert.True((await repository.SelectOutAsync(gameId)).IsSuccess);
    }

    private static async Task<GameScoringHalfInning> PrepareSecondOpponentHalfAsync(
        GameScoringSessionRepository repository, int gameId)
    {
        await CompleteThreeEmptyBaseOutsAsync(repository, gameId);
        var first = await repository.GetPendingHalfInningTransitionAsync(gameId);
        Assert.NotNull(first);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(gameId, first.Id));
        Assert.True((await repository.CompleteOpponentHalfInningAsync(
            gameId, first.Id, 2, 2)).IsSuccess);

        await CompleteThreeEmptyBaseOutsAsync(repository, gameId);
        var second = await repository.GetPendingHalfInningTransitionAsync(gameId);
        Assert.NotNull(second);
        Assert.True(await repository.AcknowledgeHalfInningSummaryAsync(gameId, second.Id));
        return second;
    }

    private static async Task SeedBasesAsync(TemporaryTestDatabase db, int sessionId,
        IReadOnlyList<Player> players, string occupiedBases)
    {
        foreach (var text in occupiedBases.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var baseNumber = int.Parse(text);
            await db.SeedBaseRunnerAsync(new()
            {
                GameScoringSessionId = sessionId,
                PlayerId = players[baseNumber].Id,
                Base = (ScoringBase)baseNumber,
            });
        }
    }

    private static IReadOnlyList<(int Base, int PlayerId)> BaseValues(
        string bases, IReadOnlyList<Player> players) => bases
        .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)
        .Select(value => (value, players[value].Id)).ToArray();

    private static IReadOnlyList<(int Base, int PlayerId)> BaseValues(
        IEnumerable<GameScoringBaseRunner> runners) => runners
        .OrderBy(item => item.Base).Select(item => ((int)item.Base, item.PlayerId)).ToArray();

    private static (int Id, int GameId, int PlayerId, int Order) FrozenRow(
        GameLineupPlayer item) => (item.Id, item.GameId, item.PlayerId, item.BattingOrder);

    private static (int Id, int SessionId, int PlayerId, int Position) LiveRow(
        GameScoringLineupPlayer item) =>
        (item.Id, item.GameScoringSessionId, item.PlayerId, item.BattingPosition);

    private static async Task<(Team Team, Game Game, IReadOnlyList<Player> Players)>
        CreateReadyGameAsync(TemporaryTestDatabase db, int playerCount)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Season", Year = 2026 };
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
                FirstName = $"Player{index}", LastName = "Test", IsActive = true,
            };
            await db.CreatePlayerRepository().CreateAsync(player);
            await db.SeedTeamPlayerAsync(new()
            {
                TeamId = team.Id, PlayerId = player.Id, IsActive = true,
            });
            await db.SeedGameLineupPlayerAsync(new()
            {
                GameId = game.Id, PlayerId = player.Id, BattingOrder = index + 1,
            });
            players.Add(player);
        }
        return (team, game, players);
    }

    [SQLite.Table("GameScoringSession")]
    private sealed class LegacyStory63ScoringSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed("UX_GameScoringSession_Game", 1, Unique = true)]
        public int GameId { get; set; }

        public GameScoringStatus Status { get; set; }
        public TeamGameSide TeamSide { get; set; }
        public int HomeScore { get; set; }
        public int VisitorScore { get; set; }
        public int CurrentBatterPosition { get; set; }
    }

    [SQLite.Table("GameScoringSession")]
    private sealed class LegacyEarly64ScoringSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed("UX_GameScoringSession_Game", 1, Unique = true)]
        public int GameId { get; set; }

        public GameScoringStatus Status { get; set; }
        public TeamGameSide TeamSide { get; set; }
        public int HomeScore { get; set; }
        public int VisitorScore { get; set; }
        public int CurrentBatterPosition { get; set; }
        public int CurrentInning { get; set; }
        public int CurrentOuts { get; set; }
    }

    [SQLite.Table("GamePlateAppearance")]
    private sealed class LegacyEarly64PlateAppearance
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        public int GameScoringSessionId { get; set; }
        public int PlayerId { get; set; }
        public int BattingPosition { get; set; }
        public int PlateAppearanceNumber { get; set; }
        public BatterResult? Result { get; set; }
    }

    [SQLite.Table("GamePlateAppearanceRunnerMovement")]
    private sealed class LegacyEarly64RunnerMovement
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        public int GamePlateAppearanceId { get; set; }
        public int PlayerId { get; set; }
        public RunnerStartingLocation StartingLocation { get; set; }
        public RunnerDestination Destination { get; set; }
    }

    [SQLite.Table("GameScoringHalfInning")]
    private sealed class LegacyEarly64HalfInning
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed("UX_GameScoringHalfInning_Session_Inning", 1, Unique = true)]
        public int GameScoringSessionId { get; set; }

        [SQLite.Indexed("UX_GameScoringHalfInning_Session_Inning", 2, Unique = true)]
        public int Inning { get; set; }

        public int TeamRuns { get; set; }
        public int TeamHomeRuns { get; set; }
        public bool TeamSummaryAcknowledged { get; set; }
    }
}
