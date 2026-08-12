# Softball Scorekeeper — Feature History

## Document Purpose

This file is the canonical implementation-history record for the Softball Scorekeeper repository. Future AI assistants, implementation chats, developers returning to the project, reviewers, and maintainers should use it to understand completed work and the actual current repository state before starting a story.

Update this file for every story after implementation and validation and before committing. Entries summarize completed implementation work, reusable components, important decisions, validation actually performed, known limitations, and deferred work. They do not duplicate full story specifications or acceptance criteria.

Stable product and architecture decisions belong in the project architecture documentation. Repository-wide implementation instructions belong in `AGENTS.md`. This document records what has actually been implemented.

## Current Project Status

- **Latest completed story:** Story 4.5 — Edit Game
- **Completed stories:** Stories 0.0 through 3.1 and Stories 3.5 through 4.5; Stories 3.2 through 3.4 retain their recorded validation status
- **Current story:** None; Story 4.5 — Edit Game is complete
- **Current phase:** Team-scoped Game Management workspace foundation
- **Foundation status:** Complete

The repository contains a four-project .NET 9 solution: the Android- and Windows-targeted .NET MAUI application, platform-independent Core, Infrastructure, and the automated test project. Its implemented foundation includes repository guidance, pragmatic layered dependencies, CommunityToolkit.Mvvm, MAUI dependency injection, device-local SQLite initialization, centralized user-facing resources, strongly typed application settings, and isolated xUnit repository integration tests. Team Workspace contains local Games and Roster sections, supports adding or removing existing active Players, and supports creating a global Player with immediate TeamPlayer membership from the Team-scoped Add Players workflow. Player Workspace shows active Team memberships, and Player Manager retains bulk lifecycle actions; Game schedule management and other later softball workflows remain deferred.

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
