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
    IReadOnlyList<GameBattingPlayerProjection> Players,
    bool HasManualOverrides = false);

public sealed record GameBattingStatisticsProjection(
    int GameId,
    int ScoringSessionId,
    IReadOnlyList<GameBattingPlayerProjection> Players);

public sealed record GameBattingPlayerProjection(
    int PlayerId,
    string DisplayName,
    int BattingPosition,
    PlayerBattingStatLine BattingStatistics,
    int OrdinaryOuts = 0);
