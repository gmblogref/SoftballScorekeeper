using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringHistory
{
    [PrimaryKey]
    public int GameScoringSessionId { get; set; }

    public int CurrentPosition { get; set; }

    public int LatestPosition { get; set; }

    public int? PendingStartPosition { get; set; }
}

public sealed class GameScoringHistorySnapshot
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringHistorySnapshot_Session_Position", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    [Indexed("UX_GameScoringHistorySnapshot_Session_Position", 2, Unique = true)]
    public int Position { get; set; }

    public string StateJson { get; set; } = string.Empty;
}
