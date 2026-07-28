using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SoftballScorekeeper.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private int count;

    [ObservableProperty]
    private string counterText = "Click me";

    [RelayCommand]
    private void IncrementCounter()
    {
        count++;
        CounterText = count == 1
            ? $"Clicked {count} time"
            : $"Clicked {count} times";
    }
}
