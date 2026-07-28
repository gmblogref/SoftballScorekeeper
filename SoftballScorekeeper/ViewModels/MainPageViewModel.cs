using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private int count;

    [ObservableProperty]
    private string counterText = AppResources.CounterButtonText;

    [RelayCommand]
    private void IncrementCounter()
    {
        count++;
        CounterText = string.Format(
            CultureInfo.CurrentCulture,
            count == 1 ? AppResources.CounterStatusSingular : AppResources.CounterStatusPlural,
            count);
    }
}
