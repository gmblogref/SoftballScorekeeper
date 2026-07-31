using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class RemovePlayersFromTeamViewModel : ObservableObject, IQueryAttributable
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
    private bool hasRosterPlayers;

    [ObservableProperty]
    private bool hasNoRosterPlayers;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedPlayersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRemoveSelectedPlayersCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedPlayersCommand))]
    private bool isTeamLoaded;

    public RemovePlayersFromTeamViewModel(
        TeamRepository teamRepository,
        TeamPlayerRepository teamPlayerRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);
        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
    }

    public ObservableCollection<PlayerSelectionItem> Players { get; } = new();

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
        if (IsBusy || IsTeamLoaded)
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
            await ReloadRosterPlayersAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedPlayers))]
    private async Task RemoveSelectedPlayersAsync()
    {
        if (!CanRemoveSelectedPlayers())
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
        if (!CanConfirmRemoveSelectedPlayers())
        {
            return;
        }

        var requestedIds = pendingPlayerIds.ToArray();
        IsBusy = true;
        try
        {
            var result = await teamPlayerRepository.RemovePlayersFromTeamAsync(TeamId, requestedIds);
            if (result.SuccessfulRemovalCount == requestedIds.Length)
            {
                ValidationMessage = string.Empty;
                await Shell.Current.GoToAsync("..");
                return;
            }

            await ReloadRosterPlayersAsync(requestedIds);
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
            await ReloadRosterPlayersAsync(requestedIds);
            ValidationMessage = AppResources.RemovePlayersNoOpMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRemoveSelectedPlayers() =>
        !IsBusy && !isConfirmationPending && IsTeamLoaded && HasSelectedPlayers;

    private bool CanConfirmRemoveSelectedPlayers() =>
        !IsBusy && isConfirmationPending && IsTeamLoaded && pendingPlayerIds.Count > 0;

    private async Task ReloadRosterPlayersAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var rosterPlayers = await teamPlayerRepository.GetActivePlayersForTeamAsync(TeamId);
        var selectedIds = idsToReselect?.ToHashSet();

        Players.Clear();
        foreach (var player in rosterPlayers)
        {
            var item = new PlayerSelectionItem(player, OnSelectionChanged);
            if (selectedIds?.Contains(player.Id) == true)
            {
                item.IsSelected = true;
            }

            Players.Add(item);
        }

        HasRosterPlayers = Players.Count > 0;
        HasNoRosterPlayers = !HasRosterPlayers;
        UpdateSelectionState();
    }

    private void OnSelectionChanged(PlayerSelectionItem item) => UpdateSelectionState();

    private void UpdateSelectionState() =>
        SelectedPlayerCount = Players.Count(item => item.IsSelected);

    private void NotifyCommandStates()
    {
        RemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
        ConfirmRemoveSelectedPlayersCommand.NotifyCanExecuteChanged();
    }

    private void ResetState()
    {
        TeamId = 0;
        hasValidTeamId = false;
        TeamName = string.Empty;
        Players.Clear();
        HasRosterPlayers = false;
        HasNoRosterPlayers = false;
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
