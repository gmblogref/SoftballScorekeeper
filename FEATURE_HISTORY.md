# Softball Scorekeeper — Feature History

## Document Purpose

This file is the canonical implementation-history record for the Softball Scorekeeper repository. Future AI assistants, implementation chats, developers returning to the project, reviewers, and maintainers should use it to understand completed work and the actual current repository state before starting a story.

Update this file for every story after implementation and validation and before committing. Entries summarize completed implementation work, reusable components, important decisions, validation actually performed, known limitations, and deferred work. They do not duplicate full story specifications or acceptance criteria.

Stable product and architecture decisions belong in the project architecture documentation. Repository-wide implementation instructions belong in `AGENTS.md`. This document records what has actually been implemented.

## Current Project Status

- **Current story:** Story 0.9 — Create and Require Feature History Updates
- **Completed stories before the current story:** Stories 0.0 through 0.8
- **Next planned implementation story:** Story 1.0 — Create Landing Page
- **Current phase:** Foundation phase
- **Foundation status:** The implementation foundation is complete; the phase will be complete when Story 0.9 documentation is validated and committed.

The repository contains a single-project .NET 9 MAUI application targeting Android and Windows. Its implemented foundation includes repository guidance, a pragmatic folder structure, CommunityToolkit.Mvvm, MAUI dependency injection, device-local SQLite initialization, centralized user-facing resources, and strongly typed application settings. Product-facing softball features remain deferred.

## Completed Story Summaries

### Story 0.0 — Verify Development Environment

**Status:** Complete

#### Implementation Summary

Verified that the development workflow was ready before application implementation began. The recorded environment covered Visual Studio and Visual Studio Code, the .NET SDK and MAUI tooling, Android and Windows development capability, Git, GitHub access, and the Codex-assisted workflow. Exact tool versions were not preserved in the available repository history.

#### Impact on Future Stories

The required development and repository workflow was established before the MAUI foundation was created. Re-verify local workloads or device tooling only when a later story requires it or an environment has changed.

### Story 0.1 — Create Repository and MAUI Foundation

**Status:** Complete

#### Implementation Summary

- Created the private GitHub repository `SoftballScorekeeper` with the default branch `main`.
- Added `SoftballScorekeeper.sln` and the single-project MAUI application `SoftballScorekeeper`.
- Limited active targets to Android and Windows and upgraded the foundation to .NET 9.
- Added the root `README.md`, `.gitignore`, and `.gitattributes`.
- Retained the MAUI template application as the initial runnable foundation.

#### Architecture and Design Decisions

- Android tablet is the primary platform; Windows is secondary.
- The solution remains a single MAUI project unless a future architectural need justifies a split.

#### Existing Components to Reuse

- `SoftballScorekeeper.sln`
- `SoftballScorekeeper/SoftballScorekeeper.csproj`
- Existing MAUI platform and resource structure

#### Validation Completed

Recorded story validation included successful Windows and Android builds and launches.

#### Deferred Work

Application architecture, persistence, MVVM, navigation, and softball functionality were deferred to later stories.

#### Commit Messages

- `Add .gitattributes and .gitignore.`
- `Add project files.`
- `add initial README`
- `Limit MAUI targets to Android and Windows`
- `Upgrade MAUI foundation to .NET 9`

#### Impact on Future Stories

Do not recreate the repository, solution, MAUI project, or supported targets. Extend the existing single-project foundation.

### Story 0.2 — Add Repository Development Guidance

**Status:** Complete

#### Implementation Summary

Created root-level `AGENTS.md` as the authoritative repository guidance for AI assistants and contributors. It records product boundaries, architecture and dependency direction, presentation/application/domain/infrastructure responsibilities, coding standards, resource conventions, testing expectations, Git practices, documentation expectations, and Version 1 exclusions.

#### Architecture and Design Decisions

- Use pragmatic layered architecture and Clean Architecture principles where they add clear value.
- Keep business rules out of views and code-behind.
- Preserve offline operation and avoid cloud or account infrastructure.

#### Validation Completed

Documentation content and repository scope were reviewed; no runtime files changed.

#### Commit Message

`docs: add repository AI development guidance`

#### Impact on Future Stories

Read and follow `AGENTS.md` before implementation.

### Story 0.3 — Configure Initial Project Structure

**Status:** Complete

#### Implementation Summary

- Added tracked `Data`, `Models`, `Services`, and `ViewModels` folders and created `Views` by moving `MainPage` into it.
- Updated `MainPage` namespaces and `AppShell` references.
- Retained the existing `Resources/Styles` organization.
- Kept the single-project solution and temporarily retained the template page content for platform validation.

