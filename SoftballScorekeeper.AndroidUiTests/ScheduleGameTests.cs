using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ScheduleGameTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Schedule_two_games_for_existing_season_succeeds()
    {
        fixture.ResetApplication(AndroidAutomationConstants.ScheduleGamesTestSeedRequestFileName);
        try
        {
            var scheduledDateDisplay = ScheduleDoubleheader(fixture.Driver);
            AssertScheduledDoubleheader(fixture.Driver, scheduledDateDisplay);

            fixture.RelaunchApplicationWithoutReset();
            OpenSeededTeam(fixture.Driver);
            AssertScheduledDoubleheader(fixture.Driver, scheduledDateDisplay);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Schedule_two_games_for_existing_season_succeeds), exception);
            throw new Xunit.Sdk.XunitException(
                $"Android schedule-game reproduction failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    private static string ScheduleDoubleheader(AndroidDriver driver)
    {
        OpenSeededTeam(driver);
        AndroidElementWait.UntilText(driver, "Add Game").Click();
        AndroidElementWait.UntilText(driver, "Schedule Game");
        AndroidElementWait.UntilText(driver, "Summer 2 2026");
        var scheduledDateDisplay = DateTime.Today.ToString(
            "MMM d",
            CultureInfo.CurrentCulture
        );
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton");
        Assert.DoesNotContain(
            "The Game schedule could not be saved",
            driver.PageSource,
            StringComparison.Ordinal);
        return scheduledDateDisplay;
    }

    private static void OpenSeededTeam(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton");
    }

    private static void AssertScheduledDoubleheader(
        AndroidDriver driver,
        string scheduledDateDisplay)
    {
        ExpandCurrentMonthIfCollapsed(driver);
        AssertVisibleTextContaining(driver, "6:30", "PM");
        AssertVisibleTextContaining(driver, "7:30", "PM");
        AssertVisibleText(driver, scheduledDateDisplay);
        Assert.True(driver.FindElements(By.XPath("//*[@text='TBD']")).Count >= 4);
        Assert.DoesNotContain(
            "The Game schedule could not be saved",
            driver.PageSource,
            StringComparison.Ordinal);
    }

    private static void ExpandCurrentMonthIfCollapsed(AndroidDriver driver)
    {
        var month = DateTime.Today.ToString("MMM", CultureInfo.CurrentCulture)
            .ToUpper(CultureInfo.CurrentCulture);
        var expandedText = $"{month} ▼";
        var collapsedText = $"{month} ▶";
        PhaseAUiWorkflow.Wait(driver).Until(current =>
        {
            var otherExpandedMonth = current.FindElements(By.XPath(
                    $"//*[@text and contains(@text, '▼') and @text != '{expandedText}']"))
                .FirstOrDefault();
            if (otherExpandedMonth is not null)
            {
                otherExpandedMonth.Click();
                return false;
            }

            var collapsedMonth = current.FindElements(By.XPath(
                    $"//*[@text='{collapsedText}']"))
                .FirstOrDefault();
            if (collapsedMonth is not null)
            {
                collapsedMonth.Click();
                return false;
            }

            return current.FindElements(By.XPath($"//*[@text='{expandedText}']")).Count > 0;
        });
    }

    private static void AssertVisibleText(AndroidDriver driver, string text) =>
        Assert.Contains(
            driver.FindElements(By.XPath($"//*[@text='{text}']")),
            element => element.Displayed);

    private static void AssertVisibleTextContaining(
        AndroidDriver driver,
        string first,
        string second) =>
        Assert.Contains(
            driver.FindElements(By.XPath("//*[@text]")),
            element =>
                element.Displayed
                && element.Text.Contains(first, StringComparison.Ordinal)
                && element.Text.Contains(second, StringComparison.Ordinal));
}
