namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class PlayerRosterLifecycleTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Player_and_roster_lifecycle_preserves_one_global_identity()
    {
        PhaseAUiWorkflow.Run(
            fixture,
            nameof(Player_and_roster_lifecycle_preserves_one_global_identity),
            driver =>
            {
                PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
                PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
                AndroidElementWait.UntilText(driver, "Add Player").Click();
                CreatePlayer(driver, "Phase Alice", "Foundation", "Save & Add Another");
                AndroidElementWait.UntilText(driver, "Create Player");
                CreatePlayer(driver, "Phase Rapid", "Foundation", "Save");

                PhaseAUiWorkflow.ScrollToText(driver, "Phase Rapid Foundation");
                var alice = PhaseAUiWorkflow.ScrollToText(driver, "Phase Alice Foundation");
                PhaseAUiWorkflow.TapCenter(driver, alice);
                AndroidElementWait.UntilText(driver, "Phase Alice Foundation");
                AndroidElementWait.UntilAutomationId(driver, "EditPlayerNameButton").Click();
                var nameEntries = PhaseAUiWorkflow.VisibleEditTexts(driver);
                Assert.Equal(2, nameEntries.Length);
                PhaseAUiWorkflow.ReplaceText(nameEntries[0], "Phase Edited");
                AndroidElementWait.UntilText(driver, "Save").Click();
                AndroidElementWait.UntilText(driver, "Phase Edited Foundation");

                PhaseAUiWorkflow.TapCenter(driver, AndroidElementWait.UntilText(driver, "Teams"));
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait
                    .UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton")
                    .Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();
                var eligiblePlayer = AndroidElementWait.UntilText(
                    driver,
                    "Phase Edited Foundation"
                );
                AndroidElementWait.UntilText(driver, "Select All").Click();
                Assert.Equal(
                    "true",
                    PhaseAUiWorkflow
                        .NearestVisibleCheckbox(driver, eligiblePlayer)
                        .GetAttribute("checked")
                );
                AndroidElementWait.UntilText(driver, "Clear All").Click();
                Assert.Equal(
                    "false",
                    PhaseAUiWorkflow
                        .NearestVisibleCheckbox(driver, eligiblePlayer)
                        .GetAttribute("checked")
                );
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, eligiblePlayer).Click();
                AndroidElementWait.UntilText(driver, "Add Selected to Team").Click();
                AndroidElementWait.UntilText(driver, "Add Players to Team");
                AndroidElementWait.UntilText(driver, "Add").Click();

                var rosterPlayer = PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");
                EditJersey(driver, rosterPlayer, "42");
                AndroidElementWait.UntilText(driver, "No. 42");
                rosterPlayer = PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");
                EditJersey(driver, rosterPlayer, "24");
                AndroidElementWait.UntilText(driver, "No. 24");

                AndroidElementWait.UntilText(driver, "Remove Players").Click();
                var playerToRemove = PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited");
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, playerToRemove).Click();
                AndroidElementWait.UntilText(driver, "Remove Selected from Team").Click();
                AndroidElementWait.UntilText(driver, "Remove Players from Team");
                AndroidElementWait.UntilText(driver, "Remove").Click();
                PhaseAUiWorkflow.AssertTextAbsent(driver, "Phase Edited Foundation");

                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilAutomationId(driver, "PlayersFlyoutMenuItem").Click();
                PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");
                PhaseAUiWorkflow.AssertSingleVisibleText(driver, "Phase Edited Foundation");
                PhaseAUiWorkflow.TapCenter(driver, AndroidElementWait.UntilText(driver, "Teams"));
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait
                    .UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton")
                    .Click();
                AndroidElementWait.UntilText(driver, "Add Players").Click();
                eligiblePlayer = AndroidElementWait.UntilText(driver, "Phase Edited Foundation");
                var eligibleCheckbox = PhaseAUiWorkflow.NearestVisibleCheckbox(
                    driver,
                    eligiblePlayer
                );
                eligibleCheckbox.Click();
                Assert.Equal("true", eligibleCheckbox.GetAttribute("checked"));
                AndroidElementWait.UntilText(driver, "Add Selected to Team").Click();
                AndroidElementWait.UntilText(driver, "Add Players to Team");
                AndroidElementWait.UntilText(driver, "Add").Click();
                PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");

                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilAutomationId(driver, "PlayersFlyoutMenuItem").Click();
                OpenByRealTap(driver, "Phase Edited Foundation");
                AndroidElementWait.UntilText(driver, "Deactivate Player").Click();
                AndroidElementWait.UntilText(driver, "Deactivate Player");
                AndroidElementWait.UntilText(driver, "Deactivate").Click();
                PhaseAUiWorkflow.AssertTextAbsent(driver, "Phase Edited Foundation");

                PhaseAUiWorkflow.OpenShellFlyout(driver);
                AndroidElementWait.UntilText(driver, "Manage Inactive Players").Click();
                var inactivePlayer = AndroidElementWait.UntilText(
                    driver,
                    "Phase Edited Foundation"
                );
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, inactivePlayer).Click();
                AndroidElementWait.UntilText(driver, "Reactivate Selected").Click();
                AndroidElementWait.UntilText(driver, "Reactivate Players");
                AndroidElementWait.UntilText(driver, "Reactivate").Click();

                PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");
                PhaseAUiWorkflow.AssertSingleVisibleText(driver, "Phase Edited Foundation");
                PhaseAUiWorkflow.TapCenter(driver, AndroidElementWait.UntilText(driver, "Teams"));
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait
                    .UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton")
                    .Click();
                PhaseAUiWorkflow.ScrollToText(driver, "Phase Edited Foundation");
                PhaseAUiWorkflow.AssertSingleVisibleText(driver, "Phase Edited Foundation");
            }
        );
    }

    private static void CreatePlayer(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string firstName,
        string lastName,
        string action
    )
    {
        var entries = PhaseAUiWorkflow.VisibleEditTexts(driver);
        Assert.Equal(2, entries.Length);
        entries[0].SendKeys(firstName);
        entries[1].SendKeys(lastName);
        AndroidElementWait.UntilText(driver, action).Click();
    }

    private static void OpenByRealTap(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string text
    ) => PhaseAUiWorkflow.TapCenter(driver, PhaseAUiWorkflow.ScrollToText(driver, text));

    private static void EditJersey(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        OpenQA.Selenium.IWebElement player,
        string number
    )
    {
        PhaseAUiWorkflow.NearestByText(driver, "Edit", player).Click();
        var entry = PhaseAUiWorkflow.VisibleEditTexts(driver).Single();
        PhaseAUiWorkflow.ReplaceText(entry, number);
        PhaseAUiWorkflow.NearestByText(driver, "Save", entry).Click();
    }
}
