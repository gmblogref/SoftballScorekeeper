using SQLite;

namespace SoftballScorekeeper.Core.Models;

public sealed class GameScoringSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("UX_GameScoringSession_Game", 1, Unique = true)]
    public int GameId { get; set; }

    public GameScoringStatus Status { get; set; }

    public TeamGameSide TeamSide { get; set; }

    public int HomeScore { get; set; }

    public int VisitorScore { get; set; }

    public int CurrentBatterPosition { get; set; }
}

public enum GameScoringStatus
{
    InProgress = 1,
    Completed = 2,
}

public enum TeamGameSide
{
    Away = 1,
    Home = 2,
}
