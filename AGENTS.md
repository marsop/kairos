# AI Agent Guidelines

Welcome to the Kairos codebase! This file contains important guidelines and context for AI agents working on this project.

## General
* Always start tasks in a deep planning mode. Ask clarifying questions using `request_user_input` and `message_user` tools until absolute certainty of the user's requirements is achieved. Only create an execution plan using `set_plan` after all assumptions have been tested and doubts resolved. Once the plan is approved and executed, proceed autonomously without asking for further confirmation unless absolutely necessary.
* Always delete any temporal files and logs that you have used preparing the PR as long as they are no longer required by the application.
* Always remember to update the `CHANGELOG.md` after changes, bugfixes and user-facing features. If in doubt, ask the user whether something should be included in CHANGELOG or not. Do not automatically increase the version unless when explicitly asked to do so.
* The repository contains a .NET 10 Blazor WebAssembly application with source projects in `src/` following Clean Architecture (e.g., Kairos.Web, Kairos.App, Kairos.Core, Kairos.Application, Kairos.Infrastructure) and test projects in `tests/` (e.g., `tests/Kairos.ValidationTest/Kairos.ValidationTest.csproj` using xUnit).
* When creating execution plans, always include an explicit step to run all relevant tests (e.g., `dotnet test`), followed immediately by a pre-commit step using the exact phrasing: 'Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.'

## Build & Dependencies
* If `dotnet build` or `dotnet test` fails with `Nerdbank.GitVersioning.GitException: Shallow clone lacks the objects required to calculate version height`, resolve it by running `git fetch --unshallow` to retrieve the full commit history.
* The solution utilizes Central Package Management (CPM). All NuGet package versions must be managed centrally in a root `Directory.Packages.props` file using `<PackageVersion>` elements. Project `.csproj` files should omit the `Version` attribute from `<PackageReference>` items.
* The project treats compiler warnings as errors during the build process. Use `#pragma warning disable` (e.g., `#pragma warning disable CS0067`) to suppress warnings on intentionally unused members in test stubs to prevent build failures.
* When running automated tests with `dotnet test`, avoid passing multiple project directory paths in a single command to prevent MSB1008 errors. Execute them individually or target the solution file.

## Blazor & UI
* When modifying the UI, always ensure that the design is consistent across the application and avoid having different pages looking wildly different from one another.
* For performance in Blazor components (e.g., `History.razor`), avoid executing heavy data aggregation or chart refresh logic (like `RefreshBudgetTimelineData`) inside high-frequency UI timers (e.g., 1-second ticks). Instead, trigger these updates via state-change event handlers (e.g., `TimeService.OnStateChanged`).
* For performant continuous UI animations in Blazor (like zoom or scroll scaling), avoid C#-driven frame-by-frame loops calling `StateHasChanged()`. Delegate the animation loop to JavaScript (`requestAnimationFrame`) and apply scaling via CSS variables (e.g., `calc(var(--variable) * 1px)`) to update the layout instantly without Blazor DOM diffing.
* In Blazor components, use `NavigationManager.NavigateTo("")` rather than `NavigationManager.NavigateTo("/")` when navigating to the application root. Navigating to `"/"` can be interpreted as an external URL in PWA or hybrid environments, causing the app to erroneously open a new browser tab.
* In Blazor components, prefer using `@onchange` instead of `@oninput` for input fields that trigger asynchronous save operations or network requests (e.g., calling fire-and-forget `SaveAsync()`). This prevents rapid concurrent executions, blocked locks, and dropped API calls caused by firing on every keystroke.
* In Blazor components, the standard HTML `autofocus` attribute may fail upon reopening dynamic dialogs. To reliably focus input elements, use an `ElementReference` and programmatically call `FocusAsync()` within `OnAfterRenderAsync`, catching potential exceptions if the element is unexpectedly removed from the DOM before focus completes.
* Prefer using native HTML `<details>` and `<summary>` elements for implementing collapsible accordion UI sections to reduce visual clutter and save screen space without needing external libraries or additional JavaScript.
* Prefer using standard HTML `title` attributes for implementing simple UI hover tooltips on elements (e.g., buttons, activity cards) to reduce DOM and CSS complexity.
* Standard UI input elements (e.g., text, number, select) across the application use the `edit-input` CSS class defined in `app.css` to maintain consistent visual styling with a specific background and borders.
* Application (in-app) notifications (to toasts) are rendered by `src/Kairos.App/Components/ToastContainer.razor` and styled using `.toast` related CSS classes in `src/Kairos.App/wwwroot/css/app.css`.
* The main application navigation layout and bottom footer tabs are defined in `src/Kairos.App/Layout/MainLayout.razor`.
* The UI for global application settings (such as toggles for general or advanced settings) is centralized in `src/Kairos.App/Pages/Settings.razor`.
* The Activity editing interface (which also houses the activity deletion functionality) is located within `src/Kairos.App/Pages/Activities.razor` as a modal dialog (using `modal-overlay` and `modal-dialog` classes) managed via an `_editingActivityId` state, not an inline `isEditing` state. In `src/Kairos.App/Pages/Activities.razor`, the buttons to edit or interact with individual activities use the CSS class `.activity-action-btn`.
* Activity Group editing in `Settings.razor` is managed via a modal dialog pattern using `_isEditingGroupModalOpen` and `_editingGroupId`, mirroring the Activity edit dialog implementation.
* Budget UI elements (like progress bars and item borders) should use the application's overall theme colors defined via CSS variables (e.g., green/positive and red/negative) rather than individual activity colors.
* When implementing drag-and-drop interactions in Blazor components, apply `user-select: none;` to draggable elements via CSS to prevent text highlighting, and consider using a full-screen `.drag-overlay` div with pointer event bindings to reliably catch fast movements.
* The application implements its charts (e.g., the trend line in Statistics.razor) using native inline SVG elements rather than relying on external charting libraries. Graph lines in the application (e.g., in Statistics and History pages) should never fall below 0. Ensure the minimum Y-axis value (the 'floor') is explicitly 0.

