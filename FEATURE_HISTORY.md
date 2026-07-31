# Softball Scorekeeper — Feature History

## Document Purpose

This file is the canonical implementation-history record for the Softball Scorekeeper repository. Future AI assistants, implementation chats, developers returning to the project, reviewers, and maintainers should use it to understand completed work and the actual current repository state before starting a story.

Update this file for every story after implementation and validation and before committing. Entries summarize completed implementation work, reusable components, important decisions, validation actually performed, known limitations, and deferred work. They do not duplicate full story specifications or acceptance criteria.

Stable product and architecture decisions belong in the project architecture documentation. Repository-wide implementation instructions belong in `AGENTS.md`. This document records what has actually been implemented.

## Current Project Status

- **Latest completed story:** Story 3.1 — Automated Test Foundation
- **Completed stories:** Stories 0.0 through 3.1
- **Current story:** Story 3.4 — Add Players to Team (implementation complete; manual runtime validation pending)
- **Current phase:** Team and Player workspace workflows and validation
- **Foundation status:** Complete

The repository contains a four-project .NET 9 solution: the Android- and Windows-targeted .NET MAUI application, platform-independent Core, Infrastructure, and the automated test project. Its implemented foundation includes repository guidance, pragmatic layered dependencies, CommunityToolkit.Mvvm, MAUI dependency injection, device-local SQLite initialization, centralized user-facing resources, strongly typed application settings, and isolated xUnit repository integration tests. Team Workspace can add existing active Players to its roster, Player Workspace shows active Team memberships, and Player Manager retains bulk lifecycle actions; roster removal and other product-facing softball workflows remain deferred.

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
