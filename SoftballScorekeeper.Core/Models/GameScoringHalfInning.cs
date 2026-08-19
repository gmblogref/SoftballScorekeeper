using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringHalfInning
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringHalfInning_Session_Inning", 1, Unique = true)]
    public int GameScoringSessionId { get; set; }

    [Indexed("UX_GameScoringHalfInning_Session_Inning", 2, Unique = true)]
    public int Inning { get; set; }

    public int TeamRuns { get; set; }

    public int TeamHomeRuns { get; set; }

    public bool TeamSummaryAcknowledged { get; set; }

    public int? OpponentRuns { get; set; }

    public int? OpponentHomeRuns { get; set; }

    public bool OpponentEntryCompleted { get; set; }
}
