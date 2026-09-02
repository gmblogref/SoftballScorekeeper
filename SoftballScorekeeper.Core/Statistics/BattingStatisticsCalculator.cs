namespace SoftballScorekeeper.Core.Statistics;

public static class BattingStatisticsCalculator
{
    internal static decimal DivideOrZero(int numerator, int denominator) =>
        denominator == 0 ? 0m : (decimal)numerator / denominator;

    public static IReadOnlyList<PlayerBattingStatLine> Calculate(
        IEnumerable<BattingOutcome> outcomes) => Calculate([], outcomes);

    public static IReadOnlyList<PlayerBattingStatLine> Calculate(
        IEnumerable<int> participantPlayerIds,
        IEnumerable<BattingOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(participantPlayerIds);
        ArgumentNullException.ThrowIfNull(outcomes);

        var accumulators = new Dictionary<int, StatAccumulator>();
        var playerOrder = new List<int>();
        foreach (var playerId in participantPlayerIds)
            AddPlayer(playerId, accumulators, playerOrder);

        foreach (var outcome in outcomes)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            AddPlayer(outcome.PlayerId, accumulators, playerOrder);
            accumulators[outcome.PlayerId].Add(
                BatterResultStatContributions.Get(outcome.Result), 1);
        }

        return playerOrder.Select(playerId =>
            accumulators[playerId].ToStatLine(playerId)).ToArray();
    }

    public static PlayerBattingStatLine Calculate(BattingOutcomeCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ValidatePlayerId(counts.PlayerId);

        var accumulator = new StatAccumulator();
        foreach (var (result, count) in counts.GetResultCounts())
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(counts),
                    "Batting outcome counts cannot be negative.");
            accumulator.Add(BatterResultStatContributions.Get(result), count);
        }
        return accumulator.ToStatLine(counts.PlayerId);
    }

    public static TeamBattingStatLine CalculateTeam(
        IEnumerable<PlayerBattingStatLine> playerLines)
    {
        ArgumentNullException.ThrowIfNull(playerLines);

        var plateAppearances = 0;
        var atBats = 0;
        var singles = 0;
        var doubles = 0;
        var triples = 0;
        var homeRuns = 0;
        var walks = 0;
        var fielderChoices = 0;
        var sacrificeFlies = 0;
        foreach (var line in playerLines)
        {
            ArgumentNullException.ThrowIfNull(line);
            plateAppearances = checked(plateAppearances + line.PlateAppearances);
            atBats = checked(atBats + line.AtBats);
            singles = checked(singles + line.Singles);
            doubles = checked(doubles + line.Doubles);
            triples = checked(triples + line.Triples);
            homeRuns = checked(homeRuns + line.HomeRuns);
            walks = checked(walks + line.Walks);
            fielderChoices = checked(fielderChoices + line.FielderChoices);
            sacrificeFlies = checked(sacrificeFlies + line.SacrificeFlies);
        }

        return new TeamBattingStatLine(
            plateAppearances,
            atBats,
            singles,
            doubles,
            triples,
            homeRuns,
            walks,
            fielderChoices,
            sacrificeFlies);
    }

    private static void AddPlayer(
        int playerId,
        IDictionary<int, StatAccumulator> accumulators,
        ICollection<int> playerOrder)
    {
        ValidatePlayerId(playerId);
        if (accumulators.TryAdd(playerId, new())) playerOrder.Add(playerId);
    }

    private static void ValidatePlayerId(int playerId)
    {
        if (playerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerId),
                "Player Id must be positive.");
    }

    private sealed class StatAccumulator
    {
        private int plateAppearances;
        private int atBats;
        private int singles;
        private int doubles;
        private int triples;
        private int homeRuns;
        private int walks;
        private int fielderChoices;
        private int sacrificeFlies;

        public void Add(BatterResultStatContribution contribution, int count)
        {
            plateAppearances = checked(plateAppearances
                + contribution.PlateAppearances * count);
            atBats = checked(atBats + contribution.AtBats * count);
            singles = checked(singles + contribution.Singles * count);
            doubles = checked(doubles + contribution.Doubles * count);
            triples = checked(triples + contribution.Triples * count);
            homeRuns = checked(homeRuns + contribution.HomeRuns * count);
            walks = checked(walks + contribution.Walks * count);
            fielderChoices = checked(fielderChoices
                + contribution.FielderChoices * count);
            sacrificeFlies = checked(sacrificeFlies
                + contribution.SacrificeFlies * count);
        }

        public PlayerBattingStatLine ToStatLine(int playerId) => new(
            playerId, plateAppearances, atBats, singles, doubles, triples,
            homeRuns, walks, fielderChoices, sacrificeFlies);
    }
}
