# AGENTS.md

## Project Overview

Softball Scorekeeper is a lightweight, offline-first scorekeeping application for live adult slow-pitch softball games.

The application is designed to replace paper scorekeeping and unnecessary complexity with a fast, tablet-friendly workflow that matches how the team actually records games.

Primary goals:

- Fast score entry
- Minimal taps
- One-handed tablet use
- Automatic runner-movement defaults
- Easy corrections
- Automatic statistics
- Automatic beer-rule tracking
- Reliable offline operation

The core product principle is:

> Assume the normal softball result and require user input only for unusual outcomes.

## Supported Platforms

Primary deployment and design target:

- 10-inch Android tablet
- Landscape-first scoring experience unless real-device testing supports a different decision

Android tablet behavior is the primary UI and interaction acceptance standard.

Supported secondary platform:

- Windows for development, debugging, validation, and convenience

When Android tablet usability and desktop conventions conflict, Android tablet usability takes priority. Windows must remain supported, but desktop conventions must not drive the primary experience.

## Branding

Replace the default .NET application icon and splash branding early in the feature phase. Initial branding may be simple and generated, and it should establish project ownership without delaying functional development. Keep Android and Windows branding assets visually consistent.

## Technology Stack

Use:

- .NET MAUI
- C#
- XAML
- MVVM
- CommunityToolkit.Mvvm
- SQLite for local persistence

The application must remain fully functional without an internet connection.

Do not introduce:

- Server-side services
- Cloud databases
- User accounts
- Cloud synchronization
- Internet-dependent application behavior

## Architecture

Use a pragmatic layered architecture with Clean Architecture principles where they provide clear value.

The goal is separation of responsibilities, testable business logic, and maintainable code without unnecessary abstraction.

The repository uses three production projects plus one automated test project: the `SoftballScorekeeper` .NET MAUI presentation and composition-root project, the platform-independent `SoftballScorekeeper.Core` domain project, the `SoftballScorekeeper.Infrastructure` SQLite persistence project, and `SoftballScorekeeper.Tests`. The MAUI project references Core and Infrastructure, Infrastructure references Core, and Core references no other solution project.

An Application project remains planned future work. The `SoftballScorekeeper.Tests` project references Core and Infrastructure and must not reference the MAUI presentation project. Do not create additional project boundaries early merely to imitate textbook Clean Architecture. Clear dependency direction, independently testable business rules, and separation of MAUI, SQLite, and domain logic matter more than the number of projects, interfaces, or wrapper services.

A `Team` record represents exactly one Team Year. Its owned `Season` records represent League Season subdivisions within that Team Year, such as Spring, Summer, or Fall; do not use League Season scope as the Team Year boundary.

Team Year is required, valid from 1 through 9999, and immutable after Team
creation. New Season writes must derive and match their owning Team Year.
Pre-Story-7.7 multi-year Teams retain older mismatched Seasons and Games as
readable legacy history; those Seasons cannot define new Games or copy forward.
Seasonless legacy Teams migrate to fixed year 2026, single-year Teams use that
year, and multi-year Teams use their greatest Season year without splitting.

Every Team has an immutable lineage identifier and optional immutable immediate
predecessor. Normal Team creation starts a new lineage. Copy Team creates only
the next year, shares the lineage, points to the source Team, and cannot branch.
Team lineage/year and normalized Team name/year are unique across active and
inactive Teams. Copy collisions never reuse or reactivate an existing Team.

Team activity and Team-Year finalization are independent lifecycle concepts.
Explicit Year Over is irreversible and makes that Team Year permanently
read-only. Team Year Summary is available exactly when the Team has a successor
or its year has been explicitly marked over; never infer completion from
activity, dates, Games, Seasons, or Beer state. Copy Team to New Year must occur
before explicit finalization; a source that already has a successor does not
need to be marked over.

The local SQLite database is the complete authoritative portable application
state. A backup must contain a SQLite-consistent snapshot plus independently
versioned backup metadata; the backup format version and database schema version
are separate compatibility boundaries. Restore replaces local application state
and is never merge, import, or deduplication. Never downgrade a backup from a
newer unsupported database schema, bind a backup to source-device identity, or
auto-purge canonical history for backup/storage concerns. Document and cloud
providers may store user-selected backup artifacts but are not application data
dependencies.

