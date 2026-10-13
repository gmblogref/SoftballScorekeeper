namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class LiveLineupManagementTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Manage_lineup_cancel_returns_to_scoring_and_remove_still_opens_explicitly()
    {
        Run(
            nameof(Manage_lineup_cancel_returns_to_scoring_and_remove_still_opens_explicitly),
            driver =>
            {
                OpenScoring(driver);
                var initialState = CaptureScoringState(driver);

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Cancel").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                Assert.Empty(
                    driver
                        .FindElements(OpenQA.Selenium.By.XPath("//*[@text='1. Alex Live']"))
                        .Where(element => element.Displayed)
                );
                AssertScoringState(driver, initialState);

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Remove Player").Click();
                AndroidElementWait.UntilText(driver, "1. Alex Live");
                AndroidElementWait.UntilText(driver, "Cancel").Click();

                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AssertScoringState(driver, initialState);
            }
        );
    }

    [AndroidUiFact]
    public void View_lineup_tracks_live_order_and_batters_without_changing_scoring()
    {
        PhaseAUiWorkflow.Run(
            fixture,
            nameof(View_lineup_tracks_live_order_and_batters_without_changing_scoring),
            driver =>
            {
                OpenScoring(driver);
                var initialState = CaptureScoringState(driver);

                OpenViewLineup(driver);
                AssertLineupPlayer(driver, 1, "Alex Live");
                AssertLineupPlayer(driver, 2, "Blake Live");
                AssertLineupPlayer(driver, 3, "Casey Live");
                AssertLineupPlayer(driver, 10, "Jordan Live");
                AssertMarker(driver, "ViewLineupCurrent-1", "CURRENT");
                AssertMarker(driver, "ViewLineupNext-2", "NEXT");
                fixture.SetOrientation(OpenQA.Selenium.ScreenOrientation.Portrait);
                AssertLineupPlayer(driver, 10, "Jordan Live");
                AssertCloseWithinViewport(driver);
                driver.Navigate().Back();
                AssertScoringState(driver, initialState);
                Assert.False(
                    AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Enabled
                );

                fixture.SetOrientation(OpenQA.Selenium.ScreenOrientation.Landscape);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Replace Player").Click();
                AndroidElementWait.UntilText(driver, "2. Blake Live").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                Assert.True(
                    AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Enabled
                );

                OpenViewLineup(driver);
                AssertLineupPlayer(driver, 2, "Blake Live");
                CloseViewLineup(driver);
                Assert.True(
                    AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Enabled
                );
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();

                OpenViewLineup(driver);
                AssertLineupPlayer(driver, 2, "Bailey Live");
                AssertMarker(driver, "ViewLineupCurrent-1", "CURRENT");
                AssertMarker(driver, "ViewLineupNext-2", "NEXT");
                CloseViewLineup(driver);

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Remove Player").Click();
                AndroidElementWait.UntilText(driver, "1. Alex Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Bailey Live");
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Alex Live").Click();
                AndroidElementWait.UntilTextContaining(driver, "Before Casey Live").Click();

                OpenViewLineup(driver);
                AssertLineupPlayer(driver, 1, "Bailey Live");
                AssertLineupPlayer(driver, 2, "Alex Live");
                AssertLineupPlayer(driver, 10, "Jordan Live");
                AssertMarker(driver, "ViewLineupCurrent-1", "CURRENT");
                AssertMarker(driver, "ViewLineupNext-2", "NEXT");
                CloseViewLineup(driver);

                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                OpenViewLineup(driver);
                AssertMarker(driver, "ViewLineupCurrent-2", "CURRENT");
                AssertMarker(driver, "ViewLineupNext-3", "NEXT");
                CloseViewLineup(driver);

                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                OpenViewLineup(driver);
                AssertLineupPlayer(driver, 1, "Bailey Live");
                AssertLineupPlayer(driver, 2, "Alex Live");
                AssertLineupPlayer(driver, 10, "Jordan Live");
                AssertMarker(driver, "ViewLineupCurrent-2", "CURRENT");
                AssertMarker(driver, "ViewLineupNext-3", "NEXT");
                CloseViewLineup(driver);
            },
            AndroidAutomationConstants.ViewLineupTestSeedRequestFileName
        );
    }

    [AndroidUiFact]
    public void Existing_global_player_joins_team_through_replace_and_remains_undoable()
    {
        Run(
            nameof(Existing_global_player_joins_team_through_replace_and_remains_undoable),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Replace Player").Click();
                AndroidElementWait.UntilText(driver, "2. Blake Live").Click();

                AssertSelectorWithinViewport(driver);
                fixture.SetOrientation(OpenQA.Selenium.ScreenOrientation.Portrait);
                AssertSelectorWithinViewport(driver);
                fixture.SetOrientation(OpenQA.Selenium.ScreenOrientation.Landscape);
                AssertSelectorWithinViewport(driver);
                Assert.DoesNotContain(
                    driver.FindElements(OpenQA.Selenium.By.XPath("//*[@text='Global Live']")),
                    element => element.Displayed
                );

                AndroidElementWait.UntilAutomationId(
                    driver,
                    "LiveLineupAllPlayersFilter"
                ).Click();
                AndroidElementWait.UntilText(
                    driver,
                    "Global Live — Not on Team — will be added"
                ).Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Global Live");

                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Blake Live");
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Global Live");

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Replace Player").Click();
                AndroidElementWait.UntilText(driver, "3. Casey Live").Click();
                AndroidElementWait.UntilAutomationId(
                    driver,
                    "LiveLineupAllPlayersFilter"
                ).Click();
                Assert.DoesNotContain(
                    driver.FindElements(
                        OpenQA.Selenium.By.XPath("//*[contains(@text,'Global Live')]")
                    ),
                    element => element.Displayed
                );
                AndroidElementWait.UntilAutomationId(
                    driver,
                    "LiveLineupPlayerCancelButton"
                ).Click();
            }
        );
    }

    [AndroidUiFact]
    public void Add_in_middle_shows_named_positions_and_preserves_current_batter()
    {
        Run(
            nameof(Add_in_middle_shows_named_positions_and_preserves_current_batter),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "Where should Bailey Live bat?");
                AndroidElementWait.UntilText(driver, "1 — Before Alex Live");
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();

                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Blake Live");
            }
        );
    }

    [AndroidUiFact]
    public void Add_at_end_preserves_append_rotation()
    {
        Run(
            nameof(Add_at_end_preserves_append_rotation),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "4 — End of Lineup").Click();

                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Blake Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Casey Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Bailey Live");
            }
        );
    }

    [AndroidUiFact]
    public void Replace_participates_in_chronological_undo_redo_and_resume()
    {
        Run(
            nameof(Replace_participates_in_chronological_undo_redo_and_resume),
            driver =>
            {
                OpenScoring(driver);
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Replace Player").Click();
                AndroidElementWait.UntilText(driver, "2. Blake Live").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );

                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Bailey Live",
                    "Top 1",
                    "0 Outs"
                );
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );

                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Casey Live",
                    "Top 1",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void Remove_and_readd_uses_selected_middle_position()
    {
        Run(
            nameof(Remove_and_readd_uses_selected_middle_position),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Remove Player").Click();
                AndroidElementWait.UntilText(driver, "1. Alex Live").Click();
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Blake Live",
                    "Top 1",
                    "0 Outs"
                );

                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Alex Live").Click();
                AndroidElementWait.UntilText(driver, "2 — Before Casey Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Alex Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Alex Live",
                    "Top 1",
                    "0 Outs"
                );
            }
        );
    }

    [AndroidUiFact]
    public void New_player_uses_normal_roster_creation_then_can_join_live_lineup()
    {
        Run(
            nameof(New_player_uses_normal_roster_creation_then_can_join_live_lineup),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Create New Player").Click();
                var entries = AndroidScoringWorkflow.VisibleEditTexts(driver);
                Assert.Equal(2, entries.Length);
                entries[0].SendKeys("Taylor");
                entries[1].SendKeys("Live");
                AndroidElementWait.UntilText(driver, "Save").Click();
                AndroidElementWait.UntilText(driver, "Where should Taylor Live bat?");
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Taylor Live");
                AndroidScoringWorkflow.SelectResult(driver, "Home Run");
                AndroidScoringWorkflow.AssertBatterInningAndOuts(
                    driver,
                    "Taylor Live",
                    "Top 1",
                    "0 Outs"
                );

                ExitToWorkspace(driver);
                AndroidElementWait.UntilText(driver, "Reset Game").Click();
                AndroidElementWait.UntilText(driver, "Continue").Click();
                AndroidElementWait.UntilText(driver, "Reset Game").Click();
                AndroidElementWait.UntilText(driver, "Edit Lineup").Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();
                AndroidElementWait.UntilText(driver, "Taylor Live");
            }
        );
    }

    [AndroidUiFact]
    public void Middle_insertion_undo_redo_restores_exact_order()
    {
        Run(
            nameof(Middle_insertion_undo_redo_restores_exact_order),
            driver =>
            {
                OpenScoring(driver);
                OpenManageLineup(driver);
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                AndroidElementWait.UntilText(driver, "Bailey Live").Click();
                AndroidElementWait.UntilText(driver, "2 — Before Blake Live").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");

                AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Blake Live");
                AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Click();
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");

                AndroidScoringWorkflow.ExitAndResume(driver);
                AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
                AndroidElementWait.UntilAutomationIdText(driver, "OnDeckBatter", "Bailey Live");
                AndroidElementWait.UntilAutomationIdText(driver, "InTheHoleBatter", "Blake Live");
            }
        );
    }

    private void Run(string name, Action<OpenQA.Selenium.Appium.Android.AndroidDriver> scenario) =>
        PhaseAUiWorkflow.Run(
            fixture,
            name,
            scenario,
            AndroidAutomationConstants.LiveLineupTestSeedRequestFileName
        );

    private static void OpenScoring(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
        AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", "Alex Live");
    }

    private static void OpenManageLineup(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ManageLiveLineupButton").Click();
        AndroidElementWait.UntilText(driver, "Manage Live Lineup");
    }

    private static void OpenViewLineup(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    )
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ViewLineupButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ViewLineupDialog");
    }

    private static void CloseViewLineup(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    ) => AndroidElementWait.UntilAutomationId(driver, "ViewLineupCloseButton").Click();

    private static void AssertLineupPlayer(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        int position,
        string playerName
    )
    {
        var automationId = $"ViewLineupName-{position}";
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        driver.FindElement(
            OpenQA.Selenium.Appium.MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().resourceId(\"{AndroidAutomationConstants.AppPackage}:id/ViewLineupList\")).setAsVerticalList().scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"
            )
        );
        AndroidElementWait.UntilAutomationIdText(
            driver,
            automationId,
            $"{position}. {playerName}"
        );
    }

    private static void AssertMarker(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string automationId,
        string text
    ) => AndroidElementWait.UntilAutomationIdText(driver, automationId, text);

    private static void AssertCloseWithinViewport(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    )
    {
        var close = AndroidElementWait.UntilAutomationId(driver, "ViewLineupCloseButton");
        var viewport = driver.Manage().Window.Size;
        Assert.True(close.Displayed);
        Assert.InRange(close.Location.X, 0, viewport.Width - 1);
        Assert.InRange(close.Location.Y, 0, viewport.Height - 1);
        Assert.InRange(close.Location.X + close.Size.Width, 1, viewport.Width);
        Assert.InRange(close.Location.Y + close.Size.Height, 1, viewport.Height);
    }

    private static ScoringStateSnapshot CaptureScoringState(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    ) =>
        new(
            AndroidElementWait.UntilAutomationId(driver, "CurrentBatter").Text,
            AndroidElementWait.UntilAutomationId(driver, "VisitorScoreValue").Text,
            AndroidElementWait.UntilAutomationId(driver, "HomeScoreValue").Text,
            GetInningAndOuts(driver),
            AndroidElementWait.UntilAutomationId(driver, "FirstBaseRunnerButton").Text,
            AndroidElementWait.UntilAutomationId(driver, "SecondBaseRunnerButton").Text,
            AndroidElementWait.UntilAutomationId(driver, "ThirdBaseRunnerButton").Text,
            AndroidElementWait.UntilAutomationId(driver, "UndoScoringButton").Enabled,
            AndroidElementWait.UntilAutomationId(driver, "RedoScoringButton").Enabled
        );

    private static void AssertScoringState(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        ScoringStateSnapshot expected
    ) => Assert.Equal(expected, CaptureScoringState(driver));

    private static string GetInningAndOuts(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    )
    {
        var combinedId =
            $"{AndroidAutomationConstants.AppPackage}:id/InningOutStateLabel";
        var combined = driver
            .FindElements(OpenQA.Selenium.By.Id(combinedId))
            .FirstOrDefault(element => element.Displayed);
        return combined?.Text
            ?? $"{AndroidElementWait.UntilAutomationId(driver, "InningStateLabel").Text}    {AndroidElementWait.UntilAutomationId(driver, "OutsStateLabel").Text}";
    }

    private sealed record ScoringStateSnapshot(
        string CurrentBatter,
        string VisitorScore,
        string HomeScore,
        string InningAndOuts,
        string FirstBase,
        string SecondBase,
        string ThirdBase,
        bool CanUndo,
        bool CanRedo
    );

    private static void ExitToWorkspace(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
    }

    private static void AssertSelectorWithinViewport(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver
    )
    {
        foreach (
            var element in new[]
            {
                AndroidElementWait.UntilAutomationId(driver, "LiveLineupRosteredFilter"),
                AndroidElementWait.UntilAutomationId(driver, "LiveLineupAllPlayersFilter"),
                AndroidElementWait.UntilText(driver, "Bailey Live"),
                AndroidElementWait.UntilAutomationId(driver, "LiveLineupPlayerCancelButton"),
            }
        )
        {
            var viewport = driver.Manage().Window.Size;
            Assert.True(element.Displayed);
            Assert.InRange(element.Location.X, 0, viewport.Width - 1);
            Assert.InRange(element.Location.Y, 0, viewport.Height - 1);
            Assert.InRange(element.Location.X + element.Size.Width, 1, viewport.Width);
            Assert.InRange(element.Location.Y + element.Size.Height, 1, viewport.Height);
        }
    }
}
