using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class SeasonBackupReminderCheckpoint
{
    [PrimaryKey]
    public string CheckpointId { get; set; } = string.Empty;

    public int SeasonId { get; set; }

    public SeasonBackupReminderCheckpointDisposition Disposition { get; set; }
}

public enum SeasonBackupReminderCheckpointDisposition
{
    HistoricalBaseline = 1,
    BackupSatisfied = 2,
    Dismissed = 3,
}
