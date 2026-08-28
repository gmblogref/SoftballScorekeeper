# Softball Scorekeeper — Feature History

## Document Purpose

This file is the canonical implementation-history record for the Softball Scorekeeper repository. Future AI assistants, implementation chats, developers returning to the project, reviewers, and maintainers should use it to understand completed work and the actual current repository state before starting a story.

Update this file for every story after implementation and validation and before committing. Entries summarize completed implementation work, reusable components, important decisions, validation actually performed, known limitations, and deferred work. They do not duplicate full story specifications or acceptance criteria.

Stable product and architecture decisions belong in the project architecture documentation. Repository-wide implementation instructions belong in `AGENTS.md`. This document records what has actually been implemented.

## Current Project Status

- **Latest completed story:** Story 6.7.2 — Android UI / Integration Test Automation Harness
- **Completed stories:** Stories 0.0 through 6.7.2; Stories 3.2 through 3.4 retain their recorded validation status
- **Current story:** None; Story 6.7.2 is complete and PR-ready on `task/android-ui-test-harness`
- **Current phase:** Scoring interaction refinement
- **Foundation status:** Complete

The repository contains a five-project .NET 9 solution: the Android- and Windows-targeted .NET MAUI application, platform-independent Core, Infrastructure, the emulator-independent automated test project, and the opt-in Android UI test project. Its implemented foundation includes repository guidance, pragmatic layered dependencies, CommunityToolkit.Mvvm, MAUI dependency injection, device-local SQLite initialization, centralized user-facing resources, strongly typed application settings, and isolated xUnit repository integration tests. Team Workspace contains local Games and Roster sections, supports adding or removing existing active Players, and supports creating a global Player with immediate TeamPlayer membership from the Team-scoped Add Players workflow. Player Workspace shows active Team memberships, and Player Manager retains bulk lifecycle actions; Game schedule management and other later softball workflows remain deferred.

## Approved Project-Wide Direction

The following project-wide architecture and planning decisions were approved while Story 1.2 was in progress. They guide future work but do not represent completed implementation or physical-device validation.

### Primary Device and Tablet-First UX

The primary deployment and UX target is a 10-inch Android tablet used for live adult slow-pitch softball scorekeeping. The preferred scoring experience is landscape-first unless later real-device testing supports a different decision. Android tablet behavior is the primary UI and interaction acceptance standard; Windows remains supported for development, debugging, secondary validation, and convenience.

Tablet UI should favor large finger-friendly touch targets, clear readability at normal viewing distance, minimal taps, one-handed operation where practical, stable control placement, touch-appropriate spacing, and reliable offline use. Desktop conventions should not drive the primary experience when they conflict with Android tablet usability.

### Current and Planned Architecture

Story 3.0 transitioned the repository from one MAUI project to pragmatic MAUI, Core, and Infrastructure project boundaries after Team and Player CRUD. Story 3.1 added the Tests boundary. XAML, MVVM, CommunityToolkit.Mvvm, dependency injection composition, centralized resources, configuration, and platform assets remain in MAUI; models are in Core; SQLite persistence is in Infrastructure. Clear dependency direction and independent testability continue to matter more than project count, and unnecessary interfaces and abstractions should be avoided.

### Planned Automated Testing

Story 3.1 added the first automated test project immediately after the architecture transition and before complex roster, lineup, statistics, and scoring-engine development. Current coverage exercises repository behavior against isolated temporary SQLite databases without launching MAUI. Future domain and application rules should remain independently testable, and scoring rules are expected to receive comprehensive table-driven coverage. UI automation remains out of scope unless a later story explicitly adds it.

### Planned Early Branding

Story 3.7 replaced the default .NET application icon for the Android deployment target. Splash-screen redesign and any further branding work remain planned. Initial branding may be simple and generated and should establish project ownership without delaying functional development.

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

### Story 0.9 — Create and Require Feature History Updates

**Status:** Complete

#### Implementation Summary

- Added root-level `FEATURE_HISTORY.md` as the canonical implementation-history record.
- Reconstructed concise, chronological implementation summaries for Stories 0.0 through 0.8 from repository state, Git history, and recorded story information.
- Documented current status, reusable components, architectural decisions, validation, warnings, limitations, deferred work, and verified commit messages.
- Updated `AGENTS.md` to require reading and updating the history for every story.
- Added feature-history requirements to story discipline, validation, definition of done, Git workflow, and documentation guidance.

#### Architecture and Design Decisions

The feature history records actual completed implementation state without duplicating full story specifications. Stable architecture belongs in architecture documentation, while repository-wide implementation instructions remain in `AGENTS.md`.

A story is not complete until its reviewed history entry is included in the same story-focused commit as the implementation.

#### Existing Components to Reuse

- Root-level `FEATURE_HISTORY.md`
- The maintenance workflow documented in `FEATURE_HISTORY.md`
- Repository-wide history requirements in `AGENTS.md`

#### Validation Completed

The complete history, Story 0.0–0.8 coverage, status information, reusable components, and `AGENTS.md` workflow requirements were reviewed. `git diff --check` passed, and only documentation files changed. No application build was run.

#### Commit Message

`docs: add canonical feature history and maintenance rules`

#### Impact on Future Stories

Read the history before implementation, reuse documented components, and update the history after validation and before every story commit.

### Story 0.10 — Update README.md

**Status:** Complete

#### Implementation Summary

- Rewrote root-level `README.md` and removed obsolete default MAUI template status text.
- Documented the project purpose, foundation status, verified technology stack, single-project folder structure, documentation, build requirements, current infrastructure, boundaries, and planned functionality.
- Clearly separated implemented foundation infrastructure from future team, player, game, lineup, scorekeeping, statistics, and beer-rule workflows.
- Identified Story 1.0 — Create Landing Page as the next planned story.

#### Architecture and Design Decisions

The README provides a concise project orientation and links to `AGENTS.md` for implementation guidance and `FEATURE_HISTORY.md` for detailed implementation state. It does not duplicate those documents or describe planned features as implemented.

#### Validation Completed

The complete README and Story 0.10 history/status updates were reviewed. `git diff --check` passed, and only `README.md` and `FEATURE_HISTORY.md` changed. No application build or launch was performed.

#### Impact on Future Stories

The repository documentation now accurately presents the completed foundation and is ready for Story 1.0 — Create Landing Page.

### Story 1.0 — Create Landing Page

**Status:** Complete

#### Implementation Summary

- Replaced the default .NET MAUI counter page with a tablet-friendly landing page displaying the application title, Teams heading, empty-state message, Create Team action, and temporary command feedback.
- Added `LandingPageViewModel`, deriving from `ObservableObject`, with a generated relay command that updates resource-backed presentation state without navigating or creating data.
- Updated `MainPage` to use `LandingPageViewModel` through constructor injection, compiled bindings, and the existing shared styles.
- Replaced the `MainPageViewModel` dependency-injection registration with `LandingPageViewModel` and removed the obsolete view model and counter behavior.
- Added landing-page resources and removed obsolete template text, accessibility descriptions, counter resources, the .NET template image, and its project-file resize entry.

#### Architecture and Design Decisions

The temporary Create Team action remains presentation-only. It does not introduce team models, repositories, services, database tables, navigation routes, or team-management pages. The existing AppShell startup and route behavior, dependency-injection flow, `ApplicationSettings`, and `AppDatabase` initialization remain unchanged.

#### Existing Components to Reuse

- `LandingPageViewModel` and its constructor-injected `MainPage` integration
- Strongly typed `AppResources` accessors and compiled XAML bindings
- Existing AppShell startup, dependency-injection, and shared-style patterns

#### Validation Completed

Windows and Android builds and application launches completed successfully during manual validation. On both platforms, the landing page displayed correctly and the Create Team placeholder feedback worked without reported warnings, layout problems, or runtime exceptions. `git diff --check` passed. No automated tests were run or added. `README.md` did not require changes.

#### Deferred Work

Team creation, team persistence, navigation to team-management pages, and other team functionality remain deferred to later stories.

#### Impact on Future Stories

Replace the temporary Create Team feedback only when a later story introduces the corresponding team workflow. Reuse the established resource, MVVM, constructor-injection, and Shell startup patterns.

### Story 1.1 — Create Team

**Status:** Complete

#### Implementation Summary

##### Team Persistence

- Added `Team` with `Id`, `Name`, and `IsActive`; IDs are SQLite auto-incremented primary keys and new Teams are active by default.
- Added Team table creation to the existing asynchronous, once-only, `SemaphoreSlim`-protected `AppDatabase` initialization flow.

##### Repository

- Added concrete `TeamRepository` operations for creating a Team, loading active Teams alphabetically, and checking active duplicate names.
- Duplicate comparison trims supplied and stored names, ignores casing, and excludes inactive Teams without reactivating them.
- No generic repository, service, manager, base repository, or unnecessary interface was introduced.

##### Create Team Workflow

- Added `CreateTeamViewModel` and `CreateTeamPage` with resource-backed required-name and duplicate-name validation.
- Save persists one Team using the trimmed name and returns to the landing page only after insertion succeeds; Cancel clears the form and returns without saving.
- All fixed runtime user-facing text uses strongly typed `AppResources` entries.

##### Landing Page

- Replaced the placeholder Create Team behavior with asynchronous Shell navigation through the shared `create-team` route.
- Added active-Team loading, stable collection binding, automatic refresh whenever the landing page appears, and empty/populated-state switching.
- Removed the obsolete placeholder status state. Team selection remains intentionally inactive.

##### Android Tablet Presentation

- Added bordered, rounded, content-sized Team cards optimized for the 10-inch Android tablet target.
- Cards share a left-aligned page edge while Team names remain centered within each card.
- Kept the Create Team button centered at a natural, touch-friendly width.
- Team record display and Team-page navigation remain deferred.

##### Dependency Injection and Navigation

- Preserved singleton lifetimes for `AppDatabase`, `TeamRepository`, `LandingPageViewModel`, and `MainPage`.
- Registered `CreateTeamViewModel` and `CreateTeamPage` as transient so each form workflow receives fresh state.
- Registered and reused the stable `create-team` Shell route.

##### Placeholder Cleanup

- Removed `Models/.gitkeep` after adding `Team`; no `.gitkeep` remains in a populated folder.

#### Architecture and Design Decisions

Team persistence remains behind the concrete repository, while ViewModels contain presentation and workflow coordination without direct SQLite access. The landing page refreshes through its existing page lifecycle after navigation returns, avoiding messaging, event aggregation, or a navigation-service abstraction.

#### Existing Components to Reuse

- `Team`, `TeamRepository`, and the initialized connection access owned by `AppDatabase`
- `CreateTeamViewModel` and `CreateTeamPage` form, validation, and transient DI pattern
- `LandingPageViewModel.Teams`, appearance-based refresh, and resource-backed compiled bindings
- Shared `AppShell.CreateTeamRoute` navigation constant

#### Validation Completed

- Windows and Android builds succeeded, and both applications launched successfully.
- Create Team navigation, required-name validation, duplicate-name validation, and Cancel returning without saving worked.
- Successful Save persisted the Team and returned to the automatically refreshed landing page.
- Saved Teams remained after restarting the application and appeared alphabetically.
- Final Android Team-card and Create Team button presentation was manually approved.
- Team cards remained non-interactive as required by Story 1.1.

#### Deferred Work

Edit Team; delete or deactivate Team; reactivation; Team record; Team-page navigation; Team year; Team logo and colors; players; games; rosters; and statistics remain deferred.

#### Impact on Future Stories

Reuse the Team persistence, validation, transient form, Shell route, and landing-page refresh patterns. Add Team-card interaction, record display, or additional Team fields only when a later story explicitly requires them.

### Story 1.2 — Edit Team

**Status:** Complete

#### Implementation Summary

- Made active Team cards tappable while retaining their existing appearance and `CollectionView.SelectionMode="None"`.
- Added MVVM-based Team selection through `LandingPageViewModel` and Shell navigation to the Edit Team workflow.
- Added `EditTeamViewModel` and `EditTeamPage` with constructor injection, compiled bindings, resource-backed validation, and Save and Cancel commands.
- Registered the Edit Team page and view model as transient so each edit workflow receives fresh state.
- Preserved the landing page's existing `OnAppearing` refresh, which reloads active Teams and alphabetically repositions a renamed Team after returning.
- Updated the existing SQLite Team row rather than inserting a replacement; only one Team card remains after a rename.

#### Repository Changes

- Added `GetActiveByIdAsync` to load only an active Team with the requested ID.
- Added `UpdateAsync` using sqlite-net update behavior for an existing Team row.
- Extended active-name duplicate checking with an overload that excludes the current Team ID.
- Preserved active-only, trimmed, case-insensitive duplicate comparison. Inactive Teams do not block Save, an unchanged name succeeds, and Create Team duplicate validation remains unchanged.
- Missing and inactive Teams are not editable.

#### Navigation and State Behavior

- Added the centralized Shell route `edit-team` and query key `teamId`.
- Landing-page navigation uses `ShellNavigationQueryParameters` and passes only the Team ID, not a mutable Team object as authoritative state.
- `EditTeamViewModel` implements `IQueryAttributable`, explicitly parses positive integer and numeric-string IDs, and reloads the Team from `TeamRepository` as the persistence source of truth.
- Editable Team-name state remains separate from the loaded model until Save. Save trims before validation and persistence; Cancel does not mutate or persist the Team.
- Busy-state handling prevents repeated command execution. Repeated page appearances do not overwrite unsaved input.
- Failed and zero-row updates do not navigate and restore the original in-memory model name.
- Missing, invalid, inactive, and nonexistent Team IDs display the resource-backed Team-not-found state and block Save.

#### Resources and Dependency Injection

- Added `EditTeamTitle` and `TeamNotFound` to the centralized resources and strongly typed accessors.
- Reused the existing Team Name label and placeholder, Save, Cancel, required-name, and duplicate-name resources. No runtime user-facing text was hardcoded.
- Registered `EditTeamViewModel` and `EditTeamPage` as transient while preserving singleton `TeamRepository` and `AppDatabase` lifetimes.

#### Reusable Components and Decisions

- Use ID-only Shell navigation and repository reloads when editing persisted entities.
- Use `IQueryAttributable` for explicit query parsing and centralized route and query-key constants.
- Use transient form pages and view models for fresh workflow state.
- Continue using page `OnAppearing` refresh instead of messaging when returning to an existing list page.
- Keep editable form state separate from the loaded persistence model and exclude the current entity during duplicate validation.

#### Documentation Updates

`AGENTS.md`, `README.md`, and this feature history were intentionally updated during Story 1.2. The updates clarify the current single-project architecture, planned pragmatic layered/Clean Architecture transition, planned automated test project, primary Android tablet focus, secondary Windows support, and planned branding work. These project-wide plans are not Story 1.2 implementation and remain future work.

#### Validation Completed

The Android build succeeded, and the application launched successfully. Selecting an active Team card opened Edit Team with the correct name. Cancel returned without saving, and saving an unchanged name succeeded without duplicate validation or inserting another Team.

Empty and whitespace-only names displayed required-name validation. Duplicate active names were rejected when entered exactly, with different casing, and with surrounding whitespace. A valid rename was trimmed, updated the existing SQLite row, left one Team card, refreshed automatically, moved to the correct alphabetical position, and persisted after the application was closed and reopened.

The 10-inch Android tablet layout was readable and usable. No clipping, overlap, touch-target issues, or runtime exceptions were observed.

Windows validation was intentionally not performed for Story 1.2. Current development and deployment validation focused on the Android tablet; Windows remains a supported secondary target and was not a completion requirement for this story.

No automated tests were run because no automated test project currently exists.

#### Known Warnings and Deferred Work

Team deactivation, deletion, reactivation, year, record, colors, logo, statistics, and workspace navigation remain deferred. Player and Game workflows also remain deferred.

Windows runtime behavior was not validated in this story. The automated test project and layered/Clean Architecture migration remain planned, as does replacement of the default icon and splash branding.

Git reported informational LF-to-CRLF conversion warnings during diff review; no unintended whole-file line-ending rewrite was present.

### Story 1.3 — Deactivate Team

**Status:** Complete

#### Implementation Summary

- Added a Deactivate Team action to the existing Edit Team page while retaining Save and Cancel as the normal editing actions.
- Positioned deactivation below the normal action group with clear spacing, tablet-friendly minimum sizing, and theme-aware red outline and text.
- Added a resource-backed confirmation that identifies the persisted Team name and explains that the Team will be hidden, its data preserved, and no permanent deletion performed.
- Added no reactivation, permanent deletion, or separate inactive-Team management page.

#### Repository and Persistence

- Added `TeamRepository.DeactivateAsync(int id)` using one parameterized SQLite update matching both Team ID and `IsActive == true`.
- The operation changes only `IsActive` to `false`, returns the affected-row count, and preserves every other Team value and the existing SQLite row.
- A successful active-Team update returns one. A missing or already inactive Team returns zero.
- No Team row is deleted or inserted, and no database schema change was required.
- Existing active-Team loading and duplicate-name queries remain unchanged and continue to exclude inactive Teams.

#### ViewModel and Confirmation Workflow

- Extended `EditTeamViewModel` with the asynchronous `DeactivateTeamConfirmationRequested` delegate and generated `RequestDeactivateTeamCommand` and `ConfirmDeactivateTeamCommand`.
- The request command supplies the persisted loaded Team name rather than potentially unsaved `TeamName` Entry text. It performs no mutation or persistence before confirmation.
- Shared `CanExecute` rules require a loaded, valid, active Team and a non-busy ViewModel. Notifications refresh command availability as loading and busy state change.
- Confirmed deactivation rechecks state, prevents repeated execution, calls `TeamRepository.DeactivateAsync`, and uses `TeamNotFound` when zero rows are affected.
- The ViewModel marks its in-memory Team inactive only after persistence succeeds and navigates back only after success.
- Direct SQLite access, dialog and navigation services, messaging, event aggregation, service location, and a Team service were not introduced.

#### Page and Resources

- Bound the page action to `RequestDeactivateTeamCommand` and relied on generated command availability before loading and while busy.
- Assigned the confirmation delegate once in the transient `EditTeamPage` constructor.
- Kept code-behind limited to the MAUI `DisplayAlert` confirmation, resource formatting with `CultureInfo.CurrentCulture`, and awaiting `ConfirmDeactivateTeamCommand` after explicit confirmation.
- Canceling confirmation leaves the Team active, keeps the page open, and preserves unsaved form state.
- Added `DeactivateTeamButton`, `DeactivateTeamConfirmationTitle`, `DeactivateTeamConfirmationMessage`, and `ConfirmDeactivateTeamButton` to `AppResources` and its strongly typed accessors.
- Reused `CancelButton` and `TeamNotFound`. The confirmation template uses `{0}` for the persisted Team name and contains all deactivation consequences. No runtime user-facing text was hardcoded.

#### Navigation, Refresh, and Duplicate Names

- Reused the existing Edit Team Shell route and `Shell.Current.GoToAsync("..")` back-navigation pattern.
- Reused `MainPage.OnAppearing`, `LandingPageViewModel.LoadTeamsAsync`, and `TeamRepository.GetActiveAsync`; no landing-page collection was manually modified.
- After deactivation, the Team disappears, remaining active Teams stay alphabetically ordered, empty-state visibility updates, and Create Team remains available.
- Active duplicate-name protection remains trimmed and case-insensitive. A new Team or another active Team may use the name of an inactive Team.
- Existing transient `EditTeamPage` and `EditTeamViewModel` lifetimes remain unchanged; no DI registration was added.

#### Files Changed

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/Data/Repositories/TeamRepository.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/ViewModels/EditTeamViewModel.cs`
- `SoftballScorekeeper/Views/EditTeamPage.xaml`
- `SoftballScorekeeper/Views/EditTeamPage.xaml.cs`

#### Validation Completed

Windows build and launch succeeded. Edit Team opened correctly, the deactivation action was clearly separated and unavailable until loading completed, and confirmation used the persisted name rather than unsaved Entry text. Canceling kept the Team active, retained the page and unsaved form state, while confirmation returned to the refreshed landing page without a stale or duplicate card. Remaining Teams stayed alphabetical and Create Team remained available.

The Android build and launch succeeded on the 10-inch tablet emulator. The deactivation action was readable, visually distinct, comfortably tappable, unavailable until loading completed, and acceptable in the tested theme presentation. No clipping, overlap, touch-target issue, or runtime exception occurred. Confirmation used the persisted name; cancel preserved the active Team and unsaved state; confirmed deactivation refreshed the landing page without stale or duplicate cards.

After the application was fully closed and relaunched, the deactivated Team remained hidden. The empty state appeared when no active Teams remained, and Create Team remained available.

Manual duplicate validation confirmed that two active Teams cannot share the same normalized name, while a new Team and an existing active Team can use a deactivated Team's name. Case and surrounding-whitespace behavior remained correct.

`git diff --check` passed. Git emitted informational LF-to-CRLF conversion warnings only. No generated build output, local SQLite database, secret, machine-specific file, or unexpected change was included. No automated tests were added or run because the planned test project does not yet exist.

#### Reusable Components and Decisions

- Use conditional soft-delete updates at the repository boundary and return affected-row counts to distinguish success from unavailable records.
- Continue ID-only navigation and repository reloads as the persistence source of truth.
- Use lightweight ViewModel-to-View coordination for view-only confirmation dialogs without adding a dialog service.
- Use persisted entity values in destructive confirmations, generated command `CanExecute` for loaded/busy protection, and resource-backed formatted messages for dynamic text.
- Reuse existing page `OnAppearing` refresh and active-only list and duplicate queries after soft deletion.

#### Known Warnings and Deferred Work

The LF-to-CRLF messages remain informational and did not indicate a build failure or implementation defect.

Team reactivation, an inactive-Team management screen, and permanent Team deletion remain deferred. Team year, record, colors, logo, statistics, and workspace navigation also remain deferred.

Player and Game management, lineups, scorekeeping, statistics, beer rules, the automated test project, layered/Clean Architecture migration, and replacement application branding and icons remain future work.

### Story 1.4 — Manage and Reactivate Inactive Teams

**Status:** Complete

#### Implementation Summary

- Added a secondary, resource-backed Manage Inactive Teams action to the landing page and a transient inactive-Team page and view model.
- The new page reloads on every appearance, shows only inactive Teams in repository-provided alphabetical order, preserves one stable observable collection, and switches between list and empty states.
- Each inactive-Team card offers only a tablet-friendly Reactivate action. No editing, permanent deletion, bulk action, or unrelated administration behavior was added.

#### Repository and Reactivation Behavior

- Added `GetInactiveAsync`, which queries only rows where `IsActive` is false and orders them by name.
- Added `ReactivateAsync(int id)` as a conditional SQLite update matching the existing Team ID and inactive state. It changes only `IsActive` to true, returns zero for a missing or already-active row, and never inserts, copies, or deletes a Team.
- Reused active-name checking with current-Team exclusion. Comparison continues to trim surrounding whitespace, ignore casing, and consider only active Teams.

#### ViewModel, Confirmation, and Conflict Behavior

- Added `InactiveTeamsViewModel` with generated request and confirmed-reactivation commands, busy-state overlap protection, collection and empty-state properties, and a resource-backed validation message.
- Reused Story 1.3's lightweight asynchronous confirmation delegate. Page code-behind formats the persisted Team name into a resource template, displays Reactivate and Cancel actions, and invokes the confirmed ViewModel command only after approval.
- Cancel performs no persistence work. Confirmation checks for an active normalized-name conflict, leaves the inactive row unchanged when blocked, conditionally reactivates the existing row, and reloads the inactive list after success.
- Reused `DuplicateTeamName`, `TeamNotFound`, and `CancelButton` where their meanings match.

#### Navigation, Resources, and Dependency Injection

- Added and registered the centralized `inactive-teams` Shell route. Landing navigation remains owned by `LandingPageViewModel`.
- Registered `InactiveTeamsViewModel` and `InactiveTeamsPage` as transient without changing existing service lifetimes.
- Added `ManageInactiveTeamsButton`, `InactiveTeamsTitle`, `NoInactiveTeamsMessage`, `ReactivateTeamButton`, `ReactivateTeamConfirmationTitle`, `ReactivateTeamConfirmationMessage`, and `ConfirmReactivateTeamButton` resources and strongly typed accessors.
- Returning to the landing page continues to rely exclusively on `MainPage.OnAppearing` and its existing active-Team reload; the inactive-Team workflow does not modify the landing collection.

#### Files Added

- `SoftballScorekeeper/ViewModels/InactiveTeamsViewModel.cs`
- `SoftballScorekeeper/Views/InactiveTeamsPage.xaml`
- `SoftballScorekeeper/Views/InactiveTeamsPage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/Data/Repositories/TeamRepository.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/ViewModels/LandingPageViewModel.cs`
- `SoftballScorekeeper/Views/MainPage.xaml`

#### Validation Status, Warnings, and Deferred Work

The implementation and complete Story 1.4 diff received static review, and `git diff --check` passed. A command-line Windows framework build succeeded with zero warnings and zero errors. User validation then confirmed that the Windows build passed with zero warnings and errors, the application launched, existing Create, Edit, and Deactivate workflows remained functional, and all inactive-Team navigation, ordering, empty-state, confirmation, cancel, reactivation, landing refresh, restart persistence, and active-name conflict behavior passed without runtime exceptions.

User validation also confirmed that the Android build and launch passed and that all Story 1.4 workflows worked on the 10-inch tablet. The tablet presentation was readable with no clipping, overlap, unexpected scrolling, binding errors, or touch-target problems. No application-code correction was required after validation. No automated test project exists, so no automated tests were added or run. Git continued to report only the known informational LF-to-CRLF conversion messages during diff review.

Create and Edit continue to allow reuse of an inactive Team name, consistent with the behavior established before Story 1.4; the user accepted this as outside Story 1.4 and deferred any change to the next story. Permanent deletion, inactive-Team editing, bulk actions, Team merging or duplication, additional Team fields and statistics, Team workspace behavior, Player and Game workflows, generic administration or soft-delete frameworks, the automated test project, architecture migration, and branding remain deferred.

### Story 1.5 — Prevent Reuse of Inactive Team Names

**Status:** Complete

#### Implementation Summary

- Added focused `TeamRepository.InactiveNameExistsAsync` overloads that load inactive Teams only, trim surrounding whitespace from supplied and persisted names, compare with `StringComparison.OrdinalIgnoreCase`, and support current-Team ID exclusion.
- Create Team now checks inactive normalized-name matches after required-name and active-duplicate validation. A conflict displays resource-backed guidance directing the user to Manage Inactive Teams, remains on the form, and performs no insert.
- Edit Team now performs the same inactive-name check after active-duplicate validation and excludes the loaded Team ID. A conflict occurs before editable or persisted model state is mutated, remains on the form, and performs no update.
- Existing active duplicate validation and Story 1.4 reactivation behavior remain intact. Create and Edit do not automatically navigate, reactivate, merge, rename, or delete a Team when blocked.

#### Files Changed

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/Data/Repositories/TeamRepository.cs`
- `SoftballScorekeeper/ViewModels/CreateTeamViewModel.cs`
- `SoftballScorekeeper/ViewModels/EditTeamViewModel.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`

#### Architecture and Design Decisions

- Preserved repository ownership of persisted Team-name lookup and kept active and inactive checks as separate public operations.
- Reused the established trimmed, case-insensitive in-memory comparison behavior without introducing a generic identity or validation abstraction.
- Added only the `InactiveTeamNameExists` resource and strongly typed accessor for runtime guidance.
- No model, page, route, service, interface, database table, migration, dependency-injection registration, or architectural-layer change was required.
- No automatic reactivation or navigation to Manage Inactive Teams was introduced.

#### Validation Completed

- `git diff --check` passed.
- The Windows framework build succeeded with 19 existing warnings and zero errors. The warnings remained in the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; no warning originated from Story 1.5 changes.
- Android build, launch, and runtime validation passed through the user's configured Visual Studio environment.
- Create and Edit correctly blocked exact, casing-varied, and surrounding-whitespace-varied inactive-name conflicts, displayed the resource-backed guidance, remained on their forms, and performed no prohibited insert or update.
- Active-duplicate regression tests, unique Create and Edit, unchanged-name Edit without a separate inactive conflict, Manage Inactive Teams reactivation, and persistence after restart passed.
- Story 1.4 reactivation continued updating the existing inactive row, and the reactivated Team appeared exactly once.
- The Android 10-inch tablet layout remained readable and usable, and no runtime exceptions occurred.
- Windows runtime validation was not performed; Windows compile validation passed.

#### Limitations and Deferred Work

Automatic navigation to Manage Inactive Teams, automatic reactivation, Team merging, permanent deletion, and editing inactive Teams remain outside scope. No database-level unique constraint or automated test project was added. The layered/Clean Architecture migration remains deferred.

#### Intended Commit Message

`Prevent inactive Team name reuse`

### Story 2.0 — Player List and Multi-Selection Foundation

**Status:** Complete

#### Implementation Summary

- Added the persisted `Player` model with a SQLite auto-increment primary key, non-null `FirstName`, optional `LastName`, and active-by-default soft-delete state. Player table creation now uses the existing guarded `AppDatabase` initialization flow alongside Team initialization.
- Added concrete `PlayerRepository` active and inactive queries. Both retain repository ownership of SQLite access and return predictably ordered results by first name and then last name.
- Added `PlayerSelectionItem` as a reusable ViewModel-layer wrapper around `Player`. Its observable checkbox state is transient presentation state and is intentionally absent from the persisted Player model; its display name handles missing or blank last names without unwanted spacing.
- Added `PlayerListViewModel` with constructor-injected repository access, one stable observable collection, active-Player loading, overlapping-load protection, coherent list and empty-state visibility, and resource-backed placeholder commands for Add, Edit, Deactivate Selected, and Manage Inactive Players.
- Added the Player List page with appearance-based loading, compiled bindings, left-aligned names, right-aligned two-way checkboxes, light gray dividers, comfortable row touch targets, and an empty state.
- Added a resource-backed Landing Page Players entry and registered the centralized Player List Shell route.

#### Files Changed

Added:

- `SoftballScorekeeper/Models/Player.cs`
- `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs`
- `SoftballScorekeeper/ViewModels/PlayerSelectionItem.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/Views/PlayerListPage.xaml`
- `SoftballScorekeeper/Views/PlayerListPage.xaml.cs`

Modified:

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/Data/AppDatabase.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/ViewModels/LandingPageViewModel.cs`
- `SoftballScorekeeper/Views/MainPage.xaml`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`

#### Architecture and Design Decisions

- Preserved the existing concrete-repository, constructor-injection, Shell-route, transient-page/ViewModel, and resource-backed text patterns without adding interfaces, services, managers, or another architectural layer.
- Registered `PlayerRepository` as a singleton, consistent with `TeamRepository`, and registered `PlayerListViewModel` and `PlayerListPage` as transient.
- Kept checkbox selection in the reusable Player-specific ViewModel wrapper rather than adding presentation or persistence state to `Player`; no speculative generic selectable-item framework was introduced.
- Reused the historical ViewModel-owned status-message convention for temporary Player actions. The placeholders perform no persistence and do not navigate to nonexistent CRUD workflows.
- Sized all four action buttons from shared page-local values at 210 by 44 pixels with compact padding. The centered, content-sized two-column grid preserves list space while keeping the longest label on one line and retaining tablet-appropriate touch height.

#### Validation Completed

- Android build, launch, navigation, empty-state presentation, placeholder feedback, and button touch interaction passed on the 10-inch tablet emulator without runtime exceptions, clipping, or unintended horizontal scrolling.
- Android visual review approved the heading, improved Player-list area, and compact centered two-column action layout. `Manage Inactive Players` remained on one line.
- The populated Player row template received static review because Player creation is outside Story 2.0. The flexible name column, right-aligned two-way checkbox, light gray divider, touch height, blank-last-name formatting, and non-persisted selection design were confirmed; no seed or debug data was added.
- The Windows-target build succeeded with 24 warnings and zero errors. Warnings remained in the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; the new field-based observable properties follow the repository's established `MVVMTK0045` pattern.
- `git diff --check` passed, and the complete Story 2.0 diff was reviewed for binding, navigation, dependency-injection, persistence, resource, scope, and Git hygiene.

#### Limitations and Deferred Work

Player creation, editing, deactivation, reactivation, bulk persistence, Team membership, `TeamPlayer`, jersey numbers, lineups, games, and statistics remain deferred. Populated-row runtime validation remains deferred until a Player creation workflow exists. The proposed application-wide fast-access bar and menu-based navigation design remains deferred to a separate architecture and technical discussion. No automated tests were added because the planned test project does not yet exist.

