namespace SoftballScorekeeper.Core.Models;

public sealed record TeamYearSummaryEligibility(bool HasSuccessor, bool IsYearOver)
{
    public bool CanViewSummary => HasSuccessor || IsYearOver;

    public bool CanMarkYearOver => !HasSuccessor && !IsYearOver;
}