## State & Data Management
* Global application settings changes can be observed in Blazor components by injecting `ISettingsService` and subscribing to its `OnSettingsChanged` event (e.g., `SettingsService.OnSettingsChanged += StateHasChanged`) during initialization and unsubscribing during disposal.
* When adding new properties to `ISettingsService` or `SettingsService`, you must also update the mock implementation `StubSettingsService` located in `tests/Kairos.ValidationTest/TestDoubles.cs` to prevent compile errors in the test suite.
* When adding new global user settings, update the `public.user_settings` schema in `supabase/user_settings.sql` (using `ALTER TABLE IF NOT EXISTS`), add the property to `ISettingsService`, `SettingsService` (including all mappings in `LoadAsync`, `BuildSyncedSettings`, and `ApplySyncedSettings`), `SettingsData`, and `SyncedSettingsData`, and explicitly update the REST `select` query parameters and data mapping in `SupabaseSettingsStore`.
* Supabase C# integration relies on `HttpClient` for REST API calls. When adding or modifying persisted fields, you must update the schema in the `supabase/` directory, map the field in the C# DTOs using `[JsonPropertyName]`, and explicitly append the new column name to the `select` query string strictly separated by commas without spaces in the REST API calls, as spaces will break the PostgREST parser.
* When removing columns from Supabase schemas, execute the change by appending an `ALTER TABLE public.[table_name] DROP COLUMN IF EXISTS [column_name];` statement to the corresponding `.sql` files in the `supabase/` directory.
* ActivityBudgets are persisted in local storage under the key 'Kairos_budgets' and are managed via `StatisticsService` using `IStorageService`. ActivityBudgets are designed as rolling/recurring budgets based on their `BudgetType` (Monthly, Weekly, Daily) rather than absolute start and end dates. An activity can have at most one active budget per type, which maps directly to the UI's `ViewMode` (Day, Week, Month) in the Statistics component.
* The `IStorageService` interface's `GetItemAsync` method returns a `Task<string?>` and is not generic. It must be called as `GetItemAsync(key)`, followed by manual deserialization (e.g., using `JsonSerializer.Deserialize<T>`) when retrieving complex objects.
* The `TimeTrackingService.GetTimelineData` method calculates timeline graphs for the Statistics page and expects absolute `DateTimeOffset` start and end boundaries to properly scope data points and start the running balance at zero.
* The application uses `CsvHelper` library for CSV reading and writing logic. Avoid manual string construction or other CSV libraries.
* Use ILogger when possible for logging; never call console.log directly if ILogger is available in the current context.

