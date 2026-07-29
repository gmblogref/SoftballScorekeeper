# Softball Scorekeeper

## Project Overview

Softball Scorekeeper is a lightweight, fully offline scorekeeper for live adult slow-pitch softball games. It is focused on fast entry, minimal taps, automatic statistics, custom beer-rule tracking, and simple correction of unusual plays.

The primary deployment and UX target is a 10-inch Android tablet, with a landscape-first scoring experience unless real-device testing supports a different decision. Windows remains a supported secondary platform for development, debugging, validation, and convenience.

## Tablet-First Experience

Android tablet behavior is the primary UI and interaction acceptance standard. The interface prioritizes large finger-friendly touch targets, clear readability at normal tablet viewing distance, minimal interaction steps, one-handed use where practical, stable control placement, touch-appropriate spacing, and reliable offline operation. Desktop conventions must not drive the primary experience when they conflict with Android tablet usability.

## Current Status

- **Foundation phase:** Complete
- **Latest completed story:** Story 1.2 — Edit Team
- **Completed stories:** Stories 0.0 through 1.2

The application has foundational presentation, dependency-injection, persistence, resource, configuration, and documentation infrastructure. Its landing page and Team creation and editing workflows are implemented. Player, game, roster, lineup, scorekeeping, statistics, and beer-rule workflows remain planned.

## Technology Stack

- .NET MAUI
- C#
- XAML
- CommunityToolkit.Mvvm
- SQLite through `sqlite-net-pcl`
- Built-in .NET dependency injection

## Repository Structure

The application currently uses one pragmatic .NET MAUI project while preserving separation of responsibilities through logical folders and repository conventions.

- `Configuration` — Strongly typed application settings and defaults.
- `Data` — Device-local SQLite initialization and repository infrastructure.
- `Models` — Application data models introduced by feature stories.
- `Resources` — Styles, images, fonts, app assets, and centralized `.resx` strings.
- `Services` — Reserved for application services introduced by future stories.
- `ViewModels` — MVVM presentation state and commands.
- `Views` — MAUI XAML pages and minimal code-behind.

## Architecture Direction

The current single-project structure, XAML/MVVM presentation, CommunityToolkit.Mvvm, dependency injection, centralized resources, configuration, and SQLite persistence remain valid through Team CRUD and Player CRUD.

A later dedicated story is expected to transition the solution toward pragmatic layered/Clean Architecture boundaries for Presentation / .NET MAUI, Application, Domain, Infrastructure, and Tests. This transition is planned after Team and Player CRUD but before roster, scheduling, lineup, statistics, and scoring-rule complexity grows substantially. It has not been implemented; clear dependency direction and testability matter more than project count or unnecessary interfaces and abstractions.

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

## Testing Direction

An automated test project is planned during or immediately after the architecture transition and before complex roster, lineup, statistics, and scoring-engine development. Domain and application rules should be testable without launching MAUI, and the scoring engine is expected to receive comprehensive table-driven coverage. Repository integration tests may use isolated temporary SQLite databases. UI automation is not currently required unless a later story adds it.

Manual Android and Windows validation remain part of the current workflow until automated testing is introduced. Android validation is the primary UI standard; emulator validation is useful, with real-device validation expected for major tablet interaction and scoring workflows when the target device is available.

## Current Functionality

The current implementation includes:

- A basic single-project .NET MAUI application targeting Android and Windows
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
