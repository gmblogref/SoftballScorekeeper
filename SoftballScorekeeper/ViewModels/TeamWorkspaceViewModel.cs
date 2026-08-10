using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly GameRepository gameRepository;
    private bool hasGames;
    private bool hasInitialSectionSelection;
    private bool hasValidTeamId;
    private bool isConfirmationPending;
    private IReadOnlyList<int> pendingPlayerIds = Array.Empty<int>();
    private int selectedPlayerCount;
    private TeamWorkspaceSection selectedSection = TeamWorkspaceSection.Roster;
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
    private bool isRemovingPlayers;

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
        TeamPlayerRepository teamPlayerRepository,
        GameRepository gameRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);
        ArgumentNullException.ThrowIfNull(gameRepository);

        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
        this.gameRepository = gameRepository;
    }

    public ObservableCollection<PlayerSelectionItem> RosterPlayers { get; } = new();

    public Func<string, Task>? DeactivateTeamConfirmationRequested { get; set; }

    public Func<int, string, Task>? RemovePlayersConfirmationRequested { get; set; }

    public int SelectedPlayerCount
    {
        get => selectedPlayerCount;
        private set
        {
            if (SetProperty(ref selectedPlayerCount, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlayers));
                NotifyCommandStates();
            }
        }
    }

    public bool HasSelectedPlayers => SelectedPlayerCount > 0;

    public bool HasGames
    {
        get => hasGames;
        private set
        {
            if (SetProperty(ref hasGames, value))
            {
                OnPropertyChanged(nameof(HasNoGames));
            }
        }
    }

    public bool HasNoGames => !HasGames;

    public TeamWorkspaceSection SelectedSection
    {
        get => selectedSection;
        private set
        {
            if (SetProperty(ref selectedSection, value))
            {
                OnPropertyChanged(nameof(IsGamesSectionSelected));
                OnPropertyChanged(nameof(IsRosterSectionSelected));
            }
        }
    }

    public bool IsGamesSectionSelected => SelectedSection == TeamWorkspaceSection.Games;

    public bool IsRosterSectionSelected => SelectedSection == TeamWorkspaceSection.Roster;

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
            var teamHasGames = await gameRepository.HasGamesForTeamAsync(TeamId);

            loadedTeam = team;
            TeamName = team.Name;
            EditableTeamName = team.Name;
            RosterPlayers.Clear();
            foreach (var player in players)
            {
                RosterPlayers.Add(new PlayerSelectionItem(player, OnPlayerSelectionChanged));
            }

            HasPlayers = RosterPlayers.Count > 0;
            HasNoPlayers = !HasPlayers;
            HasGames = teamHasGames;
            if (!hasInitialSectionSelection)
            {
                SelectedSection = HasGames
                    ? TeamWorkspaceSection.Games
                    : TeamWorkspaceSection.Roster;
                hasInitialSectionSelection = true;
            }

            ValidationMessage = string.Empty;
            StatusMessage = string.Empty;
            ExitRemovePlayersMode();
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

    [RelayCommand]
    private void SelectGamesSection()
    {
        ExitRemovePlayersMode();
        SelectedSection = TeamWorkspaceSection.Games;
        hasInitialSectionSelection = true;
    }

    [RelayCommand]
    private void SelectRosterSection()
    {
        SelectedSection = TeamWorkspaceSection.Roster;
        hasInitialSectionSelection = true;
    }

    [RelayCommand(CanExecute = nameof(CanBeginRemovePlayers))]
    private void RemovePlayers()
    {
        ClearRosterSelection();
        IsRemovingPlayers = true;
        NotifyCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanCancelRemovePlayers))]
    private void CancelRemovePlayers()
    {
        ExitRemovePlayersMode();
        NotifyCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedPlayers))]
    private async Task RemoveSelectedPlayersAsync()
    {
        pendingPlayerIds = RosterPlayers
            .Where(item => item.IsSelected && item.Player.Id > 0)
            .Select(item => item.Player.Id)
            .Distinct()
            .ToArray();

        if (pendingPlayerIds.Count == 0)
        {
            UpdateSelectionState();
            return;
        }

        isConfirmationPending = true;
        NotifyCommandStates();
        try
        {
            var confirmationRequested = RemovePlayersConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(pendingPlayerIds.Count, TeamName);
            }
        }
        finally
        {
            pendingPlayerIds = Array.Empty<int>();
            isConfirmationPending = false;
            NotifyCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmRemoveSelectedPlayers))]
    private async Task ConfirmRemoveSelectedPlayersAsync()
    {
        var requestedIds = pendingPlayerIds.ToArray();
        IsBusy = true;
        try
        {
            var result = await teamPlayerRepository.RemovePlayersFromTeamAsync(TeamId, requestedIds);
            await ReloadRosterAsync(result.SuccessfulRemovalCount == requestedIds.Length ? null : requestedIds);

            if (result.SuccessfulRemovalCount == requestedIds.Length)
            {
                ValidationMessage = string.Empty;
                ExitRemovePlayersMode();
                return;
            }

            ValidationMessage = result.SuccessfulRemovalCount == 0
                ? AppResources.RemovePlayersNoOpMessage
                : string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.RemovePlayersPartialResultMessage,
                    result.SuccessfulRemovalCount,
                    requestedIds.Length);
        }
        catch
        {
            await ReloadRosterAsync(requestedIds);
            ValidationMessage = AppResources.RemovePlayersNoOpMessage;
        }
        finally
        {
            IsBusy = false;
        }
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
        return CanManageTeam() && !IsEditingTeamName && !IsRemovingPlayers && !isConfirmationPending;
    }

    private bool CanBeginRemovePlayers() => CanManageRoster() && HasPlayers;

    private bool CanCancelRemovePlayers() =>
        CanManageTeam() && IsRemovingPlayers && !isConfirmationPending;

    private bool CanRemoveSelectedPlayers() =>
        CanCancelRemovePlayers() && HasSelectedPlayers;

    private bool CanConfirmRemoveSelectedPlayers() =>
        CanManageTeam() && IsRemovingPlayers && isConfirmationPending && pendingPlayerIds.Count > 0;

    private bool CanManageTeam()
    {
        return !IsBusy
            && hasValidTeamId
            && IsTeamLoaded
            && loadedTeam is { Id: > 0, IsActive: true };
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();

    partial void OnIsTeamLoadedChanged(bool value) => NotifyCommandStates();

    partial void OnIsEditingTeamNameChanged(bool value) => NotifyCommandStates();

    partial void OnIsRemovingPlayersChanged(bool value) => NotifyCommandStates();

    private void NotifyCommandStates()
    {
        BeginTeamNameEditCommand.NotifyCanExecuteChanged();
        SaveTeamNameCommand.NotifyCanExecuteChanged();
        CancelTeamNameEditCommand.NotifyCanExecuteChanged();
        RequestDeactivateTeamCommand.NotifyCanExecuteChanged();
        ConfirmDeactivateTeamCommand.NotifyCanExecuteChanged();
        AddPlayersCommand.NotifyCanExecuteChanged();
        RemovePlayersCommand.NotifyCanExecuteChanged();
        CancelRemovePlayersCommand.NotifyCanExecuteChanged();
        RemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
        ConfirmRemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
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
        RosterPlayers.Clear();
        HasPlayers = false;
        HasNoPlayers = false;
        HasGames = false;
        hasInitialSectionSelection = false;
        ValidationMessage = string.Empty;
        StatusMessage = string.Empty;
        IsEditingTeamName = false;
        ExitRemovePlayersMode();
        SelectedSection = TeamWorkspaceSection.Roster;
        IsTeamLoaded = false;
    }

    private async Task ReloadRosterAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var players = await teamPlayerRepository.GetActivePlayersForTeamAsync(TeamId);
        var selectedIds = idsToReselect?.ToHashSet();

        RosterPlayers.Clear();
        foreach (var player in players)
        {
            var item = new PlayerSelectionItem(player, OnPlayerSelectionChanged);
            if (selectedIds?.Contains(player.Id) == true)
            {
                item.IsSelected = true;
            }

            RosterPlayers.Add(item);
        }

        HasPlayers = RosterPlayers.Count > 0;
        HasNoPlayers = !HasPlayers;
        UpdateSelectionState();
    }

    private void OnPlayerSelectionChanged(PlayerSelectionItem item) => UpdateSelectionState();

    private void UpdateSelectionState() =>
        SelectedPlayerCount = RosterPlayers.Count(item => item.IsSelected);

    private void ClearRosterSelection()
    {
        foreach (var item in RosterPlayers)
        {
            item.IsSelected = false;
        }

        UpdateSelectionState();
    }

    private void ExitRemovePlayersMode()
    {
        ClearRosterSelection();
        IsRemovingPlayers = false;
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
