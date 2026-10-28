namespace SoftballScorekeeper.Tests;

public sealed class GameStartPresentationTests
{
    [Fact]
    public void Scoring_setup_suppresses_only_its_modal_return_reload()
    {
        var page = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "GameWorkspacePage.xaml.cs"
            )
        );
        var flagDeclaration = page.IndexOf(
            "private bool isScoringSetupPresented;",
            StringComparison.Ordinal
        );
        var onAppearing = page.IndexOf(
            "protected override async void OnAppearing()",
            StringComparison.Ordinal
        );
        var normalLoadGuard = page.IndexOf(
            "if (!isScoringSetupPresented)",
            onAppearing,
            StringComparison.Ordinal
        );
        var workspaceLoad = page.IndexOf(
            "await viewModel.LoadAsync();",
            normalLoadGuard,
            StringComparison.Ordinal
        );
        var scoreGameHandler = page.IndexOf(
            "private async void OnScoreGameClicked",
            StringComparison.Ordinal
        );
        var flagSet = page.IndexOf(
            "isScoringSetupPresented = true;",
            scoreGameHandler,
            StringComparison.Ordinal
        );
        var showDialog = page.IndexOf(
            "setup = await dialog.ShowAsync(Navigation);",
            flagSet,
            StringComparison.Ordinal
        );
        var cleanup = page.IndexOf("finally", showDialog, StringComparison.Ordinal);
        var flagCleared = page.IndexOf(
            "isScoringSetupPresented = false;",
            cleanup,
            StringComparison.Ordinal
        );
        var initializeScoring = page.IndexOf(
            "await viewModel.InitializeScoringAsync",
            flagCleared,
            StringComparison.Ordinal
        );

        Assert.True(flagDeclaration >= 0);
        Assert.True(onAppearing > flagDeclaration);
        Assert.True(normalLoadGuard > onAppearing);
        Assert.True(workspaceLoad > normalLoadGuard);
        Assert.True(flagSet > scoreGameHandler);
        Assert.True(showDialog > flagSet);
        Assert.True(cleanup > showDialog);
        Assert.True(flagCleared > cleanup);
        Assert.True(initializeScoring > flagCleared);
    }

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
