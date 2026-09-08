using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public enum LineupManagerMode
{
    Normal,
    Add,
    Remove,
}

public partial class SetLineupViewModel : ObservableObject, IQueryAttributable
{
    private readonly GameLineupRepository repository;
    private readonly List<int> selectionOrder = [];
    private bool canUsePreviousLineup;
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
    [ObservableProperty] private LineupManagerMode mode;
    [ObservableProperty] private string validationMessage = string.Empty;
    [ObservableProperty] private bool isBusy;

    public SetLineupViewModel(GameLineupRepository repository) => this.repository = repository;
    public ObservableCollection<PlayerSelectionItem> Players { get; } = [];
    public ObservableCollection<LineupPlayerSelectionItem> ExistingLineupPlayers { get; } = [];
    public Func<int, Task>? RemovalConfirmationRequested { get; set; }
    public Func<Task<bool>>? PreviousLineupReplacementConfirmationRequested { get; set; }
    public bool IsNormalMode => Mode == LineupManagerMode.Normal;
    public bool IsAddMode => Mode == LineupManagerMode.Add;
    public bool IsRemovalMode => Mode == LineupManagerMode.Remove;
    public bool IsCurrentLineupVisible => !IsAddMode;
    public bool CanReorderLineup => IsNormalMode && !IsBusy && ExistingLineupPlayers.Count > 1;
    public bool HasEmptyLineup => !HasExistingLineup;
    public bool ShowNoEligiblePlayersMessage => IsAddMode && HasNoPlayers && !HasExistingLineup;
    public bool ShowNoAdditionalPlayersMessage => IsAddMode && HasNoPlayers && HasExistingLineup;
    public bool HasSelectedRemovals => SelectedRemovalCount > 0;
    public bool CanUsePreviousLineup
    {
        get => canUsePreviousLineup;
        private set
        {
            if (SetProperty(ref canUsePreviousLineup, value)) NotifyCommands();
        }
    }
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
        var hasTeamId = query.ContainsKey(AppShell.TeamIdQueryParameter);
        var hasGameId = query.ContainsKey(AppShell.GameIdQueryParameter);
        if (!hasTeamId && !hasGameId)
        {
            if (!hasContext) ValidationMessage = AppResources.GameNotFound;
            return;
        }

