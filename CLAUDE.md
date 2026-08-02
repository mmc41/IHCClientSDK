# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building
```bash
# Build entire solution (run from repository root)
dotnet build IHCClientSDK.sln

# Build specific project
dotnet build ihcclient/ihcclient.csproj
dotnet build tests/safe_integration_tests/safe_integration_tests.csproj
```

### Testing
```bash
# Run SDK integration tests (safe to run against active controllers)
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# Run IHC Lab GUI tests (headless Avalonia UI tests)
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj

# Run controller-free unit tests (no Avalonia headless app, no active controller)
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj

# Run .vis project engine + ProjectAppService tests (controller-free, oracle-based)
dotnet test tests/safe_project_tests/safe_project_tests.csproj

# Run IHC OpenVisual desktop app GUI tests (headless Avalonia UI tests, no controller)
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj

# Run architecture / dependency-rule tests (ArchUnitNET; controller-free; enforces SDK layering)
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj

# Run tests with detailed output
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj --verbosity detailed

# Run specific test by name filter
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj --filter "FullyQualifiedName~TestName"
```

### Running Examples
```bash
# Run example programs (requires ihcsettings.json configuration at repo root)
dotnet run --project examples/ihcclient_example1/example1.csproj
dotnet run --project examples/ihcclient_example2/example2.csproj
```

### Running Utilities
```bash
# Run IHC project IO extractor utility
dotnet run --project utilities/ihc_project_io_extractor/ihc_projectextractor.csproj

# Run HTTP proxy recorder for debugging API calls
dotnet run --project utilities/ihc_httpproxyrecorder/ihc_httpproxyrecorder.csproj

# Run project download/upload tool
dotnet run --project utilities/ihc_project_download_upload/ihc_ProjectDownloadUpload.csproj

# Run IHC Lab GUI utility (Avalonia-based desktop application)
dotnet run --project utilities/ihc_lab/ihc_lab.csproj
```

### Running Applications
```bash
# Run IHC OpenVisual desktop application (Avalonia, .NET 10; GUI over ProjectAppService)
dotnet run --project applications/ihc_openvisual/ihc_openvisual.csproj
```

## Development Skills

Two Claude Code skills are set up for working in this repo:

- **`openobserve` skill** — check for and diagnose runtime errors and other issues in exported
  logs and traces. The SDK and the apps/utilities emit OpenTelemetry telemetry, so after running
  an app/utility — or on any reported bug, exception, silent failure, or slowness — use this skill
  instead of guessing from source code alone. Requires OpenObserve up and `ihcsettings.json`
  telemetry configured; if the skill reports it can't connect, that means "unknown", not "no errors".
- **`aui-openvisual` skill** — remote-control the running IHC OpenVisual desktop app through
  Windows UI Automation: launching it, navigating the trees, invoking menu/toolbar/context
  commands, clicking nodes, reading tooltips, capturing screenshots, and running repeatable
  command sequences with JSON results. Use it whenever you need to drive, functionally test,
  or visually inspect the live OpenVisual GUI (Windows only) — prefer it over ad-hoc
  PowerShell/pywinauto automation.

## Project Architecture

> For a whole-repo overview of layers, invariants, and boundaries, see [ARCHITECTURE.md](ARCHITECTURE.md).

### Core Structure
This is a .NET 10 mono-repository containing an unofficial SDK for IHC (Intelligent House Concept) controllers from LK/Schneider Electric. All projects target `net10.0` and the solution builds under the .NET 10 SDK.

