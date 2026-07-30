using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class InactivePlayersViewModel : ObservableObject
{
    private readonly PlayerRepository playerRepository;
    private IReadOnlyList<int> pendingPlayerIds = Array.Empty<int>();
    private bool isConfirmationPending;
    private bool hasInactivePlayers;
    private bool hasNoInactivePlayers = true;
    private bool isBusy;
    private int selectedPlayerCount;
    private string validationMessage = string.Empty;

    public InactivePlayersViewModel(PlayerRepository playerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);
        this.playerRepository = playerRepository;
    }

    public ObservableCollection<PlayerSelectionItem> InactivePlayers { get; } = new();

    public Func<int, Task>? ReactivatePlayersConfirmationRequested { get; set; }

    public bool HasInactivePlayers
    {
        get => hasInactivePlayers;
        private set => SetProperty(ref hasInactivePlayers, value);
    }

    public bool HasNoInactivePlayers
    {
        get => hasNoInactivePlayers;
        private set => SetProperty(ref hasNoInactivePlayers, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                ReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
                ConfirmReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int SelectedPlayerCount
    {
        get => selectedPlayerCount;
        private set
        {
            if (SetProperty(ref selectedPlayerCount, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlayers));
                ReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
                ConfirmReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPlayers => SelectedPlayerCount > 0;

    public string ValidationMessage
    {
        get => validationMessage;
        private set => SetProperty(ref validationMessage, value);
    }

    public async Task LoadInactivePlayersAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            ValidationMessage = string.Empty;
            await ReloadInactivePlayersAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReactivateSelectedPlayers))]
    private async Task ReactivateSelectedPlayersAsync()
    {
        if (!CanReactivateSelectedPlayers())
        {
            return;
        }

        pendingPlayerIds = InactivePlayers
            .Where(item => item.IsSelected && item.Player is { Id: > 0, IsActive: false })
            .Select(item => item.Player.Id)
            .Distinct()
            .ToArray();

        if (pendingPlayerIds.Count == 0)
        {
            UpdateSelectionState();
            return;
        }

        isConfirmationPending = true;
        ReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
        try
        {
            var confirmationRequested = ReactivatePlayersConfirmationRequested;
            if (confirmationRequested is not null)
            {
                await confirmationRequested(pendingPlayerIds.Count);
            }
        }
        finally
        {
            pendingPlayerIds = Array.Empty<int>();
            isConfirmationPending = false;
            ReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
            ConfirmReactivateSelectedPlayersCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmReactivateSelectedPlayers))]
    private async Task ConfirmReactivateSelectedPlayersAsync()
    {
        if (!CanConfirmReactivateSelectedPlayers())
        {
            return;
        }

        var selectedIds = pendingPlayerIds.ToArray();
        IsBusy = true;
        try
        {
            var affectedRows = await playerRepository.ReactivateAsync(selectedIds);
            var idsToReselect = affectedRows == selectedIds.Length
                ? Array.Empty<int>()
                : selectedIds;

            await ReloadInactivePlayersAsync(idsToReselect);
            ValidationMessage = affectedRows == selectedIds.Length
                ? string.Empty
                : string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.PlayerReactivationPartialFailureMessage,
                    affectedRows,
                    selectedIds.Length);

            if (!HasInactivePlayers)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanReactivateSelectedPlayers()
    {
        return !IsBusy && !isConfirmationPending && HasSelectedPlayers;
    }

    private bool CanConfirmReactivateSelectedPlayers()
    {
        return !IsBusy && isConfirmationPending && pendingPlayerIds.Count > 0;
    }

    private async Task ReloadInactivePlayersAsync(
        IReadOnlyCollection<int>? playerIdsToReselect = null)
    {
        var players = await playerRepository.GetInactiveAsync();
        var selectedIds = playerIdsToReselect is null
            ? null
            : playerIdsToReselect.ToHashSet();

        InactivePlayers.Clear();
        foreach (var player in players)
        {
            var item = new PlayerSelectionItem(player, OnPlayerSelectionChanged);
            if (selectedIds?.Contains(player.Id) == true)
            {
                item.IsSelected = true;
            }

            InactivePlayers.Add(item);
        }

        HasInactivePlayers = InactivePlayers.Count > 0;
        HasNoInactivePlayers = !HasInactivePlayers;
        UpdateSelectionState();
    }

    private void OnPlayerSelectionChanged(PlayerSelectionItem item)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedPlayerCount = InactivePlayers.Count(item => item.IsSelected);
    }
}
