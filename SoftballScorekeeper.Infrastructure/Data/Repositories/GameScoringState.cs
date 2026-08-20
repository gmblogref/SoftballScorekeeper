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
    int VisitorHomeRuns)
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
    PersistenceFailure,
}
