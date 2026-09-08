using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed partial class GameScoringSessionRepositoryTests
{
    [Fact]
    public async Task Team_defaults_cover_all_rules_and_game_snapshot_excludes_low_man()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var configurations = db.CreateBeerRuleConfigurationRepository();

        var team = (await configurations.GetTeamAsync(context.Team.Id))!;

        Assert.Equal(
            new[]
            {
                (BeerRuleType.Dbo, true, 1),
                (BeerRuleType.SoloHomeRun, true, 1),
                (BeerRuleType.DoublePlay, true, 1),
                (BeerRuleType.TriplePlay, true, 1),
                (BeerRuleType.TwoOutsInOneInning, true, 1),
                (BeerRuleType.Strikeout, true, 2),
                (BeerRuleType.LowMan, true, 1),
            },
            Values(team)
        );

        await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var game = (await configurations.GetGameAsync(context.Game.Id))!;

        Assert.Equal(6, game.Count);
        Assert.DoesNotContain(game, item => item.RuleType == BeerRuleType.LowMan);
        Assert.Equal(BeerRuleConfigurationDefaults.ScoringRules, game);
    }

    [Fact]
    public async Task Team_configuration_persists_through_reload_and_database_reopen()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, null, null);
        var repository = db.CreateBeerRuleConfigurationRepository();
        var updated = Change(
            (await repository.GetTeamAsync(context.Team.Id))!,
            BeerRuleType.Strikeout,
            false,
            4
        );

        Assert.True(await repository.SaveTeamAsync(context.Team.Id, updated));
        Assert.Equal(7, (await db.GetTeamBeerRuleConfigurationsAsync()).Count);
        Assert.Equal(Values(updated), Values((await repository.GetTeamAsync(context.Team.Id))!));

        await db.Database.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        try
        {
            var persisted = (
                await new BeerRuleConfigurationRepository(reopened).GetTeamAsync(context.Team.Id)
            )!;
            Assert.Equal(Values(updated), Values(persisted));
        }
        finally
        {
            await reopened.CloseAsync();
        }
    }

    [Fact]
    public async Task Team_edits_do_not_change_existing_snapshot_but_new_game_uses_them()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var configurations = db.CreateBeerRuleConfigurationRepository();
        var scoring = db.CreateGameScoringSessionRepository();
        var settingA = Change(
            (await configurations.GetTeamAsync(first.Team.Id))!,
            BeerRuleType.Strikeout,
            true,
            4
        );
        Assert.True(await configurations.SaveTeamAsync(first.Team.Id, settingA));
        await scoring.InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);

        var settingB = Change(settingA, BeerRuleType.Strikeout, false, 1);
        Assert.True(await configurations.SaveTeamAsync(first.Team.Id, settingB));
        Assert.Equal(
            (true, 4),
            Rule((await configurations.GetGameAsync(first.Game.Id))!, BeerRuleType.Strikeout)
        );

        var season = Assert.Single(await db.GetSeasonsAsync());
        var player = Assert.Single(await db.GetGameLineupPlayersAsync());
        var secondGame = new Game
        {
            TeamId = first.Team.Id,
            SeasonId = season.Id,
            ScheduledDate = DateTime.Today.AddDays(1),
            ScheduledTime = TimeSpan.FromHours(18),
        };
        await db.SeedGameAsync(secondGame);
        await db.SeedGameLineupPlayerAsync(
            new()
            {
                GameId = secondGame.Id,
                PlayerId = player.PlayerId,
                BattingOrder = 1,
            }
        );
        await scoring.InitializeAsync(first.Team.Id, secondGame.Id, TeamGameSide.Home);

        Assert.Equal(
            (false, 1),
            Rule((await configurations.GetGameAsync(secondGame.Id))!, BeerRuleType.Strikeout)
        );
    }

    [Fact]
    public async Task Reset_removes_snapshot_and_restart_copies_current_team_defaults()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var configurations = db.CreateBeerRuleConfigurationRepository();
        var scoring = db.CreateGameScoringSessionRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var oldSession = Assert.Single(await db.GetGameScoringSessionsAsync());
        var changed = Change(
            (await configurations.GetTeamAsync(context.Team.Id))!,
            BeerRuleType.Strikeout,
            true,
            5
        );
        Assert.True(await configurations.SaveTeamAsync(context.Team.Id, changed));

        Assert.True((await scoring.ResetGameAsync(context.Game.Id)).IsSuccess);
        Assert.DoesNotContain(
            await db.GetGameBeerRuleConfigurationsAsync(),
            item => item.GameScoringSessionId == oldSession.Id
        );

        var restarted = await scoring.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home
        );
        Assert.True(restarted.WasCreated);
        Assert.Equal(
            (true, 5),
            Rule((await configurations.GetGameAsync(context.Game.Id))!, BeerRuleType.Strikeout)
        );
    }

    [Theory]
    [InlineData(BatterResult.Strikeout, BeerRuleType.Strikeout, 5)]
    [InlineData(BatterResult.DeadBallOut, BeerRuleType.Dbo, 0)]
    public async Task Disabled_direct_rule_changes_no_scoring_or_statistics(
        BatterResult result,
        BeerRuleType ruleType,
        int homeRunLimit
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(
            context.Team.Id,
            context.Game.Id,
            TeamGameSide.Home,
            homeRunLimit,
            false
        );
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            ruleType,
            false,
            3
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));

        Assert.True((await scoring.SelectBatterResultAsync(context.Game.Id, result)).IsSuccess);

        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType == ruleType
        );
        var appearance = Assert.Single(await db.GetGamePlateAppearancesAsync());
        Assert.Equal(result, appearance.Result);
        var stats = Assert
            .Single((await scoring.GetInProgressBattingStatisticsAsync(context.Game.Id))!.Players)
            .BattingStatistics;
        Assert.Equal(
            (1, 1, 0, 0),
            (stats.PlateAppearances, stats.AtBats, stats.Hits, stats.TotalBases)
        );
    }

    [Fact]
    public async Task In_game_amount_edit_is_prospective()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );
        var changed = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.Strikeout,
            true,
            4
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, changed));
        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );

        Assert.Equal(
            new[] { 2, 4 },
            (await db.GetBeerLedgerEntriesAsync())
                .Where(item => item.RuleType == BeerRuleType.Strikeout)
                .Select(item => item.Amount)
        );
    }

    [Fact]
    public async Task Amount_one_rule_uses_configured_game_amount()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.SoloHomeRun,
            true,
            3
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));

        Assert.True((await scoring.CompleteHomeRunAsync(context.Game.Id)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal((BeerRuleType.SoloHomeRun, 3), (entry.RuleType, entry.Amount));
    }

    [Fact]
    public async Task Disabling_rule_is_prospective_and_preserves_existing_entry()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );
        var original = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.Strikeout,
            false,
            2
        );
        settings = Change(settings, BeerRuleType.TwoOutsInOneInning, false, 1);
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));

        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );

        var preserved = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal((original.Id, 2), (preserved.Id, preserved.Amount));
    }

    [Fact]
    public async Task Disabled_triple_play_creates_no_beer_but_keeps_authoritative_play()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        var session = (
            await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.TriplePlay,
            false,
            1
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));
        var lineup = await db.GetGameScoringLineupPlayersAsync();
        await db.SeedBaseRunnerAsync(
            new()
            {
                GameScoringSessionId = session.Id,
                PlayerId = lineup[1].PlayerId,
                Base = ScoringBase.First,
            }
        );
        await db.SeedBaseRunnerAsync(
            new()
            {
                GameScoringSessionId = session.Id,
                PlayerId = lineup[2].PlayerId,
                Base = ScoringBase.Second,
            }
        );

        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.TriplePlay)
            ).IsSuccess
        );

        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(
            BatterResult.TriplePlay,
            Assert.Single(await db.GetGamePlateAppearancesAsync()).Result
        );
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public async Task Stacked_rules_respect_independent_enablement(
        bool strikeoutEnabled,
        bool twoOutsEnabled,
        bool expectStrikeout,
        bool expectTwoOuts
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.Strikeout,
            strikeoutEnabled,
            3
        );
        settings = Change(settings, BeerRuleType.TwoOutsInOneInning, twoOutsEnabled, 2);
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));
        Assert.True((await scoring.SelectOutAsync(context.Game.Id)).IsSuccess);

        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );

        var entries = await db.GetBeerLedgerEntriesAsync();
        Assert.Equal(
            expectStrikeout,
            entries.Any(item => item.RuleType == BeerRuleType.Strikeout && item.Amount == 3)
        );
        Assert.Equal(
            expectTwoOuts,
            entries.Any(item =>
                item.RuleType == BeerRuleType.TwoOutsInOneInning && item.Amount == 2
            )
        );
    }

    [Theory]
    [InlineData(BatterResult.TriplePlay, BeerRuleType.DoublePlay, BeerRuleType.TriplePlay, 3)]
    [InlineData(BatterResult.DoublePlay, BeerRuleType.TriplePlay, BeerRuleType.DoublePlay, 2)]
    public async Task Double_play_and_triple_play_configuration_are_independent(
        BatterResult result,
        BeerRuleType disabledRule,
        BeerRuleType expectedRule,
        int playerCount
    )
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, playerCount);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        var session = (
            await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home)
        ).Session!;
        var settings = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            disabledRule,
            false,
            1
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, settings));
        var lineup = await db.GetGameScoringLineupPlayersAsync();
        await db.SeedBaseRunnerAsync(
            new()
            {
                GameScoringSessionId = session.Id,
                PlayerId = lineup[1].PlayerId,
                Base = ScoringBase.First,
            }
        );
        if (result == BatterResult.TriplePlay)
            await db.SeedBaseRunnerAsync(
                new()
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = lineup[2].PlayerId,
                    Base = ScoringBase.Second,
                }
            );

        Assert.True((await scoring.SelectBatterResultAsync(context.Game.Id, result)).IsSuccess);

        var entry = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal(expectedRule, entry.RuleType);
        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item => item.RuleType == BeerRuleType.TwoOutsInOneInning
        );
    }

    [Fact]
    public async Task Undo_redo_restores_original_consequence_without_reverting_configuration()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        Assert.True(
            (
                await scoring.SelectBatterResultAsync(context.Game.Id, BatterResult.Strikeout)
            ).IsSuccess
        );
        var original = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        var changed = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.Strikeout,
            true,
            1
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, changed));

        Assert.True((await scoring.UndoAsync(context.Game.Id)).IsSuccess);
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
        Assert.True((await scoring.RedoAsync(context.Game.Id)).IsSuccess);

        var restored = Assert.Single(await db.GetBeerLedgerEntriesAsync());
        Assert.Equal((original.Id, 2), (restored.Id, restored.Amount));
        Assert.Equal(
            (true, 1),
            Rule((await configurations.GetGameAsync(context.Game.Id))!, BeerRuleType.Strikeout)
        );
    }

    [Fact]
    public async Task Game_snapshot_survives_resume_reopen_and_completion_and_is_read_only()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var scoring = db.CreateGameScoringSessionRepository();
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await scoring.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var changed = Change(
            (await configurations.GetGameAsync(context.Game.Id))!,
            BeerRuleType.SoloHomeRun,
            false,
            3
        );
        Assert.True(await configurations.SaveGameAsync(context.Game.Id, changed));

        configurations = db.CreateBeerRuleConfigurationRepository();
        Assert.Equal(
            (false, 3),
            Rule((await configurations.GetGameAsync(context.Game.Id))!, BeerRuleType.SoloHomeRun)
        );
        Assert.True((await scoring.CompleteGameAsync(context.Game.Id)).IsSuccess);
        var teamChanged = Change(
            (await configurations.GetTeamAsync(context.Team.Id))!,
            BeerRuleType.SoloHomeRun,
            true,
            1
        );
        Assert.True(await configurations.SaveTeamAsync(context.Team.Id, teamChanged));
        Assert.Equal(
            (false, 3),
            Rule((await configurations.GetGameAsync(context.Game.Id))!, BeerRuleType.SoloHomeRun)
        );

        await db.Database.CloseAsync();
        var reopened = new AppDatabase(db.DatabasePath);
        try
        {
            var reopenedConfigurations = new BeerRuleConfigurationRepository(reopened);
            Assert.Equal(
                (false, 3),
                Rule(
                    (await reopenedConfigurations.GetGameAsync(context.Game.Id))!,
                    BeerRuleType.SoloHomeRun
                )
            );
            Assert.False(
                await reopenedConfigurations.SaveGameAsync(
                    context.Game.Id,
                    BeerRuleConfigurationDefaults.ScoringRules
                )
            );
        }
        finally
        {
            await reopened.CloseAsync();
        }
    }

    [Fact]
    public async Task Configuration_rejects_zero_amount_without_clamping()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, null, null);
        var configurations = db.CreateBeerRuleConfigurationRepository();
        var invalid = Change(
            (await configurations.GetTeamAsync(context.Team.Id))!,
            BeerRuleType.Dbo,
            true,
            0
        );

        Assert.False(await configurations.SaveTeamAsync(context.Team.Id, invalid));
        Assert.Empty(await db.GetTeamBeerRuleConfigurationsAsync());
    }

    [Fact]
    public async Task Database_initialization_backfills_missing_existing_game_snapshot()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var session = Assert.Single(await db.GetGameScoringSessionsAsync());
        Assert.Equal(
            6,
            await db.ExecuteAsync(
                "DELETE FROM GameScoringBeerRuleConfiguration WHERE GameScoringSessionId = ?",
                session.Id
            )
        );
        await db.Database.CloseAsync();

        var reopened = new AppDatabase(db.DatabasePath);
        try
        {
            await reopened.InitializeAsync();
            var settings = (
                await new BeerRuleConfigurationRepository(reopened).GetGameAsync(context.Game.Id)
            )!;
            Assert.Equal(BeerRuleConfigurationDefaults.ScoringRules, settings);
        }
        finally
        {
            await reopened.CloseAsync();
        }
    }

    private static IReadOnlyList<BeerRuleSetting> Change(
        IReadOnlyList<BeerRuleSetting> settings,
        BeerRuleType ruleType,
        bool isEnabled,
        int amount
    ) =>
        settings
            .Select(item =>
                item.RuleType == ruleType ? new BeerRuleSetting(ruleType, isEnabled, amount) : item
            )
            .ToArray();

    private static (bool IsEnabled, int Amount) Rule(
        IReadOnlyList<BeerRuleSetting> settings,
        BeerRuleType ruleType
    )
    {
        var setting = settings.Single(item => item.RuleType == ruleType);
        return (setting.IsEnabled, setting.Amount);
    }

    private static IEnumerable<(BeerRuleType RuleType, bool IsEnabled, int Amount)> Values(
        IEnumerable<BeerRuleSetting> settings
    ) => settings.Select(item => (item.RuleType, item.IsEnabled, item.Amount));
}
