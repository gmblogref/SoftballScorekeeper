using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class Game
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public int TeamId { get; set; }

    public int SeasonId { get; set; }

    public DateTime ScheduledDate { get; set; }

    public TimeSpan ScheduledTime { get; set; }

    public string? Opponent { get; set; }

    public string? Field { get; set; }
}
