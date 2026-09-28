using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class HomeRunLimitScoringTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Home_run_limit_one_up_dbo_and_strikeout_preserve_authoritative_state()
    {
        AndroidScoringWorkflow.Run(fixture, nameof(
            Home_run_limit_one_up_dbo_and_strikeout_preserve_authoritative_state), driver =>
        {
            AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton").Click();
            SetVisibleGameRules(driver, homeRunLimit: "1", oneUpEnabled: false);
            AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            AndroidElementWait.UntilText(driver, "HR 0/1");

            AndroidScoringWorkflow.SelectResult(driver, "Home Run");
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "1");
            AndroidElementWait.UntilText(driver, "HR 1/1");

            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.SelectResultMenu(driver);
            AndroidElementWait.UntilText(driver, "Dead Ball Out");
            Assert.DoesNotContain(
                driver.FindElements(By.XPath("//*[@text='Home Run']")), item => item.Displayed);
            AndroidElementWait.UntilText(driver, "Dead Ball Out").Click();
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, AndroidAutomationConstants.FourthBatterName, "Top 1", "1 Out");
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "1");

            AndroidScoringWorkflow.SelectResult(driver, "Strikeout");
            AndroidScoringWorkflow.AssertBatterInningAndOuts(
                driver, "Evan Automation", "Top 1", "2 Outs");
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", AndroidAutomationConstants.SecondBatterName);

            AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton").Click();
            AndroidElementWait.UntilText(driver, "Game Rules").Click();
            SetVisibleGameRules(driver, homeRunLimit: "1", oneUpEnabled: true);
            AndroidElementWait.UntilAutomationId(driver, "ConfirmGameRulesButton").Click();
            AndroidElementWait.UntilText(driver, "1-UP ON");

            AndroidScoringWorkflow.SelectResult(driver, "OUT");
            AndroidElementWait.UntilText(driver, "OK").Click();
            var opponentEntries = AndroidScoringWorkflow.VisibleEditTexts(driver);
            Assert.Equal(2, opponentEntries.Length);
            opponentEntries[0].SendKeys("1");
            opponentEntries[1].SendKeys("1");
            AndroidElementWait.UntilAutomationId(
                driver, "OpponentHalfInningSubmitButton").Click();

            AndroidScoringWorkflow.SelectResult(driver, "Home Run");
            AndroidElementWait.UntilAutomationIdText(driver, "VisitorScoreValue", "2");
            AndroidElementWait.UntilAutomationIdText(driver, "HomeScoreValue", "1");
            AndroidElementWait.UntilText(driver, "HR 2");
            AndroidScoringWorkflow.SelectResult(driver, "Single");
            AndroidScoringWorkflow.CompletePendingPlay(driver);
            AndroidScoringWorkflow.AssertRunner(
                driver, "FirstBaseRunnerButton", "Gray Automation");
        });
    }

    private static void SetVisibleGameRules(
        OpenQA.Selenium.Appium.Android.AndroidDriver driver,
        string homeRunLimit,
        bool oneUpEnabled)
    {
        var limit = AndroidScoringWorkflow.VisibleEditTexts(driver).Single();
        limit.Clear();
        limit.SendKeys(homeRunLimit);
        var oneUp = AndroidScoringWorkflow.VisibleElements(
            driver, "android.widget.Switch").Single();
        if (string.Equals(oneUp.GetAttribute("checked"), "true", StringComparison.Ordinal)
            != oneUpEnabled)
            oneUp.Click();
    }
}