**Main Projects:**
- `ihcclient/` - Core SDK library with high-level API wrapper around SOAP services
- `tests/safe_integration_tests/` - NUnit test suite for SDK integration tests (safe to run against active controllers)
- `tests/safe_lab_tests/` - NUnit test suite for IHC Lab GUI tests (headless Avalonia UI tests with diagnostic features)
- `tests/safe_unit_tests/` - NUnit test suite for controller-free unit tests (no Avalonia headless app; mocks IHC services with FakeItEasy)
- `tests/safe_architecture_tests/` - NUnit test suite enforcing the SDK's directional layering rules and the OpenVisual GUI's thin-shell boundary via ArchUnitNET (controller-free)
- `tests/safe_project_tests/` - NUnit test suite for the `.vis` project engine and `ProjectAppService` (controller-free; byte-fidelity round-trips against the shared `tests/testdata/` oracles, editing, catalog, validation, reporting)
- `tests/safe_visual_tests/` - NUnit test suite for the IHC OpenVisual desktop app (headless Avalonia UI tests against the real `ihc_openvisual.App`; no controller)
- `tests/testdata/` - Shared oracle fixtures (not a test project): vendor-authored and synthetic `.vis` projects (`projects/`), product `.def` files (`products/`), function-block `.ifb` files (`functionblocks/`), and report-format oracles (`reports/`) — see Test Infrastructure below for how suites consume them
- `applications/ihc_openvisual/` - Avalonia desktop application recreating IHC Visual project editing; pure GUI over `ProjectAppService` (all business logic stays in the SDK)
- `examples/ihcclient_example1/` & `examples/ihcclient_example2/` - Console application examples
- `utilities/ihc_lab/` - Avalonia-based GUI desktop application for IHC controller interaction and testing
- `utilities/ihc_admin/` - Command line utility that downloads/uploads controller administrator settings as a JSON file
- `utilities/ihc_info/` - Command line utility that prints IHC system information (versions, license, users, modules, resources)
- `utilities/ihc_project_io_extractor/` - Utility to generate C# constants from IHC project files
- `utilities/ihc_httpproxyrecorder/` - HTTP proxy for debugging/investigating IHC API calls
- `utilities/ihc_project_download_upload/` - Tool for downloading/uploading IHC project files
- `utilities/ihc_settings_encrypt/` - Tool to encrypt/decrypt passwords in `ihcsettings.json` (AES-256-GCM)
- `utilities/ihc_catalog_codegen/` - Developer-time generator that decompiles a vendor IHC Visual catalog into the SDK's built-in catalog C# sources (only needed when regenerating the catalog)

### SDK Architecture
The `ihcclient` project follows a layered architecture:

**High-Level Services** (`ihcclient/src/api/services/`):
- Service classes: `AuthenticationService`, `ResourceInteractionService`, `ConfigurationService`, `ControllerService`, `MessagecontrollogService`, `ModuleService`, `NotificationManagerService`, `TimeManagerService`, `UserManagerService`, `OpenAPIService`, `AirlinkManagementService`, `SmsModemService`, `InternalTestService`, `LedDimmerManagementService`, `ProductionTestService`
- Each service wraps a corresponding SOAP implementation (SoapImpl classes)
- Uses custom data models in `src/models/` instead of exposing SOAP artifacts
- Fully async API design with no SOAP inheritance
- `AuthenticationService`/`OpenAPIService` take an `IhcSettings` in the constructor; the other services take an `IAuthenticationService` (from which they inherit settings/endpoint). Most require authentication first (except `OpenAPIService`)
- `SmsModemService` - SMS modem control including settings, status, hardware/firmware info, and reset operations
- `InternalTestService` - LK/Schneider internal testing operations for hardware diagnostics, LED control, board version queries, time/date management, and RS485 communication. Some potentially dangerous operations (BurnIO, TestSdCard, TestIOBoard, RS485 operations, ProductionTestPassed) require `allowDangerousInternTestCalls` setting enabled in IhcSettings. Intended for manufacturing/testing scenarios.
- `LedDimmerManagementService` - LED dimmer device management: enter/exit configuration mode, detect/scan devices, assign channel IDs, read device count/light level, list devices, and start/monitor firmware upgrades.
- `ProductionTestService` - LK/Schneider production-test service. INTERNAL / potentially dangerous (manufacturing use). The controller WSDL currently defines no operations, so this is an empty placeholder wrapper; future operations should be gated on `allowDangerousInternTestCalls` like `InternalTestService`.

