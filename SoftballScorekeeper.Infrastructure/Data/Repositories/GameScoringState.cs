using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record GameScoringState(
    GameScoringSession Session,
    IReadOnlyList<GameScoringLineupPlayer> BattingSequence,
    IReadOnlyDictionary<int, Player> PlayersById,
    IReadOnlyList<GamePlateAppearance> PlateAppearances,
    IReadOnlyList<GameScoringBaseRunner> BaseRunners,
    IReadOnlyList<GamePlateAppearanceRunnerMovement> RunnerMovements,
    PendingScoringPlay? PendingPlay);

public sealed record PendingScoringPlay(
    GameScoringPendingPlay Play,
    IReadOnlyList<GameScoringPendingRunnerMovement> Movements);
