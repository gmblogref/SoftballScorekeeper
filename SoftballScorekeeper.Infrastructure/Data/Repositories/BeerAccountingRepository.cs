using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class BeerAccountingRepository(AppDatabase database)
{
    public async Task<IReadOnlyList<TeamBeerPlayerSummary>?> GetTeamLifetimeAsync(int teamId)
    {
        if (teamId <= 0)
            return null;

        var connection = await database.GetConnectionAsync();
        if (await connection.FindAsync<Team>(teamId) is null)
            return null;

        var memberships = await connection
            .Table<TeamPlayer>()
            .Where(item => item.TeamId == teamId)
            .ToListAsync();
        var entries = await connection
            .Table<BeerLedgerEntry>()
            .Where(item => item.TeamId == teamId)
            .OrderBy(item => item.Id)
            .ToListAsync();
        var playerIds = memberships
            .Select(item => item.PlayerId)
            .Concat(entries.Select(item => item.PlayerId))
            .ToHashSet();
        var players = (await connection.Table<Player>().ToListAsync())
            .Where(item => playerIds.Contains(item.Id))
            .OrderBy(item => item.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();

        return players.Select(player => BuildSummary(player, entries)).ToArray();
    }

    public Task<BeerAccountingUpdateResult> SetEffectiveRuleCountAsync(
        int teamId,
        int playerId,
        BeerRuleType ruleType,
        int desiredEffectiveCount
    )
    {
        if (desiredEffectiveCount < 0)
            return Task.FromResult(Failure(BeerAccountingUpdateFailure.InvalidValue));
        if (!Enum.IsDefined(ruleType))
            return Task.FromResult(Failure(BeerAccountingUpdateFailure.InvalidRule));

        return UpdateAsync(
            teamId,
            playerId,
            transaction =>
                SetEffectiveRuleCount(
                    transaction,
                    teamId,
                    playerId,
                    ruleType,
                    desiredEffectiveCount
                )
        );
    }

    public Task<BeerAccountingUpdateResult> SetBroughtAsync(int teamId, int playerId, int brought)
    {
        if (brought < 0)
            return Task.FromResult(Failure(BeerAccountingUpdateFailure.InvalidValue));

        return UpdateAsync(
            teamId,
            playerId,
            transaction => SetBrought(transaction, teamId, playerId, brought)
        );
    }

    private async Task<BeerAccountingUpdateResult> UpdateAsync(
        int teamId,
        int playerId,
        Func<SQLiteConnection, BeerAccountingUpdateResult> update
    )
    {
        if (teamId <= 0)
            return Failure(BeerAccountingUpdateFailure.TeamNotFound);
        if (playerId <= 0)
            return Failure(BeerAccountingUpdateFailure.PlayerNotFound);

        var connection = await database.GetConnectionAsync();
        var result = Failure(BeerAccountingUpdateFailure.PersistenceError);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var validation = ValidateContext(transaction, teamId, playerId);
                result = validation.IsSuccess ? update(transaction) : validation;
            });
        }
        catch
        {
            return Failure(BeerAccountingUpdateFailure.PersistenceError);
        }

        return result;
    }

    private static BeerAccountingUpdateResult ValidateContext(
        SQLiteConnection transaction,
        int teamId,
        int playerId
    )
    {
        if (transaction.Find<Team>(teamId) is null)
            return Failure(BeerAccountingUpdateFailure.TeamNotFound);
        if (transaction.Find<Player>(playerId) is null)
            return Failure(BeerAccountingUpdateFailure.PlayerNotFound);
        var isAssociated = transaction
            .Table<TeamPlayer>()
            .Any(item => item.TeamId == teamId && item.PlayerId == playerId);
        var hasHistory = transaction
            .Table<BeerLedgerEntry>()
            .Any(item => item.TeamId == teamId && item.PlayerId == playerId);
        return isAssociated || hasHistory
            ? BeerAccountingUpdateResult.Success
            : Failure(BeerAccountingUpdateFailure.PlayerNotAssociatedWithTeam);
    }

    private static BeerAccountingUpdateResult SetEffectiveRuleCount(
        SQLiteConnection transaction,
        int teamId,
        int playerId,
        BeerRuleType ruleType,
        int desiredEffectiveCount
    )
    {
        var entries = transaction
            .Table<BeerLedgerEntry>()
            .Where(item =>
                item.TeamId == teamId && item.PlayerId == playerId && item.RuleType == ruleType
            )
            .OrderBy(item => item.Id)
            .ToList();
        var automatic = entries
            .Where(item => item.Origin == BeerLedgerEntryOrigin.AutomaticScoring)
            .ToArray();
        var existingAdjustments = entries
            .Where(item =>
                item.Origin == BeerLedgerEntryOrigin.ManualAdjustment
                && item.RuleCountAdjustment.HasValue
            )
            .ToArray();
        var currentEffectiveCount = Math.Max(
            0L,
            automatic.Length
                + existingAdjustments.Sum(item => (long)item.RuleCountAdjustment!.Value)
        );
        if (currentEffectiveCount == desiredEffectiveCount)
            return BeerAccountingUpdateResult.Success;

        long effectiveAmount;
        if (desiredEffectiveCount <= automatic.Length)
        {
            effectiveAmount = automatic.Take(desiredEffectiveCount).Sum(item => (long)item.Amount);
        }
        else
        {
            var persisted = transaction
                .Table<TeamBeerRuleConfiguration>()
                .Where(item => item.TeamId == teamId)
                .ToList();
            var currentAmount = BeerRuleConfigurationRepository
                .ResolveTeamSettings(persisted)
                .Single(item => item.RuleType == ruleType)
                .Amount;
            effectiveAmount =
                automatic.Sum(item => (long)item.Amount)
                + ((long)desiredEffectiveCount - automatic.Length) * currentAmount;
        }

        var automaticAmount = automatic.Sum(item => (long)item.Amount);
        var countAdjustment = (long)desiredEffectiveCount - automatic.Length;
        var amountAdjustment = effectiveAmount - automaticAmount;
        if (
            countAdjustment is < int.MinValue or > int.MaxValue
            || amountAdjustment is < int.MinValue or > int.MaxValue
        )
            return Failure(BeerAccountingUpdateFailure.ValueTooLarge);

        transaction.Execute(
            "DELETE FROM BeerLedgerEntry WHERE TeamId = ? AND PlayerId = ? AND RuleType = ? AND Origin = ?",
            teamId,
            playerId,
            ruleType,
            BeerLedgerEntryOrigin.ManualAdjustment
        );

        if (countAdjustment == 0 && amountAdjustment == 0)
            return BeerAccountingUpdateResult.Success;

        var inserted = transaction.Insert(
            new BeerLedgerEntry
            {
                TeamId = teamId,
                PlayerId = playerId,
                RuleType = ruleType,
                Origin = BeerLedgerEntryOrigin.ManualAdjustment,
                RuleCountAdjustment = (int)countAdjustment,
                Amount = (int)amountAdjustment,
            }
        );
        if (inserted != 1)
            throw new InvalidOperationException("Beer count correction insertion failed.");
        return BeerAccountingUpdateResult.Success;
    }

    private static BeerAccountingUpdateResult SetBrought(
        SQLiteConnection transaction,
        int teamId,
        int playerId,
        int brought
    )
    {
        transaction.Execute(
            "DELETE FROM BeerLedgerEntry WHERE TeamId = ? AND PlayerId = ? AND Origin = ?",
            teamId,
            playerId,
            BeerLedgerEntryOrigin.BroughtCredit
        );
        if (brought == 0)
            return BeerAccountingUpdateResult.Success;

        var inserted = transaction.Insert(
            new BeerLedgerEntry
            {
                TeamId = teamId,
                PlayerId = playerId,
                Origin = BeerLedgerEntryOrigin.BroughtCredit,
                Amount = brought,
            }
        );
        if (inserted != 1)
            throw new InvalidOperationException("Beer brought credit insertion failed.");
        return BeerAccountingUpdateResult.Success;
    }

    private static TeamBeerPlayerSummary BuildSummary(
        Player player,
        IReadOnlyList<BeerLedgerEntry> teamEntries
    )
    {
        var playerEntries = teamEntries.Where(item => item.PlayerId == player.Id).ToArray();
        var counts = new Dictionary<BeerRuleType, int>();
        long obligation = 0;
        foreach (
            var ruleType in BeerRuleConfigurationDefaults.TeamRules.Select(item => item.RuleType)
        )
        {
            var automatic = playerEntries
                .Where(item =>
                    item.RuleType == ruleType
                    && item.Origin == BeerLedgerEntryOrigin.AutomaticScoring
                )
                .ToArray();
            var manual = playerEntries
                .Where(item =>
                    item.RuleType == ruleType
                    && item.Origin == BeerLedgerEntryOrigin.ManualAdjustment
                    && item.RuleCountAdjustment.HasValue
                )
                .ToArray();
            var countAdjustment = manual.Sum(item => (long)item.RuleCountAdjustment!.Value);
            var effectiveCount = (int)Math.Max(0L, automatic.Length + countAdjustment);
            counts[ruleType] = effectiveCount;
            obligation +=
                countAdjustment < 0
                    ? automatic.Take(effectiveCount).Sum(item => (long)item.Amount)
                    : Math.Max(
                        0L,
                        automatic.Sum(item => (long)item.Amount)
                            + manual.Sum(item => (long)item.Amount)
                    );
        }

        var brought = playerEntries
            .Where(item => item.Origin == BeerLedgerEntryOrigin.BroughtCredit && item.Amount >= 0)
            .Sum(item => (long)item.Amount);
        var displayName = string.Join(
            " ",
            new[] { player.FirstName, player.LastName }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
        );
        return new TeamBeerPlayerSummary(
            player.Id,
            displayName,
            counts[BeerRuleType.Dbo],
            counts[BeerRuleType.SoloHomeRun],
            counts[BeerRuleType.DoublePlay],
            counts[BeerRuleType.TriplePlay],
            counts[BeerRuleType.TwoOutsInOneInning],
            counts[BeerRuleType.Strikeout],
            counts[BeerRuleType.LowMan],
            brought,
            obligation - brought
        );
    }

    private static BeerAccountingUpdateResult Failure(BeerAccountingUpdateFailure failure) =>
        new(failure);
}