**Application Services** (`ihcclient/src/app/services/`, namespace: `Ihc.App`):
- Higher-level, tech-agnostic backend services intended for GUI or console applications
- Build on top of SDK services to provide specialized functionality for specific application use cases
- All application services inherit from `AppServiceBase` and support auto-authentication
- Service classes:
  - `AdminAppService` - Manages administrator-related data (users, email, SMTP, DNS, network, web access, WLAN settings). Features change tracking that detects and applies only modified settings to minimize API calls. Supports JSON serialization with optional encryption of sensitive data (marked with `[SensitiveData]` attribute). Provides `GetModel()` to retrieve settings, `Store()` to apply changes, and `SaveAsJson()`/`LoadFromJson()` for file operations.
  - `InformationAppService` - Retrieves read-only controller information (system status, versions, uptime, time settings, SD card info, SMS modem info). Provides `GetInformationModel()` for comprehensive system information retrieval.
  - `LabAppService` - Laboratory/testing backend where users can dynamically select and execute individual IHC service operations. Supports runtime service and operation selection for experimentation and testing scenarios.
  - `ProjectAppService` (namespace `Ihc.Vis`) - The single door for IHC project (`.vis`) IO and editing, and the backend for the `ihc_openvisual` app: `CreateNew` (File→New template), `Load`/`Save` (byte-fidelity round-trip, atomic writes, optional `.BAK`), `Validate`, catalog discovery (`GetAvailableProducts`/`GetAvailableFunctionBlocks` plus the `GetProductCatalogItems`/`GetFunctionBlockCatalogItems` tree views, all from the SDK-embedded `BuiltInCatalog` — no IHC Visual install required), runtime single-file catalog import (`ImportCatalogFile` — one `.def`/`.ifb` per call; import a folder by enumerating it caller-side and calling this per file), function-block export and library save (`ExportFunctionBlock`/`SaveFunctionBlockToLibrary`), the unified categorized verification (`ValidateCategorized` — the structural checklist plus the advisory Documentation-category findings that feed the report appendix; `Validate` stays structural-only), documentation reporting (`GenerateReport(project, ReportKind, ReportMode, mimetype, Stream|path, IReportIconProvider?)` — generates the FINISHED report bytes, content AND formatting, for the three report kinds × Standard/Full × `text/html`/`text/plain`; the internal pipeline in `ihcclient/src/vis/reporting/` builds a mode-tagged shape document and one generic writer per format renders it; the only caller customization is the icon mapping, defaulting to unicode stand-ins), and the controller bridge (`DownloadFrom`/`UploadTo`; construct via `CreateWithControllerBridge` to enable it). **Editing** a loaded/created project goes through the `Commands` gateway — a stateless `ProjectCommands` planner that mints undoable **command objects** from the `Ihc.Vis.Session` layer — and **execution has two doors**: an interactive frontend calls `OpenDocument(project, HistoryPolicy?)` and drives every edit through the returned **`IProjectDocument`** port (one lock-serialized session per open file owning labelled undo/redo, dirty/version, change events and the per-commit index), while one-shot callers (console tools, tests) use the stateless `Apply`/`CanApply`/`Preview`, which run one command on a throwaway session. The `project.Edit()` extension (`Ihc.Vis.Editing.ProjectEditor`) remains the low-level mutation entry point inside the SDK; GUIs never call it directly.
- Application services can create their own SDK service instances or accept existing instances via constructor injection
- Designed to be framework-agnostic, suitable for WPF, Avalonia, console apps, or web backends

**Generated SOAP Layer** (`ihcclient/generatedsrc/`):
- Auto-generated from WSDL files using dotnet-svcutil (authentication.cs, configuration.cs, controller.cs, resourceinteraction.cs, openapi.cs, airlinkmanagment.cs, etc.)
- Low-level SOAP implementations in `Ihc.Soap.*.*` namespaces
- Should not be used directly - access through high-level services
- Regeneration requires macOS with `download_wsdl.sh` and `generate.sh` scripts located in `ihcclient/`

**Supporting Infrastructure** (`ihcclient/src/api/util/` and `ihcclient/src/util/`):
- HTTP client utilities and cookie handling for maintaining IHC controller sessions (`src/api/util/`)
- Serialization helpers, encoding/copy utilities (`src/util/`); settings live in `src/config/`

**Extensions** (`ihcclient/src/extensions/`):
- Extension methods for various types

