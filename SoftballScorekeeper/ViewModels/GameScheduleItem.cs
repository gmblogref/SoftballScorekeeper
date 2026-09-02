using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.ViewModels;

public sealed class GameScheduleItem : ObservableObject
{
    private readonly Action<GameScheduleItem>? selectionChanged;
    private bool isSelectionVisible;
    private bool isSelected;

    public GameScheduleItem(
        Action<GameScheduleItem>? selectionChanged = null,
        Func<GameScheduleItem, Task>? openRequested = null)
    {
        this.selectionChanged = selectionChanged;
        OpenCommand = new AsyncRelayCommand(
            () => openRequested is null ? Task.CompletedTask : openRequested(this));
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

    public required bool HasLineup { get; init; }

    public required string LineupStatusText { get; init; }

    public required string LineupStatusSemanticDescription { get; init; }

    public required GameScheduleStatus Status { get; init; }

    public required string StatusText { get; init; }

    public required TeamGameResult? Result { get; init; }

    public required string ResultText { get; init; }

    public required string FinalScoreDisplay { get; init; }

    public bool IsUpcoming => Status == GameScheduleStatus.Upcoming;

    public bool IsInProgress => Status == GameScheduleStatus.InProgress;

    public bool IsFinal => Status == GameScheduleStatus.Final;

    public bool IsWin => Result == TeamGameResult.Win;

    public bool IsLoss => Result == TeamGameResult.Loss;

    public bool IsTie => Result == TeamGameResult.Tie;

    public bool ShowLineupStatus => IsUpcoming && IsNextPlayingDate;

    public string StatusAutomationId => $"GameScheduleStatus{GameId}";

    public string ResultAutomationId => $"GameScheduleResult{GameId}";

    public string ScoreAutomationId => $"GameScheduleScore{GameId}";

    public IAsyncRelayCommand OpenCommand { get; }

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
        set
        {
            SetProperty(ref isSelectionVisible, value);
        }
    }
}
