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

## Agent Rules & Workflows
* When generating dates for documentation (like `CHANGELOG.md`), use the actual system output (e.g., via `date +%Y-%m-%d`), even if it outputs a seemingly hallucinated future date (e.g., 2026) due to the specific sandbox configuration. It is technically correct for the environment context.
* The application uses `Nerdbank.GitVersioning` with `version.json` at the repository root. When cutting a release, the new `CHANGELOG.md` version tag is derived from the current `version.json` value (e.g., '1.8' becomes '1.8.0'). Replace `## [Unreleased]` with this new version and date, add a new empty `## [Unreleased]` section at the top, and then increment `version.json` (e.g., to '1.9') for the next cycle.
* When planning string replacements across multiple localized files (e.g., `.resx`), you must read the exact current text from *each* target file individually (e.g., using separate `grep` commands per file) to avoid output truncation and adhere to the Groundedness Rule. The plan must then include the exact original and replacement strings for each file to satisfy the Specificity Rule.
* When removing columns from Supabase schemas, execute the change by appending an `ALTER TABLE public.[table_name] DROP COLUMN IF EXISTS [column_name];` statement to the corresponding `.sql` files in the `supabase/` directory.
* When updating CHANGELOG.md, adhere strictly to 'Keep a Changelog' formatting conventions, ensuring there is only one of each category heading (e.g., `### Added`, `### Changed`, `### Fixed`) per version block.
* Always delete any temporal files and logs that you have used preparing the PR as long as they are no longer required by the application.
* When creating execution plans using the `set_plan` tool, strictly include only future, actionable steps. Do not include steps for exploration or recount actions that have already been completed.
* When setting up Playwright for CI workflows (e.g., GitHub Actions on newer Ubuntu runners), avoid using the `--with-deps` flag during `playwright install`. It can cause `apt-get` failures if specific older package versions (like `libicu74` or `libvpx9`) are unavailable in the runner's package repository.
* When starting a background Blazor server (e.g., `dotnet run &`) for frontend verification, ensure you clean up the process after testing (e.g., using `kill $(lsof -t -i :5111)`) to prevent 'Address already in use' errors on port 5111 during subsequent runs.
* Always remember to update the CHANGELOG.md after changes, bugfixes and user-facing features. If in doubt, ask the user whether something should be included in CHANGELOG or not. Do not automatically increase the version unless when explicitly asked to do so.
* The application uses `CsvHelper` library for CSV reading and writing logic. Avoid manual string construction or other CSV libraries.
* When using the `run_in_bash_session` tool, every invocation starts from the repository root directory. Directory changes (e.g., `cd`) do not persist across separate tool calls.
* When modifying files in execution plans, do not rely on non-existent pseudo-tools like `replace_with_git_merge_diff`. Instead, adhere to the Specificity Rule by explicitly defining the text replacements using `run_in_bash_session` to execute concrete `sed` commands or temporary bash/Python scripts (e.g., via `cat << 'EOF' > temp_update_script.py`).
* When running automated tests with `dotnet test`, avoid passing multiple project directory paths in a single command to prevent MSB1008 errors. Execute them individually or target the solution file.
* In GitHub Actions workflows, background processes (e.g., `dotnet run &`) are killed at the end of a step. To execute Playwright tests against the dev server in CI workflows, combine the server startup, an initialization delay (e.g., `sleep 20`), and the `dotnet test` command into a single step.
* When creating execution plans, adhere to the 'Completeness Rule' by explicitly including a step to run all relevant tests (e.g., `dotnet test Kairos.sln`) to verify no regressions were introduced before the final pre-commit and submission phases.
* When creating execution plans, adhere to the 'Verification Rule' by including a corresponding verification step (e.g., using `run_in_bash_session` to run `git diff` or `read_file`) after modifying files to confirm the changes were applied correctly.
* When instructed to prepare a release, only trigger a new version and PR if there are items in the `## [Unreleased]` section of the CHANGELOG. Otherwise, finalize the task without creating a PR.
* If `Nerdbank.GitVersioning` fails during a build or test run with a 'Shallow clone lacks the objects required' error, resolve it by running `git fetch --unshallow` in the repository.
* If `git log` unexpectedly shows limited history (e.g., only a single commit) when inspecting recent changes, the repository is likely a shallow clone. Run `git fetch --unshallow` to retrieve the full commit history.
* When programmatically modifying localized XML resource files (`.resx`), use a robust XML parser via a temporary script (e.g., Python's `xml.etree.ElementTree`) to perform exact node text replacements, rather than relying on fragile `sed` or `grep` combinations.
* The project treats compiler warnings as errors. Use `#pragma warning disable` to suppress warnings on intentionally unused members in test stubs to prevent build failures.
* When creating temporary scripts or files during execution, use clearly unique filenames (e.g., `temp_update_script.sh`) to avoid accidentally overwriting existing repository files. Ensure they are cleaned up after use.
* When formatting execution plans, the final pre-commit step must not be formatted as a bullet point or have a markdown title, and it must be placed immediately before the final submission step. It must be phrased exactly as: 'Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.'
* When creating execution plans, follow the 'Specificity Rule' by providing exact commands for testing and environment setup (e.g., `dotnet tool install --global PowerShell` and `pwsh playwright.ps1 install`, starting the Blazor server, and running `dotnet test Kairos.sln`) rather than generic instructions like 'Install dependencies' or 'Run Tests'.
* When creating execution plans that involve file modifications, the 'Specificity Rule' requires providing the full, exact directory paths for all target files (e.g., `src/Kairos.Application/Resources/Strings.resx`) rather than just listing ambiguous file names.
* When generating execution plans, strictly adhere to the 'Groundedness Rule' by ensuring all referenced methods, variables, line numbers, and logic details are explicitly confirmed in the current session's trace (e.g., via `read_file` or `grep -n`). Do not assume or hallucinate implementation details or specific line numbers based on prior knowledge.
* When running tests from the repository root, use `dotnet test Kairos.sln` instead of `dotnet test` alone to prevent MSB1003 errors (missing project/solution file).
