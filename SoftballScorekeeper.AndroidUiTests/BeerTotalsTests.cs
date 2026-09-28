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
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            AndroidElementWait.UntilText(driver, "Beer Totals");
            AndroidElementWait.UntilText(driver, "Automation Beer Team");
            Assert.DoesNotContain("STILL OWED", driver.PageSource, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TEAM TOTAL", driver.PageSource, StringComparison.OrdinalIgnoreCase);

            var alex = AndroidElementWait.UntilText(driver, "Alex Automation");
            var blake = AndroidElementWait.UntilText(driver, "Blake Automation");
            var historical = AndroidElementWait.UntilText(driver, "Zulu Historical");
            Assert.True(alex.Location.Y < blake.Location.Y);
            Assert.True(blake.Location.Y < historical.Location.Y);
            Assert.Equal("1", FindByAutomationId(driver, "BeerTotalsDbo-3").Text);
            Assert.Equal("1", FindByAutomationId(driver, "BeerTotalsOwed-3").Text);

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
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            Assert.Equal("-1", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);

            FindByAutomationId(driver, "BeerTotalsStrikeout-1").Click();
            var entry = AndroidElementWait.UntilAutomationId(driver, "BeerTotalsStrikeoutEdit-1");
            entry.Clear();
            entry.SendKeys("-1");
            PerformDoneAction(driver);
            AndroidElementWait.UntilText(driver, "Enter a non-negative whole number.");
            Assert.True(entry.Displayed);
            Assert.Equal("2", FindByAutomationId(driver, "BeerTotalsStrikeout-1").Text);
            driver.HideKeyboard();
            Assert.Equal("0", FindByAutomationId(driver, "BeerTotalsStrikeout-2").Text);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(nameof(Effective_count_and_brought_edits_refresh_signed_owed_and_persist), exception);
        }
    }

    [AndroidUiFact]
    public void Calculate_low_man_for_selected_game_night_refreshes_low_man_and_owed()
    {
        fixture.ResetApplication(AndroidAutomationConstants.BeerTotalsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenBeerTotals(driver);
            Assert.Equal("2", FindByAutomationId(driver, "BeerTotalsLowMan-1").Text);
            Assert.Equal("-2", FindByAutomationId(driver, "BeerTotalsOwed-1").Text);

            var picker = AndroidElementWait.UntilAutomationId(driver, "LowManGameNightPicker");
            Assert.Equal("Calculate Low Man",
                AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Text);
            Assert.Contains("Aug 15, 2026", picker.Text, StringComparison.Ordinal);
            Assert.Contains("1 of 2 Games completed", picker.Text, StringComparison.Ordinal);
            AndroidElementWait.UntilText(
                driver,
                "1 incomplete Game(s) will be excluded from this calculation.");
            picker.Click();
            AndroidElementWait.UntilText(
                driver,
                "Aug 15, 2026 — 1 of 2 Games completed").Click();
            AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Click();

            AndroidElementWait.UntilAutomationIdText(driver, "BeerTotalsLowMan-1", "3");
            AndroidElementWait.UntilAutomationIdText(driver, "BeerTotalsOwed-1", "-1");
            AndroidElementWait.UntilText(
                driver,
                "Low Man: Alex Automation");
            Assert.Equal("Recalculate Low Man",
                AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Text);
            Assert.Equal("0", FindByAutomationId(driver, "BeerTotalsLowMan-2").Text);
            Assert.Equal("0", FindByAutomationId(driver, "BeerTotalsOwed-2").Text);

            AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Click();
            AndroidElementWait.UntilAutomationIdText(driver, "BeerTotalsLowMan-1", "3");
            AndroidElementWait.UntilAutomationIdText(driver, "BeerTotalsOwed-1", "-1");
            Assert.Equal("Recalculate Low Man",
                AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Text);

            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            AndroidElementWait.UntilText(driver, "Low Man: Alex Automation");
            Assert.Equal("Recalculate Low Man",
                AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Text);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Calculate_low_man_for_selected_game_night_refreshes_low_man_and_owed),
                exception);
        }
    }

    private void AssertGridReachable(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        Assert.True(FindByAutomationId(driver, "BeerTotalsDbo-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsSoloHomeRun-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsDoublePlay-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsTriplePlay-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsTwoOuts-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsStrikeout-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsLowMan-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsOwed-1").Displayed);
        Assert.True(FindByAutomationId(driver, "BeerTotalsBrought-1").Displayed);
    }

    private static void OpenBeerTotals(AndroidDriver driver)
    {
        OpenTeamWorkspace(driver);
        AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
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
