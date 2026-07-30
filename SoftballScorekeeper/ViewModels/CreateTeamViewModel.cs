using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class CreateTeamViewModel : ObservableObject
{
    private readonly TeamRepository teamRepository;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isSaved;

    public CreateTeamViewModel(TeamRepository teamRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);

        this.teamRepository = teamRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsSaved = false;
        IsBusy = true;
        try
        {
            if (!await ValidateAsync())
            {
                return;
            }

            var team = new Team
            {
                Name = TeamName,
            };

            await teamRepository.CreateAsync(team);
            IsSaved = true;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        TeamName = string.Empty;
        ValidationMessage = string.Empty;
        IsSaved = false;
        await Shell.Current.GoToAsync("..");
    }

    private async Task<bool> ValidateAsync()
    {
        var trimmedName = TeamName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ValidationMessage = AppResources.TeamNameRequired;
            return false;
        }

        TeamName = trimmedName;

        if (await teamRepository.ActiveNameExistsAsync(trimmedName))
        {
            ValidationMessage = AppResources.DuplicateTeamName;
            return false;
        }

        if (await teamRepository.InactiveNameExistsAsync(trimmedName))
        {
            ValidationMessage = AppResources.InactiveTeamNameExists;
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }
}
