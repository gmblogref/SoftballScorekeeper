using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class AddPlayersToTeamViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly TeamPlayerRepository teamPlayerRepository;
    private IReadOnlyList<int> pendingPlayerIds = Array.Empty<int>();
    private bool hasValidTeamId;
    private bool isConfirmationPending;
    private int selectedPlayerCount;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private bool hasEligiblePlayers;

    [ObservableProperty]
    private bool hasNoEligiblePlayers;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmAddSelectedPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreatePlayerCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreatePlayerCommand))]
    private bool isTeamLoaded;

    public AddPlayersToTeamViewModel(
        TeamRepository teamRepository,
        TeamPlayerRepository teamPlayerRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);
        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
    }

    public ObservableCollection<PlayerSelectionItem> Players { get; } = new();

    public Func<int, string, Task>? AddPlayersConfirmationRequested { get; set; }

    public int SelectedPlayerCount
    {
        get => selectedPlayerCount;
        private set
        {
            if (SetProperty(ref selectedPlayerCount, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlayers));
                AddSelectedPlayersCommand.NotifyCanExecuteChanged();
                ConfirmAddSelectedPlayersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPlayers => SelectedPlayerCount > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value)
            || !TryParseTeamId(value, out var parsedTeamId))
        {
            ResetState();
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        if (hasValidTeamId && TeamId == parsedTeamId)
        {
            return;
        }

        ResetState();
        TeamId = parsedTeamId;
        hasValidTeamId = true;
    }

    public async Task LoadAsync()
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

            TeamName = team.Name;
            IsTeamLoaded = true;
            ValidationMessage = string.Empty;
            await ReloadEligiblePlayersAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedPlayers))]
    private async Task AddSelectedPlayersAsync()
    {
        if (!CanAddSelectedPlayers())
        {
            return;
        }

        pendingPlayerIds = Players
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
            var confirmationRequested = AddPlayersConfirmationRequested;
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

    [RelayCommand(CanExecute = nameof(CanCreatePlayer))]
    private async Task CreatePlayerAsync()
    {
        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId
        };

        await Shell.Current.GoToAsync(AppShell.CreatePlayerRoute, parameters);
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var player in Players) player.IsSelected = true;
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var player in Players) player.IsSelected = false;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmAddSelectedPlayers))]
    private async Task ConfirmAddSelectedPlayersAsync()
    {
        if (!CanConfirmAddSelectedPlayers())
        {
            return;
        }

        var requestedIds = pendingPlayerIds.ToArray();
        IsBusy = true;
        try
        {
            var result = await teamPlayerRepository.AddPlayersToTeamAsync(TeamId, requestedIds);
            if (result.SuccessfullyActiveMembershipCount == requestedIds.Length)
            {
                ValidationMessage = string.Empty;
                await Shell.Current.GoToAsync("..");
                return;
            }

            await ReloadEligiblePlayersAsync(requestedIds);
            ValidationMessage = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.AddPlayersPartialResultMessage,
                result.SuccessfullyActiveMembershipCount,
                requestedIds.Length);
        }
        catch
        {
            await ReloadEligiblePlayersAsync(requestedIds);
            ValidationMessage = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.AddPlayersPartialResultMessage,
                0,
                requestedIds.Length);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanAddSelectedPlayers()
    {
        return !IsBusy && !isConfirmationPending && IsTeamLoaded && HasSelectedPlayers;
    }

    private bool CanCreatePlayer()
    {
        return !IsBusy && !isConfirmationPending && IsTeamLoaded;
    }

    private bool CanConfirmAddSelectedPlayers()
    {
        return !IsBusy && isConfirmationPending && IsTeamLoaded && pendingPlayerIds.Count > 0;
    }

    private async Task ReloadEligiblePlayersAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var eligiblePlayers = await teamPlayerRepository.GetEligiblePlayersForTeamAsync(TeamId);
        var selectedIds = idsToReselect?.ToHashSet();

        Players.Clear();
        foreach (var player in eligiblePlayers)
        {
            var item = new PlayerSelectionItem(player, OnSelectionChanged);
            if (selectedIds?.Contains(player.Id) == true)
            {
                item.IsSelected = true;
            }

            Players.Add(item);
        }

        HasEligiblePlayers = Players.Count > 0;
        HasNoEligiblePlayers = !HasEligiblePlayers;
        UpdateSelectionState();
    }

    private void OnSelectionChanged(PlayerSelectionItem item)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedPlayerCount = Players.Count(item => item.IsSelected);
    }

    private void NotifyCommandStates()
    {
        AddSelectedPlayersCommand.NotifyCanExecuteChanged();
        ConfirmAddSelectedPlayersCommand.NotifyCanExecuteChanged();
        CreatePlayerCommand.NotifyCanExecuteChanged();
    }

    private void ResetState()
    {
        TeamId = 0;
        hasValidTeamId = false;
        TeamName = string.Empty;
        Players.Clear();
        HasEligiblePlayers = false;
        HasNoEligiblePlayers = false;
        ValidationMessage = string.Empty;
        IsTeamLoaded = false;
        UpdateSelectionState();
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
