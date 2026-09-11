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
    public void Team_workspace_keeps_primary_sections_and_moves_team_actions_to_header_overflow()
    {
        var teamPage = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var ns = teamPage.Root!.Name.Namespace;
        var sectionGrid = Assert.Single(
            teamPage.Descendants(ns + "Grid"),
            grid =>
                grid.Attribute("ColumnDefinitions")?.Value == "*,*"
                && grid.Elements(ns + "Button").Count() == 2
        );
        var sectionButtons = sectionGrid.Elements(ns + "Button").ToArray();
        var menuTrigger = Assert.Single(teamPage.Descendants(ns + "ToolbarItem"));
        var actionMenu = Assert.Single(
            teamPage.Descendants(ns + "Border"),
            item => item.Attribute("AutomationId")?.Value == "TeamWorkspaceActionMenu"
        );
        var menuButtons = actionMenu.Descendants(ns + "Button").ToArray();

        Assert.Equal(
            [
                "{x:Static strings:AppResources.GamesSectionLabel}",
                "{x:Static strings:AppResources.RosterSectionLabel}",
            ],
            sectionButtons.Select(button => button.Attribute("Text")?.Value)
        );
        Assert.Equal(
            ["TeamWorkspaceGamesSectionButton", "TeamWorkspaceRosterSectionButton"],
            sectionButtons.Select(button => button.Attribute("AutomationId")?.Value)
        );
        Assert.Equal("TeamWorkspaceMenuButton", menuTrigger.Attribute("AutomationId")?.Value);
        Assert.Equal("Primary", menuTrigger.Attribute("Order")?.Value);
        Assert.Equal("OnTeamMenuClicked", menuTrigger.Attribute("Clicked")?.Value);
        Assert.Equal("{Binding IsTeamLoaded}", menuTrigger.Attribute("IsEnabled")?.Value);
        Assert.Equal(
            "{x:Static strings:AppResources.TeamWorkspaceMenuAccessibility}",
            menuTrigger.Attribute("Text")?.Value
        );
        Assert.Contains(
            menuTrigger.Descendants(ns + "FontImageSource"),
            icon => icon.Attribute("Glyph")?.Value == "☰"
        );
        Assert.Equal("False", actionMenu.Attribute("IsVisible")?.Value);
        Assert.Equal("7", actionMenu.Attribute("Grid.RowSpan")?.Value);
        Assert.DoesNotContain("IsGamesSectionSelected", actionMenu.ToString());
        Assert.DoesNotContain("IsRosterSectionSelected", actionMenu.ToString());
        Assert.Equal(6, menuButtons.Length);
        Assert.Equal(
            [
                "{x:Static strings:AppResources.TeamStatsButton}",
                "{x:Static strings:AppResources.TeamYearSummaryButton}",
                "{x:Static strings:AppResources.BeerTotalsButton}",
                "{x:Static strings:AppResources.TeamBeerRulesButton}",
                "{x:Static strings:AppResources.CopyTeamToNewYearButton}",
                "{x:Static strings:AppResources.DeactivateTeamButton}",
            ],
            menuButtons.Select(item => item.Attribute("Text")?.Value)
        );
        Assert.Equal("OnTeamStatsClicked", menuButtons[0].Attribute("Clicked")?.Value);
        Assert.Equal("OnTeamYearSummaryClicked", menuButtons[1].Attribute("Clicked")?.Value);
        Assert.Equal("OnBeerTotalsClicked", menuButtons[2].Attribute("Clicked")?.Value);
        Assert.Equal("OnBeerRulesClicked", menuButtons[3].Attribute("Clicked")?.Value);
        Assert.Equal("OnCopyTeamClicked", menuButtons[4].Attribute("Clicked")?.Value);
        Assert.Equal("OnDeactivateTeamClicked", menuButtons[5].Attribute("Clicked")?.Value);
        Assert.Equal("2", menuButtons[5].Attribute("BorderWidth")?.Value);
        Assert.Contains("#B3261E", menuButtons[5].Attribute("TextColor")?.Value);
        Assert.Contains("#B3261E", menuButtons[5].Attribute("BorderColor")?.Value);

        var buttons = teamPage.Descendants(ns + "Button").ToArray();
        Assert.DoesNotContain(
            sectionButtons,
            item =>
                item.Attribute("Text")?.Value
                    is "{x:Static strings:AppResources.BeerTotalsButton}"
                        or "{x:Static strings:AppResources.TeamBeerRulesButton}"
                        or "{x:Static strings:AppResources.TeamStatsButton}"
                        or "{x:Static strings:AppResources.TeamYearSummaryButton}"
                        or "{x:Static strings:AppResources.DeactivateTeamButton}"
        );
        Assert.Contains(
            buttons,
            item => item.Attribute("Text")?.Value == "{x:Static strings:AppResources.AddGameButton}"
        );
        Assert.Contains(
            buttons,
            item =>
                item.Attribute("Text")?.Value == "{x:Static strings:AppResources.RemoveGamesButton}"
        );
    }

    [Fact]
    public void Previous_lineup_button_uses_loaded_view_model_availability()
    {
        var lineupPage = LoadXaml("Views", "SetLineupPage.xaml");
        var ns = lineupPage.Root!.Name.Namespace;
        var button = Assert.Single(
            lineupPage.Descendants(ns + "Button"),
            item =>
                item.Attribute("Text")?.Value
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
