using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class BeerRulesConfigurationTests
{
    private const string TeamTitle = "Team Beer Rule Defaults";
    private const string GameTitle = "Game Beer Rules";
    private const string ProspectiveMessage =
        "Changes apply only to future Beer consequences in this Game.";
    private readonly AndroidAppiumFixture fixture;

    public BeerRulesConfigurationTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Team_beer_rules_show_defaults_and_persist_representative_edits()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamRecordTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            OpenTeamBeerRules(driver);

            AssertRule(driver, "Dbo", true, "1");
            SetToggle(driver, "Dbo", false, captureVisibleState: true);
            AssertRule(driver, "SoloHomeRun", true, "1");
            AssertRule(driver, "DoublePlay", true, "1");
            AssertRule(driver, "TriplePlay", true, "1");
            AssertRule(driver, "TwoOutsInOneInning", true, "1");
            AssertRule(driver, "Strikeout", true, "2");
            AssertRule(driver, "LowMan", true, "1");

            SetAmount(driver, "Strikeout", "3");
            SetAmount(driver, "LowMan", "2");
            AndroidElementWait.UntilAutomationId(driver, "SaveBeerRulesButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "TeamBeerRulesButton").Click();

            AndroidElementWait.UntilText(driver, TeamTitle);
            AssertRule(driver, "Dbo", false, "1");
            AssertRule(driver, "Strikeout", true, "3");
            AssertRule(driver, "LowMan", true, "2");
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Team_beer_rules_show_defaults_and_persist_representative_edits),
                exception
            );
        }
    }

    [AndroidUiFact]
    public void Game_beer_rules_exclude_low_man_and_persist_prospective_edits_after_resume()
    {
        fixture.ResetApplication();
        var driver = fixture.Driver;
        try
        {
            StartScoring(driver);
            OpenGameBeerRules(driver);

            AndroidElementWait.UntilText(driver, ProspectiveMessage);
            AssertRule(driver, "Dbo", true, "1");
            AssertRule(driver, "SoloHomeRun", true, "1");
            AssertRule(driver, "DoublePlay", true, "1");
            AssertRule(driver, "TriplePlay", true, "1");
            AssertRule(driver, "TwoOutsInOneInning", true, "1");
            AssertRule(driver, "Strikeout", true, "2");
            AssertRuleAbsent(driver, "LowMan");

            SetToggle(driver, "TriplePlay", false);
            SetAmount(driver, "Strikeout", "4");
            AndroidElementWait.UntilAutomationId(driver, "SaveBeerRulesButton").Click();

            ExitAndResumeScoring(driver);
            OpenGameBeerRules(driver);

            AndroidElementWait.UntilText(driver, ProspectiveMessage);
            AssertRule(driver, "TriplePlay", false, "1");
            AssertRule(driver, "Strikeout", true, "4");
            AssertRuleAbsent(driver, "LowMan");
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Game_beer_rules_exclude_low_man_and_persist_prospective_edits_after_resume),
                exception
            );
        }
    }

    [AndroidUiFact]
    public void Beer_rules_navigation_and_dialog_controls_work_in_portrait_and_landscape()
    {
        fixture.ResetApplication();
        var driver = fixture.Driver;
        try
        {
            StartScoring(driver);
            AssertGameDialogUsable(driver, ScreenOrientation.Landscape);
            AssertGameDialogUsable(driver, ScreenOrientation.Portrait);

            fixture.SetOrientation(ScreenOrientation.Landscape);
            ExitScoringToGameWorkspace(driver);
            driver.Navigate().Back();
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();

            AssertTeamDialogUsable(driver, ScreenOrientation.Landscape);
            AssertTeamDialogUsable(driver, ScreenOrientation.Portrait);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Beer_rules_navigation_and_dialog_controls_work_in_portrait_and_landscape),
                exception
            );
        }
    }

    private void AssertGameDialogUsable(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        OpenGameBeerRules(driver);
        AssertDialogControlsWithinViewport(driver, GameTitle);
        AndroidElementWait.UntilAutomationId(driver, "CancelBeerRulesButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatter");
    }

    private void AssertTeamDialogUsable(AndroidDriver driver, ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        AndroidElementWait.UntilAutomationId(driver, "TeamBeerRulesButton").Click();
        AssertDialogControlsWithinViewport(driver, TeamTitle);
        AndroidElementWait.UntilAutomationId(driver, "CancelBeerRulesButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamBeerRulesButton");
    }

    private static void AssertDialogControlsWithinViewport(AndroidDriver driver, string title)
    {
        AssertWithinViewport(driver, AndroidElementWait.UntilText(driver, title));
        AssertWithinViewport(
            driver,
            AndroidElementWait.UntilAutomationId(driver, "CancelBeerRulesButton")
        );
        AssertWithinViewport(
            driver,
            AndroidElementWait.UntilAutomationId(driver, "SaveBeerRulesButton")
        );
    }

    private static void AssertWithinViewport(AndroidDriver driver, IWebElement element)
    {
        var viewport = driver.Manage().Window.Size;
        Assert.True(element.Displayed);
        Assert.InRange(element.Location.X, 0, viewport.Width - 1);
        Assert.InRange(element.Location.Y, 0, viewport.Height - 1);
        Assert.InRange(element.Location.X + element.Size.Width, 1, viewport.Width);
        Assert.InRange(element.Location.Y + element.Size.Height, 1, viewport.Height);
    }

    private static void OpenTeamBeerRules(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
        AndroidElementWait.UntilAutomationId(driver, "TeamBeerRulesButton").Click();
        AndroidElementWait.UntilText(driver, TeamTitle);
    }

    private static void StartScoring(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatter");
    }

    private static void OpenGameBeerRules(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "GameBeerRulesButton").Click();
        AndroidElementWait.UntilText(driver, GameTitle);
    }

    private static void ExitAndResumeScoring(AndroidDriver driver)
    {
        ExitScoringToGameWorkspace(driver);
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatter");
    }

    private static void ExitScoringToGameWorkspace(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
    }

    private static void AssertRule(
        AndroidDriver driver,
        string ruleName,
        bool expectedEnabled,
        string expectedAmount
    )
    {
        var toggle = ScrollToAutomationId(driver, $"BeerRule{ruleName}Toggle");
        var amount = ScrollToAutomationId(driver, $"BeerRule{ruleName}Amount");
        Assert.Equal(expectedEnabled.ToString().ToLowerInvariant(), toggle.GetAttribute("checked"));
        Assert.Equal(expectedAmount, amount.Text);
    }

    private static void AssertRuleAbsent(AndroidDriver driver, string ruleName)
    {
        ScrollToAutomationId(driver, "BeerRuleStrikeoutAmount");
        Assert.Empty(driver.FindElements(By.Id(ResourceId($"BeerRule{ruleName}Toggle"))));
        Assert.DoesNotContain("Low Man", driver.PageSource, StringComparison.Ordinal);
    }

    private static void SetToggle(
        AndroidDriver driver,
        string ruleName,
        bool enabled,
        bool captureVisibleState = false
    )
    {
        var toggle = ScrollToAutomationId(driver, $"BeerRule{ruleName}Toggle");
        var expected = enabled.ToString().ToLowerInvariant();
        if (
            !string.Equals(
                toggle.GetAttribute("checked"),
                expected,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (captureVisibleState)
                CaptureSwitchState(driver, ruleName, "before-tap");
            toggle.Click();
            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(_ =>
                    string.Equals(
                        toggle.GetAttribute("checked"),
                        expected,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            }
            finally
            {
                if (captureVisibleState)
                    CaptureSwitchState(driver, ruleName, "after-tap");
            }
        }
    }

    private static void CaptureSwitchState(AndroidDriver driver, string ruleName, string state)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "SwitchState");
        Directory.CreateDirectory(directory);
        driver.GetScreenshot().SaveAsFile(Path.Combine(directory, $"{ruleName}-{state}.png"));
    }

    private static void SetAmount(AndroidDriver driver, string ruleName, string amount)
    {
        var entry = ScrollToAutomationId(driver, $"BeerRule{ruleName}Amount");
        entry.Clear();
        entry.SendKeys(amount);
        try
        {
            driver.HideKeyboard();
        }
        catch (WebDriverException) { }
    }

    private static IWebElement ScrollToAutomationId(AndroidDriver driver, string automationId)
    {
        var resourceId = ResourceId(automationId);
        return driver.FindElement(
            MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().scrollable(true)).scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"
            )
        );
    }

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Beer Rules workflow failed. Diagnostics: {artifacts}"
                + $"{Environment.NewLine}{exception}"
        );
    }
}
