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
