# Android UI Tests

`SoftballScorekeeper.AndroidUiTests` is the separately runnable Appium suite for Android UI and integration automation. It does not reference or replace `SoftballScorekeeper.Tests`, which remains the fast, emulator-independent repository test suite.

The Android UI tests are opt-in acceptance tests. Ordinary solution, Test Explorer,
and `SoftballScorekeeper.Tests` runs do not require Appium or an emulator; the three
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

From the repository root, run:

```powershell
.\scripts\run-android-ui-tests.ps1
```

The runner then:

- Checks the configured Appium status endpoint. It reuses a healthy pre-existing
  server, or starts the installed Appium CLI and waits for a healthy status response.
- Verifies that UiAutomator2 is installed before starting a new emulator.
- Uses an existing usable device when exactly one is connected, or starts the
  standard `softball_tablet_api_35` AVD when necessary.
- Waits for the emulator to become an `adb` device, for Android boot completion,
  and for the package manager to accept install operations.
- Builds the required embedded-assembly Android APK.
- Installs the APK on the selected device.
- Runs the complete Android UI suite.
- Shuts down only the exact Appium process it started. A healthy Appium server that
  existed before the runner began remains running.
- Shuts down only an emulator that it started, including during failure cleanup,
  and waits for both adb disappearance and the runner-owned process to exit before
  returning.

Appium and the compatible UiAutomator2 driver must already be installed; the runner
does not install or upgrade npm packages. Runner-owned Appium stdout and stderr are
captured beneath the ignored
`SoftballScorekeeper.AndroidUiTests/TestResults/Appium` directory. Routine runs stay
quiet, while startup and unexpected-exit failures include the recent log output.

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
.\scripts\run-android-ui-tests.ps1 -AppiumCommand 'appium'
```

`-AvdName` overrides `SOFTBALL_ANDROID_AVD`; both override the default
`softball_tablet_api_35` AVD used only when no usable device is connected.
`-DeviceId`, `-AppiumServerUrl`, and `-AdbPath` override their corresponding
environment variables for that run. `-AppiumCommand` selects an already-installed
Appium CLI when it is not available under the default `appium` command.
`-TestFilter` is passed to `dotnet test`; omit it to run the complete suite.

The Appium URL defaults to `http://127.0.0.1:4723/`. A local override supplies the
host, port, and optional base path used for automatic startup as well as the URL
forwarded to the tests. The runner can reuse a healthy remote Appium endpoint, but it
will not attempt to start or own a remote server. If the configured endpoint responds
without a healthy Appium status payload, the runner reports the conflict and does not
terminate the process occupying that port.

## Appium lifecycle troubleshooting

If Appium cannot be resolved, the runner fails before starting a new emulator and
reports the missing command. If installed-driver discovery does not report
UiAutomator2, it fails with the driver installation prerequisite; it never installs
or upgrades the driver automatically. Confirm the validated local setup with:

```powershell
appium --version
appium driver list --installed
```

If automatic startup times out, Appium exits early, or the port is already occupied,
review the error's log tail and the captured stdout/stderr files in the Appium test
results directory. Resolve only the reported endpoint or runner-owned PID. Never kill
all `node.exe` processes: unrelated developer tools may also use Node.

Manually running `appium` remains useful for debugging. A healthy manually started
server is detected and preserved by the repository runner. Stop that server from the
terminal that owns it when debugging is complete.

## Emulator lifecycle recovery

Normal runner-owned shutdown uses the supported `adb emu kill` command and waits up
to 60 seconds for the selected serial and owned emulator process to disappear. This
makes an immediate next run safe without a fixed delay. A healthy emulator retained
with `-KeepEmulatorRunning`, left by an interrupted runner, or started before the
runner is treated as pre-existing on the next invocation: it is reused when it is the
only usable device and is not shut down by that invocation.

To return one known emulator to a clean state, target its exact serial and wait until
it is absent from `adb devices` before rerunning:

```powershell
adb devices
adb -s emulator-5554 emu kill
adb devices
```

If the exact emulator is still starting, offline, or shutting down, let that lifecycle
finish or close that specific AVD through Android Device Manager before retrying. Do
not kill all emulator, qemu, adb, or Crashpad processes. Do not delete an AVD lock
merely because it exists: active emulators legitimately own lock files, and a
persistent lock filename alone does not prove stale state. Crashpad diagnostics alone
also do not establish the cause of an emulator failure. If startup continues to fail
after `adb` shows no device and the selected AVD has no live emulator process, capture
the exact emulator output and use Android Device Manager to inspect or cold-boot that
AVD rather than deleting SDK or AVD directories.

Appium lifecycle is independent of `-KeepEmulatorRunning`. When the runner starts
both resources, it stops Appium first after the tests and then either synchronously
stops its emulator or retains that emulator when requested. A later run reuses the
retained emulator and starts a fresh runner-owned Appium server when needed.

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
the installed package, and the app prepares one Team, ten Players, roster
memberships, one Season, one Game, and a frozen lineup ordered Alex Automation,
Blake Automation, Casey Automation, Drew Automation, Evan Automation, Finley
Automation, Gray Automation, Harper Automation, Jordan Automation, and Morgan
Automation. Release builds contain no seed initializer. Every Android UI test starts
from newly cleared application data and a new Appium session, so scoring workflows
remain independently runnable.

For lower-level troubleshooting only, opt in and run the complete Android UI suite
against an already installed APK and a separately started Appium server, including
the session smoke test:

```powershell
appium
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
