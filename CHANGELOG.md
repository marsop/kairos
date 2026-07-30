# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.6.0] - 2026-07-29

### Added

- **Statistics**: Added a new 'Today' view to the Statistics page alongside Week and Month views.
- **Avatars**: Added new images for the kawaii avatar style.
- **Settings**: Added a toggle for Timeular settings under Advanced settings.
- **Budgets**: Added a new rolling budget feature for activities.

### Changed

- **Budgets**: Budget progress bars now use the colors defined in settings — green below the threshold, yellow between threshold and 100%, red when over budget.
- **Budgets**: When the minimum setting is enabled, each budget can have a minimum hours value. Progress bars use blue when tracked time is below the minimum, and a small marker shows the minimum position on the bar.
- **Budgets UI**: Budget definition now labels inputs as "Min Hours" and "Max Hours", and shows a read-only calculated threshold hours value between them when a threshold percentage is configured.
- **Budgets UI**: Budget editor now enforces `Min Hours <= Max Hours` both in input constraints and in saved values.
- **Activities UI**: Formatted the lower banner to display the active activity name and comment inline on the same line, removing the standalone comment block.
- **Settings UI**: Display only major, minor, and patch version (plus pre-release tags) in the About section, removing the Git commit hash.
- **Settings UI**: Redesigned the Budgets color configuration controls into a clearer card/grid layout with improved color picker presentation.
- **Settings UI**: Improved budget color section titles by using consistent terminology ("Below min", "Between min and threshold", "Between threshold and max", "Above max") instead of verbose "Color for" prefixes and ambiguous terms.
- **Statistics UI**: Moved the timeline chart in the Statistics tab above the budgets section.
- **Activities UI**: Hide activity index from card UI and display it via tooltip.
- **Timeular UI**: Hide Timeular status indicator when settings are disabled.

### Fixed

- **Statistics**: Fix timeline graph calculation to start count from 0 for the selected period.
- **Statistics**: Fix color timeline chart segments by active activity.
- **Supabase Sync**: Sync activity budgets (allocated/minimum hours and period type) across devices, not just budget display settings.
- **UI**: Fix CSS stacking issue causing dropdown clicks to be intercepted by calendar.
- **CSV**: Fix CSV export button not clicking in Calendar mode.

## [1.5.0] - 2026-07-22

### Added

- **UI**: The Timeular tracker icon tooltip now displays its battery percentage dynamically.
- Feature: Clicking an event in the Calendar History view now adapts the zoom level and scroll position to center it and take exactly 50% of the visible space.
- **History**: Allow editing the comment of an active time-tracking event in the List view.
- **History UI**: Added sync status indicators (🔗/⛓️‍💥) for activity events in the History views.
- **Avatars**: Generated and added new tutorial avatar poses (`tutorial-avatar-11.png`) for both Kawaii and Zarzaparrilla styles to reduce image reuse in the tutorial.
- **Settings**: Added a new Advanced setting for configuring sticky events duration.

### Changed

- **History UI**: Improved visual accuracy of event blocks in the Calendar view by accounting for seconds in their start and end calculations to prevent short false visual overlaps.
- **Localization**: Localized hardcoded UI text in razor components.
- **Logging**: Added structured logging for Timeular Bluetooth auto-reconnect outcomes.
- **Supabase Sync**: First-sync divergence now prompts conflict resolution when both local and server activity events contain different data.
- **Architecture**: Reorganized the solution from the old `Kairos.Shared`/`Kairos.Web` split into `Kairos.Core`, `Kairos.Application`, `Kairos.Infrastructure`, `Kairos.App`, and `Kairos.Web` to establish clearer boundaries between domain, use cases, infrastructure, UI, and host startup.
- **Namespaces**: Realigned namespaces to match the new project boundaries (`Kairos.Core.*`, `Kairos.Application.*`, `Kairos.Infrastructure.*`, `Kairos.App.*`) and updated all references/usings across source and tests.
- **Codebase Hygiene**: Removed obsolete project references/paths and cleaned unused `using` directives after the refactor.

### Fixed

- **Supabase Sync**: Fixed syncing of user settings to Supabase (malformed API query and missing sticky events duration mapping).
- **Supabase Sync**: Prevented silent server overwrite when conflict UI listeners are unavailable by safely keeping local activity events.
- **Supabase Sync**: Prevented redundant immediate re-sync loops triggered by local state notifications after applying server event updates.
- **Tests**: Added and updated synchronization tests to lock in conflict-handling and realtime re-sync behavior.

