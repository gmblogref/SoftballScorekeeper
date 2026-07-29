using SQLite;

namespace SoftballScorekeeper.Models;

public sealed class Team
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
