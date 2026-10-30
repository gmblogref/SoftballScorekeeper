# Softball Scorekeeper

<p align="center">
  <img src="SoftballScorekeeper/Resources/AppIcon/softball_scorekeeper_icon.png" alt="Softball Scorekeeper logo" width="140">
</p>

Softball Scorekeeper is a fast, offline-first scorekeeping application for live adult slow-pitch softball. It is designed around a touch-friendly Android tablet workflow with quick scoring, automatic runner-movement defaults, easy corrections, automatic statistics, and configurable Beer Rules.

Version 1.0.1 is the latest published release.

## Upcoming Version 2.0.0

Version 2.0.0 is being prepared but has not been published. It uses .NET 10, Visual Studio Community 2026 version 18.11.2, MAUI 10.0.110, Android version name 2.0.0/code 6, and Windows version 2.0.0.0. It preserves Android and Windows Release identity `com.companyname.softballscorekeeper`, SQLite schema version 6, and backup format version 1.

The signed Android candidate, `SoftballScorekeeper-2.0.0.apk` (36,477,330 bytes; SHA-256 `328DF16906732A5980B5661C5030D4E5B0A38CDA0EF0218DDC2FDDCDB3B6E58C`), passed the automated v1.0.1-to-v2.0.0 in-place upgrade workflow without uninstalling or clearing application data. The workflow preserved Test Mode Team and Player records and verified offline license documents plus portrait and landscape behavior. Final Backup/Restore was not independently verified with this signed APK, and physical-tablet acceptance was not performed; both await release-owner disposition.

The self-contained, unpackaged Windows x64 candidate, `SoftballScorekeeper-2.0.0-windows-x64-portable.zip` (118,020,556 bytes; SHA-256 `F0DE2C5A15AAE80260E0B72EE27D032DE61E04AF062AF045322C0F65D18935CA`), passed ZIP integrity and inventory checks plus manual startup, navigation, Production persistence, Backup/Restore, local-license, v1.0.1 persisted-data compatibility, continued-write, and restart acceptance. It contains 624 application/runtime files and 21 licensing files. The in-app Licenses menu remains intentionally Android-only because the Windows agreements and notices are included in the portable ZIP.

These candidates are not yet definitive release artifacts: Android was built from `d688bf5aeea09260680adb5246eb6c37fb9da265`, while Windows was built from `a1b2c8a1c8a5578060bf0b6f40d29b7e9523a1a1`. The release process requires one exact accepted merged-main source commit. Unless an explicit provenance exception is approved, both artifacts must be rebuilt from final accepted `main` and affected acceptance repeated. Publication also remains blocked on qualified clarification or legal review of the Microsoft downstream protective-terms question. No `v2.0.0` tag or GitHub Release has been created.

## Download and Install on Android

Android tablet is the primary supported deployment for Version 1.0.

