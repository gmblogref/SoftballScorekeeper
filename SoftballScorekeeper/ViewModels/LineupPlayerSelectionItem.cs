using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class LineupPlayerSelectionItem : ObservableObject
{
    private readonly Action<LineupPlayerSelectionItem>? selectionChanged;
    private bool isSelected;
    [ObservableProperty] private bool isSelectionVisible;

    public LineupPlayerSelectionItem(
        GameLineupPlayer lineupPlayer,
        Player player,
        Action<LineupPlayerSelectionItem>? selectionChanged = null)
    {
        LineupPlayer = lineupPlayer;
        Player = player;
        this.selectionChanged = selectionChanged;
    }

    public GameLineupPlayer LineupPlayer { get; }
    public Player Player { get; }
    public string DisplayText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        AppResources.LineupPlayerFormat,
        LineupPlayer.BattingOrder,
        string.Join(" ", new[] { Player.FirstName, Player.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))));

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value)) selectionChanged?.Invoke(this);
        }
    }
}
