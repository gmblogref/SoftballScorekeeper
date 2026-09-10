using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class LowManCalculationRepositoryTests
{
    private static readonly DateTime GameNight = new(2026, 8, 15);

    [Fact]
    public async Task Single_completed_game_awards_lowest_eligible_obp_and_updates_totals()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Single Game");
        var low = await CreatePlayerAsync(db, context.Team, "Low");
        var high = await CreatePlayerAsync(db, context.Team, "High");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (low, BatterResult.Out), (high, BatterResult.Single));

        var result = await db.CreateLowManCalculationRepository()
            .CalculateAsync(context.Team.Id, GameNight);

        Assert.True(result.IsSuccess);
        Assert.Equal([low.Id], result.PlayerIds);
        Assert.Equal("Low", Assert.Single(result.Winners).DisplayName);
        Assert.Equal("Low", Assert.Single(
            (await db.CreateLowManCalculationRepository()
                .GetPersistedAutomaticWinnersAsync(context.Team.Id, GameNight))!)
            .DisplayName);
        var lowSummary = (await db.CreateBeerAccountingRepository()
            .GetTeamLifetimeAsync(context.Team.Id))!.Single(item => item.PlayerId == low.Id);
        Assert.Equal((1, 1L), (lowSummary.LowManCount, lowSummary.Owed));
    }

    [Fact]
    public async Task Doubleheader_combines_components_instead_of_averaging_game_rates()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Doubleheader");
        var playerA = await CreatePlayerAsync(db, context.Team, "Player A");
        var playerB = await CreatePlayerAsync(db, context.Team, "Player B");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (playerA, BatterResult.Single), (playerB, BatterResult.Out));
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            Enumerable.Repeat((playerA, BatterResult.Out), 9)
                .Append((playerB, BatterResult.Single)).ToArray());

        var result = await db.CreateLowManCalculationRepository()
            .CalculateAsync(context.Team.Id, GameNight);

        Assert.Equal([playerA.Id], result.PlayerIds);
        Assert.Equal(2, result.CompletedGameCount);
    }

    [Fact]
    public async Task Tournament_uses_all_completed_games_and_excludes_incomplete_other_dates_and_teams()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Tournament");
        var low = await CreatePlayerAsync(db, context.Team, "Low");
        var high = await CreatePlayerAsync(db, context.Team, "High");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (low, BatterResult.Out), (high, BatterResult.Single));
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (low, BatterResult.Out));
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (high, BatterResult.Walk));
        await AddGameAsync(db, context, GameNight, GameScoringStatus.InProgress,
            (low, BatterResult.Single));
        await AddGameAsync(db, context, GameNight, null,
            (low, BatterResult.Single));
        await AddGameAsync(db, context, GameNight.AddDays(1), GameScoringStatus.Completed,
            (low, BatterResult.Single));
        var other = await CreateTeamAsync(db, "Other Team");
        var otherPlayer = await CreatePlayerAsync(db, other.Team, "Other");
        await AddGameAsync(db, other, GameNight, GameScoringStatus.Completed,
            (otherPlayer, BatterResult.Out));

        var repository = db.CreateLowManCalculationRepository();
        Assert.Equal([otherPlayer.Id],
            (await repository.CalculateAsync(other.Team.Id, GameNight)).PlayerIds);
        var nights = (await repository.GetGameNightsAsync(context.Team.Id))!;
        var selected = nights.Single(item => item.PlayingDate == GameNight);
        var result = await repository.CalculateAsync(context.Team.Id, GameNight);

        Assert.Equal((3, 5, 2),
            (selected.CompletedGameCount, selected.TotalGameCount, selected.IncompleteGameCount));
        Assert.Equal((3, 2), (result.CompletedGameCount, result.IncompleteGameCount));
        Assert.Equal([low.Id], result.PlayerIds);
        Assert.DoesNotContain(otherPlayer.Id, result.PlayerIds);
        Assert.Contains(await db.GetBeerLedgerEntriesAsync(), item =>
            item.TeamId == other.Team.Id && item.PlayerId == otherPlayer.Id);
    }

    [Fact]
    public async Task One_pa_players_are_eligible_zero_pa_players_are_excluded_and_exact_ties_all_win()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Eligibility");
        var onePa = await CreatePlayerAsync(db, context.Team, "One PA");
        var anotherGameOnly = await CreatePlayerAsync(db, context.Team, "Other Game");
        var zeroPa = await CreatePlayerAsync(db, context.Team, "Zero PA");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (onePa, BatterResult.Out));
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (anotherGameOnly, BatterResult.Out));
        await db.ExecuteAsync("DELETE FROM TeamPlayer WHERE TeamId = ? AND PlayerId = ?",
            context.Team.Id, onePa.Id);
        await db.ExecuteAsync("UPDATE Player SET IsActive = 0 WHERE Id = ?", onePa.Id);

        var result = await db.CreateLowManCalculationRepository()
            .CalculateAsync(context.Team.Id, GameNight);

        Assert.Equal([onePa.Id, anotherGameOnly.Id], result.PlayerIds.Order());
        Assert.Equal(["One PA", "Other Game"],
            result.Winners.Select(item => item.DisplayName).Order());
        Assert.DoesNotContain(zeroPa.Id, result.PlayerIds);
    }

    [Fact]
    public async Task Current_configuration_controls_disabled_reconciliation_and_applied_amount()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Configuration");
        var player = await CreatePlayerAsync(db, context.Team, "Configured");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (player, BatterResult.Out));
        var calculation = db.CreateLowManCalculationRepository();
        var configuration = db.CreateBeerRuleConfigurationRepository();
        await SaveLowManSettingAsync(configuration, context.Team.Id, true, 4);

        Assert.Equal([player.Id],
            (await calculation.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        var automatic = Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring));
        Assert.Equal(4, automatic.Amount);

        await SaveLowManSettingAsync(configuration, context.Team.Id, true, 2);
        Assert.Equal([player.Id],
            (await calculation.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        automatic = Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring));
        Assert.Equal(2, automatic.Amount);

        await SaveLowManSettingAsync(configuration, context.Team.Id, false, 7);
        Assert.Empty((await calculation.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        Assert.Empty((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring));
    }

    [Fact]
    public async Task Unchanged_recalculation_is_idempotent_and_winner_and_tie_changes_reconcile()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Reconcile");
        var playerA = await CreatePlayerAsync(db, context.Team, "Player A");
        var playerB = await CreatePlayerAsync(db, context.Team, "Player B");
        var appearanceIds = await AddGameAsync(
            db, context, GameNight, GameScoringStatus.Completed,
            (playerA, BatterResult.Out), (playerB, BatterResult.Single));
        var repository = db.CreateLowManCalculationRepository();

        Assert.Equal([playerA.Id],
            (await repository.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        var firstId = Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)).Id;
        Assert.Equal([playerA.Id],
            (await repository.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        Assert.Equal(firstId, Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)).Id);

        await db.ExecuteAsync("UPDATE GamePlateAppearance SET Result = ? WHERE Id = ?",
            BatterResult.Out, appearanceIds[1]);
        Assert.Equal([playerA.Id, playerB.Id],
            (await repository.CalculateAsync(context.Team.Id, GameNight)).PlayerIds.Order());

        await db.ExecuteAsync("UPDATE GamePlateAppearance SET Result = ? WHERE Id = ?",
            BatterResult.Single, appearanceIds[0]);
        Assert.Equal([playerB.Id],
            (await repository.CalculateAsync(context.Team.Id, GameNight)).PlayerIds);
        var entries = (await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.AutomaticScoring).ToArray();
        Assert.Equal(playerB.Id, Assert.Single(entries).PlayerId);
    }

    [Fact]
    public async Task Manual_adjustments_and_other_dates_remain_untouched_while_totals_refresh()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Isolation");
        var player = await CreatePlayerAsync(db, context.Team, "Player");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (player, BatterResult.Out));
        await AddGameAsync(db, context, GameNight.AddDays(1), GameScoringStatus.Completed,
            (player, BatterResult.Out));
        var repository = db.CreateLowManCalculationRepository();
        await repository.CalculateAsync(context.Team.Id, GameNight);
        await repository.CalculateAsync(context.Team.Id, GameNight.AddDays(1));
        Assert.True((await db.CreateBeerAccountingRepository().SetEffectiveRuleCountAsync(
            context.Team.Id, player.Id, BeerRuleType.LowMan, 3)).IsSuccess);
        var manualId = Assert.Single((await db.GetBeerLedgerEntriesAsync()).Where(item =>
            item.Origin == BeerLedgerEntryOrigin.ManualAdjustment)).Id;

        await repository.CalculateAsync(context.Team.Id, GameNight);

        var entries = await db.GetBeerLedgerEntriesAsync();
        Assert.Contains(entries, item => item.Id == manualId);
        Assert.Single(entries.Where(item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
            && item.PlayingDate?.Date == GameNight.AddDays(1)));
        var summary = Assert.Single((await db.CreateBeerAccountingRepository()
            .GetTeamLifetimeAsync(context.Team.Id))!);
        Assert.Equal((3, 3L), (summary.LowManCount, summary.Owed));
    }

    [Fact]
    public async Task No_completed_or_no_pa_creates_no_result_and_calculation_is_scoring_read_only()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Read Only");
        var player = await CreatePlayerAsync(db, context.Team, "Player");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.InProgress,
            (player, BatterResult.Out));
        await AddGameAsync(db, context, GameNight.AddDays(1), GameScoringStatus.Completed);
        var sessionsBefore = await db.GetGameScoringSessionsAsync();
        var appearancesBefore = await db.GetGamePlateAppearancesAsync();
        var repository = db.CreateLowManCalculationRepository();

        var noCompletedResult = await repository.CalculateAsync(context.Team.Id, GameNight);
        var noPaResult = await repository.CalculateAsync(
            context.Team.Id, GameNight.AddDays(1));
        Assert.Empty(noCompletedResult.PlayerIds);
        Assert.Empty(noCompletedResult.Winners);
        Assert.Empty(noPaResult.PlayerIds);
        Assert.Empty(noPaResult.Winners);
        Assert.Equal(sessionsBefore.Select(SessionValues),
            (await db.GetGameScoringSessionsAsync()).Select(SessionValues));
        Assert.Equal(appearancesBefore.Select(AppearanceValues),
            (await db.GetGamePlateAppearancesAsync()).Select(AppearanceValues));
    }

    [Fact]
    public async Task Persisted_winner_status_is_automatic_team_date_scoped_and_resolves_all_names()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateTeamAsync(db, "Persisted Status");
        var alex = await CreatePlayerAsync(db, context.Team, "Alex");
        var blake = await CreatePlayerAsync(db, context.Team, "Blake");
        await AddGameAsync(db, context, GameNight, GameScoringStatus.Completed,
            (alex, BatterResult.Out), (blake, BatterResult.Out));
        await AddGameAsync(db, context, GameNight.AddDays(1),
            GameScoringStatus.Completed, (alex, BatterResult.Out));
        Assert.True((await db.CreateBeerAccountingRepository().SetEffectiveRuleCountAsync(
            context.Team.Id, alex.Id, BeerRuleType.LowMan, 1)).IsSuccess);

        var other = await CreateTeamAsync(db, "Other Persisted Status");
        var otherPlayer = await CreatePlayerAsync(db, other.Team, "Other");
        await AddGameAsync(db, other, GameNight, GameScoringStatus.Completed,
            (otherPlayer, BatterResult.Out));
        var repository = db.CreateLowManCalculationRepository();
        await repository.CalculateAsync(other.Team.Id, GameNight);

        Assert.Empty((await repository.GetPersistedAutomaticWinnersAsync(
            context.Team.Id, GameNight))!);
        await repository.CalculateAsync(context.Team.Id, GameNight);
        Assert.Equal(["Alex", "Blake"],
            (await repository.GetPersistedAutomaticWinnersAsync(
                context.Team.Id, GameNight))!.Select(item => item.DisplayName));
        Assert.Empty((await repository.GetPersistedAutomaticWinnersAsync(
            context.Team.Id, GameNight.AddDays(1)))!);
        Assert.Equal(["Alex", "Blake"],
            (await repository.GetPersistedAutomaticWinnersAsync(
                context.Team.Id, GameNight))!.Select(item => item.DisplayName));
        Assert.Equal(["Alex", "Blake"],
            (await new LowManCalculationRepository(db.Database)
                .GetPersistedAutomaticWinnersAsync(context.Team.Id, GameNight))!
            .Select(item => item.DisplayName));
    }

    private static async Task<TeamContext> CreateTeamAsync(
        TemporaryTestDatabase db,
        string name)
    {
        var team = new Team { Name = name, IsActive = true };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        var season = new Season { TeamId = team.Id, Name = "2026", Year = 2026 };
        Assert.Equal(1, await db.SeedSeasonAsync(season));
        return new(team, season);
    }

    private static async Task<Player> CreatePlayerAsync(
        TemporaryTestDatabase db,
        Team team,
        string name)
    {
        var player = new Player { FirstName = name, IsActive = true };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        Assert.Equal(1, await db.SeedTeamPlayerAsync(new TeamPlayer
        {
            TeamId = team.Id,
            PlayerId = player.Id,
            IsActive = true,
        }));
        return player;
    }

    private static async Task<int[]> AddGameAsync(
        TemporaryTestDatabase db,
        TeamContext context,
        DateTime date,
        GameScoringStatus? status,
        params (Player Player, BatterResult Result)[] outcomes)
    {
        var game = new Game
        {
            TeamId = context.Team.Id,
            SeasonId = context.Season.Id,
            ScheduledDate = date.Date,
            ScheduledTime = TimeSpan.FromHours(18),
        };
        Assert.Equal(1, await db.SeedGameAsync(game));
        if (status is null)
            return [];

        return await db.UseConnectionAsync(async connection =>
        {
            var session = new GameScoringSession
            {
                GameId = game.Id,
                Status = status.Value,
                TeamSide = TeamGameSide.Home,
                CompletedAtUtc = status == GameScoringStatus.Completed ? DateTime.UtcNow : null,
            };
            Assert.Equal(1, await connection.InsertAsync(session));
            var ids = new List<int>();
            for (var index = 0; index < outcomes.Length; index++)
            {
                var appearance = new GamePlateAppearance
                {
                    GameScoringSessionId = session.Id,
                    PlayerId = outcomes[index].Player.Id,
                    BattingPosition = index + 1,
                    PlateAppearanceNumber = index + 1,
                    Inning = 1,
                    Result = outcomes[index].Result,
                };
                Assert.Equal(1, await connection.InsertAsync(appearance));
                ids.Add(appearance.Id);
            }
            return ids.ToArray();
        });
    }

    private static async Task SaveLowManSettingAsync(
        BeerRuleConfigurationRepository repository,
        int teamId,
        bool enabled,
        int amount)
    {
        var settings = (await repository.GetTeamAsync(teamId))!
            .Select(item => item.RuleType == BeerRuleType.LowMan
                ? new BeerRuleSetting(item.RuleType, enabled, amount)
                : item)
            .ToArray();
        Assert.True(await repository.SaveTeamAsync(teamId, settings));
    }

    private static object SessionValues(GameScoringSession item) => new
    {
        item.Id, item.GameId, item.Status, item.CompletedAtUtc,
        item.HomeScore, item.VisitorScore, item.CurrentInning, item.CurrentOuts,
    };

    private static object AppearanceValues(GamePlateAppearance item) => new
    {
        item.Id, item.GameScoringSessionId, item.PlayerId,
        item.PlateAppearanceNumber, item.Result,
    };

    private sealed record TeamContext(Team Team, Season Season);
}