### Key Design Patterns
- Services are constructed from an `IhcSettings` (`AuthenticationService`/`OpenAPIService`) or an `IAuthenticationService` (all others) — not a logger + endpoint
- The SDK core emits OpenTelemetry traces via `ActivitySource` and has no logging dependency; only setup/utility/app code uses `ILogger`
- Authentication required before using most services (except OpenAPIService)
- Async/await throughout with async enumerables for long polling operations (see `ResourceInteractionService.GetResourceValueChanges`)
- Custom serialization layer to handle IHC-specific data formats
- Cookie-based session management for maintaining controller connections
- Each service uses internal SoapImpl wrapper around generated SOAP code
- Prioritise the following patterns in specified order of priority: D.R.Y, KISS, YAGNI, Single return statements, SOLID.
- Use nameof() instead of hardcoded parameter names

### OpenVisual Desktop Application (`applications/ihc_openvisual/`)

`ihc_openvisual` (product name **IHC OpenVisual**) is a cross-platform Avalonia desktop app that recreates the vendor's Windows-only IHC Visual authoring tool for `.vis` project files. It is a **thin MVVM GUI over `ProjectAppService`**: all `.vis` parsing, editing, validation, catalog, reporting, and controller logic stays in the SDK (`Ihc.Vis`). The UI never hand-rolls XML and holds element ids (not object references). Every mutation is a **command object** from the `Ihc.Vis.Session` layer (`ProjectCommand`, `ProjectChangeSet`, `ProjectIndex`, and the per-family command records under `ihcclient/src/vis/session/`), minted by the **`ProjectAppService.Commands` gateway** and executed through the **`IProjectDocument` port** obtained from `ProjectAppService.OpenDocument` — one lock-serialized document per open file owning the undo/redo history (`HistoryPolicy.Unlimited` by default), dirty/version, and the change sets (including undo/redo deltas) that drive keyed in-place tree reconciliation; the stateless `Apply`/`CanApply`/`Preview` facade remains the one-shot door for non-interactive callers. The GUI never constructs a command, reaches `project.Edit()` / `ProjectEditor`, or calls the stateless facade anywhere in the GUI assembly (all arch-enforced). Command **availability** has one home too: the declarative `CommandRegistry` (`ViewModels/CommandRegistry.cs`) holds one `CommandSpec` row per fixed user-facing command; parameterized item commands such as `SetThemeCommand` are narrow, checked exceptions, materializes each `IRelayCommand` from the row's single gate over the immutable `ShellContext` snapshot, and computes the per-surface `Availability` the XAML binds (context flyout omits, bar/toolbar grey with a reason; measured per-surface divergences are `SurfacePolicy` data; a two-way registry↔XAML consistency test plus the US-068/US-044 data-driven spec harness guard it). The app-side façade is **`ProjectWorkflow`** (`applications/ihc_openvisual/Services/`), holding the document plus file lifecycle/backup and delegating to extracted collaborators (`ProjectReportWorkflow` for reports, `CatalogImportWorkflow` for catalog import); the former name `ProjectSession` is retired.

