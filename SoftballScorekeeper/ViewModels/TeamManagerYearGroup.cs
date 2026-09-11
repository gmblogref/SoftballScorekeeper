using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class TeamManagerYearGroup : ObservableObject
{
    private bool isExpanded;

    public TeamManagerYearGroup(
        int year,
        IReadOnlyList<TeamManagerItem> teams,
        bool isExpanded)
    {
        Year = year;
        Teams = teams;
        this.isExpanded = isExpanded;
        ToggleExpansionCommand = new RelayCommand(ToggleExpansion);
    }

    public int Year { get; }

    public IReadOnlyList<TeamManagerItem> Teams { get; }

    public IRelayCommand ToggleExpansionCommand { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
                OnPropertyChanged(nameof(HeaderDisplay));
        }
    }

    public string HeaderDisplay => string.Format(
        CultureInfo.CurrentCulture,
        IsExpanded
            ? AppResources.ExpandedTeamYearHeaderFormat
            : AppResources.CollapsedTeamYearHeaderFormat,
        Year);

    private void ToggleExpansion() => IsExpanded = !IsExpanded;
}
