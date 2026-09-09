using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class BeerTotalsTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Team_beer_totals_navigation_and_grid_are_accessible_in_both_orientations()
    {
        fixture.ResetApplication(AndroidAutomationConstants.BeerTotalsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenTeamWorkspace(driver);
            Assert.True(AndroidElementWait.UntilAutomationId(driver, "TeamBeerTotalsButton").Displayed);
            Assert.True(AndroidElementWait.UntilAutomationId(driver, "TeamBeerRulesButton").Displayed);
            AndroidElementWait.UntilAutomationId(driver, "TeamBeerTotalsButton").Click();
            AndroidElementWait.UntilText(driver, "Beer Totals");
            AndroidElementWait.UntilText(driver, "Automation Beer Team");
            Assert.DoesNotContain("STILL OWED", driver.PageSource, StringComparison.OrdinalIgnoreCase);

            AssertGridReachable(driver, ScreenOrientation.Landscape);
            AssertGridReachable(driver, ScreenOrientation.Portrait);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(nameof(Team_beer_totals_navigation_and_grid_are_accessible_in_both_orientations), exception);
        }
    }

    [AndroidUiFact]
    public void Effective_count_and_brought_edits_refresh_signed_owed_and_persist()
    {
        fixture.ResetApplication(AndroidAutomationConstants.BeerTotalsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenBeerTotals(driver);
            Assert.Equal("-2", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);

            EditValue(driver, "BeerTotalsStrikeout-1", "2");
            Assert.Equal("2", FindByAutomationId(driver, "BeerTotalsStrikeout-1").Text);
            Assert.Equal("2", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);

            EditValue(driver, "BeerTotalsBrought-1", "8");
            Assert.Equal("-1", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);
            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamBeerTotalsButton").Click();
            Assert.Equal("-1", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);

            FindByAutomationId(driver, "BeerTotalsStrikeout-1").Click();
            var entry = AndroidElementWait.UntilAutomationId(driver, "BeerTotalsStrikeoutEdit-1");
            entry.Clear();
            entry.SendKeys("-1");
            PerformDoneAction(driver);
            AndroidElementWait.UntilText(driver, "Enter a non-negative whole number.");
            Assert.True(entry.Displayed);
            Assert.Equal("2", FindByAutomationId(driver, "BeerTotalsStrikeout-1").Text);
            Assert.Equal("0", FindByAutomationId(driver, "BeerTotalsStrikeout-2").Text);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(nameof(Effective_count_and_brought_edits_refresh_signed_owed_and_persist), exception);
        }
    }

    private void AssertGridReachable(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        Assert.True(FindByAutomationId(driver, "BeerTotalsTriplePlay-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsLowMan-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsOwed-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsBrought-1").Displayed);
    }

    private static void OpenBeerTotals(AndroidDriver driver)
    {
        OpenTeamWorkspace(driver);
        AndroidElementWait.UntilAutomationId(driver, "TeamBeerTotalsButton").Click();
        AndroidElementWait.UntilText(driver, "Beer Totals");
    }

    private static void OpenTeamWorkspace(AndroidDriver driver) =>
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();

    private static void EditValue(AndroidDriver driver, string cellId, string value)
    {
        FindByAutomationId(driver, cellId).Click();
        var separator = cellId.LastIndexOf('-');
        var editId = $"{cellId[..separator]}Edit{cellId[separator..]}";
        var entry = AndroidElementWait.UntilAutomationId(driver, editId);
        entry.Clear();
        entry.SendKeys(value);
        PerformDoneAction(driver);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.Until(_ => driver.FindElements(By.Id(
            $"{AndroidAutomationConstants.AppPackage}:id/{editId}")).Count == 0);
    }

    private static void PerformDoneAction(AndroidDriver driver) =>
        driver.ExecuteScript(
            "mobile: performEditorAction",
            new Dictionary<string, object> { ["action"] = "done" });

    private static IWebElement FindByAutomationId(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        var matches = driver.FindElements(By.Id(resourceId));
        if (matches.Count > 0)
            return matches[0];
        return driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().scrollable(true)).setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
    }

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Beer Totals workflow failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}");
    }
}
