using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record GameScoringState(
    GameScoringSession Session,
    IReadOnlyList<GameScoringLineupPlayer> BattingSequence,
    IReadOnlyDictionary<int, Player> PlayersById,
    IReadOnlyList<GamePlateAppearance> PlateAppearances);
