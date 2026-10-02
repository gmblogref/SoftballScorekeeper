using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class Story10ManualAcceptancePresentationTests
{
    [Fact]
    public void Current_lineup_renders_authoritative_formatted_obp()
    {
        var page = LoadXaml("Views", "SetLineupPage.xaml");
        var currentLineupTemplate = page.Descendants()
            .Single(element =>
                element.Name.LocalName == "CollectionView"
                && element.Attribute("ItemsSource")?.Value == "{Binding ExistingLineupPlayers}"
            )
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTemplate");
        var item = Read("ViewModels", "LineupPlayerSelectionItem.cs");
        var viewModel = Read("ViewModels", "SetLineupViewModel.cs");

        Assert.Contains(
            currentLineupTemplate.Descendants(),
            element =>
                element.Name.LocalName == "Label"
                && element.Attribute("Text")?.Value == "{Binding OnBasePercentageDisplay}"
        );
        Assert.Contains("BattingRateFormatter.Format(value)", item);
        Assert.Contains("AppResources.LineupObpDisplayFormat", item);
        Assert.Contains("BattingRateFormatter.Format(0)", item);
        Assert.Contains("rates.GetValueOrDefault(existingPlayers[index].Id)", viewModel);
        Assert.Contains("statisticsRepository.GetAsync(TeamId)", viewModel);
    }

    [Fact]
    public void Completed_game_stats_and_plays_visibility_uses_page_context_containers()
    {
        var page = LoadXaml("Views", "GameWorkspacePage.xaml");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var stats = page.Descendants()
            .Single(element =>
                element.Attribute("AutomationId")?.Value == "CompletedGameStatsView"
            );
        var plays = page.Descendants()
            .Single(element =>
                element.Attribute("AutomationId")?.Value == "CompletedGamePlaysView"
            );
        var statisticsGrid = Assert.Single(
            stats.Descendants().Where(element => element.Name.LocalName == "BattingStatisticsGrid")
        );

        Assert.Equal("{Binding ShowingGameStats}", stats.Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding ShowingGamePlays}", plays.Attribute("IsVisible")?.Value);
        Assert.Equal(
            "{Binding CompletedBattingStatistics}",
            statisticsGrid.Attribute("BindingContext")?.Value
        );
        Assert.Null(statisticsGrid.Attribute("IsVisible"));
        Assert.NotNull(
            page.Descendants()
                .Single(element => element.Attribute(x + "Name")?.Value == "CompletedGameGrid")
        );
    }

    [Fact]
    public void Opening_opponent_entry_can_be_resumed_without_hiding_game_rules_or_redo()
    {
        var page = LoadXaml("Views", "ScoringPage.xaml");
        var codeBehind = Read("Views", "ScoringPage.xaml.cs");
        var button = page.Descendants()
            .Single(element =>
                element.Attribute("AutomationId")?.Value == "EnterOpponentHalfInningButton"
            );
        var startupRedo = page.Descendants()
            .Single(element =>
                element.Attribute("AutomationId")?.Value == "StartupRedoScoringButton"
            );

        Assert.Equal("OnEnterOpponentHalfInningClicked", button.Attribute("Clicked")?.Value);
        Assert.Equal("{Binding CanRedo}", startupRedo.Attribute("IsEnabled")?.Value);
        Assert.Equal("OnRedoClicked", startupRedo.Attribute("Clicked")?.Value);
        Assert.Equal(
            "{Binding IsAwaitingOpponentHalfInning}",
            button.Parent?.Attribute("IsVisible")?.Value
        );
        Assert.Contains("await HandlePendingInningTransitionAsync()", codeBehind);
        Assert.Contains(
            page.Descendants(),
            element => element.Attribute("AutomationId")?.Value == "ScoringMenuButton"
        );
        Assert.Contains(
            page.Descendants(),
            element => element.Attribute("AutomationId")?.Value == "RedoScoringButton"
        );
    }

    [Fact]
    public void Obp_lineup_action_changes_label_and_caps_only_empty_population_at_ten()
    {
        var page = LoadXaml("Views", "SetLineupPage.xaml");
        var viewModel = Read("ViewModels", "SetLineupViewModel.cs");
        var button = page.Descendants()
            .Single(element =>
                element.Attribute("AutomationId")?.Value == "OrderLineupByObpButton"
            );

        Assert.Equal("{Binding OrderByObpButtonText}", button.Attribute("Text")?.Value);
        Assert.Contains("AppResources.SetLineupByObpButton", viewModel);
        Assert.Contains("AppResources.OrderLineupByObpButton", viewModel);
        Assert.Contains("AutomaticLineupPlayerLimit = 10", viewModel);
        Assert.Contains("AutomaticLineupPlayerLimit", viewModel);
        Assert.Contains("LineupOnBasePercentageOrderer.Order", viewModel);
        Assert.Contains("HasExistingLineup", viewModel);
        Assert.Contains("repository.ReorderAsync(GameId, orderedIds)", viewModel);
        Assert.Contains("repository.AppendAsync(TeamId, GameId, orderedIds)", viewModel);
    }

    [Fact]
    public void Add_live_player_prompts_with_numbered_existing_names_and_end_option()
    {
        var page = Read("Views", "ScoringPage.xaml.cs");
        var scoringViewModel = Read("ViewModels", "ScoringViewModel.cs");
        var createPlayerViewModel = Read("ViewModels", "CreatePlayerViewModel.cs");
        var resources = LoadResx("AppResources.resx");

        Assert.Contains("AddPlayerAtSelectedPositionAsync(replacement)", page);
        Assert.Contains("item.BattingPosition", page);
        Assert.Contains("item.DisplayName", page);
        Assert.Contains("AppResources.LiveLineupInsertBeforeFormat", page);
        Assert.Contains("AppResources.LiveLineupEndPositionFormat", page);
        Assert.Contains("selectedIndex + 1", page);
        Assert.Contains("PromptForCreatedLiveLineupPlayerAsync", page);
        Assert.Contains("TakeCreatedLiveLineupPlayerId", scoringViewModel);
        Assert.Contains("LiveLineupPlayerIdQueryParameter", createPlayerViewModel);
        Assert.Equal("{0} — Before {1}", ResourceValue(resources, "LiveLineupInsertBeforeFormat"));
        Assert.Equal(
            "{0} — End of Lineup",
            ResourceValue(resources, "LiveLineupEndPositionFormat")
        );
        Assert.Equal(
            "Where should {0} bat?",
            ResourceValue(resources, "LiveLineupInsertionTitleFormat")
        );
    }

    private static XDocument LoadXaml(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static XDocument LoadResx(string fileName) =>
        XDocument.Load(
            Path.Combine(RepositoryRoot, "SoftballScorekeeper", "Resources", "Strings", fileName)
        );

    private static string? ResourceValue(XDocument resources, string name) =>
        resources
            .Descendants("data")
            .Single(element => element.Attribute("name")?.Value == name)
            .Element("value")
            ?.Value;

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, "SoftballScorekeeper", .. path]));

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