#### Intended Commit Message

`Implement Player list foundation`

### Story 2.1 — Create Player

**Status:** Complete

#### Implementation Summary

- Added the transient Create Player page and ViewModel with First Name, optional Last Name, resource-backed validation, Save, and Cancel behavior following the established Create Team form pattern.
- Replaced only the Player List Add Player placeholder with Shell navigation through the centralized `create-player` route; the other Player-management actions remain placeholders.
- Save trims both names, requires a nonblank First Name, persists a blank Last Name as `null` under the existing nullable model convention, and inserts one active Player through `PlayerRepository`.
- Duplicate Player names remain intentionally allowed. Failed validation and Cancel perform no repository write, and successful Save returns to the existing Player List `OnAppearing` refresh flow.
- Corrected active Player ordering after Android validation exposed SQLite's binary text collation behavior. The repository now orders active Players case-insensitively by First Name and then Last Name.

#### Files Changed

Added:

- `SoftballScorekeeper/ViewModels/CreatePlayerViewModel.cs`
- `SoftballScorekeeper/Views/CreatePlayerPage.xaml`
- `SoftballScorekeeper/Views/CreatePlayerPage.xaml.cs`

Modified:

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`

#### Architecture and Design Decisions

- Preserved repository-owned SQLite persistence by adding only `PlayerRepository.CreateAsync`; the Create ViewModel contains no direct database access.
- Kept active Player ordering repository-owned. Active rows are filtered in SQLite and ordered in memory with `StringComparer.OrdinalIgnoreCase`, using First Name as the primary field and Last Name as the secondary field.
- Null Last Names use `string.Empty` only as the ordering comparison key; persisted data is not modified. Blank Last Name input is stored as `null`, consistent with the existing optional `Player.LastName` model property.
- Player names are not unique, and no duplicate-name query, warning, or constraint was introduced.
- Registered `CreatePlayerViewModel` and `CreatePlayerPage` as transient and reused Shell back-navigation without adding a navigation service.
- Retained Player List appearance-based refresh, minimal constructor-only page code-behind, compiled bindings, and centralized runtime resources.
- Used `ObservableObject.SetProperty` for the new form state and generated RelayCommands, avoiding new `MVVMTK0045` warnings without adding a base ViewModel, service, generic CRUD framework, form framework, or architectural layer.

#### Validation Completed

- `git diff --check` passed.
- The Windows target build succeeded with 24 warnings and zero errors. Warnings remained in the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; Story 2.1 introduced no remaining new warning.
- The command-line Android build could not locate the Android SDK and reported `XA5300`; Android build, launch, and runtime validation were subsequently completed through the configured Visual Studio environment.
- Android validation passed for Add Player navigation, blank and whitespace-only First Name validation, no insert on invalid input, name trimming, optional blank Last Name, duplicate names, single active-Player insertion, Save and Cancel navigation, list refresh, empty-state transition, restart persistence, and clean transient form state.
- Existing checkbox selection behavior and Android tablet layout remained functional without crashes, binding failures, clipping, overlap, or runtime exceptions.
- The initial case-sensitive ordering defect was corrected and retested successfully. Active Players appeared case-insensitively by First Name and then Last Name, including safe handling of null and blank Last Names.

#### Limitations and Deferred Work

Player editing, deactivation, reactivation, Team membership, `TeamPlayer`, jersey numbers, duplicate-name warnings, bulk operations, games, lineups, and statistics remain deferred. Automated tests, the layered/Clean Architecture migration, and application branding also remain future work.

#### Intended Commit Message

`Implement Player creation`

### Story 2.2 — Edit Player

**Status:** Complete

#### Implementation Summary

- Added transient Edit Player page and ViewModel support for editing one existing active Player's First Name and optional Last Name.
- Added a dedicated Edit button to each Player row. This keeps single-Player editing distinct from the existing checkboxes, which remain transient selection state for future bulk workflows.
- Player List navigation passes only the selected Player ID through the centralized `edit-player` Shell route. The edit ViewModel parses the ID and reloads the active Player through `PlayerRepository` as the persistence source of truth.
- Save trims both names, requires a nonblank First Name, normalizes a blank Last Name to `null`, permits duplicate names, and conditionally updates exactly one existing active row. Cancel performs no persistence work.
- Returning after Save or Cancel reuses the Player List's existing `OnAppearing` reload, rebuilding selection wrappers and preserving repository-owned case-insensitive First Name then Last Name ordering.

#### Files Added

- `SoftballScorekeeper/ViewModels/EditPlayerViewModel.cs`
- `SoftballScorekeeper/Views/EditPlayerPage.xaml`
- `SoftballScorekeeper/Views/EditPlayerPage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/Views/PlayerListPage.xaml`

#### Architecture and Design Decisions

- Added only `GetActiveByIdAsync` and a conditional active-row `UpdateAsync` to the concrete repository. The update changes only First Name and Last Name, preserves identity and active state, returns the affected-row count, and cannot insert a Player.
- Kept persisted identity/state separate from editable form values. Validation occurs before creating a safe update copy, so validation or persistence failure cannot leave the loaded model partially changed.
- Guarded repeated loading, Save, and Cancel execution. Repeated appearances do not overwrite edits after a Player has loaded, while missing, malformed, inactive, and nonexistent IDs keep Save disabled and show the resource-backed Player-not-found message.
- Registered `EditPlayerViewModel` and `EditPlayerPage` as transient; `PlayerRepository` remains singleton. No service, navigation abstraction, generic editing framework, or direct SQLite access in presentation code was added.
- Added `EditPlayerTitle` and `PlayerNotFound`; reused the established name labels, placeholders, required-name message, and Save and Cancel resources.

#### Validation Completed

- Reviewed the complete Story 2.2 implementation and diff for ID-only navigation, active-only loading and update behavior, editable-state separation, validation, null normalization, duplicate-name allowance, list refresh, ordering preservation, bindings, resources, dependency-injection lifetimes, and excluded scope.
- `git diff --check` passed. Informational LF-to-CRLF conversion messages remain present.
- The Windows target build succeeded with 25 warnings and zero errors. Warning categories were the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; the per-row explicit-source command binding adds one `XC0025` occurrence consistent with existing collection-row command bindings.
- The command-line Android build was attempted but could not locate the Android SDK and failed with `XA5300`. Android and Windows runtime validation were not performed.

#### Limitations and Deferred Work

Android build and tablet runtime validation remain required in the user's configured environment. Player deactivation, reactivation, bulk persistence, Team membership, jersey numbers, lineups, games, statistics, automated tests, architecture migration, and branding remain deferred.

#### Intended Commit Message

`Implement Player editing`

### Story 2.3 — Deactivate Selected Players

**Status:** Complete

#### Implementation Summary

- Made the existing Player List checkboxes the sole source of bulk selection and added live selected-count state and display.
- Replaced the Deactivate Selected placeholder with a guarded confirmation and persistence workflow. The action is enabled only when at least one Player is selected and disabled during loading, persistence, or an active confirmation request.
- Added singular and plural resource-backed confirmation messages containing the selected count, preservation guidance, and future-reactivation guidance. Cancel performs no repository call and preserves all selections.
- Confirmed deactivation stays on the Player List, reloads active Players from the repository, preserves case-insensitive ordering, and shows the existing empty state when no active Players remain.

#### Files Added

None.

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs`
- `SoftballScorekeeper/ViewModels/PlayerSelectionItem.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/Views/PlayerListPage.xaml`
- `SoftballScorekeeper/Views/PlayerListPage.xaml.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`

#### Repository and Selection Behavior

- Added focused `PlayerRepository.DeactivateAsync`, which safely returns zero for null, empty, or invalid ID collections and performs one conditional SQLite update for distinct valid IDs. It changes only `IsActive` from true to false, ignores missing or already-inactive rows, returns the affected-row count, and performs no insert or delete.
- Kept selection outside the persisted Player model. `PlayerSelectionItem` now accepts an optional focused selection callback, allowing `PlayerListViewModel` to maintain `SelectedPlayerCount`, `HasSelectedPlayers`, and command availability without generic selection infrastructure or event subscriptions that require cleanup.
- The dedicated per-row Edit action remains independent of checkbox state, and Add Player behavior remains unchanged.

#### Confirmation, Busy State, and Partial Updates

- Reused the Team workflow's lightweight ViewModel-to-page confirmation delegate and request/confirmed-command pattern. Page code-behind only selects the singular or plural resource, formats the count, displays the native alert, and invokes the confirmed command.
- The ViewModel snapshots distinct selected active IDs before confirmation. Cancel leaves the snapshot unapplied and preserves visible selection; loading, confirmation, and persistence guards prevent overlapping repository calls.
- Full success rebuilds the active list with fresh unselected wrappers. If fewer rows are affected, the list still reloads from repository truth, a resource-backed partial-result message is shown, and requested Players that remain active are reselected.
- Reload continues to use repository-owned case-insensitive First Name then Last Name ordering and recomputes the existing list and empty-state properties.

#### Resources

- Added `SelectedPlayerCountText`, `DeactivatePlayersConfirmationTitle`, `DeactivatePlayerConfirmationMessage`, `DeactivatePlayersConfirmationMessage`, `ConfirmDeactivatePlayersButton`, and `PlayerDeactivationPartialFailureMessage`.
- Reused `DeactivateSelectedPlayersButton`, `CancelButton`, and `NoPlayersMessage`.

#### Validation Completed

- Reviewed the complete Story 2.3 diff for selection tracking, command availability, confirmation cancellation, conditional active-only persistence, partial-update recovery, repository reload, empty state, resource usage, and excluded scope.
- `git diff --check` passed. Informational LF-to-CRLF conversion notices remain present.
- The Windows target build succeeded with 24 warnings and zero errors. Warning categories remain the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; Story 2.3 introduced no new warning category.
- The command-line Android build was attempted but could not locate the Android SDK and failed with `XA5300`. Android and Windows runtime validation were not performed.

#### Limitations and Deferred Work

Android build and 10-inch tablet runtime validation remain pending in the user's configured environment. Player reactivation, inactive-Player management, permanent deletion, Team membership, jersey numbers, lineups, games, undo, statistics, automated tests, architecture migration, and branding remain deferred.

#### Intended Commit Message

`Deactivate selected Players`

### Story 2.4 — Manage and Reactivate Inactive Players

**Status:** Complete

#### Implementation Summary

- Replaced the Manage Inactive Players placeholder with Shell navigation to a transient inactive-Player page that loads on every appearance.
- Added an inactive-only, case-insensitively ordered Player list with presentation-layer checkboxes, live selected count, empty state, and a tablet-sized Reactivate Selected action.
- Added count-aware singular and plural confirmation before bulk reactivation. Cancel performs no repository call and preserves selection; confirmed operations remain on and refresh the inactive-Player page.
- Returning to the active Player List relies exclusively on its existing `OnAppearing` repository reload, so reactivated Players appear once in active case-insensitive order without cross-page collection mutation or messaging.
- Corrected inactive-management completion so reactivating the final inactive Player automatically returns to Player Manager, while the inactive page remains open whenever inactive Players remain. The same final-item behavior now returns from inactive Teams to Team Manager for workflow consistency.
- Made Player Manager's Add, per-row Edit, Deactivate Selected, and Manage Inactive commands participate explicitly in loading and confirmation-state `CanExecute` notifications, restoring immediate interaction after returning from inactive management without requiring a checkbox change.

#### Files Added

- `SoftballScorekeeper/ViewModels/InactivePlayersViewModel.cs`
- `SoftballScorekeeper/Views/InactivePlayersPage.xaml`
- `SoftballScorekeeper/Views/InactivePlayersPage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/InactiveTeamsViewModel.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`

#### Navigation, Selection, and Confirmation Behavior

- Added and registered the centralized `inactive-players` Shell route. `PlayerListViewModel` owns navigation, and `InactivePlayersViewModel` and `InactivePlayersPage` are transient while `PlayerRepository` remains singleton.
- Reused `PlayerSelectionItem` and Story 2.3's focused callback pattern, keeping selection outside `Player` and SQLite. Selected count and command availability update immediately, and loading, confirmation, and persistence guards prevent overlapping work.
- Reused the established ViewModel confirmation delegate and request/confirmed-command pattern. Page code-behind only selects and formats singular or plural resources, displays the native confirmation, and invokes the confirmed command.

#### Repository, Ordering, and Partial Updates

- Added focused `PlayerRepository.ReactivateAsync`, which returns zero for null, empty, or invalid ID collections and performs one conditional update for distinct valid IDs. It changes only `IsActive` from false to true, ignores missing or already-active rows, uses IDs rather than names, returns the affected-row count, and performs no insert or delete.
- Updated the inactive query to match active Player ordering: case-insensitive First Name followed by case-insensitive Last Name, with null Last Names using an empty comparison key without changing persisted data.
- Full success rebuilds the inactive list with no selections. If fewer rows are affected, repository truth is still reloaded, a resource-backed partial-result message is shown, and requested Players that remain inactive are reselected.
- Duplicate Player names remain allowed and do not participate in loading, identity, confirmation, or reactivation.
- Final-item navigation occurs only after the repository reload confirms that the inactive collection is empty. Active manager pages continue to refresh through their existing `OnAppearing` flows; no messaging or manual cross-page collection synchronization was added.

#### Resources

- Added `InactivePlayersTitle`, `NoInactivePlayersMessage`, `ReactivateSelectedPlayersButton`, `ReactivatePlayersConfirmationTitle`, `ReactivatePlayerConfirmationMessage`, `ReactivatePlayersConfirmationMessage`, `ConfirmReactivatePlayersButton`, and `PlayerReactivationPartialFailureMessage`.
- Reused `SelectedPlayerCountText` and `CancelButton`.

#### Validation Completed

- Reviewed the complete Story 2.4 implementation and diff for navigation, inactive-only loading, ordering, selection, confirmation cancellation, conditional ID-only persistence, partial-update recovery, active-list refresh behavior, resources, dependency injection, and excluded scope.
- Statically reviewed the Android-reported workflow corrections: final-item Player and Team navigation is gated by post-reload empty state, inactive pages remain open for nonempty results and cancellation, and all Player Manager commands now receive load and confirmation-state `CanExecute` invalidation.
- `git diff --check` passed. Informational LF-to-CRLF conversion notices remain present.
- The Windows target build succeeded with 24 warnings and zero errors. Warning categories remain the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; Story 2.4 introduced no new warning category.
- The command-line Android build was attempted but could not locate the Android SDK and failed with `XA5300`.
- Manual Android runtime validation passed for reactivating some but not all inactive Players while remaining on the inactive page, reactivating the final inactive Player with automatic return to Player Manager, immediate Player Manager interaction after returning, and reactivated Players appearing exactly once in correct order.
- Manual Android runtime validation also passed for reactivating some but not all inactive Teams while remaining on the inactive page, reactivating the final inactive Team with automatic return to Team Manager, and Team Manager refreshing and remaining immediately interactive. No runtime exceptions occurred during the reported Player or Team workflows.

#### Limitations and Deferred Work

The command-line Android SDK limitation remains, and restart persistence was not included in the reported Android retest results. Editing inactive Players, permanent deletion, duplicate-name checks, Player merging, Team membership, jersey numbers, lineups, games, undo, statistics, automated tests, architecture migration, and branding remain deferred.

#### Intended Commit Message

`Reactivate selected Players`

### Story 3.0 — Layered Solution Restructure

**Status:** Complete

#### Implementation Summary

- Reorganized the single-project solution into the `SoftballScorekeeper` .NET MAUI project, the platform-independent `SoftballScorekeeper.Core` class library, and the `SoftballScorekeeper.Infrastructure` class library without introducing new application functionality.
- Moved `Team` and `Player` into Core and moved `AppDatabase`, `TeamRepository`, and `PlayerRepository` into Infrastructure while preserving existing behavior.
- Kept Views, ViewModels, resources, Shell, configuration, dependency-injection composition, and platform assets in the MAUI project.

#### Files Added

- `SoftballScorekeeper.Core/SoftballScorekeeper.Core.csproj`
- `SoftballScorekeeper.Core/Models/Team.cs`
- `SoftballScorekeeper.Core/Models/Player.cs`
- `SoftballScorekeeper.Infrastructure/SoftballScorekeeper.Infrastructure.csproj`
- `SoftballScorekeeper.Infrastructure/Data/AppDatabase.cs`
- `SoftballScorekeeper.Infrastructure/Data/Repositories/TeamRepository.cs`
- `SoftballScorekeeper.Infrastructure/Data/Repositories/PlayerRepository.cs`

#### Files Moved or Removed

- Moved, rather than functionally deleted, `SoftballScorekeeper/Models/Team.cs` and `SoftballScorekeeper/Models/Player.cs` into Core.
- Moved, rather than functionally deleted, `SoftballScorekeeper/Data/AppDatabase.cs`, `SoftballScorekeeper/Data/Repositories/TeamRepository.cs`, and `SoftballScorekeeper/Data/Repositories/PlayerRepository.cs` into Infrastructure.
- Removed the obsolete source copies and empty Data and Models folder declarations from the MAUI project.

#### Files Modified

- `AGENTS.md`
- `README.md`
- `FEATURE_HISTORY.md`
- `SoftballScorekeeper.sln`
- `SoftballScorekeeper/SoftballScorekeeper.csproj`
- `SoftballScorekeeper/App.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/ViewModels/CreatePlayerViewModel.cs`
- `SoftballScorekeeper/ViewModels/CreateTeamViewModel.cs`
- `SoftballScorekeeper/ViewModels/EditPlayerViewModel.cs`
- `SoftballScorekeeper/ViewModels/EditTeamViewModel.cs`
- `SoftballScorekeeper/ViewModels/InactivePlayersViewModel.cs`
- `SoftballScorekeeper/ViewModels/InactiveTeamsViewModel.cs`
- `SoftballScorekeeper/ViewModels/LandingPageViewModel.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/ViewModels/PlayerSelectionItem.cs`
- `SoftballScorekeeper/Views/InactiveTeamsPage.xaml`
- `SoftballScorekeeper/Views/InactiveTeamsPage.xaml.cs`
- `SoftballScorekeeper/Views/MainPage.xaml`

#### Architecture and Design Decisions

- Established the final dependency graph:

  ```text
  SoftballScorekeeper
  ├── SoftballScorekeeper.Core
  └── SoftballScorekeeper.Infrastructure
      └── SoftballScorekeeper.Core
  ```

- Core references no solution project, Infrastructure references only Core, and MAUI references Core and Infrastructure; no circular references exist.
- Changed model namespaces from `SoftballScorekeeper.Models` to `SoftballScorekeeper.Core.Models`, database namespaces from `SoftballScorekeeper.Data` to `SoftballScorekeeper.Infrastructure.Data`, and repository namespaces from `SoftballScorekeeper.Data.Repositories` to `SoftballScorekeeper.Infrastructure.Data.Repositories`. Affected XAML model namespace declarations identify the Core assembly explicitly.
- Kept `ApplicationSettings` and the sole composition root in MAUI. `MauiProgram` continues combining `FileSystem.AppDataDirectory` with `ApplicationSettings.DatabaseFileName` and passes the complete path explicitly to `AppDatabase`; Infrastructure does not reference MAUI, and existing service lifetimes remain unchanged.
- Chose lightweight project separation appropriate to the current application size. The existing sqlite-net attributes remain on Core models to preserve the schema and avoid duplicate persistence entities, mapping code, and premature abstractions.
- Added no service locator, assembly scanning, alternate container, speculative repository interfaces, repository redesign, Application project, test project, or Shared, Contracts, Services, Common, or Utilities project. Views and ViewModels were not reorganized into feature folders.
- Preserved `SoftballScorekeeper.db3`, its existing application-data location, model class names, properties, sqlite-net attributes, `Team` and `Player` table names, initialization order, repository behavior, and existing service lifetimes. No migration, database recreation, deletion, or schema change was added.

#### Validation Completed

- `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0` succeeded with 24 warnings and zero errors. The existing warning categories were `CS8602`, `MVVMTK0045`, and `XC0025`.
- `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-android` remained blocked by `XA5300` because the command-line environment could not locate the Android SDK; this is an environmental limitation rather than a confirmed code failure.
- Manual Android runtime validation through Visual Studio passed using the existing installed database. Existing Teams and Players loaded with no reset, migration, data loss, startup exception, or runtime exception.
- Manual Team validation passed for creation, editing, soft-deactivation, inactive management, reactivation, final-item automatic navigation, inactive-name validation, list refresh, reactivated-item display, and page interactivity.
- Manual Player validation passed for creation, editing, bulk soft-deactivation, inactive management, partial reactivation remaining on the inactive page, final-item reactivation returning to Player Manager, ordering, list refresh, reactivated-item display, and page interactivity.
- General Android regression validation passed for Team and Player navigation, back navigation, confirmation dialogs, validation messages, empty states, resource-backed runtime text, resource-key availability, and page interactivity.
- `git diff --check` passed. Static review confirmed exactly three project files, the required dependency graph with no cycles, no stale namespaces or duplicate moved sources, SQLite implementation only in Infrastructure, presentation and platform assets remaining in MAUI, unchanged Shell route values, no migration or schema changes, and only intended Story 3.0 changes.

#### Limitations and Deferred Work

Android command-line compilation remains blocked by local Android SDK discovery and `XA5300`. Automated tests remain deferred to Story 3.1. View and ViewModel feature-folder organization, TeamPlayer, Team rosters, games, lineups, scoring, statistics, Beer Rules, and additional architecture layers remain deferred.

#### Intended Commit Message

`Restructure solution into layered architecture`

### Story 3.1 — Automated Test Foundation

**Status:** Complete

#### Implementation Summary

- Added `SoftballScorekeeper.Tests`, the solution's first and only automated test project, targeting .NET 9 with xUnit. It is a solution member, references Core and Infrastructure only, does not reference the MAUI project, and no production project references it.
- Added exact test dependencies `Microsoft.NET.Test.Sdk` 17.8.0, `xunit` 2.5.3, and `xunit.runner.visualstudio` 2.5.3. The Visual Studio runner uses `PrivateAssets="all"` and includes runtime, build, native, content files, analyzers, and build-transitive assets. No mocking, coverage, or UI-testing package was added.
- Added a reusable, file-backed temporary SQLite fixture that creates one GUID-named directory beneath `Path.GetTempPath()`, stores `repository-tests.db3` within it, and initializes the real `AppDatabase`, `TeamRepository`, and `PlayerRepository`. Tests never use the real application database, `FileSystem.AppDataDirectory`, shared static database state, or network access.
- Made the fixture asynchronously and idempotently disposable. Cleanup closes the SQLite connection first, removes the database plus `-journal`, `-wal`, and `-shm` sidecars, removes any remaining files inside the fixture-owned directory, and then removes that directory.
- Added the narrowly scoped public `AppDatabase.CloseAsync()` lifecycle method so tests can release SQLite file handles before deletion. It delegates to the existing connection's asynchronous close operation and does not change the database path, schema, flags, initialization, repository behavior, dependency injection, startup, or UI behavior.

#### Repository Test Coverage

- Added two foundation tests proving that separate fixtures use distinct database paths and that disposing a fixture removes its owned directory and database artifacts.
- Added 21 `TeamRepository` tests covering creation defaults and generated IDs, active and inactive loading and filtering, the repository's existing SQLite name ordering, normalized duplicate-name checks with active/inactive and current-Team exclusion behavior, editing, deactivation, reactivation, invalid and missing IDs, already-correct states, affected-row counts, and row preservation.
- Added 27 `PlayerRepository` tests covering creation defaults and generated IDs, nullable last names, duplicate names, active and inactive loading and filtering, case-insensitive first-name/last-name ordering, active-by-ID loading, editing, deactivation, reactivation, invalid, missing, and duplicate IDs, empty and null input, already-correct states, affected-row counts, and row preservation. The hardening review strengthened selected lifecycle assertions to verify every selected Player's first and last name.
- The 50 tests use behavior-focused names, arrange independent fixtures, assert public repository behavior and persisted state, and pass under both normal parallel execution and forced sequential execution. They use no raw SQL, shared base fixture, mocks, speculative repository interfaces, or production-only implementation shortcuts.

#### Validation Completed

- Focused runs passed: foundation 2/2 (75 ms), Team repository 21/21 (468 ms), and Player repository 27/27 (705 ms), with zero failures or skips.
- Two complete test-project runs each passed all 50 tests with zero failures and zero skips; the final recorded duration was 907 ms. A separate forced-sequential run also passed all 50 tests with zero failures and zero skips in approximately one second, demonstrating isolation independent of execution order.
- The test project build succeeded with zero warnings and zero errors.
- A plain root `dotnet test` stalled silently during implicit restore and was safely stopped. Explicit restore of the test project with `--disable-parallel` then succeeded with all projects up to date. The root workaround `dotnet test --no-restore --maxcpucount:1 -nodeReuse:false` built and evaluated the solution and all 50 tests passed, but the overall command exited with failure because the MAUI Android target encountered existing `XA5300` SDK discovery. This was an environment/toolchain failure, not a test or source-code failure.
- `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false` succeeded incrementally with zero warnings and zero errors. Windows runtime validation was not performed.
- Android runtime or emulator validation was not performed because this story contains no UI behavior. Android was evaluated indirectly by the root test command and encountered `XA5300`; no Android build or runtime success is claimed, and the existing environment was not repaired as part of this story.
- Repeated validation left no test databases, SQLite sidecars, fixture directories, result directories, coverage artifacts, or temporary run-settings files. `git diff --check` passed with only informational LF-to-CRLF notices.

#### Documentation and Guidance

- Updated `README.md` with the xUnit project, Core and Infrastructure references, isolated temporary file-backed SQLite strategy, cleanup guarantees, the root `dotnet test` command, and explicit current test-suite limitations without publishing brittle test counts.
- Updated `AGENTS.md` to describe the four-project solution and require focused and full-suite validation, assertions against persisted state, isolated and repeatable repository databases, minimal test abstractions, honest reporting, and a clear distinction between non-UI automated tests and platform runtime validation.

#### Files Added

- `SoftballScorekeeper.Tests/SoftballScorekeeper.Tests.csproj`
- `SoftballScorekeeper.Tests/TemporaryTestDatabase.cs`
- `SoftballScorekeeper.Tests/TemporaryTestDatabaseTests.cs`
- `SoftballScorekeeper.Tests/TeamRepositoryTests.cs`
- `SoftballScorekeeper.Tests/PlayerRepositoryTests.cs`

#### Files Modified

- `AGENTS.md`
- `README.md`
- `FEATURE_HISTORY.md`
- `SoftballScorekeeper.Infrastructure/Data/AppDatabase.cs`
- `SoftballScorekeeper.sln`

#### Limitations and Deferred Work

ViewModel tests, MAUI UI automation, Android instrumentation, Windows UI automation, coverage thresholds and enforcement, continuous integration, and a general-purpose ViewModel test suite remain deferred. TeamPlayer and roster behavior, games, lineups, scoring, statistics, Beer Rule behavior, performance testing, additional test-project splits by layer, and a mocking framework remain deferred unless a later story provides a concrete need and justification.

#### Intended Commit Message

`Add automated test foundation`

### Story 3.2 — Team Roster Foundation

**Status:** Implementation complete; manual runtime validation pending

#### Implementation Summary

- Added the Core `TeamPlayer` persisted model with only auto-incrementing `Id`, `TeamId`, `PlayerId`, and active-by-default `IsActive`. No presentation selection state, jersey number, lineup, position, season, game, statistics, or timestamps were added.
- Added `TeamPlayer` table creation to the existing guarded `AppDatabase.InitializeAsync` sequence after Team and Player creation. Existing connection flags, database location and filename, semaphore protection, connection lifetime, and existing tables remain unchanged; existing databases receive the new table without recreation or data deletion.
- Added the concrete Infrastructure `TeamPlayerRepository` with the single read operation required by this story. It safely rejects invalid or missing Teams, requires an active Team, joins active TeamPlayer relationships to active Players, excludes other Teams, returns distinct Players by ID, and sorts case-insensitively by First Name and then Last Name with null Last Names treated as empty. It performs no writes and exposes no future roster-management methods.

#### Team Workspace and Navigation

- Made each bordered landing-page Team control a compact, content-sized, left-aligned, tablet-sized tap target that opens Team Workspace. Each border wraps its Team name with horizontal padding instead of filling the row. `LandingPageViewModel` owns the navigation command, preserves existing Team loading and ordering, and passes only the Team ID through `ShellNavigationQueryParameters`; the controls contain no nested Open Team or Edit Team actions.
- Registered the centralized `team-workspace` Shell route using the existing `teamId` query key. `TeamWorkspacePage` and `TeamWorkspaceViewModel` are transient, while `TeamPlayerRepository` is singleton alongside the existing repositories.
- Added `TeamWorkspaceViewModel` using `ObservableObject`, generated commands, `IQueryAttributable`, constructor-injected repositories, explicit positive-ID parsing, repository reload as source of truth, overlapping-operation prevention, and a stable `ObservableCollection<Player>` updated in place. Query handling preserves an existing valid Team when later attributes omit the ID, avoids resetting the same Team unnecessarily, and resets correctly when a different explicit Team ID is supplied.
- Made Team Workspace the Team management hub. Tapping the displayed Team-name heading enables an inline, prepopulated Entry with Save and Cancel actions. Save trims input, preserves required-name, active-duplicate, and inactive-name validation through existing TeamRepository APIs, excludes the current Team ID, avoids writes when the persisted name is unchanged, updates the loaded state after success, and remains on Workspace. Cancel restores the persisted name, clears edit validation, exits edit mode, and performs no write.
- Added direct soft deactivation to Team Workspace using the existing resource-backed terminology, confirmation dialog pattern, and `TeamRepository.DeactivateAsync`. Confirmation and persistence operations prevent overlap; cancellation performs no write, and successful deactivation returns to Landing so its existing appearance reload removes the inactive Team.
- Removed the Workspace Edit Team button and its normal navigation to `EditTeamPage`, eliminating the broken Workspace → Edit Team → Back path. The existing Edit Team page, ViewModel, route, editing behavior, and deactivation behavior remain available and unchanged for still-supported callers.
- The tablet-oriented Workspace continues showing Current Roster, its empty state or ordered Player rows, and Add Players and Remove Players actions. Both roster actions remain resource-backed, non-persisting placeholders; no TeamPlayer write methods, selection, relationship deactivation, or global Player changes were added.
- Missing, malformed, inactive, and nonexistent Team IDs show the existing resource-backed Team-not-found message, do not load a roster, and leave roster commands disabled. Add Players and Remove Players currently show separate resource-backed placeholder messages, navigate nowhere, and perform no persistence.
- Player Manager UI and Player row interactions were intentionally unchanged. Team roster membership remains separate from future game lineup participation and batting-order responsibilities; no game-specific state was added.

#### Automated Tests

- Extended the isolated temporary database helper with focused test-only TeamPlayer seeding and inspection through a separate SQLite connection to the fixture-owned database. This supports relationship tests without adding production create, remove, deactivate, or reactivate methods.
- Extended the foundation initialization test to initialize repeatedly, seed a TeamPlayer row, and load the Player through the real TeamPlayer repository while retaining Team and Player table assertions.
- Added 14 isolated `TeamPlayerRepository` tests covering empty and valid rosters; active Team, Player, and relationship filtering; other-Team exclusion; multiple Players; case-insensitive First and Last Name ordering; null Last Names; duplicate names remaining distinct by ID; missing and non-positive Team IDs; and preservation of Team, Player, and TeamPlayer rows during reads.
- The focused TeamPlayer group passed 14 tests with zero failures and zero skips. The complete automated suite passed all 64 tests with zero failures and zero skips in approximately one second.

#### Files Added

