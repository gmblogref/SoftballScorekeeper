using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamYearCopyTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Copy_team_to_new_year_preserves_current_configuration_without_history()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamYearCopyTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            AssertRoster(driver, includeNonRosterPlayers: false);

            AndroidTeamWorkspaceMenu.Select(driver, "Copy Team to New Year");
            AndroidElementWait.UntilText(driver, "Copy Team to New Year?");
            Assert.Contains("Automation Copy Team", driver.PageSource, StringComparison.Ordinal);
            Assert.Contains("2026", driver.PageSource, StringComparison.Ordinal);
            Assert.Contains("2027", driver.PageSource, StringComparison.Ordinal);
            AndroidElementWait.UntilText(driver, "Copy Team").Click();

            AndroidTeamWorkspaceMenu.UntilAvailable(driver);
            AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton").Click();
            AndroidElementWait.UntilText(
                driver,
                "No Games have been scheduled for this Team.");
            AssertRoster(driver, includeNonRosterPlayers: false);

            AndroidTeamWorkspaceMenu.Select(driver, "Beer Rules");
            AndroidElementWait.UntilText(driver, "Team Beer Rule Defaults");
            Assert.Equal("false", ScrollToAutomationId(driver, "BeerRuleDboToggle")
                .GetAttribute("checked"));
            Assert.Equal("4", ScrollToAutomationId(driver, "BeerRuleStrikeoutAmount").Text);
            AndroidElementWait.UntilAutomationId(driver, "CancelBeerRulesButton").Click();

            AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton").Click();
            AndroidElementWait.UntilText(driver, "Add Game").Click();
            AndroidElementWait.UntilText(driver, "Schedule Game");
            var selectedSeason = AndroidElementWait.UntilText(driver, "Summer 2027");
            selectedSeason.Click();
            AndroidElementWait.UntilText(driver, "Spring 2027");
            AndroidElementWait.UntilText(driver, "Summer 2027");
            Assert.DoesNotContain("Legacy 2025", driver.PageSource, StringComparison.Ordinal);

            AndroidElementWait.UntilText(driver, "Cancel").Click();
            AndroidElementWait.UntilText(driver, "Cancel").Click();
            AndroidElementWait.UntilAutomationId(
                driver, "TeamWorkspaceGamesSectionButton");
            PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
            AndroidElementWait.UntilAutomationId(driver, "TeamYearGroup-2027");
            ExpandYearAndFindVisibleCard(driver, 2026).Click();
            AndroidTeamWorkspaceMenu.Select(driver, "Copy Team to New Year");
            AndroidElementWait.UntilText(driver, "Copy Team to New Year?");
            AndroidElementWait.UntilText(driver, "Copy Team").Click();
            AndroidElementWait.UntilText(
                driver,
                "The destination Team Year already exists. Use the existing Team or reactivate it separately.");
            PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
            Assert.DoesNotContain(
                driver.FindElements(By.Id(ResourceId("TeamYearGroup-2028"))),
                element => element.Displayed);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Copy_team_to_new_year_preserves_current_configuration_without_history),
                exception);
        }
    }

    [AndroidUiFact]
    public void Copied_team_beer_totals_show_roster_before_signed_nonroster_balances()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamYearCopyTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            CopyTeam(driver);

            AssertRoster(driver, includeNonRosterPlayers: false);
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            AndroidElementWait.UntilText(driver, "Beer Totals");

            var mike = AndroidElementWait.UntilText(driver, "Mike Current");
            var zulu = AndroidElementWait.UntilText(driver, "Zulu Current");
            var aaron = AndroidElementWait.UntilText(driver, "Aaron Owed");
            var beta = AndroidElementWait.UntilText(driver, "Beta Credit");
            Assert.True(mike.Location.Y < zulu.Location.Y);
            Assert.True(zulu.Location.Y < aaron.Location.Y);
            Assert.True(zulu.Location.Y < beta.Location.Y);
            Assert.DoesNotContain("Zero Balance", driver.PageSource, StringComparison.Ordinal);

            foreach (var playerId in new[] { 1, 2, 3, 4 })
            {
                foreach (var rule in new[]
                {
                    "Dbo", "SoloHomeRun", "DoublePlay", "TriplePlay",
                    "TwoOuts", "Strikeout", "LowMan",
                })
                    Assert.Equal("0", ScrollToAutomationId(
                        driver, $"BeerTotals{rule}-{playerId}").Text);
                Assert.Equal("0", ScrollToAutomationId(
                    driver, $"BeerTotalsBrought-{playerId}").Text);
            }

            Assert.Equal("0", ScrollToAutomationId(driver, "BeerTotalsOwed-1").Text);
            Assert.Equal("0", ScrollToAutomationId(driver, "BeerTotalsOwed-2").Text);
            Assert.Equal("3", ScrollToAutomationId(driver, "BeerTotalsOwed-3").Text);
            Assert.Equal("-2", ScrollToAutomationId(driver, "BeerTotalsOwed-4").Text);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Copied_team_beer_totals_show_roster_before_signed_nonroster_balances),
                exception);
        }
    }

    private static void CopyTeam(AndroidDriver driver)
    {
        AndroidTeamWorkspaceMenu.Select(driver, "Copy Team to New Year");
        AndroidElementWait.UntilText(driver, "Copy Team to New Year?");
        AndroidElementWait.UntilText(driver, "Copy Team").Click();
        AndroidTeamWorkspaceMenu.UntilAvailable(driver);
    }

    private static void AssertRoster(AndroidDriver driver, bool includeNonRosterPlayers)
    {
        AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton").Click();
        AndroidElementWait.UntilText(driver, "Mike Current");
        AndroidElementWait.UntilText(driver, "Zulu Current");
        AndroidElementWait.UntilText(driver, "No. 12");
        AndroidElementWait.UntilText(driver, "No. 34");
        if (!includeNonRosterPlayers)
        {
            Assert.DoesNotContain("Aaron Owed", driver.PageSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Beta Credit", driver.PageSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Zero Balance", driver.PageSource, StringComparison.Ordinal);
        }
    }

    private static IWebElement ScrollToAutomationId(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        var matches = driver.FindElements(By.Id(resourceId));
        if (matches.Count > 0)
            return matches[0];
        return driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().scrollable(true)).setAsHorizontalList()"
            + $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
    }

    private static IWebElement ExpandYearAndFindVisibleCard(AndroidDriver driver, int year)
    {
        var nextTapUtc = DateTime.MinValue;
        return PhaseAUiWorkflow.Wait(driver).Until(current =>
        {
            var yearGroup = current.FindElements(By.Id(ResourceId($"TeamYearGroup-{year}")))
                .FirstOrDefault(element => element.Displayed);
            if (yearGroup is null)
                return null;

            if (!yearGroup.Text.Contains('▶'))
            {
                var visibleCard = current.FindElements(By.Id(ResourceId("TeamCard-1")))
                    .FirstOrDefault(element => element.Displayed);
                if (visibleCard is not null)
                    return visibleCard;
            }

            if (DateTime.UtcNow >= nextTapUtc)
            {
                yearGroup.Click();
                nextTapUtc = DateTime.UtcNow.AddSeconds(1);
            }
            return null;
        })!;
    }

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Team Year copy workflow failed. Diagnostics: {artifacts}"
            + $"{Environment.NewLine}{exception}");
    }
}
