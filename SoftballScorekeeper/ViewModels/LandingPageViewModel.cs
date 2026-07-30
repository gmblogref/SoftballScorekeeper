using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.Models;

namespace SoftballScorekeeper.ViewModels;

public partial class LandingPageViewModel : ObservableObject
{
    private readonly TeamRepository teamRepository;

    [ObservableProperty]
    private bool hasTeams;

    [ObservableProperty]
    private bool hasNoTeams = true;

    [ObservableProperty]
    private bool isLoading;

    public LandingPageViewModel(TeamRepository teamRepository)
    {
        ArgumentNullException.ThrowIfNull(teamRepository);

        this.teamRepository = teamRepository;
    }

    public ObservableCollection<Team> Teams { get; } = new();

    public async Task LoadTeamsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var teams = await teamRepository.GetActiveAsync();

            Teams.Clear();
            foreach (var team in teams)
            {
                Teams.Add(team);
            }

            HasTeams = Teams.Count > 0;
            HasNoTeams = !HasTeams;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateTeamAsync()
    {
        await Shell.Current.GoToAsync(AppShell.CreateTeamRoute);
    }

    [RelayCommand]
    private async Task ManageInactiveTeamsAsync()
    {
        await Shell.Current.GoToAsync(AppShell.InactiveTeamsRoute);
    }

    [RelayCommand]
    private async Task EditTeamAsync(Team? team)
    {
        if (team is null || team.Id <= 0)
        {
            return;
        }

        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = team.Id
        };

        await Shell.Current.GoToAsync(AppShell.EditTeamRoute, parameters);
    }
}
