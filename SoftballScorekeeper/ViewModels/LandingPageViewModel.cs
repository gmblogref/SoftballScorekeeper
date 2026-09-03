using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.ViewModels;

public partial class LandingPageViewModel : ObservableObject
{
    private readonly TeamRepository teamRepository;
    private readonly TeamRecordRepository teamRecordRepository;

    [ObservableProperty]
    private bool hasTeams;

    [ObservableProperty]
    private bool hasNoTeams = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenTeamCommand))]
    private bool isLoading;

    public LandingPageViewModel(
        TeamRepository teamRepository,
        TeamRecordRepository teamRecordRepository
    )
    {
        ArgumentNullException.ThrowIfNull(teamRepository);
        ArgumentNullException.ThrowIfNull(teamRecordRepository);

        this.teamRepository = teamRepository;
        this.teamRecordRepository = teamRecordRepository;
    }

    public ObservableCollection<TeamManagerItem> Teams { get; } = new();

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
            var records = await teamRecordRepository.GetForTeamsAsync(
                teams.Select(team => team.Id)
            );

            Teams.Clear();
            foreach (var team in teams)
            {
                var record = records.GetValueOrDefault(team.Id) ?? new TeamYearRecord(null, 0, 0);
                Teams.Add(new TeamManagerItem(team, record));
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

    [RelayCommand(CanExecute = nameof(CanOpenTeam))]
    private async Task OpenTeamAsync(TeamManagerItem? team)
    {
        if (team is null || team.Id <= 0)
        {
            return;
        }

        var parameters = new ShellNavigationQueryParameters
        {
            [AppShell.TeamIdQueryParameter] = team.Id,
        };

        await Shell.Current.GoToAsync(AppShell.TeamWorkspaceRoute, parameters);
    }

    private bool CanOpenTeam(TeamManagerItem? team)
    {
        return !IsLoading && team is { Id: > 0 };
    }
}
