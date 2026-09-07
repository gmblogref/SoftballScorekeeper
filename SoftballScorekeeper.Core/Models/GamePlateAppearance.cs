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

    public int Inning { get; set; }

    public BatterResult? Result { get; set; }
}

public enum BatterResult
{
    Single = 1,
    Double = 2,
    Triple = 3,
    HomeRun = 4,
    Out = 5,
    Walk = 6,
    FielderChoice = 7,
    DoublePlay = 8,
    TriplePlay = 9,
    SacrificeFly = 10,
    Strikeout = 11,
    DeadBallOut = 12,
}
