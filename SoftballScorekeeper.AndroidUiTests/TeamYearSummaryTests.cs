using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamYearSummaryTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Team_year_summary_is_successor_gated_and_loads_read_only_data_in_both_orientations()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.TeamYearSummaryTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-2", "2027");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            Assert.DoesNotContain(
                driver.FindElements(By.Id(
                    $"{AndroidAutomationConstants.AppPackage}:id/TeamYearSummaryButton")),
                element => element.Displayed);
            Assert.DoesNotContain(
                "Team Year Summary",
                driver.PageSource,
                StringComparison.Ordinal);
            AndroidTeamWorkspaceMenu.UntilAvailable(driver).Click();
            driver.FindElement(MobileBy.AccessibilityId("Navigate up")).Click();

            AndroidElementWait.UntilAutomationId(driver, "TeamYearGroup-2026").Click();
            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
            AndroidElementWait.UntilText(driver, "Automation Year Summary");
            AndroidTeamWorkspaceMenu.Select(driver, "Team Year Summary");
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamYearSummaryHeader",
                "Automation Year Summary — 2026 Year Summary");

            AssertOrientation(driver, ScreenOrientation.Landscape);
            AssertOrientation(driver, ScreenOrientation.Portrait);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Team_year_summary_is_successor_gated_and_loads_read_only_data_in_both_orientations),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Team Year Summary workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    [AndroidUiFact]
    public void Frozen_team_year_beer_grid_keeps_headers_players_and_read_only_values_aligned_in_both_orientations()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.TeamYearSummaryTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenTeamYearSummary(driver);
            AssertFrozenBeerGrid(driver, ScreenOrientation.Landscape);
            AssertFrozenBeerGrid(driver, ScreenOrientation.Portrait);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Frozen_team_year_beer_grid_keeps_headers_players_and_read_only_values_aligned_in_both_orientations),
                exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Team Year Beer grid acceptance failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    private void AssertOrientation(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearGamesPlayed", "3");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearWins", "1");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearLosses", "1");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearTies", "1");

        ScrollTo(driver, "TeamYearBattingSection");
        ScrollBattingRowIntoView(driver, "CompletedBattingRowPosition1");
        Assert.Equal("3", ScrollBattingStatisticIntoView(
            driver, "CompletedBattingPlateAppearancesPosition1").Text);
        Assert.Equal("2", ScrollBattingStatisticIntoView(
            driver, "CompletedBattingHitsPosition1").Text);
        Assert.Equal("1", ScrollBattingStatisticIntoView(
            driver, "CompletedBattingHomeRunsPosition1").Text);
        Assert.True(ScrollBattingRowIntoView(driver, "CompletedBattingTeamTotal").Displayed);

        var beerGrid = ScrollTo(driver, "TeamYearBeerSummaryGrid");
        ScrollTo(driver, "TeamYearBeerRow-1");
        AssertRowValueAlignedWithHeader(
            driver, "TeamYearBeerDboHeader", "TeamYearBeerDbo-1", "1");
        SwipeLeft(driver, beerGrid);
        ScrollTo(driver, "TeamYearBeerRow-1");
        AssertRowValueAlignedWithHeader(
            driver, "TeamYearBeerLowManHeader", "TeamYearBeerLowMan-1", "4");
        AssertRowValueAlignedWithHeader(
            driver, "TeamYearBeerBroughtHeader", "TeamYearBeerBrought-1", "2");
        AssertRowValueAlignedWithHeader(
            driver, "TeamYearBeerOwedHeader", "TeamYearBeerOwed-1", "11");

        var pageSource = driver.PageSource;
        Assert.DoesNotContain("android.widget.EditText", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Finalize", pageSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Freeze", pageSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recalculate", pageSource, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRowValueAlignedWithHeader(
        AndroidDriver driver,
        string headerAutomationId,
        string valueAutomationId,
        string expectedValue)
    {
        var header = FindDisplayedAutomationElement(driver, headerAutomationId);
        var value = FindDisplayedAutomationElement(driver, valueAutomationId);
        Assert.Equal(expectedValue, value.Text);
        Assert.InRange(Math.Abs(CenterX(value) - CenterX(header)), 0, 8);
    }

    private static void OpenTeamYearSummary(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "TeamYearGroup-2026").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidTeamWorkspaceMenu.Select(driver, "Team Year Summary");
        AndroidElementWait.UntilAutomationId(driver, "TeamYearSummaryHeader");
    }

    private void AssertFrozenBeerGrid(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        ScrollTo(driver, "TeamYearBeerSummaryGridVerticalScroll");
        PositionBeerGridInPageViewport(driver);
        var firstRow = ScrollTeamYearBeerRowIntoView(
            driver, "TeamYearBeerRow-1", towardEnd: false);
        var firstValue = ScrollTeamYearBeerValueIntoView(driver, "TeamYearBeerDbo-1");
        var playerHeader = FindDisplayedAutomationElement(driver, "TeamYearBeerPlayerHeader");
        var playerHeaderLocation = playerHeader.Location;
        var firstRowX = firstRow.Location.X;
        var headerY = playerHeader.Location.Y;

        Assert.Equal("1", firstValue.Text);
        AssertAlignedVertically(firstRow, firstValue);
        AssertColumnAligned(driver, "TeamYearBeerDboHeader", "TeamYearBeerDbo-1");

        var expectedValues = new Dictionary<string, string>
        {
            ["TeamYearBeerDbo-1"] = "1",
            ["TeamYearBeerSoloHomeRun-1"] = "0",
            ["TeamYearBeerDoublePlay-1"] = "0",
            ["TeamYearBeerTriplePlay-1"] = "2",
            ["TeamYearBeerTwoOuts-1"] = "0",
            ["TeamYearBeerStrikeout-1"] = "3",
            ["TeamYearBeerLowMan-1"] = "4",
            ["TeamYearBeerBrought-1"] = "2",
            ["TeamYearBeerOwed-1"] = "11",
        };
        foreach (var expected in expectedValues)
        {
            var value = ScrollTeamYearBeerValueIntoView(driver, expected.Key);
            Assert.Equal(expected.Value, value.Text);
            Assert.Equal("false", value.GetAttribute("clickable"));
        }

        Assert.Equal(playerHeaderLocation.X,
            FindDisplayedAutomationElement(driver, "TeamYearBeerPlayerHeader").Location.X);
        Assert.Equal(firstRowX,
            FindDisplayedAutomationElement(driver, "TeamYearBeerRow-1").Location.X);
        Assert.Equal(headerY,
            FindDisplayedAutomationElement(driver, "TeamYearBeerPlayerHeader").Location.Y);
        AssertColumnAligned(driver, "TeamYearBeerOwedHeader", "TeamYearBeerOwed-1");

        var lastRow = ScrollTeamYearBeerRowIntoView(
            driver, "TeamYearBeerRow-10", towardEnd: true);
        var lastValue = FindDisplayedAutomationElement(driver, "TeamYearBeerOwed-10");
        Assert.Equal("0", lastValue.Text);
        AssertAlignedVertically(lastRow, lastValue);
        Assert.Equal(headerY,
            FindDisplayedAutomationElement(driver, "TeamYearBeerPlayerHeader").Location.Y);
        Assert.Equal(playerHeaderLocation.X,
            FindDisplayedAutomationElement(driver, "TeamYearBeerPlayerHeader").Location.X);
        AssertColumnAligned(driver, "TeamYearBeerOwedHeader", "TeamYearBeerOwed-10");
        Assert.True(lastValue.Location.X >= lastRow.Location.X + lastRow.Size.Width - 2);
        Assert.DoesNotContain("android.widget.EditText", driver.PageSource,
            StringComparison.Ordinal);
    }

    private static IWebElement ScrollTeamYearBeerValueIntoView(
        AndroidDriver driver,
        string automationId)
    {
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("TeamYearBeerSummaryGrid")}\"))"
                + $".setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{ResourceId(automationId)}\"))"));
        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static void PositionBeerGridInPageViewport(AndroidDriver driver)
    {
        var pageScroll = FindDisplayedAutomationElement(driver, "TeamYearSummaryPageScroll");
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var beerScroll = FindDisplayedAutomationElement(
                driver, "TeamYearBeerSummaryGridVerticalScroll");
            if (beerScroll.Size.Height >= 200)
                return;

            driver.ExecuteScript(
                "mobile: dragGesture",
                new Dictionary<string, object>
                {
                    ["startX"] = pageScroll.Location.X + pageScroll.Size.Width - 80,
                    ["startY"] = pageScroll.Location.Y + pageScroll.Size.Height - 60,
                    ["endX"] = pageScroll.Location.X + pageScroll.Size.Width - 80,
                    ["endY"] = pageScroll.Location.Y + 80,
                    ["speed"] = 1200,
                });
        }

        var finalBeerScroll = FindDisplayedAutomationElement(
            driver, "TeamYearBeerSummaryGridVerticalScroll");
        Assert.True(
            finalBeerScroll.Size.Height >= 200,
            $"The Team Year Beer viewport remained only {finalBeerScroll.Size.Height} pixels high.");
    }

    private static IWebElement ScrollTeamYearBeerRowIntoView(
        AndroidDriver driver,
        string automationId,
        bool towardEnd)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var displayed = driver.FindElements(By.Id(ResourceId(automationId)))
                .FirstOrDefault(item => item.Displayed);
            if (displayed is not null)
                return displayed;

            var beerScroll = FindDisplayedAutomationElement(
                driver, "TeamYearBeerSummaryGridVerticalScroll");
            driver.ExecuteScript(
                "mobile: dragGesture",
                new Dictionary<string, object>
                {
                    ["startX"] = beerScroll.Location.X + 100,
                    ["startY"] = towardEnd
                        ? beerScroll.Location.Y + beerScroll.Size.Height - 30
                        : beerScroll.Location.Y + 30,
                    ["endX"] = beerScroll.Location.X + 100,
                    ["endY"] = towardEnd
                        ? beerScroll.Location.Y + 30
                        : beerScroll.Location.Y + beerScroll.Size.Height - 30,
                    ["speed"] = 1000,
                });
        }

        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static void AssertColumnAligned(
        AndroidDriver driver,
        string headerAutomationId,
        string valueAutomationId)
    {
        var header = FindDisplayedAutomationElement(driver, headerAutomationId);
        var value = FindDisplayedAutomationElement(driver, valueAutomationId);
        Assert.InRange(Math.Abs(CenterX(value) - CenterX(header)), 0, 8);
    }

    private static void AssertAlignedVertically(IWebElement row, IWebElement value) =>
        Assert.InRange(Math.Abs(CenterY(row) - CenterY(value)), 0, 8);

    private static IWebElement FindDisplayedAutomationElement(
        AndroidDriver driver,
        string automationId)
    {
        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(
            driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait.Until(current => current.FindElements(By.Id(ResourceId(automationId)))
            .FirstOrDefault(item => item.Displayed)
            ?? throw new NoSuchElementException())!;
    }

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private static IWebElement ScrollBattingStatisticIntoView(
        AndroidDriver driver,
        string automationId)
    {
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("CompletedBattingStats")}\"))."
                + $"setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{ResourceId(automationId)}\"))"));
        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static IWebElement ScrollBattingRowIntoView(
        AndroidDriver driver,
        string automationId)
    {
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("CompletedBattingStatsVerticalScroll")}\"))."
                + $"setAsVerticalList().scrollIntoView(new UiSelector().resourceId(\"{ResourceId(automationId)}\"))"));
        return FindDisplayedAutomationElement(driver, automationId);
    }

    private static IWebElement ScrollTo(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        var displayed = driver.FindElements(By.Id(resourceId))
            .FirstOrDefault(item => item.Displayed);
        if (displayed is not null)
            return displayed;

        return driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("TeamYearSummaryPageScroll")}\"))"
                + $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
    }

    private static void SwipeLeft(AndroidDriver driver, IWebElement element)
    {
        var location = element.Location;
        var size = element.Size;
        driver.ExecuteScript(
            "mobile: dragGesture",
            new Dictionary<string, object>
            {
                ["startX"] = location.X + size.Width - 120,
                ["startY"] = location.Y + (size.Height / 2),
                ["endX"] = location.X + 240,
                ["endY"] = location.Y + (size.Height / 2),
                ["speed"] = 1200,
            });
    }

    private static int CenterX(IWebElement element) =>
        element.Location.X + (element.Size.Width / 2);

    private static int CenterY(IWebElement element) =>
        element.Location.Y + (element.Size.Height / 2);
}
