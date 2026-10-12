using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Core.Statistics;

public sealed record CompletedBattingOutcomeValues(
    int Singles,
    int Doubles,
    int Triples,
    int HomeRuns,
    int Walks,
    int FielderChoices,
    int SacrificeFlies,
    int Outs);

public sealed record CompletedGameBattingSource(
    int GameId,
    IReadOnlyList<int> ParticipantPlayerIds,
    IReadOnlyList<BattingOutcome> Outcomes);

public sealed record EffectiveCompletedBattingPlayer(
    int PlayerId,
    int CanonicalPlateAppearances,
    CompletedBattingOutcomeValues CanonicalValues,
    CompletedBattingOutcomeValues EffectiveValues,
    PlayerBattingStatLine Statistics);

public static class EffectiveCompletedBattingStatistics
{
    public static IReadOnlyList<EffectiveCompletedBattingPlayer> ProjectGame(
        CompletedGameBattingSource source,
        IEnumerable<CompletedGameBattingOverride>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var overrideRows = (overrides ?? []).ToArray();
        var effectiveCounts = ResolveGame(source, overrideRows);
        var canonicalCounts = ResolveGame(source, []);

        return effectiveCounts.Select(item =>
        {
            var canonical = canonicalCounts[item.Key];
            return new EffectiveCompletedBattingPlayer(
                item.Key,
                canonical.PlateAppearances,
                canonical.EditableValues,
                item.Value.EditableValues,
                BattingStatisticsCalculator.Calculate(item.Value.Counts));
        }).ToArray();
    }

    public static IReadOnlyList<PlayerBattingStatLine> Project(
        IEnumerable<CompletedGameBattingSource> sources,
        IEnumerable<CompletedGameBattingOverride>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var sourceRows = sources.ToArray();
        var overrideRows = (overrides ?? []).ToArray();
        if (sourceRows.Select(item => item.GameId).Distinct().Count() != sourceRows.Length)
            throw new ArgumentException("Completed Game sources must have unique Game Ids.", nameof(sources));
        if (overrideRows.Any(item => sourceRows.All(source => source.GameId != item.GameId)))
            throw new ArgumentException("A batting override does not belong to a selected Game.", nameof(overrides));

        var totals = new Dictionary<int, BattingOutcomeCounts>();
        var order = new List<int>();
        foreach (var source in sourceRows)
        {
            var gameOverrides = overrideRows.Where(item => item.GameId == source.GameId).ToArray();
            foreach (var item in ResolveGame(source, gameOverrides))
            {
                if (!totals.TryGetValue(item.Key, out var total))
                {
                    totals[item.Key] = item.Value.Counts;
                    order.Add(item.Key);
                }
                else
                {
                    totals[item.Key] = Add(total, item.Value.Counts);
                }
            }
        }

        return order.Select(playerId => BattingStatisticsCalculator.Calculate(totals[playerId]))
            .ToArray();
    }

    private static Dictionary<int, ResolvedCounts> ResolveGame(
        CompletedGameBattingSource source,
        IReadOnlyCollection<CompletedGameBattingOverride> overrides)
    {
        if (source.GameId <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), "Game Id must be positive.");
        ArgumentNullException.ThrowIfNull(source.ParticipantPlayerIds);
        ArgumentNullException.ThrowIfNull(source.Outcomes);

        var playerOrder = source.ParticipantPlayerIds
            .Concat(source.Outcomes.Select(item => item.PlayerId))
            .Distinct()
            .ToArray();
        if (playerOrder.Any(id => id <= 0))
            throw new ArgumentOutOfRangeException(nameof(source), "Player Ids must be positive.");
        if (overrides.Any(item => item.GameId != source.GameId))
            throw new ArgumentException("A batting override belongs to a different Game.", nameof(overrides));
        if (overrides.GroupBy(item => item.PlayerId).Any(group => group.Count() > 1))
            throw new ArgumentException("A Player has duplicate batting overrides.", nameof(overrides));
        if (overrides.Any(item => !playerOrder.Contains(item.PlayerId)))
            throw new ArgumentException("A batting override Player did not participate in the Game.", nameof(overrides));

