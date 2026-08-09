# IHC OpenVisual

> A modern, cross-platform, open-source desktop application called IHC OpenVisual for creating and editing IHC
> home-automation project files (`.vis`) — reading and writing the `.vis` project format used by IHC
> controllers with byte-exact fidelity.

## Vision and Purpose

IHC OpenVisual exists to allow owners and installers of IHC installations to keep
maintaining them for the long term — on any modern desktop OS, in Danish, using an open codebase.

## Key Features

| Feature | Benefit |
| --------- | --------- |
| Binary-compatible open/save of `.vis` projects | Existing project files open and re-save with zero risk of corruption — byte-identical round-trips of the `.vis` format. |
| Full project editing: localities, products, function blocks, links | The complete authoring workflow — model rooms, place wired/wireless devices, add logic blocks, wire inputs to outputs — in one two-pane workspace: **what is installed on the left, what it does on the right**, linked across the middle. |
| Function-block programming | Author control logic (typed variables, events, conditions, commands, enums, case structures) so installations do exactly what the household needs. |
| Built-in component catalog | The stock products and function blocks are embedded, so nothing else needs to be installed to create or extend a project. |
| Modern flat-line SVG icon language + help | A themeable (light/dark), accessible UI that new users can actually read: purpose-designed glyphs plus context-sensitive help in the application's own language. |

## Architecture Overview

IHC OpenVisual is a cross-platform desktop front-end over a shared project engine; all `.vis` parsing,
editing, validation, catalog, and controller communication live in that engine, and the UI never
hand-rolls the file format.

**Deployment model:** a locally installed desktop application for Windows, macOS, and Linux.
**Key integrations:** `.vis` project files (the byte-exact format contract); optional
live IHC controller access for project transfer.

## Key Differentiators

IHC OpenVisual is an open-source, cross-platform editor for IHC `.vis` project files with byte-exact
format fidelity, enabling installers and homeowners to maintain their installations on any modern
desktop OS.

| Differentiator | What It Delivers |
| --------------- | ---------------- |
| Binary compatibility | Generic XML editors break the format; IHC OpenVisual reproduces unchanged `.vis` files byte-for-byte and stamps save metadata exactly as the format requires. |
| Cross-platform | Runs natively on Windows, macOS, and Linux. |
| Self-contained catalog | The stock product and function-block catalog is embedded; no separate catalog installation is required. |
| Open source (Apache-2.0) | The full source is open for inspection, extension, and long-term maintenance. |
| Modern, accessible UX | A Danish UI and help with themeable flat-line SVG icons designed for legibility, using non-color-alone state cues. |

## Differences from the Original IHC Visual

IHC OpenVisual mostly matches the original Windows authoring tool's behaviour, except for the following:

**Enhancements**

- Runs on Windows, macOS, and Linux; the original is Windows-only.
- Refuses to save text the `.vis` character repertoire cannot store — naming the offending element and
  character — where the original writes an unparsable file.
- Every drag-and-drop operation is also reachable from the menus and the keyboard, so linking, moving, and reordering never require a mouse.
- Unavailable commands explain themselves: pressing the keyboard shortcut of a greyed menu command shows the reason in the status bar.
- Enhanced support for assistive technology and automation.
- Embedded stock catalog.
- Documentation reports render as self-contained static HTML that works in any modern browser, with optional enhanced variants and no dependency on a legacy browser component.
- Menu commands that do nothing in the original are omitted rather than reproduced.
- Support multiple instances.

**Presentation**

- The user interface is in Danish, as the original is — including the menu and dialog wording, which follows the original's where the two apps offer the same command.
- A title-bar dirty marker (`•`) shows at a glance that the project has unsaved changes.
- Tree-node tooltips always include the node's IHC resource ID, without holding a modifier key.
- Modern flat-line SVG icon set, themeable, and never signalling state by colour alone.
- A light/dark theme switcher.

**Exclusions**

- No simulation mode.
- No auto backup.
- Editing rapport data tables
- Product help.

## What This Product Is Not

