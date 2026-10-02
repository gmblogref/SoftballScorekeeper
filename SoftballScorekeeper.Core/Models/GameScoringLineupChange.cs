using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringLineupChange
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int GameScoringSessionId { get; set; }

    public GameScoringLineupChangeType ChangeType { get; set; }

    public int PlayerId { get; set; }

    public int? OutgoingPlayerId { get; set; }

    public int BattingPosition { get; set; }

    public int AfterPlateAppearanceNumber { get; set; }

    public int Inning { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}

public enum GameScoringLineupChangeType
{
    Add = 1,
    Remove = 2,
    Replace = 3,
}
