using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class InactiveTeamsViewModel : ObservableObject
{
    private readonly TeamRepository teamRepository;

    [ObservableProperty]
    private bool hasInactiveTeams;

    [ObservableProperty]
    private bool hasNoInactiveTeams = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestReactivateTeamCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmReactivateTeamCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    public InactiveTeamsViewModel(TeamRepository teamRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);

        this.teamRepository = teamRepository;
    }

    public ObservableCollection<Team> InactiveTeams { get; } = new();

    public Func<Team, Task>? ReactivateTeamConfirmationRequested { get; set; }

    public async Task LoadInactiveTeamsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await ReloadInactiveTeamsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReactivateTeam))]
    private async Task RequestReactivateTeamAsync(Team? team)
    {
        if (!CanReactivateTeam(team))
        {
            return;
        }

        var confirmationRequested = ReactivateTeamConfirmationRequested;
        if (confirmationRequested is not null)
        {
            await confirmationRequested(team!);
        }
    }

    [RelayCommand(CanExecute = nameof(CanReactivateTeam))]
    private async Task ConfirmReactivateTeamAsync(Team? team)
    {
        if (!CanReactivateTeam(team))
        {
            ValidationMessage = AppResources.TeamNotFound;
            return;
        }

        IsBusy = true;
        try
        {
            if (await teamRepository.ActiveNameExistsAsync(team!.Name, team.Id))
            {
                ValidationMessage = AppResources.DuplicateTeamName;
                return;
            }

            if (await teamRepository.ReactivateAsync(team.Id) == 0)
            {
                ValidationMessage = AppResources.TeamNotFound;
                return;
            }

            ValidationMessage = string.Empty;
            await ReloadInactiveTeamsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanReactivateTeam(Team? team)
    {
        return !IsBusy
            && team is { Id: > 0, IsActive: false };
    }

    private async Task ReloadInactiveTeamsAsync()
    {
        var teams = await teamRepository.GetInactiveAsync();

        InactiveTeams.Clear();
        foreach (var team in teams)
        {
            InactiveTeams.Add(team);
        }

        HasInactiveTeams = InactiveTeams.Count > 0;
        HasNoInactiveTeams = !HasInactiveTeams;
    }
}
