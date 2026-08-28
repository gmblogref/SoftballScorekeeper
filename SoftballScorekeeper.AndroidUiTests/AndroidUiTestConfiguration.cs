namespace SoftballScorekeeper.AndroidUiTests;

internal sealed record AndroidUiTestConfiguration(
    Uri AppiumServerUrl,
    string DeviceName,
    string? DeviceId,
    string? PlatformVersion,
    string AdbPath)
{
    public static AndroidUiTestConfiguration Load()
    {
        var serverUrl = GetValueOrDefault(
            "SOFTBALL_APPIUM_SERVER_URL",
            AndroidAutomationConstants.DefaultAppiumServerUrl);

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var appiumServerUrl))
            throw new InvalidOperationException(
                "SOFTBALL_APPIUM_SERVER_URL must be an absolute URL.");

        return new AndroidUiTestConfiguration(
            appiumServerUrl,
            GetValueOrDefault(
                "SOFTBALL_ANDROID_DEVICE_NAME",
                AndroidAutomationConstants.DefaultDeviceName),
            GetOptionalValue("SOFTBALL_ANDROID_DEVICE_ID"),
            GetOptionalValue("SOFTBALL_ANDROID_PLATFORM_VERSION"),
            GetValueOrDefault("SOFTBALL_ADB_PATH", "adb"));
    }

    private static string GetValueOrDefault(string name, string defaultValue) =>
        GetOptionalValue(name) ?? defaultValue;

    private static string? GetOptionalValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
