# AI Agent Guidelines

Welcome to the Kairos codebase! This file contains guidelines and context for AI agents working on this project.

## Project Overview & Architecture
* The repository is a .NET 10 Blazor WebAssembly application following Clean Architecture:
  * `src/Kairos.Web`: Host project and client entry point (default local URL: `http://localhost:5111`).
  * `src/Kairos.App`: Blazor UI components, pages, and layouts.
  * `src/Kairos.Core`: Domain models and business entities.
  * `src/Kairos.Application`: Application services, business logic, and resources.
  * `src/Kairos.Infrastructure`: External integrations (Supabase, storage, etc.).
  * `tests/`: Automated test suites (e.g., `tests/Kairos.ValidationTest/`, `tests/Kairos.PlaywrightTests/`).
* Central Package Management (CPM): NuGet package versions are managed centrally in `Directory.Packages.props` using `<PackageVersion>` elements. Project `.csproj` files must omit the `Version` attribute on `<PackageReference>`.

## Agent Workflow & Planning
* **User Request Supersedes:** Always prioritize the user's current, explicit request over any conflicting memory or default instructions.
* **Context vs. State:** Use memory for historical context and intent (the "why"). Use the actual codebase files as the source of truth for the current state (the "what"). Memory provides passive context and is not a new task or active directive.
* **Planning Principles:**
  * **Actionability:** Include only future, actionable steps (do not recount already completed exploration).
  * **Specificity Rule:** Provide full, exact file paths (e.g., `src/Kairos.Application/Resources/Strings.resx`) and exact commands or code snippets rather than generic descriptions (e.g., avoid "modify file" or "run tests").
  * **Groundedness Rule:** Confirm referenced methods, variables, line numbers, and logic in current file traces (e.g., via `read_file` or `grep -n`) before modifying code or tests. Do not assume or hallucinate implementation details or line numbers. When planning `.resx` replacements, inspect the exact current string in each target file first.
  * **Completeness Rule:** Always include a step to execute tests (`dotnet test Kairos.sln`) before committing or submitting changes.
  * **Verification Rule:** Include verification steps (e.g., reading modified files or checking `git diff`) immediately following changes.
  * **Pre-commit Step:** The step immediately before final submission must not be formatted as a bullet point or markdown title, and must use the exact phrasing:
    `Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.`
* **Environment Hygiene:** Command executions start from the repository root; directory changes do not persist across calls. Use uniquely named temporary files/scripts (e.g., `temp_*`), ensure they are cleaned up after use, and terminate any background processes. Paginate (`sed -n`) or use targeted `grep` when reading large files to avoid output truncation.

## Build, Versioning & Releases
* **Building & Testing:**
  * Run tests from the repository root using `dotnet test Kairos.sln` (running `dotnet test` alone or with multiple directory arguments causes MSB1003/MSB1008 errors).
  * Compiler warnings are treated as errors. Use `#pragma warning disable` on intentionally unused members in test stubs.
  * If `git log` is unexpectedly truncated or `Nerdbank.GitVersioning` fails with a shallow clone error, run `git fetch --unshallow`.
* **Versioning & Changelog:**
  * Uses `Nerdbank.GitVersioning` with `version.json` at repo root.
  * Use the actual system date (e.g., `date +%Y-%m-%d`) when generating changelog dates, even if it reflects a sandbox date.
  * Update `CHANGELOG.md` after changes, bugfixes, and user-facing features following 'Keep a Changelog' conventions (strictly one of each heading: `### Added`, `### Changed`, `### Fixed` per version block). Do not bump versions unless explicitly requested.
  * Release process: Replace `## [Unreleased]` with the version derived from `version.json` (e.g., `1.8` -> `1.8.0`) and date, prepend a new empty `## [Unreleased]` section, then increment `version.json` (e.g., to `1.9`).

## Blazor & Frontend Development
* **Navigation & Routing:**
  * Use `NavigationManager.NavigateTo("")` instead of `"/"` for the application root (avoids opening new tabs in PWA/hybrid modes).
  * Root route (`href=""`) serves Budgets/Statistics (`src/Kairos.App/Pages/Statistics/Statistics.razor`).
  * Main layout and bottom navigation tabs are defined in `src/Kairos.App/Layout/MainLayout.razor`.