Product Test Mode uses a physically separate SQLite database and must never be
implemented through row tagging or repository-specific test branches. Database
mode switching requires exclusive ownership of the existing database lifecycle,
and every successful switch resets navigation and application state to the Teams
root. Product Test cleanup may target only the dedicated known Test database and
its explicit SQLite sidecars; the Production and Android UI-test databases are
never valid cleanup targets. Portable Backup and Restore are Production-only.
While Test Mode is active, a clear TEST MODE indicator must remain visible
throughout normal application navigation.

League Season backup reminders are Production-only and are driven by a
prospective, schedule-aware completion checkpoint: every scheduled Game must
be authoritative Completed and the latest playing date must be before the
current local calendar date. Only a successful final Production backup
destination write or an explicit Dismiss handles an eligible checkpoint;
Remind Later is session-only and never creates automatic backup behavior. Test
Mode must not evaluate or modify Production reminder state.

Preferred dependency direction:

```text
Presentation
    ↓
Application
    ↓
Domain

Infrastructure
    ↑ implements persistence and platform abstractions
```

Normal softball scoring is authoritative. Beer Rules observe committed scoring
semantics and derive separate consequences; Beer evaluation must not redefine
scoring, runner movement, outs, innings, score, batting order, plate-appearance
classification, or statistics.

Beer-rule configuration uses normalized Team defaults and historically stable
Game-scoring snapshots. The six plate-appearance rules are copied when scoring
starts; later Team edits do not reinterpret active or completed Games. In-game
edits are prospective, and existing automatic Beer ledger consequences remain
immutable. Scoring Undo/Redo restores its snapshotted consequences but does not
restore configuration changes. Low Man remains Team/date-level configuration
and is not part of ordinary Game or plate-appearance Beer evaluation.

Low Man is the deliberate recalculation exception: it aggregates authoritative
numeric batting-statistic components across every completed Game for one Team
and scheduled playing date, then reconciles only that date's automatic Low Man
ledger results. It must not average formatted or per-Game rates, run when a Game
completes, or become a global Beer recalculation workflow.

Automatic Beer consequences are immutable scoring-derived history. Manual rule
count adjustments and BROUGHT credits layer on top without changing scoring or
automatic rows. Beer accounting is lifetime for one Team record, and balances
such as OWED are derived from ledger data rather than persisted as independently
mutable truth. Date-scoped automatic Low Man results are the narrow exception to
immutability because deliberate recalculation must reconcile corrected completed
Game history while preserving all manual adjustments.

Team Beer accounting may include Players without current TeamPlayer membership
when they have a nonzero Team-scoped Beer balance. Active/current roster Players
are displayed first, followed by non-roster Players with nonzero balances. Beer
balance alone must never create or reactivate roster membership.

Copy Team to New Year is one atomic local transaction. It copies current-year
League Season definitions, active roster memberships for active Players with
jersey numbers, and all seven resolved Team Beer settings. It does not copy
Games, scoring or lineup history, statistics, automatic or manual rule history,
Low Man rows, or BROUGHT rows. The source Team is never mutated.

Only the source's signed unresolved Beer balance crosses the Team-Year boundary.
Each nonzero Player balance becomes one immutable destination CarryForward row
with the stable PlayerId and source Team ID; zero creates no row. CarryForward
contributes directly to OWED, affects no Beer-rule count, and is not BROUGHT:
OWED equals signed carry-forward plus current-year effective obligation minus
current-year BROUGHT. Carry-forward alone never creates or reactivates roster or
Player state.

## Presentation Layer

The Presentation layer is responsible for:

- .NET MAUI XAML pages
- XAML layouts, styles, templates, and bindings
- View models
- Observable presentation state
- Commands
- Navigation
- Dialogs
- Touch interactions
- Tablet-friendly layouts
- Platform-facing behavior

Use XAML and MVVM as the default presentation approach.

Design UI and interaction work for live use on the primary 10-inch Android tablet. Prioritize:

