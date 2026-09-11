using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class TeamYearCopyRepositoryTests
{
    [Fact]
    public async Task Team_identity_requires_valid_year_enforces_name_year_and_is_immutable()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var repository = db.CreateTeamRepository();
        Assert.Equal(0, await repository.CreateAsync(new Team { Name = "Bad", Year = 0 }));
        Assert.Equal(0, await repository.CreateAsync(new Team { Name = "Bad", Year = 10000 }));
        var first = new Team { Name = " Misfits ", Year = 2026 };
        Assert.Equal(1, await repository.CreateAsync(first));
        Assert.NotEqual(Guid.Empty, first.TeamLineageId);
        Assert.Null(first.PreviousTeamId);
        var nextYear = new Team { Name = "misfits", Year = 2027 };
        Assert.Equal(1, await repository.CreateAsync(nextYear));
        Assert.Equal(
            0,
            await repository.CreateAsync(
                new Team
                {
                    Name = "MISFITS",
                    Year = 2026,
                    IsActive = false,
                }
            )
        );

        var originalLineage = first.TeamLineageId;
        first.Year = 2025;
        Assert.Equal(0, await repository.UpdateAsync(first));
        first.Year = 2026;
        first.TeamLineageId = Guid.NewGuid();
        Assert.Equal(0, await repository.UpdateAsync(first));
        first.TeamLineageId = originalLineage;
        first.Name = "Renamed";
        Assert.Equal(1, await repository.UpdateAsync(first));
        nextYear.Name = "Renamed";
        Assert.Equal(1, await repository.UpdateAsync(nextYear));
    }

    [Fact]
    public async Task Version_zero_migrates_team_years_lineage_and_preserves_legacy_seasons()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.ExecuteAsync(
            "CREATE TABLE Team (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, IsActive INTEGER NOT NULL)"
        );
        await db.ExecuteAsync(
            "CREATE TABLE Season (Id INTEGER PRIMARY KEY AUTOINCREMENT, TeamId INTEGER NOT NULL, Name TEXT NOT NULL, Year INTEGER NOT NULL)"
        );
        await db.ExecuteAsync(
            "INSERT INTO Team (Name, IsActive) VALUES ('Single', 1), ('Empty', 1), ('Mixed', 1)"
        );
        await db.ExecuteAsync(
            "INSERT INTO Season (TeamId, Name, Year) VALUES (1, 'Summer', 2025), (3, 'Fall', 2024), (3, 'Spring', 2026)"
        );

        await db.Database.InitializeAsync();

        var teams = await db.UseConnectionAsync(connection =>
            connection.Table<Team>().OrderBy(item => item.Id).ToListAsync()
        );
        Assert.Equal([2025, 2026, 2026], teams.Select(item => item.Year));
        Assert.All(teams, item => Assert.NotEqual(Guid.Empty, item.TeamLineageId));
        Assert.Equal(3, teams.Select(item => item.TeamLineageId).Distinct().Count());
        Assert.All(teams, item => Assert.Null(item.PreviousTeamId));
        Assert.Equal(
            [2024, 2026],
            (await db.GetSeasonsAsync()).Where(item => item.TeamId == 3).Select(item => item.Year)
        );
        Assert.Equal(
            1,
            await db.UseConnectionAsync(connection =>
                connection.ExecuteScalarAsync<int>("PRAGMA user_version")
            )
        );
    }

    [Fact]
    public async Task Migration_duplicate_normalized_name_and_year_fails_without_repair_or_version_advance()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        await db.ExecuteAsync(
            "CREATE TABLE Team (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, IsActive INTEGER NOT NULL)"
        );
        await db.ExecuteAsync(
            "CREATE TABLE Season (Id INTEGER PRIMARY KEY AUTOINCREMENT, TeamId INTEGER NOT NULL, Name TEXT NOT NULL, Year INTEGER NOT NULL)"
        );
        await db.ExecuteAsync(
            "INSERT INTO Team (Name, IsActive) VALUES (' Misfits ', 1), ('misfits', 0)"
        );
        await db.ExecuteAsync(
            "INSERT INTO Season (TeamId, Name, Year) VALUES (1, 'A', 2026), (2, 'B', 2026)"
        );

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.Database.InitializeAsync()
        );

        Assert.Contains("duplicate Team name/year", error.Message);
        Assert.Equal(
            2,
            (await db.GetPlayersAsync()).Count
                + await db.UseConnectionAsync(connection =>
                    connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Team")
                )
        );
        Assert.Equal(
            0,
            await db.UseConnectionAsync(connection =>
                connection.ExecuteScalarAsync<int>("PRAGMA user_version")
            )
        );
    }

    [Fact]
    public async Task Copy_creates_next_team_year_and_copies_only_approved_current_state()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var source = await CreateTeamAsync(db, "Misfits", 2026);
        var active = await CreatePlayerAsync(db, source.Id, "Active", true, true, 12);
        var inactiveMembership = await CreatePlayerAsync(db, source.Id, "Former", false, true, 7);
        var inactivePlayer = await CreatePlayerAsync(db, source.Id, "Inactive", true, false, null);
        var balanceOnly = new Player { FirstName = "Credit" };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(balanceOnly));
        await db.SeedSeasonAsync(
            new Season
            {
                TeamId = source.Id,
                Name = "Spring",
                Year = 2026,
            }
        );
        var legacy = new Season
        {
            TeamId = source.Id,
            Name = "Legacy",
            Year = 2025,
        };
        await db.SeedSeasonAsync(legacy);
        await db.SeedGameAsync(
            new Game
            {
                TeamId = source.Id,
                SeasonId = legacy.Id,
                ScheduledDate = new(2025, 1, 1),
                ScheduledTime = TimeSpan.FromHours(18),
            }
        );
        await SeedAutomaticAsync(db, source.Id, active.Id, BeerRuleType.Dbo, 5);
        Assert.True(
            (
                await db.CreateBeerAccountingRepository().SetBroughtAsync(source.Id, active.Id, 2)
            ).IsSuccess
        );
        await db.SeedBeerLedgerEntryAsync(
            new BeerLedgerEntry
            {
                TeamId = source.Id,
                PlayerId = balanceOnly.Id,
                Origin = BeerLedgerEntryOrigin.BroughtCredit,
                Amount = 2,
            }
        );
        var settings = (await db.CreateBeerRuleConfigurationRepository().GetTeamAsync(source.Id))!
            .Select(item =>
                item.RuleType == BeerRuleType.Dbo
                    ? item with
                    {
                        IsEnabled = false,
                        Amount = 9,
                    }
                    : item
            )
            .ToArray();
        Assert.True(
            await db.CreateBeerRuleConfigurationRepository().SaveTeamAsync(source.Id, settings)
        );
        var sourceEntries = await db.GetBeerLedgerEntriesAsync();
        var sourceIdentity = (
            source.Name,
            source.Year,
            source.TeamLineageId,
            source.PreviousTeamId,
            source.IsActive
        );

        var result = await db.CreateTeamYearCopyRepository().CopyAsync(source.Id);

        Assert.True(result.IsSuccess);
        var destination = result.DestinationTeam!;
        Assert.Equal(
            ("Misfits", 2027, source.TeamLineageId, source.Id),
            (
                destination.Name,
                destination.Year,
                destination.TeamLineageId,
                destination.PreviousTeamId
            )
        );
        var destinationRoster = (await db.GetTeamPlayersAsync())
            .Where(item => item.TeamId == destination.Id)
            .ToArray();
        var copiedMember = Assert.Single(destinationRoster);
        Assert.Equal((active.Id, 12), (copiedMember.PlayerId, copiedMember.JerseyNumber));
        Assert.DoesNotContain(
            destinationRoster,
            item =>
                item.PlayerId == inactiveMembership.Id
                || item.PlayerId == inactivePlayer.Id
                || item.PlayerId == balanceOnly.Id
        );
        var destinationSeasons = (await db.GetSeasonsAsync())
            .Where(item => item.TeamId == destination.Id)
            .ToArray();
        Assert.Equal("Spring", Assert.Single(destinationSeasons).Name);
        Assert.Equal(2027, destinationSeasons[0].Year);
        Assert.DoesNotContain(await db.GetGamesAsync(), item => item.TeamId == destination.Id);
        var copiedSettings = (
            await db.CreateBeerRuleConfigurationRepository().GetTeamAsync(destination.Id)
        )!;
        Assert.Equal(
            7,
            (await db.GetTeamBeerRuleConfigurationsAsync()).Count(item =>
                item.TeamId == destination.Id
            )
        );
        Assert.Equal(
            (false, 9),
            copiedSettings.Single(item => item.RuleType == BeerRuleType.Dbo) is var dbo
                ? (dbo.IsEnabled, dbo.Amount)
                : default
        );
        var carry = (await db.GetBeerLedgerEntriesAsync())
            .Where(item => item.TeamId == destination.Id)
            .OrderBy(item => item.PlayerId)
            .ToArray();
        Assert.Equal(2, carry.Length);
        Assert.All(
            carry,
            item =>
            {
                Assert.Equal(BeerLedgerEntryOrigin.CarryForward, item.Origin);
                Assert.Equal(source.Id, item.SourceTeamId);
                Assert.Null(item.RuleType);
                Assert.Null(item.GameId);
                Assert.Null(item.GameScoringSessionId);
                Assert.Null(item.GamePlateAppearanceId);
                Assert.Null(item.PlayingDate);
                Assert.Null(item.RuleCountAdjustment);
            }
        );
        Assert.Equal(3, carry.Single(item => item.PlayerId == active.Id).Amount);
        Assert.Equal(-2, carry.Single(item => item.PlayerId == balanceOnly.Id).Amount);
        var summaries = (
            await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(destination.Id)
        )!;
        Assert.All(
            summaries,
            item => Assert.Equal(0, item.GetEffectiveRuleCount(BeerRuleType.Dbo))
        );
        Assert.All(summaries, item => Assert.Equal(0, item.Brought));
        Assert.Equal(-2, summaries.Single(item => item.PlayerId == balanceOnly.Id).Owed);
        Assert.Equal(
            sourceEntries.Select(item => item.Id),
            (await db.GetBeerLedgerEntriesAsync())
                .Where(item => item.TeamId == source.Id)
                .Select(item => item.Id)
        );
        var persistedSource = Assert.IsType<Team>(
            await db.CreateTeamRepository().GetActiveByIdAsync(source.Id)
        );
        Assert.Equal(
            sourceIdentity,
            (
                persistedSource.Name,
                persistedSource.Year,
                persistedSource.TeamLineageId,
                persistedSource.PreviousTeamId,
                persistedSource.IsActive
            )
        );
        Assert.True(await db.CreateTeamRepository().HasSuccessorAsync(source.Id));
        Assert.False(await db.CreateTeamRepository().HasSuccessorAsync(destination.Id));
    }

    [Fact]
    public async Task Copy_rejects_branch_year_limit_and_name_year_collisions_without_partial_rows()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var source = await CreateTeamAsync(db, "Branch", 2026);
        Assert.True((await db.CreateTeamYearCopyRepository().CopyAsync(source.Id)).IsSuccess);
        var count = (await db.CreateTeamRepository().GetActiveAsync()).Count;
        var second = await db.CreateTeamYearCopyRepository().CopyAsync(source.Id);
        Assert.Equal(CopyTeamToNewYearFailure.DestinationExists, second.FailureReason);
        Assert.Equal(count, (await db.CreateTeamRepository().GetActiveAsync()).Count);

        var terminal = await CreateTeamAsync(db, "Terminal", 9999);
        Assert.Equal(
            CopyTeamToNewYearFailure.InvalidSourceYear,
            (await db.CreateTeamYearCopyRepository().CopyAsync(terminal.Id)).FailureReason
        );

        var collidingSource = await CreateTeamAsync(db, "Collision", 2026);
        var inactiveDestination = await CreateTeamAsync(db, "collision", 2027);
        Assert.Equal(1, await db.CreateTeamRepository().DeactivateAsync(inactiveDestination.Id));
        var collision = await db.CreateTeamYearCopyRepository().CopyAsync(collidingSource.Id);
        Assert.Equal(CopyTeamToNewYearFailure.DestinationExists, collision.FailureReason);
        Assert.Equal(inactiveDestination.Id, collision.ExistingTeamId);
    }

    [Fact]
    public async Task Multi_year_chain_carries_the_opening_balance_once_with_immediate_predecessors()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateTeamAsync(db, "Chain", 2026);
        var player = await CreatePlayerAsync(db, first.Id, "Player", true, true, null);
        await SeedAutomaticAsync(db, first.Id, player.Id, BeerRuleType.Dbo, 3);

        var second = (await db.CreateTeamYearCopyRepository().CopyAsync(first.Id)).DestinationTeam!;
        var third = (await db.CreateTeamYearCopyRepository().CopyAsync(second.Id)).DestinationTeam!;

        Assert.Equal(first.TeamLineageId, third.TeamLineageId);
        Assert.Equal(first.Id, second.PreviousTeamId);
        Assert.Equal(second.Id, third.PreviousTeamId);
        var thirdCarry = Assert.Single(
            (await db.GetBeerLedgerEntriesAsync()).Where(item => item.TeamId == third.Id)
        );
        Assert.Equal(
            (second.Id, player.Id, 3L),
            (thirdCarry.SourceTeamId, thirdCarry.PlayerId, thirdCarry.Amount)
        );
        Assert.Equal(
            3,
            Assert
                .Single((await db.CreateBeerAccountingRepository().GetTeamLifetimeAsync(third.Id))!)
                .Owed
        );
    }

    [Fact]
    public async Task Zero_balance_creates_no_carry_and_database_rejects_a_duplicate_carry_row()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var source = await CreateTeamAsync(db, "Settled", 2026);
        var settled = await CreatePlayerAsync(db, source.Id, "Settled", true, true, null);
        await SeedAutomaticAsync(db, source.Id, settled.Id, BeerRuleType.Dbo, 2);
        Assert.True(
            (
                await db.CreateBeerAccountingRepository().SetBroughtAsync(source.Id, settled.Id, 2)
            ).IsSuccess
        );

        var destination = (
            await db.CreateTeamYearCopyRepository().CopyAsync(source.Id)
        ).DestinationTeam!;

        Assert.DoesNotContain(
            await db.GetBeerLedgerEntriesAsync(),
            item => item.TeamId == destination.Id
        );
        var carry = new BeerLedgerEntry
        {
            TeamId = destination.Id,
            PlayerId = settled.Id,
            SourceTeamId = source.Id,
            Origin = BeerLedgerEntryOrigin.CarryForward,
            Amount = 1,
        };
        Assert.Equal(1, await db.SeedBeerLedgerEntryAsync(carry));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.SeedBeerLedgerEntryAsync(
                new BeerLedgerEntry
                {
                    TeamId = destination.Id,
                    PlayerId = settled.Id,
                    SourceTeamId = source.Id,
                    Origin = BeerLedgerEntryOrigin.CarryForward,
                    Amount = 2,
                }
            )
        );
    }

    [Fact]
    public async Task Persistence_failure_after_destination_creation_rolls_back_every_copied_row()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var source = await CreateTeamAsync(db, "Rollback", 2026);
        await CreatePlayerAsync(db, source.Id, "Player", true, true, 4);
        await db.SeedSeasonAsync(
            new Season
            {
                TeamId = source.Id,
                Name = "Summer",
                Year = source.Year,
            }
        );
        await db.ExecuteAsync(
            $"CREATE TRIGGER FailCopiedMembership BEFORE INSERT ON TeamPlayer WHEN NEW.TeamId != {source.Id} BEGIN SELECT RAISE(ABORT, 'injected copy failure'); END"
        );

        var result = await db.CreateTeamYearCopyRepository().CopyAsync(source.Id);

        Assert.Equal(CopyTeamToNewYearFailure.PersistenceError, result.FailureReason);
        Assert.Single(await db.CreateTeamRepository().GetActiveAsync());
        Assert.Single(await db.GetSeasonsAsync());
        Assert.Single(await db.GetTeamPlayersAsync());
        Assert.Empty(await db.GetTeamBeerRuleConfigurationsAsync());
        Assert.Empty(await db.GetBeerLedgerEntriesAsync());
    }

    private static async Task<Team> CreateTeamAsync(TemporaryTestDatabase db, string name, int year)
    {
        var team = new Team { Name = name, Year = year };
        Assert.Equal(1, await db.CreateTeamRepository().CreateAsync(team));
        return team;
    }

    private static async Task<Player> CreatePlayerAsync(
        TemporaryTestDatabase db,
        int teamId,
        string name,
        bool membershipActive,
        bool playerActive,
        int? jersey
    )
    {
        var player = new Player { FirstName = name, IsActive = playerActive };
        Assert.Equal(1, await db.CreatePlayerRepository().CreateAsync(player));
        Assert.Equal(
            1,
            await db.SeedTeamPlayerAsync(
                new TeamPlayer
                {
                    TeamId = teamId,
                    PlayerId = player.Id,
                    IsActive = membershipActive,
                    JerseyNumber = jersey,
                }
            )
        );
        return player;
    }

    private static Task<int> SeedAutomaticAsync(
        TemporaryTestDatabase db,
        int teamId,
        int playerId,
        BeerRuleType ruleType,
        long amount
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
}