- `SoftballScorekeeper.Core/Models/TeamPlayer.cs`
- `SoftballScorekeeper.Infrastructure/Data/Repositories/TeamPlayerRepository.cs`
- `SoftballScorekeeper.Tests/TeamPlayerRepositoryTests.cs`
- `SoftballScorekeeper/ViewModels/TeamWorkspaceViewModel.cs`
- `SoftballScorekeeper/Views/TeamWorkspacePage.xaml`
- `SoftballScorekeeper/Views/TeamWorkspacePage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper.Infrastructure/Data/AppDatabase.cs`
- `SoftballScorekeeper.Tests/TemporaryTestDatabase.cs`
- `SoftballScorekeeper.Tests/TemporaryTestDatabaseTests.cs`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/LandingPageViewModel.cs`
- `SoftballScorekeeper/Views/MainPage.xaml`

#### Validation Completed

- The Windows target build succeeded with 34 warnings and zero errors. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; the new ViewModel presentation fields and source-bound landing-control gesture add instances of existing warning categories but no new category.
- The command-line Android build was attempted and failed with `XA5300` because the environment could not locate the Android SDK. No Android build, deployment, emulator, or runtime success is claimed.
- Static review confirmed compact content-sized Landing controls, ID-only navigation, inline editing through existing repository APIs, soft deactivation with confirmation, corrected query-state preservation, active filtering at every roster relationship boundary, no production TeamPlayer writes, resource-backed runtime text, expected dependency-injection lifetimes, stable collection updates, disabled invalid or conflicting actions, and no future roster or lineup functionality.
- Automated runtime validation covered Infrastructure behavior only. Windows application runtime and Android tablet runtime validation were not performed; navigation, layout, placeholder feedback, and regression workflows still require manual validation on an available runtime.

#### Limitations and Deferred Work

Adding and removing Players, TeamPlayer deactivation and reactivation, jersey numbers, roster selection, seasons, records, game lineups, games, scoring, statistics, Beer Rules, UI automation, Android instrumentation, and branding remain deferred. The Workspace roster is read-only, and it will remain empty in normal application use until a later story adds membership management or existing TeamPlayer data is otherwise present. Windows and Android runtime validation of the final Team-management interactions remains required before Story 3.2 is fully complete.

#### Intended Commit Message

`Implement Team roster foundation`

### Story 3.3 — Player Workspace and Player Manager Cleanup

**Status:** Implementation complete; manual runtime validation pending

#### Implementation Summary

- Added transient `PlayerWorkspacePage` and `PlayerWorkspaceViewModel` and registered the centralized `player-workspace` Shell route. Player Manager navigation passes only the positive Player ID through the existing `playerId` query key; the Workspace reloads the active Player through `PlayerRepository` and safely rejects missing, malformed, non-positive, inactive, and nonexistent IDs.
- Made each Player name a compact, bordered, content-sized, tablet-friendly navigation control while preserving the checkbox as an independent bulk-selection control on the opposite side of the row. Player Manager retains Add Player, selected count, Deactivate Selected, Manage Inactive Players, empty state, ordering, selection wrappers, confirmation behavior, and repository-backed `OnAppearing` refresh. Its three list-level actions now share one horizontal action row, and per-row Edit buttons were removed.
- Added inline Player-name editing to Workspace with separate persisted and editable state. Tapping the displayed name prepopulates First Name and Last Name buffers; Save trims both values, requires First Name, normalizes blank Last Name to `null`, allows duplicate names, skips unchanged writes, updates exactly one active row through `PlayerRepository.UpdateAsync`, updates the display without leaving Workspace, and shows resource-backed retry feedback for failures. Cancel restores persisted values without writing.
- Added confirmed single-Player soft deactivation using the established ViewModel-to-page delegate pattern and the persisted display name. The existing parameterized bulk `PlayerRepository.DeactivateAsync` is reused with a one-ID collection; exactly one affected row is required, failures remain on Workspace with resource-backed retry feedback, and success returns to Player Manager for its normal reload. Unsaved edit buffers remain separate from the persisted confirmation name and survive a canceled confirmation.
- Added a resource-backed Team Membership heading and unavailable placeholder without TeamPlayer reads or writes, membership controls, game participation, lineup, statistics, or navigation.
- Removed the obsolete `EditPlayerPage`, `EditPlayerViewModel`, `edit-player` route and registrations, Player Manager edit command and per-row Edit button, and the exclusively used `EditPlayerButton`, `EditPlayerTitle`, and `PlayerManagementPlaceholderMessage` resources. Shared Player labels, placeholders, validation, Save, Cancel, and not-found resources remain.

#### Files Added

- `SoftballScorekeeper/ViewModels/PlayerWorkspaceViewModel.cs`
- `SoftballScorekeeper/Views/PlayerWorkspacePage.xaml`
- `SoftballScorekeeper/Views/PlayerWorkspacePage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/Views/PlayerListPage.xaml`

#### Files Removed

- `SoftballScorekeeper/ViewModels/EditPlayerViewModel.cs`
- `SoftballScorekeeper/Views/EditPlayerPage.xaml`
- `SoftballScorekeeper/Views/EditPlayerPage.xaml.cs`

#### Validation Completed

- The complete automated suite passed all 64 tests with zero failures and zero skips in approximately one second. No production repository behavior changed, so no artificial repository tests were added; Tests continues referencing only Core and Infrastructure.
- `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false -verbosity:minimal` succeeded with 42 warnings and zero errors. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; the new Workspace presentation fields and source-bound Player-name gesture add instances of existing categories but no new category.
- The Android command-line build was attempted and failed with `XA5300` because the environment could not locate the Android SDK. No Android build, deployment, emulator, or runtime success is claimed.
- Static review confirmed ID-only navigation, independent name and checkbox targets, separate persisted and editable state, no-change save behavior, persisted-name confirmation, one-ID reuse of parameterized soft deactivation, no TeamPlayer behavior, removal of obsolete Edit Player references, resource-backed runtime text, and unchanged repository APIs.

#### Limitations and Deferred Work

Windows and Android runtime validation remains required before Story 3.3 is fully complete. Team membership loading and writes, jersey numbers, Player statistics, game participation, lineups, permanent deletion, editing inactive Players, and UI automation remain deferred.

#### Intended Commit Message

`Implement Player workspace`

### Story 3.4 — Add Players to Team

**Status:** Implementation complete; manual runtime validation pending

#### Implementation Summary

- Added the transient `AddPlayersToTeamPage` and `AddPlayersToTeamViewModel`, centralized `add-players-to-team` Shell route, and ID-only navigation from Team Workspace through the existing `teamId` query key. The page reloads the active Team and eligible Players from repositories, reuses `PlayerSelectionItem` for non-persisted checkbox selection, tracks selected count, prevents overlapping load/confirmation/write operations, and uses resource-backed singular and plural confirmation through the established page delegate pattern.
- Extended `TeamPlayerRepository` with eligible-Player filtering, bulk add-or-reactivate behavior, and active-Team membership reads for a Player. Eligible Players are globally active and lack an active relationship to the selected active Team; inactive relationships and memberships on other Teams remain eligible. Results retain deterministic case-insensitive Player and Team ordering and safely reject invalid, inactive, or missing parents and Players.
- Added focused `AddPlayersToTeamResult` reporting requested, eligible, inserted, reactivated, already-active, and ignored Player IDs plus the total successfully active memberships. Repository-level duplicate enforcement deduplicates requested IDs, checks all existing Team/Player relationships, preserves and reactivates the lowest-ID inactive row, skips insertion when an active row exists, and avoids a database unique index or migration risk. Pre-existing duplicate cleanup remains outside scope.
- Complete add results return to Team Workspace, whose `OnAppearing` load now refreshes Team and roster repository state so new Players appear once in existing case-insensitive order. Partial results remain on the add page, reload eligible Players, reselect only requested IDs still eligible, and show the actual repository-reported success count. Remove Players remains the existing non-persisting Story 3.5 placeholder.
- Replaced Player Workspace’s Team Membership placeholder with a stable, read-only collection of active Teams loaded through `TeamPlayerRepository`. Membership refresh runs on each appearance without overwriting an active Player-name edit buffer; inactive Teams, inactive relationships, other Players’ memberships, and invalid or inactive Players are excluded.
- Removed the obsolete Add Players and Team Membership placeholder resources. No Core models, database schema, TeamPlayer deletion/deactivation behavior, Player creation, jersey numbers, game state, lineup, scoring, statistics, or generic relationship framework were added.

#### Files Added

- `SoftballScorekeeper.Infrastructure/Data/Repositories/AddPlayersToTeamResult.cs`
- `SoftballScorekeeper/ViewModels/AddPlayersToTeamViewModel.cs`
- `SoftballScorekeeper/Views/AddPlayersToTeamPage.xaml`
- `SoftballScorekeeper/Views/AddPlayersToTeamPage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper.Infrastructure/Data/Repositories/TeamPlayerRepository.cs`
- `SoftballScorekeeper.Tests/TeamPlayerRepositoryTests.cs`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/TeamWorkspaceViewModel.cs`
- `SoftballScorekeeper/ViewModels/PlayerWorkspaceViewModel.cs`
- `SoftballScorekeeper/Views/PlayerWorkspacePage.xaml`

#### Automated Tests and Validation

- Added 15 isolated TeamPlayer repository tests covering eligible filtering, active/inactive relationships and parents, other-Team eligibility, case-insensitive ordering, duplicate names by ID, non-mutation, insertion, reactivation with original-ID preservation, active duplicate prevention, input deduplication, invalid and inactive Players, missing/inactive Teams, empty input, result counts, cross-Team isolation, active membership filtering and ordering, and read non-mutation.
- The focused TeamPlayer repository run passed 29 of 29 tests with zero failures and zero skips. The complete suite passed 79 of 79 tests with zero failures and zero skips; Tests still references only Core and Infrastructure and uses isolated temporary SQLite databases.
- The final standalone `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false -verbosity:minimal` succeeded incrementally with one existing `CS8602` warning and zero errors. An earlier full compilation succeeded with 51 warnings from the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories and no errors; Story 3.4 introduced no new warning category.
- The Android command-line build was attempted and failed with `XA5300` because the environment could not locate the Android SDK. No Android build, deployment, emulator, Windows runtime, or Android runtime success is claimed.

#### Limitations and Deferred Work

Manual Android tablet and Windows runtime validation remain required before Story 3.4 is fully complete. Remove Players, TeamPlayer deactivation UI, roster checkboxes, jersey numbers, Player creation within the add flow, games, lineups, scoring, statistics, Beer Rules, and duplicate cleanup or database-level uniqueness remain deferred.

#### Intended Commit Message

`Implement add Players to Team`

### Story 3.5 — Remove Players from Team

**Status:** Complete; Android manual runtime validation passed, Windows runtime not run

#### Implementation Summary

- Replaced Team Workspace's removal placeholder with ID-only navigation through the centralized `remove-players-from-team` Shell route and existing `teamId` query key. The new transient page and ViewModel reload the active Team and active roster from repositories and reuse `PlayerSelectionItem`, stable collection updates, selected-count state, and overlapping-operation guards.
- Added a resource-backed empty state, selection action, and singular/plural confirmation. Confirmation limits removal to the selected Team, preserves globally active Player records and existing data, and explains that Players can be added back. Cancel performs no repository write and preserves selection.
- Extended `TeamPlayerRepository` with a focused parameterized bulk removal method and `RemovePlayersFromTeamResult`. It validates an active Team, safely deduplicates and classifies requested IDs, and updates only active matching `TeamPlayer.IsActive` values for the selected Team. It performs no inserts or deletes and preserves Player rows, TeamPlayer IDs and foreign keys, unselected relationships, and memberships on other Teams.
- Complete results navigate back so Team Workspace reloads repository truth on appearance. Partial and no-op results stay on the removal page, reload the active roster, reselect requested Players that remain active members, and display resource-backed feedback. Player Workspace's existing appearance refresh removes the Team from its read-only membership list without a lifecycle change.
- The existing Add Players workflow makes removed Players eligible and reactivates the existing inactive TeamPlayer row. Repository regression coverage verifies original relationship-ID preservation and prevents a duplicate row or duplicate active membership.

#### Files Added

- `SoftballScorekeeper.Infrastructure/Data/Repositories/RemovePlayersFromTeamResult.cs`
- `SoftballScorekeeper/ViewModels/RemovePlayersFromTeamViewModel.cs`
- `SoftballScorekeeper/Views/RemovePlayersFromTeamPage.xaml`
- `SoftballScorekeeper/Views/RemovePlayersFromTeamPage.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper.Infrastructure/Data/Repositories/TeamPlayerRepository.cs`
- `SoftballScorekeeper.Tests/TeamPlayerRepositoryTests.cs`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/TeamWorkspaceViewModel.cs`

#### Automated Tests and Validation

- Added seven isolated TeamPlayer repository tests covering relationship-only soft deactivation and row/key preservation, global Player and cross-Team isolation, unselected and duplicate relationship handling, null/empty/invalid/missing/already-inactive inputs, missing and inactive Teams, result classifications and counts, and remove-then-re-add ID preservation without duplication.
- The focused TeamPlayer repository run passed 36 of 36 tests with zero failures and zero skips. The complete suite passed 86 of 86 tests with zero failures and zero skips. Tests continue using isolated temporary SQLite databases and reference only Core and Infrastructure.
- The Windows target build succeeded with 58 warnings and zero errors. Warning categories remain the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; no new warning category was introduced.
- Manual Android tablet validation passed through the available runtime environment. The validated workflow covered navigation, correct Team and active-roster loading, single- and multi-Player selection and removal, singular and plural confirmation, cancellation with selection preservation and no removal, removal-page and Team Workspace refresh, correct remaining-roster ordering, and the empty-roster state after final removal.
- Android validation also confirmed that removed Team memberships disappear from Player Workspace, Players remain globally active, other active Team memberships remain intact, removed Players become eligible for Add Players, re-adding restores the existing membership without duplicate roster or membership entries, and Add Players remains functional. Inline Team editing, Team deactivation, Player editing, and Player deactivation also remained functional, with no clipping, overlap, stale selection, binding failure, or runtime exception observed.
- The Android command-line build remains unavailable because the environment cannot locate the Android SDK and reports `XA5300`; this CLI limitation does not negate the reported manual Android tablet runtime pass. The Windows target build succeeded, but the Windows application was not launched. Restart persistence was not tested and is not claimed.
- Static review confirmed centralized resources, transient page/ViewModel lifetimes, singleton repository lifetime, route registration, ID-only navigation, parameterized relationship-only SQL, no TeamPlayer inserts or deletes in removal, no global Player deactivation, and no database artifacts.

#### Limitations and Deferred Work

Windows application runtime and restart-persistence validation were not performed. Jersey numbers, removal metadata, lineup and game behavior, scoring, statistics, Beer Rules, generic relationship abstractions, duplicate-data cleanup, database uniqueness changes, and UI automation remain deferred.

#### Intended Commit Message

`Implement remove Players from Team`

### Story 3.6 — Global Application Navigation

**Status:** Complete; Android tablet manual validation passed, Windows runtime not run

#### Implementation Summary

- Enabled the existing Shell flyout and organized Teams and Players as native top-level `FlyoutItem` sections. Added a focused reusable `PrimarySectionNavigation` ContentView to the Team Manager, Team Workspace, Add Players, Player Manager, and Player Workspace pages. It presents only resource-backed Teams and Players actions with 48-pixel minimum touch heights and disables the current section for a compact light/dark-theme-compatible indication.
- Fast switching uses the absolute Shell routes `//teams/team-manager` and `//players/player-manager`. Absolute top-level navigation replaces the active section hierarchy instead of pushing another manager route, so repeated Teams/Players switching does not accumulate duplicate manager pages. It intentionally abandons the prior section's drill-down history; relative navigation within the selected section continues to preserve Team Workspace → Team Manager, Add Players → Team Workspace, and Player Workspace → Player Manager back behavior.
- The Shell flyout now exposes Manage Inactive Teams and Manage Inactive Players through the existing resource labels, registered routes, transient pages, ViewModels, and repository workflows. The prominent inactive-management buttons were removed from both manager pages, along with their unused ViewModel commands. The obsolete manager-only `ViewPlayersButton` resource and navigation command/route were removed.
- Existing page lifetimes remain unchanged: the injected Team Manager page and ViewModel are singletons, while Player Manager and all drill-down/inactive pages and ViewModels remain transient. Existing repository-backed `OnAppearing` loads remain the source of truth, including Team and Player manager refresh, Player selection/selected-count reset on reload, and Workspace query-ID reload behavior. No Core, Infrastructure, database, or repository behavior changed.
- Teams and Players reuse `TeamsHeading` and `PlayersTitle`; flyout inactive actions reuse `ManageInactiveTeamsButton` and `ManageInactivePlayersButton`. No Games or future navigation placeholders were added; Games expansion remains deferred.
- The final UX correction standardized temporary bulk-selection behavior. Team Workspace now enters inline remove mode from Remove Players, reveals roster checkboxes, selected count, Cancel, and Remove Selected from Team, and reuses the Story 3.5 confirmation and `TeamPlayerRepository` soft-deactivation behavior without leaving the Workspace. Player Manager similarly enters deactivate mode from the new resource-backed Deactivate Players action and reveals checkboxes, selected count, Cancel, and Deactivate Selected. Normal browsing on both screens hides checkboxes and selected-count UI; Cancel clears selections without writes, confirmation cancellation preserves mode and selections, complete success refreshes repository truth and exits mode, and partial results remain in mode with applicable selections preserved.
- Repository-backed appearance loads clear both temporary modes and selections, preventing global section switching or other navigation from restoring stale bulk-selection state. The dedicated Remove Players page, ViewModel, route, DI registrations, and page-only title/heading/empty-state resources became obsolete and were removed; Story 3.5 history and its shared repository behavior and confirmation/result resources remain intact.

#### Files Added

- `SoftballScorekeeper/Views/Controls/PrimarySectionNavigation.xaml`
- `SoftballScorekeeper/Views/Controls/PrimarySectionNavigation.xaml.cs`

#### Files Modified

- `FEATURE_HISTORY.md`
- `SoftballScorekeeper/AppShell.xaml`
- `SoftballScorekeeper/AppShell.xaml.cs`
- `SoftballScorekeeper/MauiProgram.cs`
- `SoftballScorekeeper/Resources/Strings/AppResources.resx`
- `SoftballScorekeeper/Resources/Strings/AppResources.Designer.cs`
- `SoftballScorekeeper/ViewModels/LandingPageViewModel.cs`
- `SoftballScorekeeper/ViewModels/PlayerListViewModel.cs`
- `SoftballScorekeeper/ViewModels/TeamWorkspaceViewModel.cs`
- `SoftballScorekeeper/Views/MainPage.xaml`
- `SoftballScorekeeper/Views/TeamWorkspacePage.xaml`
- `SoftballScorekeeper/Views/TeamWorkspacePage.xaml.cs`
- `SoftballScorekeeper/Views/AddPlayersToTeamPage.xaml`
- `SoftballScorekeeper/Views/PlayerListPage.xaml`
- `SoftballScorekeeper/Views/PlayerWorkspacePage.xaml`

#### Files Removed

- `SoftballScorekeeper/ViewModels/RemovePlayersFromTeamViewModel.cs`
- `SoftballScorekeeper/Views/RemovePlayersFromTeamPage.xaml`
- `SoftballScorekeeper/Views/RemovePlayersFromTeamPage.xaml.cs`

#### Automated Tests and Validation

- The complete automated suite passed 86 of 86 tests with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --no-build --verbosity:normal`. No new tests were warranted: the suite remains intentionally repository-focused with references only to Core and Infrastructure, while Story 3.6 changed MAUI navigation and presentation state and introduced no persistence behavior.
- The final Windows target rebuild succeeded with 54 warnings and zero errors using `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false -verbosity:minimal -t:Rebuild`. Warning count and categories are unchanged from the prior Story 3.6 validation: the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories remain, and the story introduced no new warning category. The Windows application was not launched.
- Manual validation passed on the 10-inch Android tablet for the final Story 3.6 implementation. It covered fast-switch layout; Teams and Players navigation, repeated switching without manager-stack growth, and current-section indication; manager cleanup and refresh/interactivity; Shell flyout and both existing inactive-management workflows; Team and Player Workspace navigation/back behavior; Add Players regression; inline Team removal and Player deactivation selection modes; selection Cancel and confirmation-cancel behavior; successful selected actions; global section switching from temporary modes; selected-count alignment; layout/clipping; stale-state prevention; and absence of observed binding failures or runtime exceptions.
- The Android command-line build remains unavailable because the environment cannot locate the Android SDK and reports `XA5300`; this CLI limitation does not negate the successful physical-tablet runtime validation.

#### Limitations and Deferred Work

Windows runtime validation was not performed. Games fast navigation, a Current Game shortcut, lineups, statistics, Beer Rules, Settings, and any other expansion of global navigation beyond Teams and Players remain deferred.

#### Intended Commit Message

`Implement global application navigation`

### Story 3.7 — Replace Default Application Icon

**Status:** Complete; Android build, runtime, and launcher validation passed; Windows validation intentionally not performed

#### Implementation Summary

- Replaced the default layered .NET MAUI icon configuration with the Softball Scorekeeper source asset at `SoftballScorekeeper/Resources/AppIcon/softball_scorekeeper_icon.png`. The icon uses an optic-yellow softball with blue stitching and bold athletic-style `350` lettering, a white home plate, and a dark blue background. Large, simple elements preserve recognition at Android launcher sizes.
- Updated `SoftballScorekeeper.csproj` to process the PNG through the existing `MauiIcon` pipeline with `BaseSize="1024,1024"`, replacing the former `appicon.svg` and `appiconfg.svg` layered configuration. Removed both obsolete default SVG assets after confirming no active project or Android references remain.
- Updated `Platforms/Android/AndroidManifest.xml` so `android:icon` and `android:roundIcon` reference the generated `@mipmap/softball_scorekeeper_icon` and `@mipmap/softball_scorekeeper_icon_round` resources that match the new `MauiIcon` filename.
- No application behavior, navigation, persistence, database schema, repository, Core, Infrastructure, Team, Player, TeamPlayer, or Games functionality changed. No application ID, application title, or version information changed. No dependencies were added.

#### Automated Tests and Validation

- The complete automated test suite was run manually and passed 86 of 86 tests with zero failures and zero skips. No icon-specific automated tests were added.
- The Android build passed, and the application launched successfully on the Android tablet. Visual validation in the Android launcher/task interface confirmed that the softball is recognizable, `350` remains readable, blue stitching and the home plate composition remain visible, important artwork is not clipped, and launcher masking is acceptable.
- Windows runtime and icon validation were intentionally not performed because Android is the deployment target for this branding story. No Windows-specific icon work was introduced.
- Repository review confirmed the project and manifest icon configuration are consistent, obsolete default icon references are absent from active Android/MAUI configuration, and no generated Android icon output, build output, database files, secrets, machine-specific files, or new dependencies are included in the story diff.

#### Limitations and Deferred Work

Splash-screen redesign was not included. Further branding work, including any future platform-specific branding beyond the Android deployment target, remains deferred.

#### Intended Commit Message

`Replace default application icon`

### Story 4.0 — Season and Game Data Foundation

**Status:** Complete; automated tests and Windows build passed; Windows and Android runtime validation not run

#### Implementation Summary

- Added the Core `Season` model with only auto-increment `Id`, owning `TeamId`, data-driven `Name`, and calendar `Year`. Season has no `IsActive`, lifecycle/status, `StartDate`, or `EndDate`: Team is the long-lived identity across years, one Team may own Seasons in multiple years, and a Season's temporal extent is represented by its Games rather than duplicated date fields.
- Added the Core `Game` model with auto-increment `Id`, owning `TeamId` and `SeasonId`, `DateTime ScheduledDate`, `TimeSpan ScheduledTime`, and nullable `Opponent` and `Field`. Scheduling represents local/device softball schedule time without timezone conversion infrastructure, and unknown opponent or field values remain null rather than persisted placeholders.
- Extended the guarded `AppDatabase` initialization to create the `Season` and `Game` tables additively after the existing `Team`, `Player`, and `TeamPlayer` tables. The existing database path, tables, records, and initialization pattern remain unchanged; no reset, migration framework, or manual migration step was introduced.
- Added the read-only, Team-scoped `SeasonRepository` APIs `GetForTeamAsync`, `GetByIdForTeamAsync`, and `HasSeasonsForTeamAsync`. Missing and non-positive contexts return safe results, and Seasons order deterministically by year, default SQLite name ordering, then ID without lifecycle filtering or hardcoded name chronology.
- Added the read-only, Team-scoped `GameRepository` APIs `GetForTeamAsync` and `HasGamesForTeamAsync`. Games order by scheduled date, scheduled time, then ID; same-date and same-time Games remain distinct. The repository has no Player, TeamPlayer, roster, or lineup dependency, so a Team with no roster Players can own and query Games.
- Registered both repositories as singletons using the existing repository lifetime convention. No page, ViewModel, resource, navigation, Team/Player workflow, roster-history behavior, or user-facing UI changed.
- Persisted ownership is Team → Season → Game: Season stores `TeamId`, and Game stores both `TeamId` and `SeasonId`. Team-scoped reads preserve and isolate these relationships. Story 4.0 exposes no public Game write API, so validation that a selected Season belongs to the same Team during a write is intentionally deferred to the future Add Game workflow and is not claimed as currently enforced against direct inserts.

#### Automated Tests and Validation

- Added 11 `SeasonRepositoryTests` covering persistence, arbitrary names and counts, one Team across multiple years, Team isolation, Team-scoped ID lookup, year/name/ID ordering, invalid and missing IDs, existence queries, and read immutability.
- Added 10 `GameRepositoryTests` covering complete persistence and nullable fields, multiple Seasons and years, Team isolation, date/time/ID ordering, distinct same-date and same-time Games, invalid and missing IDs, existence queries, zero-roster Game ownership, and read immutability. The existing initialization test now exercises all five tables, repeated initialization, and same-Team multi-year Season persistence without adding another test method.
- The complete suite increased from 86 to 107 tests and passed 107 of 107 with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --verbosity:minimal -nodeReuse:false`.
- The Windows target rebuild succeeded with 54 warnings and zero errors using `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false -verbosity:minimal -t:Rebuild`. The warning count and existing `CS8602`, `MVVMTK0045`, and `XC0025` categories match the previously recorded baseline; Story 4.0 introduced no new warning category. The Windows application was not launched.
- Android build and runtime validation were not performed. Story 4.0 adds no user-facing UI behavior; repository behavior was validated through isolated temporary SQLite databases.

#### Limitations and Deferred Work

Write-time Team/Season ownership validation, Season and Game creation/editing/removal workflows, Team Workspace Games/Roster tabs, schedule UI, month grouping, NEXT indication, doubleheader workflows, Game Workspace, roster history, lineups, scoring, statistics, Beer Rules, and all other Story 4.1+ behavior remain deferred. No Season or Game lifecycle was introduced.

#### Intended Commit Message

`Add Season and Game data foundation`

### Story 4.1 — Team Workspace Games and Roster Tabs

**Status:** Complete; automated tests, Windows build, and Android 10-inch tablet validation passed

#### Implementation Summary

- Added local Games and Roster sections within Team Workspace, preserving the global Teams | Players navigation. Games and Roster are Team-specific content sections, not global destinations, separate Shell routes, or top-level managers; the hierarchy remains Teams → Team Workspace → Games | Roster.
- Used the existing `GameRepository` to choose the initial section for each Team context. A Team with persisted Games initially opens Games, including when its roster is empty; a Team without Games initially opens Roster. Roster membership does not determine Game availability, and changing to a different Team recalculates the initial selection.
- Preserved an explicit Games or Roster selection across ordinary repository-backed refreshes after the initial decision. No global tab-state service, persisted navigation state, messaging system, or generic navigation-state framework was introduced.
- Established only the Story 4.1 Games container: Teams without Games receive a resource-backed empty state, while Teams with Games receive a resource-backed temporary Games-present state. No raw IDs or debug data are displayed.
- Moved the existing roster presentation under Roster without redesign. Active-roster display, empty state, ordering, multi-select Add Players, inline Remove Players mode, conditional checkboxes, selected count, cancellation, confirmation, repository refresh, TeamPlayer-only membership removal, Player preservation, and re-add behavior remain intact.
- Switching from Roster to Games while Remove Players mode is active exits the mode, clears temporary selections, resets the selected count, and performs no persistence or confirmation. Returning to Roster shows normal mode; hidden removal selections are not retained.
- Added Create New Player to the Team-scoped Add Players workflow by reusing the existing Create Player UI. The workflow creates the normal global Player, immediately adds TeamPlayer membership for the current Team, refreshes the Team roster, and leaves the Player available globally without duplicating Player data in TeamPlayer. Existing multi-select Add Players behavior remains unchanged.
- If global Player creation succeeds but Team membership fails, the global Player is preserved, duplicate creation is prevented, a resource-backed partial-outcome message is shown, and returning to Add Players allows the existing Player to be added normally. No new repository API or transaction behavior was introduced.
- Team editing remains outside the Games/Roster content distinction and continues to work for either selection. Games remain independent of roster membership, and TeamPlayer data is not consulted to show or enable Games. Existing Team Manager back behavior and global Teams/Players navigation remain unchanged, with no duplicate Team Workspace stack behavior.
- No Core models, database schema, Infrastructure behavior, or dependencies changed.

#### Automated Tests and Validation

- The complete automated suite passed 107 of 107 tests with zero failures and zero skips. No unnecessary MAUI presentation tests were added.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 54 warnings. Warnings remain in the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; no new warning category was introduced.
- Manual validation passed on the 10-inch Android tablet. It covered a Team without Games defaulting to Roster; Games/Roster switching and selected-section indication; Games empty presentation; roster display; Add Players; Team-scoped Create New Player with automatic TeamPlayer membership and visibility in both the Team roster and global Players; Remove Players entry, cancellation, confirmation, and section-switch cleanup; Team editing; global Teams/Players navigation; Back navigation; selector sizing and touch behavior; and light and dark themes.
- No clipping, overlap, stale hidden removal selection, binding failure, or runtime exception was observed. A Team-with-existing-Games runtime scenario could not be practically produced through the current production UI, so that initial Games selection was covered by the implemented repository-backed logic and available test data rather than claimed as a production-UI-created manual scenario.
- Final repository review confirmed `git diff --check` passes.

#### Limitations and Deferred Work

Populated Game schedule rows, Story 4.2 schedule UI, month grouping and accordion behavior, NEXT playing-date indication, Game CRUD and selection mode, Season UI, Game Workspace, lineups, live scoring, statistics, Beer Rules, and global Games navigation remain deferred. Story 4.1 introduced no schedule-management behavior or other later-story functionality.

#### Intended Commit Message

`Document completed Team Workspace Games and Roster tabs`

### Story 4.1.1 — Replace Default Startup Splash Branding

**Status:** Complete; Android 10-inch tablet startup validation passed; automated tests and Windows build passed

#### Implementation Summary

- Replaced `Resources/Splash/splash.svg`, which contained the default MAUI/.NET artwork, with `Resources/Splash/softball_scorekeeper_splash.png`, featuring the Softball Scorekeeper yellow softball, `350`, and home-plate branding.
- Updated the existing `MauiSplashScreen` declaration from the default `#512BD4` background and `128,128` base size to the branded navy `#0B2F6B` background and final `256,256` base size.
- Retained the native MAUI splash-screen pipeline. No custom Android or Windows splash implementation, fake `ContentPage` splash, timer, artificial startup delay, navigation change, dependency, or framework was introduced.
- No Team, Player, roster, Season, Game, Core, Infrastructure, or persistence behavior changed.

#### Automated Tests and Validation

- The complete automated suite passed 107 of 107 tests with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 54 warnings. Warnings remain in the existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; no new warning category was introduced. The Windows application was not launched, so Windows runtime splash appearance was not validated.
- Manual startup validation passed on the 10-inch Android tablet. The Softball Scorekeeper splash appeared centered at the final `256,256` size on the navy background without visible clipping; default MAUI/.NET branding was absent, and transition into the existing application UI remained normal with no fake in-app splash or artificial delay.

#### Limitations and Deferred Work

Windows runtime splash appearance was not validated. No broader branding, theme, navigation, or application behavior work was included.

#### Intended Commit Message

`Replace default startup splash branding`

### Story 4.2 — Team Games Schedule

**Status:** Implementation complete; automated tests passed; Android 10-inch tablet validation pending

#### Implementation Summary

