using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ReorderLiveLineupDialog : ContentPage
{
    private readonly ReorderLiveLineupDialogState state;
    private readonly Func<IReadOnlyList<int>, Task<bool>> saveOrder;
    private readonly TaskCompletionSource<bool> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private LiveLineupReorderItem? draggedItem;
    private LiveLineupReorderItem? dropTargetItem;
    private bool isClosing;
    private bool isCompletingDrop;
    private bool isDragInProgress;

    public ReorderLiveLineupDialog(
        IReadOnlyList<LiveLineupPlayerOption> players,
        Func<IReadOnlyList<int>, Task<bool>> saveOrder
    )
    {
        InitializeComponent();
        this.saveOrder = saveOrder;
        var currentPosition = players.FirstOrDefault(item => item.IsCurrentBatter)?.BattingPosition;
        state = new ReorderLiveLineupDialogState(
            players
                .OrderBy(item => item.BattingPosition)
                .Select(item => new LiveLineupReorderItem(
                    item.PlayerId,
                    item.DisplayName,
                    item.BattingPosition
                )),
            currentPosition
        );
        BindingContext = state;
    }

    public async Task<bool> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }

    protected override void OnDisappearing()
    {
        ResetDragPresentationState();
        if (!isClosing)
            completion.TrySetResult(false);
        base.OnDisappearing();
    }

    private void OnDragStarting(object sender, DragStartingEventArgs e)
    {
        if (state.IsBusy || isCompletingDrop)
        {
            e.Cancel = true;
            return;
        }

        ResetDragPresentationState();
        if (ResolveItem(sender) is not { } item)
        {
            e.Cancel = true;
            return;
        }

        draggedItem = item;
        isDragInProgress = true;
        item.IsDragged = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (
            !isDragInProgress
            || isCompletingDrop
            || draggedItem is null
            || ResolveItem(sender) is not { } target
            || ReferenceEquals(draggedItem, target)
        )
        {
            ClearDropTarget();
            return;
        }

        if (ReferenceEquals(dropTargetItem, target))
            return;

        ClearDropTarget();
        dropTargetItem = target;
        var sourceIndex = state.Items.IndexOf(draggedItem);
        var targetIndex = state.Items.IndexOf(target);
        target.IsDropGapBefore = sourceIndex > targetIndex;
        target.IsDropGapAfter = sourceIndex < targetIndex;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (ResolveItem(sender) is { } target && ReferenceEquals(dropTargetItem, target))
            ClearDropTarget();
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        if (draggedItem is null || ResolveItem(sender) is not { } target)
        {
            ResetDragPresentationState();
            return;
        }

        isCompletingDrop = true;
        isDragInProgress = false;
        var source = draggedItem;
        var sourceIndex = state.Items.IndexOf(source);
        var targetIndex = state.Items.IndexOf(target);
        ResetDragPresentationState(preserveCompletionGuard: true);
        if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
        {
            state.Items.RemoveAt(sourceIndex);
            state.Items.Insert(targetIndex, source);
            state.RefreshPositions();
        }
        ResetDragPresentationState();
    }

    private void OnDropCompleted(object sender, DropCompletedEventArgs e) =>
        ResetDragPresentationState(preserveCompletionGuard: isCompletingDrop);

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (state.IsBusy)
            return;

        state.IsBusy = true;
        state.ValidationMessage = string.Empty;
        try
        {
            var succeeded = await saveOrder(state.Items.Select(item => item.PlayerId).ToArray());
            if (succeeded)
                await CloseAsync(true);
            else
                state.ValidationMessage = AppResources.LiveLineupChangeFailedMessage;
        }
        catch
        {
            state.ValidationMessage = AppResources.LiveLineupChangeFailedMessage;
        }
        finally
        {
            state.IsBusy = false;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(false);

    private async Task CloseAsync(bool saved)
    {
        if (isClosing || state.IsBusy && !saved)
            return;

        isClosing = true;
        ResetDragPresentationState();
        await Navigation.PopModalAsync();
        completion.TrySetResult(saved);
    }

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
        foreach (var item in state.Items)
        {
            item.IsDragged = false;
            item.IsDropGapBefore = false;
            item.IsDropGapAfter = false;
        }
        draggedItem = null;
        dropTargetItem = null;
        isDragInProgress = false;
        if (!preserveCompletionGuard)
            isCompletingDrop = false;
    }

    private static LiveLineupReorderItem? ResolveItem(object sender) =>
        (sender as BindableObject)?.BindingContext as LiveLineupReorderItem;
}

public partial class ReorderLiveLineupDialogState : ObservableObject
{
    private readonly int? currentPosition;
    private bool isBusy;
    private string validationMessage = string.Empty;

    public ReorderLiveLineupDialogState(
        IEnumerable<LiveLineupReorderItem> items,
        int? currentPosition
    )
    {
        this.currentPosition = currentPosition;
        Items = new ObservableCollection<LiveLineupReorderItem>(items);
        RefreshPositions();
    }

    public ObservableCollection<LiveLineupReorderItem> Items { get; }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string ValidationMessage
    {
        get => validationMessage;
        set => SetProperty(ref validationMessage, value);
    }

    public void RefreshPositions()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].BattingPosition = index + 1;
            Items[index].IsCurrentSlot = currentPosition == index + 1;
        }
    }
}
