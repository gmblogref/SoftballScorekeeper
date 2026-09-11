namespace SoftballScorekeeper.Core.Models;

public sealed record TeamYearRecord(
    int? Year,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Ties
);