- Replaced the Story 4.1 temporary Games-present placeholder with the selected Team's persisted, read-only Game schedule inside the existing Team Workspace Games section. The ViewModel now loads Team-scoped Games through `GameRepository.GetForTeamAsync` and Team-scoped Seasons through `SeasonRepository.GetForTeamAsync`; roster membership remains unrelated to schedule availability.
- Added focused MAUI presentation models for schedule rows and expandable calendar-month groups without changing persisted `Game` or `Season` models. Groups use year/month identity, remain chronological, retain repository date/time/ID row ordering, and keep same-date Games as separate rows.
- Month headers use culture-aware uppercase abbreviated month names when the complete schedule is within one calendar year and add the year when the schedule spans multiple years. Each header has a 48-DIP touch target and a visible resource-backed right/down indicator; groups expand independently and multiple groups may remain open.
- When schedule state is first established for a Team, exactly one month expands: the month containing the earliest date on or after the local `DateTime.Today`, or the most recent Game month when every Game is in the past. Ordinary refreshes rebuild from repository truth while preserving expansion choices by year/month when possible, and changing Team context resets schedule state.
- NEXT uses date-only local-device semantics. Every Game on the earliest distinct scheduled date on or after today receives the resource-backed NEXT marker, including all same-day doubleheader or tournament rows; later dates and all-past schedules receive no marker. A Game today remains NEXT regardless of its scheduled clock time.
- Compact schedule rows show culture-aware date and time, persisted opponent, field, and Team-scoped Season name. Null, empty, or whitespace opponent and field values display resource-backed TBD without changing persisted data; an unexpectedly missing or blank Team-scoped Season also displays TBD rather than crashing or inventing a Season name.
- Preserved the existing Games empty state, initial Games-versus-Roster selection, explicit section selection across refreshes, removal-mode cleanup when switching to Games, roster workflows, Team editing and deactivation, global navigation, and repository-backed `OnAppearing` refresh. Removed the unused `GamesAvailableMessage` resource and binding.
- No Game CRUD, Season UI, lifecycle/status, scoring, lineup, Game Workspace, global Games navigation, Core, Infrastructure, database/schema, direct SQLite, dependency, or navigation-route change was introduced.

#### Automated Tests and Validation

- The complete repository suite passed 107 of 107 tests with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --verbosity:minimal -nodeReuse:false`. No new tests were added because the new behavior is MAUI presentation logic and existing repository behavior did not change.
- The Android command-line build was attempted but could not run because the environment cannot locate the Android SDK and reports `XA5300`. No Windows build or runtime validation was performed, consistent with this story's Android-only validation scope.
- Android runtime validation has not yet been performed. Schedule rendering, Team isolation, month ordering and expansion, multi-year headers, NEXT scenarios, formatting and TBD display, empty/zero-roster schedules, roster regressions, navigation, light/dark themes, layout, bindings, duplicates, and runtime stability remain pending on the 10-inch Android tablet.

#### Limitations and Deferred Work

Production UI currently has no Add Game or Create Season workflow, and the repository contains no production seed utility. Populated-schedule Android scenarios therefore require safe external setup of Season and Game rows in the device-local database. Game and Season creation/editing/removal, lifecycle/status, Game Workspace, lineups, scoring, statistics, Beer Rules, and global Games navigation remain deferred.

#### Intended Commit Message

`Implement read-only Team Games schedule`

### Story 4.3 — Add Game

**Status:** Complete

#### Implementation Summary

- Added a resource-backed Add Game action only within Team Workspace Games, a focused Shell route carrying only `teamId`, and transient Add Game page/ViewModel registrations. The workflow reloads the active Team and its Team-scoped Seasons from repositories and returns through the existing navigation stack so Team Workspace preserves Games selection and Story 4.2 reloads schedule truth on `OnAppearing`.
- Add Game validates the active Team context and loads only that Team's Seasons. The workflow supports selecting an existing Season or creating one in-form without a hardcoded Season-name list; zero-roster Teams remain fully supported.
- Added a tablet-oriented Add Game form for existing Season selection or in-page new Season entry, scheduled date/time, optional opponent/field, and Save/Cancel. Existing Seasons display their persisted name with year for disambiguation. New Season names are trimmed and required; Season Year defaults from Game Date until intentionally edited and accepts a valid calendar year.
- Defaulted Add Second Game on. The visible, editable second time defaults to one hour after the first time and continues following the first time until intentionally edited. Single saves request one Game; doubleheader saves request two independent same-date Game rows sharing Team, Season, opponent, and field without adding a pairing model.
- Extended `SeasonRepository` with normalized, Team/year-scoped get-or-create behavior. Names compare trimmed and case-insensitively, so logical duplicates reuse the existing Season while the same name in another year or Team remains independent. Invalid, missing, or inactive Team contexts and invalid Season values create nothing.
- Extended `GameRepository` with one focused transactional create operation. It validates the active Team and persisted same-Team Season inside the transaction, or resolves/creates a requested new Season in that same transaction, then inserts exactly one or two Games. Optional opponent/field values are trimmed and whitespace becomes null. Failed ownership, invalid batches, or insertion failure report incomplete results and leave no partial doubleheader or orphan new Season.
- Game/Season creation remains independent of TeamPlayer, Player, roster, and lineup state. No Core model, schema, migration, Game lifecycle/status, edit/remove behavior, Game Workspace, Season Manager, lineup, scoring, statistics, Beer Rules, global Games navigation, dependency, or generic repository/service/transaction abstraction was introduced.
- Removed the invalid `HorizontalTextAlignment` attribute from the Story 4.2 month-header Button after the required Windows build exposed it as `XC0009`; this minimal correction allows the existing schedule and Story 4.3 integration to compile without changing schedule behavior.
- The Android correction pass added one consistent resource-backed red required indicator to genuinely required Add Game labels, placed all validation failures in a padded high-contrast warning container, and strengthened the existing-Season picker with a themed border and selection guidance. Opponent and Field remain visibly optional.
- Seasons now list by year ascending and then creation ID ascending rather than alphabetically. Add Game defaults to the Season referenced by the Team's highest-ID persisted Game, falling back to the highest-ID Team Season when no Game has used a Season, and enters Create New Season when none exist. First and second Game times now default to 6:30 PM and 7:30 PM while preserving the existing manual second-time override behavior.
- Game creation now rejects duplicates by persisted `TeamId + SeasonId + ScheduledDate.Date + ScheduledTime`, regardless of opponent or field. The repository also rejects duplicate date/time keys inside a requested doubleheader; persisted conflicts are checked inside the existing transaction so either conflict inserts neither Game and rolls back any proposed new Season. Add Game remains on-screen with a highlighted duplicate-specific resource message.
- Global Teams/Players navigation remains available from detail and workspace pages: Team Workspace and Add Game can return absolutely to Team Manager where global navigation is present, and Player Workspace can return absolutely to Player Manager. The actual Team Manager or Player Manager treats a tap on its already-selected root destination as a no-op, avoiding duplicate Shell stack behavior. Selected controls are purple; available destinations use a themed bordered surface; grey/dim remains reserved for genuinely disabled or unavailable controls. Team Workspace Games/Roster follows the same convention where applicable. No global Games navigation was introduced.

#### Automated Tests and Validation

- Story 4.3 entered with 107 tests, added 15 tests in the initial implementation for a 122-test passing baseline, then added 7 correction tests for 22 new tests overall and a final total of 129. Coverage includes Season normalization and logical reuse, Team/year isolation, same-name/different-year behavior, year/creation-order listing, most-recently-used lookup, Game ownership, optional-field normalization, zero-roster creation, single and doubleheader persistence with distinct IDs and same-date ordering, exact duplicate rejection independent of opponent/field, requested duplicate times, conflicts in either doubleheader position, transactional rollback, and preservation of existing rows.
- After the correction pass, focused Season/Game validation passed 43 of 43 tests. The complete suite passed 129 of 129 with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --verbosity:minimal -nodeReuse:false`.
- The Windows `net9.0-windows10.0.19041.0` target rebuild succeeded with zero errors and 67 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; the additional warnings remain in existing categories. Windows runtime validation was not performed.
- Manual Android validation and the correction retest passed on the 10-inch tablet in light and dark themes with no observed clipping, overlap, runtime, or binding issues. Validation covered the Add Game entry point and Team-scoped context/navigation; active-Team and Team-isolated Season behavior; existing and new Season flows; required, trimmed, year-defaulting, duplicate-reuse, ordering, and most-recent defaults; native date/time controls and 6:30/7:30 defaults; optional and blank opponent/field handling; second-Game default, editing, and override preservation; single and doubleheader creation; duplicate/conflict rejection in either doubleheader position; Cancel, rollback/no orphan Season, repeated-Save protection, zero-roster creation, schedule refresh, return to Games, restart persistence, corrected global navigation, and selected/available navigation presentation.
- The same Android pass closed Story 4.2's deferred populated-schedule validation using application-created data. It confirmed immediate refresh, Team isolation, chronological rows, year/month grouping and ordering, multi-year presentation, default and independent/manual month expansion, all documented NEXT date cases including same-date doubleheaders and today, no NEXT for all-past schedules, date/time/opponent/field/Season and TBD presentation, separate same-date rows, empty-to-first-Game transition, Games selection after creation and on later populated visits, and zero-roster schedule visibility.
- Final quality review recorded the complete automated suite passing, the Windows build succeeding, Android tablet validation and its correction retest passing, and Story 4.2's deferred populated-schedule validation passing. `git diff --check` passed. No generated build output, database/test database, secret, or machine-specific file was intentionally added, and no Core, schema, or package change was required.

#### Limitations and Deferred Work

Story 4.4 Remove Games, Story 4.5 Edit Game, safe Game lifecycle/removal rules, rainout/rescheduling, Season editing/removal unless later required, Game Workspace, Lineup Management, scoring, statistics, Beer Rules, and global Games navigation remain deferred. Story 4.3 introduced none of those behaviors and added no roster dependency.

#### Intended Commit Message

`Implement Add Game workflow`

### Story 4.4 — Remove Games

**Status:** Complete

#### Implementation Summary

- Added an inline removal mode to Team Workspace Games without adding a page, ViewModel, route, or global state. Normal mode retains the schedule, Add Game, month expansion, and NEXT presentation while adding Remove Games; removal mode shows tablet-sized right-side checkboxes, resource-backed singular/plural selected counts, a disabled-until-selected Remove Selected action, and Cancel.
- Game selection is temporary presentation state on `GameScheduleItem`, not Core `Game` or persistence state. Cancel, Games-to-Roster switching, Team reload, and later Workspace visits clear selections and exit removal mode without persistence. Canceling the native resource-backed singular/plural confirmation preserves removal mode and the current selection.
- Extended `GameRepository` with a focused Team-scoped bulk hard-delete operation and `RemoveGamesResult`. It normalizes duplicate input IDs and reports deleted, missing, wrong-Team, blocked, and ignored IDs; invalid Team/input requests are safe no-ops, and a Team can never remove another Team's Game.
- Infrastructure owns the dependent-history decision. The current schema has no persisted Game-owned lineup or scoring history, so existing scheduled Games are currently eligible for hard deletion; the focused guard can incorporate real future history without changing the Games-tab contract. No lifecycle/status fields, soft deletion, fake history tables, migration, or generic operation framework was introduced.
- Removing a Game never removes its Season. Same-date doubleheader Games remain independent rows, so selecting one removes only one and selecting both removes both; no pairing inference or identifier was added.
- Confirmed operations reload Games and Seasons from repository truth and reuse the Story 4.2 schedule rebuild. This naturally removes empty months, preserves surviving month expansion where practical, recalculates NEXT, retains TBD/Season presentation, and shows the existing empty state when the final Game is removed while keeping Games selected for the current session. A later fresh empty-Team Workspace visit continues to use Story 4.1's Roster default.
- Complete success exits removal mode; partial and no-op results refresh repository truth, remain in removal mode, reselect requested Games still present where possible, and display resource-backed feedback based on the repository result. Busy and confirmation state prevent repeated execution.
- A physically deleted date/time slot no longer conflicts with Story 4.3 duplicate detection. The preserved Season remains available and the same Team/Season/date/time can be scheduled again.
- Global-navigation regression testing exposed that native Shell `FlyoutItem` switching restored each section's retained child stack. The flyout now uses built-in Teams/Players menu actions that invoke the existing absolute manager routes while the Shell sections remain route hosts, so global Teams and Players always reset to Team Manager and Player Manager rather than reopening stale Workspace pages. Repository-backed appearance reloads continue clearing transient Game, roster, and Player selections without persistence; relative local Back navigation remains unchanged.

#### Automated Tests and Validation

- Added 7 focused `GameRepositoryTests` covering single and multiple hard deletion, persisted row removal, unrelated-row preservation, Team isolation and wrong-Team reporting, duplicate/non-positive/missing/null/empty input handling, accurate result lists/counts, final-Game Season preservation, one-or-both doubleheader removal, and recreation of a deleted slot with the preserved Season. No fake dependent-history test infrastructure was added because no such persisted schema exists yet.
- Focused Game repository validation passed 33 of 33 tests with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --filter FullyQualifiedName~GameRepositoryTests --verbosity:minimal -nodeReuse:false`.
- The complete suite increased from 129 to 136 tests and passed 136 of 136 with zero failures and zero skips using `dotnet test SoftballScorekeeper.Tests\SoftballScorekeeper.Tests.csproj --no-restore --verbosity:minimal -nodeReuse:false`.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 67 warnings using `dotnet build SoftballScorekeeper\SoftballScorekeeper.csproj -f net9.0-windows10.0.19041.0 --no-restore -nodeReuse:false --verbosity:minimal`. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; Story 4.4 introduced no new warning category or net warning increase. Windows runtime validation was not performed.
- Android manual validation passed on the 10-inch tablet. It covered normal/removal mode visibility; right-side checkboxes; single, multiple, and deselection behavior; selected-count and Remove Selected command state; Cancel with no persistence; singular/plural confirmation and confirmation cancellation with selection preservation; single, multiple, and independent doubleheader deletion; repository-backed refresh, empty-month cleanup, and NEXT recalculation; final-Game current-session empty state and later fresh-visit Roster fallback; deleted-slot recreation and Season preservation; Games-to-Roster cleanup; and correct layout with no observed clipping, overlap, binding failure, or runtime exception.
- The Android regression pass also confirmed that global navigation clears Game-removal, roster-removal, and Player-deactivation temporary state without persistence; Teams and Players now reach their manager roots; repeated root navigation does not intentionally grow a child stack; local Back remains functional; and existing application workflows remain operational. General slowness observed while the development computer was under heavy concurrent load did not reproduce as a specific application performance defect and is not recorded as a Story 4.4 defect.

#### Limitations and Deferred Work

Real dependent Game history does not yet exist, so blocked and mixed production outcomes cannot be exercised until a future story introduces genuine Game-owned persistence. Standard MAUI Shell continues replacing the hamburger icon with Back on pushed routes; simultaneous standard Back and global-menu affordances would require broader title-bar/navigation customization and remain deferred navigation polish, while existing in-page Teams/Players navigation remains available on supported workflow pages. Edit Game, completion/cancellation/status, rainout/rescheduling, archiving/restoration, Game Workspace, Lineup Management, scoring, statistics, Beer Rules, Season editing/removal, and global Games navigation remain deferred.

#### Intended Commit Message

`Implement inline Game removal`

### Story 4.5 — Edit Game

**Status:** Complete on `task/edit-games`; automated tests, Windows build, and Android 10-inch tablet validation passed

#### Implementation Summary

- Team Workspace Game rows are now the normal-mode navigation target and open a transient Game Workspace using only `teamId` and `gameId`; the schedule no longer exposes Edit directly on each row. In Remove Games mode, the same row interaction only toggles removal selection and cannot navigate.
- Game Workspace reloads the active Team, Team-scoped Game, and Team-scoped Season from repository truth whenever it appears. It presents the selected Game's opponent, date, time, field, and Season with resource-backed labels and established formatting, and provides the focused Edit Game action. It is the intended home for later Game-specific actions without implementing or displaying lineup, start-game, scoring, or lifecycle behavior.
- Edit Game is entered from Game Workspace with ID-only navigation. Its existing relative Save and Cancel navigation now returns naturally to Game Workspace without stack duplication; Game Workspace reloads persisted values on appearance, and later Back navigation returns to Team Workspace, whose existing appearance reload refreshes schedule grouping and NEXT state.
- Corrected Game Workspace query handling so Shell reappearance after Edit Game does not clear the existing Team/Game context when query parameters are not resent. Save and Cancel reveal the same workspace instance, which retains its IDs and reloads repository truth; legitimate navigation to a different Game replaces the context, while malformed or invalid ID contexts still produce the missing-Game state.
- Edit Game reloads the active Team, Team-scoped Game, and Team-scoped Seasons from repositories and initializes the existing Season, persisted date/time, opponent, and field rather than Add Game defaults. The focused form reuses established Season display/creation, validation, resources, busy protection, and relative Save/Cancel navigation without doubleheader controls.
- Added Team-scoped Game lookup and a focused transactional update result/API. Updates validate active Team, authoritative persisted Game ownership, target Season ownership, date/time, and duplicate schedule while excluding the current Game. They update the existing row in place, preserve `Game.Id` and `Game.TeamId`, normalize optional opponent/field values, and leave unrelated Games unchanged.
- Existing or transactionally resolved logical Seasons may be selected; failed validation makes no Game mutation, and transaction failure cannot leave an orphan new Season. Moving a Game never deletes its old Season. Same-date/doubleheader rows remain independent and no pairing behavior was introduced.
- Successful Save preserves the existing in-place persistence behavior. Cancel performs no persistence. Editing remains roster-independent.
- Editing remains supported for Teams with zero roster Players. Existing Add Game behavior was preserved alongside Game Workspace navigation and Remove Games selection behavior.

#### Automated Tests and Validation

- Added 7 focused `GameRepositoryTests` for Team-scoped lookup, invalid/missing/wrong-Team IDs, in-place field/date/time normalization, identity preservation, unchanged self-save, duplicate rejection with original/unrelated-row preservation, Game/Season ownership rejection, Season movement/preservation, independent same-date Games, and logical Season reuse.
- Focused Game repository validation passed 40 of 40 tests with zero failures and zero skips. The complete suite increased from 136 to 143 tests and passed 143 of 143 with zero failures and zero skips.
- The Windows target build succeeded with zero errors and 89 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; the additional warnings are `MVVMTK0045` warnings from the new CommunityToolkit-generated Game Workspace observable properties. Windows runtime validation was not performed.
- Android 10-inch tablet validation passed for the complete Story 4.5 workflow. It covered Game-row entry into the correct Game Workspace, displayed details, Edit entry, repeated Save and Cancel returns with retained context, repository-backed refresh, one-step Back behavior, navigation between different Games without stale context, schedule ordering/month grouping/NEXT recalculation, Remove Games suppression and selection, Add Game, Games/Roster switching, light/dark themes, layout, bindings, and runtime stability.

#### Limitations and Deferred Work

Game lifecycle/status, completion/cancellation, rainout/rescheduling, lineup actions and persistence, start-game behavior, scoring, statistics, Beer Rules, Season editing/removal, linked-doubleheader behavior, and global Games navigation remain deferred. Story 4.5 adds no schema, Core, dependency, or generic CRUD/form/transaction framework.

#### Intended Commit Message

`Implement Edit Game workflow`

### Story 5.0 — Game Lineup Foundation

**Status:** Complete on `task/game-lineup-foundation`; automated tests, Windows build, and focused Android tablet validation passed

#### Implementation Summary

- Added additive `GameLineupPlayer` persistence with Game, global Player, and mutable one-based `BattingOrder` data plus a unique Game/Player index. Lineups remain isolated by Game, and existing Team, Player, TeamPlayer, Season, and Game schema/data remain intact.
- Added a focused Game-lineup repository. Eligible selection is restricted to active global Players with active memberships on the Game's Team. Initial save revalidates Game ownership and every Player transactionally, rejects partial saves and existing-lineup conflicts, normalizes duplicate IDs while preserving first occurrence, and assigns batting order from the supplied selection sequence.
- Added the Game-owned Set Lineup workflow using established checkbox rows and Shell ID navigation. Eligible additions require both an active global Player and active TeamPlayer membership for the Game's Team; other-Team-only Players are rejected. Presentation state separately records checkbox click order; unchecking removes the pending Player and selecting again appends that Player to the end. Duplicate supplied IDs preserve first-occurrence order without duplicate rows. Empty rosters open safely with a resource-backed state, and zero selections cannot create or extend a lineup.
- Game Workspace reloads and displays the persisted ordered lineup and keeps Set Lineup available throughout the pre-game workflow. Reopening Set Lineup shows existing historical entries separately from currently eligible additions; existing entries cannot be removed on this page, and newly selected Players append after the current highest batting order in click order without rebuilding or duplicating the lineup.
- Persisted lineups are historical snapshots: later Team-roster removal or global Player deactivation does not delete or hide saved rows. Game removal now treats lineup rows as dependent history, blocking those Games while allowing unrelated selected Games to be removed.
- The persisted model remains in Core, SQLite validation and repository behavior remain in Infrastructure, and selection/presentation state remains in MAUI. ViewModels contain no direct SQLite access, and all new runtime text is centralized in `AppResources`.

#### Automated Tests and Validation

- Added 8 focused repository tests covering selection-order persistence and duplicate normalization, active eligibility, Team isolation and all-or-nothing rejection, empty/missing/wrong-Team/existing-lineup failures, append ordering with existing-order preservation, existing-Player no-op protection, failed-append atomicity, same-Player cross-Game participation, historical independence after roster/global deactivation, ordered reload, and mixed safe Game removal.
- The complete suite increased from 143 to 151 tests and passed 151 of 151 with zero failures and zero skips.
- The final Windows target build succeeded with zero errors and 1 `CS8602` warning; Windows runtime validation was not performed. Earlier clean compilation in this story also reported the repository's existing `MVVMTK0045` and XAML compiler `XC0025` warning categories.
- Focused Android tablet validation passed for the corrected pre-game flow: Set Lineup remained available after saving, reopened successfully, and allowed additional eligible Players to append to the existing lineup. No broader Android scenarios are claimed by this closeout.

#### Limitations and Deferred Work

Removing Players from an existing pre-game lineup is deferred to the next focused story. Drag-and-drop and other manual batting-order editing, Order by OBP, Use Previous Lineup, Score Game, in-game add/remove/substitution behavior, scoring, statistics, and Beer Rules also remain deferred.

#### Intended Commit Message

`Implement Game lineup foundation`

### Story 5.1 — Remove Players from Lineup

**Status:** Complete on `task/remove-players-from-lineup`; automated tests, Windows build, and Android manual validation passed

#### Implementation Summary

- Added a temporary checkbox-based removal mode to Set Lineup for persisted pre-game lineup Players. Remove Players remains on the same page, shows the current persisted batting-order numbers with right-side checkboxes, starts with zero selections, uses singular/plural selected-count text, disables Remove Selected until needed and while confirmation or persistence is active, and offers Cancel without persistence. Long-press was not used because the established explicit checkbox pattern is more discoverable and touch-appropriate.
- Add and removal state remain separate presentation concerns. Entering removal mode clears pending Add selections and hides the eligible-Player/Add controls; Cancel and native Back clear removal selections and restore the normal Add workflow. Canceling the native confirmation preserves the removal mode, selections, and count.
- Added a focused transactional `GameLineupRepository.RemovePlayersAsync` operation and result. It deduplicates requested IDs, safely handles null, empty, invalid, and missing IDs, removes only matching rows for the requested Game, reports removed/missing/ignored IDs, and never changes Player, TeamPlayer, or another Game's lineup rows.
- Surviving lineup rows are resequenced contiguously from one in their existing relative order. Removing the entire lineup leaves no lineup rows, makes `HasLineupAsync` false, reloads Set Lineup into its no-lineup state, and naturally removes the existing Game-deletion guard when no other dependent history exists.
- Repository-truth reloads after removal make eligible removed Players addable again; the existing append behavior places them after the current highest batting order rather than restoring an old position. Former Team members and globally inactive Players can be removed from persisted lineups without reactivating their Player or TeamPlayer state and do not become addable unless normal eligibility is restored.
- This remains a pre-game-only destructive operation. No scoring-history lifecycle, scoring, substitutions, in-game lineup changes, or generic lineup-mutation framework was introduced; removal rules will require revisiting when persisted scoring history exists.

#### Automated Tests and Validation

- Expanded `GameLineupRepositoryTests` with 9 test cases covering first, middle, last, multiple, and entire-lineup removal; contiguous order and relative-order preservation; duplicate/non-positive/missing/null/empty input; accurate result reporting; Player and TeamPlayer preservation; historical roster/global-inactive behavior; cross-Game isolation; dynamic Game-removal protection; and eligible re-add-at-end behavior without duplicates or old-position restoration.
- Focused Game-lineup repository validation passed 17 of 17 tests with zero failures and zero skips. The complete suite increased from 151 to 160 tests and passed 160 of 160 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories are `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.
- The final Android manual regression pass succeeded after the emulator was reset. It confirmed normal portrait touch interaction and verified the Remove Players workflow, including entry into removal mode, right-side checkbox selection, selected-count and singular/plural presentation, confirmation and cancellation behavior, persisted removal, contiguous batting-order resequencing, eligible Player re-addition at the end, and native Back exiting removal mode before navigating away. The completed workflow displayed and behaved correctly.
- Before the successful pass, the Android emulator developed an environmental mouse/touch coordinate-mapping problem that also affected the Android launcher: home-screen icons required clicks at incorrect locations, while `adb shell input tap` successfully activated application controls. Android input/window diagnostics showed the application was resumed, focused, touchable, and responsive. Resetting/correcting the emulator restored normal portrait interaction; this was an emulator issue, not a Softball Scorekeeper defect.

#### Limitations and Deferred Work

- Story 5.1.1 — NEXT Game Lineup Status Indicator remains deferred. The planned direction is a compact, readiness-only NEXT Game indicator: zero persisted lineup Players shows a warning such as `⚠ Lineup`, while one or more shows a positive status such as `✓ Lineup`. It will communicate readiness without locking or requiring a lineup.
- Story 5.1.2 — Lineup Manager Refinement remains deferred. The planned direction is to evolve the existing Set Lineup page into the single long-term lineup-management page for adding and removing Players and, later, batting-order reordering and OBP ordering. Game Workspace will display `Set Lineup` when no lineup exists and `Edit Lineup` when one exists. Reordering and OBP behavior remain future work.
- Manual batting-order management remains Story 5.2 work. Drag-and-drop, ordering helpers, previous-lineup reuse, new-Player creation from Set Lineup, scoring, scoring-history-aware lineup lifecycle, in-game changes, substitutions, injuries, defensive positions, statistics, and Beer Rules remain deferred.

#### Intended Commit Message

`Implement pre-game lineup player removal`

### Story 5.1.1 — NEXT Game Lineup Status Indicator

**Status:** Complete on `task/lineup-indicator`; automated tests, Windows build, and Android 10-inch tablet manual validation passed

#### Implementation Summary

- Added a compact, resource-backed lineup-readiness indicator beside NEXT in Team Workspace schedule rows. NEXT Games with zero persisted `GameLineupPlayer` rows show `⚠ Lineup` and NEXT Games with one or more show `✓ Lineup`; non-NEXT Games show no lineup status. Separate semantic descriptions communicate `Lineup not set` and `Lineup set` without relying on symbols or color alone.
- Preserved Story 4.2 NEXT calculation unchanged. Every Game on the next playing date evaluates its own persisted lineup independently, so same-date Games can show different readiness states. No-future schedules have neither NEXT nor lineup-readiness indicators.
- Added a focused `GameLineupRepository.GetGameIdsWithLineupsAsync` batch query so Team Workspace can load lineup presence for the schedule in one database read rather than issuing one query per Game. Readiness remains derived, is not persisted, and does not depend on current roster membership or global Player activity.
- `GameScheduleItem` owns the presentation state, while Team Workspace obtains readiness from repository-backed persisted lineup data. Core `Game` was unchanged, no schema change was required, and no readiness state is persisted.
- Team Workspace reloads Games, Seasons, and lineup presence from repository truth whenever it appears and after its existing schedule mutations. Returning from Set Lineup or Remove Players therefore refreshes warning to positive after the first Player is added, positive to warning after the entire lineup is removed, and retains positive after partial removal without manually patching presentation state or changing lineup, Game-removal, scheduling, lifecycle, or scoring rules.
- Story 5.1.1 changed `FEATURE_HISTORY.md`, `GameLineupRepository.cs`, `GameLineupRepositoryTests.cs`, `AppResources.resx`, `AppResources.Designer.cs`, `GameScheduleItem.cs`, `TeamWorkspaceViewModel.cs`, and `TeamWorkspacePage.xaml`.

#### Automated Tests and Validation

- Added one focused repository test covering empty/invalid input, zero-, one-, and multi-row lineup presence, duplicate requested IDs, per-Game isolation on a shared date, partial and complete removal transitions, and persisted-lineup independence from roster removal and global Player deactivation.
- Focused Game-lineup repository validation passed 18 of 18 tests with zero failures and zero skips. The complete suite increased from 160 to 161 tests and passed 161 of 161 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.
- Android 10-inch tablet manual validation passed. It confirmed that a NEXT Game without a lineup shows the warning state, a persisted lineup shows the positive state, adding the first Player refreshes warning to positive, complete removal refreshes positive to warning, partial removal retains positive, same-date NEXT Games can show independent mixed states, and non-NEXT Games show no lineup indicator. Existing schedule and lineup workflows remained usable, with no clipping, overlap, binding failure, or runtime exception observed. Orientation and theme variants are not claimed because the supplied validation record did not state that they were specifically exercised.
- Final `git diff --check` validation passed.

#### Limitations and Deferred Work

The indicator is informational only and does not require a lineup or introduce completeness, minimum-size, batting-order, scoring, start-Game, or lifecycle validation. Story 5.1.2 Lineup Manager Refinement, Story 5.2 Manual Batting Order Management, drag-and-drop lineup reordering, Order Lineup by OBP, Use Previous Lineup, lineup minimum/start requirements, Score Game, in-game lineup changes, statistics, and Beer Rules remain deferred.

#### Intended Commit Message

`Add NEXT Game lineup status`

### Story 5.1.2 — Lineup Manager Refinement

**Status:** Implementation complete on `task/lineup-manager-refinement`; automated tests and Windows build passed; Android manual validation pending

#### Implementation Summary

- Refined the existing Set Lineup page into the single permanent pre-game Lineup Manager for reviewing the current persisted lineup and performing Add or Remove operations. The existing `SetLineupPage` and `SetLineupViewModel` internal names and route remain to avoid a broad terminology-only rename; the user-facing page title is Lineup Manager.
- Game Workspace now derives its resource-backed Set Lineup or Edit Lineup action from persisted lineup existence reloaded whenever the workspace appears, so adding the first Player and removing the entire lineup update the action without a separate lineup-set flag. It deliberately does not duplicate batting-order numbers or Player names; detailed persisted-order review and management belong exclusively to Lineup Manager.
- Added a focused local Normal/Add/Remove mode enum. Normal mode presents Current Lineup, Add Players, Remove Players, and Done; an empty lineup has a distinct resource-backed empty state, Add remains available, Remove is disabled, and the no-eligible-roster message remains distinct from the no-additional-eligible-Players message.
- Add mode displays only currently eligible non-lineup Team Players with checkboxes and a selected count. Existing persisted lineup Players remain protected from duplicate selection. Pending selection order remains append order, and unchecking then reselecting moves a Player to the end. Add Selected persists immediately through the existing append operation, reloads repository truth, clears temporary state, and returns to Normal mode; Cancel and native Back discard pending additions without persistence.
- Remove mode preserves Story 5.1 behavior: existing historical lineup rows receive distinct removal checkboxes and a selected count, confirmation is required, successful removal persists immediately through the existing transactional removal operation, batting order is resequenced, and repository truth is reloaded before returning to Normal mode. Cancel and native Back discard removal state without persistence, including when removing the entire lineup.
- Done is navigation only, is available in Normal mode for empty or populated lineups, clears temporary selections, and returns to Game Workspace without a final Save Lineup operation. There is no page-level unsaved lineup session.
- Existing Infrastructure behavior was reused without changes. Current eligibility remains limited to active Players with active Team memberships, while persisted historical lineup Players remain visible after later roster or activity changes. Existing per-Game isolation, NEXT readiness derivation, and Game-deletion protection therefore remain repository-driven and unchanged.
- Lineup Manager remains the sole detailed lineup presentation: its current-lineup rows display Player names in authoritative persisted `BattingOrder` and own the Add and Remove operations. Outside the manager, the existing NEXT Game readiness indicator continues providing schedule-level warning or positive lineup status.
- The current lineup row remains a simple persisted-order row with a trailing operation area that can accommodate future Story 5.2 interaction without adding drag handles, reorder controls, or ordering behavior now.

#### Automated Tests and Validation

- No tests were added because this story changes presentation workflow only and reuses already-covered repository behavior. The complete automated test project passed 161 of 161 tests with zero failures and zero skips. A solution-wide test invocation also ran all 161 tests successfully but returned a nonzero process result because the local Android SDK directory could not be found while evaluating the MAUI Android target.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings in the repository's existing `CS8602`, `MVVMTK0045`, and `XC0025` categories; no new warning category was introduced. Windows runtime validation was not performed.
- Android runtime and 10-inch tablet interaction validation remain pending. No Android runtime behavior, orientations, themes, clipping, or touch results are claimed from this implementation session because the Android SDK was unavailable to the command-line environment.

