using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class TeamYearSummaryPresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Workspace_route_and_dependency_injection_are_wired()
    {
        var workspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var button = workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "TeamYearSummaryButton");
        Assert.Equal("{x:Static strings:AppResources.TeamYearSummaryButton}",
            button.Attribute("Text")?.Value);
        Assert.Equal("OnTeamYearSummaryClicked", button.Attribute("Clicked")?.Value);
        Assert.Equal("{Binding IsTeamYearSummaryAvailable}",
            button.Attribute("IsVisible")?.Value);

        var codeBehind = Read("Views", "TeamWorkspacePage.xaml.cs");
        Assert.Contains("AppShell.TeamYearSummaryRoute", codeBehind);
        Assert.Contains("[AppShell.TeamIdQueryParameter] = viewModel.TeamId", codeBehind);
        var shell = Read("AppShell.xaml.cs");
        Assert.Contains("Routing.RegisterRoute(TeamYearSummaryRoute, typeof(Views.TeamYearSummaryPage))", shell);
        var startup = Read("MauiProgram.cs");
        Assert.Contains("AddSingleton<TeamYearSummaryRepository>()", startup);
        Assert.Contains("AddTransient<TeamYearSummaryViewModel>()", startup);
        Assert.Contains("AddTransient<TeamYearSummaryPage>()", startup);
    }

    [Fact]
    public void Workspace_menu_uses_centralized_year_summary_eligibility()
    {
        var viewModel = Read("ViewModels", "TeamWorkspaceViewModel.cs");
        Assert.Contains("await teamRepository.GetYearSummaryEligibilityAsync(TeamId)",
            viewModel);
        Assert.Contains("IsTeamYearSummaryAvailable = eligibility?.CanViewSummary == true;",
            viewModel);
        Assert.Contains("IsMarkTeamYearOverAvailable = eligibility?.CanMarkYearOver == true;",
            viewModel);

        var eligibility = Read("..", "SoftballScorekeeper.Core", "Models",
            "TeamYearSummaryEligibility.cs");
        Assert.Contains("HasSuccessor || IsYearOver", eligibility);
        Assert.Contains("!HasSuccessor && !IsYearOver", eligibility);

        var summaryRepository = Read("..", "SoftballScorekeeper.Infrastructure", "Data",
            "Repositories", "TeamYearSummaryRepository.cs");
        Assert.Contains("eligibility?.CanViewSummary != true", summaryRepository);
    }

    [Fact]
    public void Workspace_mark_year_over_action_is_conditional_confirmed_and_resource_backed()
    {
        var workspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var button = workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "MarkTeamYearOverButton");
        Assert.Equal("{x:Static strings:AppResources.MarkTeamYearOverButton}",
            button.Attribute("Text")?.Value);
        Assert.Equal("{Binding IsMarkTeamYearOverAvailable}",
            button.Attribute("IsVisible")?.Value);
        Assert.Equal("OnMarkTeamYearOverClicked", button.Attribute("Clicked")?.Value);

        var codeBehind = Read("Views", "TeamWorkspacePage.xaml.cs");
        Assert.Contains("MarkTeamYearOverConfirmationRequested = ConfirmMarkTeamYearOverAsync",
            codeBehind);
        Assert.Contains("AppResources.MarkTeamYearOverConfirmationMessage", codeBehind);
        Assert.Contains("AppResources.MarkTeamYearOverConfirmButton", codeBehind);
        Assert.Contains("AppResources.MarkTeamYearOverFinalConfirmationTitle", codeBehind);
        Assert.Contains("AppResources.MarkTeamYearOverFinalConfirmationMessage", codeBehind);
        Assert.Contains("AppResources.MarkTeamYearOverFinalConfirmButton", codeBehind);
        Assert.Contains("if (!continued)", codeBehind);
        Assert.Contains("if (confirmed && viewModel.ConfirmMarkTeamYearOverCommand.CanExecute(null))",
            codeBehind);
        Assert.Contains("ConfirmMarkTeamYearOverCommand.ExecuteAsync", codeBehind);

        var resources = XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper",
            "Resources", "Strings", "AppResources.resx"));
        var message = resources.Descendants("data").Single(item =>
            item.Attribute("name")?.Value == "MarkTeamYearOverConfirmationMessage")
            .Element("value")!.Value;
        Assert.Contains("permanently finalize", message);
        Assert.Contains("no longer be able", message);
        var finalMessage = resources.Descendants("data").Single(item =>
            item.Attribute("name")?.Value == "MarkTeamYearOverFinalConfirmationMessage")
            .Element("value")!.Value;
        Assert.Contains("cannot be undone", finalMessage);
        Assert.Contains("read-only forever", finalMessage);

        Assert.Equal("2", button.Attribute("BorderWidth")?.Value);
        var workspaceText = workspace.ToString();
        Assert.True(workspaceText.IndexOf("MarkTeamYearOverButton", StringComparison.Ordinal)
            < workspaceText.IndexOf("DeactivateTeamMenuItem", StringComparison.Ordinal));
        Assert.Contains("IsVisible=\"{Binding IsTeamYearOpen}\"", workspaceText);
    }

    [Fact]
    public void Finalized_year_mutation_actions_are_hidden_or_disabled_while_viewing_remains()
    {
        var workspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var workspaceText = workspace.ToString();
        Assert.Contains("IsTeamYearOpen", workspaceText);
        Assert.Equal("{Binding IsTeamYearOpen}", workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "CopyTeamToNewYearButton")
            .Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding IsTeamYearOpen}", workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "TeamBeerRulesButton")
            .Attribute("IsVisible")?.Value);
        Assert.DoesNotContain("IsTeamYearOpen", workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "TeamStatsButton").ToString());
        Assert.DoesNotContain("IsTeamYearOpen", workspace.Descendants().Single(item =>
            item.Attribute("AutomationId")?.Value == "TeamBeerTotalsButton").ToString());

        var workspaceViewModel = Read("ViewModels", "TeamWorkspaceViewModel.cs");
        Assert.Contains("CanMutateTeamYear() => CanManageTeam() && IsTeamYearOpen",
            workspaceViewModel);
        var gameWorkspace = Read("ViewModels", "GameWorkspaceViewModel.cs");
        Assert.Contains("ShowResumeGame => IsLoaded && IsTeamYearOpen", gameWorkspace);
        Assert.Contains("ShowResetGame => IsLoaded && IsTeamYearOpen", gameWorkspace);
        Assert.Contains("IsTeamYearOpen = !team.IsYearOver", gameWorkspace);
        var beerTotals = Read("ViewModels", "TeamBeerTotalsViewModel.cs");
        Assert.Contains("CanEdit => IsLoaded && IsTeamYearOpen", beerTotals);
        Assert.Contains("CanCalculateLowMan => IsLoaded && IsTeamYearOpen", beerTotals);
    }

    [Fact]
    public void Page_has_three_read_only_sections_and_reuses_batting_sorting()
    {
        var page = LoadXaml("Views", "TeamYearSummaryPage.xaml");
        foreach (var id in new[]
        {
            "TeamYearResultSection", "TeamYearBattingSection", "TeamYearBeerSection",
            "TeamYearGamesPlayed", "TeamYearWins", "TeamYearLosses", "TeamYearTies",
        })
            Assert.Single(page.Descendants().Where(item => item.Attribute("AutomationId")?.Value == id));

        Assert.Single(page.Descendants().Where(item => item.Name.LocalName == "BattingStatisticsGrid"));
        Assert.Single(page.Descendants().Where(item => item.Name.LocalName == "TeamYearBeerGrid"));
        var beerGrid = LoadXaml("Views", "Controls", "TeamYearBeerGrid.xaml");
        Assert.Empty(beerGrid.Descendants().Where(item =>
            item.Name.LocalName is "Button" or "Entry" or "Picker"));
        Assert.DoesNotContain("Finalize", page.ToString());
        Assert.DoesNotContain("Recalculate", page.ToString());

        var viewModel = Read("ViewModels", "TeamYearSummaryViewModel.cs");
        Assert.Contains("Statistics.Reload(projection.Batting.Players)", viewModel);
        Assert.DoesNotContain("hasLoaded", viewModel);
        var battingTable = Read("ViewModels", "BattingStatisticsTableViewModel.cs");
        Assert.Contains("BattingStatSortColumn.OnBasePercentage", battingTable);
        Assert.Contains("BattingStatisticsCalculator.CalculateTeam", battingTable);
    }

    [Fact]
    public void Page_uses_shared_orientation_independent_scrollable_tables_and_resource_text()
    {
        var page = LoadXaml("Views", "TeamYearSummaryPage.xaml");
        Assert.Empty(page.Descendants().Where(item =>
            item.Name.LocalName is "VisualStateManager.VisualStateGroups"));
        Assert.All(page.Descendants().Where(item => item.Name.LocalName == "Label")
            .Select(item => item.Attribute("Text")?.Value)
            .Where(value => value is not null && !value.StartsWith("{Binding")),
            value => Assert.StartsWith("{x:Static strings:AppResources.", value));

        var battingGrid = XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper",
            "Views", "Controls", "BattingStatisticsGrid.xaml"));
        var battingScrolls = battingGrid.Descendants()
            .Where(item => item.Name.LocalName == "ScrollView").ToArray();
        Assert.Contains(battingScrolls,
            scroll => scroll.Attribute("Orientation")?.Value == "Vertical");
        Assert.Contains(battingScrolls,
            scroll => scroll.Attribute("Orientation")?.Value == "Horizontal"
                && scroll.Attribute("HorizontalScrollBarVisibility")?.Value == "Always");

        var beerGrid = XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper",
            "Views", "Controls", "TeamYearBeerGrid.xaml"));
        var beerScrolls = beerGrid.Descendants()
            .Where(item => item.Name.LocalName == "ScrollView").ToArray();
        Assert.Contains(beerScrolls,
            scroll => scroll.Attribute("Orientation")?.Value == "Vertical");
        Assert.Contains(beerScrolls,
            scroll => scroll.Attribute("Orientation")?.Value == "Horizontal"
                && scroll.Attribute("HorizontalScrollBarVisibility")?.Value == "Always");
    }

    [Fact]
    public void Beer_grid_freezes_headers_and_player_column_without_adding_totals()
    {
        var grid = LoadXaml("Views", "Controls", "TeamYearBeerGrid.xaml");
        var ns = grid.Root!.Name.Namespace;
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var named = grid.Descendants().Where(item => item.Attribute(x + "Name") is not null)
            .ToDictionary(item => item.Attribute(x + "Name")!.Value);
        var playerRow = Assert.Single(named["TeamYearBeerPlayerNames"]
            .Descendants(ns + "DataTemplate")).Elements().Single();
        var statisticRow = Assert.Single(named["TeamYearBeerScrollableRows"]
            .Descendants(ns + "DataTemplate")).Elements().Single();

        Assert.Equal("Auto,*", named["TeamYearBeerTable"].Attribute("RowDefinitions")?.Value);
        Assert.Equal("182,*", named["TeamYearBeerHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("182,*", named["TeamYearBeerBody"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("180,2", named["TeamYearBeerPlayerHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("180,2", playerRow.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("72,88,72,72,88,100,88,100,88",
            named["TeamYearBeerScrollableHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("72,88,72,72,88,100,88,100,88",
            statisticRow.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("48,1", playerRow.Attribute("RowDefinitions")?.Value);
        Assert.Equal("48,1", statisticRow.Attribute("RowDefinitions")?.Value);
        var playerName = Assert.Single(playerRow.Elements(ns + "Label"));
        Assert.Equal("Bold", playerName.Attribute("FontAttributes")?.Value);
        Assert.Equal("TailTruncation", playerName.Attribute("LineBreakMode")?.Value);
        Assert.Equal("1", playerName.Attribute("MaxLines")?.Value);
        Assert.Equal("{Binding BeerRows}", named["TeamYearBeerPlayerNames"]
            .Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Equal("{Binding BeerRows}", named["TeamYearBeerScrollableRows"]
            .Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Empty(named["TeamYearBeerHeader"].Ancestors(ns + "ScrollView"));
        Assert.Contains(named["TeamYearBeerVerticalScroll"],
            named["TeamYearBeerHorizontalScroll"].Ancestors());
        Assert.Equal("OnStatisticsScrolled",
            named["TeamYearBeerHorizontalScroll"].Attribute("Scrolled")?.Value);
        Assert.Equal(new[]
        {
            "{x:Static strings:AppResources.BeerTotalsDboHeader}",
            "{x:Static strings:AppResources.BeerTotalsSoloHomeRunHeader}",
            "{x:Static strings:AppResources.BeerTotalsDoublePlayHeader}",
            "{x:Static strings:AppResources.BeerTotalsTriplePlayHeader}",
            "{x:Static strings:AppResources.BeerTotalsTwoOutsHeader}",
            "{x:Static strings:AppResources.BeerTotalsStrikeoutHeader}",
            "{x:Static strings:AppResources.BeerTotalsLowManHeader}",
            "{x:Static strings:AppResources.BeerTotalsBroughtHeader}",
            "{x:Static strings:AppResources.BeerTotalsOwedHeader}",
        }, named["TeamYearBeerScrollableHeader"].Elements(ns + "Label")
            .Select(item => item.Attribute("Text")?.Value));
        Assert.Contains("TeamYearBeerScrollableHeader.TranslationX = -e.ScrollX",
            Read("Views", "Controls", "TeamYearBeerGrid.xaml.cs"));
        Assert.DoesNotContain("TeamTotal", grid.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));
}
