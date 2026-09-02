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

        AssertColumns(header, new[]
        {
            ("{x:Static strings:AppResources.SacrificeFliesStatHeading}", "11"),
            ("{x:Static strings:AppResources.BattingAverageStatHeading}", "12"),
            ("{x:Static strings:AppResources.OnBasePercentageStatHeading}", "13"),
            ("{x:Static strings:AppResources.SluggingPercentageStatHeading}", "14"),
        });
        AssertColumns(playerGrid, new[]
        {
            ("{Binding SacrificeFlies}", "11"),
            ("{Binding BattingAverage}", "12"),
            ("{Binding OnBasePercentage}", "13"),
            ("{Binding SluggingPercentage}", "14"),
        });
        AssertColumns(teamTotal, new[]
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

        var headings = header.Elements(ns + "Label")
            .Select(label => label.Attribute("Text")?.Value)
            .ToArray();
        Assert.Equal(new[]
        {
            "{x:Static strings:AppResources.PlayerStatHeading}",
            "{x:Static strings:AppResources.PlateAppearancesStatHeading}",
            "{x:Static strings:AppResources.AtBatsStatHeading}",
            "{x:Static strings:AppResources.HitsStatHeading}",
            "{x:Static strings:AppResources.SinglesStatHeading}",
            "{x:Static strings:AppResources.DoublesStatHeading}",
            "{x:Static strings:AppResources.TriplesStatHeading}",
            "{x:Static strings:AppResources.HomeRunsStatHeading}",
            "{x:Static strings:AppResources.WalksStatHeading}",
            "{x:Static strings:AppResources.FielderChoicesStatHeading}",
            "{x:Static strings:AppResources.SacrificeFliesStatHeading}",
            "{x:Static strings:AppResources.BattingAverageStatHeading}",
            "{x:Static strings:AppResources.OnBasePercentageStatHeading}",
            "{x:Static strings:AppResources.SluggingPercentageStatHeading}",
        }, headings);

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
        var headingStyle = page.Descendants(ns + "Style").Single(style =>
            style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2009/xaml"))?.Value
                == "CompletedStatHeading");
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
        Assert.Contains(headingStyle.Elements(ns + "Setter"), IsCenteredSetter);
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
        Assert.DoesNotContain(named["CompletedGameGrid"].Descendants(ns + "Button"), button =>
            button.Attribute("AutomationId")?.Value == "ResumeGameButton");
    }

    private static bool IsCenteredSetter(XElement setter) =>
        setter.Attribute("Property")?.Value == "HorizontalTextAlignment"
            && setter.Attribute("Value")?.Value == "Center";

    private static void AssertColumns(
        XElement grid,
        IEnumerable<(string Binding, string Column)> expected)
    {
        var ns = grid.Name.Namespace;
        foreach (var (binding, column) in expected)
        {
            var label = grid.Elements(ns + "Label").Single(item =>
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
