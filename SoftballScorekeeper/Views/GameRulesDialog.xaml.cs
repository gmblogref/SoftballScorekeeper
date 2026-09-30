using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.Views;

public partial class GameRulesDialog : ContentPage
{
    private readonly TaskCompletionSource<GameRulesDialogResult?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<GameRulesDialogResult, Task<bool>>? saveAsync;
    private bool isClosing;
    private bool isSubmitting;

    public GameRulesDialog(bool isFirstStart, int homeRunLimit, bool isOneUpEnabled,
        Func<GameRulesDialogResult, Task<bool>>? saveAsync = null,
        TeamGameSide teamSide = TeamGameSide.Away,
        bool canChangeTeamSide = true)
    {
        InitializeComponent();
        this.saveAsync = saveAsync;
        TeamSideSection.IsVisible = true;
        TeamSideSection.IsEnabled = isFirstStart || canChangeTeamSide;
        AwayOption.IsChecked = teamSide == TeamGameSide.Away;
        HomeOption.IsChecked = teamSide == TeamGameSide.Home;
        TitleLabel.Text = isFirstStart
            ? AppResources.ScoringSetupTitle : AppResources.GameRulesTitle;
        SaveButton.Text = isFirstStart
            ? AppResources.StartGameButton : AppResources.SaveButton;
        HomeRunLimitEntry.Text = homeRunLimit.ToString();
        OneUpSwitch.IsToggled = isOneUpEnabled;
        UpdateOneUpStatus();
    }

    public async Task<GameRulesDialogResult?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!isSubmitting) _ = CloseAsync(null);
        return true;
    }

    private void OnOneUpToggled(object sender, ToggledEventArgs e) => UpdateOneUpStatus();

    private void OnDecreaseClicked(object sender, EventArgs e)
    {
        var value = ParseCurrentLimit();
        HomeRunLimitEntry.Text = Math.Max(0, value - 1).ToString();
        ValidationLabel.Text = string.Empty;
    }

    private void OnIncreaseClicked(object sender, EventArgs e)
    {
        var value = ParseCurrentLimit();
        if (value < int.MaxValue) HomeRunLimitEntry.Text = (value + 1).ToString();
        ValidationLabel.Text = string.Empty;
    }

    private int ParseCurrentLimit() =>
        int.TryParse(HomeRunLimitEntry.Text?.Trim(), out var value) && value >= 0
            ? value : 0;

    private void UpdateOneUpStatus() => OneUpStatusLabel.Text = OneUpSwitch.IsToggled
        ? AppResources.OnLabel : AppResources.OffLabel;

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        if (!isSubmitting) await CloseAsync(null);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (isClosing || isSubmitting) return;
        if (!int.TryParse(HomeRunLimitEntry.Text?.Trim(), out var limit)
            || limit < 0)
        {
            ValidationLabel.Text = AppResources.NonNegativeWholeNumberRequiredMessage;
            return;
        }
        var result = new GameRulesDialogResult(
            AwayOption.IsChecked ? TeamGameSide.Away : TeamGameSide.Home,
            limit, OneUpSwitch.IsToggled);
        isSubmitting = true;
        SaveButton.IsEnabled = false;
        ValidationLabel.Text = string.Empty;
        if (saveAsync is not null && !await saveAsync(result))
        {
            ValidationLabel.Text = AppResources.GameRulesSaveFailed;
            SaveButton.IsEnabled = true;
            isSubmitting = false;
            return;
        }
        await CloseAsync(result);
    }

    private async Task CloseAsync(GameRulesDialogResult? result)
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(result);
    }
}

public sealed record GameRulesDialogResult(
    TeamGameSide TeamSide, int HomeRunLimit, bool IsOneUpEnabled);
