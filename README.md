# Softball Scorekeeper

## Project Overview

Softball Scorekeeper is a lightweight, offline-first scorekeeping application for adult slow-pitch softball. It is intended to prioritize fast score entry, minimal user input, automatic statistics, and reliable use without an internet connection.

The primary platform is Android tablet, with Windows as the secondary platform. The repository currently contains the application foundation; the planned softball workflows have not yet been implemented.

## Current Status

- **Foundation phase:** Complete
- **Completed foundation stories:** Stories 0.0 through 0.10
- **Next planned story:** Story 1.0 — Create Landing Page

The application has foundational presentation, dependency-injection, persistence, resource, configuration, and documentation infrastructure. Team, player, game, lineup, scorekeeping, statistics, and beer-rule workflows do not exist yet.

## Technology Stack

- .NET MAUI
- C#
- XAML
- CommunityToolkit.Mvvm
- SQLite through `sqlite-net-pcl`
- Built-in .NET dependency injection

## Repository Structure

The application uses a pragmatic single-project MAUI structure while preserving separation of responsibilities through folders and repository conventions.

- `Configuration` — Strongly typed application settings and defaults.
- `Data` — Device-local SQLite connection and initialization infrastructure.
- `Models` — Reserved for models introduced by future stories.
- `Resources` — Styles, images, fonts, app assets, and centralized `.resx` strings.
- `Services` — Reserved for application services introduced by future stories.
- `ViewModels` — MVVM presentation state and commands.
- `Views` — MAUI XAML pages and minimal code-behind.

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

## Current Functionality

The implemented foundation includes:

- A basic single-project .NET MAUI application targeting Android and Windows
- XAML and CommunityToolkit.Mvvm infrastructure
- Dependency-injection registrations and constructor injection
- Device-local SQLite connection and asynchronous initialization
- Centralized strongly typed `.resx` resources for user-facing text
- Strongly typed application settings registered through dependency injection
- Repository implementation guidance and canonical feature-history documentation

The current UI remains a foundational starter screen with a resource-backed counter. Actual softball workflows have not yet been implemented.

## Planned Features

Planned functionality includes:

- Team, roster, and player management
- Game and doubleheader management
- Lineup creation
- In-game scorekeeping
- Automatic player and team statistics
- Custom beer-rule tracking

## Project Boundaries

The application is device-local and offline-first. The current product scope excludes cloud synchronization, user accounts, and server-side services. Messaging, live streaming, opponent roster management, and similar platform-scale features are also outside the current scope.

## Contributing

Before implementing changes, contributors and AI coding assistants must read [AGENTS.md](AGENTS.md) and [FEATURE_HISTORY.md](FEATURE_HISTORY.md). Keep changes focused on the current story, reuse established patterns, and update `FEATURE_HISTORY.md` with the completed implementation and validation results before committing.