#### Limitations and Deferred Work

Manual batting-order reordering remains Story 5.2 work. Drag-and-drop, up/down controls, OBP ordering, Previous Lineup reuse, new-Player creation from Lineup Manager, lineup minimums, Game-start validation, Score Game, in-game substitutions or injuries, scoring, statistics, and Beer Rules remain deferred.

#### Intended Commit Message

`Refine pre-game lineup manager`

### Story 5.1.3 — Player Entry and Lineup Integration

**Status:** Complete on `task/bulk-player-entry`; automated tests, Windows build, and focused Android manual validation passed

#### Implementation Summary

- Completed rapid consecutive Player entry by adding a resource-backed Save & Add Another action to the existing Create Player page. Global, Team, and Game Lineup contexts share the established First Name/Last Name trimming and validation rules, allow duplicate names, and share one busy guard so Save and Save & Add Another cannot persist concurrently.
- A successful Save & Add Another remains on Create Player, preserves its immutable Team ID and optional Game ID context, and clears First Name, Last Name, and validation/result state. Normal Save preserves existing navigation: global creation returns to Player Manager, Team-scoped creation returns through the existing Add Players workflow, and lineup creation returns one level to Lineup Manager. Cancel performs no persistence and returns one level to the invoking workflow.
- Create Player now interprets query IDs as explicit Global, Team, or Game Lineup context without passing mutable domain objects. Lineup Manager Add mode exposes the existing resource-backed Create New Player action and passes its Team and Game IDs while preserving eligible checkbox selection, Add Selected, Cancel, and Remove-mode separation.
- Added focused transactional `PlayerRepository` operations for Team and Game Lineup creation. They validate the active Team and, for lineup creation, the persisted Game and Team ownership before atomically inserting the active Player and TeamPlayer; lineup creation also inserts exactly one `GameLineupPlayer` at the current highest persisted `BattingOrder + 1`, or order 1 for an empty lineup. Existing lineup rows, other Games, and other Teams are unchanged, and the existing unique Game/Player index remains the final duplicate-lineup safeguard.
- Team-scoped creation now avoids the former partial-success case in which Player creation could succeed before TeamPlayer creation failed. Any exception in the new Team or lineup persistence chain rolls back all inserts, returns a failure result, leaves entered fields visible, and never reports complete UI success. Existing TeamPlayer reactivation behavior for adding existing Players remains unchanged; newly created Players receive their single new active membership in the same transaction.
- No timers, delayed focus calls, platform-specific focus services, or programmatic refocus were added. Successful consecutive entry simply clears the fields and accepts natural platform focus behavior.
- Android validation identified and corrected a return-navigation regression: relative navigation from Create Player re-applied an empty Shell query to the existing Lineup Manager, whose query handler incorrectly cleared its valid Team/Game context to zero and displayed the missing-Game state. Lineup Manager now retains established IDs for an empty return query while still rejecting partial, malformed, or invalid new contexts, then reloads repository truth on appearance. The Player → TeamPlayer → GameLineupPlayer transactions had already completed before navigation and were not changed by this fix.

#### Automated Tests and Validation

- Added 5 focused `PlayerRepository` tests covering atomic Team Player/membership creation, inactive-Team rollback, empty-lineup order 1, repeated creation order after a non-contiguous existing maximum with other-Game isolation, and wrong-Team Game ownership rollback. Focused Player repository validation passed 68 of 68 tests with zero failures and zero skips.
- The complete automated suite increased from 161 to 166 tests and passed 166 of 166 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; no new warning category was introduced. Windows runtime validation was not performed.
- Focused Android validation passed for the confirmed Story 5.1.3 workflows. Global Player Manager Save & Add Another and Team/Roster Save & Add Another each remained open and successfully created multiple Players. In the Game Lineup Create New Player flow, Save & Add Another remained open and persisted the Player, TeamPlayer, and GameLineupPlayer chain; after the targeted empty-query fix, final Save returned directly to the correct Lineup Manager without the missing-Game error, repository truth reloaded, and the newly created Players appeared in the updated persisted lineup. No broader rapid-tap, remaining Cancel, theme, orientation, or layout scenarios are claimed beyond these recorded checks.

#### Limitations and Deferred Work

This story implements rapid consecutive single-Player entry, not true bulk import. CSV, spreadsheet and paste import, roster templates, batch forms, Player editing/merge, jersey numbers, manual lineup reordering, OBP ordering, Previous Lineup reuse, lineup minimum/start requirements, scoring, substitutions, statistics, and Beer Rules remain deferred.

#### Intended Commit Message

`Integrate rapid Player entry with lineups`

### Story 5.1.4 — Global Rapid Entry Pattern

**Status:** Complete on `task/global-rapid-entry`; automated tests, Windows build, and focused Android manual validation passed

#### Implementation Summary

- Extended the resource-backed Save & Add Another repeated-entry pattern from Story 5.1.3 Player creation to Create Team and Add Game. Existing global, Team-scoped, and Game Lineup Player behavior remains unchanged.
- Create Team Save and Save & Add Another now share one authoritative operation for trimming, required-name validation, case-insensitive active and inactive duplicate protection, persistence, persistence-failure handling, and busy-state protection. Normal Save retains existing return navigation; successful Save & Add Another creates exactly one Team, remains on the form, and clears Team Name and validation state. Failed operations retain the normalized entered name and do not reset or navigate.
- Add Game Save and Save & Add Another now share one authoritative operation that uses the existing transactional `GameRepository.CreateForTeamAsync` workflow. Single-Game and doubleheader counts, logical Season resolution/reuse, Team and Season ownership validation, optional Opponent and Field normalization, duplicate-schedule protection, and all-or-nothing Game/new-Season persistence therefore remain repository-controlled and unchanged.
- Successful Add Game Save & Add Another retains Team, resolved Season, Opponent, Field, and the Add Second Game choice while advancing Date by exactly seven days. A newly created or logically reused Season becomes the selected persisted Season and exits Create New Season mode. The next weekly entry searches the existing authoritative Team/Season/date schedule from 6:30 PM in one-hour increments and selects the earliest available slot; a doubleheader requires both the candidate first time and its consecutive one-hour-later second time to be available. Prior manual second-time override tracking is cleared, and the fresh entry restores the normal one-hour second-time default.
- Add Game validation or persistence failure retains the current form and times, performs no carry-forward or time advancement, and shows the existing resource-backed failure state. Both save actions share the existing busy guard, so they are disabled together during the complete persistence and post-success preparation operation.
- No Core model, database schema, repository, or Infrastructure behavior changed. No generic rapid-entry framework or new application layer was introduced.

#### Automated Tests and Validation

- No automated tests were added because the changed behavior is confined to MAUI view models and XAML, while the automated test project intentionally references Core and Infrastructure but not the MAUI presentation project. Existing repository coverage remains the authoritative automated coverage for Team duplicate checks and Game transactions, Season ownership/reuse, duplicate schedules, and normalization.
- Focused Team, Game, Season, and Player repository validation passed 146 of 146 tests with zero failures and zero skips.
- The complete automated suite passed 166 of 166 tests with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; no new warning category was introduced. Windows runtime validation was not performed.
- Final `git diff --check` validation passed.
- Focused Android tablet validation passed. Create Team Save & Add Another remained on Create Team and prepared another Team entry. Add Game Save & Add Another retained Team, Season, Opponent, Field, and the Add Second Game choice; single-Game entry advanced to the same weekday exactly seven days later, selected the standard 6:30 PM slot when available, and advanced to the next available hourly slot when the normal slot was occupied. Normal Save remained intact, and the rapid-entry UX worked correctly on the Android tablet. Custom doubleheader time combinations, rapid multi-tap stress, every duplicate or ownership failure path, and all theme or orientation combinations were not reported as manually exercised and are not claimed.

#### Limitations and Deferred Work

Bulk, CSV, spreadsheet, and paste import; Team and schedule templates; automatic full-season scheduling; Season Manager; lineup and batting-order changes; scoring; statistics; and Beer Rules remain deferred. Broader Android regression coverage for custom doubleheader time combinations, rapid multi-tap stress, every duplicate or ownership failure path, and all theme or orientation combinations was not part of the reported focused validation.

#### Intended Commit Message

`Add global rapid-entry pattern`

### Story 5.2 — Manual Batting Order Management

**Status:** Complete on `task/manual-batting-order`; automated tests, Windows build, and focused Android 10-inch tablet manual validation passed

#### Implementation Summary

- Added direct drag-and-drop batting-order management to the normal persisted-lineup `CollectionView` using the built-in .NET MAUI `DragGestureRecognizer` and `DropGestureRecognizer`. Framework-standard press/hold drag initiation keeps ordinary vertical scrolling available without a Reorder action, separate Reorder mode, Move Up/Move Down buttons, third-party dependency, or platform-specific handler. During a drag, transient item presentation state lifts and highlights the source row, while the current valid target displays an accent insertion gap that displaces rows and shows where the Player will land. Empty and single-Player lineups perform no reorder.
- A centralized cleanup clears the dragged Player ID/reference, drop-target reference, drag-in-progress and drop-completion guards, and every current or tracked row's `IsDragged`, `IsDropGapBefore`, and `IsDropGapAfter` state. It runs before persistence, after repository reload in `finally`, on invalid/canceled/failed paths, on page exit, and after initial page load. The completion guard prevents late gesture callbacks from restoring target state during the asynchronous reload. Because virtualized `CollectionView` rows could otherwise reuse drag styling, the normal row state explicitly restores scale 1, opacity 1, transparent background and stroke, and stroke thickness 0.
- A completed drop identifies the source and target persisted Player rows at the View boundary, then the ViewModel submits the complete resulting Player-ID order to `GameLineupRepository.ReorderAsync` and reloads repository truth. Dragging is accepted only in Normal mode with at least two Players and is unavailable in Add or Remove mode. There is no page-level Save Lineup, Apply Changes, or pending reorder session; Done and native Back retain their established Normal/Add/Remove behavior, and completed drops are already persisted.
- Existing Normal, Add, and Remove modes remain unchanged. Add Players appends after the reordered lineup; Create New Player still creates the Player, TeamPlayer, and lineup relationship and appends it; Remove Players preserves remaining relative order and resequences contiguously. NEXT Game readiness remains based only on lineup existence, Game-removal protection is unchanged, and Game Workspace continues to expose Set Lineup/Edit Lineup without a detailed lineup preview.
- The repository accepts the complete desired lineup order and validates a positive persisted Game, non-null and distinct positive requested Player IDs, and exact equality with the complete persisted lineup Player set. Invalid or missing Games, null input, incomplete lineups, extra/non-lineup Players, duplicate IDs, cross-Game Players, and otherwise mismatched requests are rejected without writes. Reordering does not apply current roster or global Player eligibility, so historical persisted lineup Players remain reorderable after roster removal or global deactivation.
- Complete reorder updates run in the existing SQLite transaction pattern and assign contiguous `BattingOrder` values 1 through N. The existing schema uniquely indexes `(GameId, PlayerId)` but not `BattingOrder`, so direct transactional order updates do not create temporary uniqueness conflicts. Successful operations preserve every `GameLineupPlayer.Id`, `GameId`, and `PlayerId`, modify only `BattingOrder`, and leave other Games unchanged. Failure reloads persisted order and displays a resource-backed error.
- Gesture and transient presentation logic remain at the MAUI View boundary, reorder calculation/workflow remains in the ViewModel and repository layers, and SQLite ownership remains in Infrastructure. No Core change, schema migration, NuGet package, third-party drag/drop dependency, generic reorder framework, Game Workspace preview, readiness-rule change, Game-removal-protection change, or page-level persistence abstraction was added.

#### Automated Tests and Validation

- Added 4 focused repository tests covering two-Player reorder, multi-Player first-to-last and last-to-first movement, contiguous order, identifier preservation, Game isolation, null/invalid/duplicate/missing/extra/other-Game request rejection with unchanged state, and historical Player reordering after roster removal or global deactivation.
- Focused `GameLineupRepository` validation passed 22 of 22 tests with zero failures and zero skips. The complete suite increased from 166 to 170 tests and passed 170 of 170 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; no new warning category was introduced. Windows runtime validation was not performed.
- Focused Android 10-inch tablet manual validation exercised direct drag/drop and reviewed the visual pickup and placement behavior. Validation reproduced stale purple post-drop highlighting, after which the cleanup, completion-guard, and virtualized-row normal-state fix was applied. The final retest was reported as much better and acceptable, and the user approved Story 5.2 closeout and PR preparation. This does not claim exhaustive coverage of every orientation or theme, every canceled/invalid/persistence-failure path, 10–15 Player auto-scroll, historical inactive-Player dragging, or rapid repeated interactions.
- `git diff --check` passed; only line-ending notices were emitted.

#### Limitations and Deferred Work

Story 5.3 Order Lineup by OBP, Story 5.4 Use Previous Lineup, scoring, statistics, substitutions, in-game lineup changes, innings, runners, outs, Undo/Redo, Beer Rules, and lineup minimum or start requirements remain deferred.

#### Intended Commit Message

`Add manual batting order management`

### Story 5.3 — Use Previous Lineup

**Status:** Complete on `task/use-previous-lineup`; automated tests, Windows build, and focused Android tablet manual validation passed

#### Implementation Summary

- Added a resource-backed Use Previous Lineup action to Normal mode in the existing Lineup Manager. Empty targets copy immediately; populated targets require explicit confirmation that the current Game lineup will be replaced while the previous Game and its historical lineup remain unchanged. Cancel performs no persistence. Existing Add Players, Create New Player, Remove Players, direct drag/drop reorder, Done, Set Lineup/Edit Lineup, NEXT readiness, and Game-removal protection behavior remains repository-driven and unchanged; no Game Workspace lineup preview was added.
- Added focused `GameLineupRepository.UsePreviousAsync(teamId, targetGameId)` behavior. Within one SQLite transaction it validates the active Team and target ownership, considers only same-Team Games ordered before the target by `ScheduledDate`, `ScheduledTime`, then `Id`, skips prior Games without persisted lineup rows, and selects the most recent qualifying source. The current Game, future Games, and other Teams cannot qualify, and source selection avoids per-Game lineup queries.
- The source lineup is read as historical persisted truth in `BattingOrder` and row-ID fallback order without current eligibility filtering. When creating the target, the operation retains only globally active Players with an active `TeamPlayer` membership for the target Team, preserves their relative historical order, skips ineligible Players without reactivation, and writes contiguous target `BattingOrder` values `1..N`.
- Copied rows are independent target-owned `GameLineupPlayer` records; source rows, identifiers, ownership, and order are never moved or changed. Full replacement intentionally recreates target rows and does not preserve prior target row IDs. Validation, source lookup, eligibility filtering, target deletion, and insertion share one transaction; target deletion begins only after a valid source and nonempty eligible subset are known. SQLite rollback preserves the prior target on unexpected persistence failure.
- Added focused `UsePreviousLineupResult` outcomes for success, invalid context, no previous lineup, and no currently eligible source Players, with source Game ID, copied Player IDs, and skipped Player IDs/count where applicable. No-source and zero-eligible results perform no writes, including for an existing target. Partial eligibility succeeds with the eligible subset and resource-backed singular/plural skipped-Player feedback. The ViewModel reloads repository truth after every attempted operation and uses its existing busy/confirmation gates; Story 5.2 drag gesture and cleanup code was not modified.
- Story 5.3 changed `GameLineupRepository`, added `UsePreviousLineupResult`, expanded focused repository tests, and updated Lineup Manager ViewModel, View, code-behind confirmation, and string resources. No Core model, schema, migration, NuGet package, generic copy/reorder framework, statistics, scoring, or OBP implementation was added. Order Lineup by OBP is deferred until meaningful Player statistics exist; the next roadmap direction is preparation for Score Game, with OBP ordering returning only after scoring/statistics infrastructure exists.

#### Automated Tests and Validation

- Added 4 scenario-dense `GameLineupRepository` tests covering most-recent prior source selection, persisted and relative order, contiguous target order, source/target row independence and identifier behavior, same-Team and other-Game isolation, skipped empty prior Games, future/current exclusion, same-date and same-time ID ordering, global and membership eligibility filtering without reactivation, partial and zero eligibility, safe no-source behavior, ownership validation, and existing-target replacement/preservation. Focused `GameLineupRepositoryTests` passed 26 of 26 with zero failures and zero skips. There is no focused `SetLineupViewModel` test fixture.
- The complete automated suite increased from 170 to 174 tests and passed 174 of 174 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 99 warnings. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime was not exercised.
- Focused Android tablet manual validation passed for the two primary flows actually exercised: Use Previous Lineup successfully copied and displayed a previous lineup for an empty current Game, and replacement confirmation/replacement successfully replaced an existing current lineup. Android validation did not specifically exercise intervening empty-Game lookup, doubleheader lookup, inactive Player or membership filtering, partial-skipped feedback, source-history inspection after filtering, Add/Create/Remove/drag after copy, NEXT readiness, Game-removal protection, rapid repeated taps, failure paths, alternate themes, or additional orientations.
- `git diff --check` passed; only line-ending notices were emitted.

#### Limitations and Deferred Work

Order Lineup by OBP remains deferred until meaningful scoring and Player statistics exist. Score Game preparation is next; scoring, substitutions, in-game lineup changes, innings, runners, outs, Undo/Redo, statistics, Beer Rules, and lineup minimum or start requirements remain deferred.

#### Intended Commit Message

`Add previous lineup reuse`

### Story 6.0 — Scoring Session Foundation

**Status:** Complete on `task/add-scoring-foundation`; automated tests, Windows build, and focused Android tablet manual validation passed

#### Implementation Summary

- Established the lifecycle boundary `Ready to Score -> In Progress -> Exit -> Resume`. Ready to Score is derived from persisted Game, lineup, and scoring-session state rather than stored separately. Reset Game and Game Over remain deferred.
- Added the focused persisted `GameScoringSession` model and SQLite table. A unique Game association enforces at most one scoring session per Game; the session persists the selected Home/Away side and represents `InProgress` and `Completed` lifecycle states. Story 6.0 creates only `InProgress` sessions. First initialization validates and writes within one transaction and is idempotent: an existing valid session is returned without replacement or duplicate creation.
- Scoring initialization requires a persisted Game owned by the selected Team, no Completed scoring session, and one or more structurally valid persisted lineup rows. Lineup Player uniqueness and contiguous batting order are validated, but no league minimum beyond one Player is enforced and current roster eligibility is not reapplied. Schedule date/time does not affect readiness, so past, present, and future Games may start; missing Opponent or Field values do not block scoring.
- First Score Game requires an explicit Home/Away choice. The side is not inferred from doubleheader position, scheduled time, Game ID, or schedule order; cancel creates no scoring state. The selected side is stored on the session, and Resume loads the existing choice without prompting or reinitializing.
- Once a scoring session exists, the existing `GameLineupPlayer` rows become that Game's immutable official starting-lineup history; no redundant lineup snapshot was added. Infrastructure rejects Add, Remove, Reorder, Use Previous Lineup, and lineup-scoped Create New Player mutations after scoring starts. Use Previous Lineup therefore continues to read historical starting-lineup data. Future substitutions, additions, removals, and order changes belong to scoring-session/live-game state rather than rewriting `GameLineupPlayer`.
- Scoring-session existence independently counts as dependent Game history and blocks normal Game hard deletion, even if lineup rows are otherwise absent.
- Updated Game Workspace state presentation: no lineup exposes Set Lineup; a ready Game exposes Edit Lineup in the existing management area and Score Game directly beneath Game Details; In Progress exposes Resume Game in that same primary-action position and no pre-game Edit Lineup; Completed exposes neither Score Game nor Resume Game.
- Added a dedicated scoring page and ViewModel displaying Team/Game context, Home/Away, status, and the immutable official starting lineup. Normal Teams/Players management navigation is unavailable, and the visible Shell Back arrow is hidden. Android/native Back remains intercepted and routed through the same safe Exit semantics.
- Established a scoring-specific hamburger/menu boundary for future scoring commands. Story 6.0 contains only Exit Game; Game Over, Reset Game, manual overrides, and other future commands are not implemented. The hamburger remains compact independently of menu width, and the menu uses a white surface, black command text, a thin black item-divider pattern, and no persistent purple command background.
- Exit Game preserves the scoring session, `InProgress` state, Home/Away selection, and official starting lineup, then returns to the same Game Workspace. Resume loads that existing session and original lineup without reinitialization or another Home/Away prompt.

#### Automated Tests and Validation

- Added 17 focused `GameScoringSessionRepositoryTests` covering successful and idempotent initialization, unique session ownership, invalid Game/Team/completed/lineup cases, structurally valid lineup rules, schedule and optional-field independence, historical roster state, lineup mutation guards, and scoring-history Game deletion protection. Focused validation passed 17 of 17 tests with zero failures and zero skips.
- The complete automated suite increased from 174 to 191 tests and passed 191 of 191 with zero failures and zero skips.
- The final Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 109 warnings. Warning categories were `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.
- Focused Android tablet validation confirmed that a no-lineup Game cannot start scoring; a ready Game exposes Score Game directly beneath Game Details; Resume Game occupies the same primary location after scoring starts; and past and future Games with valid lineups can start. The dedicated scoring presentation, absent visible Back arrow, scoring hamburger/menu, compact hamburger sizing, white menu surface, removal of the persistent purple command background, black divider treatment, and overall revised scoring UX were reviewed and accepted as looking good.
- Android validation did not explicitly report exercising every repository failure/rollback path, a today Game, missing Opponent or Field, one-Player and malformed-lineup boundaries, Home/Away cancel and repeat-prompt behavior, completed-state presentation, Android/native Back exit, session preservation across application restart, or every orientation/theme; those scenarios are not claimed as manually validated.
- Final `git diff --check` passed with line-ending notices only. No generated build output, database/test-database artifacts, secrets, or machine-specific files were included.

#### Limitations and Deferred Work

Story 6.0 does not implement Reset Game, Game Over, plate appearances, batter results, runner movement, base state, outs, innings or inning transitions, scoring events, score calculation, statistics, OBP ordering, Beer Rules, completed-game correction, or Undo/Redo. The global application dropdown may later retain its white surface while adopting the scoring menu's thin black dividers between item areas; that visual follow-up is not part of Story 6.0.

#### Intended Commit Message

`Add scoring session foundation`

### Story 6.1 — Scoring Home, Scoreboard, and Current Batter Foundation

**Status:** Complete on `task/scoring-home-foundation`; automated tests, Windows build, and focused Android tablet manual validation passed

#### Implementation Summary

- Extended `GameScoringSession` with persisted Home score, Visitor score, and current-batter position. New sessions start `0–0` with the current batter at position 1, and those values remain persisted across Exit and Resume.
- Preserved the core historical boundary: `GameLineupPlayer` remains the frozen official starting lineup, and scoring initialization does not rewrite its rows, IDs, Player IDs, Game IDs, or batting order. Historical starting-lineup semantics remain intact, including Use Previous Lineup reading that frozen Game history.
- Added the separately persisted, scoring-owned `GameScoringLineupPlayer` live batting sequence. First initialization copies Player identity and order directly from the frozen lineup exactly once without reapplying current Player activity or Team membership. Historical inactive or roster-removed Players therefore remain in the initial live sequence. Sequence positions are unique within a session, while Player uniqueness is intentionally not constrained because substitution and re-entry rules remain deferred. Future live-order changes can operate on this state without modifying starting-lineup history.
- Extended the authoritative Infrastructure initialization transaction so the session, Home/Away choice, `0–0` score, live sequence, current position 1, and `InProgress` status are established atomically. Sequence-insert failure rolls back both session and sequence, while repeated and concurrent initialization retains the original session and live state without duplicate rows.
- Added focused `GameScoringState` loading for Resume, returning the persisted session, ordered live sequence, and referenced Players. Resume does not copy the frozen lineup again, reset scores or current position, create another session, or prompt for Home/Away again.
- Added a narrow additive startup compatibility upgrade for existing Story 6.0 databases. sqlite-net adds the new session columns and live table; one transaction preserves existing scoring-session IDs, Game, Team-side/status state, Team/Game data, and every frozen lineup row/ID/order while initializing missing Story 6.1 state. The upgrade is idempotent, resets no installed Team/Game/Lineup data, and rejects malformed frozen legacy lineups without fabricating partial live state.
- Replaced the generic scoring details/starting-lineup presentation with the permanent compact quick-glance scoreboard. HOME and VISITOR remain fixed visual roles; persisted `TeamSide` places our Team on the correct side and the scheduled Opponent on the other. Blank Opponent values display the existing resource-backed `TBD`, and the score labels bind to persisted Home/Visitor values rather than hardcoded zeroes.
- Added a resource-backed CURRENT BATTER section beneath the scoreboard. The ViewModel resolves the Player from persisted current position and `GameScoringLineupPlayer` state, uses the established trimmed nonblank Player-name presentation, and never derives or rebuilds current-batter state from `GameLineupPlayer`. Missing or inconsistent live state uses the existing error feedback without silently rebuilding or fabricating state.
- Preserved Story 6.0 dedicated scoring behavior: the compact scoring hamburger and accepted menu styling remain, the visible Shell Back and global/management navigation remain unavailable, Exit Game keeps the session `InProgress`, and Exit/Resume returns to the same persisted scoring state.

#### Automated Tests and Validation

- Expanded focused scoring coverage from 17 to 24 tests. Added scenario-dense validation for zeroed scores, position-1 current batter, one- and multi-Player live sequences, session ownership and order, unchanged frozen rows/IDs/order, historical inactive and roster-removed Players, persisted Resume state, repeated/concurrent initialization, independent Games, forced transactional rollback, genuine Story 6.0 schema upgrade, upgrade preservation/idempotency, and malformed legacy rejection. Focused tests passed 24 of 24 with zero failures and zero skips.
- The complete automated suite increased from 191 to 198 tests and passed 198 of 198 with zero failures and zero skips.
- The final Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 110 warnings. Warning categories were `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime validation was not performed.
- Android CLI build/deployment was unavailable in the implementation environment because no Android SDK or `adb` was configured. Focused manual validation was subsequently completed through the normal Android tablet workflow. The permanent 10-inch-tablet scoreboard was accepted as large, easy to see, and suitable for scoring: the compact hamburger remained; HOME and VISITOR, team names, prominent scores, and CURRENT BATTER were clear; and clean space remained for later controls. Away mapping visibly placed blank Opponent `TBD` under HOME and our Team under VISITOR with `0–0` and the correct current batter. Home mapping and Exit/Resume behavior were subsequently reported as good.
- Android validation did not explicitly report a one-Player lineup, nonzero persisted scores/current position, malformed live-state error presentation, every long-name boundary, native/system Back, every orientation/theme, or application-restart persistence; those scenarios are not claimed as manually validated.
- Final `git diff --check` passed with line-ending notices only. No generated build output, database/test-database artifacts, secrets, machine-specific files, unrelated formatting churn, or unrelated feature changes were included.

#### Limitations and Deferred Work

Story 6.1 does not implement plate appearances, batter-result buttons, batter advancement, runner/base state, outs, innings or inning transitions, score-changing actions, Undo/Redo, statistics, OBP, substitutions, Reset Game, or Game Over. The next scoring story can begin actual plate-appearance and scoring behavior from the persisted live-state and quick-glance presentation foundation.

#### Intended Commit Message

`Add scoring home foundation`

### Story 6.2 — Plate Appearance Foundation and Batter Advancement

**Status:** Complete on `task/plate-appearance-foundation`; automated tests and Windows build passed, with focused Android tablet validation of live-lineup advancement

#### Implementation Summary

- Added persisted `GamePlateAppearance` history owned by `GameScoringSession`, and therefore by its Game. Each record preserves the historical batter Player ID, batting-position context, and a unique sequential appearance number within the scoring session. Scoring-state loading now returns this ordered history with the session and live lineup.
- Added authoritative transactional plate-appearance completion. The current batter's appearance is inserted and `GameScoringSession.CurrentBatterPosition` is advanced together; insertion or session-update failure rolls back the entire operation so no partial appearance or advancement remains.
- Batter advancement follows the ordered `GameScoringLineupPlayer` live sequence, moves exactly one live position, wraps from the last row to the first, and leaves a one-Player lineup on its sole position. The advanced position is persisted, and subsequent state loads and Resume use that repository truth.
- Preserved historical boundaries. A plate appearance does not rewrite frozen `GameLineupPlayer` history or alter the contents or ordering of `GameScoringLineupPlayer`. Historical inactive or roster-removed Players already present in the live sequence remain valid scoring participants; current Team roster eligibility is not reapplied during completion.
- Story 6.2 plate appearances do not change `HomeScore` or `VisitorScore`.
- Added the plate-appearance table through additive sqlite-net initialization. Existing scoring-session and lineup data is preserved, no destructive reset occurs, and no fake or backfilled plate appearances are created.
- Added the resource-backed **Next Batter** scoring control. The UI deliberately uses plain scorekeeper language while the domain/repository operation remains plate-appearance completion. Next Batter is the explicit user signal that the current play or plate appearance is resolved and scoring may advance. The command retains busy/double-tap protection and reloads authoritative scoring state after success.

#### Automated Tests and Validation

- Expanded focused scoring coverage from 24 to 33 tests. Added scenario-dense validation for multi-batter advancement and wraparound, deterministic persisted appearance history, one-Player lineups, historical inactive and roster-removed Players, independent Games, invalid or completed scoring state, unchanged scores and lineup state, and transactional rollback. Focused tests passed 33 of 33 with zero failures and zero skips.
- The complete automated suite increased from the repository-verified Story 6.1 baseline of 198 to 207 tests and passed 207 of 207 with zero failures and zero skips.
- An earlier external Story 6.1 report referenced 215 tests, but the committed Story 6.1 history records 198 of 198 and the current branch contains nine Story 6.2 additions. Repository history provides no evidence establishing a 215-test baseline, so that figure could not be reconciled and is not treated as a verified repository result.
- The final Windows `net9.0-windows10.0.19041.0` target build succeeded with zero warnings and zero errors. Windows runtime validation was not performed.
- Android CLI build remained unavailable because no Android SDK directory was configured; the attempt failed with `XA5300` despite the installed .NET Android tooling. Focused manual validation on the Android tablet confirmed that the completion/advancement action moved successfully through the live batting lineup. That report does not establish rapid double-tap behavior, one-Player behavior, Exit/Resume persistence, score preservation, wraparound specifically, malformed/failure states, orientation/theme combinations, live-lineup mutation, substitutions, or statistics.
- Final `git diff --check` passed with line-ending notices only. No generated build output, database/test-database artifacts, secrets, or machine-specific files were included.

#### Limitations and Deferred Work

Story 6.2 does not implement batting results, hits, walks, fielder's choices, strikeouts, double plays, runner/base movement, outs, innings, score calculation, statistics, Undo/Redo, substitutions, live lineup mutation, Game Over, or completed-game correction. A future scoring-mode quick-navigation bar is expected to provide SCORE, LINEUP, and STATS destinations: LINEUP will use `GameScoringLineupPlayer` without rewriting frozen `GameLineupPlayer`, and statistics should preferably derive from persisted scoring/result events. Those navigation, lineup-management, result, and statistics designs remain future scope.

#### Intended Commit Message

`Add plate appearance foundation`

### Story 6.3 — Batter Results, Base Runners, and Run Scoring Foundation

**Status:** Complete on `task/batter-result-runner-foundation`; automated tests and Windows build passed, with focused physical Android tablet interaction and layout validation

#### Implementation Summary

