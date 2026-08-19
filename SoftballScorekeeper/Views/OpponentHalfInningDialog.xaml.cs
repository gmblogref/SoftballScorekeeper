using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Views;

public partial class OpponentHalfInningDialog : ContentPage
{
    private readonly TaskCompletionSource<bool> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<OpponentHalfInningDialogResult, Task<string?>> submitAsync;
    private bool isClosing;
    private bool isSubmitting;

    public OpponentHalfInningDialog(string title,
        Func<OpponentHalfInningDialogResult, Task<string?>> submitAsync)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        this.submitAsync = submitAsync;
    }

    public async Task ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = TrySubmitAsync(new(null, OpponentEntryMode.Total,
            null, OpponentEntryMode.Total));
        return true;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (!TryParseOptional(RunsEntry.Text, out var runs)
            || !TryParseOptional(HomeRunsEntry.Text, out var homeRuns))
        {
            ValidationLabel.Text = AppResources.NonNegativeWholeNumberMessage;
            return;
        }

        await TrySubmitAsync(new(
            runs,
            RunsTotalMode.IsChecked ? OpponentEntryMode.Total : OpponentEntryMode.ThisHalf,
            homeRuns,
            HomeRunsTotalMode.IsChecked ? OpponentEntryMode.Total : OpponentEntryMode.ThisHalf));
    }

    private async Task TrySubmitAsync(OpponentHalfInningDialogResult result)
    {
        if (isClosing || isSubmitting) return;
        isSubmitting = true;
        SubmitButton.IsEnabled = false;
        ValidationLabel.Text = string.Empty;
        var validationMessage = await submitAsync(result);
        if (!string.IsNullOrEmpty(validationMessage))
        {
            ValidationLabel.Text = validationMessage;
            SubmitButton.IsEnabled = true;
            isSubmitting = false;
            return;
        }
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(true);
    }

    private static bool TryParseOptional(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), out var parsed) || parsed < 0) return false;
        value = parsed;
        return true;
    }
}

public sealed record OpponentHalfInningDialogResult(
    int? Runs,
    OpponentEntryMode RunsMode,
    int? HomeRuns,
    OpponentEntryMode HomeRunsMode);
