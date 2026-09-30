using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class Story10ManualAcceptancePresentationTests
{
    [Fact]
    public void Current_lineup_renders_authoritative_formatted_obp()
    {
        var page = LoadXaml("Views", "SetLineupPage.xaml");
        var currentLineupTemplate = page.Descendants()
            .Single(element => element.Name.LocalName == "CollectionView"
                && element.Attribute("ItemsSource")?.Value
                    == "{Binding ExistingLineupPlayers}")
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTemplate");
        var item = Read("ViewModels", "LineupPlayerSelectionItem.cs");
        var viewModel = Read("ViewModels", "SetLineupViewModel.cs");

        Assert.Contains(currentLineupTemplate.Descendants(), element =>
            element.Name.LocalName == "Label"
            && element.Attribute("Text")?.Value
                == "{Binding OnBasePercentageDisplay}");
        Assert.Contains("BattingRateFormatter.Format(value)", item);
        Assert.Contains("AppResources.UndefinedBattingRate", item);
        Assert.Contains("rates.GetValueOrDefault(existingPlayers[index].Id)", viewModel);
        Assert.Contains("statisticsRepository.GetAsync(TeamId)", viewModel);
    }

    [Fact]
    public void Completed_game_stats_and_plays_visibility_uses_page_context_containers()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var stats = page.Descendants().Single(element =>
            element.Attribute("AutomationId")?.Value == "CompletedGameStatsView");
        var plays = page.Descendants().Single(element =>
            element.Attribute("AutomationId")?.Value == "CompletedGamePlaysView");
        var statisticsGrid = Assert.Single(stats.Descendants().Where(element =>
            element.Name.LocalName == "BattingStatisticsGrid"));

        Assert.Equal("{Binding ShowingGameStats}",
            stats.Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding ShowingGamePlays}",
            plays.Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding CompletedBattingStatistics}",
            statisticsGrid.Attribute("BindingContext")?.Value);
        Assert.Null(statisticsGrid.Attribute("IsVisible"));
        Assert.NotNull(page.Descendants().Single(element =>
            element.Attribute(x + "Name")?.Value == "CompletedGameGrid"));
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
}
