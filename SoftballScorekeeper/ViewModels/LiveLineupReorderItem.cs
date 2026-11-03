using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class LiveLineupReorderItem : ObservableObject
{
    private bool isCurrentSlot;
    private bool isDragged;
    private bool isDropGapAfter;
    private bool isDropGapBefore;
    private int battingPosition;

    public LiveLineupReorderItem(int playerId, string displayName, int battingPosition)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        this.battingPosition = battingPosition;
    }

    public int PlayerId { get; }

    public string DisplayName { get; }

    public int BattingPosition
    {
        get => battingPosition;
        set
        {
            if (SetProperty(ref battingPosition, value))
            {
                OnPropertyChanged(nameof(PositionAndNameDisplay));
                OnPropertyChanged(nameof(RowAutomationId));
            }
        }
    }

    public bool IsCurrentSlot
    {
        get => isCurrentSlot;
        set => SetProperty(ref isCurrentSlot, value);
    }

    public bool IsDragged
    {
        get => isDragged;
        set => SetProperty(ref isDragged, value);
    }

    public bool IsDropGapBefore
    {
        get => isDropGapBefore;
        set => SetProperty(ref isDropGapBefore, value);
    }

    public bool IsDropGapAfter
    {
        get => isDropGapAfter;
        set => SetProperty(ref isDropGapAfter, value);
    }

    public string PositionAndNameDisplay =>
        string.Format(AppResources.LiveLineupPlayerPositionFormat, BattingPosition, DisplayName);

    public string RowAutomationId => $"ReorderLiveLineupRow-{BattingPosition}";
}