- **Stack**: .NET 10, Avalonia 12 (Fluent theme, Inter fonts, compiled bindings), CommunityToolkit.Mvvm; in-process `ihcclient` project reference (no version skew).
- **Status**: incubating — the authoring shell and broad headless GUI regression suite are implemented against the epics/stories in `docs/`. The app is in `IHCClientSDK.sln`, and `safe_visual_tests` runs in CI (Windows).
- **Layering/test rule**: view-models avoid Avalonia types so logic is testable headlessly — view-model/logic tests go in `safe_unit_tests`, headless-UI tests in `safe_visual_tests`, engine byte-fidelity **and the `Ihc.Vis.Session` command/changeset/index/session tests** in `safe_project_tests`. Shares the `ihc_lab` headless-test and telemetry-bootstrap conventions (the bootstrap is currently duplicated rather than shared).
- **Language/BCL baseline (fablerefac §3.0)**: new and moved code targets C# 14 / .NET 10 idioms — the extension-member read surface (`element.Kind`, fine `Is…` predicates, `project.View(element)`), `Frozen*` collections for immutable indexes/change-sets, `readonly record struct` for keys/verdicts/policies, and partial-property (`[ObservableProperty] partial`) MVVM. Apply to **new/moved code only** — never churn existing syntax for its own sake.
- **MVVM differs from `ihc_lab` — follow OpenVisual's, not the Lab's**: OpenVisual uses CommunityToolkit.Mvvm (`ObservableObject`/`[ObservableProperty]`/`[RelayCommand]`), thin code-behind, and an `IDialogService` abstraction so dialogs are fakeable in headless tests. `ihc_lab` predates this: hand-rolled `INotifyPropertyChanged`, an 833-line `MainWindow.axaml.cs`, and dialogs constructed inline. Do not copy Lab's MVVM into OpenVisual.
- **Reporting**: the SDK generates the FINISHED report (content and formatting) via `ProjectAppService.GenerateReport` — three kinds × Standard/Fuld × HTML/text. The GUI's whole reporting surface is the shared picker dialog (three Documentation-menu entries, each pre-selecting its report type) plus the `SvgReportIconProvider` (serves the app's `Assets/*.svg` as the report icon mapping — per-instance fragments and the inline sprite — readable without a running Avalonia platform); `ProjectReportWorkflow` only calls the facade and writes the returned bytes (temp file → browser for view/print, chosen file for save-as). The GUI composes no report HTML/text (arch-enforced). The exact output of all 24 kind×mode×format×fixture combinations is pinned byte-for-byte by the `tests/testdata/reports/` oracles.
- **Manual/functional GUI testing**: use the `aui-openvisual` skill (Windows UI Automation) to launch and drive the real running app — see Development Skills above. Headless automated tests stay in `safe_visual_tests`.

**Documentation** lives in `applications/ihc_openvisual/docs/` — read the relevant doc before implementing an app feature.

**`product.md` and `stories/*.md` specify WHAT the app must do — not HOW, and not WHEN.** They are the product **specification** (requirements, intended behaviour, acceptance criteria) and are the source of truth for *what* is correct. They are **not**:
- **HOW (implementation)** — class/method/file design, patterns, tech choices, code structure. That belongs in the code and in `ARCHITECTURE.md`, never in these docs.
- **WHEN (plans)** — milestones, roadmaps, sequencing, task backlogs, or progress tracking. Keep planning artefacts out of these docs (use `tmp/` backlogs, issues, etc.).

So when editing them, describe behaviour, not implementation or schedule. (The one allowed exception is the short per-story *Readiness* / *Implementation status* line — a status annotation that is a natural part of a user story, not a plan.)

| Doc | Purpose |
|-----|---------|
| `product.md` | The product spec (WHAT): vision, features F1–F11, quality attributes, data requirements, test information, glossary |
| `stories/*.md` | The behavioural spec (WHAT): epics **E1–E16** and their user stories (`US-NNN`) with Given-When-Then acceptance criteria — start here for any feature |
| `icons_design.md` | Flat-line SVG icon design guidelines (24-unit grid, `currentColor`, legible at 16 px; state via glyph + colour, never colour alone) |
| `icon_codes.md` | `.vis`/`.ifb` element (and vendor `_0xNN` code) → `Assets/*.svg` icon mapping, plus the 1–3 char Unicode stand-ins for text-only renderings (§7) |

## Configuration Requirements

### ihcsettings.json
All tests, examples, and utilities require an `ihcsettings.json` file in the repository root (not tracked in git). Use `ihcsettings_template.json` or `ihcsettings_example.json` as reference.

Required for development:
- IHC controller endpoint and credentials
- Test resource IDs for boolean inputs/outputs
- Logging configuration
- Security settings (see below)

Note: The SDK library itself does NOT require this file - only the test/example/utility projects need it for configuration.

**SDK API Usage:**
Services are constructed from an `IhcSettings`. `AuthenticationService`/`OpenAPIService` take the settings directly; other services take an already-constructed `IAuthenticationService`:

```csharp
IhcSettings settings = IhcSettings.GetFromConfiguration(config);
var authService = new AuthenticationService(settings);
var resourceService = new ResourceInteractionService(authService);
```

`logSensitiveData` is a field on `IhcSettings` (`"logSensitiveData"` in `ihcsettings.json`), not a constructor parameter. Keep it `false` unless debugging — when `true`, credentials are visible in traces.

