namespace SoftballScorekeeper.Core.Statistics;

public sealed class PlayerBattingStatLine
{
    internal PlayerBattingStatLine(
        int playerId,
        int plateAppearances,
        int atBats,
        int singles,
        int doubles,
        int triples,
        int homeRuns,
        int walks,
        int fielderChoices,
        int sacrificeFlies)
    {
        PlayerId = playerId;
        PlateAppearances = plateAppearances;
        AtBats = atBats;
        Singles = singles;
        Doubles = doubles;
        Triples = triples;
        HomeRuns = homeRuns;
        Walks = walks;
        FielderChoices = fielderChoices;
        SacrificeFlies = sacrificeFlies;
    }

    public int PlayerId { get; }
    public int PlateAppearances { get; }
    public int AtBats { get; }
    public int Hits => checked(Singles + Doubles + Triples + HomeRuns);
    public int Singles { get; }
    public int Doubles { get; }
    public int Triples { get; }
    public int HomeRuns { get; }
    public int Walks { get; }
    public int FielderChoices { get; }
    public int SacrificeFlies { get; }
    public int TotalBases => checked(Singles + 2 * Doubles + 3 * Triples + 4 * HomeRuns);
    public decimal BattingAverage => BattingStatisticsCalculator.DivideOrZero(Hits, AtBats);
    public decimal OnBasePercentage => BattingStatisticsCalculator.DivideOrZero(
        checked(Hits + Walks), checked(AtBats + Walks + SacrificeFlies));
    public decimal SluggingPercentage =>
        BattingStatisticsCalculator.DivideOrZero(TotalBases, AtBats);
}
