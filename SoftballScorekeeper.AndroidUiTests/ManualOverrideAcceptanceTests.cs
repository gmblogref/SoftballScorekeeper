using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ManualOverrideAcceptanceTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Frozen_statistics_grid_keeps_headers_players_and_total_aligned_in_both_orientations()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.FinalStory14AcceptanceTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AssertFrozenGridInOrientation(driver, ScreenOrientation.Landscape);
            AssertFrozenGridInOrientation(driver, ScreenOrientation.Portrait);
            fixture.SetOrientation(ScreenOrientation.Landscape);

            AndroidElementWait.UntilAutomationId(driver, "CompletedStatsSortPlayer").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedStatsSortPlayer", "Player ▲");
            AndroidElementWait.UntilAutomationId(driver, "CompletedStatsSortPlayer").Click();
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedStatsSortPlayer", "Player ▼");
            var descendingFirst = ScrollVerticallyIntoView(
                driver, "CompletedBattingRowPosition10");
            var descendingValue = ScrollVerticallyIntoView(
                driver, "CompletedBattingPlateAppearancesPosition10");
            AssertAlignedVertically(descendingFirst, descendingValue);

            NavigateToTeamWorkspace(driver);
            AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
            AndroidElementWait.UntilAutomationIdText(
                driver, "TeamStatsActiveFilter", "All Games");
            AssertColumnAligned(driver,
                "CompletedStatsSortPlateAppearances",
                "CompletedBattingPlateAppearancesPosition1");

            NavigateToTeamWorkspace(driver);
            AndroidTeamWorkspaceMenu.Select(driver, "Team Year Summary");
            AndroidElementWait.UntilAutomationId(driver, "TeamYearSummaryHeader");
            ScrollAnyVerticalContainerIntoView(
                driver, "CompletedBattingPlateAppearancesPosition1");
            AssertColumnAligned(driver,
                "CompletedStatsSortPlateAppearances",
                "CompletedBattingPlateAppearancesPosition1");
            Assert.True(ScrollVerticallyIntoView(
                driver, "CompletedBattingTeamTotal").Displayed);
            Assert.True(AndroidElementWait.UntilAutomationId(
                driver, "CompletedStatsSortPlayer").Displayed);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Frozen_statistics_grid_keeps_headers_players_and_total_aligned_in_both_orientations),
                exception);
        }
    }

    [AndroidUiFact]
    public void Low_man_recalculation_accepts_manual_override_player_without_canonical_pa()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.FinalStory14AcceptanceTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            NavigateToTeamWorkspace(driver);
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            AssertBeerTotal(driver, "BeerTotalsLowMan-1", "1");
            AssertBeerTotal(driver, "BeerTotalsLowMan-2", "0");
            AndroidElementWait.UntilText(driver, "Low Man: Alex Automation");

            NavigateToTeamWorkspace(driver);
            OpenScheduledGame(driver, 1);
            BeginEdit(driver);
            ScrollStatisticIntoView(driver, "CompletedBattingSinglesPosition2");
            var singles = AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingSinglesPosition2");
            ReplaceText(singles, "1");
            Assert.Contains("1B, unsaved change", singles.Text, StringComparison.Ordinal);
            ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition2");
            var outs = AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingOutsPosition2");
            ReplaceText(outs, "3");
            Assert.Contains("OUT, unsaved change", outs.Text, StringComparison.Ordinal);
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingPlateAppearancesPosition2", "0");
            Assert.NotEqual("android.widget.EditText",
                AndroidElementWait.UntilAutomationId(
                    driver, "CompletedBattingPlateAppearancesPosition2")
                    .GetAttribute("class"));
            HideKeyboard(driver);

            fixture.SetOrientation(ScreenOrientation.Portrait);
            ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition2");
            AssertWithinViewport(driver, AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingOutsPosition2"));
            fixture.SetOrientation(ScreenOrientation.Landscape);
            AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton").Click();

            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingPlateAppearancesPosition2", "4");
            ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition2");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingOutsPosition2", "3");
            ScrollStatisticIntoView(driver, "CompletedBattingOnBasePercentagePosition2");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingOnBasePercentagePosition2", ".250");
            AssertCanonicalGamePlaysExcludeBlake(driver);

            NavigateToTeamWorkspace(driver);
            OpenScheduledGame(driver, 1);
            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingPlateAppearancesPosition2", "4");

            NavigateToTeamWorkspace(driver);
            AndroidTeamWorkspaceMenu.Select(driver, "Beer Totals");
            AssertBeerTotal(driver, "BeerTotalsLowMan-1", "1");
            AssertBeerTotal(driver, "BeerTotalsLowMan-2", "0");
            AndroidElementWait.UntilText(driver, "Low Man: Alex Automation");
            AndroidElementWait.UntilAutomationId(driver, "CalculateLowManButton").Click();
            AssertBeerTotal(driver, "BeerTotalsLowMan-1", "1");
            AssertBeerTotal(driver, "BeerTotalsLowMan-2", "1");
            AndroidElementWait.UntilText(
                driver, "Low Man: Alex Automation, Blake Automation");
            Assert.DoesNotContain(
                "Low Man could not be calculated", driver.PageSource,
                StringComparison.Ordinal);

            NavigateToTeamWorkspace(driver);
            OpenScheduledGame(driver, 1);
            AssertCanonicalGamePlaysExcludeBlake(driver);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Low_man_recalculation_accepts_manual_override_player_without_canonical_pa),
                exception);
        }
    }

    [AndroidUiFact]
    public void Completed_game_manual_override_saves_persists_integrates_and_reverts()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.DynamicManualOverrideTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AssertCanonicalCompletedGame(driver);
            AssertCancelAndValidation(driver);
            SaveMissingSingleAndScoreOverrides(driver);
            AssertMissingSingleCompletedGame(driver);
            ReclassifyOutAsSingle(driver);
            AssertReclassifiedCompletedGame(driver);

            NavigateToTeamWorkspace(driver);
            AssertScheduleResult(driver, "W", "2–0");
            OpenScheduledGame(driver, 1);
            AssertReclassifiedCompletedGame(driver);
            AssertCanonicalGamePlays(driver);

            NavigateToTeamWorkspace(driver);
            AndroidTeamWorkspaceMenu.Select(driver, "Team Stats");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingPlateAppearancesPosition1", "4");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingHitsPosition1", "2");
            ScrollStatisticIntoView(driver,
                "CompletedBattingOnBasePercentagePosition1");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingOnBasePercentagePosition1", ".500");

            NavigateToTeamWorkspace(driver);
            OpenScheduledGame(driver, 2);
            AndroidElementWait.UntilText(driver, "Set Lineup").Click();
            AndroidElementWait.UntilText(driver, "Lineup Manager");
            AndroidElementWait.UntilText(driver, "Add Players").Click();
            AndroidElementWait.UntilText(driver, "Alex Automation");
            AndroidElementWait.UntilText(driver, "OBP: .500");

            NavigateToTeamWorkspace(driver);
            OpenScheduledGame(driver, 1);
            AssertCompleteRevert(driver);
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Completed_game_manual_override_saves_persists_integrates_and_reverts),
                exception);
        }
    }

    [AndroidUiFact]
    public void Finalized_team_year_keeps_completed_history_read_only()
    {
        fixture.ResetApplication(
            AndroidAutomationConstants.FinalizedManualOverrideTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
            AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "2");
            AndroidElementWait.UntilAutomationIdText(
                driver, "CompletedBattingHitsPosition1", "4");
            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
            AssertNotDisplayed(driver, "BeginManualOverrideButton");
            AssertNotDisplayed(driver, "RevertManualOverridesButton");

            AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
            AndroidElementWait.UntilText(driver, "Alex Automation — Single");
            AndroidElementWait.UntilText(
                driver, "SCORE — Automation Team: 0 | Automation Opponent: 0");
            AndroidElementWait.UntilAutomationId(driver, "GameStatsButton").Click();
            ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition1");
            AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingPlateAppearancesPosition1");

            fixture.SetOrientation(ScreenOrientation.Portrait);
            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
            AssertNotDisplayed(driver, "BeginManualOverrideButton");
            AssertNotDisplayed(driver, "RevertManualOverridesButton");
            fixture.SetOrientation(ScreenOrientation.Landscape);
            AndroidElementWait.UntilAutomationId(driver, "CompletedGameStatus");
        }
        catch (Exception exception)
        {
            ThrowWithDiagnostics(
                nameof(Finalized_team_year_keeps_completed_history_read_only), exception);
        }
    }

    private static void AssertCanonicalCompletedGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "0");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedHomeScore", "0");
        AssertLeftStatistics(driver, plateAppearances: "4", atBats: "4", hits: "1",
            singles: "1", outs: "3");
        ScrollStatisticIntoView(driver, "CompletedBattingBattingAveragePosition1");
        AssertRateStatistics(driver, battingAverage: ".250",
            onBasePercentage: ".250", sluggingPercentage: ".250");
        AssertNotDisplayed(driver, "ManualOverrideChip");
    }

    private static void AssertCancelAndValidation(AndroidDriver driver)
    {
        BeginEdit(driver);
        AssertApprovedFieldsEditableAndDerivedFieldsReadOnly(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingSinglesPosition1");
        var singles = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingSinglesPosition1");
        ReplaceText(singles, "2");
        Assert.Contains("1B, unsaved change", singles.Text, StringComparison.Ordinal);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "1");
        HideKeyboard(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "4");
        AndroidElementWait.UntilAutomationId(driver, "CancelManualOverrideButton").Click();
        ScrollStatisticIntoView(driver, "CompletedBattingSinglesPosition1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingSinglesPosition1", "1");
        AssertNotDisplayed(driver, "ManualOverrideChip");

        BeginEdit(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition1");
        var outs = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingOutsPosition1");
        ReplaceText(outs, "2");
        Assert.Contains("OUT, unsaved change", outs.Text, StringComparison.Ordinal);
        ReplaceText(outs, "3");
        Assert.DoesNotContain("unsaved change", outs.Text, StringComparison.Ordinal);
        ReplaceText(outs, "-1");
        HideKeyboard(driver);
        AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton").Click();
        AndroidElementWait.UntilText(driver,
            "Enter a non-negative whole number in every editable statistic and final score.");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "1");
        AssertNotDisplayed(driver, "ManualOverrideChip");
        ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition1");
        outs = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingOutsPosition1");
        outs.Click();
        outs.Clear();
        HideKeyboard(driver);
        AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton").Click();
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "4");
        AssertNotDisplayed(driver, "ManualOverrideChip");
        AndroidElementWait.UntilAutomationId(driver, "CancelManualOverrideButton").Click();
        AssertLeftStatistics(driver, plateAppearances: "4", atBats: "4", hits: "1",
            singles: "1", outs: "3");
    }

    private void SaveMissingSingleAndScoreOverrides(AndroidDriver driver)
    {
        BeginEdit(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingSinglesPosition1");
        var singles = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingSinglesPosition1");
        ReplaceText(singles, "2");
        var visitorScore = AndroidElementWait.UntilAutomationId(
            driver, "ManualOverrideVisitorScore");
        ReplaceText(visitorScore, "2");
        Assert.Contains("1B, unsaved change", singles.Text, StringComparison.Ordinal);
        Assert.Contains("Visitor Final Score, unsaved change", visitorScore.Text,
            StringComparison.Ordinal);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "4");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "0");
        AssertNotDisplayed(driver, "ManualOverrideChip");
        HideKeyboard(driver);

        fixture.SetOrientation(ScreenOrientation.Portrait);
        AssertWithinViewport(driver,
            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideVisitorScore"));
        AssertWithinViewport(driver,
            AndroidElementWait.UntilAutomationId(driver, "ManualOverrideHomeScore"));
        AssertWithinViewport(driver,
            AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton"));
        AssertWithinViewport(driver,
            AndroidElementWait.UntilAutomationId(driver, "CancelManualOverrideButton"));
        fixture.SetOrientation(ScreenOrientation.Landscape);

        AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
    }

    private static void AssertMissingSingleCompletedGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "2");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedHomeScore", "0");
        AssertLeftStatistics(driver, plateAppearances: "5", atBats: "5", hits: "2",
            singles: "2", outs: "3");
        ScrollStatisticIntoView(driver, "CompletedBattingBattingAveragePosition1");
        AssertRateStatistics(driver, battingAverage: ".400",
            onBasePercentage: ".400", sluggingPercentage: ".400");
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
    }

    private static void ReclassifyOutAsSingle(AndroidDriver driver)
    {
        BeginEdit(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition1");
        var outs = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingOutsPosition1");
        ReplaceText(outs, "2");
        Assert.Contains("OUT, unsaved change", outs.Text, StringComparison.Ordinal);
        HideKeyboard(driver);
        ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "5");
        ScrollStatisticIntoView(driver, "CompletedBattingHitsPosition1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "2");
        AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
    }

    private static void AssertReclassifiedCompletedGame(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "2");
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedHomeScore", "0");
        AssertLeftStatistics(driver, plateAppearances: "4", atBats: "4", hits: "2",
            singles: "2", outs: "2");
        ScrollStatisticIntoView(driver, "CompletedBattingBattingAveragePosition1");
        AssertRateStatistics(driver, battingAverage: ".500",
            onBasePercentage: ".500", sluggingPercentage: ".500");
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
    }

    private static void AssertCanonicalGamePlays(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
        AndroidElementWait.UntilText(driver, "Alex Automation — Single");
        AndroidElementWait.UntilText(driver,
            "SCORE — Automation Team: 0 | Automation Opponent: 0");
        AndroidElementWait.UntilAutomationId(driver, "GameStatsButton").Click();
        ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition1");
        AndroidElementWait.UntilAutomationId(driver,
            "CompletedBattingPlateAppearancesPosition1");
    }

    private void AssertFrozenGridInOrientation(
        AndroidDriver driver,
        ScreenOrientation orientation)
    {
        fixture.SetOrientation(orientation);
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
        var playerHeader = AndroidElementWait.UntilAutomationId(
            driver, "CompletedStatsSortPlayer");
        var firstPlayer = ScrollVerticallyIntoView(driver, "CompletedBattingRowPosition1");
        var playerHeaderX = playerHeader.Location.X;
        var firstPlayerX = firstPlayer.Location.X;
        var headerY = playerHeader.Location.Y;

        ScrollStatisticIntoView(driver, "CompletedBattingSluggingPercentagePosition1");
        Assert.Equal(playerHeaderX,
            AndroidElementWait.UntilAutomationId(
                driver, "CompletedStatsSortPlayer").Location.X);
        Assert.Equal(firstPlayerX,
            AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingRowPosition1").Location.X);
        AssertColumnAligned(driver,
            "CompletedStatsSortSluggingPercentage",
            "CompletedBattingSluggingPercentagePosition1");

        var lastPlayer = ScrollVerticallyIntoView(driver, "CompletedBattingRowPosition10");
        ScrollVerticallyIntoView(
            driver, "CompletedBattingSluggingPercentagePosition10");
        lastPlayer = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingRowPosition10");
        var lastValue = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingSluggingPercentagePosition10");
        AssertAlignedVertically(lastPlayer, lastValue);
        Assert.Equal(headerY,
            AndroidElementWait.UntilAutomationId(
                driver, "CompletedStatsSortPlayer").Location.Y);
        AssertColumnAligned(driver,
            "CompletedStatsSortSluggingPercentage",
            "CompletedBattingSluggingPercentagePosition10");

        var totalLabel = ScrollVerticallyIntoView(driver, "CompletedBattingTeamTotal");
        var totalLabelX = totalLabel.Location.X;
        totalLabel = AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingTeamTotal");
        var totalValue = FindDisplayedAutomationElement(
            driver, "CompletedBattingSluggingPercentageTeamTotal");
        AssertAlignedVertically(totalLabel, totalValue);
        Assert.Equal(totalLabelX,
            AndroidElementWait.UntilAutomationId(
                driver, "CompletedBattingTeamTotal").Location.X);
        AssertColumnAligned(driver,
            "CompletedStatsSortSluggingPercentage",
            "CompletedBattingSluggingPercentageTeamTotal");
        Assert.Single(driver.FindElements(By.Id(ResourceId("CompletedBattingTeamTotal"))));
        Assert.True(totalLabel.Location.Y > lastPlayer.Location.Y);

        ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition10");
        AssertColumnAligned(driver,
            "CompletedStatsSortPlateAppearances",
            "CompletedBattingPlateAppearancesTeamTotal");
        var atBatsHeader = AndroidElementWait.UntilAutomationId(
            driver, "CompletedStatsSortAtBats");
        var hitsHeader = AndroidElementWait.UntilAutomationId(
            driver, "CompletedStatsSortHits");
        Assert.True(atBatsHeader.Location.X > AndroidElementWait.UntilAutomationId(
            driver, "CompletedStatsSortPlateAppearances").Location.X);
        Assert.True(hitsHeader.Location.X > atBatsHeader.Location.X);
    }

    private static void AssertCanonicalGamePlaysExcludeBlake(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "GamePlaysButton").Click();
        AndroidElementWait.UntilText(driver, "Alex Automation — Single");
        Assert.DoesNotContain("Blake Automation", driver.PageSource,
            StringComparison.Ordinal);
        AndroidElementWait.UntilAutomationId(driver, "GameStatsButton").Click();
        ScrollStatisticIntoView(driver, "CompletedBattingPlateAppearancesPosition1");
        AndroidElementWait.UntilAutomationId(
            driver, "CompletedBattingPlateAppearancesPosition1");
    }

    private static void AssertBeerTotal(
        AndroidDriver driver,
        string automationId,
        string expected)
    {
        var resourceId = ResourceId(automationId);
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("BeerTotalsScrollView")}\"))"
            + $".setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
        AndroidElementWait.UntilAutomationIdText(driver, automationId, expected);
    }

    private static void AssertColumnAligned(
        AndroidDriver driver,
        string headerAutomationId,
        string valueAutomationId)
    {
        var header = FindDisplayedAutomationElement(driver, headerAutomationId);
        var value = FindDisplayedAutomationElement(driver, valueAutomationId);
        Assert.InRange(Math.Abs(CenterX(header) - CenterX(value)), 0, 3);
    }

    private static IWebElement FindDisplayedAutomationElement(
        AndroidDriver driver,
        string automationId)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        wait.IgnoreExceptionTypes(
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException));
        return wait.Until(current => current.FindElements(By.Id(ResourceId(automationId)))
            .Concat(current.FindElements(MobileBy.AccessibilityId(automationId)))
            .FirstOrDefault(item => item.Displayed)
            ?? throw new NoSuchElementException())!;
    }

    private static void AssertAlignedVertically(IWebElement label, IWebElement value) =>
        Assert.InRange(Math.Abs(CenterY(label) - CenterY(value)), 0, 3);

    private static IWebElement ScrollVerticallyIntoView(
        AndroidDriver driver,
        string automationId)
    {
        var displayed = driver.FindElements(By.Id(ResourceId(automationId)))
            .FirstOrDefault(item => item.Displayed);
        if (displayed is not null)
            return displayed;
        return driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{ResourceId("CompletedBattingStatsVerticalScroll")}\"))"
            + $".setAsVerticalList().scrollIntoView(new UiSelector().resourceId(\"{ResourceId(automationId)}\"))"));
    }

    private static IWebElement ScrollAnyVerticalContainerIntoView(
        AndroidDriver driver,
        string automationId)
    {
        var displayed = driver.FindElements(By.Id(ResourceId(automationId)))
            .FirstOrDefault(item => item.Displayed);
        return displayed ?? driver.FindElement(MobileBy.AndroidUIAutomator(
            "new UiScrollable(new UiSelector().scrollable(true).instance(0))"
            + $".scrollIntoView(new UiSelector().resourceId(\"{ResourceId(automationId)}\"))"));
    }

    private static int CenterX(IWebElement element) =>
        element.Location.X + (element.Size.Width / 2);

    private static int CenterY(IWebElement element) =>
        element.Location.Y + (element.Size.Height / 2);

    private static void AssertCompleteRevert(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideChip");
        AndroidElementWait.UntilAutomationId(driver, "RevertManualOverridesButton").Click();
        AndroidElementWait.UntilText(driver, "Revert Manual Override?");
        Assert.Contains("Historical Game Plays will remain unchanged.", driver.PageSource,
            StringComparison.Ordinal);
        AndroidElementWait.UntilText(driver, "Revert").Click();

        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "0");
        AssertLeftStatistics(driver, plateAppearances: "4", atBats: "4", hits: "1",
            singles: "1", outs: "3");
        AssertNotDisplayed(driver, "ManualOverrideChip");
        AssertNotDisplayed(driver, "RevertManualOverridesButton");
        AssertCanonicalGamePlays(driver);
        AssertNotDisplayed(driver, "ManualOverrideChip");

        NavigateToTeamWorkspace(driver);
        AssertScheduleResult(driver, "T", "0–0");
        OpenScheduledGame(driver, 1);
        AndroidElementWait.UntilAutomationIdText(driver, "CompletedVisitorScore", "0");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", "1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", "4");
        AssertNotDisplayed(driver, "ManualOverrideChip");
    }

    private static void AssertApprovedFieldsEditableAndDerivedFieldsReadOnly(
        AndroidDriver driver)
    {
        foreach (var field in new[]
        {
            "Singles", "Doubles", "Triples", "HomeRuns", "Walks",
            "FielderChoices", "SacrificeFlies", "Outs",
        })
        {
            ScrollStatisticIntoView(driver, $"CompletedBatting{field}Position1");
            var element = FindByAutomationId(driver, $"CompletedBatting{field}Position1");
            Assert.Equal("android.widget.EditText", element.GetAttribute("class"));
            Assert.True(element.Enabled);
        }

        foreach (var field in new[]
        {
            "PlateAppearances", "AtBats", "Hits", "BattingAverage",
            "OnBasePercentage", "SluggingPercentage",
        })
        {
            ScrollStatisticIntoView(driver, $"CompletedBatting{field}Position1");
            var element = FindByAutomationId(driver, $"CompletedBatting{field}Position1");
            Assert.NotEqual("android.widget.EditText", element.GetAttribute("class"));
        }
    }

    private static void AssertLeftStatistics(AndroidDriver driver, string plateAppearances,
        string atBats, string hits, string singles, string outs)
    {
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingPlateAppearancesPosition1", plateAppearances);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingAtBatsPosition1", atBats);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingHitsPosition1", hits);
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingSinglesPosition1", singles);
        ScrollStatisticIntoView(driver, "CompletedBattingOutsPosition1");
        AndroidElementWait.UntilAutomationIdText(
            driver, "CompletedBattingOutsPosition1", outs);
    }

    private static void AssertRateStatistics(AndroidDriver driver, string battingAverage,
        string onBasePercentage, string sluggingPercentage)
    {
        foreach (var (field, expected) in new[]
        {
            ("BattingAverage", battingAverage),
            ("OnBasePercentage", onBasePercentage),
            ("SluggingPercentage", sluggingPercentage),
        })
        {
            var automationId = $"CompletedBatting{field}Position1";
            ScrollStatisticIntoView(driver, automationId);
            AndroidElementWait.UntilAutomationIdText(driver, automationId, expected);
        }
    }

    private static void BeginEdit(AndroidDriver driver)
    {
        AndroidElementWait.UntilAutomationId(driver, "BeginManualOverrideButton").Click();
        AndroidElementWait.UntilAutomationId(driver, "ManualOverrideVisitorScore");
        AndroidElementWait.UntilAutomationId(driver, "SaveManualOverrideButton");
        AndroidElementWait.UntilAutomationId(driver, "CancelManualOverrideButton");
    }

    private static void AssertScheduleResult(AndroidDriver driver, string result, string score)
    {
        AndroidElementWait.UntilAutomationIdText(driver, "GameScheduleResult1", result);
        AndroidElementWait.UntilAutomationIdText(driver, "GameScheduleScore1", score);
    }

    private static void OpenScheduledGame(AndroidDriver driver, int gameId)
    {
        AndroidElementWait.UntilAutomationId(driver, $"GameScheduleStatus{gameId}").Click();
        if (gameId == 1)
            AndroidElementWait.UntilAutomationIdText(driver, "CompletedGameStatus", "FINAL");
        else
            AndroidElementWait.UntilText(driver, "Other Automation Opponent");
    }

    private static void NavigateToTeamWorkspace(AndroidDriver driver)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (driver.FindElements(MobileBy.AccessibilityId("TeamWorkspaceMenuButton"))
                .Any(item => item.Displayed)) return;
            if (!IsDisplayed(driver, "TeamCard-1")
                && IsDisplayed(driver, "TeamYearGroup-2026"))
                AndroidElementWait.UntilAutomationId(
                    driver, "TeamYearGroup-2026").Click();
            if (IsDisplayed(driver, "TeamCard-1"))
            {
                AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();
                AndroidTeamWorkspaceMenu.UntilAvailable(driver);
                return;
            }
            PhaseAUiWorkflow.NavigateUp(driver);
        }
        throw new WebDriverTimeoutException("Could not navigate to Team Workspace.");
    }

    private static IWebElement FindByAutomationId(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        return new WebDriverWait(driver, TimeSpan.FromSeconds(15)).Until(current =>
            current.FindElements(By.Id(resourceId)).FirstOrDefault())!;
    }

    private static void ReplaceText(IWebElement element, string value)
    {
        element.Click();
        element.Clear();
        element.SendKeys(value);
    }

    private static void HideKeyboard(AndroidDriver driver)
    {
        try { driver.HideKeyboard(); }
        catch (WebDriverException) { }
    }

    private static void ScrollStatisticIntoView(AndroidDriver driver, string automationId)
    {
        var containerId = ResourceId("CompletedBattingStats");
        var targetId = ResourceId(automationId);
        driver.FindElement(MobileBy.AndroidUIAutomator(
            $"new UiScrollable(new UiSelector().resourceId(\"{containerId}\"))"
            + $".setAsHorizontalList().scrollIntoView(new UiSelector().resourceId(\"{targetId}\"))"));
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

    private static void AssertNotDisplayed(AndroidDriver driver, string automationId)
    {
        var resourceId = $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
        Assert.DoesNotContain(driver.FindElements(By.Id(resourceId)), item => item.Displayed);
    }

    private static bool IsDisplayed(AndroidDriver driver, string automationId) =>
        driver.FindElements(By.Id(ResourceId(automationId))).Any(item => item.Displayed);

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";

    private void ThrowWithDiagnostics(string testName, Exception exception)
    {
        var artifacts = fixture.CaptureFailureArtifacts(testName, exception);
        throw new Xunit.Sdk.XunitException(
            $"Android Story 14 Manual Override acceptance failed. Diagnostics: {artifacts}"
            + $"{Environment.NewLine}{exception}");
    }
}
