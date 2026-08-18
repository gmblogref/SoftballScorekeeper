using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringBaseRunner
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringBaseRunner_Session_Base", 1, Unique = true)]
    [Indexed("UX_GameScoringBaseRunner_Session_Player", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    [Indexed("UX_GameScoringBaseRunner_Session_Player", 2, Unique = true)]
    public int PlayerId { get; set; }

    [Indexed("UX_GameScoringBaseRunner_Session_Base", 2, Unique = true)]
    public ScoringBase Base { get; set; }
}

public enum ScoringBase
{
    First = 1,
    Second = 2,
    Third = 3,
}

public enum RunnerStartingLocation
{
    Home = 0,
    First = 1,
    Second = 2,
    Third = 3,
}

public enum RunnerDestination
{
    First = 1,
    Second = 2,
    Third = 3,
    Scores = 4,
}
