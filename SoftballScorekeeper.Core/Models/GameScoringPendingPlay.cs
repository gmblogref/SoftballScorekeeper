using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringPendingPlay
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringPendingPlay_Session", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    public int BatterPlayerId { get; set; }

    public int BatterPosition { get; set; }

    public BatterResult Result { get; set; }
}

public sealed class GameScoringPendingRunnerMovement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringPendingMovement_Play_Player", 1, Unique = true)]
    public int GameScoringPendingPlayId { get; set; }

    [Indexed("UX_GameScoringPendingMovement_Play_Player", 2, Unique = true)]
    public int PlayerId { get; set; }

    public RunnerStartingLocation StartingLocation { get; set; }

    public RunnerDestination Destination { get; set; }

    public RunnerDestination? SafeDestination { get; set; }

    public RunnerDestination? AttemptedDestination { get; set; }

    public bool RequiresOutcomeConfirmation { get; set; }
}