- **Unofficial project.** Not affiliated with or endorsed by LK/Schneider Electric.
- **Not a runtime administration or monitoring tool.** Live dashboards, user administration, and scene administration are out of scope.
- **Not a general smart-home hub.** No MQTT/Home Assistant/Matter integration — the scope is IHC project authoring.
- **Not a wireless commissioning tool (initially).** Placing wireless products in a project is in scope; RF linking/signal-testing of physical devices is out of scope until a wireless API exists.
- **Not a web or mobile app.** Desktop only.
- **Does not offer offline simulation.**

## Success Metrics

| Metric | Target |
| -------- | -------- |
| Round-trip fidelity | 100% byte-identical preserve-mode save across the reference project corpus, at app level. |
| Format conformance | Projects created or edited in IHC OpenVisual remain valid `.vis` files that load cleanly on IHC controllers. |
| Cross-platform health | Build and the headless UI test suite green on Windows, macOS, and Linux. |
| Authoring coverage | The core capabilities (project lifecycle, localities, products, function blocks, links, programming) are fully usable in the UI. |
| Specification conformance | Every behaviour matches its story's acceptance criteria, or is a deliberate, documented exception that a story records. |

---

# Part 2 — PRD-lite

## Product Context

IHC OpenVisual is the primary consumer of the shared project engine's project-edit capability. The
engine loads, validates, edits, creates, and saves `.vis` files with byte-exact fidelity; the embedded
catalog supplies the stock product/function-block library; the application-facing facade includes an
optional bridge for downloading and uploading projects from and to a live controller.

## User Classes and Characteristics

| User Class | Characteristics | Frequency of Use | Technical Proficiency |
| ----------- | ----------------- | ------------------- | --------------------- |
| Professional installer | Knows the IHC domain deeply (products, wiring, logic blocks); may be new to this app but already fluent in IHC project concepts | Weekly on customer projects | Domain: high · Software: medium |
| Technical homeowner | Knows software well; learns the IHC domain as they go; benefits most from clear UI, help, and validation feedback | Bursts (renovations, tweaks) | Domain: low-medium · Software: high |
| Contributor / developer | Extends the app or engine; needs strict layering so UI logic is testable without a running UI and the engine stays free of UI concerns | Ongoing | High |

## Operating Environment

- **Runtime**: a modern desktop application.
- **Client platforms**: Windows 10/11, modern macOS, mainstream Linux desktop distributions.
- **Storage**: local file system only — `.vis` project files; no database.
- **Network**: none required for authoring; optional HTTP(S)/USB access to an IHC v3.0 controller for project transfer.
- **Display**: standard desktop resolutions; light and dark themes.

## Constraints and Dependencies

### Design Constraints

- **Binary compatibility is a hard contract.** Every file the app writes must be a valid `.vis` file accepted by IHC controllers; the UI never hand-rolls the file format. Preserve-mode saves of unchanged content stay byte-identical; default saves re-stamp metadata exactly as the format requires.
- **The user stories are the authoritative behavioural spec.** Where observed behaviour and an IHC OpenVisual story disagree, the story is the thing to fix. Three principles guide behaviour the stories leave open:
  1. **IHC OpenVisual keeps its safety guards and error feedback.** They change nothing about *what* happens, only warn or explain — and they never guard an action that is already reversible (which is why an undoable unlock needs no warning — FR-5.2).
  2. **Simulation stays out of scope** (F10).
  3. **The app degrades gracefully** on malformed or self-contradictory input rather than crashing.
