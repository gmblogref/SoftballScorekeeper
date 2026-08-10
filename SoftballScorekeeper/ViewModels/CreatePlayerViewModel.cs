using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class CreatePlayerViewModel : ObservableObject, IQueryAttributable
{
    private readonly PlayerRepository playerRepository;
    private readonly TeamRepository teamRepository;
    private readonly TeamPlayerRepository teamPlayerRepository;
    private string firstName = string.Empty;
    private string lastName = string.Empty;
    private string validationMessage = string.Empty;
    private bool isBusy;
    private bool hasInvalidTeamContext;
    private bool hasTeamContext;
    private bool playerCreated;
    private int teamId;

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
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public CreatePlayerViewModel(
        PlayerRepository playerRepository,
        TeamRepository teamRepository,
        TeamPlayerRepository teamPlayerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamPlayerRepository);

        this.playerRepository = playerRepository;
        this.teamRepository = teamRepository;
        this.teamPlayerRepository = teamPlayerRepository;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        teamId = 0;
        hasTeamContext = false;
        hasInvalidTeamContext = false;

        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value))
        {
            return;
        }

        if (!TryParseTeamId(value, out teamId))
        {
            hasInvalidTeamContext = true;
            ValidationMessage = AppResources.TeamNotFound;
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        hasTeamContext = true;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave())
        {
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

            if (hasTeamContext && await teamRepository.GetActiveByIdAsync(teamId) is null)
            {
                hasInvalidTeamContext = true;
                ValidationMessage = AppResources.TeamNotFound;
                SaveCommand.NotifyCanExecuteChanged();
                return;
            }

            var player = new Player
            {
                FirstName = trimmedFirstName,
                LastName = string.IsNullOrEmpty(trimmedLastName) ? null : trimmedLastName,
                IsActive = true,
            };

            if (await playerRepository.CreateAsync(player) != 1)
            {
                ValidationMessage = AppResources.PlayerCreationFailed;
                return;
            }

            playerCreated = true;
            SaveCommand.NotifyCanExecuteChanged();

            if (hasTeamContext)
            {
                var result = await teamPlayerRepository.AddPlayersToTeamAsync(teamId, [player.Id]);
                if (result.SuccessfullyActiveMembershipCount != 1)
                {
                    ValidationMessage = AppResources.PlayerCreatedTeamMembershipFailed;
                    return;
                }
            }

            ValidationMessage = string.Empty;
            await Shell.Current.GoToAsync(hasTeamContext ? "../.." : "..");
        }
        catch
        {
            ValidationMessage = playerCreated
                ? AppResources.PlayerCreatedTeamMembershipFailed
                : AppResources.PlayerCreationFailed;
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

        FirstName = string.Empty;
        LastName = string.Empty;
        ValidationMessage = string.Empty;
        await Shell.Current.GoToAsync("..");
    }

    private bool CanSave()
    {
        return !IsBusy && !playerCreated && !hasInvalidTeamContext;
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