1. Download the verified [`SoftballScorekeeper-1.0.1.apk`](https://github.com/gmblogref/SoftballScorekeeper/releases/download/v1.0.1/SoftballScorekeeper-1.0.1.apk) release asset.
2. Open the downloaded APK on the Android device.
3. If Android requests permission to install apps from that source, enable it for the installer you used and continue the installation.

The application is distributed directly through GitHub rather than an app store.

## Download and Run on Windows

Windows x64 is supported through a self-contained portable ZIP. No installer is required.

1. Download the verified [`SoftballScorekeeper-1.0.1-windows-x64-portable.zip`](https://github.com/gmblogref/SoftballScorekeeper/releases/download/v1.0.1/SoftballScorekeeper-1.0.1-windows-x64-portable.zip) release asset.
2. Extract the entire ZIP to a folder where you want to keep the application.
3. Open that folder and run `SoftballScorekeeper.exe`.

Keep all extracted files and directories together. The published v1.0.1 ZIP was produced from the .NET 9 release workflow and includes its required .NET and Windows App SDK runtime dependencies. Its package integrity and licensing inventory were verified, but isolated runtime-launch acceptance was not completed and is not claimed.

The portable executable is unsigned, so Windows may identify it as an unrecognized downloaded application or display a security warning. Verify that the ZIP came from the release page, and follow any organizational security requirements; do not bypass policies that prohibit running unsigned software.

### Windows Data and Removal

Windows application data is stored separately from the extracted program folder under:

```text
%LOCALAPPDATA%\User Name\com.companyname.softballscorekeeper\Data\
```

Production data is stored in `SoftballScorekeeper.db3`; Test Mode uses the separate `SoftballScorekeeper.Test.db3`. Use **Backup Data** and **Restore Data** for supported data transfer between devices or installations.

To remove the portable application, close it and delete its extracted folder. Deleting that folder does not delete the separately stored application data. Create a backup before removing the application or manually deleting saved data so that the database can be restored later.

## Version 1.0 Features

### Teams, Seasons, and Games

- Manage Players, Team rosters, Team Years, League Seasons, and scheduled Games.
- Create starting lineups manually or reuse a previous lineup.
- Copy a Team into its next year with its roster, season definitions, Beer settings, and unresolved Beer balances while preserving prior-year history.
- Finalize a Team Year and review its read-only Team Year Summary.

### Live Scoring and Lineups

- Score as the Home or Away team with inning, out, score, and batting-order tracking.
- Record hits, walks, outs, fielder's choices, sacrifice flies, strikeouts, dead-ball outs, double plays, and triple plays.
- Resolve base-runner movement with normal defaults and corrections for unusual outcomes.
- Apply Home Run limits and one-up rules.
- Undo and redo committed scoring actions and in-progress runner decisions.
- Exit and resume an active Game without losing scoring progress.
- Add, remove, replace, and re-enter Players in the live batting sequence without changing the frozen starting lineup.
- View the effective live lineup with CURRENT and NEXT batter indicators.

### Statistics and Game History

- View the canonical chronological Game Plays recorded during scoring.
- View Completed Game Stats, in-game statistics, and filterable Team Stats.
- Sort shared batting-statistics grids and review Team totals.
- Apply completed-game Manual Overrides to official batting totals and final scores without rewriting the original Game Plays.

### Beer Rules

- Configure Team Beer Rule defaults and prospective in-game settings.
- Automatically track supported scoring-derived Beer consequences.
- Record manual rule adjustments and BROUGHT credits.
- Review Beer Totals, calculate Low Man for a playing date, and carry unresolved balances into the next Team Year.

### Data Management

- Create and restore portable backups of the complete production database.
- Use an isolated Product Test Mode without changing Production data.
- Receive schedule-aware backup reminders after a League Season is complete.

## Offline and Local Data

Softball Scorekeeper remains fully functional without an internet connection. Teams, Players, Games, scoring history, statistics, Beer accounting, settings, and backup-reminder state are stored in a local SQLite database on the device.

There are no user accounts, server-side services, cloud database, or automatic cloud synchronization. Backup and Restore use files explicitly selected by the user; a document or cloud provider may store a chosen backup file, but the application does not depend on that provider during normal use.

## Platform Support

- **Android:** Primary Version 1.0 deployment and UI acceptance target, optimized for a 10-inch tablet with responsive portrait and landscape layouts. The project declares Android API 21 as its minimum supported platform version.
- **Windows x64:** Supported through a self-contained portable ZIP with no installer or MSIX registration. The published v1.0.1 ZIP passed package-integrity and licensing validation, but isolated runtime-launch acceptance was not completed.

## Developer Setup

### Prerequisites

- .NET 10 SDK 10.0.403 (`global.json` requests 10.0.402 with `latestPatch` roll-forward)
- Visual Studio Community 2026 version 18.11.2 with MAUI 10.0.110, or an equivalent .NET MAUI toolchain
- .NET MAUI Android workload and Android SDK for Android builds
- JDK 21 for Android command-line builds
- .NET MAUI Windows workload and Windows development tooling for Windows builds
- An Android emulator or physical Android device for Android runtime validation

Clone the repository and open `SoftballScorekeeper.sln` in Visual Studio, then select the Android or Windows target and run the application. Command-line builds can also be run from the repository root:

```powershell
dotnet restore SoftballScorekeeper.sln
dotnet build SoftballScorekeeper/SoftballScorekeeper.csproj --framework net10.0-android
dotnet build SoftballScorekeeper/SoftballScorekeeper.csproj --framework net10.0-windows10.0.19041.0
```

### Windows v2.0.0 portable release workflow

The validated .NET 10 preparation workflow restores the Windows ReadyToRun runtime pack before publishing. The Windows-specific restore below supplies `TargetFramework` globally, which also overwrites the referenced Core and Infrastructure asset graphs with the Windows target. The two following native project restores are therefore required to restore their `net10.0` graphs. This exact sequence was validated; an alternative project-reference-suppression command has not been validated and is not documented here.

```powershell
dotnet restore SoftballScorekeeper/SoftballScorekeeper.csproj --property:TargetFramework=net10.0-windows10.0.19041.0 --property:RuntimeIdentifier=win-x64 --property:PublishReadyToRun=true --property:WindowsPackageType=None --property:WindowsAppSDKSelfContained=true --disable-parallel
dotnet restore SoftballScorekeeper.Core/SoftballScorekeeper.Core.csproj --disable-parallel
dotnet restore SoftballScorekeeper.Infrastructure/SoftballScorekeeper.Infrastructure.csproj --disable-parallel
```

Before publishing, verify that Core and Infrastructure each have a `net10.0` target in their `obj/project.assets.json`, that the application has `net10.0-windows10.0.19041.0/win-x64`, and that its asset graph contains `Microsoft.NETCore.App.Crossgen2.win-x64`.

Publish the self-contained, unpackaged `win-x64` application with ReadyToRun enabled, then package it with `scripts/package-windows-portable.ps1`. The packager validates the v2.0.0 licensing manifest, recorded checksums, required notices, ZIP inventory, deterministic output, overwrite protection, and sensitive-file exclusions. Independently confirm the executable version and identity, fully inspect the ZIP, record its size and SHA-256, and reconcile its runtime payload with the production licensing inventory.

The accepted candidate ZIP contains the PolyForm license, consolidated third-party notices, root `LICENSE-INFO.txt`, and applicable component agreements under `licenses/common/` and `licenses/windows/`; keep all 645 extracted entries together. Its build-source commit differs from the Android candidate's source commit, so it is not yet the definitive release artifact. Build definitive binaries from the final accepted merged-main commit unless an explicit provenance exception is approved, and repeat affected runtime acceptance before tagging or publication.

## Testing

The emulator-independent xUnit suite exercises domain rules, SQLite repositories, scoring, statistics, Beer accounting, backup and restore, lifecycle behavior, and presentation contracts against isolated temporary databases:

```powershell
dotnet test SoftballScorekeeper.Tests/SoftballScorekeeper.Tests.csproj
```

The repository also includes an opt-in Android/Appium acceptance suite and its PowerShell runner. See the [Android UI test documentation](SoftballScorekeeper.AndroidUiTests/README.md) for its prerequisites, isolation rules, and supported commands. Android UI tests must use their dedicated test database and emulator; they are not part of the normal emulator-independent test command.

## Architecture and Repository Structure

The solution uses three production projects and two test projects:

- `SoftballScorekeeper` — .NET MAUI presentation layer and composition root for Android and Windows.
- `SoftballScorekeeper.Core` — platform-independent domain models and softball rules.
- `SoftballScorekeeper.Infrastructure` — SQLite persistence, repositories, backup and restore, and application workflow coordination; references Core.
- `SoftballScorekeeper.Tests` — emulator-independent xUnit coverage for Core, Infrastructure, and presentation contracts without referencing the MAUI project.
- `SoftballScorekeeper.AndroidUiTests` — opt-in Android/Appium acceptance tests.

The MAUI project references Core and Infrastructure, Infrastructure references Core, and Core references no other solution project. `MauiProgram` is the composition root. The architecture favors clear dependency direction and independently testable business rules without unnecessary project or interface layers.

The application uses .NET MAUI, C#, XAML, CommunityToolkit.Mvvm, built-in dependency injection, and `sqlite-net-pcl`.

## Project Documentation

- [AGENTS.md](AGENTS.md) contains repository-wide architecture, implementation, validation, Git, and documentation requirements.
- [FEATURE_HISTORY.md](FEATURE_HISTORY.md) is the canonical record of completed stories, implementation decisions, validation, and current repository state.
- [CONTRIBUTING.md](CONTRIBUTING.md) explains the pull-request workflow and contribution licensing.
- [SECURITY.md](SECURITY.md) defines supported versions and private vulnerability-reporting guidance.

## Project Boundaries

The application is intentionally device-local and offline-first. Version 1.0 does not provide cloud synchronization, user accounts, server-side services, messaging, live streaming, opponent roster management, or multi-user editing.

## License

Softball Scorekeeper is free to use for noncommercial purposes permitted by the [PolyForm Noncommercial License 1.0.0](LICENSE). The source code is provided in this repository for inspection, and noncommercial use, modification, and redistribution are permitted subject to the license terms.

Commercial use requires separate permission from the copyright holder. Selling the original application, or a commercial derivative that uses the licensed Softball Scorekeeper code, is not authorized by this license; attribution alone does not authorize commercial use.

This is source-available software, not OSI-approved open-source software. Third-party software and fonts retain their own licenses and notices, documented separately in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Windows Portable Distribution

Windows portable ZIPs produced by the release packager include the PolyForm license, the consolidated third-party notices, a root `LICENSE-INFO.txt`, and the applicable component agreements under `licenses/common/` and `licenses/windows/`. Microsoft components remain governed by their separate Microsoft license agreements and notices; they are not relicensed under PolyForm Noncommercial.

By downloading or using the Windows distribution, users agree to comply with the applicable third-party licensing conditions, including redistribution restrictions and required notices. The packaged notice summarizes these terms and their locations; it does not replace or modify an agreement, guarantee legally binding acceptance, or claim that every Microsoft downstream-license obligation has been satisfied.

## Contributing

Before making changes, read [AGENTS.md](AGENTS.md) and [FEATURE_HISTORY.md](FEATURE_HISTORY.md). Keep work focused on the approved story, preserve the established dependency direction, validate changes proportionately, and update the feature history before committing completed work.
