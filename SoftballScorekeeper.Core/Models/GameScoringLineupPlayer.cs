using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringLineupPlayer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringLineupPlayer_Session_Position", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    public int PlayerId { get; set; }

    [Indexed("UX_GameScoringLineupPlayer_Session_Position", 2, Unique = true)]
    public int BattingPosition { get; set; }
}