- **Where a behaviour is unspecified, the app stays permissive** rather than guessing — it refuses only what is known to be invalid.
- **Commands act on the selected element, never on which pane holds keyboard focus.** All mutations run on the engine's immutable model; the UI holds element ids, not object references.
- **UI logic is testable without a running UI** — view-model logic avoids UI-framework types where feasible.
- **Danish is the product language** for UI text and help. Project file content (the user's own names/notes, catalog text) is data and is preserved verbatim, whatever language it is in — the application never restates it in another language.
- **Icon rules**: one flat-line SVG family (24-unit grid, `currentColor`, legible at small sizes); state is conveyed by colour *plus* a glyph/decoration, never colour alone. See the icon design guideline (linked in Part 4).

### Assumptions

- Users have `.vis` files from existing installations and/or an IHC v3.0 controller.
- One project open per window (single-project model).
- The embedded catalog covers the stock product and function-block set; genuinely custom components can be imported from `.def`/`.ifb` files.

### Dependencies

- The shared project engine (engine, catalog, validation, controller services) — same repository, so no version skew.
- Offline simulation (out of scope — F10) would require a program-execution engine that does not exist yet — the largest open technical dependency were it ever taken on.
- Wireless RF commissioning depends on a wireless API that does not exist yet (explicitly out of scope until it does).

## System Features

### F1 — Project lifecycle

**Description**: Create, open, and save `.vis` projects safely.

**Functional Requirements**:

- FR-1.1: Create a new project from the built-in template (standard starting localities and built-in enumeration types) — self-contained, with nothing else to install.
- FR-1.2: Open an existing `.vis` file; exactly one project is open at a time; switching or closing prompts to save unsaved changes.
- FR-1.3: Save and Save-As. Saving an unchanged loaded project in preserve mode is byte-identical; a normal save re-stamps metadata exactly as the format requires. Writes are atomic — a failed save never corrupts the target file.
- FR-1.4: A recent-projects list (at least the four most recent) is available for one-click reopening.
- FR-1.5: A project file named at launch — the file the desktop hands the application when the installer opens a `.vis` with it — is the document opened, in place of the empty starting project. A file that cannot be opened is reported like any other failed open, leaving the application on the empty project rather than failing to start.

### F2 — Two-pane authoring workspace

**Description**: The main window presents the installation (physical) view and the functions
(logic) view side by side over the same locality structure.

> **The two panes are not two views of one menu model — each pane owns half the authoring vocabulary.**
> This is the workspace's central rule, and it decides where every insert
> command belongs:
>
> | | **LEFT pane — Installation** | **RIGHT pane — Functions** |
> | --- | --- | --- |
> | Shows | localities → **products** → pins | localities → **function blocks** → pins |
> | Owns the insert of | **products** (wired, wireless, special) | **function blocks** (library and empty) |
> | Answers | *what is physically installed, and where* | *what the installation does* |
>
> **The locality structure is shared** — every locality appears in **both** panes, in the same order, and
> a rename/add/delete shows up in both at once. What differs is what hangs beneath it, and therefore what
> each pane lets you insert: **a product is never inserted on the right, a function block never on the
> left.** Links are the one operation that deliberately spans the panes (F6), which is why the two are
> shown side by side rather than as tabs.

**Functional Requirements**:

- FR-2.1: Two tree panes — **Installation** (left: localities → products → pins) and **Functions** (right: localities → function blocks → pins) — over one shared locality structure, with a draggable splitter; a change to a locality reflects in both panes immediately.
- FR-2.1a: **Pane ownership of the insert vocabulary.** Products are inserted **only** from the Installation pane and function blocks **only** from the Functions pane; each pane offers exactly its own half **on the node's context menu**. A pane never offers a *context-menu* insert whose result it could not show.
- FR-2.1b: **The menu bar is deliberately NOT pane-gated.** It offers the whole vocabulary regardless of which pane has focus or what is selected. A context menu answers *"what can I do to this?"*, the menu bar *"what can this app do?"*.
- FR-2.2: Every node renders a type icon from the flat-line set (per the icon-mapping doc) plus decorations for state (e.g. unconfigured/unlinked warning, locked block badge); variables show inline `name = value`.
- FR-2.3: Every command is reachable three equivalent ways: menu bar, context menu on the target node, and (where assigned) a keyboard shortcut; a documented keymap covers navigation, editing, properties, link-jumping, and pane switching.
- FR-2.4: A status bar confirms the result of the last action in a short sentence, and carries a controller-connection indicator whose connected and not-connected states differ in glyph shape (never colour alone) and are also stated in words.
- FR-2.5: Light and dark themes; icon ink and state colours follow the theme tokens.
- FR-2.6: **One language for the application's own text, verbatim for everyone else's.** Every caption the application invents is written in a single language (Danish); text that comes from the project file or the component catalog is rendered exactly as stored and is never translated.

### F3 — Locality management

**Description**: Model the rooms/places of the installation.

**Functional Requirements**:

- FR-3.1: Add, rename (name + note properties), and delete localities; changes appear in both panes and are confirmed in the status bar.
- FR-3.2: Deleting a locality that still contains products or blocks requires explicit confirmation and cascades cleanly: contained elements and the links/logic references that point at them are removed consistently.

### F4 — Product management

**Description**: Place wired and wireless products from the catalog into localities, document them,
and address wired terminals.

**Functional Requirements**:

- FR-4.1: Insert any catalog product into a locality selected **in the Installation (left) pane** from categorized menus; the product appears there with its pins/sub-resources and their default values, and does **not** appear in the Functions pane (FR-2.1a).
- FR-4.2: Edit product documentation properties (name, placement, note, cable data, identification code, light group where applicable, and inclusion in the end-user report) in a properties dialog titled with the **product type**, opened on demand from the tree — inserting a product opens no dialog. The **name** field is disabled when the placed element's `locked` attribute resolves to `yes` (resolved against the project's own inline DTD, which defaults it to `no`); the **placement** field is a free-text placement descriptor with suggestions, **not** a room selector — a product's room is its position in the tree.
- FR-4.3: Configure input/output terminal addressing (data line + module terminal) with in-use indication, output initial values (normally-open/normally-closed semantics), per-terminal wire colour, and power-fail save-current-value behaviour. The address editor opens by **double-clicking a terminal row** or from a *Configure* button — two routes onto one sub-dialog. The terminal grids are enabled by the product's **shape** — whether it has inputs and/or outputs — not by its family, so a wireless product uses the same dialog and grids as a wired one.
- FR-4.4: Wireless products can be inserted and documented **through the same properties dialog and the same field set as wired products** (FR-4.2/FR-4.3); products that are not yet fully configured/commissioned carry a visible warning decoration. (RF linking itself is out of scope — see Constraints.)
- FR-4.5: Catalog/project constraints are enforced at edit time via the validator (e.g. at most one modem product per project).