## JS Interop
* The application's emoji picker relies on JS interop via `window.emojiPickerInterop.showPicker`, requiring a target button ID, a `DotNetObjectReference`, and a callback method name. Always dispose of the `DotNetObjectReference` within the component's `Dispose()` method to prevent memory leaks.
* Numpad keyboard shortcuts for triggering activities (1-9) are handled via JS interop in `src/Kairos.Web/wwwroot/js/activitiesKeyboardInterop.js`.
* Global keyboard shortcuts (e.g., PageDown/PageUp for navigation) are managed via JS interop in `src/Kairos.Web/wwwroot/js/navigationInterop.js` and bound to the Blazor lifecycle in `src/Kairos.App/Layout/MainLayout.razor` using `IAsyncDisposable` to ensure proper cleanup and listener removal.

## Timeular Tracker
* The physical Timeular tracker maps its active face (1-based index) to the ordered list (by `DisplayOrder`) of the user's activities in the currently active UI activity group (`SettingsService.ActiveActivityGroup`). Faces > 8 are explicitly mapped to -1 to ensure no activity is matched and the current activity is deactivated.
* The application retrieves battery levels from connected Timeular trackers using the standard Web Bluetooth Battery Service (`battery_service` / `0x180F`) and Characteristic (`battery_level` / `0x2A19`) via `timeularInterop.js`, syncing it to `TimeularService.BatteryLevel`.
* When Timeular Bluetooth auto-reconnect fails, falling back to the device picker is the correct and intended behavior. Do not remove or alter this fallback.

## Localization & Internationalization
* The application uses `.resx` files for localization (e.g., `Strings.resx`, `Strings.es.resx`, `Strings.de.resx`, `Strings.gl.resx`, `Strings.gsw.resx`) located in `src/Kairos.Application/Resources/`. In Blazor `.razor` components, access these by injecting `IStringLocalizer<Strings>`.
* When you add new translateable strings to the application, you need to translate the values to the proper language in the Strings files. For example, `Strings.de.resx` should contain words in german and `Strings.gl.resx` should contain words in galician.
* When programmatically editing `.resx` files to add translations, avoid using standard XML parsing libraries (e.g., Python's `xml.etree.ElementTree`) as they can corrupt the strict XML formatting required by the framework. Use text-based manipulation like string replacement or bash tools (e.g., `sed`) to safely append new `<data>` nodes without altering existing formatting.

## Playwright & Testing
* The default local application URL for running `src/Kairos.Web` is `http://localhost:5111`, as defined in `launchSettings.json`. Use this URL when writing automated Playwright tests.
* To bypass the application tutorial during automated frontend tests (e.g., with Playwright), inject a settings JSON object into `localStorage` under the key `Kairos_settings` with `"TutorialCompleted": true` and a valid language property (e.g., `"Language": "en"`).
* To prevent the 'Add Comment' modal from intercepting subsequent UI clicks during automated Playwright tests when starting an activity, set `"CommentRequired": false` in the injected `Kairos_settings` localStorage object.
* When using Playwright for automated frontend verification, ensure the Blazor WebAssembly application has fully initialized by explicitly waiting for specific page elements to load (e.g., `page.wait_for_selector(".bottom-nav")` for the main layout or `.app-container`) and utilizing adequate timeouts (e.g., up to `60000ms` to account for slow WebAssembly dev load times) before interacting with the page, especially when bypassing auth/tutorial.
* When using Playwright for automated tests, target the navigation link to the Activities page using `a.nav-item[href='activities']` to avoid strict mode violations caused by multiple activity links in the DOM.

## Tutorial & Avatars
* The application tutorial sequence is defined in `src/Kairos.Application/Services/TutorialService.cs` by populating a `_steps` list with `TutorialStep` instances, which map a localized string, a target route, and an `ImageUrl`. The avatar images for these steps are assigned dynamically by cycling through available images based on the step index and the current avatar's `ImageCount`.
* Avatar image assets for different styles (e.g., 'kawaii', 'zarzaparrilla') are stored in `src/Kairos.Web/wwwroot/img/avatars/` and named sequentially (e.g., `tutorial-avatar-1.png`). Each style directory must also contain a `tutorial-avatar.png` file (often a resized version of the first pose) to serve as the dropdown icon. When adding new avatar images, add the file to all style directories, update the `README.txt` files, and increment the `ImageCount` property of the respective `AvatarProfile` to dynamically use them in the tutorial.