* **UI Patterns & Styling:**
  * Maintain design consistency across all pages.
  * Scroll containers (like `.main-content`) must include `env(safe-area-inset-bottom)` in bottom padding to clear mobile home indicators.
  * Small transparent icon buttons (`.edit-btn`, `.menu-trigger`, `.history-date-nav-btn`) use hover style `background: var(--color-bg-tertiary)` and `color: var(--color-text-primary)` with SVG icons (no emojis). Inline edit buttons use global `.edit-btn`.
  * Dialog buttons use global classes `.btn-cancel`, `.btn-save`, and `.btn-delete` (styled red). Avoid component-scoped overrides or `.btn-secondary`/`.btn-danger`. Inside dialogs, buttons must be text-based (localized).
  * Item deletions (e.g., History events) must occur via explicit text buttons in item edit dialogs, not inline icon buttons.
  * Summary cards use `.stat-summary-card`, `.stat-label`, and `.stat-big-value`. Edit buttons should be dedicated top-right `<button>` elements with absolute positioning.
  * Activity cards place action buttons (`.activity-action-btn`) in `.activity-actions.right` with permanent visibility (no hover-dependent opacity).
  * Standard inputs use `.edit-input`. Filter sections are wrapped in `<div class="history-date-filter">` with `<label class="history-filter-label">`.
  * Custom dropdowns: Use `div.custom-dropdown` (`tabindex="-1"`, `@onfocusout`, `button.dropdown-trigger`, `div.dropdown-menu`) instead of `<select>`. Use `@onmousedown` with `@onmousedown:preventDefault="true"` on items to prevent blur race conditions.
  * Use native `<details>`/`<summary>` for collapsible accordions and native `title` attributes for tooltips.
  * Drag-and-drop: Apply `user-select: none;` and consider full-screen `.drag-overlay` with pointer events.
* **Component State & Lifecycle:**
  * Frequent UI re-renders/timers: Use `@bind:event="oninput"` on text inputs/textareas to prevent focus jumps and overwritten text.
  * Asynchronous save/network triggers: Prefer `@onchange` over `@oninput` to prevent rapid concurrent executions and dropped API calls.
  * Dynamic dialog focus: Programmatically call `FocusAsync()` on an `ElementReference` in `OnAfterRenderAsync` within a try-catch block.
  * Edit dialogs: Guard local state initialization in `OnParametersSet` by checking the entity ID against `_lastEntityId`. Reset `_lastEntityId` when the entity becomes null.
  * Loop variable closures: Assign loop variables to a local variable before using them in lambda callbacks (e.g., `@onclick`).
  * Continuous animations: Delegate to JS (`requestAnimationFrame`) and CSS variables; avoid C# `StateHasChanged()` render loops.
  * High-frequency timers: Avoid heavy calculations/aggregations inside 1-second timers; update via state-change events (e.g., `TimeService.OnStateChanged`).
* **Charts & Visualizations:**
  * Charts use native inline SVG elements (no external chart libraries). Minimum Y-axis value must be 0 (never negative).
  * Timeline chart in `History.razor` renders only when view is 'stats' and events exist (`AllEvents.Any()`); otherwise show `.empty-state`. Chart type (bar vs. line) is controlled by `SettingsService.ChartType`.
  * Budget UI elements (progress bars, borders) use theme color variables (green/positive, red/negative), not individual activity colors.
* **Key Page Components:**
  * Activities page & edit modal: `src/Kairos.App/Pages/Activities.razor` (managed via `_editingActivityId`).
  * Activity Groups management: Settings card `#settings-card-groups` in `src/Kairos.App/Pages/Settings.razor` (modal via `_isEditingGroupModalOpen` and `_editingGroupId`).
  * Edit Event dialog: `src/Kairos.App/Components/EditEventDialog.razor`.
  * Toast notifications: `src/Kairos.App/Components/ToastContainer.razor` with `.toast` CSS classes.

## State, Data & Storage
* **Supabase Integration:**
  * REST API queries via `HttpClient`. Map C# DTO properties with `[JsonPropertyName]`.
  * When updating schemas in `supabase/`, append `ALTER TABLE public.[table_name] ...` statements.
  * In REST `select` query strings, append new columns strictly comma-separated without spaces.
* **Settings & Sync:**
  * When adding global settings, update: `supabase/user_settings.sql`, `ISettingsService`, `SettingsService` (`LoadAsync`, `BuildSyncedSettings`, `ApplySyncedSettings`), `SettingsData`, `SyncedSettingsData`, and `SupabaseSettingsStore`.
  * Always update the test mock `StubSettingsService` in `tests/Kairos.ValidationTest/TestDoubles.cs`.
  * Subscribe to settings changes via `ISettingsService.OnSettingsChanged` and unsubscribe on disposal.
  * `SettingsData.ActivityGroupCount` is local-only; during Supabase sync, infer group count from retrieved `user_activity_groups` length.
  * Activity ordering in groups is determined by `DisplayOrder` (highest number appears last).
* **Budgets & Storage:**
  * Activity budgets are persisted in `localStorage` under `Kairos_budgets`, managed by `StatisticsService` via `IStorageService`.
  * Budgets roll recurringly by `BudgetType` (Monthly, Weekly, Daily), mapping to the Statistics `ViewMode`. Max 1 active budget per type per activity.
  * `IStorageService.GetItemAsync` returns `Task<string?>`; manually deserialize complex JSON payloads.
  * `TimeTrackingService.GetTimelineData` requires absolute `DateTimeOffset` boundaries and initializes running balances at zero.
* **CSV & Logging:**
  * Use `CsvHelper` for CSV parsing and generation.
  * Use `ILogger` for application logging (avoid `console.log`).