### F5 — Function blocks and library

**Description**: Insert ready-made logic blocks from the built-in catalog or start from an empty
block; manage a personal library.

**Functional Requirements**:

- FR-5.1: Insert stock function blocks from the categorized built-in library, or an empty block, into a locality selected **in the Functions (right) pane** — and only there; a block does **not** appear in the Installation pane (FR-2.1a).
- FR-5.2: Stock (locked) blocks show a distinct badge and are read-only internally until explicitly unlocked, after which they behave like user blocks. **The unlock is silent and undoable** — no warning, and one *Undo* re-locks the block. (No warning is needed precisely because the unlock is undoable.)
- FR-5.3: Save own blocks for reuse and maintain a favourites collection; import external component definitions (`.def`/`.ifb`) into the session catalog. **Saving a block to the library locks the in-project copy**: the saved block is renamed, stamped with master name/author/date, marked `locked`, given the library badge, and becomes view-only until unlocked (FR-5.2), with no re-insertion.

### F6 — Product ↔ function-block linking

**Description**: Wire physical products to logic by direct manipulation across the two panes.

**Functional Requirements**:

- FR-6.1: Create links by dragging one pin onto another (product input → block input; block output → product output); invalid targets are rejected with feedback.
- FR-6.1a: **Link legality is a data-flow rule.** A link is legal iff the **source** produces a signal, the **target** consumes one, and **at least one end is a function-block pin** — two product pins never link directly, because routing product logic through a block *is* the IHC programming model. The rule is keyed on the pin's element kind and the **roles in the drag**, never on "kind matching": the *same pin pair* is accepted one drag direction and refused the other, so it must not be restated as "inputs↔inputs, outputs↔outputs". It is enforced so a `.vis` stays valid whoever drives the editor.
- FR-6.2: Links display reciprocally: each end shows a link child naming the full path of the opposite end, with **direction carried by the row's icon** and the label left bare.
- FR-6.2a: **A link's halves are written in the format's canonical orientation** — the dragged pin (the source/producer) owns the `link_from_resource` half; the pin dropped on (the target/consumer) owns the `link_to_resource` half. The element names read backwards from the roles (a producer owns the *from* half), so the check and the write must agree on which end is which.
- FR-6.3: Dropping onto a scene-capable output opens a dialog for the scene value (light level + ramp time for dimmers; on/off for relays) before the link is created.
- FR-6.4: A single action jumps from a link row to its opposite end in the other pane.

