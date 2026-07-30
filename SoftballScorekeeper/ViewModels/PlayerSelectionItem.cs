using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerSelectionItem : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    public PlayerSelectionItem(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        Player = player;
    }

    public Player Player { get; }

    public string DisplayName => string.Join(
        " ",
        new[] { Player.FirstName, Player.LastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim()));
}
