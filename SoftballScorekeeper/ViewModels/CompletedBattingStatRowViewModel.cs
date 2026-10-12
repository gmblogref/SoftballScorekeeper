using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class CompletedBattingStatRowViewModel : ObservableObject
{
    private string singlesEdit = string.Empty;
    private string doublesEdit = string.Empty;
    private string triplesEdit = string.Empty;
    private string homeRunsEdit = string.Empty;
    private string walksEdit = string.Empty;
    private string fielderChoicesEdit = string.Empty;
    private string sacrificeFliesEdit = string.Empty;
    private string outsEdit = string.Empty;
    private bool isEditing;

    public CompletedBattingStatRowViewModel(GameBattingPlayerProjection player, int projectionOrder)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (projectionOrder < 0) throw new ArgumentOutOfRangeException(nameof(projectionOrder));
        PlayerId = player.PlayerId;
        PlayerName = player.DisplayName;
        BattingPosition = player.BattingPosition;
        ProjectionOrder = projectionOrder;
        PlayerStatistics = player.BattingStatistics;
        AutomationKey = $"Position{BattingPosition}";
        Apply(player.BattingStatistics);
        Outs = player.OrdinaryOuts;
        ResetEdits();
    }

    public CompletedBattingStatRowViewModel(
        string playerName,
        TeamBattingStatLine statistics,
        int ordinaryOuts = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        ArgumentNullException.ThrowIfNull(statistics);
        PlayerName = playerName;
        AutomationKey = "TeamTotal";
        Apply(statistics);
        Outs = ordinaryOuts;
        ResetEdits();
    }

    public int PlayerId { get; }
    public string PlayerName { get; }
    public int BattingPosition { get; }
    public int ProjectionOrder { get; }
    public PlayerBattingStatLine? PlayerStatistics { get; }
    private string AutomationKey { get; }
    public string AutomationId => GetAutomationId("Row");
    public string PlateAppearancesAutomationId => GetAutomationId("PlateAppearances");
    public string AtBatsAutomationId => GetAutomationId("AtBats");
    public string HitsAutomationId => GetAutomationId("Hits");
    public string SinglesAutomationId => GetAutomationId("Singles");
    public string DoublesAutomationId => GetAutomationId("Doubles");
    public string TriplesAutomationId => GetAutomationId("Triples");
    public string HomeRunsAutomationId => GetAutomationId("HomeRuns");
    public string WalksAutomationId => GetAutomationId("Walks");
    public string FielderChoicesAutomationId => GetAutomationId("FielderChoices");
    public string SacrificeFliesAutomationId => GetAutomationId("SacrificeFlies");
    public string OutsAutomationId => GetAutomationId("Outs");
    public string BattingAverageAutomationId => GetAutomationId("BattingAverage");
    public string OnBasePercentageAutomationId => GetAutomationId("OnBasePercentage");
    public string SluggingPercentageAutomationId => GetAutomationId("SluggingPercentage");
    public int PlateAppearances { get; private set; }
    public int AtBats { get; private set; }
    public int Hits { get; private set; }
    public int Singles { get; private set; }
    public int Doubles { get; private set; }
    public int Triples { get; private set; }
    public int HomeRuns { get; private set; }
    public int Walks { get; private set; }
    public int FielderChoices { get; private set; }
    public int SacrificeFlies { get; private set; }
    public int Outs { get; private set; }
    public int TotalBases { get; private set; }
    public string BattingAverage { get; private set; } = string.Empty;
    public string OnBasePercentage { get; private set; } = string.Empty;
    public string SluggingPercentage { get; private set; } = string.Empty;

    public bool IsEditing
    {
        get => isEditing;
        private set
        {
            if (!SetProperty(ref isEditing, value)) return;
            OnPropertyChanged(nameof(IsReadOnly));
        }
    }
    public bool IsReadOnly => !IsEditing;
    public string SinglesEdit { get => singlesEdit; set => SetEdit(ref singlesEdit, value, nameof(SinglesEdit), nameof(IsSinglesDirty), nameof(SinglesEditDescription)); }
    public string DoublesEdit { get => doublesEdit; set => SetEdit(ref doublesEdit, value, nameof(DoublesEdit), nameof(IsDoublesDirty), nameof(DoublesEditDescription)); }
    public string TriplesEdit { get => triplesEdit; set => SetEdit(ref triplesEdit, value, nameof(TriplesEdit), nameof(IsTriplesDirty), nameof(TriplesEditDescription)); }
    public string HomeRunsEdit { get => homeRunsEdit; set => SetEdit(ref homeRunsEdit, value, nameof(HomeRunsEdit), nameof(IsHomeRunsDirty), nameof(HomeRunsEditDescription)); }
    public string WalksEdit { get => walksEdit; set => SetEdit(ref walksEdit, value, nameof(WalksEdit), nameof(IsWalksDirty), nameof(WalksEditDescription)); }
    public string FielderChoicesEdit { get => fielderChoicesEdit; set => SetEdit(ref fielderChoicesEdit, value, nameof(FielderChoicesEdit), nameof(IsFielderChoicesDirty), nameof(FielderChoicesEditDescription)); }
    public string SacrificeFliesEdit { get => sacrificeFliesEdit; set => SetEdit(ref sacrificeFliesEdit, value, nameof(SacrificeFliesEdit), nameof(IsSacrificeFliesDirty), nameof(SacrificeFliesEditDescription)); }
    public string OutsEdit { get => outsEdit; set => SetEdit(ref outsEdit, value, nameof(OutsEdit), nameof(IsOutsDirty), nameof(OutsEditDescription)); }
    public bool IsSinglesDirty => IsDirty(SinglesEdit, Singles);
    public bool IsDoublesDirty => IsDirty(DoublesEdit, Doubles);
    public bool IsTriplesDirty => IsDirty(TriplesEdit, Triples);
    public bool IsHomeRunsDirty => IsDirty(HomeRunsEdit, HomeRuns);
    public bool IsWalksDirty => IsDirty(WalksEdit, Walks);
    public bool IsFielderChoicesDirty => IsDirty(FielderChoicesEdit, FielderChoices);
    public bool IsSacrificeFliesDirty => IsDirty(SacrificeFliesEdit, SacrificeFlies);
    public bool IsOutsDirty => IsDirty(OutsEdit, Outs);
    public string SinglesEditDescription => GetEditDescription(AppResources.SinglesStatHeading, IsSinglesDirty);
    public string DoublesEditDescription => GetEditDescription(AppResources.DoublesStatHeading, IsDoublesDirty);
    public string TriplesEditDescription => GetEditDescription(AppResources.TriplesStatHeading, IsTriplesDirty);
    public string HomeRunsEditDescription => GetEditDescription(AppResources.HomeRunsStatHeading, IsHomeRunsDirty);
    public string WalksEditDescription => GetEditDescription(AppResources.WalksStatHeading, IsWalksDirty);
    public string FielderChoicesEditDescription => GetEditDescription(AppResources.FielderChoicesStatHeading, IsFielderChoicesDirty);
    public string SacrificeFliesEditDescription => GetEditDescription(AppResources.SacrificeFliesStatHeading, IsSacrificeFliesDirty);
    public string OutsEditDescription => GetEditDescription(AppResources.OutsStatHeading, IsOutsDirty);

    public void BeginEdit() { ResetEdits(); IsEditing = true; }
    public void CancelEdit() { ResetEdits(); IsEditing = false; }

    public bool TryCreateCorrection(out CompletedGameBattingCorrection correction)
    {
        correction = new(PlayerId, 0, 0, 0, 0, 0, 0, 0, 0);
        if (!TryParse(SinglesEdit, out var singles)
            || !TryParse(DoublesEdit, out var doubles)
            || !TryParse(TriplesEdit, out var triples)
            || !TryParse(HomeRunsEdit, out var homeRuns)
            || !TryParse(WalksEdit, out var walks)
            || !TryParse(FielderChoicesEdit, out var fielderChoices)
            || !TryParse(SacrificeFliesEdit, out var sacrificeFlies)
            || !TryParse(OutsEdit, out var outs)) return false;
        correction = new(PlayerId, singles, doubles, triples, homeRuns, walks,
            fielderChoices, sacrificeFlies, outs);
        return true;
    }

    private void Apply(PlayerBattingStatLine statistics)
    {
        PlateAppearances = statistics.PlateAppearances;
        AtBats = statistics.AtBats;
        Hits = statistics.Hits;
        Singles = statistics.Singles;
        Doubles = statistics.Doubles;
        Triples = statistics.Triples;
        HomeRuns = statistics.HomeRuns;
        Walks = statistics.Walks;
        FielderChoices = statistics.FielderChoices;
        SacrificeFlies = statistics.SacrificeFlies;
        TotalBases = statistics.TotalBases;
        BattingAverage = BattingRateFormatter.Format(statistics.BattingAverage);
        OnBasePercentage = BattingRateFormatter.Format(statistics.OnBasePercentage);
        SluggingPercentage = BattingRateFormatter.Format(statistics.SluggingPercentage);
    }

    private void Apply(TeamBattingStatLine statistics)
    {
        PlateAppearances = statistics.PlateAppearances;
        AtBats = statistics.AtBats;
        Hits = statistics.Hits;
        Singles = statistics.Singles;
        Doubles = statistics.Doubles;
        Triples = statistics.Triples;
        HomeRuns = statistics.HomeRuns;
        Walks = statistics.Walks;
        FielderChoices = statistics.FielderChoices;
        SacrificeFlies = statistics.SacrificeFlies;
        TotalBases = statistics.TotalBases;
        BattingAverage = BattingRateFormatter.Format(statistics.BattingAverage);
        OnBasePercentage = BattingRateFormatter.Format(statistics.OnBasePercentage);
        SluggingPercentage = BattingRateFormatter.Format(statistics.SluggingPercentage);
    }

    private void ResetEdits()
    {
        singlesEdit = Format(Singles);
        doublesEdit = Format(Doubles);
        triplesEdit = Format(Triples);
        homeRunsEdit = Format(HomeRuns);
        walksEdit = Format(Walks);
        fielderChoicesEdit = Format(FielderChoices);
        sacrificeFliesEdit = Format(SacrificeFlies);
        outsEdit = Format(Outs);
        OnPropertyChanged(nameof(SinglesEdit)); OnPropertyChanged(nameof(DoublesEdit));
        OnPropertyChanged(nameof(TriplesEdit)); OnPropertyChanged(nameof(HomeRunsEdit));
        OnPropertyChanged(nameof(WalksEdit)); OnPropertyChanged(nameof(FielderChoicesEdit));
        OnPropertyChanged(nameof(SacrificeFliesEdit)); OnPropertyChanged(nameof(OutsEdit));
        NotifyDirtyChanged();
    }

    private void SetEdit(ref string storage, string? value, string propertyName, string dirtyName,
        string descriptionName)
    {
        value ??= string.Empty;
        if (!SetProperty(ref storage, value, propertyName)) return;
        OnPropertyChanged(dirtyName);
        OnPropertyChanged(descriptionName);
    }

    private void NotifyDirtyChanged()
    {
        OnPropertyChanged(nameof(IsSinglesDirty)); OnPropertyChanged(nameof(IsDoublesDirty));
        OnPropertyChanged(nameof(IsTriplesDirty)); OnPropertyChanged(nameof(IsHomeRunsDirty));
        OnPropertyChanged(nameof(IsWalksDirty)); OnPropertyChanged(nameof(IsFielderChoicesDirty));
        OnPropertyChanged(nameof(IsSacrificeFliesDirty));
        OnPropertyChanged(nameof(IsOutsDirty));
        OnPropertyChanged(nameof(SinglesEditDescription)); OnPropertyChanged(nameof(DoublesEditDescription));
        OnPropertyChanged(nameof(TriplesEditDescription)); OnPropertyChanged(nameof(HomeRunsEditDescription));
        OnPropertyChanged(nameof(WalksEditDescription)); OnPropertyChanged(nameof(FielderChoicesEditDescription));
        OnPropertyChanged(nameof(SacrificeFliesEditDescription));
        OnPropertyChanged(nameof(OutsEditDescription));
    }

    private static bool TryParse(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.CurrentCulture, out result) && result >= 0;
    private static string Format(int value) => value.ToString(CultureInfo.CurrentCulture);
    private static bool IsDirty(string text, int persistedValue) =>
        !TryParse(text, out var value) || value != persistedValue;
    private static string GetEditDescription(string fieldName, bool isDirty) =>
        isDirty
            ? string.Format(CultureInfo.CurrentCulture,
                AppResources.ManualOverrideUnsavedFieldDescription, fieldName)
            : fieldName;
    private string GetAutomationId(string value) => $"CompletedBatting{value}{AutomationKey}";
}
