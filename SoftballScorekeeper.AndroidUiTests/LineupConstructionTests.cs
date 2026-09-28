using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class LineupConstructionTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Lineup_can_be_built_resequenced_reused_reopened_and_frozen_for_scoring()
    {
        PhaseAUiWorkflow.Run(
            fixture,
            nameof(Lineup_can_be_built_resequenced_reused_reopened_and_frozen_for_scoring),
            driver =>
            {
                AndroidElementWait.UntilText(driver, "Set Lineup").Click();
                AndroidElementWait.UntilText(driver, "Lineup Manager");
                AndroidElementWait.UntilText(driver, "No Players are currently in this lineup.");

                AndroidElementWait.UntilText(driver, "Use Previous Lineup").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Bravo Lineup",
                    "3. Charlie Lineup");

                AndroidElementWait.UntilText(driver, "Add Players").Click();
                var delta = AndroidElementWait.UntilText(driver, "Delta Lineup");
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, delta).Click();
                AndroidElementWait.UntilText(driver, "Add Selected").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Bravo Lineup",
                    "3. Charlie Lineup",
                    "4. Delta Lineup");

                AndroidElementWait.UntilText(driver, "Remove Players").Click();
                var bravo = AndroidElementWait.UntilText(driver, "2. Bravo Lineup");
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, bravo).Click();
                AndroidElementWait.UntilText(driver, "Remove Selected").Click();
                AndroidElementWait.UntilText(driver, "Remove Lineup Players");
                AndroidElementWait.UntilText(driver, "Remove").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Charlie Lineup",
                    "3. Delta Lineup");

                AndroidElementWait.UntilText(driver, "Add Players").Click();
                bravo = AndroidElementWait.UntilText(driver, "Bravo Lineup");
                PhaseAUiWorkflow.NearestVisibleCheckbox(driver, bravo).Click();
                AndroidElementWait.UntilText(driver, "Add Selected").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Charlie Lineup",
                    "3. Delta Lineup",
                    "4. Bravo Lineup");

                AndroidElementWait.UntilText(driver, "Use Previous Lineup").Click();
                AndroidElementWait.UntilText(driver, "Replace Current Lineup?");
                AndroidElementWait.UntilText(driver, "Replace Lineup").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Bravo Lineup",
                    "3. Charlie Lineup");
                PhaseAUiWorkflow.AssertTextAbsent(driver, "4. Delta Lineup");

                AndroidElementWait.UntilText(driver, "Done").Click();
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
                PhaseAUiWorkflow.NavigateUp(driver);
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidElementWait.UntilAutomationId(
                    driver, "TeamWorkspaceGamesSectionButton");
                AndroidElementWait.UntilText(driver, "NEXT");
                PhaseAUiWorkflow.Wait(driver).Until(current =>
                    current.PageSource.Contains("Lineup set", StringComparison.Ordinal));
                PhaseAUiWorkflow.TapCenter(
                    driver,
                    AndroidElementWait.UntilText(driver, "Current Lineup Opponent"));
                AndroidElementWait.UntilText(driver, "Game Details");
                AndroidElementWait.UntilText(driver, "Edit Lineup").Click();
                AssertOrder(driver,
                    "1. Alpha Lineup",
                    "2. Bravo Lineup",
                    "3. Charlie Lineup");
                AndroidElementWait.UntilText(driver, "Done").Click();

                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "CurrentBatter", "Alpha Lineup");
                AndroidElementWait.UntilText(driver, "Bravo Lineup");
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
                PhaseAUiWorkflow.AssertTextAbsent(driver, "Edit Lineup");
            },
            AndroidAutomationConstants.PhaseALineupTestSeedRequestFileName);
    }

    private static void AssertOrder(AndroidDriver driver, params string[] expected)
    {
        var elements = expected.Select(text => AndroidElementWait.UntilText(driver, text)).ToArray();
        Assert.Equal(
            elements.Select(item => item.Location.Y).OrderBy(value => value),
            elements.Select(item => item.Location.Y));
    }
}
