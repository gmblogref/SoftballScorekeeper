using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class CreatePlayerViewModel : ObservableObject
{
    private readonly PlayerRepository playerRepository;
    private string firstName = string.Empty;
    private string lastName = string.Empty;
    private string validationMessage = string.Empty;
    private bool isBusy;

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
        set => SetProperty(ref isBusy, value);
    }

    public CreatePlayerViewModel(PlayerRepository playerRepository)
    {
        ArgumentNullException.ThrowIfNull(playerRepository);

        this.playerRepository = playerRepository;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
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

            var player = new Player
            {
                FirstName = trimmedFirstName,
                LastName = string.IsNullOrEmpty(trimmedLastName) ? null : trimmedLastName,
                IsActive = true,
            };

            await playerRepository.CreateAsync(player);
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

        FirstName = string.Empty;
        LastName = string.Empty;
        ValidationMessage = string.Empty;
        await Shell.Current.GoToAsync("..");
    }
}