- Added strong persisted `BatterResult` values for Single, Double, Triple, and Home Run. `GamePlateAppearance.Result` is nullable so existing Story 6.2 resultless history remains loadable after the additive schema upgrade; no other batter-result types were introduced.
- Added scoring-session-owned committed occupancy for First, Second, and Third using `GameScoringBaseRunner`. Rows persist Player identity rather than display text, enforce one occupant per base and one base per Player within a session, and remain separate from `Game`, frozen `GameLineupPlayer` history, and the `GameScoringLineupPlayer` live batting sequence.
- Added persisted `GamePlateAppearanceRunnerMovement` history. Each completed plate appearance now retains its batter and batting-position context, sequence number, result, participating Player identities, starting locations, final destinations, batter movement, and scored Players. Historical movement rows remain distinct from current committed base occupancy.
- Implemented default movement from the committed pre-play state: a Single advances existing runners one base, a Double advances them two, a Triple scores all existing runners and places the batter on Third, and a Home Run scores every existing runner plus the batter and clears the bases.
- Single, Double, and Triple selection creates a persisted unresolved play without changing committed bases, permanent score, or current batter. The board immediately renders its destinations; Exit/Resume restores the selected result, adjustments, current-play base layout, SCORED Players, and derived run count. Changing the result regenerates the unresolved play from authoritative committed pre-play bases rather than compounding earlier adjustments.
- Added direct correction for Players on occupied bases and in SCORED. Valid destinations are the applicable subset of First, Second, Third, and Scores; `Out` is not supported. Moving between a base and SCORED immediately updates the unresolved board and `RUNS: n`.
- Added the focused Core `PendingRunnerCorrection` calculation and reused it for Infrastructure validation and application. Moving a runner backward onto an occupied base displaces that occupant backward one base and continues until an empty base is reached while preserving Player identity and relative order. The current PA batter is a hard boundary that cannot be displaced backward, and destinations requiring an impossible cascade are excluded and independently rejected by Infrastructure.
- Made Next Batter the sole commit action for unresolved Single, Double, and Triple plays. One Infrastructure transaction inserts the completed PA and result, persists final movement history, replaces committed base occupancy, applies runs to the correct score, advances through the existing live batting sequence, and clears pending state. A failure rolls back the complete operation and preserves the prior authoritative and unresolved state.
- Kept Home Run as an intentional immediate-completion shortcut. Its authoritative transaction discards any unresolved adjustment, rebuilds from committed pre-play bases, scores all runners and the batter, clears bases, persists PA/result/movement history, updates the correct score, advances or wraps the batter, clears pending state, and presents the next batter without an intermediate SCORED/RUNS review or Next Batter tap.
- Offensive runs update `HomeScore` when our Team is Home and `VisitorScore` when our Team is Away; the opponent score is unchanged. Single/Double/Triple runs remain unresolved until Next Batter, while Home Run runs appear after its immediate transaction succeeds. The permanent HOME/VISITOR scoreboard remains authoritative.
- Exit/Resume restores committed Home and Visitor scores, base occupancy and Player identity, current batter, PA results and history, and runner-movement history. It also restores unresolved non-HR result/destination state without advancing the batter.
- Preserved lineup boundaries: `GameLineupPlayer` remains frozen official starting-lineup history; `GameScoringLineupPlayer` remains the live batting sequence; ordinary scoring does not rewrite either set of rows; and batter advancement continues through the existing live sequence. Story 6.3 adds no substitutions or live-lineup management.

#### Scoring Presentation and Interaction

- Replaced the single current-batter/Next Batter presentation with the resource-backed BASE RUNNERS scoring surface in a fan-perspective diamond: Second is top-center, Third left, First right, Home/SCORED bottom-center, and Next Batter centered in the approximate pitcher's-mound position.
- Before result selection, the current batter appears in the bottom Home area and opens the Single, Double, Triple, and Home Run action sheet. After an unresolved Single/Double/Triple, the batter appears only at the resulting base and Home becomes SCORED; tapping that batter by Player identity reopens the result sheet and regenerates a changed result from committed state. The temporary `◆ BATTER` distinction remains pending a follow-up custom-icon refinement; ordinary occupied bases no longer show the temporary RUNNER marker.
- Native base buttons provide reliable Android touch handling and bind display, enabled state, and click identity to the same current-play Player state. The accepted controls use a white surface, black centered Player text, subtle gray border, label above the button, and stable 160×66 dimensions. Empty bases display `Empty`, are disabled/non-interactive, and produce no normal empty-base error. Occupied bases and SCORED Players remain tappable, and no separate RUNNERS, proposed-movement, or textual movement-history section is shown.
- The context-sensitive SCORED area lists Players currently scoring on the unresolved play and keeps them available for correction; it is not historical movement presentation. The compact `RUNS: n` display remains between Next Batter and Home/SCORED.

#### Automated Tests and Validation

- Expanded focused scoring coverage from 33 to 68 tests. Added table-driven repository and Core coverage for result defaults across base states, Home/Away score mapping, committed bases, movement history, unresolved persistence and regeneration, runner corrections, backward cascades and batter boundaries, impossible-destination filtering, transactional rollback, Home Run immediate completion and replacement of an unresolved result, one-Player and wraparound advancement, independent Games, unchanged frozen/live lineup rows, and additive Story 6.2 compatibility. Focused scoring passed 68 of 68 tests with zero failures and zero skips.
- The complete automated suite increased from 207 to 242 tests and passed 242 of 242 with zero failures and zero skips. Tests used isolated temporary SQLite databases.
- The final Windows `net9.0-windows10.0.19041.0` target build succeeded with zero errors and 114 warning instances. Warning categories were `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime validation was not performed.
- Android CLI build remained unavailable in the Codex environment because the Android SDK directory was not configured. The attempt ended with `XA5300`, zero warnings, and one error; this is an environment/tooling limitation rather than a functional Android result.
- Focused physical Android tablet validation confirmed that empty First/Second/Third display correctly, empty bases are non-clickable without error feedback, selecting Single immediately places the batter on First, occupied First shows the correct Player, and tapping an occupied base opens correction with First/Second/Third/Scores wording. The diamond/fan-perspective direction, white base controls, base-runner movement and correction, current-batter interaction from the field, centered Next Batter position, Home/SCORED context, and overall current-play interaction were reviewed and accepted as working and looking good.
- The final 160×66 sizing and removal of the ordinary RUNNER marker were applied after the accepted physical-tablet review and are not claimed as subsequently rechecked on-device. Manual Android validation also did not explicitly establish a complete scored-runner backward cascade after final implementation, bases-loaded impossible-destination filtering, unresolved Exit/Resume after a cascade, committed Exit/Resume, both Home and Away score mappings, Home Run with empty or occupied bases, rapid Home Run taps, changing an unresolved hit to Home Run, one-Player behavior, or portrait/orientation/theme combinations. Automated coverage for related rules is not presented as Android UI validation.
- Final `git diff --check` passed with line-ending normalization notices only. The final diff contained no generated build output, database/test-database artifacts, secrets, machine-specific files, or unrelated changes.

#### Limitations and Deferred Work

Story 6.3 does not implement Walk, batter or runner Out, Error/Reach on Error, Fielder's Choice, Sacrifice Fly, Double Play, Triple Play, authoritative outs or inning state/transitions, opponent offense, substitutions or live-lineup management, SCORE/LINEUP/STATS navigation, statistics including AVG or OBP, Undo/Redo, Reset Game, Game Over, Beer Rules, or completed-game correction. Global game Undo remains deferred, and the removed result-clearing interaction is not Undo. The immediate follow-up direction may refine custom batter and runner icons; Story 6.3 intentionally retains only the temporary `◆ BATTER` marker and adds no custom icon assets.

#### Intended Commit Message

`Add batter results and runner scoring foundation`

### Story 6.4 — Outs and Inning State Foundation

**Status:** Complete on `task/outs-inning-foundation`; automated tests, Windows build, and focused physical Android validation passed

#### Implementation Summary

- Added scoring-session-owned `CurrentInning` and `CurrentOuts`. New sessions begin inning 1 with 0 outs; valid committed out counts are 0, 1, and 2 because a third out performs the half-inning transition instead of persisting 3. The persisted Home/Away `TeamSide` remains authoritative for `Top`/`Bottom` presentation. There is no fixed inning maximum or automatic completion after seven innings, so extra innings continue naturally without putting scoring state on the schedule-oriented `Game`.
- Added the persisted `BatterResult.Out` value without changing existing Single, Double, Triple, HomeRun, or nullable legacy PA compatibility. An OUT with empty committed bases completes immediately: it records the PA, increments outs, advances the current batter, leaves score unchanged, and retains rapid-tap/busy protection. With committed runners and fewer than two outs, OUT becomes a pending play; runners default to their committed bases and may advance, score, or be marked Out before Next Batter transactionally resolves the play. A batter third OUT is immediate and offers no runner-resolution step.
- Added generic pending-runner `Out` destinations for runners from any base and for a batter-runner who first reached safely. Multiple runner Outs may be recorded in one play. Runner Outs remain distinct from `BatterResult.Out`, so later batter-runner advancement or an Out does not rewrite an established hit result: for example, a Single remains a Single if that batter-runner advances to Second or Third or is subsequently thrown Out. Dedicated Fielder's Choice, Double Play, Triple Play, and Sacrifice Fly result semantics were not introduced.
- Expanded completed-play resolution so one transaction owns the PA result, safe movements, runner-Out outcomes, counted runs, outs, inning transition, committed bases, current-batter advancement, and completed-half summary creation. Failure cannot leave a partially advanced inning or score. Inning transitions clear bases and reset outs while continuing to the next live batting position; they do not reset the order, rewrite `GameScoringLineupPlayer`, or touch frozen `GameLineupPlayer`. One-Player lineups safely wrap to the same Player.
- Added explicit third-out run rulings when a runner Out creates the inning-ending out while Players are marked Scores. The scorekeeper decides whether each run counts in lead-runner order. If a lead scoring runner does not count, later scoring runners also do not count and redundant prompts are skipped. This cascade is limited to inning-ending rulings; non-third-out runner outcomes remain independent. Physical movement history remains separate from `RunCounts`, and only counted runs reach the scoreboard and completed-half Runs.
- Preserved Story 6.3 hit behavior: 1B/2B/3B default movement and correction, Home/SCORED interaction, persisted pending hits, movement history, Home/Away score mapping, Exit/Resume, and immediate HR completion. Normal hits do not add outs. A Home Run with two outs leaves two outs and does not end the inning. Selecting OUT over an unresolved 1B/2B/3B discards that proposal and its corrections, restores committed bases, and starts the applicable immediate or pending OUT flow from authoritative state.
- Added persisted `GameScoringHalfInning` summaries when the Team's third out completes. Each record contains the completed inning's Team Runs and Team Home Runs, summary acknowledgement progress, optional opponent Runs and Home Runs, and opponent-entry completion. Completed PAs persist inning context, allowing inning-specific PA and HR history to be derived without transient ViewModel counters.
- Preserved interruption-safe progress through third-out completion, pending Team summary, pending opponent entry, and completed transition. The workflow targets the persisted completed-half ID rather than the already-advanced session inning. Exit/Resume does not replay the third out, duplicate the transition, or fabricate opponent batters or PAs.

#### Opponent Entry and Presentation

- Added the modal opponent half-inning workflow after the Team summary. Runs support **Total Score** (default) or **Runs This Half**; Home Runs support **Total HRs** (default) or **HRs This Half**. Blank stays null and makes no corresponding total change, while entered zero persists as an explicit half-inning zero.
- Total modes derive and persist the actual half value from authoritative cumulative state; This Half modes add the entered half value to cumulative state. Opponent score remains authoritative on the scoring session. Cumulative opponent HRs are derived by summing completed `OpponentHomeRuns` half records, so no duplicate cumulative-HR field was added. Entry modes are UI state and are not persisted because completed history is unambiguous.
- Normal opponent entry is forward-only. A cumulative score or HR value below its authoritative current total is rejected with resource-backed feedback, leaves score/history/transition state unchanged, and keeps the dialog available for correction. Opponent Runs/HRs, score update, and transition completion remain one transaction.
- Corrected Android modal navigation handling so an empty Shell query after modal close preserves already-valid scoring Team/Game context while explicit invalid or partial parameters remain invalid. The opponent dialog uses existing theme-aware error resources; temporary raw exception UI was removed, while structured diagnostics remain for persistence failures.
- Added responsive scoreboard states at the established 900-width breakpoint. Landscape uses reduced vertical padding and spacing, smaller HOME/VISITOR and team-name typography, prominent but reduced score typography, and a compact inning/out line, leaving more vertical space for the unchanged diamond, Next Batter, and current-batter controls. Portrait retains the prior stacked scoreboard presentation.

#### Legacy Compatibility

- Database initialization remains additive and non-destructive. Story 6.3 sessions receive safe inning-1/0-out defaults; historical outs are not reconstructed. Existing scores, current batter, live lineup, committed bases, PA history, and movement history remain intact. Older completed PAs receive the implemented safe inning-context default. Upgrade, reopen, preservation, malformed-state, and idempotence scenarios are covered by automated tests.

#### Automated Tests and Validation

- Final focused scoring validation passed 141 of 141 tests with zero failures and zero skips. Coverage includes inning/out initialization and persistence, immediate and pending OUT paths, third-out transitions, batting continuity, runner Outs and multiple Outs, batter-runner result separation, third-out run rulings and ordering, rollback, opponent entry modes and score mapping, interruption-safe transition progress, navigation/resource regressions, and legacy upgrades.
- The final complete automated suite passed 315 of 315 tests with zero failures and zero skips against isolated temporary SQLite databases. The latest dedicated opponent-entry run before completion passed 29 of 29.
- The final Windows `net9.0-windows10.0.19041.0` build succeeded with zero errors and 114 warning instances. Warning categories remained `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime validation was not performed.
- Android command-line build remained unavailable because the local CLI could not locate the Android SDK and returned `XA5300`. This is an environment limitation, not an Android functional failure. Earlier emulator validation after the navigation/resource correction successfully exercised blank opponent submission, Runs 2 / HR 1 entry, scoreboard update, and inning advancement.
- Focused physical Android validation accepted OUT with empty bases; first/second out progression; a third batter OUT ending the half; inning advancement; out reset; base clearing; Team half summary; blank, Total Score, and Runs This Half opponent entry; the next offensive inning; pending OUT with runners and fewer than two outs through Next Batter; third OUT with runners; and the overall third-out → summary → opponent-entry → next-inning mechanism. Earlier device/emulator work also exercised corrected transition and runner-resolution behavior. This is focused workflow validation, not exhaustive coverage of every base/out/result/ruling, failure, theme, or orientation permutation. An emulator orientation problem was resolved by cold reboot and was identified as emulator/device state rather than an application orientation defect.
- Final `git diff --check` passed with line-ending notices only. The Story 6.4 diff was reviewed as coherent and contained no tracked build output, database/test-database artifacts, secrets, machine-specific files, or stale temporary diagnostics.

#### Limitations and Deferred Work

Walk, dedicated Fielder's Choice, Double Play, Triple Play, Sacrifice Fly, Error/Reach on Error, opponent plate appearances, substitutions, live-lineup management, SCORE/LINEUP/STATS navigation, Player statistics including AVG and OBP, Undo/Redo, Reset Game, Game Over, completed-game correction, Beer Rules, and Manual Overrides remain deferred. Generic runner Out is implemented and is not deferred.

A future **Manual Overrides** scoring-menu workflow must show and allow correction of both teams' scores and cumulative HR totals together, including downward adjustments after incorrect entry or later umpire rulings. Future **Beer Rules** evaluation may suppress opponent-HR entry after the opponent exhausts its usable allowance and one-up permits no additional HR. Neither workflow is implemented by Story 6.4.

#### Intended Commit Message

`Add outs and inning state foundation`

### Story 6.4.1 — Scoring Game Rules and Presentation Foundation

**Status:** Complete and PR-ready on `task/scoring-rules-presentation`

#### Implementation Summary

- Replaced the separate first-start Home/Away prompt with one focused scoring setup modal. Away/Home, a non-negative Home Run Limit (default 5), and 1-Up (default Off) are submitted to the existing Infrastructure initialization boundary, which persists configuration, session state, and the copied live batting sequence atomically. The HR Limit stepper uses the accepted ASCII `-` decrement control. Cancel invokes no persistence, and repeated initialization returns the existing session without replacing its rules or creating duplicate live state.
- Added `HomeRunLimit` and `IsOneUpEnabled` to `GameScoringSession`. Startup detects a pre-rules scoring-session schema and assigns 5/off while preserving the session ID, score, inning/outs, current batter, live lineup, bases, PAs, runner history, pending state, half-inning progress, and opponent data. The compatibility path is additive and idempotent; configured zero remains valid after later application starts.
- Added transactional live Game Rules editing for HR Limit and 1-Up. Invalid limits are rejected without changing the prior values. Rule changes update configuration only and do not rewrite scores, PAs, HR history, bases, or prior results; historical HR totals may therefore exceed a newly lowered limit. HR-limit and 1-Up enforcement remain deferred.
- Extended focused scoring-state reads with HOME and VISITOR HR totals derived from committed history rather than duplicate counters. Our Team's total counts completed Home Run PAs, opponent totals sum completed persisted half-inning opponent HR entries, and persisted TeamSide maps both totals to the correct scoreboard columns.
- Updated the permanent scoreboard with side-by-side VISITOR-left/HOME-right columns while retaining authoritative `TeamSide` mapping of Team and opponent state. Score remains the primary value, with committed `HR total/limit` context, one compact shared `1-UP ON` indicator when enabled, and the inning/out status associated with the scoreboard.
- Kept portrait intentionally composed around the accepted diamond and result controls. Current Batter remains in the lower scoring area, followed by a visually separate compact two-row On Deck/In the Hole list at the bottom of the scrollable scoring content; upcoming batters are not part of the Current Batter card.
- At the existing responsive landscape breakpoint, the page intentionally uses width instead of portrait-style vertical stacking. The compact VISITOR | status | HOME scoreboard places inning and outs on separate center lines with optional 1-Up beneath them; a vertical-divider-separated On Deck/In the Hole rail occupies the left, bases and scoring controls remain central, and the smaller Next Batter control is centered under the bases. The Current Batter card sits directly below it before a result is selected, then hides after Single/Double/Triple creates a pending batter-runner so that Player has one clear editing location on a base or the scored-runner rail. This presentation rule applies in portrait and landscape; the authoritative current PA and `CurrentBatterPosition` remain unchanged until completion, when the next Player appears as Current Batter. Pending scored Players appear by actual Player name in a right-side-divider-separated rail with room for normal multiple-runner scoring. The accepted composition is designed to keep normal landscape scoring visible without routine scrolling, and base-button sizes remain unchanged. Portrait and landscape intentionally use different scoring compositions.
- Current Batter, On Deck, and In the Hole derive from the ordered, persisted `GameScoringLineupPlayer` live batting sequence and current position with natural wraparound, including safe one- and two-Player behavior. Resume reconstructs the sequence from that persisted state, and inning transitions do not reset batting order. `GameLineupPlayer` remains frozen official starting-lineup history, no duplicate upcoming-batter IDs are persisted, and pending plays do not advance the display independently of authoritative state.
- Finalized the live scoring menu as Game Rules, Exit Game, and Game Over with the accepted compact white/black/divider styling; Reset Game and normal management navigation/actions are absent. Exit preserves all scoring state, leaves the Game In Progress, exits dedicated scoring mode, and permits Resume without completing or resetting the Game. Confirmed Game Over requires explicit confirmation and transactionally rejects pending plays or incomplete half-inning opponent workflow, otherwise preserves committed scoring history, marks the session Completed, exits scoring mode, and removes normal Resume availability. Repeated completion is idempotent, and no statistics-finalization subsystem was introduced.
- Added Reset Game only to Game Workspace for an In Progress session; it is absent from the live scoring menu. Two resource-backed confirmations precede one Infrastructure transaction that explicitly removes pending movements/plays, PA runner movements, committed bases, PAs, half-inning/opponent progress, the live scoring lineup, and the scoring session. The scheduled Game and frozen `GameLineupPlayer` rows, IDs, membership, and batting order remain unchanged. Failure rolls back all deletions; completed sessions are rejected. The next Score Game action follows the unified first-start setup again.
- Added resource-backed setup, rules, HR, inning/status, batter-sequence, Game Over, Reset, validation, and confirmation wording. The scoring ViewModel exposes separate resource-backed inning and outs display strings only to support the multiline landscape status panel; the accepted portrait combined line and all authoritative state behavior remain unchanged. Infrastructure continues to own scoring-state changes, authoritative persisted PA/half-inning history supplies HR totals instead of transient UI counters, and setup/rules/Game Over/Reset surfaces retain double-execution protection.

#### Automated Tests and Validation

- Added 15 focused lifecycle/rules cases covering default, zero, and custom initialization; Home/Away and 1-Up persistence; invalid start; repeated start; non-retroactive rule updates; committed Team and opponent HR derivation/mapping; live-sequence wraparound for one, two, and four Players; Game Over blocking, preservation, idempotence, and forced failure rollback; exhaustive scoring-owned Reset deletion with frozen Game/lineup preservation; forced Reset rollback; completed Reset rejection; and legacy rules upgrade/reopen/idempotence. Focused tests passed 15 of 15 with zero failures and zero skips.
- Physical Android testing accepted the corrected ASCII minus control, VISITOR-left/HOME-right scoreboard, portrait Current Batter placement, separate portrait On Deck/In the Hole list, compact landscape scoreboard with separate inning/out lines, landscape upcoming-batter left rail, central scoring composition, right scored-runner rail, actual scored-Player names, and the normal landscape layout. The same testing exposed limitations in the existing runner-correction/cascade architecture. Experimental attempts to extend that architecture during Story 6.4.1 validation were intentionally removed, returning runner correction to its pre-6.4.1 implementation boundary. The failed physical correction scenarios are not claimed as passed; Story 6.4.1 makes no new runner-correction correctness claim because the issue requires the architectural resolver work deferred to Story 6.4.2. No abandoned correction destination matrix, ordered participant resolver, occupied-destination behavior, batter-result reconciliation, or related tests remain in Story 6.4.1.
- Post-cleanup focused validation passed the Story 6.3 scoring-session/play set 68 of 68, the Story 6.4 scoring/session/play/out/runner-out set 141 of 141, and the Story 6.4.1 lifecycle/rules set 15 of 15, all with zero failures and zero skips. The complete automated suite passed 330 of 330 against isolated temporary SQLite databases.
- The MAUI presentation property is not unit-tested because the test project intentionally references only Core and Infrastructure; Windows XAML compilation validates the binding, and physical Android interaction remains required for future UI changes.
- The Windows `net9.0-windows10.0.19041.0` build succeeded with zero errors and 114 warning instances. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime validation was not performed.
- Android command-line build remains unavailable because the local CLI cannot locate an Android SDK and returned `XA5300` with one error and zero warnings. Physical-tablet validation accepted the portrait and landscape composition: compact center inning/outs status, divided functional side rails, centered Next Batter, conditional Current Batter visibility, actual scored-Player names on the right, and the separate portrait On Deck/In the Hole list. Runner-correction behavior is not claimed as validated by Story 6.4.1.

#### Limitations and Deferred Work

Runner correction remains at the pre-6.4.1 implementation boundary and is not considered complete. Walk, Fielder's Choice, Double Play, Triple Play, Sacrifice Fly, Error/Reach on Error, additional batter-result mechanics, opponent PAs, substitutions, live lineup management, statistics/AVG/OBP, Undo/Redo, HR-limit enforcement, 1-Up enforcement, Beer Rules, Manual Overrides, and completed-game correction remain deferred. The HR integer control has no product maximum; it accepts the non-negative `Int32` range rather than imposing an arbitrary softball limit.

The immediate next story is **Story 6.4.2 — Deterministic Base-Runner Resolver Engine**. It will replace heuristic runner-correction/cascade behavior with one authoritative bounded resolver for up to four offensive participants. The resolver will model Player identity, pre-play runner order, batter-runner identity, First/Second/Third/Scores/Out destinations, legal complete arrangements, no runner passing, unique base occupancy, multiple scored runners, impossible-state rejection, and explicitly defined pending-result reconciliation. Correction destination generation and correction execution must consume the same engine so their behavior agrees. Story 6.4.2 is roadmap scope only and is not implemented here.

#### Intended Commit Message

`Add scoring rules and presentation foundation`

### Story 6.4.2 — Deterministic Base-Runner Resolver Engine

**Status:** Complete and PR-ready on `task/base-runner-resolver`; automated tests, the Windows build, and focused physical Android tablet resolver validation passed

#### Implementation Summary

- Replaced the Story 6.3 heuristic correction cascade with one pure, platform-independent Core resolver for the complete pending offensive arrangement. It accepts one to four uniquely identified participants, requires exactly one batter-runner starting at Home, and normalizes immutable pre-play order as Third, Second, First, then batter. The deleted `PendingRunnerCorrection` heuristic is no longer an authoritative or competing path.
- The resolver considers First, Second, Third, Scores, and Out for every participant while holding the requested Player/destination fixed. A complete arrangement is legal only when First/Second/Third have at most one occupant, non-Out participants preserve pre-play order without passing, no participant disappears or duplicates, and multiple Scores and Outs are allowed. Existing non-selected Out/non-Out status is preserved, so correcting one Player cannot silently manufacture or erase another out. Malformed current arrangements are rejected rather than repaired implicitly.
- Legal candidates are ranked deterministically by: fewest changed non-selected participants; least total physical displacement among those participants; preservation of earlier lead participants before later trailing participants; and a stable canonical destination order of First, Second, Third, Scores, then Out. Stable participant ordering supplies the final deterministic tie boundary. Repeated resolution therefore returns the same complete arrangement.
- Added a focused adapter from persisted pending plays and movements to the pure resolver state. Player ID is the stable identity boundary; starting location and current destination remain distinct. Positive hit results require the persisted batter movement. A pending batter Out without a batter movement receives a synthetic in-memory Home-to-Out participant solely for complete resolution and does not create an artificial persisted movement row.
- Correction-menu destination generation and correction execution now consume the same resolver. Execution reloads authoritative pending state inside the transaction, independently resolves the requested correction, applies the existing three-out lifecycle constraint, and persists every existing movement from the returned complete arrangement. The old cascade and separate requested/resolved-destination validators were removed.
- Preserved scoring lifecycle and result boundaries. Runner destination corrections do not rewrite the selected batter result; unusual legal arrangements such as a Triple batter ending on Second remain representable. Changing the batter result still regenerates its defaults from committed pre-play state. Third-out completion, run-ruling order, score/inning transitions, Home Run handling, and current-batter advancement continue through the established Story 6.4 transaction boundary.
- Exit/Resume continues to persist and restore the exact pending result and movement rows. Resolver choices reconstructed after Resume match the choices available before Exit. The resolver replacement required no schema, migration, project-reference, resource, or ViewModel change, so existing databases remain compatible through the prior additive initialization path. The only presentation change was the later scored-runner binding correction documented below.
- Focused Android validation exposed a small portrait-only scored-runner display regression: scored-runner buttons showed the generic `RUNNER` text because the portrait template bound to `DisplayText`, while landscape already bound directly to `PlayerName`. Restoring the portrait binding to `PlayerName` preserved the existing layout, runner click handler, Player identity, ordering, resolver-generated menus, and correction flow. No resolver, ranking, legality, adapter, persistence, lifecycle, or pending-correction behavior changed.

#### Automated Tests and Validation

- Added pure table-driven resolver coverage including every legal current state for eight representative participant sets. The generated matrix exercised 278 legal states and 4,375 participant/destination requests, verifying menu/execution agreement, complete-arrangement legality, stable identity, no passing, base capacity, Out preservation, determinism, and rejection of malformed state. Default and correction coverage includes bases-loaded Single, Double, and Triple arrangements plus exact ranking/tie-break winners. Focused pure resolver tests passed 43 of 43 with zero failures and zero skips.
- Added 21 focused repository/adapter integration tests covering Single, Double, Triple, implicit batter Out, existing Outs and Scores, prior corrections, exact occupied-base and bases-loaded ranking winners, multiple scored runners, unusual result/destination separation, malformed-state rejection without repair, exact pending Exit/Resume reconstruction, and resumed menu parity with the resolver. These tests passed 21 of 21 with zero failures and zero skips.
- Third-out integration coverage verifies exact out identities, two pending runner Outs after one committed out, batter Out identity, lead-runner run-ruling order, inning transition behavior, and no duplicate or omitted participants.
- Final regression validation passed the Story 6.3 scoring-session/play group 66 of 66, the Story 6.4 scoring/session/play/out/runner-out group 141 of 141, and the Story 6.4.1 lifecycle/rules group 15 of 15, all with zero failures and zero skips. The Story 6.3 grouping is two tests smaller than its historical 68 because two direct tests of the deleted heuristic were replaced by resolver and integration coverage.
- The complete automated suite passed 394 of 394 with zero failures and zero skips against isolated temporary SQLite databases.
- The scored-runner presentation regression test passed 1 of 1 and verifies that both portrait and landscape scored-runner templates bind to `PlayerName` and retain `OnRunnerButtonClicked`. The Story 6.4 out/presentation-adjacent group passed 56 of 56, the Story 6.4.1 lifecycle group passed 15 of 15, the pure resolver recheck passed 43 of 43, and the resolver integration recheck passed 21 of 21, all with zero failures and zero skips.
- After the presentation fix, the Windows `net9.0-windows10.0.19041.0` XAML build succeeded with zero errors and 114 existing warning instances. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.
- Focused physical Android tablet validation confirmed normal Single flow and pending-batter presentation after result selection, correct Double runner movement, the expected Triple scored-runner state, appropriately different scored-runner destination menus based on runner order and legal resolver outcomes, correct resolver-generated destination lists, working scored-runner correction interaction, and accepted overall deterministic runner-resolution behavior. This was focused validation rather than an exhaustive exercise of every occupied-destination permutation, forward/backward combination, multiple-Out or third-out ruling combination, the complete Exit/Resume matrix, or the full scoring regression surface.
- Final `git diff --check` passed with line-ending normalization notices only. The reviewed story diff contains no schema or project changes, generated build output, database/test-database artifacts, secrets, machine-specific files, stale heuristic references, or unrelated feature work.

#### Limitations and Deferred Work

Story 6.4.2 is deliberately a resolver-engine replacement and does not add new scoring UI or layout architecture, new batter-result types, statistics, Undo/Redo, HR-limit or 1-Up enforcement, Beer Rules, Manual Overrides, substitutions, live-lineup management, opponent PAs, or completed-game correction. Broader manual coverage of occupied-destination permutations, forward/backward combinations, multiple-Out and third-out ruling combinations, Exit/Resume, and the full scoring regression surface remains deferred; automated coverage is not presented as additional device validation. Those deferred product capabilities are unchanged from Story 6.4.1; **Story 6.4.3 — Scoring Regression Coverage Hardening** is the immediate next scoring story.

#### Intended Commit Message

`Add deterministic base-runner resolver`

### Story 6.4.3 — Scoring Regression Coverage Hardening

**Status:** Complete and ready for final review on `task/scoring-regression-coverage`; automated tests and the Windows build passed

#### Regression Audit and Coverage

- Audited the implemented scoring contracts from Stories 6.0 through 6.4.2 against tests that would meaningfully fail if accepted behavior regressed. Existing behavioral coverage already protects scoring eligibility and Team ownership; one-session/idempotent initialization; frozen-lineup mutation guards; live batting-sequence identity and order; current-batter advancement, wraparound, and one-Player safety; PA identity and transactional completion; pending versus committed hit state; result regeneration; immediate Home Run and basic batter-Out completion; runner movements and score-side mapping; inning/out state, extra innings, third-out transitions and rulings; opponent entry modes; Game Rules and legacy compatibility; Reset deletion/rollback boundaries; and deterministic resolver legality, identity, ranking, malformed-state rejection, menu/execution equivalence, transactional persistence, and pending-state Resume.
- Added a public-workflow close/reopen regression that assembles and exactly restores a mixed authoritative state containing non-zero Team and opponent scores, committed HR history and totals, inning 2 with one out, an advanced batter, committed PAs and runner movements, occupied committed First base, custom Game Rules, completed half-inning/opponent history, and a pending Double. The comparison includes session identity/state, frozen-derived live sequence, Players, PAs, movements, bases, pending play and starting-location-aware movements, half-inning history, and mapped HR totals.
- Added Home and Away Game Over cases that build meaningful history through public scoring operations, then verify Completed status while preserving scores, Team/opponent HR totals, rules, live sequence, PAs, runner movements, committed bases, and completed half-inning/opponent history. The paired cases explicitly protect that our Team maps to HOME or VISITOR according to `TeamSide`, the opponent maps to the opposite side, and a Completed Game cannot start a replacement scoring session or regain normal Resume eligibility.
- Added a Reset/fresh-start/isolation regression. It verifies that Reset removes the selected Game's scoring-owned state while preserving the exact scheduled Game fields and frozen `GameLineupPlayer` IDs, Player IDs, and batting order; leaves another Game's scored state unchanged; and permits a later start to create a distinct fresh session with newly selected rules, zero scores, inning 1, zero outs, batter position 1, a fresh live sequence, and no PA, movement, base, half-inning, or pending history.
- No obsolete tests were removed or replaced. The audit found no production defect, disputed historical contract, or need to change production, persistence, resolver, ViewModel, XAML, schema, or project code.

#### Presentation and Device-Only Boundaries

