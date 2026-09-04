using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class TeamPlayer
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public int TeamId { get; set; }

    public int PlayerId { get; set; }

    public int? JerseyNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
