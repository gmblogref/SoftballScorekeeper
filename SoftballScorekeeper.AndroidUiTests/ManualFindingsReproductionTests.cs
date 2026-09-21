using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ManualFindingsReproductionTests(AndroidAppiumFixture fixture)
{
    private const string FirstPlayer = "First Reproduction";
    private const string SecondPlayer = "Second Reproduction";
    private const string ThirdPlayer = "Third Reproduction";

    [AndroidUiFact]
    public void Press_hold_drag_reorders_lineup_and_persists()
    {
        Run(
            nameof(Press_hold_drag_reorders_lineup_and_persists),
            driver =>
            {
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
                AndroidElementWait.UntilText(driver, "Edit Lineup").Click();
                AndroidElementWait.UntilText(driver, "Current Lineup");

                AssertLineupOrder(
                    driver,
                    $"1. {FirstPlayer}",
                    $"2. {SecondPlayer}",
                    $"3. {ThirdPlayer}");

                var source = AndroidElementWait.UntilText(driver, $"1. {FirstPlayer}");
                var target = AndroidElementWait.UntilText(driver, $"3. {ThirdPlayer}");
                PerformPressHoldDrag(driver, source, target);

                AssertLineupOrder(
                    driver,
                    $"1. {SecondPlayer}",
                    $"2. {ThirdPlayer}",
                    $"3. {FirstPlayer}");

                AndroidElementWait.UntilText(driver, "Done").Click();
                AndroidElementWait.UntilAutomationId(driver, "ScoreGameButton");
                AndroidElementWait.UntilText(driver, "Edit Lineup").Click();
                AssertLineupOrder(
                    driver,
                    $"1. {SecondPlayer}",
                    $"2. {ThirdPlayer}",
                    $"3. {FirstPlayer}");
            });
    }

    [AndroidUiFact]
    public void Start_scoring_records_single_and_returns_to_resume_game()
    {
        Run(
            nameof(Start_scoring_records_single_and_returns_to_resume_game),
            driver =>
            {
                var scoreGame = AndroidElementWait.UntilAutomationId(
                    driver, "ScoreGameButton");
                Assert.True(scoreGame.Enabled);
                scoreGame.Click();

                AndroidElementWait.UntilText(driver, "Start Scoring");
                Assert.Equal(
                    "true",
                    AndroidElementWait
                        .UntilAutomationId(driver, "AwayTeamSideOption")
                        .GetAttribute("checked"));
                AndroidElementWait.UntilAutomationId(
                    driver, "ConfirmGameRulesButton").Click();

                AndroidElementWait.UntilAutomationIdText(
                    driver, "CurrentBatter", FirstPlayer);
                AndroidElementWait.UntilAutomationId(driver, "ScoringMenuButton");

                AssertLandscapeScoringLayout(driver);
                fixture.SetOrientation(ScreenOrientation.Portrait);
                AssertPortraitScoringLayout(driver);
                WritePortraitScoringGeometry(driver);
                fixture.SetOrientation(ScreenOrientation.Landscape);
                AssertLandscapeScoringLayout(driver);

                TapCenter(
                    driver,
                    AndroidElementWait.UntilAutomationId(driver, "CurrentBatter"));
                AndroidElementWait.UntilText(driver, "Single").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "FirstBaseRunnerButton", FirstPlayer);
                AndroidElementWait.UntilAutomationId(
                    driver, "NextBatterButton").Click();
                AndroidElementWait.UntilAutomationIdText(
                    driver, "CurrentBatter", SecondPlayer);

                AndroidElementWait.UntilAutomationId(
                    driver, "ScoringMenuButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ExitGameButton").Click();
                AndroidElementWait.UntilAutomationId(driver, "ResumeGameButton");
            });
    }

    private void Run(string testName, Action<AndroidDriver> scenario)
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.ManualFindingsReproductionTestSeedRequestFileName);
        try
        {
            scenario(fixture.Driver);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
            throw new Xunit.Sdk.XunitException(
                $"Android manual-finding reproduction failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}");
        }
    }

    private static void PerformPressHoldDrag(
        AndroidDriver driver,
        IWebElement source,
        IWebElement target)
    {
        var finger = new PointerInputDevice(PointerKind.Touch, "lineup-reorder-finger");
        var actions = new ActionSequence(finger, 0);
        actions.AddAction(
            finger.CreatePointerMove(
                CoordinateOrigin.Viewport,
                CenterX(source),
                CenterY(source),
                TimeSpan.Zero));
        actions.AddAction(finger.CreatePointerDown(MouseButton.Left));
        actions.AddAction(
            finger.CreatePointerMove(
                CoordinateOrigin.Viewport,
                CenterX(source),
                CenterY(source),
                TimeSpan.FromMilliseconds(900)));
        actions.AddAction(
            finger.CreatePointerMove(
                CoordinateOrigin.Viewport,
                CenterX(target),
                CenterY(target),
                TimeSpan.FromMilliseconds(1400)));
        actions.AddAction(finger.CreatePointerUp(MouseButton.Left));

        driver.PerformActions([actions]);
        driver.ResetInputState();
    }

    private static void AssertLineupOrder(AndroidDriver driver, params string[] expected)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        wait.Until(current =>
        {
            var rows = expected
                .Select(text => current.FindElements(
                        By.XPath($"//*[@text={XPathLiteral(text)}]"))
                    .FirstOrDefault(element => element.Displayed))
                .ToArray();
            return rows.All(row => row is not null)
                && rows.Zip(rows.Skip(1), (first, second) =>
                        first!.Location.Y < second!.Location.Y)
                    .All(isOrdered => isOrdered);
        });
    }

    private static int CenterX(IWebElement element) =>
        element.Location.X + (element.Size.Width / 2);

    private static int CenterY(IWebElement element) =>
        element.Location.Y + (element.Size.Height / 2);

    private static void AssertLandscapeScoringLayout(AndroidDriver driver)
    {
        var wait = CreateLayoutWait(driver);
        Assert.True(wait.Until(current => current.Manage().Window.Size.Width > current.Manage().Window.Size.Height));

        var upcoming = AndroidElementWait.UntilAutomationId(driver, "UpcomingBattersHeading");
        var currentBatter = AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
        var outs = AndroidElementWait.UntilAutomationId(driver, "OutsThisInningHeading");

        Assert.True(CenterX(upcoming) < CenterX(currentBatter));
        Assert.True(CenterX(currentBatter) < CenterX(outs));
        AssertControlsFitViewport(
            driver,
            "VisitorScoreValue",
            "HomeScoreValue",
            "SecondBaseRunnerButton",
            "ThirdBaseRunnerButton",
            "FirstBaseRunnerButton",
            "NextBatterButton",
            "CurrentBatterButton",
            "UpcomingBattersHeading",
            "OutsThisInningHeading",
            "UndoScoringButton",
            "RedoScoringButton");
    }

    private static void AssertPortraitScoringLayout(AndroidDriver driver)
    {
        var wait = CreateLayoutWait(driver);
        Assert.True(wait.Until(current => current.Manage().Window.Size.Height > current.Manage().Window.Size.Width));

        var currentBatter = AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
        var nextBatter = AndroidElementWait.UntilAutomationId(driver, "NextBatterButton");
        var upcoming = AndroidElementWait.UntilAutomationId(driver, "UpcomingBattersHeading");
        var outs = AndroidElementWait.UntilAutomationId(driver, "OutsThisInningHeading");

        Assert.InRange(Math.Abs(CenterX(currentBatter) - CenterX(nextBatter)), 0, 4);
        Assert.True(CenterY(currentBatter) < CenterY(upcoming));
        Assert.True(CenterY(currentBatter) < CenterY(outs));
        Assert.True(CenterX(upcoming) < CenterX(outs));
        AssertControlsFitViewport(
            driver,
            "VisitorScoreValue",
            "HomeScoreValue",
            "SecondBaseRunnerButton",
            "ThirdBaseRunnerButton",
            "FirstBaseRunnerButton",
            "NextBatterButton",
            "CurrentBatterButton",
            "UpcomingBattersHeading",
            "OutsThisInningHeading",
            "UndoScoringButton",
            "RedoScoringButton");
    }

    private static void AssertControlsFitViewport(AndroidDriver driver, params string[] automationIds)
    {
        var viewport = driver.Manage().Window.Size;
        foreach (var automationId in automationIds)
        {
            var element = AndroidElementWait.UntilAutomationId(driver, automationId);
            Assert.True(element.Displayed, $"{automationId} was not displayed.");
            Assert.InRange(element.Location.X, 0, viewport.Width - 1);
            Assert.InRange(element.Location.Y, 0, viewport.Height - 1);
            Assert.True(
                element.Location.X + element.Size.Width <= viewport.Width,
                $"{automationId} extended beyond the viewport width.");
            Assert.True(
                element.Location.Y + element.Size.Height <= viewport.Height,
                $"{automationId} extended beyond the viewport height.");
        }
    }

    private static void WritePortraitScoringGeometry(AndroidDriver driver)
    {
        var nextBatter = AndroidElementWait.UntilAutomationId(driver, "NextBatterButton");
        var currentBatter = AndroidElementWait.UntilAutomationId(driver, "CurrentBatterButton");
        var upcoming = AndroidElementWait.UntilAutomationId(driver, "UpcomingBattersHeading");
        var outs = AndroidElementWait.UntilAutomationId(driver, "OutsThisInningHeading");
        var viewport = driver.Manage().Window.Size;
        var informationTop = Math.Min(upcoming.Location.Y, outs.Location.Y);
        var gapAbove = currentBatter.Location.Y
            - (nextBatter.Location.Y + nextBatter.Size.Height);
        var gapBelow = informationTop
            - (currentBatter.Location.Y + currentBatter.Size.Height);
        var availableCenter = (
            nextBatter.Location.Y + nextBatter.Size.Height + informationTop) / 2d;
        var currentCenter = currentBatter.Location.Y + (currentBatter.Size.Height / 2d);
        var pageCenterX = viewport.Width / 2d;
        var nextCenterX = CenterX(nextBatter);
        var currentCenterX = CenterX(currentBatter);

        var lines = new[]
        {
            $"Viewport: X=0 Y=0 Width={viewport.Width} Height={viewport.Height} Bottom={viewport.Height} CenterX={pageCenterX:F1} CenterY={viewport.Height / 2d:F1}",
            FormatBounds("Next Batter", nextBatter),
            FormatBounds("Current Batter", currentBatter),
            FormatBounds("Upcoming heading", upcoming),
            FormatBounds("Outs heading", outs),
            $"Gap Next-to-Current={gapAbove}",
            $"Gap Current-to-InfoHeading={gapBelow}",
            $"AvailableRegionCenterY={availableCenter:F1}",
            $"CurrentBatterCenterY={currentCenter:F1}",
            $"CenterError={currentCenter - availableCenter:F1}",
            $"NextBatterCenterX={nextCenterX:F1}",
            $"CurrentBatterCenterX={currentCenterX:F1}",
            $"HorizontalCenterError={currentCenterX - nextCenterX:F1}",
        };
        var resultsDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllLines(
            Path.Combine(resultsDirectory, "portrait-scoring-geometry.txt"),
            lines);
    }

    private static string FormatBounds(string name, IWebElement element) =>
        $"{name}: X={element.Location.X} Y={element.Location.Y} "
        + $"Width={element.Size.Width} Height={element.Size.Height} "
        + $"Bottom={element.Location.Y + element.Size.Height} "
        + $"CenterX={element.Location.X + (element.Size.Width / 2d):F1} "
        + $"CenterY={element.Location.Y + (element.Size.Height / 2d):F1}";

    private static WebDriverWait CreateLayoutWait(AndroidDriver driver)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait;
    }

    private static void TapCenter(AndroidDriver driver, IWebElement element) =>
        driver.ExecuteScript(
            "mobile: clickGesture",
            new Dictionary<string, object>
            {
                ["x"] = CenterX(element),
                ["y"] = CenterY(element),
            });

    private static string XPathLiteral(string value) =>
        !value.Contains('\'')
            ? $"'{value}'"
            : $"concat('{value.Replace("'", "',\"'\",'")}')";
}
