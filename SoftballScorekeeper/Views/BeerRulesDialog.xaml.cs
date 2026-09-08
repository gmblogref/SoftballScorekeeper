using System.Collections.ObjectModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class BeerRulesDialog : ContentPage
{
    private readonly TaskCompletionSource<bool> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly Func<IReadOnlyList<BeerRuleSetting>, Task<bool>> saveAsync;
    private readonly ObservableCollection<BeerRuleSettingViewModel> rules;
    private bool isClosing;
    private bool isSubmitting;

    public BeerRulesDialog(
        bool isTeamDefaults,
        IReadOnlyList<BeerRuleSetting> settings,
        Func<IReadOnlyList<BeerRuleSetting>, Task<bool>> saveAsync
    )
    {
        InitializeComponent();
        this.saveAsync = saveAsync;
        TitleLabel.Text = isTeamDefaults
            ? AppResources.TeamBeerRulesTitle
            : AppResources.GameBeerRulesTitle;
        DescriptionLabel.Text = isTeamDefaults
            ? AppResources.TeamBeerRulesDescription
            : AppResources.GameBeerRulesDescription;
        rules = new(
            settings.Select(item => new BeerRuleSettingViewModel(item, FormatRule(item.RuleType)))
        );
        RulesCollection.ItemsSource = rules;
    }

    public async Task ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!isSubmitting)
            _ = CloseAsync();
        return true;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        if (!isSubmitting)
            await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (isClosing || isSubmitting)
            return;
        var settings = new List<BeerRuleSetting>(rules.Count);
        foreach (var rule in rules)
        {
            if (!int.TryParse(rule.AmountText?.Trim(), out var amount) || amount <= 0)
            {
                ValidationLabel.Text = AppResources.PositiveWholeNumberRequiredMessage;
                return;
            }
            settings.Add(new(rule.RuleType, rule.IsEnabled, amount));
        }

        isSubmitting = true;
        SaveButton.IsEnabled = false;
        ValidationLabel.Text = string.Empty;
        if (!await saveAsync(settings))
        {
            ValidationLabel.Text = AppResources.BeerRulesSaveFailed;
            SaveButton.IsEnabled = true;
            isSubmitting = false;
            return;
        }
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (isClosing)
            return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(true);
    }

    private static string FormatRule(BeerRuleType ruleType) =>
        ruleType switch
        {
            BeerRuleType.Dbo => AppResources.BeerRuleDboLabel,
            BeerRuleType.SoloHomeRun => AppResources.BeerRuleSoloHomeRunLabel,
            BeerRuleType.DoublePlay => AppResources.BeerRuleDoublePlayLabel,
            BeerRuleType.TriplePlay => AppResources.BeerRuleTriplePlayLabel,
            BeerRuleType.TwoOutsInOneInning => AppResources.BeerRuleTwoOutsLabel,
            BeerRuleType.Strikeout => AppResources.BeerRuleStrikeoutLabel,
            BeerRuleType.LowMan => AppResources.BeerRuleLowManLabel,
            _ => throw new ArgumentOutOfRangeException(nameof(ruleType)),
        };
}
