using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class BeerRuleConfigurationPresentationTests
{
    [Fact]
    public void Beer_rules_dialog_uses_touch_editors_for_each_normalized_rule()
    {
        var dialog = LoadXaml("Views", "BeerRulesDialog.xaml");
        var ns = dialog.Root!.Name.Namespace;

        Assert.Single(dialog.Descendants(ns + "CollectionView"));
        Assert.Single(dialog.Descendants(ns + "Switch"));
        var amount = Assert.Single(dialog.Descendants(ns + "Entry"));
        Assert.Equal("Numeric", amount.Attribute("Keyboard")?.Value);
        Assert.Contains(
            dialog.Descendants(ns + "Button"),
            item => item.Attribute("Clicked")?.Value == "OnSaveClicked"
        );
    }

    [Fact]
    public void Team_and_scoring_surfaces_open_focused_beer_rules_dialog()
    {
        var teamPage = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var scoringPage = LoadXaml("Views", "ScoringPage.xaml");
        var ns = teamPage.Root!.Name.Namespace;

        Assert.Contains(
            teamPage.Descendants(ns + "Button"),
            item =>
                item.Attribute("Clicked")?.Value == "OnBeerRulesClicked"
                && item.Attribute("Text")?.Value
                    == "{x:Static strings:AppResources.TeamBeerRulesButton}"
        );
        Assert.Contains(
            scoringPage.Descendants(ns + "Button"),
            item =>
                item.Attribute("Clicked")?.Value == "OnBeerRulesClicked"
                && item.Attribute("Text")?.Value
                    == "{x:Static strings:AppResources.GameBeerRulesTitle}"
        );
    }

    [Fact]
    public void Team_workspace_groups_games_roster_and_beer_rules_in_equal_width_columns()
    {
        var teamPage = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var ns = teamPage.Root!.Name.Namespace;
        var actionGrid = Assert.Single(
            teamPage.Descendants(ns + "Grid"),
            grid => grid.Attribute("ColumnDefinitions")?.Value == "*,*,*"
        );
        var buttons = actionGrid.Elements(ns + "Button").ToArray();

        Assert.Equal(3, buttons.Length);
        Assert.Equal(
            [
                "{x:Static strings:AppResources.GamesSectionLabel}",
                "{x:Static strings:AppResources.RosterSectionLabel}",
                "{x:Static strings:AppResources.TeamBeerRulesButton}",
            ],
            buttons.Select(button => button.Attribute("Text")?.Value)
        );
        Assert.Equal("TeamBeerRulesButton", buttons[2].Attribute("AutomationId")?.Value);
        Assert.Equal("OnBeerRulesClicked", buttons[2].Attribute("Clicked")?.Value);
    }

    [Fact]
    public void Previous_lineup_button_uses_loaded_view_model_availability()
    {
        var lineupPage = LoadXaml("Views", "SetLineupPage.xaml");
        var ns = lineupPage.Root!.Name.Namespace;
        var button = Assert.Single(
            lineupPage.Descendants(ns + "Button"),
            item => item.Attribute("Text")?.Value
                == "{x:Static strings:AppResources.UsePreviousLineupButton}"
        );
        var viewModel = ReadText("ViewModels", "SetLineupViewModel.cs");

        Assert.Equal("{Binding UsePreviousLineupCommand}", button.Attribute("Command")?.Value);
        Assert.Contains("CanUsePreviousLineup = await repository.CanUsePreviousAsync", viewModel);
        Assert.Contains("hasContext && CanUsePreviousLineup", viewModel);
    }

    [Fact]
    public void Game_surface_communicates_prospective_behavior_and_team_surface_has_low_man()
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
        var values = resources
            .Descendants("data")
            .ToDictionary(
                item => item.Attribute("name")!.Value,
                item => item.Element("value")!.Value
            );
        var dialogCode = ReadText("Views", "BeerRulesDialog.xaml.cs");

        Assert.Contains("future Beer consequences", values["GameBeerRulesDescription"]);
        Assert.Contains("BeerRuleType.LowMan => AppResources.BeerRuleLowManLabel", dialogCode);
        Assert.Contains(
            "item.RuleType != BeerRuleType.LowMan",
            ReadText("..", "SoftballScorekeeper.Core", "Models", "BeerRuleConfiguration.cs")
        );
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(
            Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray())
        );

    private static string ReadText(params string[] path) =>
        File.ReadAllText(
            Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray())
        );

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
