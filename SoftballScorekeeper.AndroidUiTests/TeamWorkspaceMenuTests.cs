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
            var markYearOver = AndroidElementWait.UntilText(driver, "Mark Team Year Over");
            var beerTotals = AndroidElementWait.UntilText(driver, "Beer Totals");
            var beerRules = AndroidElementWait.UntilText(driver, "Beer Rules");
            var deactivateTeam = AndroidElementWait.UntilText(driver, "Deactivate Team");
            Assert.All(
                [teamStats, markYearOver, beerTotals, beerRules, deactivateTeam],
                AssertWithinViewport);
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

    [AndroidUiFact]
    public void Team_year_over_requires_confirmation_and_finalizes_team_menu()
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
            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            AndroidElementWait.UntilText(driver, "Automation Team");

            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            AssertMenuActionHidden(driver, "TeamYearSummaryButton", "Team Year Summary");
            var copyTeam = AndroidElementWait.UntilText(driver, "Copy Team to New Year");
            var markYearOver = AndroidElementWait.UntilText(driver, "Mark Team Year Over");
            var deactivateTeam = AndroidElementWait.UntilText(driver, "Deactivate Team");
            Assert.True(copyTeam.Displayed);
            AssertAdjacentDestructiveActions(markYearOver, deactivateTeam);

            markYearOver.Click();
            AndroidElementWait.UntilText(driver, "Mark Team Year Over?");
            Assert.Contains("permanently finalize this Team Year", driver.PageSource,
                StringComparison.Ordinal);
            Assert.Contains("you will no longer be able to add Games or change scoring",
                driver.PageSource, StringComparison.Ordinal);
            AndroidElementWait.UntilText(driver, "Continue").Click();

            AndroidElementWait.UntilText(driver, "Are you absolutely sure?");
            Assert.Contains("This action cannot be undone", driver.PageSource,
                StringComparison.Ordinal);
            Assert.Contains("read-only forever", driver.PageSource, StringComparison.Ordinal);
            AndroidElementWait.UntilText(driver, "Cancel").Click();

            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            AssertMenuActionHidden(driver, "TeamYearSummaryButton", "Team Year Summary");
            Assert.True(AndroidElementWait.UntilText(driver, "Copy Team to New Year").Displayed);
            Assert.True(AndroidElementWait.UntilText(driver, "Mark Team Year Over").Displayed);

            AndroidElementWait.UntilText(driver, "Mark Team Year Over").Click();
            AndroidElementWait.UntilText(driver, "Mark Team Year Over?");
            AndroidElementWait.UntilText(driver, "Continue").Click();
            AndroidElementWait.UntilText(driver, "Are you absolutely sure?");
            AndroidElementWait.UntilText(driver, "Permanently Mark Year Over").Click();

            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            var summary = AndroidElementWait.UntilText(driver, "Team Year Summary");
            AssertMenuActionHidden(driver, "MarkTeamYearOverButton", "Mark Team Year Over");
            AssertMenuActionHidden(driver, "CopyTeamToNewYearButton", "Copy Team to New Year");
            Assert.True(AndroidElementWait.UntilText(driver, "Deactivate Team").Displayed);
            summary.Click();

            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamYearSummaryHeader",
                "Automation Team — 2026 Year Summary");
            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
            AndroidElementWait.UntilText(driver, "Automation Team");
            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            Assert.True(AndroidElementWait.UntilText(driver, "Deactivate Team").Displayed);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Team_year_over_requires_confirmation_and_finalizes_team_menu),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Team Year finalization workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}");
        }
    }

    private static void AssertAdjacentDestructiveActions(
        IWebElement markYearOver,
        IWebElement deactivateTeam)
    {
        AssertWithinViewport(markYearOver);
        AssertWithinViewport(deactivateTeam);
        Assert.Equal(markYearOver.Location.X, deactivateTeam.Location.X);
        Assert.Equal(markYearOver.Size, deactivateTeam.Size);
        Assert.True(markYearOver.Location.Y < deactivateTeam.Location.Y);
        Assert.InRange(
            deactivateTeam.Location.Y - (markYearOver.Location.Y + markYearOver.Size.Height),
            0,
            12);
    }

    private static void AssertMenuActionHidden(
        AppiumDriver driver,
        string automationId,
        string text)
    {
        Assert.DoesNotContain(
            driver.FindElements(By.Id(
                $"{AndroidAutomationConstants.AppPackage}:id/{automationId}")),
            element => element.Displayed);
        Assert.DoesNotContain(text, driver.PageSource, StringComparison.Ordinal);
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
