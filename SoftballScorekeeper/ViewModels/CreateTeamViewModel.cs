using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
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
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndAddAnotherCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool isSaved;

    public CreateTeamViewModel(TeamRepository teamRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);

        this.teamRepository = teamRepository;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => SaveCoreAsync(addAnother: false);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAndAddAnotherAsync() => SaveCoreAsync(addAnother: true);

    private async Task SaveCoreAsync(bool addAnother)
    {
        if (!CanSave())
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

            if (await teamRepository.CreateAsync(team) != 1)
            {
                ValidationMessage = AppResources.TeamCreationFailed;
                return;
            }

            ValidationMessage = string.Empty;
            if (addAnother)
            {
                TeamName = string.Empty;
                return;
            }

            IsSaved = true;
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ValidationMessage = AppResources.TeamCreationFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
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

    private bool CanSave() => !IsBusy;
}
