using System.Collections.ObjectModel;
using System.Globalization;
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
    private bool isConfirmationPending;
    private IReadOnlyList<int> pendingRemovalIds = Array.Empty<int>();
    private int selectedPlayerCount;
    private int selectedRemovalCount;
    [ObservableProperty] private int teamId;
    [ObservableProperty] private int gameId;
    [ObservableProperty] private bool hasPlayers;
    [ObservableProperty] private bool hasNoPlayers;
    [ObservableProperty] private bool hasExistingLineup;
    [ObservableProperty] private bool isRemovalMode;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private bool isBusy;

    public SetLineupViewModel(GameLineupRepository repository) => this.repository = repository;
    public ObservableCollection<PlayerSelectionItem> Players { get; } = [];
    public ObservableCollection<LineupPlayerSelectionItem> ExistingLineupPlayers { get; } = [];
    public Func<int, Task>? RemovalConfirmationRequested { get; set; }
    public bool IsNormalMode => !IsRemovalMode;
    public bool HasSelectedRemovals => SelectedRemovalCount > 0;
    public string SelectedRemovalCountText => string.Format(
        CultureInfo.CurrentCulture,
        SelectedRemovalCount == 1
            ? AppResources.LineupRemovalSelectedCountSingular
            : AppResources.LineupRemovalSelectedCountPlural,
        SelectedRemovalCount);
    public int SelectedPlayerCount { get => selectedPlayerCount; private set { if (SetProperty(ref selectedPlayerCount, value)) NotifyCommands(); } }
    public int SelectedRemovalCount { get => selectedRemovalCount; private set { if (SetProperty(ref selectedRemovalCount, value)) { OnPropertyChanged(nameof(HasSelectedRemovals)); OnPropertyChanged(nameof(SelectedRemovalCountText)); NotifyCommands(); } } }

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
        try { await ReloadAsync(); }
        finally { IsBusy = false; }
    }

    private async Task ReloadAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var existingRows = await repository.GetForGameAsync(GameId);
        var existingPlayers = await repository.GetPlayersForGameAsync(GameId);
        HasExistingLineup = existingRows.Count > 0;
        Players.Clear(); ExistingLineupPlayers.Clear(); selectionOrder.Clear();
        for (var index = 0; index < existingPlayers.Count; index++)
        {
            var item = new LineupPlayerSelectionItem(existingRows[index], existingPlayers[index], RemovalSelectionChanged)
            {
                IsSelectionVisible = IsRemovalMode,
            };
            if (idsToReselect?.Contains(item.Player.Id) == true) item.IsSelected = true;
            ExistingLineupPlayers.Add(item);
        }
        var existingIds = existingRows.Select(item => item.PlayerId).ToHashSet();
        foreach (var player in await repository.GetEligiblePlayersAsync(TeamId, GameId))
            if (!existingIds.Contains(player.Id)) Players.Add(new PlayerSelectionItem(player, AddSelectionChanged));
        HasPlayers = Players.Count > 0;
        HasNoPlayers = !HasPlayers;
        SelectedPlayerCount = 0;
        UpdateRemovalSelection();
        if (!HasExistingLineup) ExitRemovalMode();
    }

    private void AddSelectionChanged(PlayerSelectionItem item)
    {
        selectionOrder.Remove(item.Player.Id);
        if (item.IsSelected) selectionOrder.Add(item.Player.Id);
        SelectedPlayerCount = selectionOrder.Count;
    }

    private void RemovalSelectionChanged(LineupPlayerSelectionItem item) => UpdateRemovalSelection();
    private void UpdateRemovalSelection() => SelectedRemovalCount = ExistingLineupPlayers.Count(item => item.IsSelected);

    [RelayCommand(CanExecute = nameof(CanEnterRemovalMode))]
    private void EnterRemovalMode()
    {
        ClearAddSelection(); ClearRemovalSelection(); ValidationMessage = string.Empty; IsRemovalMode = true;
    }

    [RelayCommand(CanExecute = nameof(CanCancelRemovalMode))]
    private void CancelRemovalMode() => ExitRemovalMode();

    public bool TryExitRemovalMode()
    {
        if (!IsRemovalMode || isConfirmationPending || IsBusy) return false;
        ExitRemovalMode();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanRequestRemoval))]
    private async Task RequestRemovalAsync()
    {
        pendingRemovalIds = ExistingLineupPlayers.Where(item => item.IsSelected)
            .Select(item => item.Player.Id).Distinct().ToArray();
        if (pendingRemovalIds.Count == 0) return;
        isConfirmationPending = true; NotifyCommands();
        try { if (RemovalConfirmationRequested is not null) await RemovalConfirmationRequested(pendingRemovalIds.Count); }
        finally { pendingRemovalIds = Array.Empty<int>(); isConfirmationPending = false; NotifyCommands(); }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmRemoval))]
    private async Task ConfirmRemovalAsync()
    {
        var requestedIds = pendingRemovalIds.ToArray();
        IsBusy = true;
        try
        {
            var result = await repository.RemovePlayersAsync(GameId, requestedIds);
            await ReloadAsync(result.SuccessfulRemovalCount == requestedIds.Length ? null : requestedIds);
            if (result.SuccessfulRemovalCount == requestedIds.Length)
            {
                ValidationMessage = string.Empty;
                ExitRemovalMode();
            }
            else
            {
                ValidationMessage = result.SuccessfulRemovalCount == 0
                    ? AppResources.LineupRemovalNoOpMessage
                    : string.Format(CultureInfo.CurrentCulture, AppResources.LineupRemovalPartialMessage, result.SuccessfulRemovalCount, requestedIds.Length);
            }
        }
        catch
        {
            await ReloadAsync(requestedIds);
            ValidationMessage = AppResources.LineupRemovalNoOpMessage;
        }
        finally { IsBusy = false; }
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
    private bool CanSave() => !IsBusy && !IsRemovalMode && !isConfirmationPending && hasContext && SelectedPlayerCount > 0;
    private bool CanEnterRemovalMode() => !IsBusy && !IsRemovalMode && !isConfirmationPending && HasExistingLineup;
    private bool CanCancelRemovalMode() => !IsBusy && IsRemovalMode && !isConfirmationPending;
    private bool CanRequestRemoval() => CanCancelRemovalMode() && HasSelectedRemovals;
    private bool CanConfirmRemoval() => !IsBusy && IsRemovalMode && isConfirmationPending && pendingRemovalIds.Count > 0;
    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnIsRemovalModeChanged(bool value)
    {
        foreach (var item in ExistingLineupPlayers) item.IsSelectionVisible = value;
        OnPropertyChanged(nameof(IsNormalMode));
        NotifyCommands();
    }
    partial void OnHasExistingLineupChanged(bool value) => NotifyCommands();
    private void NotifyCommands() { SaveCommand.NotifyCanExecuteChanged(); EnterRemovalModeCommand.NotifyCanExecuteChanged(); CancelRemovalModeCommand.NotifyCanExecuteChanged(); RequestRemovalCommand.NotifyCanExecuteChanged(); ConfirmRemovalCommand.NotifyCanExecuteChanged(); }
    private void ClearAddSelection() { foreach (var item in Players) item.IsSelected = false; selectionOrder.Clear(); SelectedPlayerCount = 0; }
    private void ClearRemovalSelection() { foreach (var item in ExistingLineupPlayers) item.IsSelected = false; UpdateRemovalSelection(); }
    private void ExitRemovalMode() { ClearRemovalSelection(); IsRemovalMode = false; }
    private void Reset() { TeamId = GameId = 0; hasContext = false; Players.Clear(); ExistingLineupPlayers.Clear(); selectionOrder.Clear(); SelectedPlayerCount = 0; SelectedRemovalCount = 0; IsRemovalMode = false; }
    private static bool TryId(IDictionary<string, object> query, string key, out int id)
    { id = 0; return query.TryGetValue(key, out var value) && (value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0); }
}