### F7 — Function-block programming

**Description**: Author the control logic inside a block.

**Functional Requirements**:

- FR-7.1: A per-block programming mode shows the block's variable sections (inputs, outputs, settings, internal variables) beside its program tree; entering/leaving it is a single action. **The configuration-mode view shows less**: a section with no members is not drawn, and **internal variables are a programming-mode section only**. **Entering programming mode on a locked (stock) block is view-only**: the program renders for reading, but every authoring command is gated on the block being unlocked and is **removed, not greyed**.
- FR-7.2: Add typed variables across the full resource palette (on/off, counters, integers, decimals, timers, time/date/weekday, temperature, light, humidity, energy, enumerations), with section placement rules enforced and per-variable name/note/initial value/persist-on-power-loss properties.
- FR-7.3: Build programs by dragging variables onto event/condition/command groups and picking the applicable operation **from a popup whose options are a function of the pin's type and the target group** — the target group decides the row family (events / conditions / commands), the pin type decides the operator list (e.g. a bool output on a Commands group offers `= ON` / `= OFF` / `Toggle`; the same pin on an Events group offers the event set). Events are OR-combined; condition groups support AND/OR/NOT and nesting; commands execute in order, with separate true/false branches for conditional sub-programs.
- FR-7.4: Define project-global enumeration types with ordered named values; use case structures keyed on eligible variable types, with an else branch.
- FR-7.5: Support arithmetic command lines (one operation per line, decimal/integer conversion rules) and power-up events for restoring state after outages.

### F8 — Validation, undo, and integrity

**Description**: Keep the project consistent and every edit reversible.

**Functional Requirements**:

- FR-8.1: Validate on demand and before save/transfer; findings are listed with severity and one-click navigation to the offending element.
- FR-8.2: Unlimited undo/redo across all edit operations within a session — no configured step cap, bounded only by process memory. **Prefer making an irreversible action undoable over guarding it with a dialog** — no project mutation currently needs the guard.
- FR-8.3: Ids of existing elements are never renumbered or reused; deletions leave holes (ids are monotonic and never recycled).
- FR-8.4: **Catalog-owned structure is not editable.** A product's pins exist because its catalog type declares them, so they cannot be deleted, reordered, or inserted into — the commands are absent, and the engine refuses them whatever route asks.

### F9 — Controller transfer

**Description**: Move projects between the PC and a live controller.

**Functional Requirements**:

- FR-9.1: Send the open project to a connected controller with explicit confirmation before overwriting the controller's existing project, and progress/success feedback.
- FR-9.2: Retrieve the project stored in a controller into the editor; disabled when the controller holds none.

### F10 — Offline simulation (out of scope)

**Description**: Validate behaviour on the PC before deployment. **Out of scope** (consistent with
*What This Product Is Not* above and `stories/08-simulation.md`): this would require a
program-execution engine that does not exist in the engine today. The requirements below are retained
as documentation only and would be refined in a separate design document if the capability is ever
taken on.

**Functional Requirements**:

- FR-10.1: Start/stop an offline simulation of the open project; while simulating, editing is disabled and input/output state is shown by color (distinct on/off colors) plus glyph cues.
- FR-10.2: Drive inputs and block outputs interactively — momentary hold and toggle — and simulate a power-loss/power-up cycle.
- FR-10.3: Set breakpoints on program lines and step execution line by line.
- FR-10.4: Set the simulated clock and date to exercise time- and calendar-driven logic.
- FR-10.5: Capture a filterable activity log (events, conditions, commands, value changes) exportable to a file.

### F11 — Help and project documentation

**Description**: Danish help and installation documentation output.

**Functional Requirements**:

