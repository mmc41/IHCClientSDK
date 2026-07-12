# This project

> A modern, cross-platform, open-source desktop application for creating and editing IHC
> home-automation project files (`.vis`) — tested to be binary compatible with the project files of
> the vendor's legacy Windows-only authoring tool.

## Vision and Purpose

This application exist to allow  yet the only way to that owners and installers of IHC installations can keep
maintaining them for the long term — on any modern desktop OS, in English, with an open codebase.

## Key Features

| Feature | Benefit | Status |
|---------|---------|--------|
| Binary-compatible open/save of `.vis` projects | Files can move freely between This project and the vendor tool with zero risk of corruption — byte-identical round-trips are enforced by an automated oracle test suite (measured, SDK engine) | Available (SDK engine) |
| Full project editing: localities, products, function blocks, links | The complete authoring workflow — model rooms, place wired/wireless devices, add logic blocks, wire inputs to outputs — in one two-pane workspace | Planned |
| Function-block programming | Author control logic (typed variables, events, conditions, commands, enums, case structures) so installations do exactly what the household needs | Planned |
| Built-in component catalog | All stock products and function blocks are embedded in the SDK — no vendor installation is required to create or extend a project | Available (SDK engine) |
| Modern flat-line SVG icon language + English help | A themeable (light/dark), accessible UI that new users can actually read: 44 purpose-designed glyphs plus context-sensitive English help | Icons available; help planned |

## Architecture Overview

This project is a thin Avalonia MVVM desktop front-end over the repository's `ihcclient` SDK; all
file parsing, editing, validation, catalog, and controller communication live in the SDK.

```mermaid
graph LR
    User["IHC installer /\ntechnical homeowner"] -->|edits projects| App["This project\n(Avalonia desktop app)"]
    App -->|in-process API\nProjectAppService| SDK["ihcclient SDK\nIhc.Vis engine + controller services"]
    SDK -->|byte-faithful\nread/write| Vis[".vis project files"]
    SDK -.->|SOAP over HTTP/USB\noptional| Ctrl["IHC controller"]
    Legacy["Vendor's legacy tool\n(Windows)"] -.->|same files,\ninterchangeable| Vis
```

**Technology stack:** C# / .NET 10, Avalonia UI 12 (Fluent theme, compiled bindings),
CommunityToolkit.Mvvm, `ihcclient` SDK (`Ihc.Vis` project engine + controller services).
**Deployment model:** locally installed desktop application for Windows, macOS, and Linux.
**Key integrations:** `.vis` project files (compatibility contract with the vendor tool); optional
live IHC controller access for project transfer via the SDK.

## Key Differentiators

This project is the only open-source, cross-platform editor for IHC `.vis` project files that is
tested to be binary compatible with the vendor's own tool, enabling installers and homeowners to
maintain their installations without legacy Windows software.

| Differentiator | How It Compares | Evidence |
|---------------|----------------|----------|
| Binary compatibility, proven | Generic XML editors break the format; This project's engine reproduces unchanged files byte-for-byte and mimics the vendor's save metadata | Committed oracle corpus incl. vendor-authored files; byte-fidelity test suites in `tests/safe_project_tests` (measured) |
| Cross-platform | The vendor tool runs on Windows only | .NET 10 + Avalonia; the underlying SDK already builds and tests on Windows/macOS/Linux in CI (measured for SDK; inferred for the app until it joins CI) |
| No vendor install required | Legacy workflow needs the vendor product catalog on disk | Stock catalog embedded in the SDK (`BuiltInCatalog`), generated and byte-verified from vendor definitions (measured) |
| Open source (Apache-2.0) | Vendor tool is closed and unsupported for extension | Public repository, permissive license |
| Modern, accessible UX | Legacy UI is Danish-only with 1990s-era raster icons and CHM help | English UI/help, 44 themeable flat-line SVG icons designed for 16 px legibility and non-color-alone state cues |

## What This Product Is Not