- Large, finger-friendly touch targets
- Clear readability at a normal tablet viewing distance
- Minimal taps
- One-handed operation where practical
- Stable placement of frequently used controls
- Touch-appropriate spacing rather than mouse-level precision
- Simple, responsive layouts that remain usable on the target tablet
- Reliable offline operation during games

Avoid hover-only, right-click-only, keyboard-only, or other desktop-only interaction assumptions. Avoid unnecessarily dense desktop-style forms. Do not introduce arbitrary control-size measurements unless approved measurements already exist in the repository.

Use CommunityToolkit.Mvvm for common MVVM infrastructure such as:

- ObservableObject
- Generated observable properties
- Relay commands
- Async relay commands

Keep code-behind limited to view-specific behavior that cannot be expressed cleanly through XAML, bindings, behaviors, or commands.

Do not place business logic in Views or code-behind.

View models should coordinate page behavior and call application services. They should not contain core softball rules.

Presentation code must not contain:

- Direct SQLite queries
- Core scoring calculations
- Runner-movement rules
- Beer-rule logic
- Statistics formulas
- Game-state reconstruction rules

## Application Layer

The Application layer is responsible for application workflows and use cases, including:

- Creating teams
- Creating players
- Managing rosters
- Building lineups
- Starting games
- Recording batter results
- Applying runner movement
- Adding or removing players during games
- Completing games
- Correcting completed games
- Recalculating statistics

The Application layer should coordinate domain behavior and infrastructure abstractions without depending directly on UI implementation details.

## Domain Layer

The Domain layer is responsible for core rules and models, including:

- Teams
- Seasons
- Players
- Rosters
- Games
- Lineups
- Batter results
- Runner movement
- Game state
- Statistics
- Home-run rules
- Beer-rule evaluation
- Undoable game actions

The Domain layer must remain independent of:

- .NET MAUI
- XAML
- SQLite
- Android APIs
- Windows APIs
- UI resource files

Domain logic should be independently testable.

## Infrastructure Layer

The Infrastructure layer is responsible for:

- SQLite database access
- Database initialization
- Migrations
- Repository implementations
- Local settings
- Persistence services
- Platform-specific implementations

Infrastructure should implement abstractions used by the Application layer.

Do not allow Infrastructure concerns to leak into Domain logic.

## Coding Standards

AI assistants and contributors must:

- Prefer small, focused changes.
- Preserve the established architecture and dependency direction.
- Avoid unnecessary abstractions.
- Keep business logic out of Views.
- Keep business logic out of code-behind.
- Favor maintainability and clarity over clever implementations.
- Follow existing naming and formatting conventions.
- Use async APIs where appropriate.
- Avoid adding NuGet packages without clear justification.
- Avoid unrelated refactoring during feature work.
- Keep methods and classes focused on one clear responsibility.
- Prefer direct, readable code over speculative extensibility.

Do not introduce patterns, frameworks, or dependencies only because they are common in other projects.

## Project Conventions

### User-Facing Text

Do not hardcode runtime user-facing text in C#, XAML, or other application implementation files.

Store user-facing text in centralized `.resx` resource files, including:

- Page titles
- Button labels
- Menu items
- Dialog titles
- Confirmation messages
- Validation messages
- Empty-state messages
- Status text
- Accessibility text
- Other labels and text presented to the user
- Dynamic text templates

Use formatted resource templates for dynamic user-facing text. Insert runtime values such as player names, team names, scores, inning numbers, and base names through those templates rather than constructing complete user-facing sentences in code.

Internal enum names and technical identifiers may remain in code, but their user-visible labels must come from resources.

### Application Configuration

Centralize application configuration values rather than scattering them throughout the codebase.

Use focused, strongly typed configuration classes unless another mechanism is more appropriate for a specific framework or platform requirement. Supply configuration through dependency injection when appropriate.

Application configuration includes values that may reasonably vary by application setup, environment, deployment, platform, or future product decision, such as:

- Database file names
- Feature defaults
- Application behavior settings
- Platform-dependent options
- Other centralized runtime settings

Keep configuration classes focused. Do not use them as generic dumping grounds or create configuration entries merely to eliminate every string or numeric literal from source code.

Distinguish application configuration from user-facing resources and implementation-specific technical literals. Application configuration must remain centralized even when it currently has only one consumer.

