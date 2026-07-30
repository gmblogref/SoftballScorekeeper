using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class EditPlayerViewModel : ObservableObject, IQueryAttributable
{
    private readonly PlayerRepository playerRepository;
    private int playerId;
    private bool hasValidPlayerId;
    private Player? loadedPlayer;
    private string firstName = string.Empty;
    private string lastName = string.Empty;
    private string validationMessage = string.Empty;
    private bool isBusy;
    private bool isPlayerLoaded;

    public EditPlayerViewModel(PlayerRepository playerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);
        this.playerRepository = playerRepository;
    }

    public string FirstName
    {
        get => firstName;
        set => SetProperty(ref firstName, value);
    }

    public string LastName
    {
        get => lastName;
        set => SetProperty(ref lastName, value);
    }

    public string ValidationMessage
    {
        get => validationMessage;
        set => SetProperty(ref validationMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsPlayerLoaded
    {
        get => isPlayerLoaded;
        private set
        {
            if (SetProperty(ref isPlayerLoaded, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (IsPlayerLoaded)
        {
            return;
        }

        ResetPlayerState();
        if (!query.TryGetValue(AppShell.PlayerIdQueryParameter, out var value)
            || !TryParsePlayerId(value, out var parsedPlayerId))
        {
            ValidationMessage = AppResources.PlayerNotFound;
            return;
        }

        playerId = parsedPlayerId;
        hasValidPlayerId = true;
    }

    public async Task LoadPlayerAsync()
    {
        if (IsBusy || IsPlayerLoaded)
        {
            return;
        }

        if (!hasValidPlayerId)
        {
            ValidationMessage = AppResources.PlayerNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            var player = await playerRepository.GetActiveByIdAsync(playerId);
            if (player is null)
            {
                ValidationMessage = AppResources.PlayerNotFound;
                return;
            }

            loadedPlayer = player;
            FirstName = player.FirstName;
            LastName = player.LastName ?? string.Empty;
            ValidationMessage = string.Empty;
            IsPlayerLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave())
        {
            ValidationMessage = AppResources.PlayerNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            var trimmedFirstName = FirstName?.Trim() ?? string.Empty;
            var trimmedLastName = LastName?.Trim() ?? string.Empty;
            FirstName = trimmedFirstName;
            LastName = trimmedLastName;

            if (string.IsNullOrWhiteSpace(trimmedFirstName))
            {
                ValidationMessage = AppResources.FirstNameRequired;
                return;
            }

            var updatedPlayer = new Player
            {
                Id = loadedPlayer!.Id,
                FirstName = trimmedFirstName,
                LastName = string.IsNullOrEmpty(trimmedLastName) ? null : trimmedLastName,
                IsActive = loadedPlayer.IsActive,
            };

            if (await playerRepository.UpdateAsync(updatedPlayer) != 1)
            {
                loadedPlayer = null;
                IsPlayerLoaded = false;
                ValidationMessage = AppResources.PlayerNotFound;
                return;
            }

            loadedPlayer = updatedPlayer;
            ValidationMessage = string.Empty;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => !IsBusy
        && hasValidPlayerId
        && IsPlayerLoaded
        && loadedPlayer is { Id: > 0, IsActive: true };

    private void ResetPlayerState()
    {
        playerId = 0;
        hasValidPlayerId = false;
        loadedPlayer = null;
        FirstName = string.Empty;
        LastName = string.Empty;
        ValidationMessage = string.Empty;
        IsPlayerLoaded = false;
    }

    private static bool TryParsePlayerId(object? value, out int parsedPlayerId)
    {
        if (value is int integerValue && integerValue > 0)
        {
            parsedPlayerId = integerValue;
            return true;
        }

        if (value is string stringValue
            && int.TryParse(stringValue, out var stringPlayerId)
            && stringPlayerId > 0)
        {
            parsedPlayerId = stringPlayerId;
            return true;
        }

        parsedPlayerId = 0;
        return false;
    }
}
