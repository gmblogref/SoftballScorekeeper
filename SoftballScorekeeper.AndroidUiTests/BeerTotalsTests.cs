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
            Assert.True(alex.Location.Y < blake.Location.Y);
            var historical = ScrollBeerRowIntoView(driver, "BeerTotalsRow-3");
            AndroidElementWait.UntilText(driver, "Zulu Historical");
            Assert.True(historical.Displayed);
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
    public void Frozen_beer_totals_grid_keeps_headers_players_and_editing_aligned_in_both_orientations()
    {
        fixture.ResetApplication(AndroidAutomationConstants.BeerTotalsTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenBeerTotals(driver);
            foreach (var cellId in new[]
            {
                "BeerTotalsBrought-1",
                "BeerTotalsDbo-1",
                "BeerTotalsSoloHomeRun-1",
                "BeerTotalsDoublePlay-1",
                "BeerTotalsTriplePlay-1",
                "BeerTotalsTwoOuts-1",
                "BeerTotalsStrikeout-1",
                "BeerTotalsLowMan-1",
            })
                Assert.Equal("true", ScrollBeerValueIntoView(driver, cellId)
                    .GetAttribute("clickable"));

            var owed = ScrollBeerValueIntoView(driver, "BeerTotalsOwed-1");
            Assert.Equal("false", owed.GetAttribute("clickable"));
            Assert.Equal("-2", owed.Text);
            EditValue(driver, "BeerTotalsStrikeout-1", "2");
            Assert.Equal("2", ScrollBeerValueIntoView(driver, "BeerTotalsStrikeout-1").Text);
            Assert.Equal("2", ScrollBeerValueIntoView(driver, "BeerTotalsOwed-1").Text);

            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            Assert.Equal("2", ScrollBeerValueIntoView(driver, "BeerTotalsStrikeout-1").Text);
            Assert.Equal("2", ScrollBeerValueIntoView(driver, "BeerTotalsOwed-1").Text);
            Assert.Equal("1", ScrollBeerValueIntoView(driver, "BeerTotalsTriplePlay-1").Text);
            Assert.Equal("2", ScrollBeerValueIntoView(driver, "BeerTotalsLowMan-1").Text);
            Assert.Equal("5", ScrollBeerValueIntoView(driver, "BeerTotalsBrought-1").Text);
            Assert.Equal("0", ScrollBeerValueIntoView(driver, "BeerTotalsStrikeout-2").Text);
            Assert.Equal("0", ScrollBeerValueIntoView(driver, "BeerTotalsOwed-2").Text);

            AssertFrozenBeerGrid(driver, ScreenOrientation.Landscape);
            AssertFrozenBeerGrid(driver, ScreenOrientation.Portrait);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Frozen_beer_totals_grid_keeps_headers_players_and_editing_aligned_in_both_orientations),
                exception);
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

    private void AssertFrozenBeerGrid(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        var firstRow = ScrollBeerRowIntoView(driver, "BeerTotalsRow-1");
        var firstOwed = ScrollBeerValueIntoView(driver, "BeerTotalsOwed-1");
        var playerHeader = FindDisplayedAutomationElement(driver, "BeerTotalsPlayerHeader");
        var playerHeaderLocation = playerHeader.Location;
        var firstRowX = firstRow.Location.X;
        var headerY = playerHeader.Location.Y;

        AssertAlignedVertically(firstRow, firstOwed);
        AssertColumnAligned(driver, "BeerTotalsOwedHeader", "BeerTotalsOwed-1");

        ScrollBeerValueIntoView(driver, "BeerTotalsLowMan-1");
        Assert.Equal(playerHeaderLocation.X,
            FindDisplayedAutomationElement(driver, "BeerTotalsPlayerHeader").Location.X);
        Assert.Equal(firstRowX,
            FindDisplayedAutomationElement(driver, "BeerTotalsRow-1").Location.X);
        Assert.Equal(headerY,
            FindDisplayedAutomationElement(driver, "BeerTotalsPlayerHeader").Location.Y);
        AssertColumnAligned(driver, "BeerTotalsLowManHeader", "BeerTotalsLowMan-1");

        var lastRow = ScrollBeerRowIntoView(driver, "BeerTotalsRow-11");
        var lastValue = FindDisplayedAutomationElement(driver, "BeerTotalsLowMan-11");
        AssertAlignedVertically(lastRow, lastValue);
        Assert.Equal(headerY,
            FindDisplayedAutomationElement(driver, "BeerTotalsPlayerHeader").Location.Y);
        Assert.Equal(playerHeaderLocation.X,
            FindDisplayedAutomationElement(driver, "BeerTotalsPlayerHeader").Location.X);
        AssertColumnAligned(driver, "BeerTotalsLowManHeader", "BeerTotalsLowMan-11");
        Assert.True(lastValue.Location.X >= lastRow.Location.X + lastRow.Size.Width - 2);
        Assert.True(lastRow.Displayed);
        Assert.True(lastValue.Displayed);
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

    private static IWebElement ScrollBeerValueIntoView(
        AndroidDriver driver,
        string automationId)
    {
        var resourceId = ResourceId(automationId);
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("BeerTotalsScrollView")}\"))"
                + $".setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static IWebElement ScrollBeerRowIntoView(
        AndroidDriver driver,
        string automationId)
    {
        var resourceId = ResourceId(automationId);
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("BeerTotalsVerticalScroll")}\"))"
                + $".setAsVerticalList().scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static void AssertColumnAligned(
        AndroidDriver driver,
        string headerAutomationId,
        string valueAutomationId)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        wait.Until(_ =>
        {
            var header = FindDisplayedAutomationElement(driver, headerAutomationId);
            var value = FindDisplayedAutomationElement(driver, valueAutomationId);
            return Math.Abs(CenterX(header) - CenterX(value)) <= 8;
        });
        var alignedHeader = FindDisplayedAutomationElement(driver, headerAutomationId);
        var alignedValue = FindDisplayedAutomationElement(driver, valueAutomationId);
        Assert.InRange(Math.Abs(CenterX(alignedHeader) - CenterX(alignedValue)), 0, 8);
    }

    private static IWebElement FindDisplayedAutomationElement(
        AndroidDriver driver,
        string automationId)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait.Until(current => current.FindElements(By.Id(ResourceId(automationId)))
            .FirstOrDefault(item => item.Displayed)
            ?? throw new NoSuchElementException())!;
    }

    private static void AssertAlignedVertically(IWebElement row, IWebElement value) =>
        Assert.InRange(Math.Abs(CenterY(row) - CenterY(value)), 0, 8);

    private static int CenterX(IWebElement element) =>
        element.Location.X + (element.Size.Width / 2);

    private static int CenterY(IWebElement element) =>
        element.Location.Y + (element.Size.Height / 2);

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Beer Totals workflow failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}");
    }
}