### Shared Technical Constants

Centralize shared technical identifiers when reuse improves clarity, consistency, or maintainability.

Keep constants close to the feature, service, class, or responsibility that owns them. Prefer focused constants classes or static members associated with that responsibility.

Avoid broad, generic `Constants` classes that collect unrelated values. Do not promote a value to a shared constant solely because it is a string literal.

Focused centralization is appropriate when the same technical identifier is reused, must remain consistent across multiple locations, or has meaningful domain or infrastructure significance.

### Inline Technical Literals

One-use implementation details may remain inline when moving them elsewhere would reduce readability or add unnecessary indirection. Inline technical literals are appropriate when they are implementation-specific, locally understandable, and are neither application configuration nor user-facing text.

Examples include:

- One-use SQL statements
- Framework identifiers
- XAML binding paths
- Resource keys
- Platform identifiers
- Build metadata
- Format strings used only by a local implementation
- Similar framework-required or implementation-specific literals

Keep these values inline only when they are not reused and do not represent a configurable application decision. Evaluate repeated or semantically important technical identifiers for focused centralization.

User-facing text must still use `.resx` resources even when it appears only once. Application configuration must still be centralized even when it currently has only one consumer.

Use this decision boundary:

- Text shown to the user belongs in `.resx` resources.
- Values representing application behavior or runtime setup belong in centralized configuration.
- Reused technical identifiers may belong in focused constants near their owning responsibility.
- Local one-use implementation details may remain inline when centralization would make the code less clear.

The project does not require every string literal or numeric literal to be extracted.

### Async APIs

Use asynchronous APIs for:

- Database access
- File access
- Long-running work
- Platform operations
- Other I/O-bound operations

Do not use asynchronous code where it adds complexity without practical benefit.

### Naming and Style

Follow the naming, formatting, and organizational conventions already established in the repository.

Do not introduce a competing style within the same codebase.

### Soft Delete

Use soft delete for records that may be referenced by historical data, including teams and players.

Soft delete should normally:

- Set `IsActive` to `false`
- Hide the record from normal active views
- Preserve historical relationships
- Allow later reactivation

Do not permanently delete historical records unless a later story explicitly requires it.

### Story Scope

Do not implement future stories early.

Only add functionality that:

- Is explicitly required by the current story
- Is necessary to support the current story
- Has a documented reason for being included

## AI Development Expectations

AI assistants must work within the current story and repository context.

They must:

- Read `FEATURE_HISTORY.md` before implementing a story.
- Use the feature history to understand current implementation state and reuse existing components and patterns.
- Avoid rebuilding existing functionality or introducing parallel infrastructure without a documented reason.
- Stay within the scope of the current story.
- Preserve the approved architecture.
- Prefer incremental implementation.
- Make the smallest change that satisfies the acceptance criteria.
- Document assumptions that affect implementation.
- Explain any necessary deviation from the story before making it.
- Reuse existing patterns when they are appropriate.
- Review nearby code before introducing a new pattern.
- Keep generated code readable and maintainable.
- Check the current implementation state before assuming a planned feature already exists.

AI assistants must not:

- Redesign the architecture without approval.
- Introduce speculative features.
- Implement future stories early.
- Add unrelated cleanup or refactoring.
- Add NuGet packages without clear justification.
- Create abstractions without a current need.
- Replace existing patterns only because another approach is preferred.
- Claim that planned architecture is already implemented.
- Leave TODO comments unless explicitly requested.
- Silently ignore acceptance criteria.

When requirements are unclear, document the assumption or ask for clarification rather than inventing product behavior.

## Current Story Discipline

Before making changes, identify:

- Relevant completed work, reusable components, and known limitations recorded in `FEATURE_HISTORY.md`
- The current story goal
- Included scope
- Excluded scope
- Acceptance criteria
- Expected deliverables
- Existing files likely to be affected

Future stories should include an acceptance criterion requiring `FEATURE_HISTORY.md` to be updated with an accurate implementation summary. Equivalent wording is acceptable when it requires the same outcome.

Before declaring the story complete:

