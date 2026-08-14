using System.Globalization;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class SetLineupPage : ContentPage
{
    private readonly SetLineupViewModel viewModel;
    private int? draggedPlayerId;
    private LineupPlayerSelectionItem? draggedItem;
    private LineupPlayerSelectionItem? dropTargetItem;
    private bool isDragInProgress;
    private bool isCompletingDrop;
    public SetLineupPage(SetLineupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        viewModel.RemovalConfirmationRequested = ConfirmRemovalAsync;
        viewModel.PreviousLineupReplacementConfirmationRequested = ConfirmPreviousLineupReplacementAsync;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
        ResetDragPresentationState();
    }

    protected override void OnDisappearing()
    {
        ResetDragPresentationState();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => viewModel.TryExitTemporaryMode() || base.OnBackButtonPressed();

    private void OnLineupPlayerDragStarting(object sender, DragStartingEventArgs e)
    {
        if (isCompletingDrop)
        {
            e.Cancel = true;
            return;
        }

        ResetDragPresentationState();
        if (!viewModel.CanReorderLineup
            || sender is not DragGestureRecognizer
            {
                BindingContext: LineupPlayerSelectionItem item,
            })
        {
            draggedPlayerId = null;
            e.Cancel = true;
            return;
        }

        draggedPlayerId = item.Player.Id;
        draggedItem = item;
        isDragInProgress = true;
        item.IsDragged = true;
    }

    private void OnLineupPlayerDragOver(object sender, DragEventArgs e)
    {
        if (!isDragInProgress
            || isCompletingDrop
            || draggedItem is null
            || sender is not DropGestureRecognizer
            {
                BindingContext: LineupPlayerSelectionItem target,
            }
            || ReferenceEquals(draggedItem, target))
        {
            ClearDropTarget();
            return;
        }

        if (ReferenceEquals(dropTargetItem, target)) return;

        ClearDropTarget();
        dropTargetItem = target;
        var sourceIndex = viewModel.ExistingLineupPlayers.IndexOf(draggedItem);
        var targetIndex = viewModel.ExistingLineupPlayers.IndexOf(target);
        target.IsDropGapBefore = sourceIndex > targetIndex;
        target.IsDropGapAfter = sourceIndex < targetIndex;
    }

    private void OnLineupPlayerDragLeave(object sender, DragEventArgs e)
    {
        if (sender is DropGestureRecognizer
            {
                BindingContext: LineupPlayerSelectionItem target,
            }
            && ReferenceEquals(dropTargetItem, target))
        {
            ClearDropTarget();
        }
    }

    private async void OnLineupPlayerDrop(object sender, DropEventArgs e)
    {
        if (draggedPlayerId is not int sourcePlayerId
            || sender is not DropGestureRecognizer
            {
                BindingContext: LineupPlayerSelectionItem target,
            })
        {
            ResetDragPresentationState();
            return;
        }

        isCompletingDrop = true;
        isDragInProgress = false;
        ResetDragPresentationState(preserveCompletionGuard: true);
        try
        {
            await viewModel.ReorderPlayerAsync(sourcePlayerId, target.Player.Id);
        }
        finally
        {
            ResetDragPresentationState();
        }
    }

    private void OnLineupPlayerDropCompleted(object sender, DropCompletedEventArgs e) =>
        ResetDragPresentationState(preserveCompletionGuard: isCompletingDrop);

    private void ClearDropTarget()
    {
        if (dropTargetItem is not null)
        {
            dropTargetItem.IsDropGapBefore = false;
            dropTargetItem.IsDropGapAfter = false;
        }
        dropTargetItem = null;
    }

    private void ResetDragPresentationState(bool preserveCompletionGuard = false)
    {
        foreach (var item in viewModel.ExistingLineupPlayers) ResetDragVisualState(item);
        if (draggedItem is not null) ResetDragVisualState(draggedItem);
        if (dropTargetItem is not null) ResetDragVisualState(dropTargetItem);
        draggedItem = null;
        draggedPlayerId = null;
        dropTargetItem = null;
        isDragInProgress = false;
        if (!preserveCompletionGuard) isCompletingDrop = false;
    }

    private static void ResetDragVisualState(LineupPlayerSelectionItem item)
    {
        item.IsDragged = false;
        item.IsDropGapBefore = false;
        item.IsDropGapAfter = false;
    }

    private async Task ConfirmRemovalAsync(int selectedCount)
    {
        var template = selectedCount == 1
            ? AppResources.LineupRemovalConfirmationSingular
            : AppResources.LineupRemovalConfirmationPlural;
        var confirmed = await DisplayAlert(
            AppResources.LineupRemovalConfirmationTitle,
            string.Format(CultureInfo.CurrentCulture, template, selectedCount),
            AppResources.ConfirmRemoveLineupPlayersButton,
            AppResources.CancelButton);
        if (confirmed && viewModel.ConfirmRemovalCommand.CanExecute(null))
            await viewModel.ConfirmRemovalCommand.ExecuteAsync(null);
    }

    private Task<bool> ConfirmPreviousLineupReplacementAsync() => DisplayAlert(
        AppResources.PreviousLineupReplacementTitle,
        AppResources.PreviousLineupReplacementMessage,
        AppResources.ConfirmPreviousLineupReplacementButton,
        AppResources.CancelButton);
}
