using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class Team
{
    public const int LegacyDefaultYear = 2026;

    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Year { get; set; } = LegacyDefaultYear;

    public Guid TeamLineageId { get; set; }

    public int? PreviousTeamId { get; set; }

    public bool IsYearOver { get; set; }

    public bool IsActive { get; set; } = true;
}
