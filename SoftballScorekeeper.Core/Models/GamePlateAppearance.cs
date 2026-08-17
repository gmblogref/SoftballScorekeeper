using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GamePlateAppearance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GamePlateAppearance_Session_Number", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    public int PlayerId { get; set; }

    public int BattingPosition { get; set; }

    [Indexed("UX_GamePlateAppearance_Session_Number", 2, Unique = true)]
    public int PlateAppearanceNumber { get; set; }
}