- **Not a vendor product.** Unofficial and unaffiliated with LK/Schneider Electric; no endorsement or support from them.
- **Not a runtime administration or monitoring tool.** Live dashboards, user administration, and scene administration remain with the vendor's other applications and this repo's other utilities;
- **Not a general smart-home hub.** No MQTT/Home Assistant/Matter integration — the scope is IHC project authoring.
- **Not a wireless commissioning tool (initially).** Placing wireless products in a project is in scope; RF linking/signal-testing of physical devices is out of scope until a wireless API exists.
- **Not a web or mobile app.** Desktop only.
- **Does not offer offline simulation**.

## Success Metrics

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Round-trip fidelity | 100% byte-identical preserve-mode save across the committed oracle corpus, at app level | Automated byte-comparison tests (already green at SDK level — measured) |
| Vendor interop | Projects created/edited in This project load and re-save cleanly in the vendor tool | Per-release acceptance check against the vendor application |
| Cross-platform health | Build + headless UI test suite green on Windows, macOS, Linux | CI workflow (pending: app not yet wired into solution/CI) |
| Authoring coverage | Core epics (project lifecycle, localities, products, function blocks, links, programming) fully usable in the UI | Feature checklist per milestone + UI test coverage |

---

# Part 2 — PRD-lite

## Product Context

This project lives in the IHCClientSDK mono-repository as `applications/ihc_visual` and is the
primary consumer of the SDK's project-edit capability. The heavy lifting already exists and is
tested: the `Ihc.Vis` engine loads, validates, edits, creates, and saves `.vis` files with
byte-exact fidelity; `BuiltInCatalog` embeds the stock product/function-block library;
`ProjectAppService` is the application-facing facade and includes an optional bridge for
downloading/uploading projects from/to a live controller. The repository also ships `ihc_lab`, an
Avalonia GUI for exercising individual controller APIs, which established the repo's MVVM and
headless-UI-testing conventions that This project follows.

The application itself is currently an incubating skeleton: the Avalonia project, MVVM scaffolding,
the complete 44-glyph icon set with design guidelines, an icon-to-element mapping for `.vis`
content, and a headless smoke-test suite (`tests/safe_visual_tests`) exist; the editing UI does
not yet. It targets .NET 10 and is not yet part of `IHCClientSDK.sln` or CI. This document defines
the product it is being built into.

## User Classes and Characteristics

| User Class | Characteristics | Frequency of Use | Technical Proficiency |
|-----------|-----------------|-------------------|---------------------|
| Professional installer | Knows the IHC domain deeply (products, wiring, logic blocks); may be new to this app but expects the legacy workflow's concepts | Weekly on customer projects | Domain: high · Software: medium |
| Technical homeowner | Knows software well; learns the IHC domain as they go; benefits most from English UI, help, and validation feedback | Bursts (renovations, tweaks) | Domain: low-medium · Software: high |
| Contributor / developer | Extends the app or SDK; needs strict layering (UI logic testable headlessly, engine untouched by UI concerns) | Ongoing | High |

## Operating Environment

- **Runtime**: .NET 10 desktop; Avalonia UI 12 with Fluent theme and Inter fonts.
- **Client platforms**: Windows 10/11, modern macOS, mainstream Linux desktop distributions.
- **Storage**: local file system only — `.vis` project files plus an automatic backup file; no database.
- **Network**: none required for authoring; optional HTTP(S)/USB access to an IHC v3.0 controller for project transfer (via the SDK).
- **Display**: standard desktop resolutions; light and dark themes.

## Constraints and Dependencies

### Design and Implementation Constraints

