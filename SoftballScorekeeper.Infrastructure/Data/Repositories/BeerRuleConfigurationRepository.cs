using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class BeerRuleConfigurationRepository(AppDatabase database)
{
    public async Task<IReadOnlyList<BeerRuleSetting>?> GetTeamAsync(int teamId)
    {
        if (teamId <= 0)
            return null;
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        if (await connection.FindAsync<Team>(teamId) is null)
            return null;
        var persisted = await connection
            .Table<TeamBeerRuleConfiguration>()
            .Where(item => item.TeamId == teamId)
            .ToListAsync();
        return ResolveTeamSettings(persisted);
    }

    public async Task<bool> SaveTeamAsync(int teamId, IReadOnlyList<BeerRuleSetting>? settings)
    {
        if (teamId <= 0 || !IsValidCompleteSet(settings, BeerRuleConfigurationDefaults.TeamRules))
            return false;
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var saved = false;
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                if (!TeamYearWriteGuard.IsOpen(transaction, teamId))
                    return;
                UpsertTeamSettings(transaction, teamId, settings!);
                saved = true;
            });
        }
        catch
        {
            return false;
        }
        return saved;
    }

    public async Task<IReadOnlyList<BeerRuleSetting>?> GetGameAsync(int gameId)
    {
        if (gameId <= 0)
            return null;
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var session = await connection
            .Table<GameScoringSession>()
            .Where(item => item.GameId == gameId)
            .FirstOrDefaultAsync();
        if (session is null)
            return null;
        var persisted = await connection
            .Table<GameScoringBeerRuleConfiguration>()
            .Where(item => item.GameScoringSessionId == session.Id)
            .ToListAsync();
        return ResolveCompleteGameSettings(persisted);
    }

    public async Task<bool> SaveGameAsync(int gameId, IReadOnlyList<BeerRuleSetting>? settings)
    {
        if (
            gameId <= 0
            || !IsValidCompleteSet(settings, BeerRuleConfigurationDefaults.ScoringRules)
        )
            return false;
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        var saved = false;
        try
        {
            await connection.RunInTransactionAsync(transaction =>
            {
                var session = transaction
                    .Table<GameScoringSession>()
                    .FirstOrDefault(item => item.GameId == gameId);
                if (session is null
                    || session.Status != GameScoringStatus.InProgress
                    || TeamYearWriteGuard.IsGameFinalized(transaction, gameId))
                    return;
                var existing = transaction
                    .Table<GameScoringBeerRuleConfiguration>()
                    .Where(item => item.GameScoringSessionId == session.Id)
                    .ToDictionary(item => item.RuleType);
                if (existing.Count != BeerRuleConfigurationDefaults.ScoringRules.Count)
                    throw new InvalidOperationException(
                        "The Game Beer configuration snapshot is incomplete."
                    );
                foreach (var setting in settings!)
                {
                    var row = existing[setting.RuleType];
                    row.IsEnabled = setting.IsEnabled;
                    row.Amount = setting.Amount;
                    if (transaction.Update(row) != 1)
                        throw new InvalidOperationException(
                            "Game Beer configuration update did not affect one row."
                        );
                }
                saved = true;
            });
        }
        catch
        {
            return false;
        }
        return saved;
    }

    internal static void InsertGameSnapshot(
        SQLiteConnection transaction,
        int teamId,
        int gameScoringSessionId
    )
    {
        var persistedTeamSettings = transaction
            .Table<TeamBeerRuleConfiguration>()
            .Where(item => item.TeamId == teamId)
            .ToList();
        foreach (
            var setting in ResolveTeamSettings(persistedTeamSettings)
                .Where(item => item.RuleType != BeerRuleType.LowMan)
        )
        {
            if (
                transaction.Insert(
                    new GameScoringBeerRuleConfiguration
                    {
                        GameScoringSessionId = gameScoringSessionId,
                        RuleType = setting.RuleType,
                        IsEnabled = setting.IsEnabled,
                        Amount = setting.Amount,
                    }
                ) != 1
            )
                throw new InvalidOperationException(
                    "Game Beer configuration insertion did not affect one row."
                );
        }
    }

    internal static void EnsureExistingGameSnapshots(SQLiteConnection transaction)
    {
        foreach (var session in transaction.Table<GameScoringSession>().ToList())
        {
            if (
                transaction
                    .Table<GameScoringBeerRuleConfiguration>()
                    .Any(item => item.GameScoringSessionId == session.Id)
            )
                continue;
            var game = transaction.Find<Game>(session.GameId);
            if (game is not null && transaction.Find<Team>(game.TeamId) is not null)
                InsertGameSnapshot(transaction, game.TeamId, session.Id);
        }
    }

    internal static IReadOnlyList<BeerRuleSetting> ResolveTeamSettings(
        IReadOnlyList<TeamBeerRuleConfiguration> persisted
    )
    {
        var byRule = persisted
            .Where(item => Enum.IsDefined(item.RuleType) && item.Amount > 0)
            .GroupBy(item => item.RuleType)
            .ToDictionary(group => group.Key, group => group.First());
        return BeerRuleConfigurationDefaults
            .TeamRules.Select(defaultSetting =>
            {
                if (!byRule.TryGetValue(defaultSetting.RuleType, out var row))
                    return defaultSetting;
                return new BeerRuleSetting(row.RuleType, row.IsEnabled, row.Amount);
            })
            .ToArray();
    }

    private static IReadOnlyList<BeerRuleSetting> ResolveCompleteGameSettings(
        IReadOnlyList<GameScoringBeerRuleConfiguration> persisted
    )
    {
        var byRule = persisted.ToDictionary(item => item.RuleType);
        if (
            byRule.Count != BeerRuleConfigurationDefaults.ScoringRules.Count
            || BeerRuleConfigurationDefaults.ScoringRules.Any(item =>
                !byRule.TryGetValue(item.RuleType, out var row) || row.Amount <= 0
            )
        )
            throw new InvalidOperationException("The Game Beer configuration snapshot is invalid.");
        return BeerRuleConfigurationDefaults
            .ScoringRules.Select(item =>
            {
                var row = byRule[item.RuleType];
                return new BeerRuleSetting(row.RuleType, row.IsEnabled, row.Amount);
            })
            .ToArray();
    }

    private static bool IsValidCompleteSet(
        IReadOnlyList<BeerRuleSetting>? settings,
        IReadOnlyList<BeerRuleSetting> expected
    )
    {
        if (
            settings is null
            || settings.Count != expected.Count
            || settings.Any(item => !Enum.IsDefined(item.RuleType) || item.Amount <= 0)
        )
            return false;
        var ruleTypes = settings.Select(item => item.RuleType).ToArray();
        return ruleTypes.Distinct().Count() == ruleTypes.Length
            && ruleTypes.Order().SequenceEqual(expected.Select(item => item.RuleType).Order());
    }

    private static void UpsertTeamSettings(
        SQLiteConnection transaction,
        int teamId,
        IReadOnlyList<BeerRuleSetting> settings
    )
    {
        var existing = transaction
            .Table<TeamBeerRuleConfiguration>()
            .Where(item => item.TeamId == teamId)
            .ToDictionary(item => item.RuleType);
        foreach (var setting in settings)
        {
            if (existing.TryGetValue(setting.RuleType, out var row))
            {
                row.IsEnabled = setting.IsEnabled;
                row.Amount = setting.Amount;
                if (transaction.Update(row) != 1)
                    throw new InvalidOperationException(
                        "Team Beer configuration update did not affect one row."
                    );
                continue;
            }
            if (
                transaction.Insert(
                    new TeamBeerRuleConfiguration
                    {
                        TeamId = teamId,
                        RuleType = setting.RuleType,
                        IsEnabled = setting.IsEnabled,
                        Amount = setting.Amount,
                    }
                ) != 1
            )
                throw new InvalidOperationException(
                    "Team Beer configuration insertion did not affect one row."
                );
        }
    }
}