- Repository and Core tests protect authoritative state behind Ready/In Progress/Completed, Current/On Deck/In the Hole sequencing, pending-batter state, HOME/VISITOR score and HR mapping, rules, runner destination menus, and correction execution. The test project intentionally remains independent of MAUI, so no fake UI framework or brittle tests of labels, XAML hierarchy, pixel positions, styling, or subjective layout were added.
- Physical-device acceptance remains authoritative for portrait scoreboard readability; the landscape VISITOR/status/HOME composition; scored-runner rail and Player-name presentation; accepted base-control sizing and diamond perspective; Current Batter and On Deck/In the Hole usability; hamburger/menu appearance; white surfaces, black text, and dividers; clipping/overlap; and touch-target usability. No Android validation was performed during Story 6.4.3 because production and presentation code did not change; prior recorded device validation remains unchanged.

#### Automated Tests and Validation

- Story 6.4.2 documented a 394-test full-suite baseline. The actual clean Story 6.4.3 branch started at 395 of 395 because the scored-runner portrait/landscape binding regression test was added after that documented full-suite run. Story 6.4.3 added three test methods producing four executed cases and removed zero tests, bringing the suite to 399.
- Final focused validation passed: `BaseRunnerResolverTests` 43 of 43; resolver integration 21 of 21; scoring session/play 66 of 66; scoring out/runner-out 76 of 76; and lifecycle/Game Rules 19 of 19. All focused runs had zero failures and zero skips.
- The complete automated suite passed 399 of 399 with zero failures and zero skips against isolated temporary SQLite databases.
- The explicit Windows `net9.0-windows10.0.19041.0` rebuild succeeded with zero errors and 114 existing warning instances. Warning categories remain `CS8602`, `MVVMTK0045`, and `XC0025`; Windows runtime validation was not performed.
- Final `git diff --check` passed with line-ending normalization notices only. The reviewed diff is limited to focused regression tests and this history entry, with no tracked build output, database/test-database artifacts, secrets, machine-specific files, or unrelated formatting churn.

#### Protected-Behavior Convention

Future intentional changes to protected scoring behavior must update the relevant regression test and `FEATURE_HISTORY.md` deliberately. A failing regression test must not simply be deleted to make the suite pass.

#### Intended Commit Message

`Harden scoring regression coverage`

### Story 6.5 — Walk and Fielder’s Choice Scoring

**Status:** Complete and PR-ready on `task/walk-fc-scorer`; implementation, automated validation, Windows build validation, and physical Android tablet validation are complete

#### Result Semantics and Scoring Architecture

- Added distinct persisted `Walk` and `FielderChoice` plate-appearance results. A focused `PlateAppearanceResultDefinition` registry now centralizes each supported result’s batter default, existing-runner default policy, completion behavior, contextual availability, and guided-resolution requirement. Plate-appearance classification remains separate from runner destination and play-level out count; no Double Play or Triple Play batter-result values or dedicated commit paths were added.
- Walk uses forced-only defaults: the batter goes to First, runners advance only through an occupied force chain, and non-forced runners hold. Selection commits the deterministic play transactionally, persists `Walk` rather than `Single`, and advances the batter immediately without `Next Batter`; a bases-loaded Walk scores exactly the forced runner from Third. Unusual post-Walk advancement remains deferred to completed-play correction/Undo.
- Fielder’s Choice is hidden from the result menu when committed bases are empty and is also rejected by the repository when no existing runner is present. With runners aboard, it remains a distinct persisted plate-appearance classification from `Single` and generic `Out`, defaults the batter to First without guessing an Out, and persists pre-play participant identity and starting locations plus explicit confirmation state. Zero/one-Out resolution proceeds from the lead runner backward through the batter only while a genuine scorer choice remains; two-Out FC uses the dedicated third-Out participant flow.
- Every offered and applied FC destination continues through the existing `BaseRunnerResolver`; Story 6.5 extended its use without redesigning it or duplicating destination legality in the result-definition layer. Resolver displacement reopens confirmation for affected participants. Generic runner-Out, multiple-Out, third-out, run-ruling, half-inning, score, batting-order, and opponent-entry machinery remains authoritative, while FC classification is preserved whether the batter or a runner is retired.

#### Presentation and Player Identity

- The initial empty-base/highlight presentation attempted to blank only reset placeholders and used XAML occupied-state triggers. Follow-up physical Android validation later proved that the loaded-state path still supplied `Empty` and that Android could retain a trigger's occupied colors after a base object was replaced; the final correction is recorded under Physical-Validation Refinement.
- The large blank runner-dialog control was the visually secondary Cancel button despite its non-empty resource text. The unnecessary control and handler were removed; modal/background behavior, FC context, `Choose outcome` guidance, Player-name prominence, destination actions, and Android system-back cancellation remain intact.
- FC plays beginning with two Outs use a dedicated blocking `Who was Out?` dialog containing every existing runner and the batter by exact Player name and original role. Selecting a participant persists only that Player's third-Out movement and immediately invokes the existing Story 6.4 plate-appearance/third-out completion transaction; no ordinary destination prompt or `Next Batter` follows, no trailing safe destinations are invented, and the FC PA classification is preserved.
- FC plays beginning with zero or one Out retain resolver-guided ordering, forced-outcome propagation, and automatic single commit. The existing third-out run-count ruling remains unchanged for hit and other supported pending-play scenarios where the current runner model can record possible scored runners before another runner is Out.

- Added one shared blocking, background-dimming runner-destination modal used by normal pending-play corrections, FC guidance, scored runners, pending batter-runners, and existing runner-Out edits. It displays the persisted Player name as a centered, bold 28-point header separated from the destination buttons; the buttons contain only destinations returned by the existing resolver-backed ViewModel/repository workflow, and Android system-back cancellation remains supported.
- Added resource-backed Walk and Fielder’s Choice labels. Existing portrait and landscape scoring layouts, runner buttons, tap entry points, Player identity/order, pending corrections, and scored-runner presentation remain otherwise unchanged.

#### Physical-Validation Refinement

- Initial physical Android testing exposed that the guided FC menu could derive choices from an over-advanced placeholder destination, omitting a legal Second destination for a runner who started on First. Guided availability and execution now enumerate complete arrangements through `BaseRunnerResolver` from immutable pre-play participant identity and starting locations, constrained only by already confirmed scorer choices, FC result semantics, and the existing three-out limit.
- After every scorer choice, the same resolver arrangements are compared across every unresolved participant. Any destination shared by every remaining arrangement is persisted automatically, and propagation continues without another modal; a participant remains unresolved and is prompted only while multiple legal outcomes remain. Once every outcome is explicit or uniquely inferred, FC invokes the existing transactional commit machinery exactly once and advances the batter without a redundant `Next Batter` tap. PA classification remains Fielder’s Choice regardless of which participant is Out or where the batter finishes.
- Unresolved placeholder destinations are no longer presented on the diamond as confirmed outcomes. Confirmed pending destinations are shown, while unresolved existing runners remain at their committed pre-play locations and unresolved batter-runners remain at Home. Runs and scored-runner cards likewise include only confirmed pending outcomes.
- The runner-destination modal remains blocking and background-dimming, but is wider and uses a compact wrapped action layout. The Player name remains the most prominent centered element, with resource-backed `Choose outcome` guidance, compact FC context, and a divider.
- Follow-up physical Android validation found two presentation defects after FC auto-completion: loaded empty bases still rendered the `Empty` resource text, and First retained its occupied/yellow trigger styling after its runner advanced to Second and the batter was Out. Persisted final base state was already correct, so scoring semantics, `BaseRunnerResolver`, FC rules, Walk rules, and out/third-out handling were not changed for this correction.
- Base text, occupied state, background, and border now come from the same immutable current-occupant presentation object. A missing occupant produces a blank Player name, a null Player id, `IsOccupied == false`, and neutral colors; a current occupant produces the exact Player name, a non-null Player id, `IsOccupied == true`, and occupied colors. The base buttons bind colors directly to that replacement object instead of relying on Android-retained XAML trigger setters, so post-auto-commit reload, pending display, Exit/Resume, and half-inning clearing all render from the current displayed occupant.
- All previously pending pinpoint Android scenarios subsequently passed on the physical target device before the final presentation-only refinement below. This included Walk auto-completion, blank/neutral empty bases, exact occupied Player identity and highlight, runner-dialog presentation, FC runner/batter/multiple-runner/third-out flows, automatic completion, cleared third-out bases, and the Single/Double/Triple/Home Run/Out smoke checks.

#### Final Presentation-Only Refinement

- Portrait places On Deck and In the Hole inside a compact, resource-backed `UPCOMING` card beside a matching `OUTS THIS INNING` card. Android landscape review found the first landscape refinement stacked Outs beneath Upcoming on the left despite available right-rail space. Landscape now preserves Upcoming in its original left position and places a compact `OUTS THIS INNING` section at the top of the existing right information rail, sharing that rail vertically with pending scored-runner content while leaving the scoreboard, center diamond, dividers, and Next Batter composition unchanged.
- `OUTS THIS INNING` is reconstructed on every normal scoring load from existing persisted plate appearances and runner movements for the session's current inning. Normal batter Outs use the persisted Out plate appearance; FC and other runner/batter retirements use the actual persisted Out movements. Rows retain persisted play/movement order and repeated Player identities are not deduplicated.
- Exit/Resume therefore reconstructs the same current-half list without new persistence. Advancing to the next half visually produces an empty list because the session inning changes while prior plate appearances and movements remain persisted; Reset removes the existing scoring history and the ViewModel reset also clears presentation state.
- This panel records Players physically retired, not batter responsibility. Beer Rules, batter-responsibility evaluation, and dedicated Double Play/Triple Play behavior remain deferred, and no scoring, resolver, FC, Walk, persistence, out, or third-out semantics changed.
- Portrait and landscape bind the same `OutsThisInning` collection and resource-backed row format; no orientation-specific state exists. The landscape section retains its heading with zero Outs but renders no placeholder or `Empty` row.
- Android landscape review also found that a pending batter-Out presentation could leave the resolved batter card visible while portrait appeared cleared. The shared presentation derivation now treats a pending result without a batter-movement row, such as Out, as a resolved batter; confirmed safe or Out batter movements are likewise resolved, while no pending result and unresolved FC batters remain visible. Both orientations continue binding the same `IsCurrentBatterVisible` property, and no landscape-specific rule or visibility override was added.
- Portrait XAML structure and positioning were not changed by the right-rail/visibility correction. Final physical Android revalidation confirmed the corrected landscape right rail, batter visibility, half-inning clearing, portrait cards, and rotation behavior without changing scoring semantics.

#### Statistics and Boundary Decisions

- Story 6.5 records enough distinction for later statistics: Walk and Fielder’s Choice remain separate persisted plate-appearance classifications, and actual runner/batter retirements remain represented by movement and play-level Out data. The `OUTS THIS INNING` panel intentionally shows Players physically retired rather than assigning batter responsibility.
- AVG, OBP, hits, at-bats, Beer Rules, batter-responsibility evaluation, and dedicated Double Play/Triple Play behavior are not calculated or implemented here. Future statistics work must consume the persisted classifications and movement history rather than reinterpret FC as `Single` or generic `Out`.

#### Automated Tests and Validation

- Added 35 focused cases covering Walk force configurations and immediate completion; FC availability, normal zero/one-Out resolver guidance, propagation, classification, identity, final base occupancy, and duplicate-commit protection; dedicated two-out runner/batter/bases-loaded participant selection; isolated third-Out movement persistence; inning/batting-order transition; result regeneration; internally consistent empty/occupied base presentation bindings; both scoring modal contracts; Upcoming identity; current-half normal/FC Out identity and order; duplicate identities; Exit/Resume reconstruction; half-inning reset; Reset cleanup; shared current-batter resolution visibility; and portrait/left-rail/right-rail landscape contracts.
- Final focused Story 6.5 validation passed 35 of 35. The broader scoring/resolver regression group passed 260 of 260. The complete automated test project passed 434 of 434 with zero failures and zero skips against isolated temporary SQLite databases. One earlier parallel full-suite invocation failed before test execution because it contended with the regression build for `SoftballScorekeeper.Core.deps.json`; subsequent full-suite runs, including this final sequential run, passed cleanly. The earlier solution-wide test command ran the then-current 426 tests successfully but returned failure because its MAUI Android target could not locate the Android SDK (`XA5300`).
- The explicit Windows `net9.0-windows10.0.19041.0` build passed with zero errors and 114 existing warning instances in the established `CS8602`, `MVVMTK0045`, and `XC0025` categories. Windows runtime validation was not performed. Local Android compilation could not run because the Android SDK is unavailable (`XA5300`); physical target-device validation was performed separately and is recorded below.
- Final repository hygiene passed: `git diff --check` reported no whitespace errors, only line-ending normalization warnings. Final status/diff review found only Story 6.5 implementation, tests, resources, presentation, persistence/domain, and this feature-history update; no unrelated files or generated artifacts were introduced.

#### Physical Android Tablet Validation

- Empty-base, runner-on-First, and bases-loaded Walks applied forced-only movement, committed immediately, scored the forced loaded-bases run, and advanced without `Next Batter`.
- Empty bases displayed no `Empty` text, occupied bases showed the exact Player with the occupied highlight, empty bases remained neutral, and the runner dialog contained no blank control.
- Runner-to-Second/batter-Out and runner-Out/batter-to-First FC scenarios persisted FC, displayed final occupancy correctly, and advanced automatically without a redundant batter modal or `Next Batter`.
- Multiple-runner zero/one-Out FC resolution prompted only while ambiguity remained, propagated unique outcomes, committed automatically, and displayed final occupied bases correctly.
- Two-Out runner and bases-loaded FC dialogs showed exact participant identities; selecting the retired runner or batter immediately completed the half, cleared bases/highlights, retained one FC plate appearance and the correct Out, and entered the correct next-half state.
- Single, Double, and Triple retained their pending/`Next Batter` boundary; Home Run remained immediate; established Out behavior remained unchanged; portrait/landscape checks showed no regression.
- Final landscape revalidation passed with Upcoming retained on the left and `OUTS THIS INNING` in the right rail. Zero Outs showed the heading without placeholder rows; one and two normal Outs showed exact Player identities in play order with no clipping or overlap and a balanced center.
- Batter-Out presentation cleared the resolved batter card and tap hint consistently with portrait without changing batting-order behavior. FC runner-Out presentation listed the actual retired runner while preserving the accepted FC batter presentation.
- The third Out completed the established half-inning transition and cleared stale Out rows. Scores, HR totals, inning/Outs, 1-Up, Upcoming, Current Batter, Next Batter, the base diamond, scored-runner rail, and vertical dividers showed no landscape regression.
- Rotation back to portrait confirmed the `UPCOMING` and `OUTS THIS INNING` cards remained aligned, readable, correctly identified, and unclipped. These accepted target-device results complete the Android acceptance standard for Story 6.5.

#### Deferred Scope

`Out` retains its Story 6.5 meaning that the runner did not successfully reach the destination being resolved. `Out Attempting to Advance`, Out-at-base variants, and safe-base-then-Out movement history are explicitly deferred to the next scoring story.

Dedicated Double Play/Triple Play presentation, sacrifice results, HR-limit/dead-ball results, errors/reached-on-error, Beer Rules, batter-responsibility evaluation, and statistics remain deferred. Future DP/TP behavior must build on Fielder’s Choice plus generic multiple-runner-Out resolution rather than add DP/TP batter-result classifications.

#### Intended Commit Message

`Add Walk and fielder's choice scoring`

### Story 6.5.1 — Runner Out Attempting to Advance

**Status:** Complete and PR-ready on `task/651-Runner-Out-Attempting-Advance`; implementation, automated validation, Windows build validation, and focused physical Android tablet acceptance are complete

#### Persisted Semantics and Compatibility

- Added the distinct historical outcome for a runner who safely reaches a base during the current play and is then retired attempting the immediately following destination. Ordinary `Out` retains its Story 6.5 meaning that the runner did not safely reach the destination being resolved and continues to persist with no safe or attempted destination metadata.
- Added nullable `SafeDestination` and `AttemptedDestination` fields to pending and completed runner-movement rows. An attempted-advance record retains exact Player identity, original `StartingLocation`, the safely reached First/Second/Third destination, the immediately following attempted Second/Third/Scores destination, and authoritative final `Destination = Out`. `Scores` remains the internal representation for the user-facing Home destination; no destination beyond Home is invented.
- SQLite initialization adds the two nullable columns to existing movement tables without recreating the database. Pre-6.5.1 rows default both fields to null, so existing ordinary Outs and all prior Single/Double/Triple/Home Run/Walk/FC history retain their original interpretation. Pending choices and committed history survive database close/reopen with the same Player, start, safe destination, attempted destination, and final Out state.
- The additive representation preserves the information later Undo/correction work will need without adding correction UI, Undo/Redo, timestamps, or a chronological play-event engine. Reset continues deleting the scoring-session-owned movement rows through its existing transaction, and completed-game history retains the new fields like every other committed movement.

#### Resolver, Completion, and Scoring Behavior

- Added the focused `RunnerOutcomeChoice` domain value and extended `BaseRunnerResolver` at its existing arrangement boundary. A safe First permits only Out Attempting Second, safe Second only Out Attempting Third, and safe Third only Out Attempting Home (`Scores` internally). The safe destination must be a genuine forward advance from the pre-play starting location; holding the pre-play base, backward combinations, mismatched next destinations, and attempts beyond Home are rejected.
- Normal pending plays offer the attempted-Out choice only from the runner's current legal safe proposal. Guided zero/one-Out FC resolution requires the participant's exact movement to be persisted as confirmed at the safe destination during the active FC; it then uses the same compatible resolver-arrangement predicate for menu display and execution. Unresolved hypothetical arrangements and a base occupied before the active play cannot manufacture a safe destination. A resolved non-third-Out FC remains active while any current-play participant is safely on First, Second, or Third or has Scores as the pending destination, so that Player remains available for legal same-play correction. Next Batter explicitly commits that legal arrangement when the scorer declares the play finished. FC's dedicated two-Out `Who was Out?` path remains unchanged. No second movement engine or alternate FC completion path was introduced.
- Selecting the new outcome persists the safe and attempted destinations while finalizing the participant as Out. Existing resolver correction protects identity, runner order, no passing, unique final base occupancy, multiple Scores/Outs, out limits, and menu/execution agreement. The ultimately retired runner never occupies the safely reached base in committed or pending live-base presentation, so another runner may legally finish there without stale occupancy or highlight state.
- The existing completion transaction remains authoritative for the plate appearance, movement history, score, bases, batter advancement, and exactly one Out per retired Player. Batter-runners are supported without rewriting the recorded PA result: for example, a Single batter may safely reach First and be Out attempting Second while the PA remains Single. Walk remains distinct and keeps its accepted forced-only immediate-completion behavior; because that workflow exposes no pending continuation, Story 6.5.1 does not add a post-Walk adjustment step. FC attempted-advance movements likewise retain `FielderChoice` classification.
- Attempted-advance Outs feed the existing persisted Out movement history, so the actual retired Player appears exactly once and in play order in `OUTS THIS INNING`, including Exit/Resume reconstruction and duplicate identities across separate plays. Resolved non-terminal FC state, including exact scored-Player identity, survives Exit/Resume without advancing the batter or committing score, Outs, or a plate appearance. A single Core terminality predicate permits repository-owned auto-completion only when the FC arrangement is complete and resolver-legal, no movement needs confirmation, every participant is retired, no safe or scored participant remains editable, and the play produces fewer than three Outs. The predicate is based on typed pending outcomes rather than physical base occupancy. An FC whose resolved movements create the third Out remains excluded and completes through the existing third-out machinery; half-inning completion, run rulings, base clearing, outs reset, inning advancement, batting continuity, and opponent workflow remain unchanged.
- Safe/attempted destination history does not infer play timing. When a runner Out attempting to advance creates the third Out while another runner may have scored, the existing scorer-controlled third-out run-count ruling remains required and authoritative.

#### Presentation

- The accepted blocking, background-dimming runner dialog and prominent exact Player-name header remain unchanged. Its outcome buttons now carry typed `RunnerOutcomeChoice` values and add resource-backed `Out Attempting Second`, `Out Attempting Third`, or `Out Attempting Home` labels only when that specific relationship is legal. Attempted-Out buttons receive additional width for the longer softball-friendly labels; the scoring screen composition, portrait/landscape rails, scoreboard, Current Batter, Upcoming, Outs This Inning, bases, and scoring menu were not redesigned.

#### Statistics and Deferred Boundaries

- Plate-appearance classification remains separate from final runner outcome, preserving future hit, Walk, FC, AVG, OBP, and at-bat derivation. Current-play correction ends when Next Batter, all-retired terminal completion, or third-Out completion commits the play; reopening or editing committed history belongs to future Undo/correction work. Story 6.5.1 adds no post-commit editing, statistics, or batter-responsibility calculation.
- Full chronological play-by-play, automatic run timing, dedicated Double Play/Triple Play classifications, Sacrifice Fly, Error/Reach on Error, HR-limit or 1-Up enforcement, Beer Rules, substitutions, live lineup management, global Undo/Redo, and completed-game correction remain deferred.

#### Automated and Platform Validation

- The pre-change scoring/resolver baseline passed 260 of 260. Added 18 focused cases covering the three legal safe/attempted relationships; invalid, backward, held-base, mismatched, and beyond-Home rejection; menu/execution agreement; batter Single and existing-runner integration; exact persisted identity/start/safe/attempted/final-Out state; final bases; exactly-once out counting and Outs This Inning; FC classification; immediate Walk compatibility; third-out transition and retained run ruling; pending and committed reload; old-schema additive migration; ordinary-Out compatibility; resource-backed UI labels; and the physical-device FC disagreement described below.
- After the final scored-runner editability refinement, the combined Walk/FC and attempted-advance focused group passed 59 of 59. The scoring/resolver-named regression group passed 268 of 268. The complete automated suite passed 458 of 458 with zero failures and zero skips against isolated temporary SQLite databases.
- The Windows `net9.0-windows10.0.19041.0` non-incremental build passed with zero errors and 114 existing warning instances in the established `CS8602`, `MVVMTK0045`, and `XC0025` categories. Windows runtime validation was not performed.
- Android command-line compilation remains unavailable because the local Android SDK is not configured; the build failed with `XA5300`, zero warnings, and one error before application compilation.

#### Physical Android Findings, Corrections, and Final Acceptance

- Physical Android validation successfully exercised Single → safe Third → Out Attempting Home. The same validation then exposed an FC menu/execution disagreement: after an FC presentation placed a runner at Second, selecting `Out Attempting Third` returned the existing failed-save message and left the runner on Second.
- Repository-state investigation found that the failing one-runner FC had already been auto-committed after its guided outcomes became unique. That removed the current-play safe destination and advanced the batter before the scorer could extend the runner's movement. Treating the runner's resulting Second-base occupancy as part of the next play would be invalid, but hiding the choice alone would have preserved the premature FC lifecycle.
- The first targeted correction kept resolved non-third-Out FCs pending so an explicitly confirmed First-to-Second movement could continue to Third or Out Attempting Third. Subsequent review removed a redundant Next Batter tap when every participant was terminal, initially treating both Scores and Out as terminal outcomes. Final boundary review established that Scores remains editable pending-play state: a scorer may still move that exact Player back to a legal base before commit. The final predicate therefore auto-completes only an all-retired legal FC; any safe or scored participant requires Next Batter, while Out and Out Attempting are terminal for that Player. Menu and execution still use the same exact-current-play attempted-advance predicate, the PA remains Fielder's Choice, and Exit/Resume restores genuinely pending safe/scored state. A runner who merely occupied Second before the active play still cannot gain Out Attempting Third from that occupancy; ordinary Out remains available. Resolved FC movements that create the third Out continue through the authoritative existing machinery, including run-count rulings and the two-Out `Who Was Out?` route. Rejected invalid states do not mutate score, outs, bases, or pending movements.
- Final physical Android validation accepted the corrected lifecycle and pending/commit boundary. FC First-to-safe-Second remained pending and editable until Next Batter; the same runner could continue to Third and commit there; First-to-safe-Second-to-Out Attempting Third saved without error, removed the runner, counted the Out once, preserved FC classification, and auto-completed when every participant was retired. A later play did not derive Out Attempting Third from pre-play Second-base occupancy, and ordinary Out remained available.
- Physical Android also accepted the scored-runner boundary with one Out: after a runner from Third scored and the batter became Out Attempting Second, the scored Player remained visible/editable, the play and current batter remained pending, the Player could be corrected to a legal base and restored to Scores, and Next Batter committed the score and PA. The dedicated two-Out `Who Was Out?` FC path still ended the half automatically. Pending FC state, current batter, and editable runners restored through Exit/Resume. Single to safe Third to Out Attempting Home, ordinary FC, ordinary runner Out, Walk, portrait, and landscape smoke checks were accepted with no observed clipping, overlap, or stale base state.

#### Physical Android Tablet Validation — Accepted

- Accepted safe-runner pending editability, continued safe advancement, explicit Next Batter commit, and all-retired non-third-Out FC auto-completion.
- Accepted exact attempted-advance execution, persisted FC classification, authoritative exactly-once Out counting, final base removal, and the pre-play occupancy safeguard.
- Accepted scored-runner pending editability and same-play correction before Next Batter; post-commit correction remains deferred to future Undo/correction functionality.
- Accepted dedicated third-Out FC completion and Exit/Resume restoration of pending FC state.
- Accepted Single attempted-advance, ordinary FC, ordinary runner Out, Walk, portrait, and landscape smoke coverage. Multiple-scored-runner physical validation was not separately reported and is not claimed.

#### Intended Commit Message

`Add runner out attempting to advance`

### Story 6.6 — Double Play and Multi-Out Play Resolution

**Status:** Complete and PR-ready on `task/double-play-resolution`; implementation, automated validation, Windows build validation, and focused physical Android tablet acceptance are complete

#### Persisted Classification and Multi-Out Architecture

- Added explicit persisted `DoublePlay` and `TriplePlay` batter results. They remain distinct from ordinary `Out` and `FielderChoice`; individual retirements continue to use the shared pending runner-movement and completed plate-appearance movement records.
- Extended the existing result-definition and `BaseRunnerResolver` workflow with a required explicit multi-out retirement count and minimum participant count. Double Play requires two offensive participants and at least two authoritative Outs remaining; Triple Play requires three participants and all three authoritative Outs remaining. Both use the same bounded multi-out path rather than separate DP/TP engines, and unavailable classifications are rejected by the repository as well as omitted from the batter-result menu.
- Added nullable `OutOrder` to pending and completed movement rows. Explicit DP/TP retirees retain stable internal values for additive-schema compatibility, but those values are deterministic implementation details and do not represent scorer-entered defensive chronology. A remaining live DP participant later retired through the runner updater receives the next authoritative chronological value, subject to the existing three-Out ceiling. Exact Player identities, starting locations, final destinations, safe/attempted destinations, and final base state remain on the existing typed movement records. SQLite initialization adds the nullable columns to existing databases without recreation, and older rows remain compatible with null order.
- Exact retiree identity is collected only for a zero-Out Double Play, where the inning continues and the survivor/base state matters. When that continuing DP has exactly two participants, both auto-retire; otherwise the scorer selects exactly two retired Players by actual name without ordering them. Tap order does not affect membership or scoring behavior. Exit/Resume reconstructs the same classification, identities, batter, inning, Outs, score, and editable survivor arrangement; attempted-advance metadata remains owned by the separate runner-update path.
- A one-Out Double Play and every valid zero-Out Triple Play are atomic inning-ending classifications. They persist the batter PA, supply the two or three authoritative Outs through the third-Out boundary, reject all runs from that PA, clear the bases through the existing half-inning transition, advance batting-order continuity, and enter the existing summary/opponent-entry flow immediately. They create no pending retiree/survivor resolution and persist no fabricated participant movements, identities, or chronology. Future Undo must reverse any committed DP/TP as one PA rather than individual Outs.

#### Resolution, Completion, and Third-Out Boundaries

- Continuing zero-Out DP selection fixes the selected retirement membership as ordinary Outs, then feeds the complete participant arrangement through `BaseRunnerResolver`. Selected retired Players are terminal and cannot be edited. Remaining safe or scored participants retain Story 6.5.1 editing behavior and require `Next Batter` to commit. If the runner updater later retires a remaining live participant as `Out Attempting`, its safe/attempted destinations and next chronological `OutOrder` persist under the unchanged DP classification. A resolver-legal continuing DP containing only Outs auto-completes without redundant `Next Batter`, while a safe/scored editable survivor keeps the play pending.
- DP/TP survivor defaults use immutable pre-play occupancy to identify the continuous force chain beginning at First. A surviving First runner advances to Second; a surviving Second runner advances to Third only when First and Second were both occupied; a surviving Third runner defaults to Scores only when First, Second, and Third were occupied. The chain never crosses an empty starting base, retired Players remain terminal, and non-forced survivors hold their starting base. The complete proposed arrangement is validated by `BaseRunnerResolver`; safe/scored survivors remain editable, resolver correction may reposition another survivor to preserve legality, and Exit/Resume plus `Next Batter` preserve the scorer-edited arrangement without regenerating defaults.
- Double Play does not assume batter plus runner on First. The continuing flow supports runner-on-Second or runner-on-Third plus batter, two existing runners retired while the batter remains safe, and resolver-legal surviving or scoring runners. Triple Play uses the atomic inning-ending path regardless of whether three or four offensive participants exist.
- Multi-out PA classification now requires the full classification retirement count to fit before the third-Out boundary. Double Play is unavailable with two existing Outs, and Triple Play is unavailable with one or two existing Outs. The earlier Story 6.6 behavior that preserved `DoublePlay`/`TriplePlay` while recording only the remaining authoritative retirement count was rejected during physical Android validation and removed deliberately; no partial multi-out PA or fictional fourth Out can be created through either menu or repository entry.
- A small Core `ThirdOutRunRulingEvaluator` examines the PA classification plus resolved participant outcomes, batter identity, safe/attempted-base metadata, and only meaningful chronological events. It returns automatic no-run for an explicit one-Out DP or zero-Out TP, allowing the repository to commit the atomic transition without UI result-name checks. For other plays, stable internal values assigned among unordered continuing-DP members remain ignored for third-Out identity, and a known batter-runner authoritative third Out before First remains automatic no-run.
- Scorer timing input remains authoritative when unrelated recorded facts are ambiguous, including an `Out Attempting` third retirement after a runner reaches Home. A later runner-updater Out remains a distinct chronological event and can be identified as Out #3 independently of unordered continuing-DP membership. Movement-to-`Scores` and legal run count remain separate persisted facts. Existing third-Out processing remains authoritative for half-inning summary creation, base clearing, Outs reset, inning advancement, batting-order continuity, opponent entry, and genuinely ambiguous run-count rulings. Continuous-chain movement is a deterministic scoring convenience only; defensive timing, force disappearance, tag-up semantics, and exact force-play classification remain deferred.
- `OUTS THIS INNING` reads persisted movement history and shows each authoritative retired Player exactly once by actual identity; stable DP/TP member display order is not treated as defensive chronology. Ordinary Out behavior, FC selection/completion, Walk, hits, Home Run, Reset Game cleanup, and Game Over history preservation continue through their existing paths.

#### Presentation and Resources

- Added resource-backed Double Play and Triple Play result labels and a blocking membership dialog that reuses the accepted FC visual language only for an ambiguous continuing zero-Out DP. Physical Android validation rejected both selected-row Out Attempting editing and the requirement to enter defensive chronology. The dialog asks for exactly two Players who were Out, highlights selected rows, toggles a selected row off when tapped again, shows no order labels, and enables confirmation only at the required membership count. One-Out DP and zero-Out TP return completed repository state, so the existing presentation flow has no pending play from which to open retiree, survivor, run-ruling, or `Next Batter` UI. Story 6.5.1 Out Attempting remains available through the existing live-runner updater.
- The scoring screen, scoreboard, Current Batter, Upcoming, Outs This Inning, bases, scoring menu, and portrait/landscape composition were not redesigned. A cancelled or resumed unresolved multi-out play remains pending and reopens the guided selection workflow.

#### Automated and Platform Validation

