using CommunityToolkit.Mvvm.ComponentModel;

namespace SoftballScorekeeper.ViewModels;

public sealed class GameScheduleItem : ObservableObject
{
    private readonly Action<GameScheduleItem>? selectionChanged;
    private bool isSelectionVisible;
    private bool isSelected;

    public GameScheduleItem(Action<GameScheduleItem>? selectionChanged = null)
    {
        this.selectionChanged = selectionChanged;
    }

    public required int GameId { get; init; }

    public required DateTime ScheduledDate { get; init; }

    public required string NextDisplay { get; init; }

    public required string DateDisplay { get; init; }

    public required string OpponentDisplay { get; init; }

    public required string TimeDisplay { get; init; }

    public required string FieldDisplay { get; init; }

    public required string SeasonDisplay { get; init; }

    public required bool IsNextPlayingDate { get; init; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
            {
                selectionChanged?.Invoke(this);
            }
        }
    }

    public bool IsSelectionVisible
    {
        get => isSelectionVisible;
        set => SetProperty(ref isSelectionVisible, value);
    }
}
