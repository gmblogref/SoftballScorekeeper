using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class EditTeamViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private int teamId;
    private bool hasValidTeamId;
    private Team? loadedTeam;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivateTeamCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivateTeamCommand))]
    private bool isTeamLoaded;

    public EditTeamViewModel(TeamRepository teamRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);

        this.teamRepository = teamRepository;
    }

    public Func<string, Task>? DeactivateTeamConfirmationRequested { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ResetTeamState();

        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value)
            || !TryParseTeamId(value, out var parsedTeamId))
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        teamId = parsedTeamId;
        hasValidTeamId = true;
        ValidationMessage = string.Empty;
    }

    public async Task LoadTeamAsync()
    {
        if (IsBusy || IsTeamLoaded)
        {
            return;
        }

        loadedTeam = null;
        TeamName = string.Empty;
        IsTeamLoaded = false;

        if (!hasValidTeamId)
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(teamId);
            if (team is null)
            {
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            loadedTeam = team;
            TeamName = team.Name;
            ValidationMessage = string.Empty;
            IsTeamLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!hasValidTeamId || !IsTeamLoaded || loadedTeam is null)
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            var trimmedName = TeamName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ValidationMessage = AppResources.TeamNameRequired;
                return;
            }

            if (await teamRepository.ActiveNameExistsAsync(trimmedName, loadedTeam.Id))
            {
                ValidationMessage = AppResources.DuplicateTeamName;
                return;
            }

            TeamName = trimmedName;
            var originalName = loadedTeam.Name;
            loadedTeam.Name = trimmedName;

            try
            {
                if (await teamRepository.UpdateAsync(loadedTeam) == 0)
                {
                    loadedTeam.Name = originalName;
                    loadedTeam = null;
                    IsTeamLoaded = false;
                    ValidationMessage = AppResources.TeamNotFound;
                    return;
                }
            }
            catch
            {
                loadedTeam.Name = originalName;
                throw;
            }

            ValidationMessage = string.Empty;
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
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeactivateTeam))]
    private async Task RequestDeactivateTeamAsync()
    {
        if (!CanDeactivateTeam())
        {
            return;
        }

        var confirmationRequested = DeactivateTeamConfirmationRequested;
        if (confirmationRequested is not null)
        {
            await confirmationRequested(loadedTeam!.Name);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeactivateTeam))]
    private async Task ConfirmDeactivateTeamAsync()
    {
        if (!CanDeactivateTeam())
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            if (await teamRepository.DeactivateAsync(loadedTeam!.Id) == 0)
            {
                loadedTeam = null;
                IsTeamLoaded = false;
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            loadedTeam.IsActive = false;
            IsTeamLoaded = false;
            ValidationMessage = string.Empty;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDeactivateTeam()
    {
        return !IsBusy
            && hasValidTeamId
            && IsTeamLoaded
            && loadedTeam is { Id: > 0, IsActive: true };
    }

    private void ResetTeamState()
    {
        teamId = 0;
        hasValidTeamId = false;
        loadedTeam = null;
        TeamName = string.Empty;
        ValidationMessage = string.Empty;
        IsTeamLoaded = false;
    }

    private static bool TryParseTeamId(object? value, out int parsedTeamId)
    {
        if (value is int integerValue && integerValue > 0)
        {
            parsedTeamId = integerValue;
            return true;
        }

        if (value is string stringValue
            && int.TryParse(stringValue, out var stringTeamId)
            && stringTeamId > 0)
        {
            parsedTeamId = stringTeamId;
            return true;
        }

        parsedTeamId = 0;
        return false;
    }
}