- Added 31 focused Story 6.6 cases covering explicit DP/TP classification; participant and inning-Out availability combinations; repository rejection of two-Out DP and one/two-Out TP; continuing zero-Out DP auto-selection or exact identity selection; survivor defaults, correction, reload, and commit preservation; ordinary-Out-only unordered selector behavior; resolver legality; later attempted-advance metadata and chronology; automatic one-Out DP and zero-Out TP completion with three or four participants; no atomic retiree/survivor state or fabricated movement history; automatic no-run evaluator context; zero score; no redundant completion; exactly-once half-inning transition; no fourth Out; and unchanged ambiguous unrelated third-Out rulings. The final focused Story 6.6 group passed 31 of 31 with zero failures and zero skips.
- The final complete automated test project passed 489 of 489 with zero failures and zero skips against isolated temporary SQLite databases. The final Story 6.6/scoring/resolver regression group passed 315 of 315 with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` non-incremental build passed with zero errors and 114 existing warning instances in the established `CS8602`, `MVVMTK0045`, and `XC0025` categories. Windows runtime validation was not performed.
- Final physical Android tablet acceptance passed all four focused scenarios. An ambiguous zero-Out Double Play displayed the unordered two-Player membership selector, allowed selected members to toggle on and off, applied the deterministic continuous pre-play force-chain defaults to survivors, kept safe/scored survivors editable, and committed the accepted state through `Next Batter`. A one-Out bases-loaded Double Play committed atomically with no retiree selector, run-ruling prompt, or `Next Batter`; no run counted, the bases cleared, and the existing third-Out sequence displayed the half-inning summary and advanced the batting order exactly once. A zero-Out bases-loaded Triple Play likewise committed atomically with no participant dialog, run-ruling prompt, or `Next Batter`; no run counted, the bases cleared, and the half-inning transitioned immediately. Availability was also accepted: Double Play was absent with two Outs, and Triple Play was absent with one or two Outs. Broader Play Resolution Coordinator / Play Context orchestration is explicitly deferred to the next architecture story.

#### Deferred Boundaries

- Defensive Player tracking, fielder attribution, assists, putouts, force/tag/line-drive classification, automatic force-Out run rulings outside the explicit atomic DP/TP product rule, defensive timing and force disappearance, tag-up semantics, defensive sequencing, full statistics, AVG/OBP, batter-responsibility derivation, completed-game correction, global Undo/Redo, and chronological play-by-play remain deferred. Future statistics must consume the explicit PA classification, continuing-play retiree identities where recorded, and only meaningful later chronology rather than infer defensive sequence from generic/internal Out values. The evaluator boundary can accept future known-force facts when the domain records them; Story 6.6 does not infer force status that is not persisted.

#### Intended Commit Message

`Add double play resolution`

### Story 6.6.1 — Play Resolution Coordination Foundation

**Status:** Complete and PR-ready on `task/play-resolution-coordinator`; implementation, automated validation, Windows build validation, and focused physical Android tablet acceptance are complete

#### Purpose and Coordinator Boundary

- Result-specific sequencing had accumulated across presentation and repository code while `BaseRunnerResolver`, `ThirdOutRunRulingEvaluator`, and `PendingFielderChoiceCompletion` intentionally remained focused rule components. The missing abstraction was choreography. Story 6.6.1 adds the scoring-specific, stateless `PlayResolutionCoordinator` as the single active-play orchestration root before more scoring special cases are introduced; no generic application-wide coordinator framework was added.
- The coordinator determines which focused component runs and when and returns one coherent `PlayResolutionResult`. It identifies unresolved participant decisions, retirement selection and count, `ReadyForNextBatter`, `AutoComplete`, `CommitImmediately`, and the third-Out evaluation boundary. It owns no SQLite, MAUI, resources, dialogs, score mutation, inning persistence, game lifecycle, or mutable per-Game state.
- Final architecture review removed the remaining duplicate guided-FC arrangement and outcome-propagation choreography from `GameScoringSessionRepository`; applying guided participant outcomes, propagating uniquely determined outcomes, and stopping unnecessary questions now occur inside the coordinator. The repository only rehydrates/applies context and owns persistence and transactions.

#### Play Context and Rule Boundaries

- Immutable `PlayContext` represents the current active play only. It separates PA classification from each participant's canonical starting location, destination, terminal Out state, `SafeDestination`, `AttemptedDestination`, Out order/membership facts, and confirmation state. New results construct it from committed pre-play truth; Exit/Resume rehydrates the exact persisted pending movements without regenerating defaults. It is neither a persisted aggregate nor a UI model.
- `BaseRunnerResolver` remains authoritative for legal arrangements, no passing, destination availability, deterministic correction, attempted-advance relationships, and canonical offensive participant order. Guided FC resolution therefore proceeds from the runner originally on Third, then Second, then First, and finally the batter-runner.
- `ThirdOutRunRulingEvaluator` remains authoritative for automatic no-run semantics, scorer-decision-required semantics, and meaningful chronological third-Out interpretation. The coordinator owns when that evaluator runs, not its ruling rules. `PendingFielderChoiceCompletion` remains a narrow terminal FC predicate where still used.

#### Presentation and Persistence Reduction

- `ScoringViewModel` now exposes generic coordinator-derived state: `ResolutionStatus`, `DecisionRequirement`, `DecisionParticipants`, `DecisionPlayerIds`, `RequiredRetirementCount`, `ThirdOutRulingRequirement`, `CanAdvanceToNextBatter`, and `CanEditParticipantOutcomes`. The former ViewModel-specific FC/DP predicates and result-specific invocation order were removed.
- `ScoringPage` runs one current-play resolution loop driven by `PlayDecisionRequirement` instead of reconstructing FC/DP/TP sequencing from result names. Guided dialogs show each participant's resource-backed starting location, and the page reacts to retirement, participant-outcome, and third-Out-ruling requirements returned by the domain.
- `GameScoringSessionRepository` remains the single SQLite transaction and persistence pipeline. Existing Home Run, ordinary Out, FC automatic/third-Out completion, atomic DP/TP, score, base, inning, and batter-advancement paths remain repository-owned. Ordinary load evaluates only a persisted pending play; completed plate appearances and movement history remain authoritative and are not recalculated.

#### Fielder's Choice and Multi-Out Clarification

- `FielderChoice` remains the PA/result classification when appropriate and is not defined as exactly one Out. A fielder's-choice play can produce multiple participant Outs without automatically changing the PA to `DoublePlay` or `TriplePlay`; an additional runner may become Out Attempting while the PA remains FC. That terminal participant retains `SafeDestination` and `AttemptedDestination`. Explicit DP/TP results remain valid and unchanged for plays intentionally classified as DP/TP, and FC versus DP/TP is not inferred solely from the number of Outs.
- Guided FC resolution uses canonical offensive order (original Third, Second, First, then batter-runner) and displays starting location. Once the required FC retirement structure is known, unnecessary retirement questions stop. Remaining safe/scored participants are resolved or defaulted through the existing coordinator/resolver path and remain editable until commit where current rules allow.
- The accepted two-Out rule is `2 Outs + Fielder's Choice = automatic third-Out FC completion`: there is no `Who Was Out?` dialog and no run-timing/ruling dialog merely because the PA is FC; the half inning advances once, the batter advances once, bases clear through the existing inning-transition persistence, no fourth Out is created, and the PA remains FC.
- This is distinct from `2 Outs + hit + one runner Scores + another runner becomes Out Attempting for the third Out`. That play may still require scorer run-timing input. The coordinator decides when evaluation is needed, and `ThirdOutRunRulingEvaluator` decides the ruling semantics; not every third-Out play is an automatic no-run.

#### Physical Android Tablet Validation — Accepted

- Portrait and landscape visual smoke checks passed. Guided FC resolution passed with starting-location presentation; unnecessary prompts stopped once the retirement structure was known; safe/scored participants remained editable; and FC plus Out Attempting retained FC classification and movement detail.
- Two-Out FC automatic completion passed with no `Who Was Out?` dialog, no run-ruling dialog, one inning transition, one batter advancement, and no fourth Out. Explicit DP and TP behavior also passed.
- Pending FC Exit/Resume behavior passed. No duplicate dialogs, duplicate actions, or runtime exceptions were observed in the accepted scenarios. Cancellation/resumption and pending DP Exit/Resume were not separately recorded as physical-device scenarios and are not claimed here.

#### Automated and Platform Validation

- `PlayResolutionCoordinatorTests` passed 31 of 31; focused FC/Walk passed 40 of 40; `RunnerOutAttemptingAdvanceTests` passed 19 of 19; and `MultiOutPlayResolutionTests` passed 31 of 31, all with zero failures and zero skips.
- The scoring/resolver regression group passed 317 of 317. The complete automated suite passed 520 of 520 against isolated temporary SQLite databases, with zero failures and zero skips.
- The non-incremental Windows `net9.0-windows10.0.19041.0` build passed with zero errors and 114 existing warnings in the established `CS8602`, `MVVMTK0045`, and `XC0025` categories. Windows runtime validation was not performed.
- The .NET Android workload is installed, but the Android SDK is not configured or discoverable and `adb` is unavailable. Command-line Android build validation therefore stops at `XA5300` before application compilation; this is an environment limitation, not a Story 6.6.1 code failure. Android interaction acceptance was performed manually on the physical tablet as recorded above.

#### Scope and Deferred Boundaries

- Story 6.6.1 did not add Sacrifice Fly, Error/ROE, force/tag classification, HR-limit enforcement, 1-Up enforcement, Beer Rules, substitutions, live lineup management, statistics expansion, Undo/Redo, Manual Overrides, completed-game correction, generic Team/Player/Game coordinators, MediatR, CQRS, event sourcing, or a rules-engine architecture.
- Genuine future seams remain future result-specific orchestration and completed-play correction/Undo. They should extend the scoring coordinator and authoritative persisted history when their stories are approved; no speculative component was introduced here.

#### Intended Commit Message

`Add play resolution coordination`

### Story 6.6.2 — Fielder's Choice Semantic Correction

**Status:** Complete and PR-ready on `task/fielder-choice-semantic-correction`; implementation, automated validation, Windows build validation, and physical Android tablet acceptance are complete

#### Corrected Fielder's Choice Semantics

- Corrected the ambiguous Story 6.6.1 FC model: the batter-runner is now safe at First by definition, is already resolved when FC is selected, is never offered as the ordinary FC retirement candidate, and cannot be changed to Out through the FC participant-outcome path. Recording the batter retired at First while another runner advances remains the existing ordinary `Out` workflow rather than FC.
- FC still requires at least one pre-play runner. Guided decisions now concern existing runners only, in canonical Third, Second, First order. At least one existing runner must be retired, while additional ordinary or Out Attempting retirements remain supported without reclassifying the PA as Double Play or Triple Play. Explicit DP/TP behavior remains separate and unchanged.
- Safe at First is the FC batter-runner's initial default, not a permanent physical-destination restriction. Once resolved, the batter-runner remains canonically identified as starting at Home and uses `BaseRunnerResolver` for legal correction to First, Second, Third, or Scores. Later advancement does not convert FC into a hit, and no Error/ROE attribution or FC-specific movement engine was introduced.
- At two Outs, a one-runner FC automatically retires that existing runner; a multi-runner FC requests one retirement from existing runners only. The selected runner becomes the actual third Out, the batter remains safe at First in the play facts, no fourth Out is created, and the PA remains FC. The generic coordinator retirement-selection path now serves FC and multi-out plays; the former repository-specific FC third-Out completion API and the obsolete all-terminal FC predicate were removed.
- Third-Out handling now evaluates the actual resolved participants rather than treating every two-Out FC as automatic no-run by result name. A two-Out FC does not open a run-ruling dialog merely because it is FC; a timing decision is requested only when the resolved play contains a potentially scoring runner whose run requires a ruling.

#### Persistence, Presentation, and Compatibility

- `PlayResolutionCoordinator` remains the active-play choreography root, `PlayContext` carries the corrected immutable participant facts, `BaseRunnerResolver` remains authoritative for physical runner legality, and `ThirdOutRunRulingEvaluator` remains focused on third-Out run rulings. `ScoringViewModel` did not regain scoring rules or result-specific orchestration. Infrastructure retains its existing SQLite transaction and persistence responsibilities.
- New pending FC state and database rehydration preserve the corrected batter-safe state, existing-runner retirement, a corrected batter-runner destination beyond First, and exact `SafeDestination` / `AttemptedDestination` metadata where applicable. Exit/Resume does not duplicate retirement, batter advancement, commit, or inning transition.
- `ScoringViewModel` remains a consumer of generic coordinator decisions. It submits all retirement membership through the shared repository operation; no FC result choreography was reintroduced in the ViewModel or page. The existing retirement dialog receives coordinator-provided existing-runner candidates and count.
- The obsolete dedicated repository FC third-Out completion path and terminal FC predicate were removed. Existing tests encoding batter-out FC or result-name-only two-Out completion were deliberately updated rather than deleted, preserving regression intent around ordinary Out, hits, Walk, explicit DP/TP, resolver correction, run rulings, scoring history, and persistence.

#### Automated and Platform Validation

- Final validation passed 145 of 145 focused FC/coordinator/resolver tests, 358 of 358 scoring/resolver regression tests, and the complete 532 of 532 automated suite against isolated temporary SQLite databases, with zero failures and zero skips. Coverage includes FC unavailability with empty bases; batter Safe at First; batter exclusion from retirement candidates; existing-runner, multi-runner, and bases-loaded FC retirement; ordinary Out for batter retirement; two-Out FC and no fourth Out; fact-driven third-Out evaluation; FC plus Out Attempting; batter-runner correction to Second, Third, and Scores; resolver menu/execution equivalence; no-passing and destination capacity; singleton-outcome auto-resolution; Exit/Resume; exactly-once commit and inning transition; and DP/TP, ordinary Out, Walk, and hit regressions.
- The previously completed Windows `net9.0-windows10.0.19041.0` non-incremental build passed with zero errors and 114 existing warnings in the established `CS8602`, `MVVMTK0045`, and `XC0025` categories. It was not rerun after the Android-validation corrections, and Windows runtime validation was not performed.

#### Physical Android Validation Follow-up — Singleton Outcomes

- Physical Android validation found that a zero-Out FC with one runner on First correctly identified the existing runner but still opened the generic participant-outcome dialog with only `Out`. The coordinator now consumes a participant's sole legal outcome before exposing a decision; zero choices retain existing unresolved handling, while two or more choices still require scorer input. The ViewModel remains a generic coordinator consumer.
- Pending-play creation now persists the coordinator's resolved context immediately, so the automatically retired runner, batter safe at First, subsequent editing/commit choreography, and Exit/Resume state match the former manual selection path. Explicit runner correction remains an invoked editing interaction because cancellation and retaining the current outcome are meaningful there. Retirement membership, Out Attempting, third-Out rulings, and multi-out selection retain scorer input whenever they contain multiple semantic choices.
- Physical Android revalidation accepted the correction: Runner on First plus FC retired the existing runner automatically, left the batter Safe at First, and displayed no redundant singleton `Out` dialog. Two-Out FC, DP/TP, Out Attempting, third-Out rulings, ordinary Out correction, and meaningful multi-option decisions retained their established interaction behavior.

#### Physical Android Validation Follow-up — FC Batter-Runner Advancement

- Physical Android validation then found that the resolved FC batter-runner's correction menu contained only First. The FC result definition had incorrectly used its allowed-batter-destinations filter to enforce the initial Safe-at-First default as a permanent physical destination, and the coordinator routed every FC participant through guided unresolved-outcome handling. FC now retains First as the initial default while allowing the resolved batter-runner to use the shared resolver for legal safe destinations at First, Second, Third, or Scores. Ordinary batter retirement remains excluded from FC, existing-runner retirement rules are unchanged, and advancing the batter-runner does not change the FC plate-appearance classification.
- Guided FC handling now applies only while the selected participant still requires outcome confirmation. Once resolved, both the batter-runner and existing runners use normal resolver correction, preserving no-passing, destination capacity, deterministic displacement, and menu/execution equivalence. Other current results were audited: hits already use unrestricted resolver correction, Walk commits immediately, and Out/DP/TP retain their existing correction behavior; no parallel permanent-default restriction was found.
- Added focused coverage for the Safe-at-First default; legal batter-runner correction to Second, Third, and Scores; exclusion of ordinary Out; retained FC classification and absence of hit reclassification; multi-runner resolver legality; Exit/Resume beyond First; and exactly-once commit. Existing two-Out FC and FC Out Attempting regressions remain green.

#### Physical Android Tablet Validation — Accepted

- Runner on First plus FC automatically retired the existing runner, established the batter Safe at First, and displayed no redundant singleton `Out` outcome dialog. The ordinary batter-Out scenario retained the `Out` workflow: existing runners held their starting locations by default and remained manually advanceable when appropriate.
- Multi-runner FC excluded the batter from retirement decisions, resolved existing runners correctly, and retained legal survivor correction. The FC batter-runner defaulted to First and could subsequently move to legal later destinations while the plate appearance remained FC.
- Two-Out FC used an existing runner for the third Out, kept the batter Safe, persisted no fourth Out, and advanced the half inning exactly once. FC plus Out Attempting and pending FC Exit/Resume were accepted without duplicated retirement, batter advancement, or inning transition.
- Single, ordinary Out, Walk, Double Play, and Triple Play smoke scenarios passed. Portrait and landscape scoring presentation passed on the physical Android tablet.

#### Scope and Deferred Boundaries

- This correction adds no force/tag classification, defensive attribution or chronology, automatic run timing, statistics expansion, Undo/Redo, completed-game correction, new result types, or alternate persistence pipeline. Those remain deferred to their approved stories.
- Story 6.6.1 remains the historical coordinator-foundation record, but its statements that FC included the batter in guided order and that every two-Out FC was automatic by result name are superseded by this correction.

#### Intended Commit Message

`Correct fielder's choice semantics`

### Story 6.7 — Sacrifice Fly Scoring (Complete — PR-ready)

#### Implementation and Architecture

- Appended persisted `BatterResult.SacrificeFly = 10` without changing values 1–9. SQLite continues to store pending and completed batter results as integers, so no schema migration was added. Explicit Sac Fly selection requires a pre-play runner on Third and fewer than two Outs. Plays where a runner scores from First or Second while Third was empty are recorded as ordinary Out because the app does not capture enough defensive detail to classify those unusual plays reliably.
- Sac Fly selection defaults the batter to a terminal Out, automatically moves the runner starting on Third to `Scores`, and holds other existing runners. Other runners remain editable through the shared resolver with no Sac Fly-specific destination menu. `PlayResolutionCoordinator` applies the automatic initial outcome, while rehydration preserves exact persisted corrections rather than regenerating the Third-base movement.
- The focused `SacrificeFlyCompletionEvaluator` recognizes completion only when a participant identified by immutable pre-play facts as an existing runner finishes at `Scores`. The automatic Third-base outcome normally satisfies this rule immediately, but the coordinator and repository retain it as a defensive invariant and reject stale, corrupt, or reconstructed Sac Fly state with no scored pre-play runner. Changing the result still rebuilds from committed pre-play bases rather than carrying pending Sac Fly corrections forward.
- The batter retirement is persisted as an explicit Home-to-Out participant with play-relative `OutOrder = 1`. Later runner Outs and Out Attempting use the existing runner correction path and receive subsequent Out order, allowing the existing third-Out evaluator to identify the actual third participant without Sac Fly-specific run-timing rules. Authoritative Out counting comes from persisted participant movements rather than a new result-name counter.
- `BaseRunnerResolver` remains unchanged and authoritative for legal destinations, Scores, Out, Out Attempting, no passing, and base capacity. `ThirdOutRunRulingEvaluator` also remains unchanged; scorer run rulings and `RunCounts` continue through the established generic path. Additional runner Outs use the existing machinery and preserve Out ordering, no fourth Out can persist, and third-Out rulings remain based on actual participant facts.
- Home/Away score mapping, inning transitions, batter advancement, pending Exit/Resume, completed reload, and lineup preservation reuse existing repository transactions and persistence. Commit advances the current batter exactly once; the frozen starting lineup and `GameScoringLineupPlayer` remain unchanged. Pending and completed Sac Fly state preserves player identity, destinations, classification, and corrections across reload/database reopen.
- Added the localized `Sac Fly` result label through the existing resource and ViewModel formatting path. The scoring page continues to generate its result menu from Core availability and remains presentation-focused; no XAML redesign or presentation scoring rule was introduced.
- Sac Fly is now a distinct semantic plate-appearance result available for future statistical treatment. Story 6.7 does not implement AVG, OBP, AB totals, RBI, SF totals, slugging, or statistics UI.

#### Automated and Platform Validation

- Added focused enum-compatibility, Third-base-specific availability, automatic Third-runner scoring, terminal-batter, immediate pending readiness, defensive completion-invariant, multi-runner correction, result-change, additional-Out, Out Attempting, third-Out ruling, no-fourth-Out, score-mapping, lineup-preservation, and pending/completed reload coverage in `SacrificeFlyScoringTests`. Corrected validation passed 30 of 30 Sac Fly-only tests, 133 of 133 focused Story 6.7/coordinator/resolver tests, 388 of 388 scoring/resolver regression tests, and the complete 562 of 562 automated suite, with zero failures and zero skips.
- The non-incremental `net9.0-windows10.0.19041.0` build passed with zero errors and the existing 114 warnings in categories `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.

#### Physical Android Tablet Validation — Accepted

- With zero Outs and a runner on Third, Sac Fly was selectable, retired the batter, automatically displayed the Third-base runner as scored, remained pending until Next Batter, and then committed one Out and one Run with correct bases, inning continuation, and batter advancement.
- With one Out and a runner on Third, the runner automatically scored, Next Batter committed normally, Outs became two, the Run counted, and the inning did not transition.
- Sac Fly was unavailable with a runner only on First, a runner only on Second, and a runner on Third with two Outs, confirming the final availability boundary.
- With multiple runners, the Third-base runner automatically scored, another runner initially held and remained legally advanceable through the shared resolver, final bases and totals were correct, and the plate appearance remained Sac Fly.
- When another existing runner was retired, the plate appearance remained Sac Fly and the existing third-Out run-ruling workflow correctly requested whether the run counted while preserving player identities and Out handling. The physical pass exercised ordinary Out, not Out Attempting; automated coverage remains authoritative for Out Attempting metadata behavior.
- Pending Exit/Resume restored the Sac Fly classification, batter retirement, automatically scored Third-base runner, exact pending runner state, current batter, inning, and Outs, and the restored play subsequently committed normally.
- Physical regression smoke validation passed for Single, ordinary Out, Walk, Fielder's Choice, Double Play, Triple Play, portrait presentation, and landscape presentation.

#### Scope and Deferred Boundaries

- Although unusual sacrifice-fly situations may theoretically involve a runner scoring from a base other than Third, explicit Sac Fly is intentionally available only when a runner already occupies Third. The app does not capture enough defensive or catch detail to infer those uncommon classifications reliably; other batter-Out runner movement continues through the ordinary Out workflow.
- Scorer-friendly plate-appearance classification reconciliation for FC and Sac Fly, Android UI/integration automation, runner-movement UX improvements, Error/Reach on Error, statistics, HR-limit/1-Up enforcement, Beer Rules, substitutions, Undo/Redo, and completed-game correction remain deferred. Story 6.7 adds no future reconciliation behavior.

### Story 6.7.1 — PA Classification Reconciliation: Fielder's Choice + Sacrifice Fly (Complete — PR-ready)

#### Reconciliation Behavior and Boundaries

- Added active-play-only reconciliation so a scorer can enter the physical facts of a pending play without needing perfect classification knowledge or using Undo/re-entry. Completed historical plate appearances are never rewritten. Explicit result-menu replacement remains unchanged and continues to rebuild pending state from committed-base truth.
- Fielder's Choice selection now requests exactly one primary Out from the batter and all existing runners, including when only one existing runner is present. Selecting an existing runner preserves `FielderChoice` and the batter remains safe; selecting the batter silently reconciles the pending result to ordinary `Out` while existing runners remain safe and editable. Exactly one primary PA-resolution Out is allowed through FC; two primary Outs remain Double Play territory. A later legal `Out Attempting` remains distinct advancement behavior and retains its attempted and safe destinations.
- Sac Fly availability and defaults remain unchanged: Third must be occupied with fewer than two Outs, the batter defaults Out, and the runner who started on Third defaults `Scores`. Correcting that runner away from `Scores` silently reconciles the pending result to ordinary `Out`, removes the pending run, and preserves the single batter Out without Undo/re-entry.
- Reconciliation preserves compatible physical facts: participant identity, destination, legal ordinary runner Outs, `SafeDestination`, and `AttemptedDestination`. It regenerates incompatible classification-derived state, including FC primary-Out state, the Sac Fly explicit batter movement and `OutOrder`, and result-specific completion or confirmation state. Canonical ordinary-Out persistence omits a duplicate explicit batter movement so the batter retirement is counted exactly once.
- There is no reverse automatic inference from Out to FC or Sac Fly, no generic PA-classification engine, and no completed-PA correction. Broader classification assistance, sacrifice ground-ball or bunt classification, and other scoring backlog work remain outside this story.

#### Architecture and Persistence

- `PlayResolutionCoordinator` owns reconciliation choreography and evaluates final reconciled participant facts for third-Out run rulings. `BaseRunnerResolver` remains authoritative for legal physical movement, base capacity, no-passing rules, and Out/Out Attempting availability. No scoring rules were added to `ScoringViewModel` or `ScoringPage`.
- Infrastructure synchronizes the pending result header and runner movements atomically. FC or Sac Fly reconciliation to ordinary Out removes incompatible result-derived movements, preserves compatible existing-runner facts, and resets classification-specific Out-order and confirmation state before normal completion evaluation.
- Pending Exit/Resume rehydrates the persisted reconciled classification and participant facts rather than reapplying FC or Sac Fly defaults. Automated repository coverage verifies reconciled FC-to-Out and Sac-Fly-to-Out pending reload, player identity, destinations and Out Attempting metadata, current batter, inning/Out state, and subsequent normal commit.
- The reconciled semantic result is available to future statistics work, but this story does not implement or change statistics.

#### Automated and Platform Validation

- Final automated validation passed 137 of 137 focused FC/Sac Fly/Out Attempting tests, 393 of 393 scoring/resolver regression tests, and the complete 567 of 567 suite against isolated temporary SQLite databases, with zero failures and zero skips.
- The Windows `net9.0-windows10.0.19041.0` non-incremental rebuild passed with zero errors and 114 existing warnings in categories `CS8602`, `MVVMTK0045`, and `XC0025`. Windows runtime validation was not performed.

#### Physical Android Tablet Validation — Accepted

- True FC requested exactly one primary Out and offered both the batter and existing runner. Selecting the runner preserved `FielderChoice`, left the batter safe, recorded exactly one primary Out, and retained runner advancement and correction.
- Selecting the batter as the FC primary Out silently reconciled the pending result to ordinary `Out`, left the existing runner safe and editable, and required no Undo/re-entry. A later legal `Out Attempting` remained separate from the primary PA-resolution Out.
- Sac Fly retained its Third-occupied/fewer-than-two-Out availability, batter-Out and Third-runner-Scores defaults. Correcting the Third-starting runner away from Scores silently reconciled to ordinary `Out`, removed the pending run, preserved exactly one batter Out, and required no Undo/re-entry.
- Normal FC, normal Sac Fly, ordinary Out, Single, Walk, Double Play, Triple Play, portrait, and landscape smoke scenarios were accepted during the physical Android validation recorded for Stories 6.6.2 and 6.7 and were not altered by this story.
- Exit/Resume after a newly reconciled FC-to-Out play and after a newly reconciled Sac-Fly-to-Out play were not separately exercised on the physical Android tablet. Their persisted behavior is covered by the automated repository tests and remains the exact outstanding physical validation limitation.

#### Scope and Deferred Boundaries

- Undo/global correction, completed-PA correction, broader classification assistance, sacrifice ground-ball/bunt classification, and statistics remain deferred. Android UI automation and unrelated scoring backlog stories were not introduced.

### Story 6.7.2 — Android UI / Integration Test Automation Harness (Complete — PR-ready)

#### Initial Harness

- Added the separate `SoftballScorekeeper.AndroidUiTests` xUnit project targeting `net9.0`; it has no reference to `SoftballScorekeeper.Tests` or to production projects and remains independently runnable.
- Added Appium 8.3.2 client configuration for Android/UiAutomator2, portable server, device, and `adb` environment-variable overrides, installed-package activation for `com.companyname.softballscorekeeper`, reusable explicit waits for MAUI `AutomationId` resource IDs, one session smoke test, and project-local setup documentation.
- The smoke test deliberately stops at session creation, installed-app activation, and clean session disposal. Current automated coverage is intentionally limited to that smoke test and the scoring workflow below.

#### Single -> Next Batter -> Exit -> Resume Workflow

- Added one end-to-end Appium workflow that opens a prepared Game, completes the real first-start Away/Game Rules modal, verifies Alex Automation as Current Batter, selects Single through the existing native action sheet, verifies Alex on First as pending state, advances to Blake Automation, exits through the scoring menu, resumes from Game Workspace, and verifies the batter, First-base runner, tied 0-0 score, Top 1 inning, and zero-Out state remain visible and intact.
- Added only the durable `AutomationId` contracts needed by this workflow: Score Game, Resume Game, Away side, Game Rules confirmation, scoring menu, Exit Game, Current Batter, First-base runner, Next Batter, both scores, and compact/landscape inning and Out state. The existing native Single action-sheet option has no XAML control on which to place an ID and is the workflow's sole visible-text selector.
- Added an isolated, deterministic Debug-only initializer registered and invoked only in Debug builds. The fixture clears application data with `adb`, creates a private marker through Android `run-as`, and the initializer consumes that marker once to create one Team, three Players and roster memberships, one Season, one Game, and a known frozen lineup before navigating to Game Workspace. Release builds contain neither the registration nor initializer, and no visible debug menu, production reset action, or scoring-semantic change was introduced.
- Failures now write the step-wrapped exception, screenshot, Appium page source, and recent `adb logcat` output beneath ignored test-output `TestResults`. The README documents the repeatable embedded-assembly Android build, APK install, workflow filter, full-suite command, and why a Fast Deployment APK cannot survive the fixture's application-data reset.

#### Validation and Limitations

- The Android UI test project built with zero warnings and zero errors. The focused scoring workflow passed three consecutive clean-state runs; every process cleared application data and recreated the known seed state.
- The accepted emulator-independent suite passed 567 of 567. Visual Studio Test Explorer reported 569 total with 567 passed, zero failed, and the two opt-in Android UI tests skipped. The intentional Android runner executed and passed 2 of 2 with zero failures and zero skips against the Android 15 / API 35 emulator using Appium 3.7.0 and UiAutomator2 8.5.0.
- The non-incremental embedded-APK Android build succeeded with zero errors and 7 existing warnings, the APK installation succeeded, and the non-incremental Windows build succeeded with zero errors and 114 existing warnings. Android validation was emulator-only; no physical-tablet run was performed for this story.

#### One-Command Local Runner

- Added `scripts/run-android-ui-tests.ps1` as the preferred repository-root command for the existing harness. It verifies `dotnet`, `adb`, one explicit or safely auto-selected usable device, and Appium's status endpoint; performs the required serialized non-incremental embedded-assembly Android build; verifies and installs the signed APK; forwards the selected device, Appium URL, and `adb` path to the fixture; and runs the complete UI suite by default or an optional `dotnet test` filter.
- The runner now starts the configurable `softball_tablet_api_35` standard AVD when no usable device is connected, discovers its assigned emulator serial, polls bounded `adb` and Android boot-completion state, and shuts down only an emulator it started through failure-safe cleanup. `-AvdName`/`SOFTBALL_ANDROID_AVD` override the default and `-KeepEmulatorRunning` retains a runner-started emulator; pre-existing usable devices are selected through the existing rules and are never shut down by the runner. Appium remains externally managed.
- From a zero-device state, the runner started `softball_tablet_api_35`, dynamically discovered its emulator serial, observed Android boot completion, built and installed the app, passed the complete Android UI suite, and cleanly shut down only that runner-started emulator. Build-free checks also confirmed `-KeepEmulatorRunning` retains a runner-started emulator and a pre-existing usable emulator remains connected when the runner exits early. Appium startup remains external.

#### Opt-In Test Execution Correction

- Visual Studio Test Explorer originally treated the two Appium tests as ordinary tests because the Android UI project is an `IsTestProject` solution member and both methods used standard xUnit facts. Replaced those facts with a discovery-time Android UI fact that skips unless `SOFTBALL_RUN_ANDROID_UI_TESTS=1`; ordinary solution/Test Explorer runs therefore require neither Appium nor an emulator, while the project remains buildable, discoverable, and explicitly runnable.
- The Android runner sets and restores the opt-in environment variable around `dotnet test`, so its supported command executes the complete Android UI suite and preserves hard failures for broken prerequisites. The accepted normal and intentional results are recorded above.

#### Deferred Boundaries

- Appium process startup and shutdown remain intentionally external. Broader Android UI scenarios and broad orientation automation are future work.
- Emulator Crashpad and lock-file stability investigation belongs in a separate follow-up story and was not included here.

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
