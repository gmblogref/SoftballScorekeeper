using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class CompletedGameResultPresentationTests
{
    [Fact]
    public void Games_list_binds_status_result_and_score_without_removing_schedule_content()
    {
        var page = XDocument.Load(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "Views", "TeamWorkspacePage.xaml"));
        var ns = page.Root!.Name.Namespace;
        var itemTemplate = page.Descendants(ns + "DataTemplate").Single(element =>
            element.Attribute(XName.Get("DataType", XamlNamespace))?.Value
                == "viewModels:GameScheduleItem");

        Assert.Contains(itemTemplate.Descendants(ns + "Label"), element =>
            element.Attribute("Text")?.Value == "{Binding StatusText}"
                && element.Attribute("AutomationId")?.Value
                    == "{Binding StatusAutomationId}");
        Assert.Contains(itemTemplate.Descendants(ns + "Label"), element =>
            element.Attribute("Text")?.Value == "{Binding ResultText}"
                && element.Attribute("AutomationId")?.Value
                    == "{Binding ResultAutomationId}");
        Assert.Contains(itemTemplate.Descendants(ns + "Label"), element =>
            element.Attribute("Text")?.Value == "{Binding FinalScoreDisplay}"
                && element.Attribute("AutomationId")?.Value
                    == "{Binding ScoreAutomationId}");
        Assert.Contains(itemTemplate.Descendants(ns + "Border"), element =>
            element.Attribute("IsVisible")?.Value == "{Binding IsFinal}");
        foreach (var binding in new[]
        {
            "{Binding DateDisplay}",
            "{Binding OpponentDisplay}",
            "{Binding TimeDisplay}",
            "{Binding FieldDisplay}",
            "{Binding SeasonDisplay}",
        })
            Assert.Contains(itemTemplate.Descendants(ns + "Label"), element =>
                element.Attribute("Text")?.Value == binding);
        Assert.Contains(itemTemplate.Descendants(ns + "TapGestureRecognizer"), element =>
            element.Attribute("Command")?.Value == "{Binding OpenCommand}");
        Assert.Contains(itemTemplate.Descendants(ns + "CheckBox"), element =>
            element.Attribute("IsVisible")?.Value == "{Binding IsSelectionVisible}");
    }

    [Fact]
    public void Games_list_uses_semantic_result_colors_resources_and_chronological_projection()
    {
        var page = XDocument.Load(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "Views", "TeamWorkspacePage.xaml"));
        var resources = XDocument.Load(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "Resources", "Strings",
            "AppResources.resx"));
        var colors = XDocument.Load(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "Resources", "Styles", "Colors.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "ViewModels",
            "TeamWorkspaceViewModel.cs"));
        var ns = page.Root!.Name.Namespace;
        var triggers = page.Descendants(ns + "DataTrigger").ToArray();

        Assert.Contains(triggers, HasTrigger("{Binding IsWin}", "WinSurface", "WinText"));
        Assert.Contains(triggers, HasTrigger("{Binding IsLoss}", "LossSurface", "Error"));
        Assert.Contains(triggers, HasTrigger("{Binding IsTie}", "Gray200", "Gray500"));
        foreach (var resourceName in new[]
        {
            "UpcomingStatus",
            "WinResultLabel",
            "LossResultLabel",
            "TieResultLabel",
            "FinalGameScoreFormat",
        })
            Assert.Contains(resources.Descendants("data"), element =>
                element.Attribute("name")?.Value == resourceName);
        foreach (var colorName in new[]
        {
            "WinSurface", "WinSurfaceDark", "WinText", "WinTextDark",
            "LossSurface", "LossSurfaceDark",
        })
            Assert.Contains(colors.Descendants(ns + "Color"), element =>
                element.Attribute(XName.Get("Key", XamlNamespace))?.Value == colorName);
        Assert.Contains("gameScoringSessionRepository.GetForGamesAsync", viewModel);
        Assert.Contains("GameScheduleSummary.FromSession", viewModel);
        Assert.Contains("AppResources.UpcomingStatus", viewModel);
        Assert.Contains("AppResources.InProgressStatus", viewModel);
        Assert.Contains("AppResources.FinalStatus", viewModel);
        Assert.Contains("AppResources.FinalGameScoreFormat", viewModel);
        Assert.Contains(".OrderBy(game => game.ScheduledDate)", viewModel);
        Assert.Contains(".ThenBy(game => game.ScheduledTime)", viewModel);
        Assert.Contains(".ThenBy(game => game.Id)", viewModel);
    }

    private static Predicate<XElement> HasTrigger(
        string binding,
        string surfaceResource,
        string strokeResource) => element =>
        element.Attribute("Binding")?.Value == binding
            && element.Descendants().Any(setter =>
                setter.Attribute("Value")?.Value?.Contains(
                    $"StaticResource {surfaceResource}", StringComparison.Ordinal) == true)
            && element.Descendants().Any(setter =>
                setter.Attribute("Value")?.Value?.Contains(
                    $"StaticResource {strokeResource}", StringComparison.Ordinal) == true);

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
