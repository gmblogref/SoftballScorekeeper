using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.ViewModels;

public sealed class TeamYearBeerRowViewModel(TeamBeerPlayerSummary summary)
{
    public int PlayerId => summary.PlayerId;
    public string PlayerDisplayName => summary.PlayerDisplayName;
    public int DboCount => summary.DboCount;
    public int SoloHomeRunCount => summary.SoloHomeRunCount;
    public int DoublePlayCount => summary.DoublePlayCount;
    public int TriplePlayCount => summary.TriplePlayCount;
    public int TwoOutsInOneInningCount => summary.TwoOutsInOneInningCount;
    public int StrikeoutCount => summary.StrikeoutCount;
    public int LowManCount => summary.LowManCount;
    public long Brought => summary.Brought;
    public long Owed => summary.Owed;
}
