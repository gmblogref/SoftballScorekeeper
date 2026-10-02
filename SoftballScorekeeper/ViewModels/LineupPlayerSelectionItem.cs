using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class LineupPlayerSelectionItem : ObservableObject
{
    private readonly Action<LineupPlayerSelectionItem>? selectionChanged;
    private bool isDragged;
    private bool isDropGapAfter;
    private bool isDropGapBefore;
    private bool isSelected;

    [ObservableProperty]
    private bool isSelectionVisible;

    public LineupPlayerSelectionItem(
        GameLineupPlayer lineupPlayer,
        Player player,
        Action<LineupPlayerSelectionItem>? selectionChanged = null,
        decimal? onBasePercentage = null
    )
    {
        LineupPlayer = lineupPlayer;
        Player = player;
        this.selectionChanged = selectionChanged;
        OnBasePercentage = onBasePercentage;
    }

    public GameLineupPlayer LineupPlayer { get; }
    public Player Player { get; }
    public decimal? OnBasePercentage { get; }
    public string OnBasePercentageDisplay =>
        OnBasePercentage is decimal value
            ? string.Format(AppResources.LineupObpDisplayFormat, BattingRateFormatter.Format(value))
            : string.Format(AppResources.LineupObpDisplayFormat, BattingRateFormatter.Format(0));
    public string OnBasePercentageAutomationId => $"CurrentLineupObp-{Player.Id}";
    public string DisplayText =>
        string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        AppResources.LineupPlayerFormat,
        LineupPlayer.BattingOrder,
            string.Join(
                " ",
                new[] { Player.FirstName, Player.LastName }.Where(value =>
                    !string.IsNullOrWhiteSpace(value)
                )
            )
        );

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
                selectionChanged?.Invoke(this);
        }
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
}
