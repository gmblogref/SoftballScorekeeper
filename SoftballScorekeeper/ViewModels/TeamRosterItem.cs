using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class TeamRosterItem : ObservableObject
{
    private readonly Action<TeamRosterItem>? selectionChanged;
    private string editableJerseyNumber = string.Empty;
    private bool isEditingJerseyNumber;
    private bool isSelected;
    private int? jerseyNumber;

    public TeamRosterItem(TeamRosterMember member, Action<TeamRosterItem>? selectionChanged = null)
    {
        ArgumentNullException.ThrowIfNull(member);

        TeamPlayerId = member.TeamPlayerId;
        TeamId = member.TeamId;
        Player = member.Player;
        jerseyNumber = member.JerseyNumber;
        this.selectionChanged = selectionChanged;
    }

    public int TeamPlayerId { get; }

    public int TeamId { get; }

    public Player Player { get; }

    public string DisplayName =>
        string.Join(
            " ",
            new[] { Player.FirstName, Player.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
        );

    public int? JerseyNumber => jerseyNumber;

    public bool HasJerseyNumber => JerseyNumber.HasValue;

    public string JerseyNumberDisplay =>
        JerseyNumber.HasValue
            ? string.Format(
                CultureInfo.CurrentCulture,
                AppResources.JerseyNumberDisplayFormat,
                JerseyNumber.Value
            )
            : string.Empty;

    public string EditableJerseyNumber
    {
        get => editableJerseyNumber;
        set => SetProperty(ref editableJerseyNumber, value);
    }

    public bool IsEditingJerseyNumber
    {
        get => isEditingJerseyNumber;
        private set => SetProperty(ref isEditingJerseyNumber, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
            {
                selectionChanged?.Invoke(this);
            }
        }
    }

    public void BeginJerseyNumberEdit()
    {
        EditableJerseyNumber = JerseyNumber?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        IsEditingJerseyNumber = true;
    }

    public void CancelJerseyNumberEdit()
    {
        EditableJerseyNumber = JerseyNumber?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        IsEditingJerseyNumber = false;
    }

    public void CommitJerseyNumber(int? value)
    {
        jerseyNumber = value;
        OnPropertyChanged(nameof(JerseyNumber));
        OnPropertyChanged(nameof(HasJerseyNumber));
        OnPropertyChanged(nameof(JerseyNumberDisplay));
        EditableJerseyNumber = value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        IsEditingJerseyNumber = false;
    }
}
