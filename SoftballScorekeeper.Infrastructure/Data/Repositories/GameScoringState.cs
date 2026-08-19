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
    IReadOnlyList<GameScoringHalfInning> HalfInnings);

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
