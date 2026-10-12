namespace SoftballScorekeeper.Views.Controls;

public partial class TeamYearBeerGrid : ContentView
{
    public TeamYearBeerGrid()
    {
        InitializeComponent();
    }

    private void OnStatisticsScrolled(object? sender, ScrolledEventArgs e)
    {
        TeamYearBeerScrollableHeader.TranslationX = -e.ScrollX;
    }
}
