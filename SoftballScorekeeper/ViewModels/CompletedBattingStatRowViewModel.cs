using SoftballScorekeeper.Core.Statistics;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.ViewModels;

public sealed class CompletedBattingStatRowViewModel
{
    public CompletedBattingStatRowViewModel(
        GameBattingPlayerProjection player,
        int projectionOrder)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (projectionOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(projectionOrder));

        PlayerId = player.PlayerId;
        PlayerName = player.DisplayName;
        BattingPosition = player.BattingPosition;
        ProjectionOrder = projectionOrder;
        PlayerStatistics = player.BattingStatistics;
        AutomationKey = $"Position{BattingPosition}";
        Apply(player.BattingStatistics);
    }

    public CompletedBattingStatRowViewModel(
        string playerName,
        TeamBattingStatLine statistics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        ArgumentNullException.ThrowIfNull(statistics);

        PlayerName = playerName;
        AutomationKey = "TeamTotal";
        Apply(statistics);
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
    public string HomeRunsAutomationId => GetAutomationId("HomeRuns");
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
    public int TotalBases { get; private set; }
    public string BattingAverage { get; private set; } = string.Empty;
    public string OnBasePercentage { get; private set; } = string.Empty;
    public string SluggingPercentage { get; private set; } = string.Empty;

    private string GetAutomationId(string value) =>
        $"CompletedBatting{value}{AutomationKey}";
}
