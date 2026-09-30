using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record GameScoringState(
    GameScoringSession Session,
    IReadOnlyList<GameScoringLineupPlayer> BattingSequence,
    IReadOnlyDictionary<int, Player> PlayersById,
    IReadOnlyList<GamePlateAppearance> PlateAppearances,
    IReadOnlyList<GameScoringBaseRunner> BaseRunners,
    IReadOnlyList<GamePlateAppearanceRunnerMovement> RunnerMovements,
    PendingScoringPlay? PendingPlay,
    IReadOnlyList<GameScoringHalfInning> HalfInnings,
    int HomeHomeRuns,
    int VisitorHomeRuns,
    PlayResolutionResult? PlayResolution)
{
    public (int OnDeckPlayerId, int InTheHolePlayerId)? GetUpcomingPlayerIds()
    {
        var sequence = BattingSequence.OrderBy(item => item.BattingPosition).ToArray();
        var currentIndex = Array.FindIndex(sequence,
            item => item.BattingPosition == Session.CurrentBatterPosition);
        if (currentIndex < 0 || sequence.Length == 0) return null;
        return (sequence[(currentIndex + 1) % sequence.Length].PlayerId,
            sequence[(currentIndex + 2) % sequence.Length].PlayerId);
    }

    public IReadOnlyList<int> GetCurrentHalfInningOutPlayerIds()
    {
        var currentAppearances = PlateAppearances
            .Where(item => item.Inning == Session.CurrentInning)
            .OrderBy(item => item.PlateAppearanceNumber)
            .ToArray();
        var outMovements = RunnerMovements
            .Where(item => item.Destination == RunnerDestination.Out)
            .GroupBy(item => item.GamePlateAppearanceId)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(item => item.OutOrder ?? int.MaxValue)
                .ThenBy(item => item.Id).ToArray());
        var playerIds = new List<int>();

        foreach (var appearance in currentAppearances)
        {
            var hasImplicitBatterOut = appearance.Result is not null
                && BatterRetirementEvaluator.AddsImplicitBatterOut(
                    appearance.Result.Value);
            if (hasImplicitBatterOut)
                playerIds.Add(appearance.PlayerId);

            if (!outMovements.TryGetValue(appearance.Id, out var movements)) continue;
            playerIds.AddRange(movements
                .Where(item => !hasImplicitBatterOut
                    || item.PlayerId != appearance.PlayerId)
                .Select(item => item.PlayerId));
        }

        return playerIds;
    }

    public bool IsPendingBatterResolved()
    {
        if (PendingPlay is null) return false;
        var definition = PlateAppearanceResultDefinitions.Get(PendingPlay.Play.Result);
        if (!definition.IncludesBatterMovement) return true;
        return PendingPlay.Movements.Any(item =>
            item.PlayerId == PendingPlay.Play.BatterPlayerId
            && !item.RequiresOutcomeConfirmation);
    }

    public bool IsHomeRunLegal()
    {
        var battingTeamHomeRuns = Session.TeamSide == TeamGameSide.Home
            ? HomeHomeRuns : VisitorHomeRuns;
        var opponentHomeRuns = Session.TeamSide == TeamGameSide.Home
            ? VisitorHomeRuns : HomeHomeRuns;
        return HomeRunAvailabilityEvaluator.IsHomeRunLegal(
            Session.HomeRunLimit,
            Session.IsOneUpEnabled,
            battingTeamHomeRuns,
            opponentHomeRuns);
    }
}

public sealed record UpdateGameRulesResult(
    GameScoringSession? Session,
    GameScoringLifecycleFailure FailureReason)
{
    public bool IsSuccess => FailureReason == GameScoringLifecycleFailure.None;
}

public sealed record GameOverResult(
    GameScoringSession? Session,
    GameScoringLifecycleFailure FailureReason)
{
    public bool IsSuccess => FailureReason == GameScoringLifecycleFailure.None;
}

public sealed record ResetScoringResult(GameScoringLifecycleFailure FailureReason)
{
    public bool IsSuccess => FailureReason == GameScoringLifecycleFailure.None;
}

public enum GameScoringLifecycleFailure
{
    None,
    InvalidRequest,
    SessionNotFound,
    GameCompleted,
    UnresolvedScoringState,
    ScoringHistoryExists,
    TeamYearFinalized,
    Persistence,
}

public sealed record PendingScoringPlay(
    GameScoringPendingPlay Play,
    IReadOnlyList<GameScoringPendingRunnerMovement> Movements);

public sealed record OpponentHalfInningEntryResult(
    GameScoringHalfInning? HalfInning,
    bool IsSuccess,
    OpponentHalfInningEntryFailure FailureReason = OpponentHalfInningEntryFailure.None);

public enum OpponentEntryMode
{
    Total,
    ThisHalf,
}

public enum OpponentHalfInningEntryFailure
{
    None,
    InvalidValue,
    TotalRunsBelowCurrent,
    TotalHomeRunsBelowCurrent,
    InvalidScoringState,
    TeamYearFinalized,
    PersistenceFailure,
}
