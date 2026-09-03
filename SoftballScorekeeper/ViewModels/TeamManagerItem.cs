using System.Globalization;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class TeamManagerItem
{
    public TeamManagerItem(Team team, TeamYearRecord record)
    {
        Team = team;
        Year = record.Year;
        RecordDisplay = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.TeamRecordFormat,
            record.Wins,
            record.Losses
        );
    }

    public Team Team { get; }
    public int Id => Team.Id;
    public string Name => Team.Name;
    public int? Year { get; }
    public bool HasYear => Year.HasValue;
    public string RecordDisplay { get; }
    public string CardAutomationId => $"TeamCard-{Id}";
    public string NameAutomationId => $"TeamName-{Id}";
    public string YearAutomationId => $"TeamYear-{Id}";
    public string RecordAutomationId => $"TeamRecord-{Id}";
}
