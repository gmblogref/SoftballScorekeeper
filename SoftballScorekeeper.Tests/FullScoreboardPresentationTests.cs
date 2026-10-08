using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class FullScoreboardPresentationTests
{
    [Fact]
    public void Full_scoreboard_is_shared_by_live_and_completed_game_pages()
    {
        var scoringPage = LoadXaml("Views", "ScoringPage.xaml");
        var gameWorkspacePage = LoadXaml("Views", "GameWorkspacePage.xaml");

        var liveScoreboard = SingleFullScoreboard(scoringPage);
        Assert.Equal("LiveRegularPortrait", liveScoreboard.Attribute("Profile")?.Value);
        AssertBinding(liveScoreboard, "VisitorName", "VisitorTeamDisplayName");
        AssertBinding(liveScoreboard, "VisitorScore", "VisitorScore");
        AssertBinding(liveScoreboard, "HomeName", "HomeTeamDisplayName");
        AssertBinding(liveScoreboard, "HomeScore", "HomeScore");
        AssertBinding(liveScoreboard, "VisitorDetailText", "VisitorHomeRunDisplay");
        AssertBinding(liveScoreboard, "HomeDetailText", "HomeHomeRunDisplay");
        AssertBinding(liveScoreboard, "CombinedStatusText", "InningOutDisplay");
        AssertBinding(liveScoreboard, "InningStatusText", "InningDisplay");
        AssertBinding(liveScoreboard, "OutsStatusText", "OutsDisplay");
        AssertBinding(liveScoreboard, "IsOneUpEnabled", "IsOneUpEnabled");

        var completedScoreboard = SingleFullScoreboard(gameWorkspacePage);
        Assert.Equal("CompletedPortrait", completedScoreboard.Attribute("Profile")?.Value);
        AssertBinding(completedScoreboard, "VisitorName", "CompletedVisitorName");
        AssertBinding(completedScoreboard, "VisitorScore", "CompletedVisitorScore");
        AssertBinding(completedScoreboard, "HomeName", "CompletedHomeName");
        AssertBinding(completedScoreboard, "HomeScore", "CompletedHomeScore");
        Assert.Null(completedScoreboard.Attribute("VisitorDetailText"));
        Assert.Null(completedScoreboard.Attribute("HomeDetailText"));

        AssertNoDuplicatedFullScoreboardTree(scoringPage);
        AssertNoDuplicatedFullScoreboardTree(gameWorkspacePage);
    }

    [Fact]
    public void Full_scoreboard_owns_profiles_order_and_optional_content()
    {
        var scoreboard = LoadXaml("Views", "Controls", "FullScoreboard.xaml");
        var profiles = scoreboard
            .Descendants()
            .Where(element => element.Name.LocalName == "VisualState")
            .Select(element => element.Attribute(XName.Get("Name", XamlNamespace))?.Value)
            .ToHashSet();

        Assert.True(
            profiles.SetEquals([
                "LiveRegularPortrait",
                "LiveCompactPortrait",
                "LiveRegularLandscape",
                "LiveCompactLandscape",
                "CompletedPortrait",
                "CompletedLandscape",
            ])
        );

        var named = NamedElements(scoreboard);
        Assert.Equal("0", named["VisitorScoreboard"].Attribute("Grid.Column")?.Value);
        Assert.Equal("2", named["HomeScoreboard"].Attribute("Grid.Column")?.Value);

        var completedPortrait = Profile(scoreboard, "CompletedPortrait");
        AssertSetter(completedPortrait, "HeaderLabel", "IsVisible", "True");
        AssertSetter(completedPortrait, "VisitorDetailLabel", "IsVisible", "False");
        AssertSetter(completedPortrait, "HomeDetailLabel", "IsVisible", "False");
        AssertSetter(completedPortrait, "GameStatusPanel", "IsVisible", "False");

        var completedLandscape = Profile(scoreboard, "CompletedLandscape");
        AssertSetter(completedLandscape, "HeaderLabel", "IsVisible", "False");
        AssertSetter(completedLandscape, "CompletedCompactHeaderLabel", "IsVisible", "True");
        AssertSetter(completedLandscape, "VisitorDetailLabel", "IsVisible", "False");
        AssertSetter(completedLandscape, "HomeDetailLabel", "IsVisible", "False");
        AssertSetter(completedLandscape, "GameStatusPanel", "IsVisible", "True");
        Assert.DoesNotContain(
            completedLandscape.Descendants(),
            setter =>
                setter.Attribute("TargetName")?.Value == "GameStatusPanel"
                && setter.Attribute("Property")?.Value == "IsVisible"
                && setter.Attribute("Value")?.Value == "False"
        );
    }

    [Fact]
    public void Scoreboard_automation_contracts_are_preserved_and_compact_score_stays_separate()
    {
        var scoringPage = SingleFullScoreboard(LoadXaml("Views", "ScoringPage.xaml"));
        Assert.Equal("VisitorScoreValue", scoringPage.Attribute("VisitorScoreAutomationId")?.Value);
        Assert.Equal("HomeScoreValue", scoringPage.Attribute("HomeScoreAutomationId")?.Value);
        Assert.Equal(
            "InningOutStateLabel",
            scoringPage.Attribute("CombinedStatusAutomationId")?.Value
        );
        Assert.Equal("InningStateLabel", scoringPage.Attribute("InningStatusAutomationId")?.Value);
        Assert.Equal("OutsStateLabel", scoringPage.Attribute("OutsStatusAutomationId")?.Value);

        var completed = SingleFullScoreboard(LoadXaml("Views", "GameWorkspacePage.xaml"));
        Assert.Equal("CompletedGameStatus", completed.Attribute("HeaderAutomationId")?.Value);
        Assert.Equal("CompletedVisitorName", completed.Attribute("VisitorNameAutomationId")?.Value);
        Assert.Equal(
            "CompletedVisitorScore",
            completed.Attribute("VisitorScoreAutomationId")?.Value
        );
        Assert.Equal("CompletedHomeName", completed.Attribute("HomeNameAutomationId")?.Value);
        Assert.Equal("CompletedHomeScore", completed.Attribute("HomeScoreAutomationId")?.Value);

        var component = LoadXaml("Views", "Controls", "FullScoreboard.xaml");
        var named = NamedElements(component);
        foreach (var headerName in new[] { "HeaderLabel", "CompletedCompactHeaderLabel" })
        {
            var header = named[headerName];
            Assert.Equal("Level1", header.Attribute("SemanticProperties.HeadingLevel")?.Value);
            Assert.Contains("HeaderAutomationId", header.Attribute("AutomationId")?.Value);
            Assert.Contains("HeaderText", header.Attribute("Text")?.Value);
        }

        var teamWorkspace = LoadXaml("Views", "TeamWorkspacePage.xaml");
        Assert.DoesNotContain(
            teamWorkspace.Descendants(),
            element => element.Name.LocalName == "FullScoreboard"
        );
        Assert.Contains(
            teamWorkspace.Descendants(),
            element => element.Attribute("Text")?.Value == "{Binding FinalScoreDisplay}"
        );
    }

    [Fact]
    public void Full_scoreboard_sizes_vertically_to_content_in_auto_page_rows()
    {
        var scoreboard = LoadXaml("Views", "Controls", "FullScoreboard.xaml");
        Assert.Equal("Start", scoreboard.Root!.Attribute("VerticalOptions")?.Value);
        Assert.Equal("Fill", scoreboard.Root.Attribute("HorizontalOptions")?.Value);

        var named = NamedElements(scoreboard);
        Assert.Equal("Start", named["ScoreboardBorder"].Attribute("VerticalOptions")?.Value);
        Assert.Equal("Fill", named["ScoreboardBorder"].Attribute("HorizontalOptions")?.Value);
        Assert.Equal("Auto,Auto", named["ContentGrid"].Attribute("RowDefinitions")?.Value);
        Assert.DoesNotContain(
            scoreboard.Root.DescendantsAndSelf().Attributes(),
            attribute =>
                attribute.Name.LocalName
                    is "HeightRequest"
                        or "MinimumHeightRequest"
                        or "MinHeightRequest"
        );

        var workspace = LoadXaml("Views", "GameWorkspacePage.xaml");
        Assert.StartsWith(
            "Auto,",
            NamedElements(workspace)["CompletedGameGrid"].Attribute("RowDefinitions")?.Value
        );
    }

    [Fact]
    public void Completed_landscape_uses_one_content_driven_scoreboard_row()
    {
        var scoreboard = LoadXaml("Views", "Controls", "FullScoreboard.xaml");
        var landscape = Profile(scoreboard, "CompletedLandscape");

        AssertSetter(landscape, "ContentGrid", "RowDefinitions", "Auto");
        AssertSetter(landscape, "ScoreboardBorder", "Padding", "8,2");
        AssertSetter(landscape, "ScoreboardGrid", "Grid.Row", "0");
        AssertSetter(landscape, "ScoreboardGrid", "ColumnDefinitions", "*,1,Auto,1,*");
        AssertSetter(landscape, "ScoreboardGrid", "ColumnSpacing", "8");
        AssertSetter(landscape, "ScoreboardGrid", "RowSpacing", "0");
        AssertSetter(landscape, "HomeScoreboard", "Grid.Column", "4");
        AssertSetter(landscape, "HomeScoreboard", "RowSpacing", "0");
        AssertSetter(landscape, "VisitorScoreboard", "RowSpacing", "0");
        AssertSetter(landscape, "GameStatusPanel", "Grid.Column", "2");
        AssertSetter(landscape, "GameStatusPanel", "Padding", "8,0");
        AssertSetter(landscape, "ScoreboardRightDivider", "Grid.Column", "3");
        AssertSetter(landscape, "CompletedCompactHeaderLabel", "Label.FontSize", "14");
        AssertSetter(landscape, "HomeHeading", "Label.FontSize", "12");
        AssertSetter(landscape, "VisitorHeading", "Label.FontSize", "12");
        AssertSetter(landscape, "HomeTeamName", "Label.FontSize", "15");
        AssertSetter(landscape, "VisitorTeamName", "Label.FontSize", "15");
        AssertSetter(landscape, "HomeScoreValue", "Label.FontSize", "32");
        AssertSetter(landscape, "VisitorScoreValue", "Label.FontSize", "32");
        Assert.DoesNotContain(
            landscape.Descendants(),
            setter => setter.Attribute("Property")?.Value is "HeightRequest" or "MinimumHeightRequest"
        );

        var portrait = Profile(scoreboard, "CompletedPortrait");
        AssertSetter(portrait, "HeaderLabel", "IsVisible", "True");
        AssertSetter(portrait, "GameStatusPanel", "IsVisible", "False");
    }

    [Fact]
    public void Completed_portrait_retains_the_existing_vertical_composition()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var named = NamedElements(page);
        var portrait = Profile(page, "PortraitCompletedWorkspace");

        Assert.Equal("*", named["CompletedGameGrid"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(
            "Auto,Auto,Auto,*,Auto",
            named["CompletedGameGrid"].Attribute("RowDefinitions")?.Value
        );
        Assert.Equal("0", named["CompletedScoreboard"].Attribute("Grid.Row")?.Value ?? "0");
        Assert.Equal("1", named["CompletedViewButtons"].Attribute("Grid.Row")?.Value);
        Assert.Equal("2", named["CompletedStatsHeading"].Attribute("Grid.Row")?.Value);
        Assert.Equal("3", named["CompletedGameStatsView"].Attribute("Grid.Row")?.Value);
        Assert.Equal("3", named["CompletedGamePlaysView"].Attribute("Grid.Row")?.Value);
        Assert.Equal("4", named["CompletedGameInfoPanel"].Attribute("Grid.Row")?.Value);
        Assert.Equal(
            "Auto,Auto,Auto",
            named["CompletedGameInfoGrid"].Attribute("RowDefinitions")?.Value
        );
        Assert.Equal("*,*", named["CompletedGameInfoGrid"].Attribute("ColumnDefinitions")?.Value);

        AssertSetter(portrait, "CompletedGameGrid", "ColumnDefinitions", "*");
        AssertSetter(portrait, "CompletedGameGrid", "ColumnSpacing", "0");
        AssertSetter(portrait, "CompletedScoreboard", "controls:FullScoreboard.Profile", "CompletedPortrait");
        AssertSetter(portrait, "CompletedScoreboard", "Grid.Column", "0");
        AssertSetter(portrait, "CompletedViewButtons", "Grid.Column", "0");
        AssertSetter(portrait, "CompletedGameStatsView", "Grid.Column", "0");
        AssertSetter(portrait, "CompletedGamePlaysView", "Grid.Column", "0");
        AssertSetter(portrait, "CompletedGameInfoPanel", "Grid.Row", "4");
        AssertSetter(portrait, "CompletedGameInfoPanel", "Grid.RowSpan", "1");
        AssertSetter(portrait, "CompletedGameInfoGrid", "ColumnDefinitions", "*,*");
        AssertSetter(portrait, "CompletedGameInfoGrid", "RowDefinitions", "Auto,Auto,Auto");
    }

    [Fact]
    public void Completed_workspace_runtime_states_transition_both_directions_without_sticky_layout()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var portrait = SetterValues(Profile(page, "PortraitCompletedWorkspace"));
        var landscape = SetterValues(Profile(page, "LandscapeCompletedWorkspace"));
        var layout = new Dictionary<(string Target, string Property), string>();

        Apply(layout, portrait);
        Assert.Equal("*", layout[("CompletedGameGrid", "ColumnDefinitions")]);
        Assert.Equal("CompletedPortrait", layout[("CompletedScoreboard", "controls:FullScoreboard.Profile")]);
        Assert.Equal("4", layout[("CompletedGameInfoPanel", "Grid.Row")]);
        Assert.Equal("1", layout[("CompletedGameInfoPanel", "Grid.RowSpan")]);

        Apply(layout, landscape);
        Assert.Equal("0.3*,0.7*", layout[("CompletedGameGrid", "ColumnDefinitions")]);
        Assert.Equal("CompletedLandscape", layout[("CompletedScoreboard", "controls:FullScoreboard.Profile")]);
        Assert.Equal("1", layout[("CompletedScoreboard", "Grid.Column")]);
        Assert.Equal("0", layout[("CompletedGameInfoPanel", "Grid.Row")]);
        Assert.Equal("5", layout[("CompletedGameInfoPanel", "Grid.RowSpan")]);
        Assert.Equal("1", layout[("CompletedGameStatsView", "Grid.Column")]);
        Assert.Equal("1", layout[("CompletedGamePlaysView", "Grid.Column")]);

        Apply(layout, portrait);
        Assert.Equal("*", layout[("CompletedGameGrid", "ColumnDefinitions")]);
        Assert.Equal("CompletedPortrait", layout[("CompletedScoreboard", "controls:FullScoreboard.Profile")]);
        Assert.Equal("0", layout[("CompletedScoreboard", "Grid.Column")]);
        Assert.Equal("4", layout[("CompletedGameInfoPanel", "Grid.Row")]);
        Assert.Equal("1", layout[("CompletedGameInfoPanel", "Grid.RowSpan")]);
        Assert.Equal("0", layout[("CompletedGameStatsView", "Grid.Column")]);
        Assert.Equal("0", layout[("CompletedGamePlaysView", "Grid.Column")]);
        Assert.Equal("*,*", layout[("CompletedGameInfoGrid", "ColumnDefinitions")]);
        Assert.Equal("Auto,Auto,Auto", layout[("CompletedGameInfoGrid", "RowDefinitions")]);
    }

    [Fact]
    public void Completed_workspace_selects_state_from_each_allocated_size_change()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var code = Read("Views", "GameWorkspacePage.xaml.cs");

        Assert.DoesNotContain(page.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.Contains("protected override void OnSizeAllocated(double width, double height)", code);
        Assert.Contains("ResponsiveLayoutState.Current.Update(width, height)", code);
        Assert.Contains("ResponsiveLayoutState.Classify(width, height)", code);
        Assert.Contains("ResponsiveLayoutProfile.CompactLandscape", code);
        Assert.Contains("ResponsiveLayoutProfile.RegularLandscape", code);
        Assert.Contains("VisualStateManager.GoToState(RootGrid, layoutState)", code);
        Assert.Contains("layoutState != appliedCompletedLayoutState", code);

        const double measuredLandscapeWidth = 961.5;
        const double measuredLandscapeHeight = 489;
        Assert.True(measuredLandscapeWidth > measuredLandscapeHeight);
        Assert.True(measuredLandscapeWidth <= 1100 || measuredLandscapeHeight <= 700);
        Assert.Contains("? LandscapeCompletedWorkspaceState", code);
        Assert.Contains(": PortraitCompletedWorkspaceState", code);
    }

    [Fact]
    public void Completed_landscape_uses_left_info_panel_and_main_content_column()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var named = NamedElements(page);
        var landscape = Profile(page, "LandscapeCompletedWorkspace");

        AssertSetter(landscape, "CompletedGameGrid", "ColumnDefinitions", "0.3*,0.7*");
        AssertSetter(landscape, "CompletedGameGrid", "ColumnSpacing", "16");
        AssertSetter(landscape, "CompletedGameInfoPanel", "Grid.Row", "0");
        AssertSetter(landscape, "CompletedGameInfoPanel", "Grid.RowSpan", "5");
        AssertSetter(landscape, "CompletedGameInfoPanel", "VerticalOptions", "Fill");
        AssertSetter(landscape, "CompletedGameInfoGrid", "ColumnDefinitions", "*");
        AssertSetter(
            landscape,
            "CompletedGameInfoGrid",
            "RowDefinitions",
            "Auto,Auto,Auto,*,Auto,Auto"
        );
        AssertSetter(landscape, "CompletedSeasonInfo", "Grid.Row", "0");
        AssertSetter(landscape, "CompletedDateInfo", "Grid.Row", "1");
        AssertSetter(landscape, "CompletedFieldInfo", "Grid.Row", "2");
        AssertSetter(landscape, "CompletedOpponentInfo", "Grid.Row", "4");
        AssertSetter(landscape, "CompletedTimeInfo", "Grid.Row", "5");

        foreach (
            var mainElement in new[]
            {
                "CompletedScoreboard",
                "CompletedViewButtons",
                "CompletedStatsHeading",
                "CompletedGameStatsView",
                "CompletedGamePlaysView",
            }
        )
        {
            AssertSetter(landscape, mainElement, "Grid.Column", "1");
        }

        Assert.Equal("3", named["CompletedGameStatsView"].Attribute("Grid.Row")?.Value);
        Assert.Equal("3", named["CompletedGamePlaysView"].Attribute("Grid.Row")?.Value);
        Assert.Contains('*', named["CompletedGameGrid"].Attribute("RowDefinitions")!.Value);
    }

    [Fact]
    public void Completed_layout_reuses_bindings_controls_and_has_no_runtime_diagnostic()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var named = NamedElements(page);

        Assert.Single(page.Descendants().Where(element => element.Name.LocalName == "FullScoreboard"));
        Assert.Equal("Button", AutomationElement(page, "GameStatsButton").Name.LocalName);
        Assert.Equal("GamePlaysButton", AutomationElement(page, "GamePlaysButton").Attribute("AutomationId")?.Value);

        AssertBindingIn(named["CompletedSeasonInfo"], "SeasonDisplay");
        AssertBindingIn(named["CompletedDateInfo"], "DateDisplay");
        AssertBindingIn(named["CompletedFieldInfo"], "FieldDisplay");
        AssertBindingIn(named["CompletedOpponentInfo"], "OpponentDisplay");
        AssertBindingIn(named["CompletedTimeInfo"], "TimeDisplay");

        Assert.DoesNotContain("Story13ScoreboardDiagnostic", page.ToString());
        Assert.DoesNotContain(
            "Story13ScoreboardDiagnostic",
            Read("Views", "GameWorkspacePage.xaml.cs")
        );
        Assert.DoesNotContain(
            "Story13ScoreboardDiagnostic",
            Read("Views", "Controls", "FullScoreboard.xaml.cs")
        );
        Assert.DoesNotContain(
            "Story13ScoreboardDiagnostic",
            Read("Resources", "Strings", "AppResources.resx")
        );
    }

    private static XElement SingleFullScoreboard(XDocument page) =>
        page.Descendants().Single(element => element.Name.LocalName == "FullScoreboard");

    private static XElement Profile(XDocument scoreboard, string name) =>
        scoreboard
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "VisualState"
                && element.Attribute(XName.Get("Name", XamlNamespace))?.Value == name
            );

    private static void AssertBinding(
        XElement element,
        string attributeName,
        string propertyName
    ) => Assert.Equal($"{{Binding {propertyName}}}", element.Attribute(attributeName)?.Value);

    private static void AssertBindingIn(XElement element, string propertyName) =>
        Assert.Contains(
            element.DescendantsAndSelf().Attributes("Text"),
            attribute => attribute.Value == $"{{Binding {propertyName}}}"
        );

    private static XElement AutomationElement(XDocument document, string automationId) =>
        document.Descendants().Single(element => element.Attribute("AutomationId")?.Value == automationId);

    private static void AssertNoDuplicatedFullScoreboardTree(XDocument page)
    {
        var names = NamedElements(page).Keys;
        Assert.DoesNotContain("HomeScoreboard", names);
        Assert.DoesNotContain("VisitorScoreboard", names);
        Assert.DoesNotContain("ScoreboardGrid", names);
        Assert.DoesNotContain("ScoreboardBorder", names);
    }

    private static void AssertSetter(
        XElement profile,
        string targetName,
        string property,
        string value
    ) =>
        Assert.Contains(
            profile.Descendants(),
            setter =>
                setter.Attribute("TargetName")?.Value == targetName
                && setter.Attribute("Property")?.Value == property
                && setter.Attribute("Value")?.Value == value
        );

    private static Dictionary<(string Target, string Property), string> SetterValues(
        XElement profile
    ) =>
        profile
            .Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(
                setter =>
                    (
                        setter.Attribute("TargetName")!.Value,
                        setter.Attribute("Property")!.Value
                    ),
                setter => setter.Attribute("Value")!.Value
            );

    private static void Apply(
        Dictionary<(string Target, string Property), string> current,
        Dictionary<(string Target, string Property), string> state
    )
    {
        foreach (var setter in state)
            current[setter.Key] = setter.Value;
    }

    private static Dictionary<string, XElement> NamedElements(XDocument document) =>
        document
            .Descendants()
            .Where(element => element.Attribute(XName.Get("Name", XamlNamespace)) is not null)
            .ToDictionary(
                element => element.Attribute(XName.Get("Name", XamlNamespace))!.Value,
                element => element
            );

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