#### Existing Components to Reuse

- `Data`, `Models`, `Services`, `ViewModels`, and `Views` responsibilities
- Existing styles and single-project organization

#### Validation Completed

Recorded story validation included successful Windows and Android builds and launches.

#### Known Warnings or Limitations

Template page content and temporary template warnings remained intentionally for later stories.

#### Commit Message

`Configure initial project infrastructure`

#### Impact on Future Stories

Reuse the existing folder structure. Do not split the solution or introduce parallel organization without a documented architectural need.

### Story 0.4 — Add MVVM Toolkit and Dependency Injection

**Status:** Complete

#### Implementation Summary

- Added `CommunityToolkit.Mvvm` version `8.4.2`.
- Added `ViewModels/MainPageViewModel.cs`, deriving from `ObservableObject` and using generated observable properties and a generated relay command.
- Moved counter state, display text, and increment behavior from page code-behind into the view model.
- Bound the existing button to `CounterText` and `IncrementCounterCommand`.
- Constructor-injected `MainPageViewModel` into `MainPage`.
- Registered `AppShell`, `MainPage`, and `MainPageViewModel` as singletons in the MAUI DI container.

#### Architecture and Design Decisions

The root Shell, page, and view model use intentional singleton lifetimes because they form the current long-lived root UI. `App` receives `IServiceProvider`, calls `InitializeComponent()`, and only then resolves the registered `AppShell`. DI constructs `AppShell`, which receives the registered `MainPage`; DI constructs `MainPage` with its registered view model.

The startup order is critical: `App.InitializeComponent()` must complete before resolving `AppShell` or any page that uses resources from `App.xaml`. Resolving `AppShell` directly in the `App` constructor parameters caused `StaticResource` lookup failure and was corrected.

#### Existing Components to Reuse

- CommunityToolkit.Mvvm attributes and base types
- Constructor-injected pages and view models
- Existing singleton registrations and post-`InitializeComponent()` Shell resolution

#### Validation Completed

Recorded validation included Windows and Android runtime checks. Windows compilation also succeeded after the corrected startup ordering.

#### Known Warnings or Limitations

`MVVMTK0045` remains for the field-based `[ObservableProperty]` on `counterText` in WinRT scenarios. It has not been suppressed or refactored.

#### Commit Message

`added mvvm toolkit to project`

#### Impact on Future Stories

Follow the established XAML/MVVM/DI pattern. Do not resolve resource-dependent pages before application resources load.

### Story 0.5 — Add SQLite Infrastructure

**Status:** Complete

#### Implementation Summary

- Added `sqlite-net-pcl` version `1.11.285`.
- Added sealed `Data/AppDatabase.cs` using a private `SQLiteAsyncConnection`.
- Created the device-local database `SoftballScorekeeper.db3` under `FileSystem.AppDataDirectory` using read/write, create, and shared-cache flags.
- Added idempotent, concurrency-safe `InitializeAsync()` guarded by `SemaphoreSlim`; `SELECT 1` opens or creates the empty database.
- Registered `AppDatabase` as a singleton.
- Constructor-injected it into `App` and initialized it from the MAUI Window `Created` event.
- Added SQLite database and sidecar patterns to `.gitignore`.

#### Architecture and Design Decisions

One application-wide database service owns one long-lived connection. Initialization failures remain observable. No static connection or service locator was introduced.

#### Existing Components to Reuse

- `AppDatabase`
- Existing SQLite connection flags, application-data path, and startup initialization timing

#### Validation Completed

Recorded story validation confirmed database creation on Windows and Android at each platform's application-data path.

#### Deferred Work

No tables, persistence/domain models, repositories, migrations, seed data, or CRUD behavior exist yet.

#### Commit Message

`Add SQLite database infrastructure`

#### Impact on Future Stories

Extend the existing `AppDatabase` infrastructure rather than creating a second database service or connection pattern.

### Story 0.6 — Centralize Application Resources

**Status:** Complete

#### Implementation Summary

- Added `Resources/Strings/AppResources.resx` and committed `AppResources.Designer.cs`.
- Configured `PublicResXFileCodeGenerator`, generated class `AppResources`, and namespace `SoftballScorekeeper.Resources.Strings`.
- Added resource keys: `ShellTitle`, `MainPageTitle`, `MainPageHeading`, `MainPageInstructions`, `MainPageImageSemanticDescription`, `MainPageInstructionsSemanticDescription`, `CounterButtonText`, `CounterButtonSemanticHint`, `CounterStatusSingular`, and `CounterStatusPlural`.
- Replaced static XAML text and accessibility text with strongly typed `x:Static` references.
- Updated `MainPageViewModel` to initialize from `CounterButtonText` and format singular/plural counter status through `string.Format`, `CultureInfo.CurrentCulture`, and the resource templates.

