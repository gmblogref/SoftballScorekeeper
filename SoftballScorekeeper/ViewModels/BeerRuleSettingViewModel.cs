using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.ViewModels;

public sealed class BeerRuleSettingViewModel : ObservableObject
{
    private bool isEnabled;
    private string amountText;

    public BeerRuleSettingViewModel(BeerRuleSetting setting, string displayName)
    {
        RuleType = setting.RuleType;
        DisplayName = displayName;
        isEnabled = setting.IsEnabled;
        amountText = setting.Amount.ToString();
    }

    public BeerRuleType RuleType { get; }

    public string DisplayName { get; }

    public string ToggleAutomationId => $"BeerRule{RuleType}Toggle";

    public string AmountAutomationId => $"BeerRule{RuleType}Amount";

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public string AmountText
    {
        get => amountText;
        set => SetProperty(ref amountText, value);
    }
}
