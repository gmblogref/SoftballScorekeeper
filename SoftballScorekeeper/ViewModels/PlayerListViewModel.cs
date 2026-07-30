using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class PlayerListViewModel : ObservableObject
{
    private readonly PlayerRepository playerRepository;

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

    public async Task LoadPlayersAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var players = await playerRepository.GetActiveAsync();

            Players.Clear();
            foreach (var player in players)
            {
                Players.Add(new PlayerSelectionItem(player));
            }

            HasPlayers = Players.Count > 0;
            HasNoPlayers = !HasPlayers;
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
    private void EditPlayer()
    {
        ShowPlaceholderMessage();
    }

    [RelayCommand]
    private void DeactivateSelectedPlayers()
    {
        ShowPlaceholderMessage();
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
}
