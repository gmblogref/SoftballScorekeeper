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
    public void Workspace_menu_uses_successor_existence_for_year_summary_availability()
    {
        var viewModel = Read("ViewModels", "TeamWorkspaceViewModel.cs");
        Assert.Contains("var hasSuccessor = await teamRepository.HasSuccessorAsync(TeamId);",
            viewModel);
        Assert.Contains("IsTeamYearSummaryAvailable = hasSuccessor;", viewModel);
        Assert.Contains("IsTeamYearSummaryAvailable = false;", viewModel);

        var repository = Read("..", "SoftballScorekeeper.Infrastructure", "Data",
            "Repositories", "TeamRepository.cs");
        Assert.Contains("team.PreviousTeamId == teamId", repository);
        Assert.DoesNotContain("DateTime", repository);
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

        foreach (var relativePath in new[]
        {
            Path.Combine("Views", "Controls", "BattingStatisticsGrid.xaml"),
            Path.Combine("Views", "Controls", "TeamYearBeerGrid.xaml"),
        })
        {
            var grid = XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper", relativePath));
            var scroll = grid.Descendants().Single(item => item.Name.LocalName == "ScrollView");
            Assert.Equal("Both", scroll.Attribute("Orientation")?.Value);
            Assert.Equal("Always", scroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        }
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));
}