- FR-11.1: Context-sensitive Danish help: one action (e.g. `F1`) opens the topic for the selected element/view; all-new, originally authored content.
- FR-11.2: Edit project-level information (project, customer, installer identity) stored in the file.
- FR-11.3: Generate the **three documentation reports** — end-user functions (Funktionsdokumentation), installation (Installationsdokumentation) and function-block logic (Functionsblok dokumentation) — each in **Standard** or **Fuld** mode and as **HTML** or **plain text**; each report has its own Documentation-menu entry opening the one shared picker pre-selected, with view-in-browser (printing is the browser's) and save-as actions. There are no report options beyond type × mode × format, and the output carries no navigation apparatus.
- FR-11.4: **Fuld** mode is Standard plus additions only: the generation timestamp + programmer line, the Projekt identity block, inline `(ID …)` element ids at definition sites, the installation-only terminal-connections table, and a final **"Fejl i dokumentation"** section fed by the project verification checks — per locality → product → terminal, covering at least: unlinked terminal, missing identification code / light group / cable type / cable number / wire colour / placement / data-line address.
- FR-11.5: Report output carries **no images apart from the app's icon language** — product identity, module addressing and wire colours are conveyed as text and tables (no product photos, module diagrams, installer logo image, or external manual pictures); the function-block report renders its logic tree with the app's icon set (inline vector glyphs in HTML, unicode stand-ins in text).

## External Interface Requirements

### User Interfaces

Single main window with menu bar, toolbar, two tree panes — **Installation on the left** (products)
and **Functions on the right** (function blocks), over one shared locality structure (F2) — and a
status bar carrying the last action's result, a controller-connection indicator and the project-locale
indicator; modal dialogs for properties and confirmations. Keyboard-first: complete tasks are
achievable without a mouse (three-route command activation, FR-2.3). Accessibility: icons are decorative
and always accompanied by text labels; state is never signaled by color alone; both themes maintain
readable contrast at tree-row icon size.

### Software Interfaces

| System | Interface Type | Purpose | Data Format |
| -------- | --------------- | --------- | ------------- |
| Shared project engine (load/edit/validate/save, catalog, validator) | In-process API | All load/edit/validate/save/catalog operations | Immutable element model |
| `.vis` project files | File I/O (via the engine only) | Persistence; the byte-exact `.vis` format contract | XML with inline DTD and the format's encoding conventions |
| `.def` / `.ifb` catalog files | File I/O (via the engine only) | Optional import of external/custom component definitions | `.def` / `.ifb` catalog formats |
| IHC controller (v3.0) | SOAP over HTTP(S)/USB | Optional project send/retrieve | SOAP/XML (hidden by the engine) |

## Quality Attributes

| Attribute | Target | Measurement |
| ----------- | -------- | ------------- |
| Compatibility | 100% byte-identical preserve-mode round-trip over the reference corpus; authored files remain valid `.vis` files accepted by IHC controllers | Byte-comparison against the corpus; controller-acceptance check |
| Reliability | Unsaved changes are never lost silently — every path that would discard them prompts first; no partial/corrupt file is ever written | Save-prompt and atomic-save checks |
| Performance | Open + render the largest reference project (~236 KB) in < 2 s; save < 1 s, on typical developer hardware | Timed assertions |
| Usability | All authoring tasks completable via keyboard; icons legible at tree-row size; light + dark themes | UI checks + icon render checks |
| Language consistency | The application's own captions are in one language (Danish); file- and catalog-derived text is rendered verbatim and never translated | UI string checks; a tree-label check that a stored caption is not restated in another language |
| Portability | Same feature set on Windows/macOS/Linux | Cross-platform build + test |
| Maintainability | Zero build warnings; view-model logic testable without a UI; engine untouched by UI concerns | Build gates; suite layering |

## Data Requirements

### Data Model Overview

The only persistent artifact is the `.vis` project file: an XML document with an inline DTD
holding the full installation (localities, products, function blocks, links, programs, project
metadata) as one element tree with stable hexadecimal ids. In memory it is an immutable tree; edits
happen in editor sessions that produce new snapshots (enabling undo). The embedded catalog is
read-only compiled-in data.

### Data Integrity and Retention

- **Integrity**: atomic saves; validator gate before save/transfer; ids never reused.
- **Retention**: project files belong to the user on their file system; the app keeps no hidden copies of them.
- **Privacy**: project info may contain customer names/addresses. The app sends no file content anywhere; optional telemetry must not include project data. Controller credentials are handled by settings encryption, never stored in project files.

## Glossary

| Term | Definition |
| ------ | ----------- |
| IHC controller | The physical unit running a home installation; executes the deployed project. |
| `.vis` file | The XML project file (with inline DTD) holding a controller's complete configuration. |
| Locality | A room/place node organizing products and function blocks. Localities are the **shared spine of both panes** — the same locality appears in each, holding its products on the left and its blocks on the right. |
| Installation pane | The **left** tree: localities → products → pins. The physical view — what is installed and where. **Products are inserted here, and only here** (FR-2.1a). |
| Functions pane | The **right** tree: localities → function blocks → pins. The logic view — what the installation does. **Function blocks are inserted here, and only here** (FR-2.1a). |
| Product | A physical device definition (switch, lamp output, sensor, …) instantiated from the catalog into a locality. Lives in the **Installation (left)** pane. |
| Function block | A reusable logic component with typed pins, variables, and programs. Lives in the **Functions (right)** pane. |
| Pin / resource | An addressable input/output/variable on a product or block; the endpoint of links. A **product's** pins are declared by its catalog type and are **not** independently editable — not deletable, not reorderable (FR-8.4). A **block's** variables are authored (F7). |
| Link | A **directed** connection routing a signal from a **source** pin to a **target** pin. Its two halves record the direction: the **source** carries the `link_from_resource` half, the **target** the `link_to_resource` half — the element names read backwards from the roles (FR-6.2a). Legality is a data-flow rule, not a kind match (FR-6.1a). |
| Scene / scenario link | A link carrying a preset (light level + ramp, or on/off) recalled by one trigger. A **distinct link family** — the data-flow rule in FR-6.1a does not cover it. |
| Catalog | The library of stock product and function-block definitions; embedded in the app. Distinct from the **insert menu**, which is the app's *presentation* of the catalog and can differ from it. |
| Locked (stock) block | A catalog-supplied block that is read-only until explicitly unlocked. The unlock is silent and **undoable** (FR-5.2). |
| `locked` (product attribute) | Per-element flag deciding whether a placed product's *Name* is editable. Resolved against the **project's own inline DTD** (default `no`), **not** the catalog's (default `yes`) — the catalog value is only the seed written at insert time (FR-4.2). |
| Preserve save | The byte-identical save mode for unchanged content; default save re-stamps metadata as the format requires. |

---

# Part 3 — Test Information

## Test Oracles

Correctness is judged against fixed oracles rather than opinion:

| Oracle Type | Application | Example |
| ------------ | ------------- | --------- |
| Committed reference files (byte comparison) | Round-trip and authoring fidelity | Loading a reference `.vis` and preserve-saving reproduces the file byte-for-byte; scripted edit sequences reproduce the committed result files exactly. |
| IHC controller acceptance | Interop | An IHC controller loads and runs projects IHC OpenVisual wrote. |
| Invariant checking | Editing semantics | Id allocation is monotonic and never reuses freed ids; links are always reciprocal; validator findings for known-bad inputs. |
| Known-answer tests | Templates and catalog | A new empty project equals the known template output; embedded catalog components match their committed reference definitions. |
| Property-based properties | Serialization robustness | Encode/decode round-trip properties over generated text. |
| Expected UI state | Headless UI checks | Windows and view-models bind and reach the expected state after simulated user actions. |

## Test Data

The reference corpus is committed and self-contained: no controller, no external install, and no private
data are required to exercise the engine, unit, and UI checks. Fixtures contain no credentials or
personal data.

---

# Part 4 — Links and References

## Source Code

- Public repository: <https://github.com/mmc41/IHCClientSDK> — mono-repo containing the app and the shared engine.

## Companion Specifications

| Document | Location |
| ---------- | ---------- |
| Epics & user stories (E1–E16, US-NNN) — the detailed spec; **start here for any feature** | `applications/ihc_openvisual/docs/stories/` |
| Icon design guidelines (flat-line SVG family) | `applications/ihc_openvisual/docs/icons_design.md` |
| Icon selection reference (`.vis` element → SVG) | `applications/ihc_openvisual/docs/icon_codes.md` |

## Standards and Specifications

- Apache-2.0 — repository license (`LICENSE.md`).
- WCAG-informed icon rules — state never signaled by color alone; decorative icons always paired with text labels (see icon design guidelines).
- IHC `.vis` / `.def` / `.ifb` file formats — undocumented; treated as a byte-exact contract enforced by the reference test corpus rather than a written spec.
