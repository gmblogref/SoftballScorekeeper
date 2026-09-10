using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class BeerAccountingRepositoryTests
{
    [Fact]
    public async Task Initialization_adds_accounting_and_game_night_columns_to_existing_beer_ledger_table()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.ExecuteAsync(
            "CREATE TABLE BeerLedgerEntry (Id INTEGER PRIMARY KEY AUTOINCREMENT, TeamId INTEGER NOT NULL, GameId INTEGER NULL, GameScoringSessionId INTEGER NULL, PlayerId INTEGER NOT NULL, RuleType INTEGER NULL, Origin INTEGER NOT NULL, GamePlateAppearanceId INTEGER NULL, Amount INTEGER NOT NULL)"
        );

        await db.Database.InitializeAsync();

        var columns = await db.UseConnectionAsync(connection =>
            connection.GetTableInfoAsync(nameof(BeerLedgerEntry))
        );
        Assert.Contains(
            columns,
            column => column.Name == nameof(BeerLedgerEntry.RuleCountAdjustment)
        );
        Assert.Contains(
            columns,
            column => column.Name == nameof(BeerLedgerEntry.PlayingDate)
        );
    }

    [Fact]
    public async Task Summary_aggregates_automatic_entries_and_keeps_rule_counts_independent()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db, "Alex", "Rivera");
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Dbo, 1);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.TriplePlay, 1);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);

        var summary = Assert.Single(
            (await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(team.Id))!
        );

        Assert.Equal(1, summary.DboCount);
        Assert.Equal(1, summary.TriplePlayCount);
        Assert.Equal(0, summary.DoublePlayCount);
        Assert.Equal(0, summary.TwoOutsInOneInningCount);
        Assert.Equal(2, summary.StrikeoutCount);
        Assert.Equal(6, summary.Owed);
    }

    [Fact]
    public async Task Mixed_historical_strikeout_decrement_removes_newest_value_without_changing_automatic_rows()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 3);
        var repository = db.CreateBeerAccountingRepository();
        var initial = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(2, initial.StrikeoutCount);
        Assert.Equal(5, initial.Owed);

        var result = await repository.SetEffectiveRuleCountAsync(
            team.Id,
            player.Id,
            BeerRuleType.Strikeout,
            1
        );

        Assert.True(result.IsSuccess);
        var summary = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(1, summary.StrikeoutCount);
        Assert.Equal(2, summary.Owed);
        var automatic = (await db.GetBeerLedgerEntriesAsync())
            .Where(item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
            .ToArray();
        Assert.Equal(2, automatic.Length);
        Assert.Equal(new[] { 2, 3 }, automatic.Select(item => item.Amount));
        var adjustment = Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item =>
                item.Origin == BeerLedgerEntryOrigin.ManualAdjustment
            )
        );
        Assert.Equal(-1, adjustment.RuleCountAdjustment);
        Assert.Equal(-3, adjustment.Amount);
        Assert.Null(adjustment.GameId);
        Assert.Null(adjustment.GameScoringSessionId);
        Assert.Null(adjustment.GamePlateAppearanceId);
    }

    [Fact]
    public async Task Missed_infraction_uses_current_team_amount_and_snapshots_its_value()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var configurations = db.CreateBeerRuleConfigurationRepository();
        await SaveAmountAsync(configurations, team.Id, BeerRuleType.TriplePlay, 3);
        var repository = db.CreateBeerAccountingRepository();

        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.TriplePlay,
                    1
                )
            ).IsSuccess
        );
        await SaveAmountAsync(configurations, team.Id, BeerRuleType.TriplePlay, 8);

        var summary = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(1, summary.TriplePlayCount);
        Assert.Equal(3, summary.Owed);
    }

    [Fact]
    public async Task Increasing_above_historical_occurrences_preserves_old_values_and_uses_current_amount()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 4);
        await SaveAmountAsync(
            db.CreateBeerRuleConfigurationRepository(),
            team.Id,
            BeerRuleType.Strikeout,
            7
        );

        var result = await db.CreateBeerAccountingRepository()
            .SetEffectiveRuleCountAsync(team.Id, player.Id, BeerRuleType.Strikeout, 3);

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(
            (await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(team.Id))!
        );
        Assert.Equal(3, summary.StrikeoutCount);
        Assert.Equal(13, summary.Owed);
    }

    [Fact]
    public async Task Negative_adjustment_reapplies_to_the_current_automatic_baseline()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 4);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);
        var repository = db.CreateBeerAccountingRepository();
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.Strikeout,
                    1
                )
            ).IsSuccess
        );
        var newestAutomaticId = (await db.GetBeerLedgerEntriesAsync())
            .Where(item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
            .Max(item => item.Id);

        Assert.Equal(
            1,
            await db.ExecuteAsync("DELETE FROM BeerLedgerEntry WHERE Id = ?", newestAutomaticId)
        );
        var undone = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(0, undone.StrikeoutCount);
        Assert.Equal(0, undone.Owed);

        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Strikeout, 2);
        var redone = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(1, redone.StrikeoutCount);
        Assert.Equal(4, redone.Owed);
    }

    [Fact]
    public async Task Low_man_is_available_only_as_a_manual_effective_count()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();

        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.LowMan,
                    2
                )
            ).IsSuccess
        );

        var summary = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(2, summary.LowManCount);
        Assert.Equal(2, summary.Owed);
        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item =>
                item.RuleType == BeerRuleType.LowMan
                && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
        );
    }

    [Fact]
    public async Task Manual_effective_edits_for_dbo_triple_play_and_strikeout_are_independent()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();

        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(team.Id, player.Id, BeerRuleType.Dbo, 2)
            ).IsSuccess
        );
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.TriplePlay,
                    1
                )
            ).IsSuccess
        );
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.Strikeout,
                    3
                )
            ).IsSuccess
        );

        var summary = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(
            (2, 1, 3),
            (summary.DboCount, summary.TriplePlayCount, summary.StrikeoutCount)
        );
        Assert.Equal(9, summary.Owed);
    }

    [Fact]
    public async Task Negative_count_is_rejected_without_changing_persisted_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(team.Id, player.Id, BeerRuleType.Dbo, 2)
            ).IsSuccess
        );
        var before = (await db.GetBeerLedgerEntriesAsync()).Select(LedgerValues).ToArray();

        var result = await repository.SetEffectiveRuleCountAsync(
            team.Id,
            player.Id,
            BeerRuleType.Dbo,
            -1
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(BeerAccountingUpdateFailure.InvalidValue, result.FailureReason);
        Assert.Equal(before, (await db.GetBeerLedgerEntriesAsync()).Select(LedgerValues).ToArray());
    }

    [Fact]
    public async Task Brought_can_be_added_changed_cleared_and_exceed_obligations()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Dbo, 2);
        var repository = db.CreateBeerAccountingRepository();

        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 1)).IsSuccess);
        Assert.Equal(1, Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!).Owed);
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 2)).IsSuccess);
        Assert.Equal(0, Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!).Owed);
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 5)).IsSuccess);
        var excess = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(5, excess.Brought);
        Assert.Equal(-3, excess.Owed);
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 0)).IsSuccess);
        var cleared = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(0, cleared.Brought);
        Assert.Equal(2, cleared.Owed);
    }

    [Fact]
    public async Task Negative_brought_is_rejected_without_changing_persisted_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 3)).IsSuccess);

        var result = await repository.SetBroughtAsync(team.Id, player.Id, -1);

        Assert.Equal(BeerAccountingUpdateFailure.InvalidValue, result.FailureReason);
        Assert.Equal(3, Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!).Brought);
    }

    [Fact]
    public async Task Corrections_and_brought_persist_across_database_reopen()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    BeerRuleType.SoloHomeRun,
                    2
                )
            ).IsSuccess
        );
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 1)).IsSuccess);
        await db.Database.CloseAsync();

        var reopened = new AppDatabase(db.DatabasePath);
        try
        {
            var summary = Assert.Single(
                (await new BeerAccountingRepository(reopened).GetTeamLifetimeAsync(team.Id))!
            );
            Assert.Equal(player.Id, summary.PlayerId);
            Assert.Equal(2, summary.SoloHomeRunCount);
            Assert.Equal(1, summary.Brought);
            Assert.Equal(1, summary.Owed);
        }
        finally
        {
            await reopened.CloseAsync();
        }
    }

    [Fact]
    public async Task Summary_uses_current_name_and_keeps_removed_inactive_player_history()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db, "Sam", "Old");
        await SeedAutomaticAsync(db, team.Id, player.Id, BeerRuleType.Dbo, 1);
        Assert.True(
            (await db.CreateBeerAccountingRepository().SetBroughtAsync(team.Id, player.Id, 1))
                .IsSuccess
        );
        player.FirstName = "Samuel";
        player.LastName = "Current";
        Assert.Equal(1, await db.CreatePlayerRepository().UpdateAsync(player));
        Assert.Single(
            (
                await db.CreateTeamPlayerRepository()
                    .RemovePlayersFromTeamAsync(team.Id, [player.Id])
            ).RemovedPlayerIds
        );
        Assert.Equal(1, await db.CreatePlayerRepository().DeactivateAsync([player.Id]));

        var summary = Assert.Single(
            (await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(team.Id))!
        );

        Assert.Equal(player.Id, summary.PlayerId);
        Assert.Equal("Samuel Current", summary.PlayerDisplayName);
        Assert.Equal(1, summary.DboCount);
        Assert.Equal(1, summary.Brought);
        Assert.Equal(0, summary.Owed);
    }

    [Fact]
    public async Task Same_player_has_strictly_isolated_team_lifetime_totals()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (firstTeam, player) = await CreateMemberAsync(db);
        var secondTeam = new Team { Name = "Second Team" };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(secondTeam));
        Assert.Equal(
            1,
            await db.SeedTeamPlayerAsync(
                new TeamPlayer { TeamId = secondTeam.Id, PlayerId = player.Id }
            )
        );
        await SeedAutomaticAsync(db, firstTeam.Id, player.Id, BeerRuleType.Dbo, 1);
        await SeedAutomaticAsync(db, secondTeam.Id, player.Id, BeerRuleType.Strikeout, 2);
        var repository = db.CreateBeerAccountingRepository();
        Assert.True((await repository.SetBroughtAsync(firstTeam.Id, player.Id, 1)).IsSuccess);
        Assert.True((await repository.SetBroughtAsync(secondTeam.Id, player.Id, 3)).IsSuccess);

        var first = Assert.Single((await repository.GetTeamLifetimeAsync(firstTeam.Id))!);
        var second = Assert.Single((await repository.GetTeamLifetimeAsync(secondTeam.Id))!);

        Assert.Equal(1, first.DboCount);
        Assert.Equal(0, first.StrikeoutCount);
        Assert.Equal((1L, 0L), (first.Brought, first.Owed));
        Assert.Equal(0, second.DboCount);
        Assert.Equal(1, second.StrikeoutCount);
        Assert.Equal((3L, -1L), (second.Brought, second.Owed));
    }

    [Fact]
    public async Task Session_automatic_rows_can_change_without_removing_team_level_manual_rows()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        await db.SeedBeerLedgerEntryAsync(
            new BeerLedgerEntry
            {
                TeamId = team.Id,
                GameId = 10,
                GameScoringSessionId = 20,
                GamePlateAppearanceId = 30,
                PlayerId = player.Id,
                RuleType = BeerRuleType.Dbo,
                Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                Amount = 1,
            }
        );
        var repository = db.CreateBeerAccountingRepository();
        Assert.True(
            (
                await repository.SetEffectiveRuleCountAsync(team.Id, player.Id, BeerRuleType.Dbo, 2)
            ).IsSuccess
        );
        Assert.True((await repository.SetBroughtAsync(team.Id, player.Id, 1)).IsSuccess);

        Assert.Equal(
            1,
            await db.ExecuteAsync(
                "DELETE FROM BeerLedgerEntry WHERE GameScoringSessionId = ? AND Origin = ?",
                20,
                BeerLedgerEntryOrigin.AutomaticScoring
            )
        );

        var afterReset = Assert.Single((await repository.GetTeamLifetimeAsync(team.Id))!);
        Assert.Equal(1, afterReset.DboCount);
        Assert.Equal(1, afterReset.Brought);
        Assert.Equal(0, afterReset.Owed);
        var remaining = await db.GetBeerLedgerEntriesAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, item => Assert.Null(item.GameScoringSessionId));
    }

    [Fact]
    public void Read_model_does_not_persist_or_expose_still_owed()
    {
        Assert.Null(typeof(TeamBeerPlayerSummary).GetProperty("StillOwed"));
        Assert.Null(typeof(BeerLedgerEntry).GetProperty("Owed"));
    }

    [Fact]
    public async Task Player_without_team_membership_or_history_is_rejected()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, _) = await CreateMemberAsync(db);
        var unrelated = new Player { FirstName = "Other" };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(unrelated));

        var result = await db.CreateBeerAccountingRepository()
            .SetBroughtAsync(team.Id, unrelated.Id, 1);

        Assert.Equal(BeerAccountingUpdateFailure.PlayerNotAssociatedWithTeam, result.FailureReason);
    }

    [Fact]
    public async Task Missing_context_and_invalid_rule_return_structured_failures()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var (team, player) = await CreateMemberAsync(db);
        var repository = db.CreateBeerAccountingRepository();

        Assert.Equal(
            BeerAccountingUpdateFailure.TeamNotFound,
            (await repository.SetBroughtAsync(int.MaxValue, player.Id, 1)).FailureReason
        );
        Assert.Equal(
            BeerAccountingUpdateFailure.PlayerNotFound,
            (await repository.SetBroughtAsync(team.Id, int.MaxValue, 1)).FailureReason
        );
        Assert.Equal(
            BeerAccountingUpdateFailure.InvalidRule,
            (
                await repository.SetEffectiveRuleCountAsync(
                    team.Id,
                    player.Id,
                    (BeerRuleType)int.MaxValue,
                    1
                )
            ).FailureReason
        );
    }

    private static async Task<(Team Team, Player Player)> CreateMemberAsync(
        TemporaryTestDatabase db,
        string firstName = "Taylor",
        string? lastName = "Player"
    )
    {
        var team = new Team { Name = $"Team {Guid.NewGuid():N}" };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        var player = new Player { FirstName = firstName, LastName = lastName };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        Assert.Equal(
            1,
            await db.SeedTeamPlayerAsync(new TeamPlayer { TeamId = team.Id, PlayerId = player.Id })
        );
        return (team, player);
    }

    private static Task<int> SeedAutomaticAsync(
        TemporaryTestDatabase db,
        int teamId,
        int playerId,
        BeerRuleType ruleType,
        int amount
    ) =>
        db.SeedBeerLedgerEntryAsync(
            new BeerLedgerEntry
            {
                TeamId = teamId,
                PlayerId = playerId,
                RuleType = ruleType,
                Origin = BeerLedgerEntryOrigin.AutomaticScoring,
                Amount = amount,
            }
        );

    private static async Task SaveAmountAsync(
        BeerRuleConfigurationRepository repository,
        int teamId,
        BeerRuleType ruleType,
        int amount
    )
    {
        var settings = (await repository.GetTeamAsync(teamId))!
            .Select(item => item.RuleType == ruleType ? item with { Amount = amount } : item)
            .ToArray();
        Assert.True(await repository.SaveTeamAsync(teamId, settings));
    }

    private static object LedgerValues(BeerLedgerEntry item) =>
        new
        {
            item.Id,
            item.TeamId,
            item.GameId,
            item.GameScoringSessionId,
            item.PlayerId,
            item.RuleType,
            item.Origin,
            item.GamePlateAppearanceId,
            item.RuleCountAdjustment,
            item.Amount,
        };
}
