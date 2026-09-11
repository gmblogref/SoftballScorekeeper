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

    private void AssertOrientation(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearGamesPlayed", "3");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearWins", "1");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearLosses", "1");
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYearTies", "1");

        Assert.Equal("3", ScrollTo(driver, "CompletedBattingPlateAppearancesPosition1").Text);
        Assert.Equal("2", ScrollTo(driver, "CompletedBattingHitsPosition1").Text);
        Assert.Equal("1", ScrollTo(driver, "CompletedBattingHomeRunsPosition1").Text);
        Assert.True(ScrollTo(driver, "CompletedBattingTeamTotal").Displayed);

        var beerGrid = ScrollTo(driver, "TeamYearBeerSummaryGrid");
        var beerRow = ScrollTo(driver, "TeamYearBeerRow-1");
        AssertRowValueAlignedWithHeader(driver, beerRow, "DBO", "1");
        SwipeLeft(driver, beerGrid);
        beerRow = ScrollTo(driver, "TeamYearBeerRow-1");
        AssertRowValueAlignedWithHeader(driver, beerRow, "Low Man", "4");
        AssertRowValueAlignedWithHeader(driver, beerRow, "BROUGHT", "2");
        AssertRowValueAlignedWithHeader(driver, beerRow, "OWED", "11");

        var pageSource = driver.PageSource;
        Assert.DoesNotContain("android.widget.EditText", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Finalize", pageSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Freeze", pageSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recalculate", pageSource, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRowValueAlignedWithHeader(
        AndroidDriver driver,
        IWebElement row,
        string headerText,
        string expectedValue)
    {
        var header = AndroidElementWait.UntilText(driver, headerText);
        var values = row.FindElements(By.XPath($".//*[@text='{expectedValue}']"))
            .Where(item => item.Displayed)
            .ToArray();
        Assert.NotEmpty(values);
        Assert.InRange(
            values.Min(value => Math.Abs(CenterX(value) - CenterX(header))),
            0,
            8);
    }

    private static IWebElement ScrollTo(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        var displayed = driver.FindElements(By.Id(resourceId))
            .FirstOrDefault(item => item.Displayed);
        if (displayed is not null)
            return displayed;

        return driver.FindElement(MobileBy.AndroidUIAutomator(
            "new UiScrollable(new UiSelector().scrollable(true).instance(0))"
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
}
