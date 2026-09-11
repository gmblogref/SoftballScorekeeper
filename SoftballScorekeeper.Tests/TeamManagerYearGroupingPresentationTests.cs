using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class TeamManagerYearGroupingPresentationTests
{
    [Fact]
    public void Team_manager_projects_authoritative_year_groups_in_descending_order()
    {
        var viewModel = ReadSource("SoftballScorekeeper", "ViewModels", "LandingPageViewModel.cs");

        Assert.Contains("teamRepository.GetActiveAsync()", viewModel);
        Assert.Contains("GroupBy(item => item.Team.Year)", viewModel);
        Assert.Contains("OrderByDescending(group => group.Key)", viewModel);
        Assert.Contains("yearGroup.ToArray()", viewModel);
        Assert.DoesNotContain("Season", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Team_year_groups_default_newest_open_and_preserve_transient_collapse_state()
    {
        var viewModel = ReadSource("SoftballScorekeeper", "ViewModels", "LandingPageViewModel.cs");
        var group = ReadSource("SoftballScorekeeper", "ViewModels", "TeamManagerYearGroup.cs");

        Assert.Contains("TeamYearGroups.ToDictionary", viewModel);
        Assert.Contains("previousExpansion.TryGetValue(yearGroup.Key", viewModel);
        Assert.Contains("yearGroup.Key == newestYear", viewModel);
        Assert.Contains("ToggleExpansionCommand = new RelayCommand(ToggleExpansion)", group);
        Assert.Contains("private void ToggleExpansion() => IsExpanded = !IsExpanded", group);
        Assert.Contains("OnPropertyChanged(nameof(HeaderDisplay))", group);
    }

    [Fact]
    public void Team_manager_renders_game_style_collapsible_headers_with_nested_team_cards()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var collection = page.Descendants(ns + "CollectionView").Single();
        Assert.Equal("{Binding TeamYearGroups}", collection.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding HasTeams}", collection.Attribute("IsVisible")?.Value);

        var groupTemplate = page.Descendants(ns + "DataTemplate")
            .Single(element => DataType(element) == "viewModels:TeamManagerYearGroup");
        var header = groupTemplate.Descendants(ns + "Button").Single();
        Assert.Equal("{Binding HeaderDisplay}", header.Attribute("Text")?.Value);
        Assert.Equal("{Binding ToggleExpansionCommand}", header.Attribute("Command")?.Value);
        Assert.Equal("48", header.Attribute("MinimumHeightRequest")?.Value);
        Assert.Equal("Bold", header.Attribute("FontAttributes")?.Value);

        var teamContainer = groupTemplate.Descendants(ns + "VerticalStackLayout")
            .Single(element => element.Attribute("BindableLayout.ItemsSource")?.Value
                == "{Binding Teams}");
        Assert.Equal("{Binding IsExpanded}", teamContainer.Attribute("IsVisible")?.Value);
        Assert.Contains(
            teamContainer.Descendants(ns + "DataTemplate"),
            element => DataType(element) == "viewModels:TeamManagerItem");
    }

    [Fact]
    public void Grouped_team_card_keeps_identity_based_navigation_and_clean_empty_state()
    {
        var page = LoadPage();
        var ns = page.Root!.Name.Namespace;
        var itemTemplate = page.Descendants(ns + "DataTemplate")
            .Single(element => DataType(element) == "viewModels:TeamManagerItem");
        var tap = itemTemplate.Descendants(ns + "TapGestureRecognizer").Single();
        Assert.Contains("OpenTeamCommand", tap.Attribute("Command")?.Value);
        Assert.Equal("{Binding .}", tap.Attribute("CommandParameter")?.Value);
        Assert.Contains(
            page.Descendants(ns + "Label"),
            element => element.Attribute("IsVisible")?.Value == "{Binding HasNoTeams}");

        var viewModel = ReadSource("SoftballScorekeeper", "ViewModels", "LandingPageViewModel.cs");
        Assert.Contains("[AppShell.TeamIdQueryParameter] = team.Id", viewModel);
        Assert.Contains("HasTeams = TeamYearGroups.Count > 0", viewModel);
        Assert.Contains("HasNoTeams = !HasTeams", viewModel);
    }

    [Fact]
    public void Team_year_header_text_is_resource_backed()
    {
        var resources = XDocument.Load(Path.Combine(
            RepositoryRoot,
            "SoftballScorekeeper",
            "Resources",
            "Strings",
            "AppResources.resx"));

        Assert.Contains(resources.Descendants("data"), element =>
            element.Attribute("name")?.Value == "ExpandedTeamYearHeaderFormat"
            && element.Element("value")?.Value == "{0} ▼");
        Assert.Contains(resources.Descendants("data"), element =>
            element.Attribute("name")?.Value == "CollapsedTeamYearHeaderFormat"
            && element.Element("value")?.Value == "{0} ▶");
    }

    private static XDocument LoadPage() => XDocument.Load(Path.Combine(
        RepositoryRoot,
        "SoftballScorekeeper",
        "Views",
        "MainPage.xaml"));

    private static string? DataType(XElement element) =>
        element.Attribute(XName.Get("DataType", XamlNamespace))?.Value;

    private static string ReadSource(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. path]));

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
