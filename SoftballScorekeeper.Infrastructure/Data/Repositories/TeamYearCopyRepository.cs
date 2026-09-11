using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class TeamYearCopyRepository(AppDatabase database)
{
    public async Task<CopyTeamToNewYearResult> CopyAsync(int sourceTeamId)
    {
        if (sourceTeamId <= 0)
            return new(CopyTeamToNewYearFailure.SourceNotFound);

        var connection = await database.GetConnectionAsync();
        var result = new CopyTeamToNewYearResult(CopyTeamToNewYearFailure.PersistenceError);
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var source = transaction.Find<Team>(sourceTeamId);
                if (source is null || !source.IsActive)
                {
                    result = new(CopyTeamToNewYearFailure.SourceNotFound);
                    return;
                }
                if (source.Year is < 1 or >= 9999 || source.TeamLineageId == Guid.Empty)
                {
                    result = new(CopyTeamToNewYearFailure.InvalidSourceYear);
                    return;
                }

                var destinationYear = source.Year + 1;
                var teams = transaction.Table<Team>().ToList();
                var collision = teams.FirstOrDefault(item =>
                    (item.TeamLineageId == source.TeamLineageId && item.Year == destinationYear)
                    || item.PreviousTeamId == source.Id
                    || (
                        item.Year == destinationYear
                        && string.Equals(
                            item.Name.Trim(),
                            source.Name.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                );
                if (collision is not null)
                {
                    result = new(
                        CopyTeamToNewYearFailure.DestinationExists,
                        ExistingTeamId: collision.Id
                    );
                    return;
                }

                var destination = new Team
                {
                    Name = source.Name.Trim(),
                    Year = destinationYear,
                    TeamLineageId = source.TeamLineageId,
                    PreviousTeamId = source.Id,
                    IsActive = true,
                };
                RequireInsert(transaction, destination, "destination Team");

                foreach (
                    var season in transaction
                        .Table<Season>()
                        .Where(item => item.TeamId == source.Id && item.Year == source.Year)
                        .OrderBy(item => item.Id)
                )
                    RequireInsert(
                        transaction,
                        new Season
                        {
                            TeamId = destination.Id,
                            Name = season.Name,
                            Year = destinationYear,
                        },
                        "League Season"
                    );

                var activePlayerIds = transaction
                    .Table<Player>()
                    .Where(item => item.IsActive)
                    .ToList()
                    .Select(item => item.Id)
                    .ToHashSet();
                foreach (
                    var membership in transaction
                        .Table<TeamPlayer>()
                        .Where(item => item.TeamId == source.Id && item.IsActive)
                        .OrderBy(item => item.Id)
                        .ToList()
                        .Where(item => activePlayerIds.Contains(item.PlayerId))
                        .DistinctBy(item => item.PlayerId)
                )
                    RequireInsert(
                        transaction,
                        new TeamPlayer
                        {
                            TeamId = destination.Id,
                            PlayerId = membership.PlayerId,
                            JerseyNumber = membership.JerseyNumber,
                            IsActive = true,
                        },
                        "roster membership"
                    );

                var sourceConfigurations = transaction
                    .Table<TeamBeerRuleConfiguration>()
                    .Where(item => item.TeamId == source.Id)
                    .ToList();
                foreach (
                    var setting in BeerRuleConfigurationRepository.ResolveTeamSettings(
                        sourceConfigurations
                    )
                )
                    RequireInsert(
                        transaction,
                        new TeamBeerRuleConfiguration
                        {
                            TeamId = destination.Id,
                            RuleType = setting.RuleType,
                            IsEnabled = setting.IsEnabled,
                            Amount = setting.Amount,
                        },
                        "Beer Rule configuration"
                    );

                var sourceMemberships = transaction
                    .Table<TeamPlayer>()
                    .Where(item => item.TeamId == source.Id)
                    .ToList();
                var sourceEntries = transaction
                    .Table<BeerLedgerEntry>()
                    .Where(item => item.TeamId == source.Id)
                    .OrderBy(item => item.Id)
                    .ToList();
                var accountingPlayerIds = sourceMemberships
                    .Select(item => item.PlayerId)
                    .Concat(sourceEntries.Select(item => item.PlayerId))
                    .ToHashSet();
                foreach (
                    var player in transaction
                        .Table<Player>()
                        .ToList()
                        .Where(item => accountingPlayerIds.Contains(item.Id))
                )
                {
                    var summary = BeerAccountingRepository.BuildSummary(player, sourceEntries);
                    if (summary.Owed == 0)
                        continue;
                    RequireInsert(
                        transaction,
                        new BeerLedgerEntry
                        {
                            TeamId = destination.Id,
                            PlayerId = player.Id,
                            SourceTeamId = source.Id,
                            Origin = BeerLedgerEntryOrigin.CarryForward,
                            Amount = summary.Owed,
                        },
                        "Beer carry-forward"
                    );
                }

                result = new(CopyTeamToNewYearFailure.None, destination);
            });
        }
        catch
        {
            return new(CopyTeamToNewYearFailure.PersistenceError);
        }
        return result;
    }

    private static void RequireInsert(SQLiteConnection transaction, object item, string itemName)
    {
        if (transaction.Insert(item) != 1)
            throw new InvalidOperationException($"Copying the {itemName} did not affect one row.");
    }
}
