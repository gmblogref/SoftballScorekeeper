using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ScheduleGameLifecycleTests(AndroidAppiumFixture fixture)
{
    private const string DuplicateMessage =
        "A Game is already scheduled for this Team, Season, date, and time. Change the date or time and try again.";

    [AndroidUiFact]
    public void Single_game_can_be_scheduled_edited_cancelled_and_removed_with_duplicate_feedback()
    {
        PhaseAUiWorkflow.Run(
            fixture,
            nameof(Single_game_can_be_scheduled_edited_cancelled_and_removed_with_duplicate_feedback),
            driver =>
            {
                OpenSeededTeam(driver);
                AndroidElementWait.UntilText(driver, "Add Game").Click();
                AndroidElementWait.UntilText(driver, "Schedule Game");
                ToggleSwitch(driver, "Create New Season");
                ToggleSwitch(driver, "Add Second Game");
                AndroidElementWait.UntilText(driver, "Enter Season name")
                    .SendKeys("Phase A New Season");
                AssertDefaultTime(driver);
                AndroidElementWait.UntilText(driver, "Save").Click();

                AssertScheduledRow(driver, "Phase A New Season", expectEditedValues: false);
                OpenScheduledGame(driver);
                AndroidElementWait.UntilText(driver, "Edit Game").Click();
                AndroidElementWait.UntilText(driver, "Edit Game");
                AndroidElementWait.UntilText(driver, "Optional opponent")
                    .SendKeys("Phase A Opponent");
                AndroidElementWait.UntilText(driver, "Optional field").SendKeys("Diamond 9");
                AndroidElementWait.UntilText(driver, "Save").Click();
                AndroidElementWait.UntilText(driver, "Phase A Opponent");
                AndroidElementWait.UntilText(driver, "Diamond 9");
                PhaseAUiWorkflow.NavigateUp(driver);
                AssertScheduledRow(driver, "Phase A New Season", expectEditedValues: true);

                AndroidElementWait.UntilText(driver, "Add Game").Click();
                AndroidElementWait.UntilText(driver, "Schedule Game");
                ToggleSwitch(driver, "Add Second Game");
                AssertDefaultTime(driver);
                AndroidElementWait.UntilText(driver, "Save").Click();
                AndroidElementWait.UntilText(driver, DuplicateMessage);
                AndroidElementWait.UntilText(driver, "Cancel").Click();
                AssertScheduledRow(driver, "Phase A New Season", expectEditedValues: true);

                EnterRemovalAndSelectOnlyGame(driver);
                AndroidElementWait.UntilText(driver, "Cancel").Click();
                AssertScheduledRow(driver, "Phase A New Season", expectEditedValues: true);

                EnterRemovalAndSelectOnlyGame(driver);
                AndroidElementWait.UntilText(driver, "Remove Selected").Click();
                AndroidElementWait.UntilText(driver, "Remove Scheduled Games");
                AndroidElementWait.UntilText(driver, "Remove").Click();
                AndroidElementWait.UntilText(
                    driver, "Summer 2", TimeSpan.FromSeconds(30));
                PhaseAUiWorkflow.AssertTextAbsent(driver, "Phase A Opponent");
                PhaseAUiWorkflow.AssertTextAbsent(driver, "Phase A New Season");
                Assert.True(AndroidTeamWorkspaceMenu.UntilAvailable(driver).Displayed);
            },
            AndroidAutomationConstants.ScheduleGamesTestSeedRequestFileName);
    }

    private static void OpenSeededTeam(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton");
    }

    private static void AssertDefaultTime(AndroidDriver driver) =>
        Assert.Contains(
            PhaseAUiWorkflow.Visible(driver, "android.widget.EditText")
                .Concat(PhaseAUiWorkflow.Visible(driver, "android.widget.Button")),
            item => item.Text.Contains("6:30", StringComparison.Ordinal)
                && item.Text.Contains("PM", StringComparison.OrdinalIgnoreCase));

    private static void AssertScheduledRow(
        AndroidDriver driver,
        string season,
        bool expectEditedValues)
    {
        AndroidElementWait.UntilText(driver, season);
        AndroidElementWait.UntilText(
            driver,
            DateTime.Today.ToString("MMM d", CultureInfo.CurrentCulture));
        Assert.Contains(
            driver.FindElements(By.XPath("//*[@text]")),
            item => item.Displayed
                && item.Text.Contains("6:30", StringComparison.Ordinal)
                && item.Text.Contains("PM", StringComparison.OrdinalIgnoreCase));
        if (expectEditedValues)
        {
            AndroidElementWait.UntilText(driver, "Phase A Opponent");
            AndroidElementWait.UntilText(driver, "Diamond 9");
        }
        else
        {
            Assert.True(driver.FindElements(By.XPath("//*[@text='TBD']"))
                .Count(item => item.Displayed) >= 2);
        }
    }

    private static void OpenScheduledGame(AndroidDriver driver)
    {
        var time = driver.FindElements(By.XPath("//*[@text]"))
            .First(item => item.Displayed
                && item.Text.Contains("6:30", StringComparison.Ordinal)
                && item.Text.Contains("PM", StringComparison.OrdinalIgnoreCase));
        PhaseAUiWorkflow.TapCenter(driver, time);
        AndroidElementWait.UntilText(driver, "Game Details");
    }

    private static void EnterRemovalAndSelectOnlyGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilText(driver, "Remove Games").Click();
        var game = AndroidElementWait.UntilText(driver, "Phase A Opponent");
        PhaseAUiWorkflow.NearestVisibleCheckbox(driver, game).Click();
    }

    private static void ToggleSwitch(AndroidDriver driver, string labelText)
    {
        var label = driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().scrollable(true)).scrollIntoView(new UiSelector().text(\"{labelText}\"))"));
        var toggle = driver.FindElements(By.XPath("//android.widget.Switch"))
            .Where(item => item.Displayed)
            .MinBy(item => Math.Abs(
                item.Location.Y + (item.Size.Height / 2)
                - label.Location.Y - (label.Size.Height / 2)));
        Assert.NotNull(toggle);
        toggle.Click();
    }
}
