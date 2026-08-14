using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public enum CreatePlayerContext
{
    Global,
    Team,
    GameLineup,
}

public partial class CreatePlayerViewModel : ObservableObject, IQueryAttributable
{
    private readonly PlayerRepository playerRepository;
    private string firstName = string.Empty;
    private string lastName = string.Empty;
    private string validationMessage = string.Empty;
    private bool isBusy;
    private bool hasInvalidContext;
    private int teamId;
    private int gameId;
    private CreatePlayerContext context;

    public string FirstName { get => firstName; set => SetProperty(ref firstName, value); }
    public string LastName { get => lastName; set => SetProperty(ref lastName, value); }
    public string ValidationMessage { get => validationMessage; set => SetProperty(ref validationMessage, value); }
    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value)) NotifyCommands();
        }
    }

    public CreatePlayerViewModel(PlayerRepository playerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);
        this.playerRepository = playerRepository;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        teamId = gameId = 0;
        context = CreatePlayerContext.Global;
        hasInvalidContext = false;

        var hasTeamId = query.TryGetValue(AppShell.TeamIdQueryParameter, out var teamValue);
        var hasGameId = query.TryGetValue(AppShell.GameIdQueryParameter, out var gameValue);
        if (!hasTeamId && !hasGameId) { NotifyCommands(); return; }

        if (!hasTeamId || !TryParseId(teamValue, out teamId))
        {
            InvalidateContext(AppResources.TeamNotFound);
            return;
        }

        context = CreatePlayerContext.Team;
        if (!hasGameId) { NotifyCommands(); return; }
        if (!TryParseId(gameValue, out gameId))
        {
            InvalidateContext(AppResources.GameNotFound);
            return;
        }

        context = CreatePlayerContext.GameLineup;
        NotifyCommands();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => SaveCoreAsync(addAnother: false);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAndAddAnotherAsync() => SaveCoreAsync(addAnother: true);

    private async Task SaveCoreAsync(bool addAnother)
    {
        if (!CanSave()) return;
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

            var player = new Player
            {
                FirstName = trimmedFirstName,
                LastName = string.IsNullOrEmpty(trimmedLastName) ? null : trimmedLastName,
                IsActive = true,
            };

            var result = await CreateAsync(player);
            if (!result.IsSuccess)
            {
                ValidationMessage = FailureMessage(result.Failure);
                return;
            }

            ValidationMessage = string.Empty;
            if (addAnother)
            {
                FirstName = LastName = string.Empty;
                return;
            }

            await Shell.Current.GoToAsync(context switch
            {
                CreatePlayerContext.Team => "../..",
                _ => "..",
            });
        }
        catch
        {
            ValidationMessage = context == CreatePlayerContext.GameLineup
                ? AppResources.PlayerLineupCreationFailed
                : context == CreatePlayerContext.Team
                    ? AppResources.PlayerTeamCreationFailed
                    : AppResources.PlayerCreationFailed;
        }
        finally { IsBusy = false; }
    }

    private async Task<CreatePlayerResult> CreateAsync(Player player)
    {
        if (context == CreatePlayerContext.Team)
            return await playerRepository.CreateForTeamAsync(player, teamId);
        if (context == CreatePlayerContext.GameLineup)
            return await playerRepository.CreateForGameLineupAsync(player, teamId, gameId);
        return await playerRepository.CreateAsync(player) == 1
            ? new(player.Id, CreatePlayerFailure.None)
            : new(0, CreatePlayerFailure.Persistence);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsBusy) return;
        FirstName = LastName = ValidationMessage = string.Empty;
        await Shell.Current.GoToAsync("..");
    }

    private bool CanSave() => !IsBusy && !hasInvalidContext;
    private string FailureMessage(CreatePlayerFailure failure) => failure switch
    {
        CreatePlayerFailure.InvalidTeam => AppResources.TeamNotFound,
        CreatePlayerFailure.InvalidGame or CreatePlayerFailure.TeamMismatch => AppResources.GameNotFound,
        _ when context == CreatePlayerContext.GameLineup => AppResources.PlayerLineupCreationFailed,
        _ when context == CreatePlayerContext.Team => AppResources.PlayerTeamCreationFailed,
        _ => AppResources.PlayerCreationFailed,
    };
    private void InvalidateContext(string message) { hasInvalidContext = true; ValidationMessage = message; NotifyCommands(); }
    private void NotifyCommands() { SaveCommand.NotifyCanExecuteChanged(); SaveAndAddAnotherCommand.NotifyCanExecuteChanged(); }
    private static bool TryParseId(object? value, out int id)
    { id = 0; return value is int number ? (id = number) > 0 : value is string text && int.TryParse(text, out id) && id > 0; }
}
