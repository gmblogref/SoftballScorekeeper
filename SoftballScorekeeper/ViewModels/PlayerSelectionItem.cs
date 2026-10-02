using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerSelectionItem : ObservableObject
{
    private bool isSelected;
    private readonly Action<PlayerSelectionItem>? selectionChanged;

    public PlayerSelectionItem(
        Player player,
        Action<PlayerSelectionItem>? selectionChanged = null,
        decimal? onBasePercentage = null
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        Player = player;
        this.selectionChanged = selectionChanged;
        OnBasePercentage = onBasePercentage;
    }

    public Player Player { get; }
    public decimal? OnBasePercentage { get; }
    public string OnBasePercentageDisplay =>
        OnBasePercentage is decimal value
            ? string.Format(AppResources.LineupObpDisplayFormat, BattingRateFormatter.Format(value))
            : string.Format(AppResources.LineupObpDisplayFormat, BattingRateFormatter.Format(0));

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

    public string DisplayName =>
        string.Join(
        " ",
        new[] { Player.FirstName, Player.LastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
        );
}
