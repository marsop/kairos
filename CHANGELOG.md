# Changelog

All notable changes to this project will be documented in this file.


The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased]

## [1.9.0] - 2026-08-21

### Changed
- **UI**: Unified focusout behavior across custom dropdown components by ensuring `tabindex="-1"` and `@onfocusout` handlers are applied correctly to prevent unintended dropdown persistance when clicking outside.


### Fixed
- **CI/CD**: Fix deploy workflow hanging for 1h+ by reordering steps so the base-tag rewrite happens after Playwright tests instead of before.
- **CI/CD**: Add `timeout-minutes: 15` to deploy job to prevent runaway pipeline runs.
- **CI/CD**: Update `JamesIves/github-pages-deploy-action` from `@v4` to `@v4.8.0` to resolve Node 20 deprecation warning.

## [1.8.0] - 2026-08-15

### Added
- **History UI**: Added a confirmation dialog before exporting a daily CSV, featuring a date picker and a read-only filename preview.
- **History UI**: Show start and end times for selected calendar events.
- **History UI**: Add activity legend to history charts showing distinct colors and names.
- **AGENTS.md**: Add testing guideline requiring automated tests for all new functionality or modifications.
- **AGENTS.md**: Add Playwright authentication mocking documentation.

### Fixed
- **UI**: Fixed a bug where editing the comment of a completed activity event would overwrite typed text on background refreshes, and incorrectly force focus onto the start time input.

### Changed
- **UI**: Standardized the design of edit action buttons in the Activities and Settings pages by replacing the solid, scaling `.activity-action-btn` style with the transparent, color-changing `.edit-btn` style for a consistent visual experience across the application.
- **UI**: Unified Save, Cancel, and Delete buttons styling globally across components to maintain consistent design.
- **Budgets UI**: Improved clarity of the budget modal text by changing "Hours for this month/week/today" to "Budget Type: Monthly/Weekly/Daily".
- **Budgets UI**: Remove "Default" view from Budgets page, making "Budget Usage" the default.
- **Budgets UI**: Update Budgets page banner styling to match activity banner.
- **Settings UI**: Removed the inline trash-icon button for deleting Activity Groups. Deletion is now only available from within the group editing dialog.
- **Settings UI**: Unify Add Group button text with Add Activity button.
- **Settings UI**: Update "Add group" button styling to match "Add activity" button.
- **Activities UI**: Move ActiveEvent banner to the top in Activities page.
- **Activities UI**: Use a proper edit icon button instead of an emoji on the active event card in the Activities page.
- **Activities UI**: Change "Rename activity" to "Edit activity" in UI and localized resources.
- **History UI**: Moved chart type selector (line vs bar) from main toolbar to top-right corner of graph area in History view.
- **History UI**: Extract event edit dialog and allow editing active events.
- **UI**: Unify edit icons and dropdown style across the application.
- **AGENTS.md**: Updated AGENTS.md to ensure CHANGELOG.md is updated with every change made.

### Fixed
- **UI**: Fix custom dropdown premature closing on item click and prevent focus loss by using onmousedown.
- **History UI**: Move history event deletion into edit dialog to prevent accidental clicks.
- **History UI**: Hide x-axis time labels on bar chart.
- **Activities UI**: Fix activity edit button position and visibility.
- **Sync**: Fix activity group truncation on sync.


## [1.7.0] - 2026-08-05

### Added

