using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class Player
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }

    public bool IsActive { get; set; } = true;
}
