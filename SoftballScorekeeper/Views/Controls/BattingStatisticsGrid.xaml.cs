namespace SoftballScorekeeper.Views.Controls;

public partial class BattingStatisticsGrid : ContentView
{
    public BattingStatisticsGrid()
    {
        InitializeComponent();
    }

    private void OnStatisticsScrolled(object? sender, ScrolledEventArgs e)
    {
        CompletedStatsScrollableHeader.TranslationX = -e.ScrollX;
    }
}
