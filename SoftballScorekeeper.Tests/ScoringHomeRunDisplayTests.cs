using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ScoringHomeRunDisplayTests
{
    [Fact]
    public void Scoreboard_uses_current_over_limit_off_and_current_only_with_one_up()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var viewModel = File.ReadAllText(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "ViewModels", "ScoringViewModel.cs"));
        var resources = XDocument.Load(Path.Combine(repositoryRoot,
            "SoftballScorekeeper", "Resources", "Strings", "AppResources.resx"));

        var oneUpOffFormat = ResourceValue(resources, "HomeRunScoreboardFormat");
        var oneUpOnFormat = ResourceValue(
            resources, "HomeRunScoreboardCurrentFormat");
        Assert.Equal("HR 4/5", string.Format(oneUpOffFormat, 4, 5));
        Assert.Equal("HR 5/5", string.Format(oneUpOffFormat, 5, 5));
        Assert.Equal("HR 5", string.Format(oneUpOnFormat, 5));
        Assert.Equal("HR 6", string.Format(oneUpOnFormat, 6));
        Assert.Contains("FormatHomeRunScoreboard(\n            state.HomeHomeRuns, HomeRunLimit, IsOneUpEnabled)",
            viewModel, StringComparison.Ordinal);
        Assert.Contains("FormatHomeRunScoreboard(\n            state.VisitorHomeRuns, HomeRunLimit, IsOneUpEnabled)",
            viewModel, StringComparison.Ordinal);
        Assert.Contains("isOneUpEnabled\n        ? string.Format(AppResources.HomeRunScoreboardCurrentFormat",
            viewModel, StringComparison.Ordinal);
        Assert.Contains(": string.Format(AppResources.HomeRunScoreboardFormat",
            viewModel, StringComparison.Ordinal);
    }

    private static string ResourceValue(XDocument resources, string name) =>
        resources.Descendants("data")
            .Single(item => item.Attribute("name")?.Value == name)
            .Element("value")!.Value;
}