- Review every acceptance criterion.
- Confirm no excluded functionality was introduced.
- Confirm no unrelated files were changed.
- Report assumptions and deviations.
- Report validation results accurately.
- Add a concise, implementation-focused entry to `FEATURE_HISTORY.md`.
- Confirm the entry matches the actual implementation and preserves prior entries unless an intentional correction is required.

## Testing and Validation Expectations

Preserve existing build success and working application behavior.

The user manually runs builds, application launches, and tests unless the user explicitly requests otherwise. Provide exact, repository-appropriate validation instructions and report accurately what was and was not run.

For each story:

- Add or update tests when the story introduces testable behavior.
- Add or update focused automated tests when Core or Infrastructure repository behavior changes, testing public observable behavior and persisted state where meaningful.
- Do not add meaningless tests only to increase test count.
- Keep existing tests passing.
- Do not break existing functionality.
- Validate every acceptance criterion before declaring completion.
- Run relevant focused tests before declaring work complete, and run the full suite when repository behavior or shared test infrastructure changes.
- Report which tests and builds were run.
- Report any tests or validation steps that were not run.
- Report failures, skipped tests, warnings, and environmental limitations accurately.
- Do not claim validation succeeded without evidence.
- Review the new `FEATURE_HISTORY.md` entry.
- Confirm the entry describes the actual implementation.
- Confirm prior history entries remain intact unless intentionally corrected.
- Confirm `FEATURE_HISTORY.md` will be included in the story commit.

When the story changes only documentation, tests may be unnecessary unless the documentation change affects generated output or tooling.

Android validation is required for UI and interaction stories because Android tablet behavior is the primary acceptance standard. Windows remains useful for development and secondary validation.

Codex may create Android UI/Appium tests, but MUST NOT execute emulator/Appium tests unless the user explicitly authorizes that run. When authorized, run Android UI acceptance tests through `scripts/run-android-ui-tests.ps1`. Repository orchestration may stop only Appium and emulator processes that the runner itself started and precisely tracks; never broadly kill Node, Appium, emulator, qemu, adb, or Crashpad processes.

Android UI/Appium automation must use its dedicated configured test database and a dedicated UI-test AVD that is separate from the emulator reserved for Visual Studio and manual development. The repository runner must default to the dedicated UI-test AVD and must not select the reserved development AVD. Test setup may reset or recreate only the dedicated test database and must never clear application package data or modify, replace, delete, or migrate the normal development database.

Major scoring and tablet-interaction stories should be exercised on the actual target 10-inch Android tablet when it is available. Emulator validation is useful and is acceptable until the physical tablet is available when documented honestly, but it does not permanently replace real-device validation for major touch workflows. Non-UI stories should use validation appropriate to their scope.

At minimum, validate the affected supported targets when practical:

- Android
- Windows

Validation should be proportional to the change. Small documentation changes do not require unrelated exhaustive testing.

Repository tests must use isolated temporary SQLite databases, never use the real application database, clean up temporary database artifacts, and remain independently and repeatedly runnable. Prefer focused tests and minimal reusable helpers; avoid unnecessary mocking, speculative test abstractions, and broad generic fixture infrastructure without a demonstrated need.

Repository tests do not provide MAUI UI coverage and must not be reported as Android or Windows runtime validation. UI behavior still requires appropriate manual or UI-level validation. Keep test changes story-focused and report untested areas honestly rather than adding unrelated future-feature coverage.

Scoring-engine development requires comprehensive table-driven tests covering base states, out counts, batter outcomes, runner movement, corrections, and derived statistics. Repository integration tests may use isolated temporary SQLite databases. UI automation is not required unless a later story explicitly adds it.

Batting statistics must derive from authoritative batting outcomes through the centralized statistics calculator. UI and future correction workflows must not independently calculate PA, AB, H, AVG, OBP, or SLG.

Filtered batting statistics must derive from authoritative selected Completed Game data through the same centralized statistics calculation. Filter selection is transient presentation state; do not persist duplicate aggregate snapshots for filter results.

Every future `BatterResult` must explicitly define its statistical contribution to PA, AB, Hit, Total Bases, and OBP numerator/denominator behavior.

