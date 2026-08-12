using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameLineupPlayer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameLineupPlayer_Game_Player", 1, Unique = true)]
    public int GameId { get; set; }

    [Indexed("UX_GameLineupPlayer_Game_Player", 2, Unique = true)]
    public int PlayerId { get; set; }

    public int BattingOrder { get; set; }
}
