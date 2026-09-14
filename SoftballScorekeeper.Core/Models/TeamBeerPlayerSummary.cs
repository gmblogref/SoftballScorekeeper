namespace SoftballScorekeeper.Core.Models;

public sealed record TeamBeerPlayerSummary(
    int PlayerId,
    string PlayerDisplayName,
    int DboCount,
    int SoloHomeRunCount,
    int DoublePlayCount,
    int TriplePlayCount,
    int TwoOutsInOneInningCount,
    int StrikeoutCount,
    int LowManCount,
    long Brought,
    long Owed
)
{
    public int GetEffectiveRuleCount(BeerRuleType ruleType) =>
        ruleType switch
        {
            BeerRuleType.Dbo => DboCount,
            BeerRuleType.SoloHomeRun => SoloHomeRunCount,
            BeerRuleType.DoublePlay => DoublePlayCount,
            BeerRuleType.TriplePlay => TriplePlayCount,
            BeerRuleType.TwoOutsInOneInning => TwoOutsInOneInningCount,
            BeerRuleType.Strikeout => StrikeoutCount,
            BeerRuleType.LowMan => LowManCount,
            _ => throw new ArgumentOutOfRangeException(nameof(ruleType)),
        };
}

public enum BeerAccountingUpdateFailure
{
    None = 0,
    InvalidValue = 1,
    InvalidRule = 2,
    TeamNotFound = 3,
    PlayerNotFound = 4,
    PlayerNotAssociatedWithTeam = 5,
    ValueTooLarge = 6,
    PersistenceError = 7,
    TeamYearFinalized = 8,
}

public sealed record BeerAccountingUpdateResult(BeerAccountingUpdateFailure FailureReason)
{
    public bool IsSuccess => FailureReason == BeerAccountingUpdateFailure.None;

    public static BeerAccountingUpdateResult Success { get; } =
        new(BeerAccountingUpdateFailure.None);
}
