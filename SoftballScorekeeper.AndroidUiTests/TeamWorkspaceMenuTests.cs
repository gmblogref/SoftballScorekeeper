using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamWorkspaceMenuTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Team_workspace_hamburger_exposes_persistent_actions_and_destructive_emphasis()
    {
        fixture.ResetApplication();
        var driver = fixture.Driver;
        try
        {
            var teamCardId = $"{AndroidAutomationConstants.AppPackage}:id/TeamCard-1";
            if (!driver.FindElements(By.Id(teamCardId)).Any(element => element.Displayed))
            {
                AndroidElementWait.UntilText(driver, "Teams").Click();
            }
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();

            var menuButton = AndroidTeamWorkspaceMenu.UntilAvailable(driver);
            Assert.True(menuButton.Displayed);
            Assert.Empty(driver.FindElements(MobileBy.AccessibilityId("More options"))
                .Where(element => element.Displayed));

            menuButton.Click();
            var teamStats = AndroidElementWait.UntilText(driver, "Team Stats");
            var beerTotals = AndroidElementWait.UntilText(driver, "Beer Totals");
            var beerRules = AndroidElementWait.UntilText(driver, "Beer Rules");
            var deactivateTeam = AndroidElementWait.UntilText(driver, "Deactivate Team");
            Assert.All([teamStats, beerTotals, beerRules, deactivateTeam], AssertWithinViewport);
            SaveOpenMenuScreenshot(driver);

            menuButton.Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton").Click();
            Assert.True(AndroidTeamWorkspaceMenu.UntilAvailable(driver).Displayed);
            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            Assert.True(AndroidElementWait.UntilText(driver, "Deactivate Team").Displayed);

            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            var gamesSection = AndroidElementWait.UntilAutomationId(
                driver, "TeamWorkspaceGamesSectionButton");
            gamesSection.Click();
            var addGame = AndroidElementWait.UntilText(driver, "Add Game");
            var removeGames = AndroidElementWait.UntilText(driver, "Remove Games");
            Assert.True(addGame.Displayed);
            Assert.True(removeGames.Displayed);
            Assert.True(addGame.Location.Y > gamesSection.Location.Y);
            Assert.True(removeGames.Location.Y > gamesSection.Location.Y);
            Assert.Equal(addGame.Location.Y, removeGames.Location.Y);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Team_workspace_hamburger_exposes_persistent_actions_and_destructive_emphasis),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Team Workspace menu workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
        }
    }

    private static void AssertWithinViewport(IWebElement element)
    {
        Assert.True(element.Displayed);
        Assert.True(element.Location.X >= 0);
        Assert.True(element.Location.Y >= 0);
        Assert.True(element.Size.Width > 0);
        Assert.True(element.Size.Height > 0);
    }

    private static void SaveOpenMenuScreenshot(OpenQA.Selenium.Appium.Android.AndroidDriver driver)
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "TestResults",
            "TeamWorkspaceMenuValidation");
        Directory.CreateDirectory(directory);
        driver.GetScreenshot().SaveAsFile(Path.Combine(directory, "open-menu.png"));
    }
}
