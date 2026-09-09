using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.ViewModels;

public sealed class TeamBeerTotalRowViewModel : ObservableObject
{
    private TeamBeerPlayerSummary summary;
    private BeerRuleType? editingRuleType;
    private bool isEditing;

    private string editValue = string.Empty;

    public TeamBeerTotalRowViewModel(TeamBeerPlayerSummary summary) =>
        this.summary = summary;

    public int PlayerId => summary.PlayerId;
    public string PlayerDisplayName => summary.PlayerDisplayName;
    public int DboCount => summary.DboCount;
    public int SoloHomeRunCount => summary.SoloHomeRunCount;
    public int DoublePlayCount => summary.DoublePlayCount;
    public int TriplePlayCount => summary.TriplePlayCount;
    public int TwoOutsInOneInningCount => summary.TwoOutsInOneInningCount;
    public int StrikeoutCount => summary.StrikeoutCount;
    public int LowManCount => summary.LowManCount;
    public long Owed => summary.Owed;
    public long Brought => summary.Brought;
    public string EditValue
    {
        get => editValue;
        set => SetProperty(ref editValue, value);
    }

    public bool IsDboEditing => IsEditingRule(BeerRuleType.Dbo);
    public bool IsSoloHomeRunEditing => IsEditingRule(BeerRuleType.SoloHomeRun);
    public bool IsDoublePlayEditing => IsEditingRule(BeerRuleType.DoublePlay);
    public bool IsTriplePlayEditing => IsEditingRule(BeerRuleType.TriplePlay);
    public bool IsTwoOutsEditing => IsEditingRule(BeerRuleType.TwoOutsInOneInning);
    public bool IsStrikeoutEditing => IsEditingRule(BeerRuleType.Strikeout);
    public bool IsLowManEditing => IsEditingRule(BeerRuleType.LowMan);
    public bool IsBroughtEditing => isEditing && editingRuleType is null;

    public int GetEffectiveRuleCount(BeerRuleType ruleType) =>
        summary.GetEffectiveRuleCount(ruleType);

    public void BeginEdit(BeerRuleType? ruleType)
    {
        editingRuleType = ruleType;
        isEditing = true;
        EditValue = (ruleType is null ? Brought : GetEffectiveRuleCount(ruleType.Value))
            .ToString();
        NotifyEditingStateChanged();
    }

    public void EndEdit()
    {
        isEditing = false;
        editingRuleType = null;
        NotifyEditingStateChanged();
    }

    public void Update(TeamBeerPlayerSummary updatedSummary)
    {
        summary = updatedSummary;
        OnPropertyChanged(nameof(PlayerDisplayName));
        OnPropertyChanged(nameof(DboCount));
        OnPropertyChanged(nameof(SoloHomeRunCount));
        OnPropertyChanged(nameof(DoublePlayCount));
        OnPropertyChanged(nameof(TriplePlayCount));
        OnPropertyChanged(nameof(TwoOutsInOneInningCount));
        OnPropertyChanged(nameof(StrikeoutCount));
        OnPropertyChanged(nameof(LowManCount));
        OnPropertyChanged(nameof(Brought));
        OnPropertyChanged(nameof(Owed));
    }

    private bool IsEditingRule(BeerRuleType ruleType) =>
        isEditing && editingRuleType == ruleType;

    private void NotifyEditingStateChanged()
    {
        OnPropertyChanged(nameof(IsDboEditing));
        OnPropertyChanged(nameof(IsSoloHomeRunEditing));
        OnPropertyChanged(nameof(IsDoublePlayEditing));
        OnPropertyChanged(nameof(IsTriplePlayEditing));
        OnPropertyChanged(nameof(IsTwoOutsEditing));
        OnPropertyChanged(nameof(IsStrikeoutEditing));
        OnPropertyChanged(nameof(IsLowManEditing));
        OnPropertyChanged(nameof(IsBroughtEditing));
    }
}
