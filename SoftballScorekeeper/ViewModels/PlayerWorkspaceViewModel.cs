using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerWorkspaceViewModel : ObservableObject, IQueryAttributable
{
    private readonly PlayerRepository playerRepository;
    private readonly TeamPlayerRepository teamPlayerRepository;
    private bool hasValidPlayerId;
    private bool isConfirmationPending;
    private Player? loadedPlayer;

    [ObservableProperty]
    private int playerId;

    [ObservableProperty]
    private string playerDisplayName = string.Empty;

    [ObservableProperty]
    private string editableFirstName = string.Empty;

    [ObservableProperty]
    private string editableLastName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginPlayerNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePlayerNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPlayerNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivatePlayerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivatePlayerCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginPlayerNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePlayerNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPlayerNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivatePlayerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivatePlayerCommand))]
    private bool isPlayerLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginPlayerNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePlayerNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPlayerNameEditCommand))]
    private bool isEditingPlayerName;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private bool hasTeamMemberships;

    [ObservableProperty]
    private bool hasNoTeamMemberships;

    public PlayerWorkspaceViewModel(
        PlayerRepository playerRepository,
        TeamPlayerRepository teamPlayerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);
        this.playerRepository = playerRepository;
        this.teamPlayerRepository = teamPlayerRepository;
    }

    public ObservableCollection<Team> TeamMemberships { get; } = new();

    public Func<string, Task>? DeactivatePlayerConfirmationRequested { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.PlayerIdQueryParameter, out var value))
        {
            if (!hasValidPlayerId)
            {
                ValidationMessage = AppResources.PlayerNotFound;
            }

            return;
        }

        if (!TryParsePlayerId(value, out var parsedPlayerId))
        {
            ResetPlayerState();
            ValidationMessage = AppResources.PlayerNotFound;
            return;
        }

        if (hasValidPlayerId && PlayerId == parsedPlayerId)
        {
            return;
        }

        ResetPlayerState();
        PlayerId = parsedPlayerId;
        hasValidPlayerId = true;
    }

    public async Task LoadPlayerAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!hasValidPlayerId)
        {
            ValidationMessage = AppResources.PlayerNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            if (IsPlayerLoaded)
            {
                await ReloadTeamMembershipsAsync();
                return;
            }

            var player = await playerRepository.GetActiveByIdAsync(PlayerId);
            if (player is null)
            {
                ValidationMessage = AppResources.PlayerNotFound;
                return;
            }

            loadedPlayer = player;
            PlayerDisplayName = FormatDisplayName(player.FirstName, player.LastName);
            EditableFirstName = player.FirstName;
            EditableLastName = player.LastName ?? string.Empty;
            ValidationMessage = string.Empty;
            IsPlayerLoaded = true;
            await ReloadTeamMembershipsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBeginPlayerNameEdit))]
    private void BeginPlayerNameEdit()
    {
        EditableFirstName = loadedPlayer!.FirstName;
        EditableLastName = loadedPlayer.LastName ?? string.Empty;
        ValidationMessage = string.Empty;
        IsEditingPlayerName = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditPlayerName))]
    private async Task SavePlayerNameAsync()
    {
        if (!CanEditPlayerName())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var normalizedFirstName = EditableFirstName?.Trim() ?? string.Empty;
            var trimmedLastName = EditableLastName?.Trim() ?? string.Empty;
            var normalizedLastName = string.IsNullOrEmpty(trimmedLastName) ? null : trimmedLastName;

            if (string.IsNullOrWhiteSpace(normalizedFirstName))
            {
                ValidationMessage = AppResources.FirstNameRequired;
                return;
            }

            if (string.Equals(normalizedFirstName, loadedPlayer!.FirstName, StringComparison.Ordinal)
                && string.Equals(normalizedLastName, loadedPlayer.LastName, StringComparison.Ordinal))
            {
                EditableFirstName = loadedPlayer.FirstName;
                EditableLastName = loadedPlayer.LastName ?? string.Empty;
                ValidationMessage = string.Empty;
                IsEditingPlayerName = false;
                return;
            }

            var updatedPlayer = new Player
            {
                Id = loadedPlayer.Id,
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
                IsActive = loadedPlayer.IsActive,
            };

            try
            {
                if (await playerRepository.UpdateAsync(updatedPlayer) != 1)
                {
                    ValidationMessage = AppResources.PlayerUpdateFailed;
                    return;
                }
            }
            catch
            {
                ValidationMessage = AppResources.PlayerUpdateFailed;
                return;
            }

            loadedPlayer = updatedPlayer;
            PlayerDisplayName = FormatDisplayName(updatedPlayer.FirstName, updatedPlayer.LastName);
            EditableFirstName = updatedPlayer.FirstName;
            EditableLastName = updatedPlayer.LastName ?? string.Empty;
            ValidationMessage = string.Empty;
            IsEditingPlayerName = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditPlayerName))]
    private void CancelPlayerNameEdit()
    {
        EditableFirstName = loadedPlayer!.FirstName;
        EditableLastName = loadedPlayer.LastName ?? string.Empty;
        ValidationMessage = string.Empty;
        IsEditingPlayerName = false;
    }

    [RelayCommand(CanExecute = nameof(CanRequestDeactivatePlayer))]
    private async Task RequestDeactivatePlayerAsync()
    {
        if (!CanRequestDeactivatePlayer())
        {
            return;
        }

        isConfirmationPending = true;
        NotifyCommandStates();
        try
        {
            var confirmationRequested = DeactivatePlayerConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(PlayerDisplayName);
            }
        }
        finally
        {
            isConfirmationPending = false;
            NotifyCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDeactivatePlayer))]
    private async Task ConfirmDeactivatePlayerAsync()
    {
        if (!CanConfirmDeactivatePlayer())
        {
            return;
        }

        IsBusy = true;
        try
        {
            int affectedRows;
            try
            {
                affectedRows = await playerRepository.DeactivateAsync(new[] { loadedPlayer!.Id });
            }
            catch
            {
                ValidationMessage = AppResources.PlayerDeactivationFailed;
                return;
            }

            if (affectedRows != 1)
            {
                ValidationMessage = AppResources.PlayerDeactivationFailed;
                return;
            }

            loadedPlayer.IsActive = false;
            IsPlayerLoaded = false;
            ValidationMessage = string.Empty;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBeginPlayerNameEdit()
    {
        return CanManagePlayer() && !IsEditingPlayerName && !isConfirmationPending;
    }

    private bool CanEditPlayerName()
    {
        return CanManagePlayer() && IsEditingPlayerName && !isConfirmationPending;
    }

    private bool CanRequestDeactivatePlayer()
    {
        return CanManagePlayer() && !isConfirmationPending;
    }

    private bool CanConfirmDeactivatePlayer()
    {
        return CanManagePlayer() && isConfirmationPending;
    }

    private bool CanManagePlayer()
    {
        return !IsBusy
            && hasValidPlayerId
            && IsPlayerLoaded
            && loadedPlayer is { Id: > 0, IsActive: true };
    }

    private void NotifyCommandStates()
    {
        BeginPlayerNameEditCommand.NotifyCanExecuteChanged();
        SavePlayerNameCommand.NotifyCanExecuteChanged();
        CancelPlayerNameEditCommand.NotifyCanExecuteChanged();
        RequestDeactivatePlayerCommand.NotifyCanExecuteChanged();
        ConfirmDeactivatePlayerCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadTeamMembershipsAsync()
    {
        var teams = await teamPlayerRepository.GetActiveTeamsForPlayerAsync(PlayerId);
        TeamMemberships.Clear();
        foreach (var team in teams)
        {
            TeamMemberships.Add(team);
        }

        HasTeamMemberships = TeamMemberships.Count > 0;
        HasNoTeamMemberships = !HasTeamMemberships;
    }

    private void ResetPlayerState()
    {
        PlayerId = 0;
        hasValidPlayerId = false;
        loadedPlayer = null;
        PlayerDisplayName = string.Empty;
        EditableFirstName = string.Empty;
        EditableLastName = string.Empty;
        TeamMemberships.Clear();
        HasTeamMemberships = false;
        HasNoTeamMemberships = false;
        ValidationMessage = string.Empty;
        IsEditingPlayerName = false;
        IsPlayerLoaded = false;
    }

    private static string FormatDisplayName(string firstName, string? lastName)
    {
        return string.Join(
            " ",
            new[] { firstName, lastName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim()));
    }

    private static bool TryParsePlayerId(object? value, out int parsedPlayerId)
    {
        if (value is int integerValue && integerValue > 0)
        {
            parsedPlayerId = integerValue;
            return true;
        }

        if (value is string stringValue
            && int.TryParse(stringValue, out var stringPlayerId)
            && stringPlayerId > 0)
        {
            parsedPlayerId = stringPlayerId;
            return true;
        }

        parsedPlayerId = 0;
        return false;
    }
}
