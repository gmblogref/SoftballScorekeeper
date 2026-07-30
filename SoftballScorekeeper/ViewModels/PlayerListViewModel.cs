using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerListViewModel : ObservableObject
{
    private readonly PlayerRepository playerRepository;
    private IReadOnlyList<int> pendingPlayerIds = Array.Empty<int>();
    private bool isConfirmationPending;
    private int selectedPlayerCount;

    [ObservableProperty]
    private bool hasPlayers;

    [ObservableProperty]
    private bool hasNoPlayers = true;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? statusMessage;

    public PlayerListViewModel(PlayerRepository playerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);

        this.playerRepository = playerRepository;
    }

    public ObservableCollection<PlayerSelectionItem> Players { get; } = new();

    public Func<int, Task>? DeactivatePlayersConfirmationRequested { get; set; }

    public int SelectedPlayerCount
    {
        get => selectedPlayerCount;
        private set
        {
            if (SetProperty(ref selectedPlayerCount, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlayers));
                DeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
                ConfirmDeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPlayers => SelectedPlayerCount > 0;

    public async Task LoadPlayersAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await ReloadPlayersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddPlayerAsync()
    {
        await Shell.Current.GoToAsync(AppShell.CreatePlayerRoute);
    }

    [RelayCommand]
    private async Task EditPlayerAsync(int playerId)
    {
        if (playerId <= 0)
        {
            return;
        }

        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.PlayerIdQueryParameter] = playerId
        };

        await Shell.Current.GoToAsync(AppShell.EditPlayerRoute, parameters);
    }

    [RelayCommand(CanExecute = nameof(CanDeactivateSelectedPlayers))]
    private async Task DeactivateSelectedPlayersAsync()
    {
        if (!CanDeactivateSelectedPlayers())
        {
            return;
        }

        pendingPlayerIds = Players
            .Where(item => item.IsSelected && item.Player is { Id: > 0, IsActive: true })
            .Select(item => item.Player.Id)
            .Distinct()
            .ToArray();

        if (pendingPlayerIds.Count == 0)
        {
            UpdateSelectionState();
            return;
        }

        isConfirmationPending = true;
        DeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
        try
        {
            var confirmationRequested = DeactivatePlayersConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(pendingPlayerIds.Count);
            }
        }
        finally
        {
            pendingPlayerIds = Array.Empty<int>();
            isConfirmationPending = false;
            DeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
            ConfirmDeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDeactivateSelectedPlayers))]
    private async Task ConfirmDeactivateSelectedPlayersAsync()
    {
        if (!CanConfirmDeactivateSelectedPlayers())
        {
            return;
        }

        var selectedIds = pendingPlayerIds.ToArray();
        IsLoading = true;
        try
        {
            var affectedRows = await playerRepository.DeactivateAsync(selectedIds);
            var idsToReselect = affectedRows == selectedIds.Length
                ? Array.Empty<int>()
                : selectedIds;

            await ReloadPlayersAsync(idsToReselect);
            StatusMessage = affectedRows == selectedIds.Length
                ? string.Empty
                : string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.PlayerDeactivationPartialFailureMessage,
                    affectedRows,
                    selectedIds.Length);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ManageInactivePlayers()
    {
        ShowPlaceholderMessage();
    }

    private void ShowPlaceholderMessage()
    {
        StatusMessage = AppResources.PlayerManagementPlaceholderMessage;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        DeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
        ConfirmDeactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeactivateSelectedPlayers()
    {
        return !IsLoading && !isConfirmationPending && HasSelectedPlayers;
    }

    private bool CanConfirmDeactivateSelectedPlayers()
    {
        return !IsLoading && isConfirmationPending && pendingPlayerIds.Count > 0;
    }

    private async Task ReloadPlayersAsync(IReadOnlyCollection<int>? playerIdsToReselect = null)
    {
        var players = await playerRepository.GetActiveAsync();
        var selectedIds = playerIdsToReselect is null
            ? null
            : playerIdsToReselect.ToHashSet();

        Players.Clear();
        foreach (var player in players)
        {
            var item = new PlayerSelectionItem(player, OnPlayerSelectionChanged);
            if (selectedIds?.Contains(player.Id) == true)
            {
                item.IsSelected = true;
            }

            Players.Add(item);
        }

        HasPlayers = Players.Count > 0;
        HasNoPlayers = !HasPlayers;
        UpdateSelectionState();
    }

    private void OnPlayerSelectionChanged(PlayerSelectionItem item)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedPlayerCount = Players.Count(item => item.IsSelected);
    }
}
