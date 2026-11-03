using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ViewLineupPresentationTests
{
    [Fact]
    public void Scoring_menu_opens_lineup_view_and_both_entries_share_the_reorder_editor()
    {
        var scoringPage = LoadXaml("Views", "ScoringPage.xaml");
        var dialog = LoadXaml("Views", "ViewLineupDialog.xaml");
        var reorderDialog = LoadXaml("Views", "ReorderLiveLineupDialog.xaml");
        var scoringCode = Read("Views", "ScoringPage.xaml.cs");
        var dialogCode = Read("Views", "ViewLineupDialog.xaml.cs");
        var reorderCode = Read("Views", "ReorderLiveLineupDialog.xaml.cs");

        Assert.NotNull(ByAutomationId(scoringPage, "ViewLineupButton"));
        Assert.Equal("ViewLineupDialog", dialog.Root?.Attribute("AutomationId")?.Value);
        Assert.NotNull(ByAutomationId(dialog, "ViewLineupList"));
        Assert.NotNull(ByAutomationId(dialog, "ViewLineupCloseButton"));
        Assert.NotNull(ByAutomationId(dialog, "ViewLineupReorderButton"));
        Assert.Equal(
            "ReorderLiveLineupDialog",
            reorderDialog.Root?.Attribute("AutomationId")?.Value
        );
        Assert.NotNull(ByAutomationId(reorderDialog, "ReorderLiveLineupList"));
        Assert.NotNull(ByAutomationId(reorderDialog, "ReorderLiveLineupSaveButton"));
        Assert.NotNull(ByAutomationId(reorderDialog, "ReorderLiveLineupCancelButton"));
        Assert.Equal(
            "None",
            ByAutomationId(dialog, "ViewLineupList").Attribute("SelectionMode")?.Value
        );
        Assert.Empty(
            dialog
                .Descendants()
                .Where(item => item.Name.LocalName == "Button")
                .Where(item =>
                    item.Ancestors().Any(parent => parent.Name.LocalName == "DataTemplate")
                )
        );

        Assert.Contains("ScoringMenu.IsVisible = false;", scoringCode);
        Assert.Contains("GetLiveLineupPlayersAsync()", scoringCode);
        Assert.Contains("ReorderFromViewLineupAsync", scoringCode);
        Assert.Contains("ShowReorderLiveLineupAsync", scoringCode);
        Assert.Contains("AppResources.ReorderLineupButton", scoringCode);
        Assert.Contains("PushModalAsync(this)", dialogCode);
        Assert.Contains("PopModalAsync()", dialogCode);
        Assert.DoesNotContain("Repository", dialogCode, StringComparison.Ordinal);
        Assert.Contains("DragGestureRecognizer", reorderDialog.ToString());
        Assert.Contains("DropGestureRecognizer", reorderDialog.ToString());
        Assert.Contains("saveOrder(state.Items.Select", reorderCode);
        Assert.Contains("CloseAsync(false)", reorderCode);
        Assert.Contains("completion.TrySetResult(false)", reorderCode);
    }

    [Fact]
    public void Lineup_rows_expose_order_name_and_textual_current_next_markers()
    {
        var dialog = LoadXaml("Views", "ViewLineupDialog.xaml");
        var viewModel = Read("ViewModels", "ScoringViewModel.cs");
        var resources = LoadXaml("Resources", "Strings", "AppResources.resx");

        Assert.Contains("{Binding ViewRowAutomationId}", AutomationIds(dialog));
        Assert.Contains("{Binding ViewNameAutomationId}", AutomationIds(dialog));
        Assert.Contains("{Binding CurrentMarkerAutomationId}", AutomationIds(dialog));
        Assert.Contains("{Binding NextMarkerAutomationId}", AutomationIds(dialog));
        Assert.Contains("{Binding PositionAndNameDisplay}", dialog.ToString());
        Assert.Contains("{Binding IsCurrentBatter}", dialog.ToString());
        Assert.Contains("{Binding IsNextBatter}", dialog.ToString());
        Assert.Equal("CURRENT", Resource(resources, "ViewLineupCurrentMarker"));
        Assert.Equal("NEXT", Resource(resources, "ViewLineupNextMarker"));
        Assert.Equal("Reorder Lineup", Resource(resources, "ReorderLineupButton"));
        Assert.Equal("Reorder Live Lineup", Resource(resources, "ReorderLiveLineupTitle"));
        Assert.Equal("Save Order", Resource(resources, "SaveLineupOrderButton"));

        Assert.Contains("sessionRepository.GetStateForGameAsync(GameId)", viewModel);
        Assert.Contains("state.GetUpcomingPlayerIds()", viewModel);
        Assert.Contains(".BattingSequence.OrderBy", viewModel);
        Assert.Contains("item.PlayerId == currentPlayerId", viewModel);
        Assert.Contains("item.PlayerId == upcoming?.OnDeckPlayerId", viewModel);
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static XElement ByAutomationId(XDocument document, string id) =>
        document.Descendants().Single(item => item.Attribute("AutomationId")?.Value == id);

    private static string[] AutomationIds(XDocument document) =>
        document
            .Descendants()
            .Select(item => item.Attribute("AutomationId")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

    private static string Resource(XDocument resources, string name) =>
        resources
            .Root!.Elements("data")
            .Single(item => item.Attribute("name")?.Value == name)
            .Element("value")!
            .Value;

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
