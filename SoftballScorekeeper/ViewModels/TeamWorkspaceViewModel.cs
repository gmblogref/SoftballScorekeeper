using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class TeamWorkspaceViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly TeamPlayerRepository teamPlayerRepository;
    private bool hasValidTeamId;
    private bool isConfirmationPending;
    private Team? loadedTeam;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveTeamNameCommand))]
    private string editableTeamName = string.Empty;

    [ObservableProperty]
    private bool hasPlayers;

    [ObservableProperty]
    private bool hasNoPlayers;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveTeamNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePlayersCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveTeamNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePlayersCommand))]
    private bool isTeamLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveTeamNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePlayersCommand))]
    private bool isEditingTeamName;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public TeamWorkspaceViewModel(
        TeamRepository teamRepository,
        TeamPlayerRepository teamPlayerRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);

        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
    }

    public ObservableCollection<Player> Players { get; } = new();

    public Func<string, Task>? DeactivateTeamConfirmationRequested { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value))
        {
            if (!hasValidTeamId)
            {
                ValidationMessage = AppResources.TeamNotFound;
            }

            return;
        }

        if (!TryParseTeamId(value, out var parsedTeamId))
        {
            ResetTeamState();
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        if (hasValidTeamId && TeamId == parsedTeamId)
        {
            return;
        }

        ResetTeamState();
        TeamId = parsedTeamId;
        hasValidTeamId = true;
    }

    public async Task LoadTeamAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!hasValidTeamId)
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            if (team is null)
            {
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            var players = await teamPlayerRepository.GetActivePlayersForTeamAsync(TeamId);

            loadedTeam = team;
            TeamName = team.Name;
            EditableTeamName = team.Name;
            Players.Clear();
            foreach (var player in players)
            {
                Players.Add(player);
            }

            HasPlayers = Players.Count > 0;
            HasNoPlayers = !HasPlayers;
            ValidationMessage = string.Empty;
            StatusMessage = string.Empty;
            IsTeamLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBeginTeamNameEdit))]
    private void BeginTeamNameEdit()
    {
        EditableTeamName = loadedTeam!.Name;
        ValidationMessage = string.Empty;
        StatusMessage = string.Empty;
        IsEditingTeamName = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditTeamName))]
    private async Task SaveTeamNameAsync()
    {
        if (!CanEditTeamName())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var trimmedName = EditableTeamName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ValidationMessage = AppResources.TeamNameRequired;
                return;
            }

            if (string.Equals(trimmedName, loadedTeam!.Name, StringComparison.Ordinal))
            {
                EditableTeamName = loadedTeam.Name;
                ValidationMessage = string.Empty;
                IsEditingTeamName = false;
                return;
            }

            if (await teamRepository.ActiveNameExistsAsync(trimmedName, loadedTeam.Id))
            {
                ValidationMessage = AppResources.DuplicateTeamName;
                return;
            }

            if (await teamRepository.InactiveNameExistsAsync(trimmedName, loadedTeam.Id))
            {
                ValidationMessage = AppResources.InactiveTeamNameExists;
                return;
            }

            var originalName = loadedTeam.Name;
            loadedTeam.Name = trimmedName;
            try
            {
                if (await teamRepository.UpdateAsync(loadedTeam) == 0)
                {
                    loadedTeam.Name = originalName;
                    InvalidateTeam();
                    ValidationMessage = AppResources.TeamNotFound;
                    return;
                }
            }
            catch
            {
                loadedTeam.Name = originalName;
                throw;
            }

            TeamName = trimmedName;
            EditableTeamName = trimmedName;
            ValidationMessage = string.Empty;
            IsEditingTeamName = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditTeamName))]
    private void CancelTeamNameEdit()
    {
        EditableTeamName = loadedTeam!.Name;
        ValidationMessage = string.Empty;
        IsEditingTeamName = false;
    }

    [RelayCommand(CanExecute = nameof(CanRequestDeactivateTeam))]
    private async Task RequestDeactivateTeamAsync()
    {
        if (!CanRequestDeactivateTeam())
        {
            return;
        }

        isConfirmationPending = true;
        NotifyCommandStates();
        try
        {
            var confirmationRequested = DeactivateTeamConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(loadedTeam!.Name);
            }
        }
        finally
        {
            isConfirmationPending = false;
            NotifyCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDeactivateTeam))]
    private async Task ConfirmDeactivateTeamAsync()
    {
        if (!CanConfirmDeactivateTeam())
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (await teamRepository.DeactivateAsync(loadedTeam!.Id) == 0)
            {
                InvalidateTeam();
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

    [RelayCommand(CanExecute = nameof(CanManageRoster))]
    private async Task AddPlayersAsync()
    {
        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId
        };

        await Shell.Current.GoToAsync(AppShell.AddPlayersToTeamRoute, parameters);
    }

    [RelayCommand(CanExecute = nameof(CanManageRoster))]
    private void RemovePlayers()
    {
        StatusMessage = AppResources.RemovePlayersPlaceholderMessage;
    }

    private bool CanBeginTeamNameEdit()
    {
        return CanManageTeam() && !IsEditingTeamName;
    }

    private bool CanEditTeamName()
    {
        return CanManageTeam() && IsEditingTeamName;
    }

    private bool CanRequestDeactivateTeam()
    {
        return CanManageTeam() && !IsEditingTeamName && !isConfirmationPending;
    }

    private bool CanConfirmDeactivateTeam()
    {
        return CanManageTeam() && !IsEditingTeamName && isConfirmationPending;
    }

    private bool CanManageRoster()
    {
        return CanManageTeam() && !IsEditingTeamName && !isConfirmationPending;
    }

    private bool CanManageTeam()
    {
        return !IsBusy
            && hasValidTeamId
            && IsTeamLoaded
            && loadedTeam is { Id: > 0, IsActive: true };
    }

    private void NotifyCommandStates()
    {
        BeginTeamNameEditCommand.NotifyCanExecuteChanged();
        SaveTeamNameCommand.NotifyCanExecuteChanged();
        CancelTeamNameEditCommand.NotifyCanExecuteChanged();
        RequestDeactivateTeamCommand.NotifyCanExecuteChanged();
        ConfirmDeactivateTeamCommand.NotifyCanExecuteChanged();
        AddPlayersCommand.NotifyCanExecuteChanged();
        RemovePlayersCommand.NotifyCanExecuteChanged();
    }

    private void InvalidateTeam()
    {
        loadedTeam = null;
        IsTeamLoaded = false;
        IsEditingTeamName = false;
    }

    private void ResetTeamState()
    {
        TeamId = 0;
        hasValidTeamId = false;
        loadedTeam = null;
        TeamName = string.Empty;
        EditableTeamName = string.Empty;
        Players.Clear();
        HasPlayers = false;
        HasNoPlayers = false;
        ValidationMessage = string.Empty;
        StatusMessage = string.Empty;
        IsEditingTeamName = false;
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
