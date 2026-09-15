using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace SoftballScorekeeper.AndroidUiTests;

public sealed class AndroidAppiumFixture : IDisposable
{
    private AndroidDriver? driver;

    public AndroidAppiumFixture()
    {
        Configuration = AndroidUiTestConfiguration.Load();
    }

    public AndroidDriver Driver => driver ??= CreatePreparedDriver(Configuration);
    internal AndroidUiTestConfiguration Configuration { get; }

    public void ResetApplication(
        string seedRequestFileName = AndroidAutomationConstants.TestSeedRequestFileName)
    {
        driver?.Quit();
        driver = null;
        SetOrientation(ScreenOrientation.Landscape);
        driver = CreatePreparedDriver(Configuration, seedRequestFileName);
    }

    public void SetOrientation(ScreenOrientation orientation)
    {
        var sizeOutput = RunAdb(Configuration, "shell", "wm", "size");
        var sizeMatch = System.Text.RegularExpressions.Regex.Match(
            sizeOutput, @"Physical size:\s*(\d+)x(\d+)");
        if (!sizeMatch.Success)
            throw new InvalidOperationException(
                $"Could not determine the device's physical display size: {sizeOutput}");

        var naturalLandscape = int.Parse(sizeMatch.Groups[1].Value)
            > int.Parse(sizeMatch.Groups[2].Value);
        var rotation = (orientation, naturalLandscape) switch
        {
            (ScreenOrientation.Landscape, true) => "0",
            (ScreenOrientation.Portrait, false) => "0",
            (ScreenOrientation.Landscape, false) => "1",
            (ScreenOrientation.Portrait, true) => "1",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };
        RunAdb(Configuration, "shell", "cmd", "window", "user-rotation", "lock", rotation);
    }

    internal string ExecuteAdb(params string[] arguments) =>
        RunAdb(Configuration, arguments);

    private static AndroidDriver CreatePreparedDriver(
        AndroidUiTestConfiguration configuration,
        string seedRequestFileName = AndroidAutomationConstants.TestSeedRequestFileName)
    {
        RequestDeterministicSeed(configuration, seedRequestFileName);
        var driver = CreateDriver(configuration);

        try
        {
            driver.ExecuteScript(
                "mobile: activateApp",
                new Dictionary<string, object>
                {
                    ["appId"] = AndroidAutomationConstants.AppPackage,
                });
            return driver;
        }
        catch
        {
            driver.Quit();
            throw;
        }
    }

    public string CaptureFailureArtifacts(string testName, Exception exception)
    {
        var artifactDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "TestResults",
            $"{SanitizeFileName(testName)}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}");
        Directory.CreateDirectory(artifactDirectory);

        var detailsPath = Path.Combine(artifactDirectory, "failure.txt");
        File.WriteAllText(detailsPath, exception.ToString());
        try
        {
            Driver.GetScreenshot().SaveAsFile(Path.Combine(artifactDirectory, "screenshot.png"));
            File.WriteAllText(Path.Combine(artifactDirectory, "page-source.xml"), Driver.PageSource);
        }
        catch (Exception captureException)
        {
            File.AppendAllText(detailsPath,
                $"{Environment.NewLine}{Environment.NewLine}Artifact capture failed:{Environment.NewLine}{captureException}");
        }

        try
        {
            var logcat = RunAdb(Configuration, "logcat", "-d", "-t", "500");
            File.WriteAllText(Path.Combine(artifactDirectory, "logcat.txt"), logcat);
        }
        catch (Exception logcatException)
        {
            File.AppendAllText(detailsPath,
                $"{Environment.NewLine}{Environment.NewLine}Logcat capture failed:{Environment.NewLine}{logcatException}");
        }

        return artifactDirectory;
    }

    public void Dispose() => driver?.Quit();

    private static AndroidDriver CreateDriver(AndroidUiTestConfiguration configuration)
    {
        var options = new AppiumOptions
        {
            PlatformName = AndroidAutomationConstants.PlatformName,
            AutomationName = AndroidAutomationConstants.AutomationName,
            DeviceName = configuration.DeviceName,
        };

        options.AddAdditionalAppiumOption("noReset", true);

        if (configuration.DeviceId is not null)
            options.AddAdditionalAppiumOption("udid", configuration.DeviceId);

        if (configuration.PlatformVersion is not null)
            options.PlatformVersion = configuration.PlatformVersion;

        return new AndroidDriver(
            configuration.AppiumServerUrl,
            options,
            TimeSpan.FromMinutes(2));
    }

    private static void RequestDeterministicSeed(
        AndroidUiTestConfiguration configuration,
        string seedRequestFileName)
    {
        RunAdb(configuration, "shell", "am", "force-stop", AndroidAutomationConstants.AppPackage);
        RunAdb(configuration, "shell", "run-as", AndroidAutomationConstants.AppPackage,
            "mkdir", "-p", "files");
        foreach (var requestFileName in AndroidAutomationConstants.TestSeedRequestFileNames)
            RunAdb(configuration, "shell", "run-as", AndroidAutomationConstants.AppPackage,
                "rm", "-f", $"files/{requestFileName}");
        RunAdb(configuration, "shell", "run-as", AndroidAutomationConstants.AppPackage,
            "touch", $"files/{seedRequestFileName}");
        RunAdb(configuration, "logcat", "-c");
    }

    private static string RunAdb(AndroidUiTestConfiguration configuration, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = configuration.AdbPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        if (configuration.DeviceId is not null)
        {
            process.StartInfo.ArgumentList.Add("-s");
            process.StartInfo.ArgumentList.Add(configuration.DeviceId);
        }
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        if (!process.Start())
            throw new InvalidOperationException("Could not start adb.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30_000) || process.ExitCode != 0)
            throw new InvalidOperationException(
                $"adb failed ({process.ExitCode}): {standardError}{standardOutput}");
        return standardOutput;
    }

    private static string SanitizeFileName(string value) => string.Concat(
        value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}
