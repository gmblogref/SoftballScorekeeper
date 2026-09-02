using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed record CompletedGameProjection(
    int GameId,
    int ScoringSessionId,
    GameScoringStatus Status,
    DateTime? CompletedAtUtc,
    TeamGameSide TeamSide,
    string VisitorName,
    int VisitorScore,
    string HomeName,
    int HomeScore,
    IReadOnlyList<CompletedGamePlayerProjection> Players);

public sealed record CompletedGamePlayerProjection(
    int PlayerId,
    string DisplayName,
    int BattingPosition,
    PlayerBattingStatLine BattingStatistics);
