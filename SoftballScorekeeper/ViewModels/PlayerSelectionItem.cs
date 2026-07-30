using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerSelectionItem : ObservableObject
{
    private bool isSelected;
    private readonly Action<PlayerSelectionItem>? selectionChanged;

    public PlayerSelectionItem(
        Player player,
        Action<PlayerSelectionItem>? selectionChanged = null)
    {
        ArgumentNullException.ThrowIfNull(player);

        Player = player;
        this.selectionChanged = selectionChanged;
    }

    public Player Player { get; }

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

    public string DisplayName => string.Join(
        " ",
        new[] { Player.FirstName, Player.LastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim()));
}
