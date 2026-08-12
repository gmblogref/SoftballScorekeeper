using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class SetLineupViewModel : ObservableObject, IQueryAttributable
{
    private readonly GameLineupRepository repository;
    private readonly List<int> selectionOrder = [];
    private bool hasContext;
    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private bool hasPlayers;
    [ObservableProperty] private bool hasNoPlayers;
    [ObservableProperty] private bool hasExistingLineup;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))] private bool isBusy;
    private int selectedPlayerCount;

    public SetLineupViewModel(GameLineupRepository repository) => this.repository = repository;
    public ObservableCollection<PlayerSelectionItem> Players { get; } = [];
    public ObservableCollection<string> ExistingLineupPlayers { get; } = [];
    public int SelectedPlayerCount { get => selectedPlayerCount; private set { if (SetProperty(ref selectedPlayerCount, value)) SaveCommand.NotifyCanExecuteChanged(); } }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!TryId(query, AppShell.TeamIdQueryParameter, out var teamId) || !TryId(query, AppShell.GameIdQueryParameter, out var gameId))
        { Reset(); ValidationMessage = AppResources.GameNotFound; return; }
        TeamId = teamId; GameId = gameId; hasContext = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasContext) return;
        IsBusy = true;
        try
        {
            var existingRows = await repository.GetForGameAsync(GameId);
            var existingPlayers = await repository.GetPlayersForGameAsync(GameId);
            HasExistingLineup = existingRows.Count > 0;
            Players.Clear(); ExistingLineupPlayers.Clear(); selectionOrder.Clear();
            for (var index = 0; index < existingPlayers.Count; index++)
                ExistingLineupPlayers.Add(string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    AppResources.LineupPlayerFormat,
                    existingRows[index].BattingOrder,
                    string.Join(" ", new[] { existingPlayers[index].FirstName, existingPlayers[index].LastName }
                        .Where(value => !string.IsNullOrWhiteSpace(value)))));
            var existingIds = existingRows.Select(item => item.PlayerId).ToHashSet();
            foreach (var player in await repository.GetEligiblePlayersAsync(TeamId, GameId))
                if (!existingIds.Contains(player.Id))
                    Players.Add(new PlayerSelectionItem(player, SelectionChanged));
            HasPlayers = Players.Count > 0;
            HasNoPlayers = !HasPlayers;
            SelectedPlayerCount = 0;
        }
        finally { IsBusy = false; }
    }

    private void SelectionChanged(PlayerSelectionItem item)
    {
        selectionOrder.Remove(item.Player.Id);
        if (item.IsSelected) selectionOrder.Add(item.Player.Id);
        SelectedPlayerCount = selectionOrder.Count;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var result = await repository.AppendAsync(TeamId, GameId, selectionOrder);
            if (result.IsSuccess) { await Shell.Current.GoToAsync(".."); return; }
            ValidationMessage = result.FailureReason switch
            {
                SaveGameLineupFailure.EmptySelection => AppResources.LineupSelectionRequired,
                SaveGameLineupFailure.IneligiblePlayers => AppResources.LineupPlayersIneligible,
                _ => AppResources.GameNotFound,
            };
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task CancelAsync() => Shell.Current.GoToAsync("..");
    private bool CanSave() => !IsBusy && hasContext && SelectedPlayerCount > 0;
    private void Reset() { TeamId = GameId = 0; hasContext = false; Players.Clear(); ExistingLineupPlayers.Clear(); selectionOrder.Clear(); SelectedPlayerCount = 0; }
    private static bool TryId(IDictionary<string, object> query, string key, out int id)
    { id = 0; return query.TryGetValue(key, out var value) && (value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0); }
}
