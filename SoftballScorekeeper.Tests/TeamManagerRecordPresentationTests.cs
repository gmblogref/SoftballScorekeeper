using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class TeamManagerRecordPresentationTests
{
    [Fact]
    public void Team_card_binds_name_optional_year_record_and_existing_navigation()
    {
        var page = XDocument.Load(
            Path.Combine(RepositoryRoot, "SoftballScorekeeper", "Views", "MainPage.xaml")
        );
        var ns = page.Root!.Name.Namespace;
        var itemTemplate = page.Descendants(ns + "DataTemplate")
            .Single(element =>
                element.Attribute(XName.Get("DataType", XamlNamespace))?.Value
                == "viewModels:TeamManagerItem"
            );

        Assert.Contains(
            itemTemplate.Descendants(ns + "Label"),
            element => element.Attribute("Text")?.Value == "{Binding Name}"
        );
        Assert.Contains(
            itemTemplate.Descendants(ns + "Label"),
            element =>
                element.Attribute("Text")?.Value == "{Binding Year}"
                && element.Attribute("IsVisible")?.Value == "{Binding HasYear}"
        );
        Assert.Contains(
            itemTemplate.Descendants(ns + "Label"),
            element => element.Attribute("Text")?.Value == "{Binding RecordDisplay}"
        );
        Assert.Contains(
            itemTemplate.Descendants(ns + "Border"),
            element => element.Attribute("AutomationId")?.Value == "{Binding CardAutomationId}"
        );
        Assert.Contains(
            itemTemplate.Descendants(ns + "TapGestureRecognizer"),
            element =>
                element.Attribute("Command")?.Value?.Contains("OpenTeamCommand") == true
                && element.Attribute("CommandParameter")?.Value == "{Binding .}"
        );
    }

    [Fact]
    public void Team_record_text_is_resource_backed_and_projection_drives_manager_items()
    {
        var resources = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Resources",
                "Strings",
                "AppResources.resx"
            )
        );
        var viewModel = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "ViewModels",
                "LandingPageViewModel.cs"
            )
        );
        var item = File.ReadAllText(
            Path.Combine(RepositoryRoot, "SoftballScorekeeper", "ViewModels", "TeamManagerItem.cs")
        );

        Assert.Contains(
            resources.Descendants("data"),
            element =>
                element.Attribute("name")?.Value == "TeamRecordFormat"
                && element.Element("value")?.Value == "Record: {0}-{1}"
        );
        Assert.Contains("teamRecordRepository.GetForTeamsAsync", viewModel);
        Assert.Contains("new TeamManagerItem(team, record)", viewModel);
        Assert.Contains("AppResources.TeamRecordFormat", item);
    }

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
