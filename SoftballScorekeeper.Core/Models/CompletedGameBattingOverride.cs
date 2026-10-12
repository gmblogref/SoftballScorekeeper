using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class CompletedGameBattingOverride
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_CompletedGameBattingOverride_Game_Player", 1, Unique = true)]
    public int GameId { get; set; }

    [Indexed("UX_CompletedGameBattingOverride_Game_Player", 2, Unique = true)]
    public int PlayerId { get; set; }

    public int Singles { get; set; }
    public int Doubles { get; set; }
    public int Triples { get; set; }
    public int HomeRuns { get; set; }
    public int Walks { get; set; }
    public int FielderChoices { get; set; }
    public int SacrificeFlies { get; set; }
    public int Outs { get; set; }
}