- **Binary compatibility is a hard contract.** Every file the app writes must be accepted by the vendor toolchain. All persistence goes through the SDK's serializer; the UI must never hand-roll XML. Preserve-mode saves of unchanged content must stay byte-identical; default saves re-stamp metadata exactly the way the vendor tool does.
- **All project mutations go through `ProjectAppService` / `ProjectEditor` sessions** on the SDK's immutable model; the UI holds element ids, not object references.
- **MVVM with compiled bindings; view-models contain no Avalonia types** where feasible, so logic tests can run without a UI session.
- **English is the product language** for UI text and help. Project file content (user's own names/notes, vendor catalog text) is data and is preserved verbatim, whatever language it is in.
- **Icon rules**: one flat-line SVG family (24-unit grid, `currentColor`, stroke-width 2, legible at 16 px); state is conveyed by color *plus* a glyph/decoration, never color alone. See the icon design guideline (linked in Part 4).
- **Repo-wide rules apply**: `TreatWarningsAsErrors`, no logging dependency inside the SDK (tracing only), test suites must be incapable of harming a live controller.

### Assumptions

- Users have `.vis` files from existing installations and/or an IHC v3.0 controller.
- One project open per window (single-project model, matching user expectations from the legacy workflow).
- The embedded catalog is equivalent to the vendor's stock catalog; genuinely custom components can be imported from `.def`/`.ifb` files.

### Dependencies

- `ihcclient` project reference (engine, catalog, validation, controller services) — same repository, so no version skew.
- Avalonia 12 packages, CommunityToolkit.Mvvm.
- Offline simulation (M5) requires a new program-execution engine that does not exist in the SDK yet — the largest open technical dependency.
- Wireless RF commissioning depends on a wireless API that does not exist yet (explicitly out of scope until it does).

## System Features

Features are grouped into delivery milestones M1-M5. "Engine: available" means the SDK capability
exists and is tested today; the work is UI.

### F1 — Project lifecycle (M1)

**Description**: Create, open, save, and recover `.vis` projects safely.

**Functional Requirements**:

- FR-1.1: Create a new project from the built-in template (standard starting localities and built-in enumeration types) with no vendor installation present. *(Engine: available)*
- FR-1.2: Open an existing `.vis` file; exactly one project is open at a time; switching or closing prompts to save unsaved changes.
- FR-1.3: Save and Save-As. Saving an unchanged loaded project in preserve mode is byte-identical; a normal save re-stamps metadata exactly like the vendor tool. Writes are atomic — a failed save never corrupts the target file. *(Engine: available)*
- FR-1.4: A recent-projects list (at least the four most recent) is available for one-click reopening.
- FR-1.5: Automatic crash-recovery backup: written periodically and after bursts of changes, offered for recovery on restart after abnormal termination, and discarded on a clean close.

### F2 — Two-pane authoring workspace (M1)

**Description**: The main window presents the installation (physical) view and the functions
(logic) view side by side over the same locality structure.

**Functional Requirements**:

- FR-2.1: Two tree panes — **Installation** (localities → products → pins) and **Functions** (localities → function blocks → pins) — with a draggable splitter; selection-relevant changes reflect in both panes immediately.
- FR-2.2: Every node renders a type icon from the flat-line set (per the icon-mapping doc) plus decorations for state (e.g. unconfigured/unlinked warning, locked block badge); variables show inline `name = value`.
- FR-2.3: Every command is reachable three equivalent ways: menu bar, context menu on the target node, and (where assigned) a keyboard shortcut; a documented keymap covers navigation, editing, properties, link-jumping, and pane switching.
- FR-2.4: A status bar confirms the result of the last action in a short sentence.
- FR-2.5: Light and dark themes; icon ink and state colors follow the theme tokens.

### F3 — Locality management (M2)

**Description**: Model the rooms/places of the installation.

**Functional Requirements**:

- FR-3.1: Add, rename (name + note properties), and delete localities; changes appear in both panes and are confirmed in the status bar.
- FR-3.2: Deleting a locality that still contains products or blocks requires explicit confirmation and cascades cleanly: contained elements and the links/logic references that point at them are removed consistently. *(Engine: available — link cascade)*

### F4 — Product management (M2)

**Description**: Place wired and wireless products from the catalog into localities, document
them, and address wired terminals.

**Functional Requirements**:

- FR-4.1: Insert any catalog product into a selected locality from categorized menus; the product appears with its pins/sub-resources and their default values. *(Engine: available — insert transform)*
- FR-4.2: Edit product documentation properties (name, placement, note, cable data, identification code, light group where applicable) in a properties dialog opened automatically on insert and on demand thereafter.
- FR-4.3: Configure wired input/output terminal addressing (data line + module terminal) with in-use indication, and output initial values (normally-open/normally-closed semantics).
- FR-4.4: Wireless products can be inserted and documented; products that are not yet fully configured/commissioned carry a visible warning decoration. (RF linking itself is out of scope — see Constraints.)
- FR-4.5: Catalog/project constraints are enforced at edit time via the validator (e.g. at most one modem product per project).

### F5 — Function blocks and library (M2)

**Description**: Insert ready-made logic blocks from the built-in catalog or start from an empty
block; manage a personal library.

**Functional Requirements**:

- FR-5.1: Insert stock function blocks from the categorized built-in library, or an empty block, into a locality in the Functions pane. *(Engine: available)*
- FR-5.2: Stock (locked) blocks show a distinct badge and are read-only internally until explicitly unlocked, after which they behave like user blocks.
- FR-5.3: Save own blocks for reuse and maintain a favourites collection; import external component definitions (`.def`/`.ifb`) into the session catalog. *(Engine: available — catalog reader/composition)*

### F6 — Product ↔ function-block linking (M2)

**Description**: Wire physical products to logic by direct manipulation across the two panes.

**Functional Requirements**:

- FR-6.1: Create links by dragging one pin onto another (product input → block input; block output → product output); invalid targets are rejected with feedback.
- FR-6.2: Links display reciprocally: each end shows a link child naming the full path of the opposite end.
- FR-6.3: Dropping onto a scene-capable output opens a dialog for the scene value (light level + ramp time for dimmers; on/off for relays) before the link is created.
- FR-6.4: A single action jumps from a link row to its opposite end in the other pane.

### F7 — Function-block programming (M3)

**Description**: Author the control logic inside a block.

**Functional Requirements**:

- FR-7.1: A per-block programming mode shows the block's variable sections (inputs, outputs, settings, internal variables) beside its program tree; entering/leaving it is a single action.
- FR-7.2: Add typed variables across the full resource palette (on/off, counters, integers, decimals, timers, time/date/weekday, temperature, light, humidity, energy, enumerations), with section placement rules enforced and per-variable name/note/initial value/persist-on-power-loss properties.
- FR-7.3: Build programs by dragging variables onto event/condition/command groups and picking the applicable operation: events are OR-combined; condition groups support AND/OR/NOT and nesting; commands execute in order, with separate true/false branches for conditional sub-programs.
- FR-7.4: Define project-global enumeration types with ordered named values; use case structures keyed on eligible variable types, with an else branch.
- FR-7.5: Support arithmetic command lines (one operation per line, decimal/integer conversion rules) and power-up events for restoring state after outages.

### F8 — Validation, undo, and integrity (M1 onward)

**Description**: Keep the project consistent and every edit reversible.

**Functional Requirements**:

- FR-8.1: Validate on demand and before save/transfer; findings are listed with severity and one-click navigation to the offending element. *(Engine: available — `ProjectAppService.Validate`)*
- FR-8.2: Unlimited undo/redo across all edit operations within a session.
- FR-8.3: Ids of existing elements are never renumbered or reused; deletions leave holes, matching vendor semantics. *(Engine: available — allocator invariants)*

### F9 — Controller transfer (M4)

**Description**: Move projects between the PC and a live controller.

**Functional Requirements**:

- FR-9.1: Send the open project to a connected controller with explicit confirmation before overwriting the controller's existing project, and progress/success feedback. *(Engine: available — upload bridge incl. validate-on-upload)*
- FR-9.2: Retrieve the project stored in a controller into the editor; disabled when the controller holds none. *(Engine: available — download bridge)*

### F10 — Offline simulation (M5)

**Description**: Validate behaviour on the PC before deployment. **This milestone requires
building a program-execution engine that does not exist in the SDK today**; the requirements below
are epic-level and will be refined in a separate design document when M5 is scheduled.

**Functional Requirements**:

- FR-10.1: Start/stop an offline simulation of the open project; while simulating, editing is disabled and input/output state is shown by color (distinct on/off colors) plus glyph cues.
- FR-10.2: Drive inputs and block outputs interactively — momentary hold and toggle — and simulate a power-loss/power-up cycle.
- FR-10.3: Set breakpoints on program lines and step execution line by line.
- FR-10.4: Set the simulated clock and date to exercise time- and calendar-driven logic.
- FR-10.5: Capture a filterable activity log (events, conditions, commands, value changes) exportable to a file.

### F11 — Help and project documentation (M1 help shell; M5 reports)

**Description**: English help and installation documentation output.

**Functional Requirements**:

- FR-11.1: Context-sensitive English help: one action (e.g. `F1`) opens the topic for the selected element/view; all-new content, not a translation of vendor material.
- FR-11.2: Edit project-level information (project, customer, installer identity) stored in the file.
- FR-11.3 (M5): Generate installation (technical) and end-user (function) reports from entered documentation, printer-friendly, with optional installer logo.

## External Interface Requirements

### User Interfaces

Single main window with menu bar, toolbar, two tree panes, and status bar; modal dialogs for
properties and confirmations. Keyboard-first: complete tasks are achievable without a mouse
(three-route command activation, FR-2.3). Accessibility: icons are decorative and always
accompanied by text labels; state is never signaled by color alone; both themes maintain
readable contrast at 16 px tree-row icon size.

### Software Interfaces

| System | Interface Type | Purpose | Data Format |
|--------|---------------|---------|-------------|
| `ihcclient` SDK (`ProjectAppService`, `ProjectEditor`, `ICatalog`, validator) | In-process .NET API | All load/edit/validate/save/catalog operations | Immutable element model |
| `.vis` project files | File I/O (via SDK only) | Persistence; compatibility contract with the vendor tool | XML with inline DTD, vendor encoding conventions |
| `.def` / `.ifb` catalog files | File I/O (via SDK only) | Optional import of external/custom component definitions | Vendor catalog formats |
| IHC controller (v3.0) | SOAP over HTTP(S)/USB via SDK services | Optional project send/retrieve (M4) | SOAP/XML (hidden by SDK) |

## Quality Attributes

| Attribute | Target | Measurement | Confidence |
|-----------|--------|-------------|-----------|
| Compatibility | 100% byte-identical preserve-mode round-trip over the committed oracle corpus; authored files accepted by the vendor tool | Byte-comparison test suites; per-release vendor-tool acceptance check | measured (SDK today); app-level inherits via exclusive SDK persistence |
| Reliability | No data loss on crash (recoverable backup ≤ 10 min old); no partial/corrupt file ever written | Backup lifecycle tests; atomic-save tests (SDK) | measured (engine) / planned (app backup) |
| Performance | Open + render the largest committed oracle project (~236 KB) in < 2 s; save < 1 s, on typical developer hardware | Stopwatch assertions in UI tests once implemented | hypothesis (targets, unmeasured) |
| Usability | All authoring tasks completable via keyboard; icons legible at 16 px; light + dark themes | Headless UI tests + icon render checks + manual inspection | inferred (icon set already tuned/rendered at 16 px) |
| Portability | Same feature set and green test suite on Windows/macOS/Linux | CI matrix once the app joins the solution/CI | measured for SDK; planned for app |
| Maintainability | Zero build warnings (warnings-as-errors); view-model logic testable without UI; engine untouched by UI concerns | Build gates; suite layering (`safe_unit_tests` vs `safe_visual_tests`) | measured (gates exist) |

## Data Requirements

### Data Model Overview

The only persistent artifact is the `.vis` project file: an XML document with an inline DTD
holding the full installation (localities, products, function blocks, links, programs, project
metadata) as one element tree with stable hexadecimal ids. In memory the SDK exposes it as an
immutable tree; edits happen in editor sessions that produce new snapshots (enabling undo). A
sibling backup file exists between crashes and clean closes. The embedded catalog is read-only
compiled-in data.

### Data Integrity and Retention

- **Integrity**: atomic saves; validator gate before save/transfer; ids never reused.
- **Retention**: project files belong to the user on their file system; the app keeps no hidden copies beyond the crash backup, which is deleted on clean close.
- **Privacy**: project info may contain customer names/addresses. The app sends no file content anywhere; optional telemetry (if ever enabled by the host) must not include project data. Controller credentials are handled by SDK settings encryption, never stored in project files.

## Glossary

| Term | Definition |
|------|-----------|
| IHC controller | The physical unit running a home installation; executes the deployed project. |
| `.vis` file | The XML project file (with inline DTD) holding a controller's complete configuration. |
| Locality | A room/place node organizing products and function blocks. |
| Product | A physical device definition (switch, lamp output, sensor, …) instantiated from the catalog into a locality. |
| Function block | A reusable logic component with typed pins, variables, and programs. |
| Pin / resource | An addressable input/output/variable on a product or block; the endpoint of links. |
| Link | A connection between a product pin and a block pin (or scene target) that routes signals. |
| Scene / scenario link | A link carrying a preset (light level + ramp, or on/off) recalled by one trigger. |
| Catalog | The library of stock product and function-block definitions; embedded in the SDK. |
| Locked (stock) block | A catalog-supplied block that is read-only until explicitly unlocked. |
| Preserve save | The byte-identical save mode for unchanged content; default save re-stamps metadata like the vendor tool. |

---

# Part 3 — Test Information

## Test Automation Approach

**Strategy**: test pyramid on top of an already-tested engine — the byte-fidelity and editing
semantics are guaranteed by SDK suites, so app testing concentrates on view-model logic and UI
behaviour. New features follow the repository's TDD process; bugs get a failing reproduction test
before the fix.
**Frameworks**: NUnit, `Avalonia.Headless.NUnit` (`[AvaloniaTest]`) for UI, FakeItEasy for
mocking (only low-level `IIHCApiService` services may be mocked — application services are always
real), CsCheck for property-based tests in the unit suite.
**CI/CD**: GitHub Actions (`build-validation.yml`) builds the solution on Ubuntu/Windows/macOS and
runs the unit suite everywhere plus the lab UI suite on Windows. **Gap (planned work):**
`ihc_visual` and `safe_visual_tests` are not yet in the solution or CI; wiring them in — including
the engine-level project suite — is part of milestone M1.

| Test Level | Suite | Scope | Automation | Execution Frequency |
|-----------|-------|-------|-----------|-------------------|
| Engine (project files) | `tests/safe_project_tests` | Byte fidelity, editing, catalog, validation against oracle corpus | Automated | Locally on every change; CI inclusion planned |
| Unit | `tests/safe_unit_tests` | SDK + app-service/view-model logic, controller-free, mocked API services | Automated | Every PR, all three OSes (CI) |
| UI (headless) | `tests/safe_visual_tests` | This project windows/view-models under headless Avalonia | Automated | Locally today; CI planned (M1) |
| Controller integration | `tests/safe_integration_tests` | SDK against a real controller, state-safe operations only | Automated, on demand | Manual, before releases |
| Vendor interop acceptance | manual procedure | Open/re-save app-authored projects in the vendor tool | Manual | Per release |

All suites are `safe_*`: they must be incapable of changing state on a live controller; only the
integration suite may talk to one at all.

## Test Oracles

| Oracle Type | Application | Example |
|------------|-------------|---------|
| Committed reference files (byte comparison) | Round-trip and authoring fidelity | Loading an oracle `.vis` and preserve-saving must reproduce the file byte-for-byte; scripted edit sequences must reproduce vendor-saved result files exactly |
| Vendor application as ultimate oracle | Interop acceptance | The vendor tool must open, accept, and cleanly re-save files This project wrote (oracle corpus files were authored/verified against the live vendor tool) |
| Invariant checking | Editing semantics | Id allocation is monotonic and never reuses freed ids; links are always reciprocal; validator findings for known-bad inputs |
| Known-answer tests | Templates and catalog | A new empty project equals the known template output; embedded catalog components byte-match their vendor definitions |
| Property-based properties | Serialization robustness | Encode/decode round-trip properties over generated text (CsCheck) |
| Expected UI state | Headless UI tests | Window XAML loads and binds; tree/view-model state matches expectations after simulated user actions; screenshot-on-failure diagnostics (pattern established in the lab suite) |

## Test Data

**Approach**: committed fixture corpus, no generation at test time for fidelity tests, copyright free.
**Availability**: everything needed is in the repository — no controller, no vendor install, and no
private data required to run the engine, unit, and UI suites.
**Sensitive data handling**: fixtures contain no credentials or personal data; `ihcsettings.json`
(endpoints/credentials for integration tests) is untracked, with committed templates only; UI tests
use faked services behind a reserved mock endpoint scheme.

| Data Category | Source | Refresh Frequency |
|--------------|--------|-------------------|
| Authentic `.vis` oracles (incl. large ~236 KB project and vendor-saved edit-result files) | Captured from the live vendor tool, committed under `tests/safe_project_tests/testdata/` | Frozen; extended when new gaps are found |
| Synthetic component definitions (`.def`/`.ifb`) | Hand-authored, copyright-free, committed | As features require |
| Catalog generator inputs | Vendor install (dev-time only, via `ihc_catalog_codegen`); outputs committed and fingerprint-gated | On catalog regeneration only |
| UI test fixtures | Created in-test from the SDK's new-project template + fakes | Per test run |

---

# Part 4 — Links and References

## Source Code

| Repository / Path | Purpose | Access |
|-----------|---------|--------|
| <https://github.com/mmc41/IHCClientSDK> | Mono-repo containing the app and SDK | Public GitHub |
| `applications/ihc_visual/` | This application (Avalonia UI, assets, docs) | In repo |
| `ihcclient/` (`src/vis/`, `src/app/services/`) | Project-file engine, catalog, `ProjectAppService` backend | In repo |
| `tests/safe_visual_tests/`, `tests/safe_project_tests/`, `tests/safe_unit_tests/` | App UI, engine, and unit test suites | In repo |
| `utilities/ihc_lab/` | Sibling Avalonia app; established MVVM + headless-test conventions | In repo |

## Design Documents

| Document | Location | Status |
|----------|----------|--------|
| Repository architecture overview | `ARCHITECTURE.md` | Current (2026-07-10) |
| Icon design guidelines (flat-line SVG family) | `applications/ihc_visual/docs/icons_design.md` | Current |
| Icon selection reference (`.vis` element → SVG) | `applications/ihc_visual/docs/vendor_icon_codes.md` | Current |
| Test-data corpus overview | `tests/safe_project_tests/testdata/testdataoverview.md` | Current |
| Repo README (project status, disclaimers, setup) | `README.md` | Current |
| Agent/contributor instructions | `CLAUDE.md` | Partially stale (predates some SDK changes) |
| Simulation-engine design (M5) | Not yet created | TBD |
| Keymap specification (FR-2.3) | Not yet created | TBD |

## Architecture Diagrams

| Diagram | Location | Scope |
|---------|----------|-------|
| System context (Mermaid) | This document, Architecture Overview | C4 Context level |
| Whole-repo layering (textual) | `ARCHITECTURE.md` — "Layer Boundaries" | Container level, prose |
| App-internal component diagram | Not yet created | TBD |

## Standards and Specifications

- Apache-2.0 — repository license (`LICENSE.md`).
- .NET 10 / Avalonia UI 12 — runtime and UI framework baselines for this app.
- WCAG-informed icon rules — state never signaled by color alone; decorative icons always paired with text labels (see icon design guidelines).
- Vendor `.vis` / `.def` / `.ifb` file formats — undocumented; treated as a compatibility contract enforced by the oracle test corpus rather than a written spec.
