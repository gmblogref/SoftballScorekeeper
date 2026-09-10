using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class TeamStatsPresentationTests
{
    [Fact]
    public void Team_workspace_navigates_to_dedicated_team_stats_page()
    {
        var workspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var ns = workspace.Root!.Name.Namespace;
        var menuItem = Assert.Single(workspace.Descendants(ns + "Button").Where(item =>
            item.Attribute("AutomationId")?.Value == "TeamStatsButton"));
        var menuTrigger = Assert.Single(workspace.Descendants(ns + "ToolbarItem").Where(item =>
            item.Attribute("AutomationId")?.Value == "TeamWorkspaceMenuButton"));
        var code = Read("Views", "TeamWorkspacePage.xaml.cs");
        var shell = Read("AppShell.xaml.cs");
        var composition = Read("MauiProgram.cs");

        Assert.Equal("OnTeamStatsClicked", menuItem.Attribute("Clicked")?.Value);
        Assert.Equal("OnTeamMenuClicked", menuTrigger.Attribute("Clicked")?.Value);
        Assert.Equal("Primary", menuTrigger.Attribute("Order")?.Value);
        Assert.Contains("TeamStatsRoute", code);
        Assert.Contains("Routing.RegisterRoute(TeamStatsRoute, typeof(Views.TeamStatsPage))", shell);
        Assert.Contains("AddSingleton<TeamBattingStatisticsRepository>()", composition);
        Assert.Contains("AddTransient<TeamStatsViewModel>()", composition);
    }

    [Fact]
    public void Team_stats_reuses_shared_grid_and_exposes_supported_filters()
    {
        var page = LoadXaml("Views", "TeamStatsPage.xaml");
        var ns = page.Root!.Name.Namespace;
        var buttons = page.Descendants(ns + "Button").ToDictionary(
            item => item.Attribute("AutomationId")?.Value ?? string.Empty);

        Assert.Contains(page.Descendants(), item => item.Name.LocalName == "BattingStatisticsGrid");
        Assert.Equal("{Binding ShowAllGamesCommand}",
            buttons["TeamStatsAllGamesFilter"].Attribute("Command")?.Value);
        Assert.Equal("{Binding ShowLastGameNightCommand}",
            buttons["TeamStatsLastGameNightFilter"].Attribute("Command")?.Value);
        Assert.Equal("OnCustomClicked",
            buttons["TeamStatsCustomFilter"].Attribute("Clicked")?.Value);
        Assert.Equal("{Binding ShowCurrentLeagueSeasonCommand}",
            buttons["TeamStatsCurrentLeagueSeasonFilter"].Attribute("Command")?.Value);
        Assert.DoesNotContain("TeamStatsCurrentLeagueSeasonUnavailable", page.ToString());
        Assert.DoesNotContain("TeamStatsLastTwoGamesFilter", page.ToString());
        Assert.DoesNotContain("ShowLastTwoGamesCommand", Read("ViewModels", "TeamStatsViewModel.cs"));
    }

    [Fact]
    public void Custom_dialog_has_deferred_touch_friendly_selection_actions()
    {
        var dialog = LoadXaml("Views", "TeamStatsCustomFilterDialog.xaml");
        var ns = dialog.Root!.Name.Namespace;
        var source = Read("Views", "TeamStatsCustomFilterDialog.xaml.cs");

        Assert.Contains(dialog.Descendants(ns + "Grid"), item =>
            item.Attribute("MinimumHeightRequest")?.Value == "58");
        Assert.Contains("OnSelectAllClicked", source);
        Assert.Contains("OnClearAllClicked", source);
        Assert.Contains("OnApplyClicked", source);
        Assert.Contains("OnCancelClicked", source);
        Assert.Contains("completion.TrySetResult(selectedIds)", source);
        Assert.Contains("new TeamStatsGameSelectionItem", source);
    }

    [Fact]
    public void Filter_reload_preserves_sort_state_and_recalculates_team_total()
    {
        var table = Read("ViewModels", "BattingStatisticsTableViewModel.cs");
        var stats = Read("ViewModels", "TeamStatsViewModel.cs");

        Assert.Contains("public void Reload", table);
        Assert.Contains("Rows.Clear();", table);
        Assert.Contains("ApplySort();", table);
        Assert.Contains("BattingStatisticsCalculator.CalculateTeam", table);
        Assert.Contains("Statistics.Reload(projection.Players)", stats);
        Assert.Contains("TeamStatsGameFilter.AllGames", stats);
        Assert.Contains("selectedIds.Count == 0", stats);
    }

    private static XDocument LoadXaml(params string[] path) => XDocument.Load(
        Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray()));

    private static string Read(params string[] path) => File.ReadAllText(
        Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray()));

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
}
