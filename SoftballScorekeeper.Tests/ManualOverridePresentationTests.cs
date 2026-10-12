using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ManualOverridePresentationTests
{
    [Fact]
    public void Completed_game_exposes_edit_save_cancel_revert_chip_and_score_controls()
    {
        var page = XDocument.Load(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Views", "GameWorkspacePage.xaml"));
        var automationIds = page.Descendants()
            .Select(item => item.Attribute("AutomationId")?.Value)
            .Where(item => item is not null).ToHashSet();

        Assert.Contains("BeginManualOverrideButton", automationIds);
        Assert.Contains("SaveManualOverrideButton", automationIds);
        Assert.Contains("CancelManualOverrideButton", automationIds);
        Assert.Contains("RevertManualOverridesButton", automationIds);
        Assert.Contains("ManualOverrideChip", automationIds);
        Assert.Contains("ManualOverrideVisitorScore", automationIds);
        Assert.Contains("ManualOverrideHomeScore", automationIds);
        Assert.Contains(page.Descendants(), item => item.Attribute("Command")?.Value
            == "{Binding SaveManualOverrideCommand}");
        Assert.Contains(page.Descendants(), item => item.Attribute("IsVisible")?.Value
            == "{Binding IsManualOverrideEditing}");
    }

    [Fact]
    public void Eight_player_outcomes_include_editable_outs_with_dirty_highlights()
    {
        var grid = XDocument.Load(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Views", "Controls", "BattingStatisticsGrid.xaml"));
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var playerTemplate = grid.Descendants()
            .Single(item => item.Attribute(x + "Name")?.Value == "CompletedStatsPlayerRows")
            .Descendants().Single(item => item.Name.LocalName == "DataTemplate");
        var entries = playerTemplate.Descendants()
            .Where(item => item.Name.LocalName == "Entry").ToArray();

        Assert.Equal(8, entries.Length);
        Assert.Equal(new[]
        {
            "{Binding SinglesEdit}", "{Binding DoublesEdit}", "{Binding TriplesEdit}",
            "{Binding HomeRunsEdit}", "{Binding WalksEdit}",
            "{Binding FielderChoicesEdit}", "{Binding SacrificeFliesEdit}",
            "{Binding OutsEdit}",
        }, entries.Select(item => item.Attribute("Text")?.Value));
        Assert.All(entries, entry => Assert.Equal("Numeric", entry.Attribute("Keyboard")?.Value
            ?? grid.Descendants().Single(item => item.Attribute(XName.Get("Key",
                "http://schemas.microsoft.com/winfx/2009/xaml"))?.Value
                == "CompletedStatEdit").Elements()
                .Single(item => item.Attribute("Property")?.Value == "Keyboard")
                .Attribute("Value")?.Value));
        Assert.Equal(8, entries.SelectMany(item => item.Descendants())
            .Count(item => item.Name.LocalName == "DataTrigger"
                && item.Attribute("Value")?.Value == "True"));
        Assert.DoesNotContain(entries, item => item.Attribute("Text")?.Value is
            "{Binding PlateAppearances}" or "{Binding AtBats}" or "{Binding Hits}"
            or "{Binding BattingAverage}" or "{Binding OnBasePercentage}"
            or "{Binding SluggingPercentage}");
    }

    [Fact]
    public void View_model_keeps_pending_text_until_atomic_save_and_cancel_does_not_persist()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "GameWorkspaceViewModel.cs"));
        var rowSource = File.ReadAllText(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "ViewModels", "CompletedBattingStatRowViewModel.cs"));

        Assert.Contains("TryGetCorrections", source);
        Assert.Contains("manualOverrideRepository.SaveAsync", source);
        Assert.Contains("RefreshCompletedProjectionAsync", source);
        Assert.Contains("CompletedBattingStatistics.CancelEdit();", source);
        Assert.Contains("NotifyCanExecuteChangedFor(nameof(BeginManualOverrideCommand))", source);
        Assert.DoesNotContain("manualOverrideRepository.SaveAsync", rowSource);
        Assert.Contains("int.TryParse", rowSource);
        Assert.Contains("IsSinglesDirty", rowSource);
        Assert.Contains("IsOutsDirty", rowSource);
    }

    [Fact]
    public void User_facing_manual_override_text_is_resource_backed()
    {
        var resources = XDocument.Load(Path.Combine(RepositoryRoot,
            "SoftballScorekeeper", "Resources", "Strings", "AppResources.resx"));
        var keys = resources.Descendants("data")
            .Select(item => item.Attribute("name")?.Value).ToHashSet();
        Assert.Contains("ManualOverrideEditStatsButton", keys);
        Assert.Contains("ManualOverrideChip", keys);
        Assert.Contains("RevertToGamePlaysStatsButton", keys);
        Assert.Contains("ManualOverrideInvalidValueMessage", keys);
        Assert.Contains("ManualOverrideRevertMessage", keys);
    }

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
}
