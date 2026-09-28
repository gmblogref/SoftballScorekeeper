using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

internal static class PhaseAUiWorkflow
{
    public static void Run(
        AndroidAppiumFixture fixture,
        string testName,
        Action<AndroidDriver> scenario,
        string seedRequestFileName = AndroidAutomationConstants.TestSeedRequestFileName)
    {
        fixture.ResetApplication(seedRequestFileName);
        try
        {
            scenario(fixture.Driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
            throw new Xunit.Sdk.XunitException(
                $"Android Phase A workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    public static IWebElement Text(AndroidDriver driver, string value) =>
        AndroidElementWait.UntilText(driver, value);

    public static void TapCenter(AndroidDriver driver, IWebElement element) =>
        driver.ExecuteScript(
            "mobile: clickGesture",
            new Dictionary<string, object>
            {
                ["x"] = element.Location.X + (element.Size.Width / 2),
                ["y"] = element.Location.Y + (element.Size.Height / 2),
            });

    public static void NavigateSection(
        AndroidDriver driver,
        string sectionText,
        string destinationText)
    {
        var nextTapUtc = DateTime.MinValue;
        Wait(driver).Until(current =>
        {
            if (current.FindElements(By.XPath($"//*[@text={XPathLiteral(destinationText)}]"))
                .Any(item => item.Displayed))
                return true;
            if (DateTime.UtcNow < nextTapUtc)
                return false;

            var section = current.FindElements(By.XPath($"//*[@text={XPathLiteral(sectionText)}]"))
                .FirstOrDefault(item => item.Displayed);
            if (section is not null)
                TapCenter(driver, section);
            nextTapUtc = DateTime.UtcNow.AddSeconds(1);
            return false;
        });
    }

    public static IWebElement[] Visible(AndroidDriver driver, string className) =>
        driver.FindElements(By.XPath($"//*[@class={XPathLiteral(className)}]"))
            .Where(item => item.Displayed)
            .ToArray();

    public static IWebElement[] VisibleEditTexts(AndroidDriver driver) =>
        Wait(driver).Until(current =>
        {
            var entries = current.FindElements(By.XPath("//*[@class='android.widget.EditText']"))
                .Where(item => item.Displayed)
                .ToArray();
            return entries.Length > 0 ? entries : null;
        })!;

    public static IWebElement ScrollToText(AndroidDriver driver, string value)
    {
        try
        {
            return driver.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().className(\"androidx.recyclerview.widget.RecyclerView\").scrollable(true)).scrollIntoView(new UiSelector().text(\"{value}\"))"));
        }
        catch (NoSuchElementException)
        {
            var size = driver.Manage().Window.Size;
            return Wait(driver).Until(current =>
            {
                var match = current.FindElements(By.XPath($"//*[@text={XPathLiteral(value)}]"))
                    .FirstOrDefault(item => item.Displayed);
                if (match is not null)
                    return match;

                var scrollable = current
                    .FindElements(By.XPath("//*[@scrollable='true']"))
                    .Where(item => item.Displayed && item.Size.Width > 1 && item.Size.Height > 1)
                    .OrderBy(item => item.Size.Height)
                    .FirstOrDefault();
                var bounds = scrollable is null
                    ? new System.Drawing.Rectangle(20, 180, size.Width - 40, size.Height - 300)
                    : new System.Drawing.Rectangle(
                        scrollable.Location.X,
                        scrollable.Location.Y,
                        scrollable.Size.Width,
                        scrollable.Size.Height);
                driver.ExecuteScript(
                    "mobile: scrollGesture",
                    new Dictionary<string, object>
                    {
                        ["left"] = bounds.Left,
                        ["top"] = bounds.Top,
                        ["width"] = bounds.Width,
                        ["height"] = bounds.Height,
                        ["direction"] = "down",
                        ["percent"] = 0.8,
                    });
                return null;
            })!;
        }
    }

    public static void ScrollDown(AndroidDriver driver)
    {
        var size = driver.Manage().Window.Size;
        driver.ExecuteScript(
            "mobile: scrollGesture",
            new Dictionary<string, object>
            {
                ["left"] = 20,
                ["top"] = 180,
                ["width"] = size.Width - 40,
                ["height"] = size.Height - 300,
                ["direction"] = "down",
                ["percent"] = 0.8,
            });
    }

    public static void ReplaceText(IWebElement element, string value)
    {
        element.Clear();
        element.SendKeys(value);
    }

    public static IWebElement NearestByText(
        AndroidDriver driver,
        string candidateText,
        IWebElement reference)
    {
        var matches = driver.FindElements(By.XPath($"//*[@text={XPathLiteral(candidateText)}]"))
            .Where(item => item.Displayed)
            .ToArray();
        Assert.NotEmpty(matches);
        return matches.MinBy(item => Math.Abs(CenterY(item) - CenterY(reference)))!;
    }

    public static IWebElement NearestVisibleCheckbox(AndroidDriver driver, IWebElement reference)
    {
        return Wait(driver).Until(current =>
        {
            var checkboxes = current
                .FindElements(By.XPath("//*[@class='android.widget.CheckBox']"))
                .Where(item => item.Displayed)
                .ToArray();
            return checkboxes.Length == 0
                ? null
                : checkboxes.MinBy(item => Math.Abs(CenterY(item) - CenterY(reference)));
        })!;
    }

    public static void OpenShellFlyout(AndroidDriver driver)
    {
        var nextActionUtc = DateTime.MinValue;
        Wait(driver).Until(current =>
        {
            if (current.FindElements(By.XPath("//*[@text='Manage Inactive Teams']"))
                .Any(item => item.Displayed))
                return true;
            if (DateTime.UtcNow < nextActionUtc) return false;

            var drawer = current.FindElements(MobileBy.AccessibilityId("Open navigation drawer"))
                .FirstOrDefault(item => item.Displayed);
            if (drawer is not null)
            {
                drawer.Click();
                nextActionUtc = DateTime.UtcNow.AddSeconds(1);
                return false;
            }

            current.FindElements(MobileBy.AccessibilityId("Navigate up"))
                .FirstOrDefault(item => item.Displayed)?.Click();
            nextActionUtc = DateTime.UtcNow.AddSeconds(1);
            return false;
        });
    }

    public static void NavigateUp(AndroidDriver driver) =>
        Wait(driver).Until(current => current.FindElements(MobileBy.AccessibilityId("Navigate up"))
            .FirstOrDefault(item => item.Displayed) ?? throw new NoSuchElementException()).Click();

    public static void AssertTextAbsent(AndroidDriver driver, string value) =>
        Assert.DoesNotContain(
            driver.FindElements(By.XPath($"//*[@text={XPathLiteral(value)}]")),
            item => item.Displayed);

    public static void AssertSingleVisibleText(AndroidDriver driver, string value) =>
        Assert.Single(driver.FindElements(By.XPath($"//*[@text={XPathLiteral(value)}]"))
            .Where(item => item.Displayed));

    public static WebDriverWait Wait(AndroidDriver driver)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
        {
            PollingInterval = TimeSpan.FromMilliseconds(250),
        };
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait;
    }

    private static int CenterY(IWebElement element) =>
        element.Location.Y + (element.Size.Height / 2);

    private static string XPathLiteral(string value) =>
        !value.Contains('\'')
            ? $"'{value}'"
            : $"concat('{value.Replace("'", "',\"'\",'")}')";
}