### IHC Controller Setup
Before running any code that connects to an IHC controller:
1. Enable network access in IHC administrator interface
2. Enable "thirdparty access" 

## Important Notes

- Target framework: `net10.0` for all projects (version 0.8.1); `applications/ihc_openvisual` and `tests/safe_visual_tests` additionally use Avalonia 12. The whole solution builds under the .NET 10 SDK; CI pins `DOTNET_VERSION: '10.x'`.
- Test framework: NUnit 4.x
- The project wraps SOAP web services since .NET Core doesn't natively support SOAP
- `AuthenticationService` and `ResourceInteractionService` are feature-complete
- Other services are partially implemented but can be extended via the underlying SoapImpl classes
- Version 3.0+ controllers have additional OpenAPIService (not recommended for use - quality uncertain)
- WSDL regeneration requires macOS with wget and dotnet-svcutil tools
- This is an **unofficial** SDK not affiliated with or supported by LK/Schneider Electric
- Project treats warnings as errors (TreatWarningsAsErrors=true)
- Recommended usage: Use Microsoft.Extensions.Hosting (Generic Host or ASP.NET Core) for proper lifecycle management and orderly shutdown
- When refactoring, do not add simple methods that does nothing but calling another method in another class

## Test Infrastructure

### Test Suites
- **safe_integration_tests** - SDK integration tests (safe to run against active controllers)
- **safe_lab_tests** - Headless Avalonia UI tests for IHC Lab application with advanced diagnostic capabilities (using fake sevices instead of active controller)
- **safe_unit_tests** - Controller-free unit tests for SDK and Lab business logic (no Avalonia headless app; mocks IHC services with FakeItEasy). UI control-construction tests belong in safe_lab_tests instead.
- **safe_architecture_tests** - Controller-free architecture tests (ArchUnitNET) enforcing directional layering at IL level. For the SDK (`IhcClientArchitectureTests`): `Ihc.Vis` must not depend on `Ihc.Soap`, the catalog definition layer must not depend on the editing layer, the SDK must not depend on Avalonia, and the `IProjectDocument` **signature** surface (returns, parameters, generic arguments, event payloads) exposes no `Ihc.Vis.Editing`/`Ihc.Vis.Io` type — a port member handing back an engine type would deliver the GUI a banned layer through the front door while every GUI-side ban stayed green. For the `ihc_openvisual` GUI (`OpenVisualArchitectureTests`): the thin-shell boundary — the GUI must not depend on `Ihc.Soap`, hand-roll `System.Xml`, reach the `Ihc.Vis.Io`/`Ihc.Vis.Editing` engine layers or the `ProjectDocumentSession` command-runner **type** directly (IO goes through `ProjectAppService`; commands are minted by the `ProjectAppService.Commands` gateway and executed through the `IProjectDocument` port from `ProjectAppService.OpenDocument` — interactive — or the stateless `Apply`/`CanApply`/`Preview` one-shot facade; the `Ihc.Vis.Session` command/outcome/change-set contract types stay allowed, so this is a single-TYPE ban, not a namespace ban), or touch a controller `IIHCApiService` (the file-only GUI reaches the controller only through `ProjectAppService`'s bridge); it must never `new` a `ProjectCommand` (commands come from the `ProjectAppService.Commands` factories — a constructor-call scan, since the GUI legitimately *depends* on the concrete command types the factories return), and the GUI assembly must not **call** the stateless `Apply`/`CanApply`/`Preview` members (a member-call scan with a seeded-violator positive control — interactive edits go through the document). Four further whole-assembly rules pin what dependency direction cannot: only `ProjectWorkflow` may call `OpenDocument` or `IProjectDocument.Open`/`MarkSaved`/`Close` (a second document over one file splits the undo history and silently loses edits — query/edit members stay open to all); no member may declare a competing enablement source (`[NotifyCanExecuteChangedFor]`, or `[RelayCommand(CanExecute = …)]`) since availability is the registry row's gate alone; `ConfigureAwait` is banned outright with `AutoBackupScheduler` the only allowlisted caller (a pool-thread continuation corrupts bound collections partly silently); and the context/registry value types (`ShellContext`, `NodeContext`, `ClipboardContext`, `Availability`, `CommandSpec`) stay immutable and hold no live object, while `CommandRegistry` reaches no live tree state. A scope self-check pins that the assembly really is spanned by the `ihc_openvisual.` namespace root all these scans are anchored to. It also enforces the MVVM/Humble-Object direction — view-models must not depend on Avalonia, on the view layer (`Views`/`Controls`/`Converters`), or on the concrete `IDialogService`/`IThemeService` adapters (only their ports); the view layer must not drive `IProjectDocument`/`ProjectWorkflow`/`ProjectAppService`/`ProjectCommands` directly — and the identity rule, now **assembly-wide** rather than view-model-scoped: no GUI type may retain a `Project`/`ProjectElement`/editing handle — it holds `ElementId` instead, with parameters, returns and locals staying legal. `ProjectTreeProjector` (one snapshot for the duration of one projection pass) is the sole allowlisted survivor; the former "Services legitimately hold a Project" exemption was retired once the document port deleted the workflow's snapshot stacks. The fixture is self-checked (typeof-anchored namespaces, a known-violation backstop, and armed-detector checks for the custom scans, so no rule can pass vacuously). Runs on all OSes in CI.
- **safe_project_tests** - Controller-free tests for the `.vis` project engine and `ProjectAppService` (byte-fidelity round-trips against the shared `tests/testdata/` oracles, editing, catalog, validation, reporting). The regression gate for any change under `ihcclient/src/vis/`.
- **safe_visual_tests** - Headless Avalonia UI tests for the `ihc_openvisual` desktop application (runs the real `ihc_openvisual.App`; no controller, no IHC API services needed for file-only flows).

### Test Data (Oracle Fixtures)

Oracle fixtures live in `tests/testdata/` and are shared by `safe_project_tests`, `safe_unit_tests` and `safe_visual_tests`:
- `projects/` — `.vis` project oracles (byte-fidelity round-trip targets, editing/mutation replay baselines)
- `products/` — product catalog `.def` oracles
- `functionblocks/` — function-block `.ifb` oracles
- `reports/` — report-format oracles for the documentation reports (`std-*`/`full-*` per report kind and mode, as `.html` pages pinning the OpenVisual SVG-icon output and `.txt` companions pinning the default unicode-stand-in output; all 24 regenerate byte-identically through `ProjectAppService.GenerateReport` in the E2E suites)

Each consuming suite imports `tests/TestData.props`, which copies them to `$(OutDir)/testdata/...`; reach them via `TestContext.CurrentContext.TestDirectory` (the `TestData` helper), never by walking up into the source checkout. A suite that needs oracles adds that one `<Import>` line — do not add per-file `<None Link=...>` copies or new path-discovery helpers. The report fixtures are LF+UTF-8; treat all oracles as byte-exact references — regenerate by script, never retype.

### safe_lab_tests Diagnostic Features

The `safe_lab_tests` project includes comprehensive diagnostic capabilities to help troubleshoot test failures:

#### Trace Logging
All tests output detailed trace-level logs visible in test results:
- **Application logs**: MainWindow, ViewModel, and app service operations (Trace level)
- **Avalonia UI logs**: Framework internal logs (Warning level by default)

Log levels configured in `tests/safe_lab_tests/Setup.cs`:
```csharp
// Application code logs (line 196)
builder.SetMinimumLevel(LogLevel.Trace);  // Change to Information or Warning to reduce verbosity

// Avalonia framework logs (line 44)
.LogToSink(loggerFactory, LogEventLevel.Warning);  // Controls UI framework log verbosity
```

#### Automatic Screenshot Capture on Failure
Tests automatically capture screenshots when they fail using the `[CaptureScreenshotOnFailure]` attribute:

**Implementation Details:**
- Uses `IWrapSetUpTearDown` NUnit interface to hook into test execution pipeline
- Works alongside `[AvaloniaTest]` attribute (both attributes required on each test method)
- Executes screenshot capture through Avalonia's headless session dispatcher
- Must be applied to **each test method individually** (NUnit framework limitation - cannot be applied at class level)

**Screenshot Location:**
- Saved to: `tests/safe_lab_tests/bin/Debug/{DotNetVersion}/TestFailureScreenshots/`
- Format: `{TestName}_{Timestamp}.png` (e.g., `MyTest_20251106_094227.png`)
- Automatically attached to test results via `TestContext.AddTestAttachment()`
- Requires Skia renderer (already configured in TestAppBuilder)

**Features:**
- Captures exact visual state at failure
- 1024x768 resolution headless rendering
- Timestamped to prevent overwrites
- Only executes on test failure (no overhead for passing tests)
- Fully automatic - no try-finally blocks or manual calls required

### Test Guidelines
- All tests must be safe from potential harmful side effects on IHC controller, including changing state on controller.
- Only safe_integration_tests may use real IHC Api services. All other tests should use mocks of IHC services using FakeItEasy framework.
- When generating tests, only generate valuable tests for functional aspects.
- Prefer blackbox testing over whitebox testing.
- Use best practice test techniques for test cases such as Equivalence Partitioning, Boundary Value Analysis, State Transition Testing.
- Unless specifically instructed otherwise, do not add tests for the following: null checks, expected exceptions, multithreading.

### Configuring Mocked Services for Tests (safe_lab_tests)

The `safe_lab_tests` project uses mocked IHC services configured in `utilities/ihc_lab/App/IhcSetup.cs` via the `IhcFakeSetup` class.

**How Mocking Works:**
- When endpoint starts with `SpecialEndpoints.MockedPrefix`, `IhcSetup` creates mocked services instead of real ones
- Each service has a `Setup*Service` method in `IhcFakeSetup` that configures mock behavior using FakeItEasy
- Tests automatically use mocked services through the normal application flow

**Adding/Modifying Mocked Operations:**

IControllerService and IAuthenticationService has setup mocks for all method. To add more operations that tests can use,
update the corresponding setup method in `IhcFakeSetup`:

```csharp
public static IAuthenticationService SetupAuthenticationService(IhcSettings settings)
{
    var service = A.Fake<IAuthenticationService>();

    // Add mock operation behavior
    A.CallTo(() => service.Login(A<string>._, A<string>._, A<Application>._))
        .ReturnsLazily((string username, string password, Application app) =>
        {
            // Return mock result
            return new IhcUser { Username = username, ... };
        });

    return service;
}
```

**Example: If tests need a specific operation:**
1. Identify which service the operation belongs to (e.g., `IAuthenticationService`)
2. Update `IhcFakeSetup.SetupAuthenticationService()` to configure the operation
3. Use `A.CallTo()` to define the mock behavior
4. Tests will automatically see the operation through `LabAppService`

**IMPORTANT: Mocking Restrictions**

Tests MUST follow these rules when using mocks:

- ✅ **ALLOWED**: Mocking IHC API services (implementing `IIHCApiService` interface)
  - Examples: `IAuthenticationService`, `IControllerService`, `IResourceInteractionService`, etc.
  - These are low-level SDK services that communicate with the IHC controller
  - Use `IhcFakeSetup` methods in `utilities/ihc_lab/App/IhcSetup.cs` to configure mocked API services

- ❌ **FORBIDDEN**: Mocking application services (implementing `IIHCAppService` interface)
  - Examples: `LabAppService`, `AdminAppService`, `InformationAppService`
  - These are high-level business logic services in `ihcclient/src/app/services/`
  - Always use real instances: `new LabAppService(null, null)` instead of `A.Fake<LabAppService>()`
  - Reason: Application services contain business logic that should be tested, not mocked

**Example of correct vs incorrect mocking:**

```csharp
// ✅ CORRECT - Mocking API service
var authService = A.Fake<IAuthenticationService>();
A.CallTo(() => authService.Authenticate()).Returns(Task.FromResult(new IhcUser { ... }));

// ✅ CORRECT - Using real application service
var labAppService = new LabAppService(null, null);

// ❌ INCORRECT - Do not mock application services
var labAppService = A.Fake<LabAppService>(); // WRONG!
```

See `tests/safe_lab_tests/README.md` for detailed test infrastructure documentation.
