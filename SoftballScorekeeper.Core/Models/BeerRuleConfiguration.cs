using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class TeamBeerRuleConfiguration
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_TeamBeerRuleConfiguration_Team_Rule", 1, Unique = true)]
    public int TeamId { get; set; }

    [Indexed("UX_TeamBeerRuleConfiguration_Team_Rule", 2, Unique = true)]
    public BeerRuleType RuleType { get; set; }

    public bool IsEnabled { get; set; }

    public int Amount { get; set; }
}

public sealed class GameScoringBeerRuleConfiguration
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringBeerRuleConfiguration_Session_Rule", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    [Indexed("UX_GameScoringBeerRuleConfiguration_Session_Rule", 2, Unique = true)]
    public BeerRuleType RuleType { get; set; }

    public bool IsEnabled { get; set; }

    public int Amount { get; set; }
}

public sealed record BeerRuleSetting(BeerRuleType RuleType, bool IsEnabled, int Amount);

public static class BeerRuleConfigurationDefaults
{
    private static readonly BeerRuleSetting[] teamRules =
    [
        new(BeerRuleType.Dbo, true, 1),
        new(BeerRuleType.SoloHomeRun, true, 1),
        new(BeerRuleType.DoublePlay, true, 1),
        new(BeerRuleType.TriplePlay, true, 1),
        new(BeerRuleType.TwoOutsInOneInning, true, 1),
        new(BeerRuleType.Strikeout, true, 2),
        new(BeerRuleType.LowMan, true, 1),
    ];

    private static readonly BeerRuleSetting[] scoringRules = teamRules
        .Where(item => item.RuleType != BeerRuleType.LowMan)
        .ToArray();

    public static IReadOnlyList<BeerRuleSetting> TeamRules => teamRules;

    public static IReadOnlyList<BeerRuleSetting> ScoringRules => scoringRules;

    public static BeerRuleSetting For(BeerRuleType ruleType) =>
        teamRules.Single(item => item.RuleType == ruleType);
}
