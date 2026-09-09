using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views.Controls;

public partial class BeerTotalsGrid : ContentView
{
    private TeamBeerTotalRowViewModel? activeRow;
    private BeerRuleType? activeRuleType;
    private Entry? activeEntry;
    private bool isSaving;

    public BeerTotalsGrid() => InitializeComponent();

    public Func<TeamBeerTotalRowViewModel, BeerRuleType?, string?, Task<string?>>?
        SaveRequested { get; set; }

    private async void OnRuleClicked(object sender, EventArgs e)
    {
        if (BindingContext is TeamBeerTotalsViewModel { CanEdit: true }
            && sender is Button button
            && button.BindingContext is TeamBeerTotalRowViewModel row
            && button.CommandParameter is BeerRuleType ruleType)
            await BeginEditAsync(row, ruleType, (Grid)button.Parent);
    }

    private async void OnBroughtClicked(object sender, EventArgs e)
    {
        if (BindingContext is TeamBeerTotalsViewModel { CanEdit: true }
            && sender is Button button
            && button.BindingContext is TeamBeerTotalRowViewModel row)
            await BeginEditAsync(row, null, (Grid)button.Parent);
    }

    private async void OnEditCompleted(object sender, EventArgs e)
    {
        if (sender == activeEntry)
            await SaveActiveEditAsync();
    }

    private async Task BeginEditAsync(
        TeamBeerTotalRowViewModel row,
        BeerRuleType? ruleType,
        Grid cell)
    {
        if (isSaving)
            return;

        if (activeRow is not null && !await SaveActiveEditAsync())
            return;

        activeRow = row;
        activeRuleType = ruleType;
        activeEntry = cell.Children.OfType<Entry>().Single();
        row.BeginEdit(ruleType);

        await Task.Yield();
        AttachAndroidCompletionHandler(activeEntry);
        activeEntry.Focus();
        activeEntry.CursorPosition = 0;
        activeEntry.SelectionLength = activeEntry.Text?.Length ?? 0;
    }

    private async Task<bool> SaveActiveEditAsync()
    {
        if (isSaving || activeRow is null || activeEntry is null || SaveRequested is null)
            return false;

        isSaving = true;
        try
        {
            var failureMessage = await SaveRequested(
                activeRow, activeRuleType, activeEntry.Text);
            if (failureMessage is not null)
            {
                activeEntry.Focus();
                activeEntry.CursorPosition = 0;
                activeEntry.SelectionLength = activeEntry.Text?.Length ?? 0;
                return false;
            }

            var completedEntry = activeEntry;
            completedEntry.Unfocus();
            DetachAndroidCompletionHandler(completedEntry);
            activeRow.EndEdit();
            activeRow = null;
            activeRuleType = null;
            activeEntry = null;
            return true;
        }
        finally
        {
            isSaving = false;
        }
    }

    private void AttachAndroidCompletionHandler(Entry entry)
    {
#if ANDROID
        if (entry.Handler?.PlatformView is Android.Widget.TextView nativeEntry)
            nativeEntry.EditorAction += OnAndroidEditorAction;
#endif
    }

    private void DetachAndroidCompletionHandler(Entry entry)
    {
#if ANDROID
        if (entry.Handler?.PlatformView is Android.Widget.TextView nativeEntry)
            nativeEntry.EditorAction -= OnAndroidEditorAction;
#endif
    }

#if ANDROID
    private void OnAndroidEditorAction(
        object? sender,
        Android.Widget.TextView.EditorActionEventArgs e)
    {
        var isDone = e.ActionId == Android.Views.InputMethods.ImeAction.Done;
        var isEnterKey = e.Event is
        {
            Action: Android.Views.KeyEventActions.Up,
            KeyCode: Android.Views.Keycode.Enter,
        };
        if (!isDone && !isEnterKey)
            return;

        e.Handled = true;
        _ = SaveActiveEditAsync();
    }
#endif
}
