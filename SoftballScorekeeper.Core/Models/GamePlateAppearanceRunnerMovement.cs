using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GamePlateAppearanceRunnerMovement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GamePlateAppearanceRunnerMovement_Appearance_Player", 1, Unique = true)]
    public int GamePlateAppearanceId { get; set; }

    [Indexed("UX_GamePlateAppearanceRunnerMovement_Appearance_Player", 2, Unique = true)]
    public int PlayerId { get; set; }

    public RunnerStartingLocation StartingLocation { get; set; }

    public RunnerDestination Destination { get; set; }
}
