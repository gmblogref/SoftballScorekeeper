# Softball Scorekeeper

## Project Overview

Softball Scorekeeper is a lightweight, fully offline scorekeeper for live adult slow-pitch softball games. It is focused on fast entry, minimal taps, automatic statistics, custom beer-rule tracking, and simple correction of unusual plays.

The primary deployment and UX target is a 10-inch Android tablet, with a landscape-first scoring experience unless real-device testing supports a different decision. Windows remains a supported secondary platform for development, debugging, validation, and convenience.

## Tablet-First Experience

Android tablet behavior is the primary UI and interaction acceptance standard. The interface prioritizes large finger-friendly touch targets, clear readability at normal tablet viewing distance, minimal interaction steps, one-handed use where practical, stable control placement, touch-appropriate spacing, and reliable offline operation. Desktop conventions must not drive the primary experience when they conflict with Android tablet usability.

## Current Status

- **Foundation phase:** Complete
- **Latest completed story:** Story 3.0 — Layered Solution Restructure
- **Completed stories:** Stories 0.0 through 3.0

The application has foundational presentation, dependency-injection, persistence, resource, configuration, and documentation infrastructure. Team and Player management workflows are implemented. Game, roster, lineup, scorekeeping, statistics, and beer-rule workflows remain planned.

## Technology Stack

- .NET MAUI
- C#
- XAML
- CommunityToolkit.Mvvm
- SQLite through `sqlite-net-pcl`
- Built-in .NET dependency injection

## Repository Structure

The solution uses three focused production projects with one-way project references plus one automated test project.

- `SoftballScorekeeper` — .NET MAUI composition root, configuration, Views, ViewModels, resources, and platform assets.
- `SoftballScorekeeper.Core` — Platform-independent Team and Player models and shared domain types.
- `SoftballScorekeeper.Infrastructure` — Device-local SQLite initialization and Team and Player repositories; references Core.
- `SoftballScorekeeper.Tests` — xUnit repository tests for Core models and Infrastructure persistence behavior; references Core and Infrastructure only.

## Architecture Direction

The MAUI project references Core and Infrastructure, Infrastructure references Core, and Core references no other solution project. The Tests project references Core and Infrastructure without referencing MAUI. `MauiProgram` remains the only composition root. An Application project remains deferred until a later story requires it; clear dependency direction and testability matter more than unnecessary interfaces and abstractions.

## Project Documentation

- [AGENTS.md](AGENTS.md) contains repository-wide implementation, architecture, coding, resource, configuration, validation, Git, and documentation guidance.
- [FEATURE_HISTORY.md](FEATURE_HISTORY.md) is the canonical record of completed stories, implementation summaries, validation results, reusable components, and current project state.

## Build Requirements

Development requires:

- Visual Studio with .NET MAUI development support
- The .NET MAUI workload
- Windows development support for the Windows target
- An Android SDK plus an emulator or physical Android device for the Android target

Open `SoftballScorekeeper.sln` in Visual Studio, select either the Windows target or an available Android emulator/device, and run the application through the IDE.

## Testing

`SoftballScorekeeper.Tests` contains automated tests for Core models exercised through Infrastructure persistence, temporary SQLite database initialization and cleanup, `TeamRepository`, and `PlayerRepository`. Repository tests use isolated temporary file-backed SQLite databases, never use the real application database, and remove their temporary database files after execution.

Run the automated suite from the repository root:

```text
dotnet test
```

The suite does not currently include MAUI UI automation, Android instrumentation tests, Windows UI automation, a general ViewModel test suite, code-coverage threshold enforcement, or continuous-integration test execution. Manual Android and Windows validation therefore remains part of the workflow where appropriate. Android validation is the primary UI standard; emulator validation is useful, with real-device validation expected for major tablet interaction and scoring workflows when the target device is available.

## Current Functionality

The current implementation includes:

- A three-project solution with a .NET MAUI application targeting Android and Windows, a platform-independent Core library, and an Infrastructure library
- XAML and CommunityToolkit.Mvvm infrastructure
- Dependency-injection registrations and constructor injection
- Device-local SQLite connection and asynchronous initialization
- Centralized strongly typed `.resx` resources for user-facing text
- Strongly typed application settings registered through dependency injection
- Repository implementation guidance and canonical feature-history documentation
- A tablet-friendly landing page that loads active Teams
- Local Team creation and editing with validation and persistence

More advanced softball workflows remain planned and must not be treated as implemented before their stories are completed.

## Planned Features

Planned functionality includes:

- Team, roster, and player management
- Game and doubleheader management
- Lineup creation
- In-game scorekeeping
- Automatic player and team statistics
- Custom beer-rule tracking

## Branding Direction

The default .NET application icon and splash screen will be replaced early in the feature phase. Initial branding may be simple and generated so it establishes project ownership without delaying functional development, and Android and Windows assets should remain visually consistent.

## Project Boundaries

The application is device-local and offline-first. The current product scope excludes cloud synchronization, user accounts, and server-side services. Messaging, live streaming, opponent roster management, and similar platform-scale features are also outside the current scope.

## Contributing

Before implementing changes, contributors and AI coding assistants must read [AGENTS.md](AGENTS.md) and [FEATURE_HISTORY.md](FEATURE_HISTORY.md). Keep changes focused on the current story, reuse established patterns, and update `FEATURE_HISTORY.md` with the completed implementation and validation results before committing.