#### Architecture and Design Decisions

Runtime user-facing text belongs in `.resx` resources. The MAUI `<ApplicationTitle>` remains project/package metadata because MSBuild cannot consume a runtime generated resource accessor without unnecessary build complexity.

#### Existing Components to Reuse

- `AppResources` and its strongly typed properties
- Existing resource-generator project metadata
- `x:Static` for static XAML text and culture-aware resource formatting for dynamic text

#### Validation Completed

Recorded validation included successful Windows and Android checks and a project-wide hardcoded user-facing string audit.

#### Known Warnings or Limitations

The existing `MVVMTK0045` warning remains. No runtime language switching or language variants exist.

#### Commit Message

`Add centralized application resources`

#### Impact on Future Stories

Place runtime display and accessibility text in `AppResources` or another intentionally approved focused resource file. Do not reintroduce hardcoded user-facing text in runtime C# or XAML.

### Story 0.7 — Centralize Application Settings

**Status:** Complete

#### Implementation Summary

- Audited non-UI literals and distinguished configuration from shared identifiers and appropriate inline implementation details.
- Added sealed `Configuration/ApplicationSettings.cs` with the default `DatabaseFileName` value `SoftballScorekeeper.db3`.
- Registered `ApplicationSettings` as a singleton and constructor-injected it into `AppDatabase`.
- Added null and null/empty/whitespace filename validation.
- Preserved the existing database filename, application-data location, SQLite flags, connection lifetime, locking, and initialization timing.

#### Architecture and Design Decisions

A small strongly typed settings object is sufficient for current configuration. No `appsettings.json`, `IOptions<T>`, configuration package, static settings provider, or generic constants class was introduced. `SELECT 1` remains an appropriate local SQL implementation literal.

#### Existing Components to Reuse

- `ApplicationSettings` and its DI registration
- Constructor injection into configuration consumers
- `AppDatabase` validation and path construction

#### Validation Completed

Recorded validation included successful Windows and Android checks. Android validation included a clean-and-rebuild step after the constructor dependency changed.

#### Known Warnings or Limitations

Changing `DatabaseFileName` later would point the application at a different file and requires an explicit data-compatibility decision.

#### Commit Message

`centralize application settings`

#### Impact on Future Stories

Add only genuine application configuration to the focused strongly typed settings pattern unless a later story explicitly replaces it. Keep technical SQL and user-facing resources in their proper owners.

### Story 0.8 — Update AGENTS.md Configuration Guidance

**Status:** Complete

#### Implementation Summary

Updated `AGENTS.md` to refine runtime user-facing resource guidance and add focused guidance for application configuration, shared technical constants, and appropriate inline technical literals.

#### Architecture and Design Decisions

The documented decision boundary is:

- Text shown to users belongs in `.resx` resources.
- Application behavior or runtime setup belongs in centralized configuration.
- Reused technical identifiers may use focused constants near their owner.
- Clear local one-use implementation details may remain inline.

The repository does not require automatic extraction of every string or numeric literal.

#### Validation Completed

The complete document and focused diff were reviewed, `git diff --check` passed, and only `AGENTS.md` changed. This documentation-only story did not require application builds.

#### Commit Message

`updated agents file`

#### Impact on Future Stories

Follow the documented decision boundary and avoid generic constants or configuration dumping grounds.

## Maintenance Rules

Every future story must update this file before the story is committed. A story is not complete until its accurate feature-history entry is included in the same story-focused commit as its implementation.

Required workflow:

1. Implement the story.
2. Validate the acceptance criteria.
3. Update `FEATURE_HISTORY.md`.
4. Review the new history entry for accuracy.
5. Review `git diff`.
6. Review `git status`.
7. Confirm the feature-history change is included.
8. Commit the implementation and feature-history update together.

Entries must:

- Remain in chronological order.
- Describe the actual implementation concisely rather than plans.
- Record important architecture and design decisions.
- Identify existing components and patterns future work must reuse.
- Record validation actually completed.
- Record known warnings, limitations, and deferred work when relevant.
- Avoid copying full user-story specifications or acceptance criteria.
- Preserve prior entries unless correcting inaccurate information.
- Preserve historical clarity when correcting a prior entry.
- Never invent unknown information.
