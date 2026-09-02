using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class InGameStatsViewerTests
{
    [Fact]
    public void Scoring_menu_places_stats_between_game_rules_and_exit_game()
    {
        var page = LoadXaml("Views", "ScoringPage.xaml");
        var ns = page.Root!.Name.Namespace;
        var menu = NamedElements(page)["ScoringMenu"];
        var buttons = menu.Descendants(ns + "Button").ToArray();

        Assert.Equal(new[]
        {
            "OnGameRulesClicked",
            "OnStatsClicked",
            "OnExitGameClicked",
            "OnGameOverClicked",
        }, buttons.Select(button => button.Attribute("Clicked")?.Value));
        Assert.Equal("InGameStatsButton", buttons[1].Attribute("AutomationId")?.Value);
        Assert.Equal("{x:Static strings:AppResources.StatsButton}",
            buttons[1].Attribute("Text")?.Value);
    }

    [Fact]
    public void Scoring_menu_overlays_the_scoring_layout_without_consuming_a_row()
    {
        var page = LoadXaml("Views", "ScoringPage.xaml");
        var named = NamedElements(page);
        var overlayGrid = named["ScoringOverlayGrid"];
        var scoringLayout = named["RootGrid"];
        var menu = named["ScoringMenu"];

        Assert.Equal(overlayGrid, scoringLayout.Parent);
        Assert.Equal(overlayGrid, menu.Parent);
        Assert.Null(menu.Attribute("Grid.Row"));
        Assert.Equal("Start", menu.Attribute("HorizontalOptions")?.Value);
        Assert.Equal("Start", menu.Attribute("VerticalOptions")?.Value);
        Assert.Equal("10", menu.Attribute("ZIndex")?.Value);
        Assert.Equal("40,92,0,0", menu.Attribute("Margin")?.Value);
        Assert.Contains(page.Descendants(), element =>
            element.Name.LocalName == "Setter"
                && element.Attribute("TargetName")?.Value == "ScoringMenu"
                && element.Attribute("Property")?.Value == "Margin"
                && element.Attribute("Value")?.Value == "20,64,0,0");
    }

    [Fact]
    public void In_game_viewer_is_read_only_closable_and_uses_shared_grid()
    {
        var dialog = LoadXaml("Views", "InGameStatsDialog.xaml");
        var ns = dialog.Root!.Name.Namespace;

        Assert.Single(dialog.Descendants().Where(element =>
            element.Name.LocalName == "BattingStatisticsGrid"));
        Assert.Contains(dialog.Descendants(ns + "Button"), button =>
            button.Attribute("AutomationId")?.Value == "CloseInGameStatsButton"
                && button.Attribute("Clicked")?.Value == "OnCloseClicked");
        Assert.Empty(dialog.Descendants(ns + "Entry"));
        Assert.Empty(dialog.Descendants(ns + "Editor"));
        Assert.Empty(dialog.Descendants(ns + "Picker"));
        Assert.Empty(dialog.Descendants(ns + "CheckBox"));
        Assert.Empty(dialog.Descendants(ns + "Switch"));
    }

    [Fact]
    public void Every_stats_open_reloads_active_authoritative_projection()
    {
        var pageCode = ReadText("Views", "ScoringPage.xaml.cs");
        var scoringViewModel = ReadText("ViewModels", "ScoringViewModel.cs");
        var repository = ReadText("..", "SoftballScorekeeper.Infrastructure", "Data",
            "Repositories", "GameScoringSessionRepository.cs");

        Assert.Contains("OnStatsClicked", pageCode);
        Assert.Contains("await viewModel.LoadInGameBattingStatisticsAsync()", pageCode);
        Assert.Contains("new InGameStatsDialog(projection.Players)", pageCode);
        Assert.Contains("GetInProgressBattingStatisticsAsync(GameId)", scoringViewModel);
        Assert.Contains("item.Status == GameScoringStatus.InProgress", repository);
        Assert.Contains("ProjectBattingPlayersAsync(connection, session)", repository);
        Assert.DoesNotContain("GetCompletedGameAsync(GameId)", scoringViewModel);
    }

    [Fact]
    public void Shared_grid_preserves_all_columns_scrolling_sorting_and_pinned_total()
    {
        var grid = LoadXaml("Views", "Controls", "BattingStatisticsGrid.xaml");
        var ns = grid.Root!.Name.Namespace;
        var named = NamedElements(grid);
        var header = named["CompletedStatsHeader"];
        var headings = header.Elements(ns + "Button").ToArray();

        Assert.Equal("Both", named["CompletedStatsTable"].Parent!
            .Attribute("Orientation")?.Value);
        Assert.Equal(14, headings.Length);
        Assert.Equal("{Binding SacrificeFliesStatSortHeader}",
            headings[10].Attribute("Text")?.Value);
        Assert.Equal("{Binding BattingAverageStatSortHeader}",
            headings[11].Attribute("Text")?.Value);
        Assert.Equal("{Binding OnBasePercentageStatSortHeader}",
            headings[12].Attribute("Text")?.Value);
        Assert.Equal("{Binding SluggingPercentageStatSortHeader}",
            headings[13].Attribute("Text")?.Value);
        Assert.All(headings, heading =>
            Assert.Equal("{Binding SortRowsCommand}", heading.Attribute("Command")?.Value));
        Assert.Contains(named["CompletedStatsTeamTotal"],
            named["CompletedStatsPlayerRows"].ElementsAfterSelf());
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }
            .Concat(path).ToArray()));

    private static string ReadText(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }
            .Concat(path).ToArray()));

    private static Dictionary<string, XElement> NamedElements(XDocument document)
    {
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        return document.Descendants()
            .Where(element => element.Attribute(x + "Name") is not null)
            .ToDictionary(element => element.Attribute(x + "Name")!.Value);
    }

    private static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
