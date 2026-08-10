using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class Season
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public int TeamId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }
}
