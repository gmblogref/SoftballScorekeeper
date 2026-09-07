using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class BeerLedgerEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TeamId { get; set; }

    [Indexed]
    public int? GameId { get; set; }

    [Indexed]
    public int? GameScoringSessionId { get; set; }

    [Indexed]
    public int PlayerId { get; set; }

    public BeerRuleType? RuleType { get; set; }

    public BeerLedgerEntryOrigin Origin { get; set; }

    public int? GamePlateAppearanceId { get; set; }

    public int Amount { get; set; }
}

public enum BeerRuleType
{
    Dbo = 1,
    SoloHomeRun = 2,
    DoublePlay = 3,
    TwoOutsInOneInning = 4,
    Strikeout = 5,
    LowMan = 6,
}

public enum BeerLedgerEntryOrigin
{
    AutomaticScoring = 1,
    ManualAdjustment = 2,
    BroughtCredit = 3,
}
