# Softball Scorekeeper

<p align="center">
  <img src="SoftballScorekeeper/Resources/AppIcon/softball_scorekeeper_icon.png" alt="Softball Scorekeeper logo" width="140">
</p>

Softball Scorekeeper is a fast, offline-first scorekeeping application for live adult slow-pitch softball. It is designed around a touch-friendly Android tablet workflow with quick scoring, automatic runner-movement defaults, easy corrections, automatic statistics, and configurable Beer Rules.

Version 1.0.1 is the latest published release.

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

Keep all extracted files and directories together. The ZIP includes the .NET 10 and Windows App SDK runtime dependencies used by the application. The portable release has been validated on a Windows development computer; clean-machine validation remains pending.

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
- **Windows x64:** Supported through a self-contained portable ZIP with no installer or MSIX registration. The portable release has been validated on a Windows development computer; clean-machine validation remains pending.

## Developer Setup

### Prerequisites

- .NET 10 SDK 10.0.402
- Visual Studio with .NET MAUI development support, or an equivalent .NET MAUI toolchain
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
