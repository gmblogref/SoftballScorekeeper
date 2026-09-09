using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public partial class TeamBeerTotalsViewModel : ObservableObject, IQueryAttributable
{
    private readonly TeamRepository teamRepository;
    private readonly BeerAccountingRepository beerAccountingRepository;
    private bool hasValidTeamId;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool isLoaded;

    [ObservableProperty]
    private bool hasRows;

    [ObservableProperty]
    private bool hasNoRows;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    public TeamBeerTotalsViewModel(
        TeamRepository teamRepository,
        BeerAccountingRepository beerAccountingRepository)
    {
        this.teamRepository = teamRepository;
        this.beerAccountingRepository = beerAccountingRepository;
    }

    public ObservableCollection<TeamBeerTotalRowViewModel> Rows { get; } = new();

    public bool CanEdit => IsLoaded && !IsBusy;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(AppShell.TeamIdQueryParameter, out var value))
        {
            if (!hasValidTeamId)
            {
                Reset();
                ValidationMessage = AppResources.BeerTotalsLoadFailed;
            }

            return;
        }

        if (!TryParseTeamId(value, out var parsedTeamId))
        {
            Reset();
            ValidationMessage = AppResources.BeerTotalsLoadFailed;
            return;
        }

        if (hasValidTeamId && TeamId == parsedTeamId)
            return;

        Reset();
        TeamId = parsedTeamId;
        hasValidTeamId = true;
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task<string?> SetRuleCountAsync(
        TeamBeerTotalRowViewModel row,
        BeerRuleType ruleType,
        int desiredEffectiveCount) =>
        SaveAsync(() => beerAccountingRepository.SetEffectiveRuleCountAsync(
            TeamId, row.PlayerId, ruleType, desiredEffectiveCount));

    public Task<string?> SetBroughtAsync(TeamBeerTotalRowViewModel row, int brought) =>
        SaveAsync(() => beerAccountingRepository.SetBroughtAsync(
            TeamId, row.PlayerId, brought));

    public Task<string?> SaveEditAsync(
        TeamBeerTotalRowViewModel row,
        BeerRuleType? ruleType,
        string? enteredValue)
    {
        if (!int.TryParse(enteredValue?.Trim(), out var value) || value < 0)
        {
            ValidationMessage = AppResources.NonNegativeWholeNumberRequiredMessage;
            return Task.FromResult<string?>(ValidationMessage);
        }

        return ruleType is null
            ? SetBroughtAsync(row, value)
            : SetRuleCountAsync(row, ruleType.Value, value);
    }

    private async Task<string?> SaveAsync(
        Func<Task<BeerAccountingUpdateResult>> updateAsync)
    {
        if (!CanEdit)
            return AppResources.BeerTotalsSaveFailed;

        IsBusy = true;
        try
        {
            var result = await updateAsync();
            if (!result.IsSuccess)
            {
                await LoadSummaryAsync();
                ValidationMessage = result.FailureReason is
                    BeerAccountingUpdateFailure.InvalidValue
                    or BeerAccountingUpdateFailure.ValueTooLarge
                    ? AppResources.NonNegativeWholeNumberRequiredMessage
                    : AppResources.BeerTotalsSaveFailed;
                return ValidationMessage;
            }

            if (!await LoadSummaryAsync())
                return ValidationMessage;

            ValidationMessage = string.Empty;
            return null;
        }
        catch
        {
            await TryReloadSummaryAsync();
            ValidationMessage = AppResources.BeerTotalsSaveFailed;
            return ValidationMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCoreAsync()
    {
        if (!hasValidTeamId)
        {
            ValidationMessage = AppResources.BeerTotalsLoadFailed;
            return;
        }

        try
        {
            var team = await teamRepository.GetActiveByIdAsync(TeamId);
            if (team is null)
            {
                SetLoadFailure();
                return;
            }

            TeamName = team.Name;
            await LoadSummaryAsync();
        }
        catch
        {
            SetLoadFailure();
        }
    }

    private async Task<bool> LoadSummaryAsync()
    {
        var summaries = await beerAccountingRepository.GetTeamLifetimeAsync(TeamId);
        if (summaries is null)
        {
            SetLoadFailure();
            return false;
        }

        for (var index = 0; index < summaries.Count; index++)
        {
            var summary = summaries[index];
            if (index < Rows.Count && Rows[index].PlayerId == summary.PlayerId)
            {
                Rows[index].Update(summary);
                continue;
            }

            var existingIndex = Enumerable.Range(0, Rows.Count).FirstOrDefault(
                rowIndex => Rows[rowIndex].PlayerId == summary.PlayerId,
                -1);
            if (existingIndex >= 0)
            {
                Rows.Move(existingIndex, index);
                Rows[index].Update(summary);
                continue;
            }

            Rows.Insert(index, new TeamBeerTotalRowViewModel(summary));
        }

        while (Rows.Count > summaries.Count)
            Rows.RemoveAt(Rows.Count - 1);

        HasRows = Rows.Count > 0;
        HasNoRows = !HasRows;
        IsLoaded = true;
        ValidationMessage = string.Empty;
        return true;
    }

    private async Task TryReloadSummaryAsync()
    {
        try
        {
            await LoadSummaryAsync();
        }
        catch
        {
            SetLoadFailure();
        }
    }

    private void SetLoadFailure()
    {
        Rows.Clear();
        HasRows = false;
        HasNoRows = false;
        IsLoaded = false;
        ValidationMessage = AppResources.BeerTotalsLoadFailed;
    }

    private void Reset()
    {
        TeamId = 0;
        TeamName = string.Empty;
        hasValidTeamId = false;
        SetLoadFailure();
        ValidationMessage = string.Empty;
    }

    private static bool TryParseTeamId(object? value, out int teamId)
    {
        if (value is int integerValue && integerValue > 0)
        {
            teamId = integerValue;
            return true;
        }

        if (value is string text
            && int.TryParse(text, out var parsedTeamId)
            && parsedTeamId > 0)
        {
            teamId = parsedTeamId;
            return true;
        }

        teamId = 0;
        return false;
    }
}
