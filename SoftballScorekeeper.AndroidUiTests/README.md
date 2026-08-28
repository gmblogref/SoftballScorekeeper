# Android UI Tests

`SoftballScorekeeper.AndroidUiTests` is the separately runnable Appium suite for Android UI and integration automation. It does not reference or replace `SoftballScorekeeper.Tests`, which remains the fast, emulator-independent repository test suite.

The Android UI tests are opt-in acceptance tests. Ordinary solution, Test Explorer,
and `SoftballScorekeeper.Tests` runs do not require Appium or an emulator; the two
Android UI tests are discovered but skipped unless the supported runner explicitly
enables them. A runner-enabled test still fails normally when an Appium or emulator
prerequisite is broken.

## Prerequisites

- .NET 9 SDK with the Android workload
- Android SDK and `adb`
- The Android `emulator` CLI on `PATH`
- Node.js and npm
- Appium `3.7.0` (currently validated)
- Appium UiAutomator2 driver `8.5.0` (currently validated)

The standard local target is the Android 15 / API 35, x86_64, 1280x800 AVD
whose actual CLI identifier is `softball_tablet_api_35`. The runner does not
hardcode an emulator serial.

## Preferred workflow

1. Start Appium in a separate terminal. The runner expects the current default
   endpoint at `http://127.0.0.1:4723/`:

   ```powershell
   appium
   ```

2. From the repository root, run:

   ```powershell
   .\scripts\run-android-ui-tests.ps1
   ```

The runner then:

- Uses an existing usable device when exactly one is connected, or starts the
  standard `softball_tablet_api_35` AVD when necessary.
- Waits for the emulator to become an `adb` device and for Android boot completion.
- Builds the required embedded-assembly Android APK.
- Installs the APK on the selected device.
- Runs the complete Android UI suite.
- Shuts down only an emulator that it started, including during failure cleanup.

Appium remains externally managed. The runner verifies Appium's `/status` endpoint,
but it does not start or stop the Appium server.

The runner supplies the internal `SOFTBALL_RUN_ANDROID_UI_TESTS=1` opt-in automatically;
developers do not need to set it for the supported workflow.

To leave an emulator started by the runner running after the tests, use:

```powershell
.\scripts\run-android-ui-tests.ps1 -KeepEmulatorRunning
```

The runner never shuts down a device that was usable before it began. With multiple
usable devices, it requires `-DeviceId` or `SOFTBALL_ANDROID_DEVICE_ID`. An explicitly
selected device must already be usable, and an unreachable Appium endpoint stops the
runner before the build.

Optional runner parameters:

```powershell
.\scripts\run-android-ui-tests.ps1 -DeviceId emulator-5554
.\scripts\run-android-ui-tests.ps1 -AvdName softball_tablet_api_35
.\scripts\run-android-ui-tests.ps1 -TestFilter 'FullyQualifiedName~ScoringResumeWorkflowTests'
.\scripts\run-android-ui-tests.ps1 -AppiumServerUrl 'http://127.0.0.1:4723/' -AdbPath 'adb'
```

`-AvdName` overrides `SOFTBALL_ANDROID_AVD`; both override the default
`softball_tablet_api_35` AVD used only when no usable device is connected.
`-DeviceId`, `-AppiumServerUrl`, and `-AdbPath` override their corresponding
environment variables for that run. `-TestFilter` is passed to `dotnet test`; omit
it to run the complete suite.

## Lower-level manual commands

For troubleshooting, the runner's build, install, and focused workflow can be run
separately:

```powershell
dotnet build SoftballScorekeeper/SoftballScorekeeper.csproj --framework net9.0-android --no-restore --no-incremental --property:EmbedAssembliesIntoApk=true --maxcpucount:1
adb -s emulator-5554 install -r SoftballScorekeeper/bin/Debug/net9.0-android/com.companyname.softballscorekeeper-Signed.apk
$env:SOFTBALL_ANDROID_DEVICE_ID = 'emulator-5554'
$env:SOFTBALL_RUN_ANDROID_UI_TESTS = '1'
dotnet test SoftballScorekeeper.AndroidUiTests/SoftballScorekeeper.AndroidUiTests.csproj --filter 'FullyQualifiedName~ScoringResumeWorkflowTests'
```

`EmbedAssembliesIntoApk=true` is required because the fixture clears application data;
a Fast Deployment APK loses its separately deployed assemblies when that data is
cleared. The fixture then creates a Debug-only seed request through `adb`, launches
the installed package, and the app prepares one Team, three Players, roster
memberships, one Season, one Game, and a frozen lineup. Release builds contain no
seed initializer. Every test process starts from a newly cleared application-data
directory.

For lower-level troubleshooting only, opt in and run the complete Android UI suite
against an already installed APK, including the session smoke test:

```powershell
$env:SOFTBALL_RUN_ANDROID_UI_TESTS = '1'
dotnet test SoftballScorekeeper.AndroidUiTests/SoftballScorekeeper.AndroidUiTests.csproj
```

Failure diagnostics are written to
`<test-output>/TestResults/<test-name>-<UTC timestamp>/`. The ignored directory
contains the exception, screenshot, Appium page source, and recent `adb logcat`
output.

## Optional environment variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `SOFTBALL_APPIUM_SERVER_URL` | `http://127.0.0.1:4723/` | Appium server endpoint |
| `SOFTBALL_ANDROID_DEVICE_NAME` | `Android Emulator` | Appium device-name capability |
| `SOFTBALL_ANDROID_DEVICE_ID` | unset | Selects a specific `adb` device, such as `emulator-5554` |
| `SOFTBALL_ANDROID_AVD` | `softball_tablet_api_35` | Selects the AVD to start when no usable device is connected |
| `SOFTBALL_ANDROID_PLATFORM_VERSION` | unset | Restricts the Android version when needed |
| `SOFTBALL_ADB_PATH` | `adb` | Uses a non-default `adb` executable without storing a machine-specific path |
