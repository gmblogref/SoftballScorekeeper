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
    private readonly GameLineupRepository gameLineupRepository;
    private readonly GameScoringSessionRepository gameScoringSessionRepository;
    private readonly SeasonRepository seasonRepository;
    private bool hasGames;
    private bool hasInitialSectionSelection;
    private bool hasInitialScheduleState;
    private bool hasValidTeamId;
    private bool isConfirmationPending;
    private bool isRemovingGames;
    private IReadOnlyList<int> pendingGameIds = Array.Empty<int>();
    private IReadOnlyList<int> pendingPlayerIds = Array.Empty<int>();
    private int selectedGameCount;
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
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveTeamNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTeamNameEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
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
        GameRepository gameRepository,
        GameLineupRepository gameLineupRepository,
        GameScoringSessionRepository gameScoringSessionRepository,
        SeasonRepository seasonRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(gameLineupRepository);
        ArgumentNullException.ThrowIfNull(gameScoringSessionRepository);
        ArgumentNullException.ThrowIfNull(seasonRepository);

        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
        this.gameRepository = gameRepository;
        this.gameLineupRepository = gameLineupRepository;
        this.gameScoringSessionRepository = gameScoringSessionRepository;
        this.seasonRepository = seasonRepository;
    }

    public ObservableCollection<PlayerSelectionItem> RosterPlayers { get; } = new();

    public ObservableCollection<GameScheduleMonthGroup> ScheduleMonthGroups { get; } = new();

    public Func<string, Task>? DeactivateTeamConfirmationRequested { get; set; }

    public Func<int, string, Task>? RemovePlayersConfirmationRequested { get; set; }

    public Func<int, Task>? RemoveGamesConfirmationRequested { get; set; }

    public int SelectedGameCount
    {
        get => selectedGameCount;
        private set
        {
            if (SetProperty(ref selectedGameCount, value))
            {
                OnPropertyChanged(nameof(HasSelectedGames));
                OnPropertyChanged(nameof(SelectedGameCountText));
                NotifyCommandStates();
            }
        }
    }

    public bool HasSelectedGames => SelectedGameCount > 0;

    public bool IsRemovingGames
    {
        get => isRemovingGames;
        private set
        {
            if (SetProperty(ref isRemovingGames, value))
            {
                foreach (var item in ScheduleMonthGroups.SelectMany(group => group.Games))
                {
                    item.IsSelectionVisible = value;
                }

                NotifyCommandStates();
            }
        }
    }

    public string SelectedGameCountText => string.Format(
        CultureInfo.CurrentCulture,
        SelectedGameCount == 1
            ? AppResources.SelectedGameCountSingularText
            : AppResources.SelectedGameCountPluralText,
        SelectedGameCount);

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
                RemoveGamesCommand.NotifyCanExecuteChanged();
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
                AddGameCommand.NotifyCanExecuteChanged();
                RemoveGamesCommand.NotifyCanExecuteChanged();
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
            var games = await gameRepository.GetForTeamAsync(TeamId);
            var seasons = await seasonRepository.GetForTeamAsync(TeamId);
            var gameIdsWithLineups = await gameLineupRepository.GetGameIdsWithLineupsAsync(
                games.Select(game => game.Id));
            var scoringSessions = await gameScoringSessionRepository.GetForGamesAsync(
                games.Select(game => game.Id));

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
            RebuildSchedule(games, seasons, gameIdsWithLineups, scoringSessions);
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
            ExitRemoveGamesMode();
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

    [RelayCommand(CanExecute = nameof(CanAddGame))]
    private async Task AddGameAsync()
    {
        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId
        };

        await Shell.Current.GoToAsync(AppShell.AddGameRoute, parameters);
    }

    private async Task OpenGameAsync(GameScheduleItem item)
    {
        if (item.GameId <= 0) return;
        if (IsRemovingGames)
        {
            item.IsSelected = !item.IsSelected;
            return;
        }

        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId,
            [AppShell.GameIdQueryParameter] = item.GameId,
        };
        await Shell.Current.GoToAsync(AppShell.GameWorkspaceRoute, parameters);
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
        ExitRemoveGamesMode();
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

    [RelayCommand(CanExecute = nameof(CanBeginRemoveGames))]
    private void RemoveGames()
    {
        ClearGameSelection();
        ValidationMessage = string.Empty;
        IsRemovingGames = true;
        NotifyCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanCancelRemoveGames))]
    private void CancelRemoveGames()
    {
        ValidationMessage = string.Empty;
        ExitRemoveGamesMode();
        NotifyCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedGames))]
    private async Task RemoveSelectedGamesAsync()
    {
        pendingGameIds = ScheduleMonthGroups
            .SelectMany(group => group.Games)
            .Where(item => item.IsSelected && item.GameId > 0)
            .Select(item => item.GameId)
            .Distinct()
            .ToArray();

        if (pendingGameIds.Count == 0)
        {
            UpdateGameSelectionState();
            return;
        }

        isConfirmationPending = true;
        NotifyCommandStates();
        try
        {
            var confirmationRequested = RemoveGamesConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(pendingGameIds.Count);
            }
        }
        finally
        {
            pendingGameIds = Array.Empty<int>();
            isConfirmationPending = false;
            NotifyCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmRemoveSelectedGames))]
    private async Task ConfirmRemoveSelectedGamesAsync()
    {
        var requestedIds = pendingGameIds.ToArray();
        IsBusy = true;
        try
        {
            var result = await gameRepository.RemoveGamesAsync(TeamId, requestedIds);
            await ReloadScheduleAsync(
                result.SuccessfulRemovalCount == requestedIds.Length ? null : requestedIds);

            if (result.SuccessfulRemovalCount == requestedIds.Length)
            {
                ValidationMessage = string.Empty;
                ExitRemoveGamesMode();
                return;
            }

            ValidationMessage = result.SuccessfulRemovalCount == 0
                ? result.BlockedGameIds.Count > 0
                    ? AppResources.RemoveGamesBlockedMessage
                    : AppResources.RemoveGamesNoOpMessage
                : string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.RemoveGamesPartialResultMessage,
                    result.SuccessfulRemovalCount,
                    requestedIds.Length);
        }
        catch
        {
            await ReloadScheduleAsync(requestedIds);
            ValidationMessage = AppResources.RemoveGamesNoOpMessage;
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
        return CanManageTeam() && !IsEditingTeamName && !IsRemovingPlayers
            && !IsRemovingGames && !isConfirmationPending;
    }

    private bool CanBeginRemovePlayers() => CanManageRoster() && HasPlayers;

    private bool CanAddGame() =>
        CanManageTeam() && !IsEditingTeamName && !IsRemovingPlayers
        && !IsRemovingGames && !isConfirmationPending && IsGamesSectionSelected;

    private bool CanBeginRemoveGames() =>
        CanManageTeam() && !IsEditingTeamName && !IsRemovingPlayers
        && !IsRemovingGames && !isConfirmationPending && IsGamesSectionSelected && HasGames;

    private bool CanCancelRemoveGames() =>
        CanManageTeam() && IsRemovingGames && !isConfirmationPending;

    private bool CanRemoveSelectedGames() =>
        CanCancelRemoveGames() && HasSelectedGames;

    private bool CanConfirmRemoveSelectedGames() =>
        CanManageTeam() && IsRemovingGames && isConfirmationPending && pendingGameIds.Count > 0;

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
        AddGameCommand.NotifyCanExecuteChanged();
        CancelRemovePlayersCommand.NotifyCanExecuteChanged();
        RemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
        ConfirmRemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
        RemoveGamesCommand.NotifyCanExecuteChanged();
        CancelRemoveGamesCommand.NotifyCanExecuteChanged();
        RemoveSelectedGamesCommand.NotifyCanExecuteChanged();
        ConfirmRemoveSelectedGamesCommand.NotifyCanExecuteChanged();
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
        hasInitialScheduleState = false;
        ScheduleMonthGroups.Clear();
        ValidationMessage = string.Empty;
        StatusMessage = string.Empty;
        IsEditingTeamName = false;
        ExitRemovePlayersMode();
        ExitRemoveGamesMode();
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

    private async Task ReloadScheduleAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var games = await gameRepository.GetForTeamAsync(TeamId);
        var seasons = await seasonRepository.GetForTeamAsync(TeamId);
        var gameIdsWithLineups = await gameLineupRepository.GetGameIdsWithLineupsAsync(
            games.Select(game => game.Id));
        var scoringSessions = await gameScoringSessionRepository.GetForGamesAsync(
            games.Select(game => game.Id));
        RebuildSchedule(
            games, seasons, gameIdsWithLineups, scoringSessions, idsToReselect);
        UpdateGameSelectionState();
    }

    private void OnGameSelectionChanged(GameScheduleItem item) => UpdateGameSelectionState();

    private void UpdateGameSelectionState() =>
        SelectedGameCount = ScheduleMonthGroups
            .SelectMany(group => group.Games)
            .Count(item => item.IsSelected);

    private void ClearGameSelection()
    {
        foreach (var item in ScheduleMonthGroups.SelectMany(group => group.Games))
        {
            item.IsSelected = false;
        }

        UpdateGameSelectionState();
    }

    private void ExitRemoveGamesMode()
    {
        ClearGameSelection();
        IsRemovingGames = false;
    }

    private void RebuildSchedule(
        IReadOnlyList<Game> games,
        IReadOnlyList<Season> seasons,
        IReadOnlySet<int> gameIdsWithLineups,
        IReadOnlyDictionary<int, GameScoringSession> scoringSessions,
        IReadOnlyCollection<int>? idsToReselect = null)
    {
        var selectedIds = idsToReselect?.ToHashSet();
        var previousExpansion = hasInitialScheduleState
            ? ScheduleMonthGroups.ToDictionary(
                group => (group.Year, group.Month),
                group => group.IsExpanded)
            : null;
        var orderedGames = games
            .OrderBy(game => game.ScheduledDate)
            .ThenBy(game => game.ScheduledTime)
            .ThenBy(game => game.Id)
            .ToArray();
        var seasonNames = seasons
            .GroupBy(season => season.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var today = DateTime.Today;
        var nextPlayingDate = orderedGames
            .Where(game => !scoringSessions.ContainsKey(game.Id))
            .Select(game => game.ScheduledDate.Date)
            .Where(date => date >= today)
            .Cast<DateTime?>()
            .FirstOrDefault();
        var initialExpandedMonth = nextPlayingDate
            ?? orderedGames.Select(game => (DateTime?)game.ScheduledDate.Date).LastOrDefault();
        var spansMultipleYears = orderedGames
            .Select(game => game.ScheduledDate.Year)
            .Distinct()
            .Skip(1)
            .Any();

        ScheduleMonthGroups.Clear();
        foreach (var monthGames in orderedGames.GroupBy(
                     game => (game.ScheduledDate.Year, game.ScheduledDate.Month)))
        {
            var key = monthGames.Key;
            var headerFormat = spansMultipleYears ? "MMM yyyy" : "MMM";
            var header = new DateTime(key.Year, key.Month, 1)
                .ToString(headerFormat, CultureInfo.CurrentCulture)
                .ToUpper(CultureInfo.CurrentCulture);
            var scheduleItems = monthGames
                .Select(game => CreateScheduleItem(
                    game,
                    seasonNames,
                    nextPlayingDate,
                    gameIdsWithLineups.Contains(game.Id),
                    GameScheduleSummary.FromSession(
                        scoringSessions.GetValueOrDefault(game.Id))))
                .ToArray();
            foreach (var item in scheduleItems)
            {
                item.IsSelected = selectedIds?.Contains(item.GameId) == true;
            }
            var isExpanded = previousExpansion is not null
                ? previousExpansion.GetValueOrDefault(key)
                : initialExpandedMonth is not null
                    && key.Year == initialExpandedMonth.Value.Year
                    && key.Month == initialExpandedMonth.Value.Month;

            ScheduleMonthGroups.Add(new GameScheduleMonthGroup(
                key.Year,
                key.Month,
                header,
                scheduleItems,
                isExpanded));
        }

        HasGames = ScheduleMonthGroups.Count > 0;
        hasInitialScheduleState = HasGames;
    }

    private GameScheduleItem CreateScheduleItem(
        Game game,
        IReadOnlyDictionary<int, string> seasonNames,
        DateTime? nextPlayingDate,
        bool hasLineup,
        GameScheduleSummary scoringSummary)
    {
        var isNextPlayingDate = nextPlayingDate is not null
            && game.ScheduledDate.Date == nextPlayingDate.Value;
        var opponent = string.IsNullOrWhiteSpace(game.Opponent)
            ? AppResources.ScheduleTbdText
            : game.Opponent.Trim();
        var field = string.IsNullOrWhiteSpace(game.Field)
            ? AppResources.ScheduleTbdText
            : game.Field.Trim();
        var season = seasonNames.TryGetValue(game.SeasonId, out var persistedSeasonName)
            && !string.IsNullOrWhiteSpace(persistedSeasonName)
                ? persistedSeasonName.Trim()
                : AppResources.ScheduleTbdText;

        return new GameScheduleItem(OnGameSelectionChanged, OpenGameAsync)
        {
            GameId = game.Id,
            ScheduledDate = game.ScheduledDate,
            NextDisplay = isNextPlayingDate ? AppResources.NextGameLabel : string.Empty,
            DateDisplay = game.ScheduledDate.ToString("MMM d", CultureInfo.CurrentCulture),
            OpponentDisplay = opponent,
            TimeDisplay = DateTime.Today
                .Add(game.ScheduledTime)
                .ToString("t", CultureInfo.CurrentCulture),
            FieldDisplay = field,
            SeasonDisplay = season,
            IsNextPlayingDate = isNextPlayingDate,
            HasLineup = hasLineup,
            LineupStatusText = isNextPlayingDate
                ? hasLineup
                    ? AppResources.LineupReadyStatusText
                    : AppResources.LineupNotSetStatusText
                : string.Empty,
            LineupStatusSemanticDescription = isNextPlayingDate
                ? hasLineup
                    ? AppResources.LineupReadySemanticDescription
                    : AppResources.LineupNotSetSemanticDescription
                : string.Empty,
            Status = scoringSummary.Status,
            StatusText = GetScheduleStatusText(scoringSummary.Status),
            Result = scoringSummary.Result,
            ResultText = GetTeamGameResultText(scoringSummary.Result),
            FinalScoreDisplay = scoringSummary.Status == GameScheduleStatus.Final
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.FinalGameScoreFormat,
                    scoringSummary.VisitorScore,
                    scoringSummary.HomeScore)
                : string.Empty,
            IsSelectionVisible = IsRemovingGames,
        };
    }

    private static string GetScheduleStatusText(GameScheduleStatus status) => status switch
    {
        GameScheduleStatus.Upcoming => AppResources.UpcomingStatus,
        GameScheduleStatus.InProgress => AppResources.InProgressStatus,
        GameScheduleStatus.Final => AppResources.FinalStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string GetTeamGameResultText(TeamGameResult? result) => result switch
    {
        TeamGameResult.Win => AppResources.WinResultLabel,
        TeamGameResult.Loss => AppResources.LossResultLabel,
        TeamGameResult.Tie => AppResources.TieResultLabel,
        null => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

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