Every newly introduced committed scoring action must participate atomically in persisted scoring Undo/Redo and include focused reversal/restoration regression coverage.

Pending-play Undo/Redo uses persisted semantic checkpoints. One result selection plus its automatic resolver defaults is one checkpoint; meaningful scorer decisions create checkpoints, backtracking to an earlier semantic state collapses redundant history, and progressive runner decisions remain separately reversible. Committing a play collapses its temporary pending checkpoints into the single committed-action boundary.

## Completion Review and Definition of Done

Before reporting a story as complete:

- Review the final diff.
- Confirm only expected files changed.
- Confirm the solution still builds where appropriate.
- Confirm all relevant tests pass.
- Confirm the implementation matches the acceptance criteria.
- Confirm excluded scope was not implemented.
- Note any deferred work without implementing it early.
- Confirm `FEATURE_HISTORY.md` contains an accurate implementation summary for the story.
- Confirm the new history entry has been reviewed and matches the actual implementation.
- Confirm prior history entries remain intact unless intentionally corrected.
- Confirm the history update will be included in the same story-focused commit as the implementation.

A story is not complete until its feature-history entry is included.

## Git and Commit Expectations

Keep commits focused on the current story.

Contributors and AI assistants must:

- Avoid unrelated changes.
- Keep diffs small and reviewable.
- Preserve a clean commit history.
- Use clear commit messages that describe the completed work.
- Review `git diff` before committing.
- Review `git status` before committing.
- Confirm only intended files are staged.
- Do not combine unrelated stories in one commit.
- Update `FEATURE_HISTORY.md` before every story commit.
- Include the implementation and its feature-history update in the same story-focused commit.

Do not rewrite published history unless explicitly instructed.

Do not commit:

- Build output
- Temporary files
- Local database files
- User-specific settings
- Secrets
- Credentials
- Machine-specific configuration

## Documentation Expectations

Update documentation when a story changes:

- Architecture decisions
- Project conventions
- Public behavior
- Data models
- Development setup
- Testing requirements
- Implementation status

Use each document for its intended purpose.

### Architecture Documentation

Architecture documentation records:

- Stable product decisions
- Technical architecture
- Dependency direction
- Core behavior
- Project constraints
- Scope boundaries

### Feature History

`FEATURE_HISTORY.md` is the canonical record of completed implementation work and current repository state. Developers and AI assistants must read it before starting a story and update it after implementation and validation but before committing.

The feature-history document records:

- Completed stories
- Implementation summaries
- Files and areas changed
- Important architecture and design decisions
- Reusable components and patterns
- Tests and validation performed
- Known warnings and limitations
- Deferred work
- Current implementation state

Do not describe planned work as implemented.

Keep entries concise, chronological, and implementation-focused. Do not copy full user-story specifications or acceptance criteria into the history. Preserve prior entries unless correcting inaccurate information, and preserve historical clarity when making corrections. Never invent unknown information.

Documentation must accurately distinguish between:

- Approved architecture
- Current implementation
- Planned future work

When completing a story, add its reviewed history entry to `FEATURE_HISTORY.md` and include that change in the same commit as the story implementation.

## Version 1 Project Boundaries

Do not implement the following unless a later story explicitly includes them:

- Messaging
- Chat
- Team invitations
- Communication scheduling
- Opponent roster management
- Live streaming
- Spray charts
- Defensive statistics
- Pitch counts
- Balls
- Strikes
- Errors
- GPS
- Cloud synchronization
- Multi-user editing
- Push notifications
- User accounts
- Server-side services

Do not expand a story beyond its approved scope to prepare for these features.

## Current Implementation Status

The repository currently contains three production projects and one automated test project: a .NET MAUI application targeting Android and Windows, a platform-independent Core library, an Infrastructure library for SQLite persistence, and Tests for current repository behavior. Inspect `FEATURE_HISTORY.md` and the current repository before describing specific features or foundations as implemented.

The automated test project covers the current Team and Player repository behavior using isolated temporary SQLite databases. An Application project remains planned, not implemented merely because it is documented here. Complex roster and lineup workflows, scoring rules, statistics, and beer rules remain deferred until their stories are implemented.

AI assistants must inspect the current repository before assuming any deferred feature exists.
