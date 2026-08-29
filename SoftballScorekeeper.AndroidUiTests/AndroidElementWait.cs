using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

internal static class AndroidElementWait
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static IWebElement UntilAutomationId(
        AndroidDriver driver,
        string automationId,
        TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(driver, timeout ?? DefaultTimeout);
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));

        return wait.Until(currentDriver =>
            currentDriver.FindElements(By.Id(ResourceId(automationId)))
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException());
    }

    public static IWebElement UntilText(
        AndroidDriver driver,
        string text,
        TimeSpan? timeout = null)
    {
        var wait = CreateWait(driver, timeout);
        return wait.Until(currentDriver =>
            currentDriver.FindElements(By.XPath($"//*[@text={ToXPathLiteral(text)}]"))
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException());
    }

    public static IWebElement UntilAutomationIdText(
        AndroidDriver driver,
        string automationId,
        string expectedText,
        TimeSpan? timeout = null)
    {
        var wait = CreateWait(driver, timeout);
        return wait.Until(currentDriver =>
        {
            var element = currentDriver.FindElements(By.Id(ResourceId(automationId)))
                .FirstOrDefault(candidate => candidate.Displayed);
            return element is not null
                && string.Equals(element.Text, expectedText, StringComparison.Ordinal)
                ? element
                : null;
        }) ?? throw new NoSuchElementException();
    }

    public static void UntilInningAndOutState(
        AndroidDriver driver,
        string expectedInning,
        string expectedOuts,
        TimeSpan? timeout = null)
    {
        var wait = CreateWait(driver, timeout);
        wait.Until(currentDriver =>
        {
            var combinedState = currentDriver
                .FindElements(By.Id(ResourceId("InningOutStateLabel")))
                .FirstOrDefault(element => element.Displayed);
            if (combinedState is not null)
                return string.Equals(
                    combinedState.Text,
                    $"{expectedInning}    {expectedOuts}",
                    StringComparison.Ordinal);

            var inningState = currentDriver
                .FindElements(By.Id(ResourceId("InningStateLabel")))
                .FirstOrDefault(element => element.Displayed);
            var outState = currentDriver
                .FindElements(By.Id(ResourceId("OutsStateLabel")))
                .FirstOrDefault(element => element.Displayed);
            return inningState is not null
                && outState is not null
                && string.Equals(inningState.Text, expectedInning, StringComparison.Ordinal)
                && string.Equals(outState.Text, expectedOuts, StringComparison.Ordinal);
        });
    }

    private static WebDriverWait CreateWait(AndroidDriver driver, TimeSpan? timeout)
    {
        var wait = new WebDriverWait(driver, timeout ?? DefaultTimeout);
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait;
    }

    private static string ToXPathLiteral(string value) =>
        !value.Contains('\'')
            ? $"'{value}'"
            : $"concat('{value.Replace("'", "',\"'\",'")}')";

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
}
