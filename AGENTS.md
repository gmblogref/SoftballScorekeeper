# AGENTS.md

## Project Overview

Softball Scorekeeper is a lightweight, offline-first scorekeeping application for adult slow-pitch softball.

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

Primary platform:

- Android tablet

Secondary platform:

- Windows

When platform-specific compromises are necessary, Android tablet usability takes priority.

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

Do not hardcode user-facing strings in application code.

Store user-facing text in centralized `.resx` resource files, including:

- Page titles
- Button labels
- Menu text
- Dialog titles
- Confirmation messages
- Validation messages
- Empty-state messages
- Status text
- Accessibility labels
- Dynamic text templates

Insert runtime values through formatted resource templates.

Internal identifiers and enum values may remain in code, but their user-visible labels must come from resources.

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

- The current story goal
- Included scope
- Excluded scope
- Acceptance criteria
- Expected deliverables
- Existing files likely to be affected

Before declaring the story complete:

- Review every acceptance criterion.
- Confirm no excluded functionality was introduced.
- Confirm no unrelated files were changed.
- Report assumptions and deviations.
- Report validation results accurately.

## Testing and Validation Expectations

Preserve existing build success and working application behavior.

For each story:

- Add or update tests when the story introduces testable behavior.
- Do not add meaningless tests only to increase test count.
- Keep existing tests passing.
- Do not break existing functionality.
- Validate every acceptance criterion before declaring completion.
- Report which tests and builds were run.
- Report any tests or validation steps that were not run.
- Do not claim validation succeeded without evidence.

When the story changes only documentation, tests may be unnecessary unless the documentation change affects generated output or tooling.

At minimum, validate the affected supported targets when practical:

- Android
- Windows

Validation should be proportional to the change. Small documentation changes do not require unrelated exhaustive testing.

## Completion Review

Before reporting a story as complete:

- Review the final diff.
- Confirm only expected files changed.
- Confirm the solution still builds where appropriate.
- Confirm all relevant tests pass.
- Confirm the implementation matches the acceptance criteria.
- Confirm excluded scope was not implemented.
- Note any deferred work without implementing it early.

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

The feature-history document records:

- Completed stories
- Implementation summaries
- Files and areas changed
- Tests and validation performed
- Deferred work
- Current implementation state

Do not describe planned work as implemented.

Documentation must accurately distinguish between:

- Approved architecture
- Current implementation
- Planned future work

When completing a story, provide a concise completion summary suitable for adding to the feature-history document.

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

The approved architecture includes:

- .NET MAUI
- C#
- XAML
- MVVM
- CommunityToolkit.Mvvm
- Layered responsibilities
- SQLite local persistence
- Centralized resource files
- Offline-first behavior

However, planned architecture must not be described as already implemented.

At the time this document was created, the repository contains the initial .NET MAUI application foundation targeting:

- Android
- Windows

The following remain deferred until later stories:

- MVVM infrastructure
- Dependency injection
- SQLite
- Resource files
- Navigation
- Domain models
- Team management
- Player management
- Scoring
- Statistics
- Beer rules

AI assistants must inspect the current repository before assuming any deferred feature exists.