        if (!TryId(query, AppShell.TeamIdQueryParameter, out var teamId) || !TryId(query, AppShell.GameIdQueryParameter, out var gameId))
        { Reset(); ValidationMessage = AppResources.GameNotFound; return; }
        TeamId = teamId; GameId = gameId; hasContext = true; CanUsePreviousLineup = false;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || !hasContext) return;
        CanUsePreviousLineup = false;
        IsBusy = true;
        try { ExitTemporaryMode(); await ReloadAsync(); }
        finally { IsBusy = false; }
    }

    private async Task ReloadAsync(IReadOnlyCollection<int>? idsToReselect = null)
    {
        var existingRows = await repository.GetForGameAsync(GameId);
        var existingPlayers = await repository.GetPlayersForGameAsync(GameId);
        HasExistingLineup = existingRows.Count > 0;
        CanUsePreviousLineup = await repository.CanUsePreviousAsync(TeamId, GameId);
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
        OnPropertyChanged(nameof(CanReorderLineup));
        if (!HasExistingLineup && IsRemovalMode) ExitTemporaryMode();
    }

    private void AddSelectionChanged(PlayerSelectionItem item)
    {
        selectionOrder.Remove(item.Player.Id);
        if (item.IsSelected) selectionOrder.Add(item.Player.Id);
        SelectedPlayerCount = selectionOrder.Count;
    }

    private void RemovalSelectionChanged(LineupPlayerSelectionItem item) => UpdateRemovalSelection();
    private void UpdateRemovalSelection() => SelectedRemovalCount = ExistingLineupPlayers.Count(item => item.IsSelected);

    [RelayCommand(CanExecute = nameof(CanEnterAddMode))]
    private void EnterAddMode() { ClearSelections(); ValidationMessage = string.Empty; Mode = LineupManagerMode.Add; }

    [RelayCommand(CanExecute = nameof(CanEnterRemovalMode))]
    private void EnterRemovalMode() { ClearSelections(); ValidationMessage = string.Empty; Mode = LineupManagerMode.Remove; }

    [RelayCommand(CanExecute = nameof(CanCancelTemporaryMode))]
    private void CancelTemporaryMode() => ExitTemporaryMode();

    public bool TryExitTemporaryMode()
    {
        if (IsNormalMode || isConfirmationPending || IsBusy) return false;
        ExitTemporaryMode();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanRequestRemoval))]
    private async Task RequestRemovalAsync()
    {
        pendingRemovalIds = ExistingLineupPlayers.Where(item => item.IsSelected).Select(item => item.Player.Id).Distinct().ToArray();
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
            if (result.SuccessfulRemovalCount == requestedIds.Length) { ValidationMessage = string.Empty; ExitTemporaryMode(); }
            else ValidationMessage = result.SuccessfulRemovalCount == 0
                ? AppResources.LineupRemovalNoOpMessage
                : string.Format(CultureInfo.CurrentCulture, AppResources.LineupRemovalPartialMessage, result.SuccessfulRemovalCount, requestedIds.Length);
        }
        catch { await ReloadAsync(requestedIds); ValidationMessage = AppResources.LineupRemovalNoOpMessage; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelected))]
    private async Task AddSelectedAsync()
    {
        IsBusy = true;
        try
        {
            var result = await repository.AppendAsync(TeamId, GameId, selectionOrder);
            if (result.IsSuccess) { ExitTemporaryMode(); await ReloadAsync(); ValidationMessage = string.Empty; return; }
            ValidationMessage = result.FailureReason switch
            {
                SaveGameLineupFailure.EmptySelection => AppResources.LineupSelectionRequired,
                SaveGameLineupFailure.IneligiblePlayers => AppResources.LineupPlayersIneligible,
                _ => AppResources.GameNotFound,
            };
        }
        finally { IsBusy = false; }
    }

    public async Task ReorderPlayerAsync(int sourcePlayerId, int targetPlayerId)
    {
        if (!CanReorderLineup || sourcePlayerId == targetPlayerId) return;

        var orderedIds = ExistingLineupPlayers.Select(player => player.Player.Id).ToList();
        var sourceIndex = orderedIds.IndexOf(sourcePlayerId);
        var targetIndex = orderedIds.IndexOf(targetPlayerId);
        if (sourceIndex < 0 || targetIndex < 0) return;
        orderedIds.RemoveAt(sourceIndex);
        orderedIds.Insert(targetIndex, sourcePlayerId);

        IsBusy = true;
        try
        {
            var result = await repository.ReorderAsync(GameId, orderedIds);
            await ReloadAsync();
            ValidationMessage = result.IsSuccess
                ? string.Empty
                : AppResources.LineupReorderFailed;
        }
        catch
        {
            await ReloadAsync();
            ValidationMessage = AppResources.LineupReorderFailed;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUsePreviousLineup))]
    private async Task UsePreviousLineupAsync()
    {
        if (HasExistingLineup)
        {
            isConfirmationPending = true;
            NotifyCommands();
            try
            {
                if (PreviousLineupReplacementConfirmationRequested is null
                    || !await PreviousLineupReplacementConfirmationRequested())
                    return;
            }
            finally
            {
                isConfirmationPending = false;
                NotifyCommands();
            }
        }

        IsBusy = true;
        try
        {
            var result = await repository.UsePreviousAsync(TeamId, GameId);
            await ReloadAsync();
            ValidationMessage = result.FailureReason switch
            {
                UsePreviousLineupFailure.None when result.SkippedPlayerCount == 1 =>
                    string.Format(CultureInfo.CurrentCulture, AppResources.PreviousLineupCopiedWithSkippedPlayer, 1),
                UsePreviousLineupFailure.None when result.SkippedPlayerCount > 1 =>
                    string.Format(CultureInfo.CurrentCulture, AppResources.PreviousLineupCopiedWithSkippedPlayers, result.SkippedPlayerCount),
                UsePreviousLineupFailure.None => string.Empty,
                UsePreviousLineupFailure.NoPreviousLineup => AppResources.NoPreviousLineupMessage,
                UsePreviousLineupFailure.NoEligiblePlayers => AppResources.NoEligiblePreviousLineupPlayersMessage,
                _ => AppResources.PreviousLineupFailed,
            };
        }
        catch
        {
            await ReloadAsync();
            ValidationMessage = AppResources.PreviousLineupFailed;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanCreatePlayer))]
    private Task CreatePlayerAsync() => Shell.Current.GoToAsync(
        AppShell.CreatePlayerRoute,
        new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = TeamId,
            [AppShell.GameIdQueryParameter] = GameId,
        });

    [RelayCommand(CanExecute = nameof(CanDone))]
    private async Task DoneAsync() { ExitTemporaryMode(); await Shell.Current.GoToAsync(".."); }

    private bool CanAddSelected() => !IsBusy && IsAddMode && !isConfirmationPending && hasContext && SelectedPlayerCount > 0;
    private bool CanCreatePlayer() => !IsBusy && IsAddMode && !isConfirmationPending && hasContext;
    private bool CanEnterAddMode() => !IsBusy && IsNormalMode && !isConfirmationPending;
    private bool CanEnterRemovalMode() => !IsBusy && IsNormalMode && !isConfirmationPending && HasExistingLineup;
    private bool CanCancelTemporaryMode() => !IsBusy && !IsNormalMode && !isConfirmationPending;
    private bool CanRequestRemoval() => !IsBusy && IsRemovalMode && !isConfirmationPending && HasSelectedRemovals;
    private bool CanExecuteUsePreviousLineup() => !IsBusy && IsNormalMode
        && !isConfirmationPending && hasContext && CanUsePreviousLineup;
    private bool CanConfirmRemoval() => !IsBusy && IsRemovalMode && isConfirmationPending && pendingRemovalIds.Count > 0;
    private bool CanDone() => !IsBusy && IsNormalMode && !isConfirmationPending;
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanReorderLineup)); NotifyCommands(); }
    partial void OnModeChanged(LineupManagerMode value)
    {
        foreach (var item in ExistingLineupPlayers) item.IsSelectionVisible = IsRemovalMode;
        OnPropertyChanged(nameof(IsNormalMode)); OnPropertyChanged(nameof(IsAddMode)); OnPropertyChanged(nameof(IsRemovalMode)); OnPropertyChanged(nameof(IsCurrentLineupVisible)); OnPropertyChanged(nameof(CanReorderLineup)); OnPropertyChanged(nameof(ShowNoEligiblePlayersMessage)); OnPropertyChanged(nameof(ShowNoAdditionalPlayersMessage));
        NotifyCommands();
    }
    partial void OnHasExistingLineupChanged(bool value) { OnPropertyChanged(nameof(HasEmptyLineup)); OnPropertyChanged(nameof(ShowNoEligiblePlayersMessage)); OnPropertyChanged(nameof(ShowNoAdditionalPlayersMessage)); NotifyCommands(); }
    partial void OnHasNoPlayersChanged(bool value) { OnPropertyChanged(nameof(ShowNoEligiblePlayersMessage)); OnPropertyChanged(nameof(ShowNoAdditionalPlayersMessage)); }
    private void NotifyCommands() { AddSelectedCommand.NotifyCanExecuteChanged(); CreatePlayerCommand.NotifyCanExecuteChanged(); EnterAddModeCommand.NotifyCanExecuteChanged(); EnterRemovalModeCommand.NotifyCanExecuteChanged(); CancelTemporaryModeCommand.NotifyCanExecuteChanged(); RequestRemovalCommand.NotifyCanExecuteChanged(); ConfirmRemovalCommand.NotifyCanExecuteChanged(); UsePreviousLineupCommand.NotifyCanExecuteChanged(); DoneCommand.NotifyCanExecuteChanged(); }
    private void ClearSelections() { foreach (var item in Players) item.IsSelected = false; foreach (var item in ExistingLineupPlayers) item.IsSelected = false; selectionOrder.Clear(); SelectedPlayerCount = 0; UpdateRemovalSelection(); }
    private void ExitTemporaryMode() { ClearSelections(); Mode = LineupManagerMode.Normal; }
    private void Reset() { TeamId = GameId = 0; hasContext = false; CanUsePreviousLineup = false; Players.Clear(); ExistingLineupPlayers.Clear(); selectionOrder.Clear(); SelectedPlayerCount = 0; SelectedRemovalCount = 0; Mode = LineupManagerMode.Normal; }
    private static bool TryId(IDictionary<string, object> query, string key, out int id)
    { id = 0; return query.TryGetValue(key, out var value) && (value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0); }
}
