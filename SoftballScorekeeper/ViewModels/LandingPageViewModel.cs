using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class LandingPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string? statusMessage;

    [RelayCommand]
    private void CreateTeam()
    {
        // Temporary feedback until a future story implements team creation.
        StatusMessage = AppResources.CreateTeamPlaceholderMessage;
    }
}
