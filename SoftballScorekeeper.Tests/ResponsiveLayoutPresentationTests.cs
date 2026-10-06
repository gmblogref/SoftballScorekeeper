using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ResponsiveLayoutPresentationTests
{
    [Fact]
    public void Shared_layout_state_classifies_actual_width_and_height()
    {
        var state = Read("Services", "ResponsiveLayoutState.cs");
        var app = Read("App.xaml.cs");
        var styles = LoadXaml("Resources", "Styles", "Styles.xaml");

        Assert.Contains("NarrowPortraitMaximumWidth", state);
        Assert.Contains("CompactPortraitMaximumHeight", state);
        Assert.Contains("CompactLandscapeMaximumWidth", state);
        Assert.Contains("CompactLandscapeMaximumHeight", state);
        Assert.Contains("availableWidth <= availableHeight", state);
        Assert.Contains("ResponsiveLayoutProfile.CompactLandscape", state);
        Assert.Contains("window.SizeChanged += OnWindowSizeChanged", app);
        Assert.Contains("ResponsiveLayoutState.Current.Update(window.Width, window.Height)", app);
        Assert.Contains(
            styles.Descendants(),
            element =>
                element.Attribute(XName.Get("Key", XamlNamespace))?.Value == "ResponsivePageGrid"
        );
    }

    [Fact]
    public void Team_workflows_use_compact_context_header_without_global_section_buttons()
    {
        var teamPages = new[]
        {
            "AddGamePage.xaml",
            "AddPlayersToTeamPage.xaml",
            "EditGamePage.xaml",
            "GameWorkspacePage.xaml",
            "TeamBeerTotalsPage.xaml",
            "TeamStatsPage.xaml",
        };

        foreach (var pageName in teamPages)
        {
            var page = LoadXaml("Views", pageName);
            Assert.Contains(
                page.Descendants(),
                element => element.Name.LocalName == "TeamContextHeader"
            );
            Assert.DoesNotContain(
                page.Descendants(),
                element => element.Name.LocalName == "PrimarySectionNavigation"
            );
        }

        var workspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        Assert.DoesNotContain(
            workspace.Descendants(),
            element => element.Name.LocalName == "PrimarySectionNavigation"
        );

        foreach (var rootPage in new[] { "MainPage.xaml", "PlayerListPage.xaml" })
            Assert.Contains(
                LoadXaml("Views", rootPage).Descendants(),
                element => element.Name.LocalName == "PrimarySectionNavigation"
            );
    }

    [Fact]
    public void Add_players_places_list_actions_by_heading_and_keeps_primary_actions_responsive()
    {
        var page = LoadXaml("Views", "AddPlayersToTeamPage.xaml");
        var selectAll = ByAutomationId(page, "AddPlayersSelectAllButton");
        var clearAll = ByAutomationId(page, "AddPlayersClearAllButton");
        var create = ByAutomationId(page, "CreatePlayerFromTeamButton");
        var addSelected = ByAutomationId(page, "AddSelectedPlayersToTeamButton");

        Assert.Same(selectAll.Parent, clearAll.Parent);
        Assert.Contains(
            selectAll.Parent!.Parent!.Elements(),
            element =>
                element.Name.LocalName == "Label"
                && element.Attribute("Text")?.Value.Contains("EligiblePlayersHeading") == true
        );
        Assert.Same(create.Parent, addSelected.Parent);
        Assert.Contains(
            addSelected.Descendants(),
            element =>
                element.Name.LocalName == "DataTrigger"
                && element.Attribute("Value")?.Value.Contains("NarrowPortrait") == true
        );
        Assert.Null(create.Attribute("MinimumWidthRequest"));
        Assert.Null(addSelected.Attribute("MinimumWidthRequest"));
    }

    [Fact]
    public void Scoring_has_short_landscape_density_without_reducing_touch_targets()
    {
        var page = LoadXaml("Views", "ScoringPage.xaml");
        var code = Read("Views", "ScoringPage.xaml.cs");
        var compactState = page.Descendants()
            .Single(element =>
                element.Name.LocalName == "VisualState"
                && element.Attribute(XName.Get("Name", XamlNamespace))?.Value
                    == "CompactLandscapeScoreboard"
            );

        Assert.Contains("ResponsiveLayoutState.Classify(width, height)", code);
        Assert.Contains("CompactLandscapeScoreboard", code);
        Assert.Contains(
            compactState.Descendants(),
            setter =>
                setter.Attribute("TargetName")?.Value == "HomeScoreValue"
                && setter.Attribute("Property")?.Value == "Label.FontSize"
                && setter.Attribute("Value")?.Value == "28"
        );
        foreach (
            var automationId in new[]
            {
                "FirstBaseRunnerButton",
                "SecondBaseRunnerButton",
                "ThirdBaseRunnerButton",
            }
        )
            Assert.Contains(
                compactState.Descendants(),
                setter =>
                    setter.Attribute("TargetName")?.Value == automationId
                    && setter.Attribute("Property")?.Value == "MinimumHeightRequest"
                    && double.Parse(setter.Attribute("Value")!.Value) >= 52
            );
    }

    [Fact]
    public void Scoring_has_compact_portrait_density_and_existing_player_team_workflow()
    {
        var page = LoadXaml("Views", "ScoringPage.xaml");
        var code = Read("Views", "ScoringPage.xaml.cs");
        var viewModel = Read("ViewModels", "ScoringViewModel.cs");
        var compactState = page.Descendants()
            .Single(element =>
                element.Name.LocalName == "VisualState"
                && element.Attribute(XName.Get("Name", XamlNamespace))?.Value
                    == "CompactPortraitScoreboard"
            );

        Assert.Contains("ResponsiveLayoutProfile.CompactPortrait", code);
        Assert.Contains("AppResources.AddPlayersToTeamTitle", code);
        Assert.Contains("AddPlayersToTeamForLiveLineupAsync", code);
        Assert.Contains("AppShell.AddPlayersToTeamRoute", viewModel);
        Assert.Contains(
            compactState.Descendants(),
            setter =>
                setter.Attribute("TargetName")?.Value == "CurrentBatterName"
                && setter.Attribute("Property")?.Value == "Label.FontSize"
                && setter.Attribute("Value")?.Value == "22"
        );
        foreach (
            var automationId in new[]
            {
                "FirstBaseRunnerButton",
                "SecondBaseRunnerButton",
                "ThirdBaseRunnerButton",
            }
        )
            Assert.Contains(
                compactState.Descendants(),
                setter =>
                    setter.Attribute("TargetName")?.Value == automationId
                    && setter.Attribute("Property")?.Value == "MinimumHeightRequest"
                    && double.Parse(setter.Attribute("Value")!.Value) >= 52
            );

        Assert.NotNull(ByAutomationId(page, "OnDeckBatter"));
        Assert.NotNull(ByAutomationId(page, "OutsThisInningHeading"));
    }

    private static XElement ByAutomationId(XDocument page, string id) =>
        page.Descendants().Single(element => element.Attribute("AutomationId")?.Value == id);

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
