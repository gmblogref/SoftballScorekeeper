using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class CompletedGameStatGridRefinementTests
{
    private const string ExpectedColumns =
        "180,2,52,52,52,52,52,52,52,52,52,52,68,68,68";

    [Fact]
    public void Portrait_and_landscape_share_one_scrollable_scorebook_grid()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var table = named["CompletedStatsTable"];

        Assert.Equal("Both", table.Parent!.Attribute("Orientation")?.Value);
        Assert.Equal("CompletedBattingStats",
            table.Parent.Attribute("AutomationId")?.Value);
        Assert.Equal("926", table.Attribute("MinimumWidthRequest")?.Value);
        Assert.Equal("Start", table.Attribute("HorizontalOptions")?.Value);
        Assert.Empty(table.Descendants(ns + "Border"));
        Assert.Single(page.Descendants().Where(element =>
            element.Attribute("BindableLayout.ItemsSource")?.Value
                == "{Binding CompletedBattingRows}"));
        Assert.DoesNotContain(page.Descendants(ns + "Setter"), setter =>
            setter.Attribute("TargetName")?.Value.Contains("Stats", StringComparison.Ordinal)
                == true);
    }

    [Fact]
    public void Shared_scrollable_table_exposes_rate_columns_after_sacrifice_flies()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var header = named["CompletedStatsHeader"];
        var playerGrid = named["CompletedStatsPlayerRows"]
            .Descendants(ns + "DataTemplate").Single().Elements().Single();
        var teamTotal = named["CompletedStatsTeamTotal"];

        AssertColumns(header, "Button", new[]
        {
            ("{Binding SacrificeFliesStatSortHeader}", "11"),
            ("{Binding BattingAverageStatSortHeader}", "12"),
            ("{Binding OnBasePercentageStatSortHeader}", "13"),
            ("{Binding SluggingPercentageStatSortHeader}", "14"),
        });
        AssertColumns(playerGrid, "Label", new[]
        {
            ("{Binding SacrificeFlies}", "11"),
            ("{Binding BattingAverage}", "12"),
            ("{Binding OnBasePercentage}", "13"),
            ("{Binding SluggingPercentage}", "14"),
        });
        AssertColumns(teamTotal, "Label", new[]
        {
            ("{Binding CompletedBattingTeamTotal.SacrificeFlies}", "11"),
            ("{Binding CompletedBattingTeamTotal.BattingAverage}", "12"),
            ("{Binding CompletedBattingTeamTotal.OnBasePercentage}", "13"),
            ("{Binding CompletedBattingTeamTotal.SluggingPercentage}", "14"),
        });
    }

    [Fact]
    public void Header_player_rows_and_team_total_use_the_same_ordered_columns()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var header = named["CompletedStatsHeader"];
        var playerRows = named["CompletedStatsPlayerRows"];
        var playerGrid = Assert.Single(playerRows.Descendants(ns + "DataTemplate")).Elements()
            .Single();
        var teamTotal = named["CompletedStatsTeamTotal"];

        Assert.Equal(ExpectedColumns, header.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(ExpectedColumns, playerGrid.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(ExpectedColumns, teamTotal.Attribute("ColumnDefinitions")?.Value);

        var headings = header.Elements(ns + "Button")
            .Select(button => button.Attribute("Text")?.Value)
            .ToArray();
        Assert.Equal(new[]
        {
            "{Binding PlayerStatSortHeader}",
            "{Binding PlateAppearancesStatSortHeader}",
            "{Binding AtBatsStatSortHeader}",
            "{Binding HitsStatSortHeader}",
            "{Binding SinglesStatSortHeader}",
            "{Binding DoublesStatSortHeader}",
            "{Binding TriplesStatSortHeader}",
            "{Binding HomeRunsStatSortHeader}",
            "{Binding WalksStatSortHeader}",
            "{Binding FielderChoicesStatSortHeader}",
            "{Binding SacrificeFliesStatSortHeader}",
            "{Binding BattingAverageStatSortHeader}",
            "{Binding OnBasePercentageStatSortHeader}",
            "{Binding SluggingPercentageStatSortHeader}",
        }, headings);
        Assert.All(header.Elements(ns + "Button"), button =>
        {
            Assert.Equal("{Binding SortCompletedBattingRowsCommand}",
                button.Attribute("Command")?.Value);
            Assert.StartsWith("{x:Static statistics:BattingStatSortColumn.",
                button.Attribute("CommandParameter")?.Value);
            Assert.Equal("{StaticResource CompletedStatHeaderButton}",
                button.Attribute("Style")?.Value);
        });
        Assert.Equal(
            Enum.GetNames<SoftballScorekeeper.Core.Statistics.BattingStatSortColumn>()
                .Select(name =>
                    $"{{x:Static statistics:BattingStatSortColumn.{name}}}"),
            header.Elements(ns + "Button")
                .Select(button => button.Attribute("CommandParameter")?.Value));

        Assert.Contains(teamTotal, playerRows.ElementsAfterSelf());
        Assert.Equal("{Binding CompletedBattingTeamTotal.PlayerName}",
            teamTotal.Elements(ns + "Label").First().Attribute("Text")?.Value);
    }

    [Fact]
    public void Plate_appearances_and_all_stats_are_centered_in_explicit_columns()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var valueStyle = page.Descendants(ns + "Style").Single(style =>
            style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2009/xaml"))?.Value
                == "CompletedStatValue");
        var playerGrid = named["CompletedStatsPlayerRows"]
            .Descendants(ns + "DataTemplate").Single().Elements().Single();
        var pa = playerGrid.Elements(ns + "Label").Single(label =>
            label.Attribute("Text")?.Value == "{Binding PlateAppearances}");

        Assert.Equal("2", pa.Attribute("Grid.Column")?.Value);
        Assert.Equal("{Binding PlateAppearancesAutomationId}",
            pa.Attribute("AutomationId")?.Value);
        Assert.Equal("{StaticResource CompletedStatValue}", pa.Attribute("Style")?.Value);
        Assert.Contains(valueStyle.Elements(ns + "Setter"), IsCenteredSetter);
        Assert.All(playerGrid.Elements(ns + "Label").Skip(1), label =>
            Assert.Equal("{StaticResource CompletedStatValue}", label.Attribute("Style")?.Value));
    }

    [Fact]
    public void Grid_separators_team_total_loading_error_and_no_resume_behavior_are_preserved()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var named = NamedElements(page);
        var viewModel = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "GameWorkspaceViewModel.cs"));
        var resources = XDocument.Load(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Resources", "Strings", "AppResources.resx"));

        Assert.Contains(named["CompletedStatsHeader"].Elements(ns + "BoxView"), box =>
            box.Attribute("Grid.Column")?.Value == "1"
                && box.Attribute("Grid.RowSpan")?.Value == "2");
        Assert.Contains(named["CompletedStatsHeader"].Elements(ns + "BoxView"), box =>
            box.Attribute("Grid.Row")?.Value == "1"
                && box.Attribute("Grid.ColumnSpan")?.Value == "15");
        Assert.Contains(named["CompletedStatsPlayerRows"].Descendants(ns + "BoxView"), box =>
            box.Attribute("Grid.Row")?.Value == "1"
                && box.Attribute("Grid.ColumnSpan")?.Value == "15");
        Assert.Equal("2,46", named["CompletedStatsTeamTotal"]
            .Attribute("RowDefinitions")?.Value);
        Assert.Contains("BattingStatisticsCalculator.CalculateTeam", viewModel);
        Assert.Contains("AppResources.CompletedGameLoadFailedMessage", viewModel);
        Assert.Contains("HasScoringSession && !IsScoringCompleted", viewModel);
        Assert.Contains("BattingStatSortColumn.OnBasePercentage", viewModel);
        Assert.Contains("BattingStatSortDirection.Descending", viewModel);
        Assert.Contains("CompletedBattingRows.Move", viewModel);
        Assert.Contains("BattingStatSortState", viewModel);
        Assert.Contains(".Select(column)", viewModel);
        Assert.Contains("AppResources.ActiveStatSortHeaderFormat", viewModel);
        Assert.Contains("AppResources.SortAscendingIndicator", viewModel);
        Assert.Contains("AppResources.SortDescendingIndicator", viewModel);
        Assert.Contains(resources.Descendants("data"), item =>
            item.Attribute("name")?.Value == "SortAscendingIndicator"
                && item.Element("value")?.Value == "▲");
        Assert.Contains(resources.Descendants("data"), item =>
            item.Attribute("name")?.Value == "SortDescendingIndicator"
                && item.Element("value")?.Value == "▼");
        Assert.DoesNotContain(named["CompletedGameGrid"].Descendants(ns + "Button"), button =>
            button.Attribute("AutomationId")?.Value == "ResumeGameButton");
    }

    private static bool IsCenteredSetter(XElement setter) =>
        setter.Attribute("Property")?.Value == "HorizontalTextAlignment"
            && setter.Attribute("Value")?.Value == "Center";

    private static void AssertColumns(
        XElement grid,
        string elementName,
        IEnumerable<(string Binding, string Column)> expected)
    {
        var ns = grid.Name.Namespace;
        foreach (var (binding, column) in expected)
        {
            var label = grid.Elements(ns + elementName).Single(item =>
                item.Attribute("Text")?.Value == binding);
            Assert.Equal(column, label.Attribute("Grid.Column")?.Value);
        }
    }

    private static XDocument LoadPage() => XDocument.Load(Path.Combine(
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
