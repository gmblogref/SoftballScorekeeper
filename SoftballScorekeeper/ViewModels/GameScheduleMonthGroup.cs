using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class GameScheduleMonthGroup : ObservableObject
{
    private bool isExpanded;

    public GameScheduleMonthGroup(
        int year,
        int month,
        string header,
        IReadOnlyList<GameScheduleItem> games,
        bool isExpanded)
    {
        Year = year;
        Month = month;
        Header = header;
        Games = games;
        this.isExpanded = isExpanded;
        ToggleExpansionCommand = new RelayCommand(ToggleExpansion);
    }

    public int Year { get; }

    public int Month { get; }

    public string Header { get; }

    public IReadOnlyList<GameScheduleItem> Games { get; }

    public IRelayCommand ToggleExpansionCommand { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
            {
                OnPropertyChanged(nameof(HeaderDisplay));
            }
        }
    }

    public string HeaderDisplay => string.Format(
        CultureInfo.CurrentCulture,
        IsExpanded
            ? AppResources.ExpandedScheduleMonthHeaderFormat
            : AppResources.CollapsedScheduleMonthHeaderFormat,
        Header);

    private void ToggleExpansion() => IsExpanded = !IsExpanded;
}
