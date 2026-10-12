using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class CompletedGameScoreOverride
{
    [PrimaryKey]
    public int GameId { get; set; }

    public int VisitorScore { get; set; }
    public int HomeScore { get; set; }
}
