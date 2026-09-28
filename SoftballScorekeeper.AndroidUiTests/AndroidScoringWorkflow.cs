using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

internal static class AndroidScoringWorkflow
{
    public static void Run(
        AndroidAppiumFixture fixture,
        string testName,
        Action<AndroidDriver> scenario)
    {
        fixture.ResetApplication();
        try
        {
            scenario(fixture.Driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
            throw new Xunit.Sdk.XunitException(
                $"Android scoring workflow failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}");
        }
    }

    public static void OpenPreparedScoring(AndroidDriver driver) =>
        Step("open the prepared scoring workspace", () =>
        {
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CurrentBatter", AndroidAutomationConstants.FirstBatterName);
        });

    public static void SelectResult(AndroidDriver driver, string result) =>
        Step($"select {result} for the current batter", () =>
        {
            SelectResultMenu(driver);
            AndroidElementWait.UntilText(driver, result).Click();
        });

    public static void SelectResultMenu(AndroidDriver driver) =>
        AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton").Click();

    public static IWebElement[] VisibleEditTexts(AndroidDriver driver) =>
        VisibleElements(driver, "android.widget.EditText");

    public static IWebElement[] VisibleElements(AndroidDriver driver, string className)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        return wait.Until(current =>
        {
            var entries = current
                .FindElements(By.XPath($"//*[@class='{className}']"))
                .Where(item => item.Displayed)
                .ToArray();
            return entries.Length > 0 ? entries : null;
        })!;
    }

    public static void CompletePendingPlay(AndroidDriver driver) =>
        Step("complete the pending plate appearance", () =>
            AndroidElementWait.UntilAutomationId(driver, "NextBatterButton").Click());

    public static void ExitAndResume(AndroidDriver driver) =>
        Step("exit and resume scoring", () =>
        {
            AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
            AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton").Click();
        });

    public static void AssertRunner(
        AndroidDriver driver,
        string automationId,
        string playerName) =>
        Step($"verify {playerName} at {automationId}", () =>
            AndroidElementWait.UntilAutomationIdText(driver, automationId, playerName));

    public static void AssertEmptyBase(AndroidDriver driver, string automationId) =>
        Step($"verify {automationId} is empty", () =>
            AndroidElementWait.UntilAutomationIdText(driver, automationId, string.Empty));

    public static void AssertBatterInningAndOuts(
        AndroidDriver driver,
        string batter,
        string inning,
        string outs) =>
        Step($"verify {batter}, {inning}, and {outs}", () =>
        {
            AndroidElementWait.UntilAutomationIdText(driver, "CurrentBatter", batter);
            AndroidElementWait.UntilInningAndOutState(driver, inning, outs);
        });

    public static void Step(string description, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to {description}.", exception);
        }
    }
}