- **History**: Added a toggle to switch between line and bar charts in the History view.
- **Navigation**: Added keyboard navigation cyclically through main tabs using PageDown/PageUp.
- **Activities**: Enabled activating activities using numpad 1-8.
- **Settings**: Added sound effects on user interactions, controlled by a settings toggle.
- **Settings UI**: Added description text below each budget setting (Minimum, Threshold, Budget Type, Notifications) to explain what each option does.
- **Settings UI**: Added Activity Group management in Settings (visible only when Activity Groups are enabled), including listing groups with activity counts plus add/remove group actions.
- - **History Stats**: The "Day" period in the Stats view now shows a date picker, allowing the user to navigate to any day (same prev/next arrow controls as the List and Calendar views).
- **History Stats**: The "This Week" button is now "Week" with a week picker (defaults to the current week). The "This Month" button is now "Month" with a month picker (defaults to the current month). Both support prev/next navigation arrows.
- **Budgets**: When budget notifications are enabled in Settings, the app now sends a notification when an activity reaches its threshold, minimum, or maximum (100%) budget for the current period. Each event fires at most once per period.
- **Tutorial**: Added a "Skip tutorial" button on the initial setup step (language and avatar selection) that completes the tutorial immediately.
- **Budgets**: Added sort controls to the Budgets tab to order activities by tracked time (descending) or by budget usage percentage (descending). Activities without a budget are pushed to the bottom when sorting by budget usage.
- **Budgets UI**: Added a context summary to the Budgets page that shows the active budget type and the current period start/end dates.
- **Budgets UI**: Budget progress bars now display visual markers for key budget points: minimum (gray), threshold (yellow), maximum (red), and expected progress (secondary, with border). The expected progress marker shows where you should be today if the budget will be completed 100% at the end of the period. Markers are enhanced with improved size, opacity, and subtle shadows for better visibility.

### Changed
- **Settings UI**: Removed the inline trash-icon button for deleting Activity Groups. Deletion is now only available from within the group editing dialog.
- **UI**: Added focus management for input fields in History and Statistics pages.
- **Activities UI**: Replaced Activity Group header buttons with a dropdown selector to make group switching reliable when multiple groups are available.
- **Activities UI**: Removed the extra "Activity Groups" text label from the Activities header since the dropdown selector already makes the context clear.
- **Activity Groups**: Group names can now be renamed from Settings (in the same section used to delete groups), and custom names are shown in both Settings and the Activities group selector.
- **Supabase Sync**: Custom Activity Group names now sync across devices via a dedicated `user_activity_groups` table.
- **Supabase Sync**: Activity groups are now stored as row-based records with GUID `group_id` values and stable `group_order`, replacing index-based column storage.
- **Settings UI**: The add-group action now shows `+ Group` and initializes new groups with a date-based default name (for example `Group 2026-07-31`).
- **Activity Groups**: Group color and icon are now configurable from Settings and synchronized with Supabase.
- **Activity Groups**: Deleting a group now moves its activities into the previous group (instead of moving them to Group 0).
- **Translations**: Translated all previously untranslated budget settings labels and color range names (de, es, gl, gsw). Also translated the `StickyEventsDuration` setting in all non-English locales.
- **Activities**: Removed activity count caps. There is no max activities limit anymore, including when Activity Groups are enabled.
- **Activities**: When Activity Groups are turned off, activities from all groups are shown together in a single list instead of hiding non-active-group activities.
- **Budgets UI**: The threshold hours field in the budget editor now renders as a read-only calculated value (dimmed text, dashed border) instead of an editable input, making it clear it cannot be modified directly.
- **Settings UI**: Budget threshold input now shows a `%` suffix to make it clear the configured value is a percentage.
- **Budgets UI**: The selected budget ordering is now persisted in browser storage and restored when navigating back to the Budgets tab.

### Fixed

- **Navigation**: Fixed JS interop initialization bug for keyboard navigation across tabs.
- **Activity Groups**: Fixed group deletion edge cases so activities from the removed group are reassigned correctly and group IDs are compacted to keep all activities visible.
- **Budgets**: Fixed budget settings not loading from Supabase on a fresh browser. The settings service was initialized before the auth service, so the user was never authenticated when the remote fetch ran, causing defaults to be used and overwrite remote data.
- **Budgets**: Improved error handling and logging for budget settings sync operations to better diagnose synchronization issues.
- **Dates UI**: Standardized user-visible date formatting to ISO (`yyyy-MM-dd`) across history and budget period labels.

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
