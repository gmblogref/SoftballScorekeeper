using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

internal static class AndroidTeamWorkspaceMenu
{
    public static void Select(AndroidDriver driver, string actionText)
    {
        Open(driver);
        AndroidElementWait.UntilText(driver, actionText).Click();
    }

    public static IWebElement UntilAvailable(AndroidDriver driver)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait.Until(currentDriver =>
            currentDriver.FindElements(By.Id(
                    $"{AndroidAutomationConstants.AppPackage}:id/TeamWorkspaceMenuButton"))
                .Concat(currentDriver.FindElements(MobileBy.AccessibilityId("TeamWorkspaceMenuButton")))
                .FirstOrDefault(element => element.Displayed)
            ?? throw new NoSuchElementException());
    }

    private static void Open(AndroidDriver driver) => UntilAvailable(driver).Click();
}
