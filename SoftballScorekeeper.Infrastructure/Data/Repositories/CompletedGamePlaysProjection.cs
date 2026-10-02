using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public enum CompletedGameHalf
{
    Top,
    Bottom,
}

public sealed record CompletedGamePlaysProjection(
    string TeamName,
    string OpponentName,
    TeamGameSide TeamSide,
    IReadOnlyList<CompletedGameHalfInningGroup> HalfInnings
);

public sealed record CompletedGameHalfInningGroup(
    int Inning,
    CompletedGameHalf Half,
    bool IsTeamOffense,
    IReadOnlyList<CompletedGamePlayRow> Plays,
    CompletedOpponentHalfSummary? OpponentSummary,
    IReadOnlyList<CompletedGameTimelineRow>? Timeline = null
);

public sealed record CompletedGameTimelineRow(
    CompletedGamePlayRow? Play,
    CompletedLineupChangeRow? LineupChange
);

public sealed record CompletedLineupChangeRow(
    GameScoringLineupChangeType ChangeType,
    string PlayerName,
    string? OutgoingPlayerName,
    int BattingPosition
);

public sealed record CompletedGamePlayRow(
    int PlateAppearanceNumber,
    string BatterName,
    BatterResult? Result,
    IReadOnlyList<CompletedRunnerMovementRow> RunnerMovements,
    int TeamScore,
    int OpponentScore
);

public sealed record CompletedRunnerMovementRow(
    string PlayerName,
    RunnerStartingLocation StartingLocation,
    RunnerDestination Destination,
    RunnerDestination? SafeDestination,
    RunnerDestination? AttemptedDestination,
    int? OutOrder,
    bool? RunCounts
);

public sealed record CompletedOpponentHalfSummary(
    int? Runs,
    int? HomeRuns,
    int TotalHomeRuns,
    int TeamScore,
    int OpponentScore
);
