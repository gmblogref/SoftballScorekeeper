using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class CompletedGameStatGridRefinementTests
{
    private const string PlayerColumns = "180,2";
    private const string StatisticColumns =
        "52,52,52,52,52,52,52,52,52,52,52,68,68,68";

    [Fact]
    public void Shared_grid_freezes_header_and_player_column_with_one_owner_per_scroll_axis()
    {
        var page = LoadGrid();
        var named = NamedElements(page);
        var table = named["CompletedStatsTable"];
        var header = named["CompletedStatsHeader"];
        var vertical = named["CompletedStatsVerticalScroll"];
        var horizontal = named["CompletedStatsHorizontalScroll"];

        Assert.Equal("Auto,*", table.Attribute("RowDefinitions")?.Value);
        Assert.Equal(table, header.Parent);
        Assert.Equal(table, vertical.Parent);
        Assert.Equal("1", vertical.Attribute("Grid.Row")?.Value);
        Assert.Equal("Vertical", vertical.Attribute("Orientation")?.Value);
        Assert.Equal("Horizontal", horizontal.Attribute("Orientation")?.Value);
        Assert.Equal("CompletedBattingStats", horizontal.Attribute("AutomationId")?.Value);
        Assert.Equal("OnStatisticsScrolled", horizontal.Attribute("Scrolled")?.Value);
        Assert.DoesNotContain(header.Ancestors(), item => item.Name.LocalName == "ScrollView");
        Assert.Contains(horizontal.Ancestors(), item => item == vertical);
        Assert.Single(page.Descendants().Where(item =>
            item.Name.LocalName == "ScrollView"
            && item.Attribute("Orientation")?.Value == "Vertical"));
        Assert.Single(page.Descendants().Where(item =>
            item.Name.LocalName == "ScrollView"
            && item.Attribute("Orientation")?.Value == "Horizontal"));
    }

    [Fact]
    public void Player_names_and_statistics_share_vertical_scrolling_and_fixed_row_heights()
    {
        var page = LoadGrid();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var playerTemplate = ItemTemplateGrid(named["CompletedStatsPlayerNames"], ns);
        var statisticTemplate = ItemTemplateGrid(named["CompletedStatsPlayerRows"], ns);

        Assert.Equal("{Binding Rows}", named["CompletedStatsPlayerNames"]
            .Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Equal("{Binding Rows}", named["CompletedStatsPlayerRows"]
            .Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Equal("44,1", playerTemplate.Attribute("RowDefinitions")?.Value);
        Assert.Equal("44,1", statisticTemplate.Attribute("RowDefinitions")?.Value);
        Assert.Equal(PlayerColumns, playerTemplate.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns,
            statisticTemplate.Attribute("ColumnDefinitions")?.Value);
        Assert.Contains(named["CompletedStatsPlayerNames"].Ancestors(),
            item => item == named["CompletedStatsVerticalScroll"]);
        Assert.Contains(named["CompletedStatsPlayerRows"].Ancestors(),
            item => item == named["CompletedStatsVerticalScroll"]);
        Assert.Contains(named["CompletedStatsPlayerRows"].Ancestors(),
            item => item == named["CompletedStatsHorizontalScroll"]);
    }

    [Fact]
    public void Fixed_and_scrolling_headers_match_body_column_widths_and_order()
    {
        var page = LoadGrid();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var playerHeader = named["CompletedStatsPlayerHeader"];
        var statisticHeader = named["CompletedStatsScrollableHeader"];
        var statisticRow = ItemTemplateGrid(named["CompletedStatsPlayerRows"], ns);
        var totalValues = named["CompletedStatsTeamTotalValues"];

        Assert.Equal("182,*", named["CompletedStatsHeader"]
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("182,*", named["CompletedStatsBody"]
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(PlayerColumns, playerHeader.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(PlayerColumns, named["CompletedStatsTeamTotal"]
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns, statisticHeader
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns, statisticRow
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns, totalValues
            .Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("776", statisticHeader.Attribute("WidthRequest")?.Value);
        Assert.Equal("776", named["CompletedStatsScrollableRows"]
            .Attribute("WidthRequest")?.Value);

        AssertColumns(statisticHeader, "Button", new[]
        {
            ("{Binding SacrificeFliesStatSortHeader}", "9"),
            ("{Binding BattingAverageStatSortHeader}", "11"),
            ("{Binding OnBasePercentageStatSortHeader}", "12"),
            ("{Binding SluggingPercentageStatSortHeader}", "13"),
        });
        AssertColumns(statisticHeader, "Label",
            new[] { ("{Binding OutsStatHeader}", "10") });
        AssertColumns(statisticRow, "Label", new[]
        {
            ("{Binding SacrificeFlies}", "9"),
            ("{Binding Outs}", "10"),
            ("{Binding BattingAverage}", "11"),
            ("{Binding OnBasePercentage}", "12"),
            ("{Binding SluggingPercentage}", "13"),
        });
        AssertColumns(totalValues, "Label", new[]
        {
            ("{Binding TeamTotal.SacrificeFlies}", "9"),
            ("{Binding TeamTotal.Outs}", "10"),
            ("{Binding TeamTotal.BattingAverage}", "11"),
            ("{Binding TeamTotal.OnBasePercentage}", "12"),
            ("{Binding TeamTotal.SluggingPercentage}", "13"),
        });
    }

    [Fact]
    public void Horizontal_owner_drives_clipped_header_translation()
    {
        var page = LoadGrid();
        var named = NamedElements(page);
        var headerViewport = named["CompletedStatsScrollableHeader"].Parent!;
        var codeBehind = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Views", "Controls",
            "BattingStatisticsGrid.xaml.cs"));

        Assert.Equal("True", headerViewport.Attribute("IsClippedToBounds")?.Value);
        Assert.Contains("OnStatisticsScrolled", codeBehind);
        Assert.Contains("CompletedStatsScrollableHeader.TranslationX = -e.ScrollX;",
            codeBehind);
    }

    [Fact]
    public void Team_total_is_the_final_scrolling_row_and_is_not_a_sticky_footer()
    {
        var page = LoadGrid();
        var named = NamedElements(page);
        var playerTotal = named["CompletedStatsTeamTotal"];
        var statisticTotal = named["CompletedStatsTeamTotalValues"];

        Assert.Equal(named["CompletedStatsPlayerColumn"], playerTotal.Parent);
        Assert.Equal(named["CompletedStatsScrollableRows"], statisticTotal.Parent);
        Assert.Equal(playerTotal, named["CompletedStatsPlayerNames"].ElementsAfterSelf().Last());
        Assert.Equal(statisticTotal,
            named["CompletedStatsPlayerRows"].ElementsAfterSelf().Last());
        Assert.Contains(playerTotal.Ancestors(),
            item => item == named["CompletedStatsVerticalScroll"]);
        Assert.Contains(statisticTotal.Ancestors(),
            item => item == named["CompletedStatsVerticalScroll"]);
        Assert.Contains(statisticTotal.Ancestors(),
            item => item == named["CompletedStatsHorizontalScroll"]);
        Assert.Equal("2,46", playerTotal.Attribute("RowDefinitions")?.Value);
        Assert.Equal("2,46", statisticTotal.Attribute("RowDefinitions")?.Value);
        Assert.DoesNotContain(playerTotal.Ancestors(), item => item == named["CompletedStatsHeader"]);
    }

    [Fact]
    public void Sorting_remains_bound_to_whole_rows_and_is_disabled_during_editing()
    {
        var page = LoadGrid();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var headings = named["CompletedStatsHeader"].Descendants(ns + "Button").ToArray();
        var tableViewModel = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "BattingStatisticsTableViewModel.cs"));

        Assert.Equal(14, headings.Length);
        Assert.All(headings, heading =>
        {
            Assert.Equal("{Binding SortRowsCommand}", heading.Attribute("Command")?.Value);
            Assert.StartsWith("{x:Static statistics:BattingStatSortColumn.",
                heading.Attribute("CommandParameter")?.Value);
        });
        Assert.Contains("Rows.Move", tableViewModel);
        Assert.Contains("private bool CanSortRows() => !IsEditing;", tableViewModel);
        Assert.Contains("BattingStatSortColumn.OnBasePercentage", tableViewModel);
        Assert.Contains("BattingStatSortDirection.Descending", tableViewModel);
        Assert.Contains("SortRowsCommand.NotifyCanExecuteChanged();", tableViewModel);
    }

    [Fact]
    public void Shared_grid_consumers_keep_one_responsive_control_instance()
    {
        AssertConsumer("GameWorkspacePage.xaml", "{Binding CompletedBattingStatistics}");
        AssertConsumer("TeamStatsPage.xaml", "{Binding Statistics}");
        AssertConsumer("TeamYearSummaryPage.xaml", "{Binding Statistics}");

        var grid = LoadGrid();
        var table = NamedElements(grid)["CompletedStatsTable"];
        Assert.Null(table.Attribute("WidthRequest"));
        Assert.Null(table.Attribute("HeightRequest"));
        Assert.Equal("Auto,*", table.Attribute("RowDefinitions")?.Value);
    }

    [Fact]
    public void Grid_separators_calculation_loading_error_and_no_resume_behavior_are_preserved()
    {
        var page = LoadGrid();
        var workspace = LoadWorkspace();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var workspaceViewModel = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "GameWorkspaceViewModel.cs"));
        var tableViewModel = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "BattingStatisticsTableViewModel.cs"));

        Assert.Contains(named["CompletedStatsPlayerHeader"].Elements(ns + "BoxView"), box =>
            box.Attribute("Grid.Column")?.Value == "1"
                && box.Attribute("Grid.RowSpan")?.Value == "2");
        Assert.Contains(named["CompletedStatsScrollableHeader"].Elements(ns + "BoxView"), box =>
            box.Attribute("Grid.Row")?.Value == "1"
                && box.Attribute("Grid.ColumnSpan")?.Value == "14");
        Assert.Contains("BattingStatisticsCalculator.CalculateTeam", tableViewModel);
        Assert.Contains("AppResources.CompletedGameLoadFailedMessage", workspaceViewModel);
        Assert.Contains("HasScoringSession && !IsScoringCompleted", workspaceViewModel);
        var workspaceNamed = NamedElements(workspace);
        Assert.DoesNotContain(workspaceNamed["CompletedGameGrid"].Descendants(ns + "Button"),
            button => button.Attribute("AutomationId")?.Value == "ResumeGameButton");
    }

    private static void AssertConsumer(string fileName, string bindingContext)
    {
        var page = XDocument.Load(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Views", fileName));
        var control = Assert.Single(page.Descendants().Where(item =>
            item.Name.LocalName == "BattingStatisticsGrid"));
        Assert.Equal(bindingContext, control.Attribute("BindingContext")?.Value);
    }

    private static XElement ItemTemplateGrid(XElement layout, XNamespace ns) =>
        Assert.Single(layout.Descendants(ns + "DataTemplate")).Elements().Single();

    private static void AssertColumns(
        XElement grid,
        string elementName,
        IEnumerable<(string Binding, string Column)> expected)
    {
        var ns = grid.Name.Namespace;
        foreach (var (binding, column) in expected)
        {
            var item = grid.Elements(ns + elementName).Single(element =>
                element.Attribute("Text")?.Value == binding);
            Assert.Equal(column, item.Attribute("Grid.Column")?.Value ?? "0");
        }
    }

    private static XDocument LoadGrid() => XDocument.Load(Path.Combine(
        RepositoryRoot, "SoftballScorekeeper", "Views", "Controls",
        "BattingStatisticsGrid.xaml"));

    private static XDocument LoadWorkspace() => XDocument.Load(Path.Combine(
        RepositoryRoot, "SoftballScorekeeper", "Views", "GameWorkspacePage.xaml"));

    private static Dictionary<string, XElement> NamedElements(XDocument page)
    {
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        return page.Descendants().Where(element => element.Attribute(x + "Name") is not null)
            .ToDictionary(element => element.Attribute(x + "Name")!.Value);
    }

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
}
