using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class LiveLineupPlayerSelectionPresentationTests
{
    [Fact]
    public void Selector_defaults_to_rostered_and_distinguishes_all_player_membership()
    {
        var page = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "LiveLineupPlayerSelectionDialog.xaml"
            )
        );
        var code = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "LiveLineupPlayerSelectionDialog.xaml.cs"
            )
        );
        var viewModel = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "ViewModels",
                "ScoringViewModel.cs"
            )
        );

        Assert.NotNull(ByAutomationId(page, "LiveLineupRosteredFilter"));
        Assert.NotNull(ByAutomationId(page, "LiveLineupAllPlayersFilter"));
        Assert.Contains("ShowRosteredPlayers();", code);
        Assert.Contains("GetAllEligibleLiveLineupPlayersAsync", viewModel);
        Assert.Contains("playerRepository.GetActiveAsync()", viewModel);
        Assert.Contains("!activeLineupIds.Contains(player.Id)", viewModel);
        Assert.Contains("PlayerMembershipDisplayFormat", viewModel);
    }

    [Fact]
    public void Non_rostered_selection_reuses_membership_then_existing_live_lineup_operations()
    {
        var pageCode = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "ScoringPage.xaml.cs"
            )
        );
        var viewModel = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "ViewModels",
                "ScoringViewModel.cs"
            )
        );

        Assert.Contains("LiveLineupPlayerSelectionDialog", pageCode);
        Assert.Contains("AddSelectedLiveLineupPlayerAsync", pageCode);
        Assert.Contains("ReplaceWithSelectedLiveLineupPlayerAsync", pageCode);
        Assert.Contains("teamPlayerRepository.AddPlayersToTeamAsync", viewModel);
        Assert.Contains("AddLiveLineupPlayerAsync(playerId, battingPosition)", viewModel);
        Assert.Contains(
            "ReplaceLiveLineupPlayerAsync(outgoingPlayerId, replacementPlayerId)",
            viewModel
        );
    }

    [Fact]
    public void Manage_lineup_cancel_and_dismissal_exit_before_player_selection()
    {
        var pageCode = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "ScoringPage.xaml.cs"
            )
        );
        var removeGuard = pageCode.IndexOf(
            "action != AppResources.RemovePlayerButton",
            StringComparison.Ordinal
        );
        var replaceGuard = pageCode.IndexOf(
            "action != AppResources.ReplacePlayerButton",
            StringComparison.Ordinal
        );
        var playerSelection = pageCode.IndexOf(
            "var active = await viewModel.GetLiveLineupPlayersAsync();",
            StringComparison.Ordinal
        );

        Assert.True(removeGuard >= 0);
        Assert.True(replaceGuard > removeGuard);
        Assert.True(playerSelection > replaceGuard);
        Assert.Contains("action == AppResources.RemovePlayerButton", pageCode);
        Assert.Contains("action != AppResources.ReplacePlayerButton", pageCode);
    }

    private static XElement ByAutomationId(XDocument page, string id) =>
        page.Descendants().Single(element => element.Attribute("AutomationId")?.Value == id);

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
