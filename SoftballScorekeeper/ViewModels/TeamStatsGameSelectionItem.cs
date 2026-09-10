using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class TeamStatsGameSelectionItem : ObservableObject
{
    public TeamStatsGameSelectionItem(TeamCompletedGame game, bool isSelected)
    {
        GameId = game.GameId;
        DisplayText = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.TeamStatsCustomGameFormat,
            game.ScheduledDate,
            string.IsNullOrWhiteSpace(game.Opponent) ? AppResources.ScheduleTbdText : game.Opponent,
            DateTime.Today.Add(game.ScheduledTime),
            game.SeasonName);
        this.isSelected = isSelected;
    }

    public int GameId { get; }
    public string DisplayText { get; }
    public string SelectionAutomationId => $"TeamStatsGameSelection-{GameId}";
    public string DisplayAutomationId => $"TeamStatsGameDisplay-{GameId}";

    [ObservableProperty]
    private bool isSelected;
}