## JavaScript Interop
* **File Downloads:** Invoke `kairosFile.downloadText` passing a `FileDownloadRequest(FileName, Content, ContentType)`.
* **Emoji Picker:** Invoke `window.emojiPickerInterop.showPicker(targetBtnId, dotNetRef, callbackMethod)`. Always dispose the `DotNetObjectReference` on component disposal.
* **Keyboard Shortcuts:**
  * Numpad 1–8 activity triggers: `src/Kairos.Web/wwwroot/js/activitiesKeyboardInterop.js`.
  * Global navigation (PageUp/PageDown): `src/Kairos.Web/wwwroot/js/navigationInterop.js`, bound in `MainLayout.razor` using `IAsyncDisposable`.

## Hardware & Domain Features
* **Timeular Tracker:**
  * Active face (1–8) maps to ordered activities (`DisplayOrder`) in `SettingsService.ActiveActivityGroup`. Face > 8 maps to -1 (deactivates current activity).
  * Battery level is retrieved via Web Bluetooth Battery Service (`0x180F` / `0x2A19`) in `timeularInterop.js` and synced to `TimeularService.BatteryLevel`.
  * Device picker fallback on Bluetooth auto-reconnect failure is intended behavior.
* **Tutorial & Avatars:**
  * Tutorial configuration lives in `src/Kairos.Application/Services/TutorialService.cs` with localized strings in `Strings.resx`.
  * Avatar assets are stored in `src/Kairos.Web/wwwroot/img/avatars/{style}/` (named `tutorial-avatar-*.png` plus `tutorial-avatar.png` for dropdowns). When adding images, update all styles, `README.txt`, and `AvatarProfile.ImageCount`.

## Localization & Internationalization
* **Supported Locales:** English (`en`), Spanish (`es`), German (`de`), Galician (`gl`), and Vorarlbergerisch / Swiss German (`gsw`).
* **Resource Files:** Managed in `src/Kairos.Application/Resources/Strings.*.resx`. Inject `IStringLocalizer<Strings>` in Blazor components. When adding translatable strings, provide translations for all locales.
* **Resource Editing:** Avoid XML parsing libraries that reformats/corrupts `.resx` XML structure; use text manipulation or string replacement to add `<data>` nodes.
* **HTML & SVG Encoding:** Always wrap dynamic localized strings in `System.Net.WebUtility.HtmlEncode` when rendering via `MarkupString`.
* **Static Content:** `src/Kairos.Web/wwwroot/index.html` (e.g., 'Loading Kairos...', `<title>`, `<meta name="description">`) renders before Blazor initializes. Localize via inline JS reading `localStorage.Kairos_settings` or `navigator.language`.

## Testing & Playwright
* **Dev Server Setup:** Default URL is `http://localhost:5111`. When running Playwright tests locally against a background server (`dotnet run --project src/Kairos.Web`), include an adequate delay (e.g., 15–20 seconds) before executing tests to avoid connection errors, and ensure the server process is killed afterward. In CI workflows, combine startup, delay, and test execution into a single step.
* **Playwright Dependencies:** If `pwsh` is missing, run `dotnet tool install --global PowerShell`, navigate to the test output directory, and execute `pwsh playwright.ps1 install`.
* **Loading Screen Synchronization:** After navigation or `Page.ReloadAsync()`, wait for the loading screen to disappear:
  `Page.Locator("text='Loading Kairos...'").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 120000 });` (do not rely on `.bottom-nav` or `.app-container` for initial load).
* **Bypassing Auth & Modals in Tests:**
  * Wait for `.auth-card` to render before injecting mock session data into `localStorage` key `Kairos_supabase_session` (`AccessToken`, `RefreshToken`, `ExpiresAt`, `User` with lowercase `id`/`email`) and reloading. Alternatively, inject `{"OfflineMode": true}` into `Kairos_settings`.
  * Bypass tutorial and popups by injecting into `localStorage`:
    * Key `Kairos_settings`: JSON with `"TutorialCompleted": true`, `"Language": "en"`, `"CommentRequired": false`.
    * Flags: `'Kairos_TutorialCompleted': 'true'`, `'Kairos_LastSeenVersion': 'true'`, `'Kairos_ReleaseNotesSeenVersion': 'true'`.
* **DOM Selectors:** Target the Activities nav link via `a.nav-item[href='activities']` to avoid strict mode violations.
* **Test Doubles:** Centralize stubs, mocks, and fakes in `tests/Kairos.ValidationTest/TestDoubles.cs`.

## AGENTS.md Scope & Hierarchy
* The scope of an `AGENTS.md` file is the entire directory tree rooted at the folder containing it.
* Obey instructions in all applicable `AGENTS.md` files; more deeply-nested files take precedence in case of conflicts.
* Explicit user instructions supersede `AGENTS.md` rules.
* Run all relevant programmatic checks and test suites (`dotnet test Kairos.sln`) to verify changes before completing a task.