        var result = new Dictionary<int, ResolvedCounts>();
        foreach (var playerId in playerOrder)
        {
            var outcomes = source.Outcomes.Where(item => item.PlayerId == playerId).ToArray();
            var canonical = CountCanonical(playerId, outcomes);
            var overrideRow = overrides.SingleOrDefault(item => item.PlayerId == playerId);
            result[playerId] = overrideRow is null
                ? canonical
                : ApplyOverride(canonical, overrideRow);
        }
        return result;
    }

    private static ResolvedCounts CountCanonical(int playerId, IReadOnlyCollection<BattingOutcome> outcomes)
    {
        var values = new CompletedBattingOutcomeValues(
            outcomes.Count(item => item.Result == BatterResult.Single),
            outcomes.Count(item => item.Result == BatterResult.Double),
            outcomes.Count(item => item.Result == BatterResult.Triple),
            outcomes.Count(item => item.Result is BatterResult.HomeRun or BatterResult.InTheParkHomeRun),
            outcomes.Count(item => item.Result == BatterResult.Walk),
            outcomes.Count(item => item.Result == BatterResult.FielderChoice),
            outcomes.Count(item => item.Result == BatterResult.SacrificeFly),
            outcomes.Count(item => item.Result == BatterResult.Out));
        var counts = new BattingOutcomeCounts(
            playerId,
            values.Singles,
            values.Doubles,
            values.Triples,
            values.HomeRuns,
            values.Walks,
            values.Outs,
            values.FielderChoices,
            values.SacrificeFlies,
            outcomes.Count(item => item.Result == BatterResult.DoublePlay),
            outcomes.Count(item => item.Result == BatterResult.TriplePlay),
            outcomes.Count(item => item.Result == BatterResult.Strikeout),
            outcomes.Count(item => item.Result == BatterResult.DeadBallOut));
        return new(outcomes.Count, values, counts);
    }

    private static ResolvedCounts ApplyOverride(
        ResolvedCounts canonical,
        CompletedGameBattingOverride row)
    {
        var values = new CompletedBattingOutcomeValues(
            row.Singles, row.Doubles, row.Triples, row.HomeRuns, row.Walks,
            row.FielderChoices, row.SacrificeFlies, row.Outs);
        if (new[] { values.Singles, values.Doubles, values.Triples, values.HomeRuns,
                values.Walks, values.FielderChoices, values.SacrificeFlies,
                values.Outs }.Any(value => value < 0))
            throw new ArgumentOutOfRangeException(nameof(row), "Batting override counts cannot be negative.");

        return new(canonical.PlateAppearances, values, new BattingOutcomeCounts(
            row.PlayerId,
            values.Singles,
            values.Doubles,
            values.Triples,
            values.HomeRuns,
            values.Walks,
            values.Outs,
            values.FielderChoices,
            values.SacrificeFlies,
            canonical.Counts.DoublePlays,
            canonical.Counts.TriplePlays,
            canonical.Counts.Strikeouts,
            canonical.Counts.DeadBallOuts));
    }

    private static BattingOutcomeCounts Add(BattingOutcomeCounts left, BattingOutcomeCounts right) => new(
        left.PlayerId,
        checked(left.Singles + right.Singles),
        checked(left.Doubles + right.Doubles),
        checked(left.Triples + right.Triples),
        checked(left.HomeRuns + right.HomeRuns),
        checked(left.Walks + right.Walks),
        checked(left.Outs + right.Outs),
        checked(left.FielderChoices + right.FielderChoices),
        checked(left.SacrificeFlies + right.SacrificeFlies),
        checked(left.DoublePlays + right.DoublePlays),
        checked(left.TriplePlays + right.TriplePlays),
        checked(left.Strikeouts + right.Strikeouts),
        checked(left.DeadBallOuts + right.DeadBallOuts));

    private sealed record ResolvedCounts(
        int PlateAppearances,
        CompletedBattingOutcomeValues EditableValues,
        BattingOutcomeCounts Counts);
}