## [1.4.0] - 2026-07-15

### Added

- **Settings**: Added a new Advanced setting to automatically delete events that are shorter than a configured duration (in seconds).
- **Settings**: Added a 'Show Advanced Settings' toggle in the General section to hide/show the Advanced settings area.
- **Settings**: Added a link to the project CHANGELOG on GitHub in the About section of Settings.
- **UI Enhancement**: Added a permanent Tracker status indicator to the bottom navigation bar for quick visibility of the Timeular connection.
- **History Navigation**: Added "Previous Day" and "Next Day" navigation buttons to the History page date picker toolbar.
- **UI Enhancements**: Added a pulsating animation effect to active events in both Calendar and List views on the History page to make them more obvious.

### Changed

- **Settings Layout**: Moved "Activity Groups" toggle from General to a new Advanced section.
- **UI Tweaks**: Standardized Sync Conflict dialog style to match other application modals and localized strings.

### Fixed

- Fix sync conflict dialog falsely appearing on page refresh.
- Prevent URI Too Long error in Supabase activity event synchronization.
- Fix latest entries disappearing on page refresh by fetching the newest 1,000 entries from Supabase instead of the oldest.

## [1.3.0] - 2026-06-26

### Changed

- **UI Tweaks**: Made the "Display Language", "Theme" and "Tutorial Avatar" settings options more compact.
- **UI Tweaks**: Added icons to the "Dark" and "Light" theme options in Settings.

### Fixed

- **Sync Conflict**: Fixed an issue where stopping an activity triggered a false "Sync Conflict" dialog by introducing an in-memory snapshot to correctly identify actual server changes.

## [1.2.0] - 2026-06-26

### Added

- **Supabase Integration**: Implemented Supabase for authentication, settings management, and activity synchronization.
- **Real-time Sync**: Optimized Supabase sync using real-time subscriptions and dedicated table for activity events.
- **Versioning**: Integrated NerdBank.GitVersioning to accurately track and display application version.
- **Statistics Page**: Added a new Statistics page with charts and styling.
- **History Enhancements**: Added calendar view, date filtering, and CSV export functionality to the history page.
- **Activity Customization**: Added emoji and color support for activities.
- **Activity Comments**: Implemented activity start comment prompt and confirmation dialog.
- **Metadata Support**: Added a metadata field to activities and included it in exports.
- **UX Improvements**: Added focus-on-click for browser notifications, and display last Supabase sync time in settings.
- **Language Persistence**: Implemented saving selected language to local storage and export/import functionality.
- **Language Icons**: Added flags/icons to language selection options.
- **Website Logo**: Added the Kairos logo to the application.
- **README**: Created a comprehensive README.md.
- **Project Structure**: Initial setup of the Blazor + MAUI Hybrid application.
- **Pages**: Added Overview, Activities, and Timeline pages with core functionality.

### Changed

- **UI Tweaks**: Made the "Total Tracked Time" card on the Statistics page more compact by changing its layout to horizontal.
- **Core Tracking System**: Refactored the time tracking system to use "activities" instead of "meters".
- **CSV Optimization**: Optimized CSV export using `CsvHelper`, and updated exports to use Activity ID while omitting extra metadata/duration columns.
- **Code Health**: Refactored application logging to use ASP.NET Core `ILogger<T>` instead of `Console.WriteLine`.
- **UI Tweaks**: Made activity cards and history cards more compact, enhanced activity button styles with animations and hover effects.
- **Settings Layout**: Renamed "Settings" section to "General" in `Settings.razor`.
- **Tutorial Enhancement**: Updated tutorial steps for improved guidance, including language and avatar selection.
- **History View**: The Calendar view now takes up all available vertical space and automatically scrolls to the first activity (or 07:30 by default).
- **Factory Reset**: Moved the factory reset functionality from the Sync page to the Settings page.
- **Localization**: Fixed "Vorarlbergerish" language shortname issue.
- **Platform Upgrade**: Migrated projects, CI workflow, and docs from .NET 9 to .NET 10.

### Fixed

- **Security**: Fixed an XSS vulnerability in Statistics chart rendering.
- **Error Handling**: Logged exceptions in `NotificationService` browser permission handlers.
- **UI Bugs**: Fixed Timeular Connect/Disconnect button visibility and styling.
- **Documentation**: Fixed empty summary XML tags in code.

### Removed

- **Activity Factor**: Removed Activity Factor from models, UI, and event payload.
