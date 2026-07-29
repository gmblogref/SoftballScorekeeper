# Softball Scorekeeper — Feature History

## Document Purpose

This file is the canonical implementation-history record for the Softball Scorekeeper repository. Future AI assistants, implementation chats, developers returning to the project, reviewers, and maintainers should use it to understand completed work and the actual current repository state before starting a story.

Update this file for every story after implementation and validation and before committing. Entries summarize completed implementation work, reusable components, important decisions, validation actually performed, known limitations, and deferred work. They do not duplicate full story specifications or acceptance criteria.

Stable product and architecture decisions belong in the project architecture documentation. Repository-wide implementation instructions belong in `AGENTS.md`. This document records what has actually been implemented.

## Current Project Status

- **Latest completed story:** Story 1.3 — Deactivate Team
- **Completed stories:** Stories 0.0 through 1.3
- **Next planned implementation story:** Not currently documented
- **Current phase:** Initial application interface
- **Foundation status:** Complete

The repository contains a single-project .NET 9 MAUI application targeting Android and Windows. Its implemented foundation includes repository guidance, a pragmatic folder structure, CommunityToolkit.Mvvm, MAUI dependency injection, device-local SQLite initialization, centralized user-facing resources, and strongly typed application settings. The landing page and Team creation, editing, and deactivation workflows are implemented; other product-facing softball workflows remain deferred.

## Approved Project-Wide Direction

The following project-wide architecture and planning decisions were approved while Story 1.2 was in progress. They guide future work but do not represent completed implementation or physical-device validation.

### Primary Device and Tablet-First UX

The primary deployment and UX target is a 10-inch Android tablet used for live adult slow-pitch softball scorekeeping. The preferred scoring experience is landscape-first unless later real-device testing supports a different decision. Android tablet behavior is the primary UI and interaction acceptance standard; Windows remains supported for development, debugging, secondary validation, and convenience.

Tablet UI should favor large finger-friendly touch targets, clear readability at normal viewing distance, minimal taps, one-handed operation where practical, stable control placement, touch-appropriate spacing, and reliable offline use. Desktop conventions should not drive the primary experience when they conflict with Android tablet usability.

### Current and Planned Architecture

The current repository remains one .NET MAUI application project using logical folders, XAML, MVVM, CommunityToolkit.Mvvm, dependency injection, centralized resources, configuration, and SQLite foundations. This single-project organization remains valid through Team CRUD and Player CRUD.

After Team and Player CRUD, but before roster, game scheduling, lineup, statistics, and scoring-rule complexity grows substantially, a dedicated future story is expected to transition the solution toward pragmatic Presentation / .NET MAUI, Application, Domain, Infrastructure, and Tests boundaries. Clear dependency direction and independent testability matter more than project count, and unnecessary interfaces and abstractions should be avoided. This transition is approved future work and has not been implemented.

### Planned Automated Testing

An automated test project is planned during or immediately after the architecture-transition milestone and before complex roster, lineup, statistics, and scoring-engine development. Domain and application rules should be testable without launching MAUI. Scoring rules are expected to receive comprehensive table-driven coverage, while repository integration tests may use isolated temporary SQLite databases. UI automation remains out of scope unless a later story explicitly adds it. No automated test project exists yet as a result of this decision.

### Planned Early Branding

The default .NET application icon and splash screen should be replaced early in the feature phase. Initial branding may be simple and generated, should remain visually consistent across Android and Windows, and should establish project ownership without delaying functional development. This branding work remains planned and has not been completed by this documentation decision.

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
