using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class ReleaseUpgradeAcceptanceTests(AndroidAppiumFixture fixture)
{
    private const string SourceApkSha256 =
        "638220D9C91F15D8565A06D694D35390B5FE0EDF90DA855CB6FD21057CC3A361";
    private const string ReleaseCertificateSha256 =
        "D55EFDC1E25B341F4408BC3014ECFF31D94255113EDD6F9B5F3CE98E833E4E05";
    private static readonly string RunId = DateTime.UtcNow.ToString("HHmmss");
    private static readonly string TeamName = $"RELEASE 19L1 TEAM {RunId}";
    private static readonly string PlayerFirstName = $"Release{RunId}";
    private const string PlayerLastName = "Verifier";
    private static readonly string PlayerName = $"{PlayerFirstName} {PlayerLastName}";

    [AndroidUiFact]
    public void Signed_release_upgrade_preserves_data_and_licenses_work_offline()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("SOFTBALL_ANDROID_RELEASE_ACCEPTANCE"));
        var sourceApk = RequiredApk("SOFTBALL_RELEASE_SOURCE_APK");
        var destinationApk = RequiredApk("SOFTBALL_RELEASE_DESTINATION_APK");
        var sourceSha256 = RequiredSha256("SOFTBALL_RELEASE_SOURCE_SHA256");
        var destinationSha256 = RequiredSha256("SOFTBALL_RELEASE_DESTINATION_SHA256");
        var certificateSha256 = RequiredSha256("SOFTBALL_RELEASE_CERTIFICATE_SHA256");
        Assert.Equal(SourceApkSha256, sourceSha256);
        Assert.Equal(ReleaseCertificateSha256, certificateSha256);
        Assert.NotEqual(sourceSha256, destinationSha256);
        AssertApkHash(sourceApk, sourceSha256);
        AssertApkHash(destinationApk, destinationSha256);

        var installedPackages = fixture.ExecuteAdb(
            "shell",
            "pm",
            "list",
            "packages",
            AndroidAutomationConstants.AppPackage
        );
        if (installedPackages.Contains(AndroidAutomationConstants.AppPackage, StringComparison.Ordinal))
        {
            AssertVersion("5", "1.0.1");
            Install(sourceApk, replace: true);
        }
        else
        {
            Install(sourceApk);
        }
        AssertVersion("5", "1.0.1");

        fixture.SetOrientation(ScreenOrientation.Landscape);
        fixture.RelaunchApplicationWithoutReset();
        var driver = fixture.Driver;
        var networkDisabled = false;
        try
        {
            EnterProductTestMode(driver);
            CreateTeam(driver);
            CreatePlayer(driver);
            AssertPersistedData(driver);

            fixture.ExecuteAdb("logcat", "-c");
            Install(destinationApk, replace: true);
            AssertVersion("6", "2.0.0");
            fixture.RelaunchApplicationWithoutReset();
            driver = fixture.Driver;
            AssertPersistedData(driver);

            OpenLicenses(driver);
            AssertLicenseContent(driver, "PolyForm Noncommercial License 1.0.0");
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "ApplicationRequiredNotice",
                "Required Notice: Copyright 2026 gmblogref. Softball Scorekeeper."
            );
            AssertTestModeIndicator(driver);

            SelectLicense(driver, "Consolidated Third-Party Notices");
            AssertLicenseContent(driver, "Third-Party Notices");
            AssertDocumentScrolls(driver);

            fixture.ExecuteAdb("shell", "cmd", "connectivity", "airplane-mode", "enable");
            networkDisabled = true;
            Assert.Contains("enabled", fixture.ExecuteAdb("shell", "cmd", "connectivity", "airplane-mode"), StringComparison.OrdinalIgnoreCase);
            SelectLicense(driver, "Microsoft.Android Runtime 36.1.118 — Third-Party Notices");
            AssertLicenseContent(driver, "xamarin-android");

            fixture.SetOrientation(ScreenOrientation.Portrait);
            Assert.True(AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentPicker").Displayed);
            Assert.True(AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentContent").Displayed);
            AssertTestModeIndicator(driver);
            fixture.SetOrientation(ScreenOrientation.Landscape);
            Assert.True(AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentPicker").Displayed);
            AssertTestModeIndicator(driver);

            driver.Navigate().Back();
            NavigateToPersistedTeam(driver);
            AssertTestModeIndicator(driver);

            var logcat = fixture.ExecuteAdb("logcat", "-d", "-t", "1000");
            Assert.DoesNotContain("FATAL EXCEPTION: main", logcat, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SQLiteException", logcat, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Signed_release_upgrade_preserves_data_and_licenses_work_offline),
                exception
            );
            throw new Xunit.Sdk.XunitException(
                $"Signed release acceptance failed. Diagnostics: {artifacts}{Environment.NewLine}{exception}"
            );
        }
        finally
        {
            if (networkDisabled)
                fixture.ExecuteAdb("shell", "cmd", "connectivity", "airplane-mode", "disable");
            fixture.SetOrientation(ScreenOrientation.Landscape);
        }
    }

    private void Install(string apkPath, bool replace = false)
    {
        var arguments = replace ? new[] { "install", "-r", apkPath } : new[] { "install", apkPath };
        var output = fixture.ExecuteAdb(arguments);
        Assert.Contains("Success", output, StringComparison.Ordinal);
    }

    private void AssertVersion(string code, string name)
    {
        var package = fixture.ExecuteAdb("shell", "dumpsys", "package", AndroidAutomationConstants.AppPackage);
        Assert.Contains($"versionCode={code}", package, StringComparison.Ordinal);
        Assert.Contains($"versionName={name}", package, StringComparison.Ordinal);
    }

    private static string RequiredApk(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(File.Exists(path), $"Release APK not found: {path}");
        return path!;
    }

    private static string RequiredSha256(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Matches("^[0-9A-Fa-f]{64}$", value!);
        return value!.ToUpperInvariant();
    }

    private static void AssertApkHash(string path, string expectedSha256) =>
        Assert.Equal(
            expectedSha256,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
        );

    private static void EnterProductTestMode(AndroidDriver driver)
    {
        AndroidElementWait.UntilText(driver, "Create Team");
        PhaseAUiWorkflow.OpenShellFlyout(driver);

        var exitTestMode = driver.FindElements(By.XPath("//*[@text='Exit Test Mode']"))
            .FirstOrDefault(element => element.Displayed);
        if (exitTestMode is not null)
        {
            AndroidElementWait.UntilAutomationId(driver, "TeamsFlyoutMenuItem").Click();
            AndroidElementWait.UntilText(driver, "Create Team");
            AssertTestModeIndicator(driver);
            return;
        }

        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Enter Test Mode?");
        AndroidElementWait.UntilText(driver, "Enter Test Mode").Click();
        AndroidElementWait.UntilText(driver, "Create Team");
        AssertTestModeIndicator(driver);
    }

    private static void CreateTeam(AndroidDriver driver)
    {
        AndroidElementWait.UntilText(driver, "Create Team").Click();
        var entries = VisibleEntries(driver, 2);
        entries[0].SendKeys(TeamName);
        entries[1].Clear();
        entries[1].SendKeys("2026");
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilTextAbsent(driver, "Save");
        PhaseAUiWorkflow.ScrollToText(driver, TeamName);
    }

    private static void CreatePlayer(AndroidDriver driver)
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
        AndroidElementWait.UntilText(driver, "Add Player").Click();
        var entries = VisibleEntries(driver, 2);
        entries[0].SendKeys(PlayerFirstName);
        entries[1].SendKeys(PlayerLastName);
        AndroidElementWait.UntilText(driver, "Save").Click();
        AndroidElementWait.UntilTextAbsent(driver, "Save");
        PhaseAUiWorkflow.ScrollToText(driver, PlayerName);
        AssertTestModeIndicator(driver);
    }

    private static void AssertPersistedData(AndroidDriver driver)
    {
        NavigateToPersistedTeam(driver);
        PhaseAUiWorkflow.NavigateSection(driver, "Players", "Add Player");
        PhaseAUiWorkflow.ScrollToText(driver, PlayerName);
        AssertTestModeIndicator(driver);
    }

    private static void NavigateToPersistedTeam(AndroidDriver driver)
    {
        PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
        PhaseAUiWorkflow.ScrollToText(driver, TeamName);
    }

    private static void OpenLicenses(AndroidDriver driver)
    {
        PhaseAUiWorkflow.OpenShellFlyout(driver);
        AndroidElementWait.UntilAutomationId(driver, "LicensesMenuItem").Click();
        AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentPicker");
    }

    private static void SelectLicense(AndroidDriver driver, string title)
    {
        AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentPicker").Click();
        PhaseAUiWorkflow.ScrollToText(driver, title).Click();
    }

    private static void AssertLicenseContent(AndroidDriver driver, string text) =>
        Wait(driver).Until(current =>
            current.FindElements(By.Id(ResourceId("LicenseDocumentContent")))
                .Any(element => element.Displayed && element.Text.Contains(text, StringComparison.Ordinal))
        );

    private static void AssertDocumentScrolls(AndroidDriver driver)
    {
        var scroll = AndroidElementWait.UntilAutomationId(driver, "LicenseDocumentScrollView");
        var canScrollMore = driver.ExecuteScript(
            "mobile: scrollGesture",
            new Dictionary<string, object>
            {
                ["left"] = scroll.Location.X,
                ["top"] = scroll.Location.Y,
                ["width"] = scroll.Size.Width,
                ["height"] = scroll.Size.Height,
                ["direction"] = "down",
                ["percent"] = 0.8,
            }
        );
        Assert.NotNull(canScrollMore);
    }

    private static IWebElement[] VisibleEntries(AndroidDriver driver, int count) =>
        Wait(driver).Until(current =>
        {
            var entries = current.FindElements(By.XPath("//android.widget.EditText"))
                .Where(element => element.Displayed)
                .ToArray();
            return entries.Length >= count ? entries : null;
        })!;

    private static void AssertTestModeIndicator(AndroidDriver driver) =>
        AndroidElementWait.UntilAutomationIdText(driver, "TestModeIndicator", "TEST MODE");

    private static WebDriverWait Wait(AndroidDriver driver) =>
        new(driver, TimeSpan.FromSeconds(20)) { PollingInterval = TimeSpan.FromMilliseconds(250) };

    private static string ResourceId(string automationId) =>
        $"{AndroidAutomationConstants.AppPackage}:id/{automationId}";
}
