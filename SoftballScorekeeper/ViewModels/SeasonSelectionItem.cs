using System.Globalization;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class SeasonSelectionItem
{
    public SeasonSelectionItem(Season season)
    {
        ArgumentNullException.ThrowIfNull(season);

        Season = season;
        DisplayName = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.SeasonSelectionDisplayFormat,
            season.Name,
            season.Year);
    }

    public Season Season { get; }

    public string DisplayName { get; }
}
