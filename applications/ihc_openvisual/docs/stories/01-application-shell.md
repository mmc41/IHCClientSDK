---
version: 0.4.0
last-updated: 2026-07-16
status: draft
implementation-status: mostly-implemented
---

# E1 — Application shell & project lifecycle

> **Implementation status:** ✅ Implemented.

> **Current scope:** ✅ **In scope (foundational)** — this epic is the base every other epic plugs
> into: the application's start‑up/shutdown, its configuration/logging/telemetry bootstrap, the
> two‑pane window shell and its menu‑bar host, and the core project‑file CRUD.

**Goal:** Give IHC OpenVisual a stable foundation — a reliably starting main window (its menu‑bar
host, toolbar, two side‑by‑side tree panes and status bar), its configuration/logging/telemetry
bootstrap, and the project‑file lifecycle (create, name, save, reopen, recover, quit) — so that every
later capability area (E2–E16) has a predictable home to plug into.

**Scope:** application start‑up and shutdown (loading configuration, establishing the
logging/telemetry and `ProjectAppService` composition root, capturing unhandled errors, and quitting
with an unsaved‑changes prompt); the main window and its chrome (title bar, the eight‑title menu bar
*as the extensible host for every epic's commands*, toolbar, the two tree panes, status bar);
light/dark theme; showing/hiding the toolbar and status bar from *View*; and the *File* project
operations: new, open, save, save‑as, close (with its save prompt), recent projects, auto‑backup, and
the single‑project constraint. **Scope excludes:** the *command inventory* inside
*Edit/Insert/Library/Controller/Documentation* (owned by E2–E7, E9–E10, E14–E16),
locality/product/function‑block content (E2–E7), controller send/retrieve (E10), the detailed
keyboard model (E11), and **the *Simulation* menu/toolbar entirely** — simulation (E8) is out of scope
for IHC OpenVisual, so the shell carries **eight** menu titles (no *Simulation*) and the toolbar has no
simulation Start/Stop pair.

**Acceptance criteria (epic level):**
- MUST: On launch the app presents a single top‑level window titled *`<project> - IHC OpenVisual`*
  with an eight‑item menu bar (no *Simulation*), a toolbar, two headed tree panes (*Installation*,
  *Functions*), and a status bar.
- MUST: The application starts without a controller, network or prior IHC software installation, establishes one
  shared logging/telemetry pipeline and one `ProjectAppService` for the whole window, and captures
  unhandled errors to diagnostics rather than terminating silently or leaving a corrupt file.
- MUST: The eight‑title menu bar is a stable host — the shell owns *File*, *View* and *Help*; the
  remaining titles are always present and populated by their owning epics.
- MUST: A project can be created, named and saved to a `.vis` file, and exactly one project is open at
  a time.
- SHOULD: The four most recent projects are reachable from the *File* menu, and an automatic backup
  protects against crash/power loss.
- SHOULD: The installer can show or hide the toolbar and the status bar from the *View* menu, and the
  menu reflects each element's current visibility.
- SHOULD: The workspace renders in either a light or a dark theme.
- SHOULD: An *About* item on the *Help* menu identifies the application, its author and source repository, and its application and SDK versions.

**Readiness:** Ready.

---

## US-001 — Two‑pane configuration workspace

**As an** IHC installer, **I want** the application to open onto a single configuration workspace with
a menu bar, toolbar, two labelled tree panes and a status bar, **so that** I can see the installation
and its functions side by side and reach every command from a predictable place.

**Scope excludes:** programming‑mode layout (US-026), simulation‑mode colouring (US-034), and the
per‑menu command inventory beyond the top‑level menu titles.

### Acceptance criteria (Checklist)

- [x] MUST: The window title bar shows `<document> - IHC OpenVisual`, where `<document>` is
  `Untitled` before the first save and the file name (e.g. `project3.vis`) afterwards; the
  IHC OpenVisual application icon appears as the window icon, with standard Minimize/Maximize/Close buttons at top‑right.
  *(Title logic done and tested; the window icon is the `IHC OpenVisual` house lockup `Assets/openvisual.ico`; min/max/close use the default Avalonia chrome.)*
- [x] MUST: The title bar shows **no dirty marker** — no `*` or equivalent — even when the open project has
  unsaved changes. Dirty state is tracked internally and surfaced by the unsaved‑changes guard (US-002),
  not by the title.

  > **Confirmed 2026‑07‑16 — regression baseline, both apps aligned.** Neither app surfaces a title‑bar
  > dirty indicator, though both track dirty (each guard fires on a pending edit, which proves it).
  > Recorded so nobody adds a marker to IHC OpenVisual unilaterally — **if a marker is ever wanted it should
  > go on both**, and it is not a divergence today. Evidence: `RESULTS.md` **F‑041** (both titles read with a
  > pending edit present).
- [x] MUST: A single menu bar shows exactly these **eight** titles, left to right: **File, Edit, View,
  Insert, Library, Controller, Documentation, Help** — *Simulation* is out of scope (E8) and is
  omitted (amended from the original nine‑title requirement).
  *(Smoke test asserts `menu.Items.Count == 8`.)*
- [x] MUST: The menu bar is a stable host for the whole application: all eight titles are present at
  all times; the shell populates *File*, *View* and *Help* itself, while *Edit* (E14–E15), *Insert*
  (E2–E7), *Library* (E5, E16), *Controller* (E10) and *Documentation* (E9) are
  populated by their owning epics and remain visible even before those epics land.
  *(File/View/Help populated; Edit/Insert/Library/Controller/Documentation present as empty hosts.)*
- [x] MUST: A toolbar sits below the menu bar with, left to right, New / Open / Save, a separator,
  Help, and a controller send/retrieve pair, then Cut / Copy / Paste. *(The simulation Start/Stop pair
  is out of scope (E8) and omitted; controller/cut/copy/paste buttons are present but disabled pending
  their epics.)*
- [x] MUST: The client area is split into two vertical panes of equal prominence; the left pane has a
  blue header reading **Installation** and the right a blue header reading **Functions**.
- [x] MUST: In configuration mode both panes show a tree rooted at a **Localities** node (see US-006);
  the left tree is the installation view and the right tree is the functions view of the same
  localities.
- [x] MUST: A status bar spans the bottom; its left region shows the result/hint of the last action as
  a short sentence, and a locale indicator (Danish flag) sits at the far right.
- [x] SHOULD: The vertical boundary between the two panes is a splitter the installer can drag to
  reallocate width between *Installation* and *Functions*.
- [x] SHOULD: The workspace renders in either a light or a dark theme; tree icon ink and node state
  colours follow the active theme's tokens (product UI baseline, per the icon design guideline).

### AC illustrations

- Immediately after launch with no project: title bar reads `Untitled - IHC OpenVisual`; both panes
  list the ten default rooms; the status bar left region may show a residual hint such as
  `Undoing insertion of <product>`.
- After saving as `project3.vis`: the title bar reads `project3.vis - IHC OpenVisual`; nothing else in
  the chrome changes.
- Layout reference: the target arrangement is a title bar ending in the application name, the
  eight‑title menu bar (**File, Edit, View, Insert, Library, Controller, Documentation, Help** — no
  *Simulation*), a toolbar of New / Open / Save · Help · controller send/retrieve · Cut / Copy / Paste,
  two blue‑headed tree panes with a central splitter, and a status bar with
  a left hint (e.g. `For help, press F1`) and a locale flag at the far right.

### Constraints

- Verification method — **Inspection** of the application window; exact pixel
  dimensions and default window size are not specified and are out of scope (R‑note,
  not a blocker).
- Note: *Simulation* has since been ruled **out of scope** (E8), so the menu bar carries eight titles.
  Of those, *File*, *Controller* and *Documentation* were the firm requirements; the toolbar inventory
  and status bar were provisional and are now confirmed as implemented. (R‑note, resolved.)
- Note (pane headers): each pane header could instead show the *name of the currently‑shown root node*
  (e.g. `Custom blok`, `Lokaliteter`, or a block's id‑name) rather than the fixed words
  *Installation*/*Functions*. IHC OpenVisual should decide during implementation whether it
  keeps fixed *Installation*/*Functions* headers (clearer for new English‑speaking users) or uses a
  dynamic root‑node header; the fixed labels are the current provisional choice. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-002 — Create a new project

**As an** IHC installer, **I want** to start a new, empty project, **so that** I can begin a fresh
installation from the standard starting point.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Start a new project from the menu
  Given IHC OpenVisual is running
  When I choose "File" > "New project" (or press Ctrl+N)
  Then the workspace shows the standard empty project: both panes rooted at "Localities"
    with the ten default rooms, and the title bar shows "Untitled - IHC OpenVisual"

Scenario: New project prompts to save the currently open one
  Given a project is already open with unsaved changes
  When I choose "File" > "New project"
  Then the application asks whether to save the currently open project before closing it
  And it proceeds to the empty project only after I answer the prompt

Scenario: Only one project is open at a time
  Given a project is already open
  When the new project opens
  Then the previously open project is closed (it is not shown alongside the new one)
```

### Business rules (the unsaved‑changes guard)

This guard is raised wherever the open project would be discarded — *New*, *Open*, *Close* (US-004) and
*Exit* (US-064). It is specified once here.

- MUST: The prompt **names the file** whose changes are at stake, so a user with several projects in mind
  knows which one is being discarded.
- MUST: It offers **three** outcomes — save and continue, discard and continue, or cancel and stay.
- MUST: The three outcomes are labelled by what they **do**: **`Save`** / **`Don't save`** / **`Cancel`**.

  > **Deliberate divergence (C), granted 2026‑07‑16 under US-045 (follow platform convention).** IHC Visual
  > labels the same three outcomes **`Yes` / `No` / `Cancel`** on a MessageBox titled `LK IHC Visual ®`. The
  > two guards are **functionally aligned** — both name the file, both offer save/discard/cancel — and
  > `Save`/`Don't save` is the modern Windows convention, which is unambiguous where *Yes*/*No* requires the
  > user to re‑read the question to know what *No* discards. IHC OpenVisual keeps its labels. **Cited here
  > so nobody "aligns" them back to Yes/No.** Evidence: `RESULTS.md` **F‑039** (`S01\60-dirty-guard-vis.png`
  > vs `60-dirty-guard-ov.png`, both raised by File▸New with unsaved edits).

- MUST: `Esc` cancels the prompt and leaves the project open, and the **`Cancel`** option is focused when it
  opens (US-069).

### AC illustrations

- The standard empty project contains ten localities — Living room, Hall, Kitchen, Bedroom, Room,
  Bathroom, Utility room, Garage, Basement, Outdoors — in both panes, plus two built‑in enumerator
  types available to programming (Alarm state, Home simulation; see US-030).
- Choosing *File > New project* with unsaved edits to `StandardHouse_1.vis` raises a prompt naming
  `StandardHouse_1.vis` and offering `Save` / `Don't save` / `Cancel`; `Cancel` returns to the project with
  its edits intact.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the guard is measured **functionally aligned** with IHC Visual,
with its button labels as the granted platform‑convention exception (F‑039).

---

## US-003 — Save a project (Save / Save as)

**As an** IHC installer, **I want** to name and save my project to a file and re‑save it quickly
thereafter, **so that** my configuration is persisted under a meaningful name.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: First save via Save As
  Given a new, unnamed project ("Untitled")
  When I choose "File" > "Save project as"
  Then a file save dialog opens
  And after I type a file name (e.g. "StandardHouse_1") and confirm with "Save"
    the project is written to a ".vis" file and the title bar updates to "<name>.vis - IHC OpenVisual"

Scenario: Quick re-save after the file is named
  Given the project already has a file name
  When I choose "File" > "Save" (or press Ctrl+S)
  Then the project is saved to its existing file without reopening the save dialog

Scenario: Recommended first step
  Given I have just started a new project
  Then the application's guidance is to save it under a suitable name before configuring
```

### Business rules (what a save stamps)

- MUST: Saving stamps the project's **`<modified>` timestamp** with the current date and time, and
  regenerates the document's **save id**. Both are the app's own bookkeeping, not project content.
- MUST: A save writes no other change of its own — the head, the tail and the project's id allocation are
  untouched, and no data is rewritten.

> **Confirmed 2026‑07‑16 — a no‑op save legitimately changes the file.** Loading a project and saving it
> with **no edits** grows the file by **+2 bytes**, from exactly these two stamps (`<modified>` set to now,
> and `id2` regenerated). This is **correct editor behaviour, not a bug**: IHC Visual's save is likewise
> non‑idempotent (it stamps its own modified time and re‑hoists catalog enums). Recorded because it looks
> alarming and had been carried as an open question. Note the distinction from the SDK's byte‑fidelity
> oracles, which round‑trip **exactly** — they preserve bytes and deliberately **don't** stamp; only the
> *app's* save stamps, as it should. Evidence: `RESULTS.md` **F‑047** (byte‑diff: first diff at offset
> 18324 = `id2`, second at 18392 = `<modified>`).

### AC illustrations

- Saving an untitled project as `project3.vis` changes the title bar from `Untitled - IHC OpenVisual`
  to `project3.vis - IHC OpenVisual`; the two panes and their content are unchanged.
- Opening a project and immediately saving it without editing produces a file that differs from the
  original in exactly two places — the modified timestamp and the save id.

### Constraints

- The keyboard shortcut set assigns `Ctrl+S` = *Save project* and `F2` = properties of the selected
  element, but some guidance also cites `F2` for quick-save — an unresolved conflict. IHC OpenVisual
  should honour `Ctrl+S` for save and resolve during implementation whether `F2` ever saves before assigning
  `F2` to save; otherwise `F2` remains properties only. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-004 — Open recent and existing projects

**As an** IHC installer, **I want** to reopen a project I saved earlier — by browsing or from a recent
list — **so that** I can resume work without retyping paths.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Open an existing project by browsing
  Given IHC OpenVisual is running
  When I choose "File" > "Open project" (or press Ctrl+O)
  Then a file open dialog appears, defaulting to the directory where I last saved an IHC OpenVisual file
  And selecting a ".vis" file opens it as the single active project

Scenario: Open one of the four most recent projects
  Given I have worked on projects before
  When I open the "File" menu
  Then the names of the four most recently used projects are listed near the bottom of the menu
  And clicking a name opens that project directly

Scenario: Opening replaces the current project
  Given a project with unsaved changes is open
  When I open another project
  Then the application first prompts to save the open project (single-project constraint, US-002)
```

### Constraints

- **Confirmed 2026‑07‑16 — regression baseline, both apps aligned.** The recent‑projects area holds
  **4 slots** on both apps, and the *File* menu's command set matches. Evidence: `RESULTS.md` **F‑040**
  (vendor's recent list = 4 entries; IHC OpenVisual's *File ▸ Recent projects* submenu = 4 slots).
  ⚠ **Ordering and count with a populated list are not measured** — no project was opened through the recent
  mechanism during the comparison, so only the slot capacity is a baseline, not the MRU ordering.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the 4‑slot recent area is measured aligned (F‑040).

---

## US-005 — Automatic backup and recovery

**As an** IHC installer, **I want** the application to back up my project automatically, **so that** a
crash or power loss does not cost me my recent work.

### Acceptance criteria (Business Rules)

**Trigger rules:**
- MUST: A backup is taken automatically every **10 minutes**, and additionally after every **10th**
  change (event) to the project.
- MUST: The backup is usable for recovery after an application crash, a PC crash, or a power
  interruption.

**Lifecycle rules:**
- MUST: When the installer closes the project deliberately via *File > Close* and answers the
  save prompt (Yes or No), the application **deletes** the backup file — the backup is only a
  crash/power‑loss safety net, not a post‑close undo.

**Output:**
- A backup copy of the project that survives an abnormal termination and is discarded on a clean,
  acknowledged close.

### AC illustrations

- Working continuously for 25 minutes with no manual save produces at least two automatic backups
  (at ~10 and ~20 minutes); making 10 edits within one minute also triggers a backup regardless of the
  timer.
- Choosing *File > Close* and clicking *No* (don’t save) removes the backup — the just‑closed edits
  are not recoverable from backup.

### Constraints

- Verification method — **Test** by simulating abnormal termination and confirming a recoverable
  backup exists; and by confirming the backup is absent after a clean close.

**Readiness:** Ready.

**Implementation status:** 🟡 Implemented, with one wiring gap.

---

## US-051 — Show or hide the toolbar and status bar

**As an** IHC installer, **I want** to toggle the toolbar and the status bar on or off from the *View*
menu, **so that** I can reclaim screen space or restore the chrome to suit how I am working.

**Scope excludes:** switching between configuration and programming views (US-026); the toolbar's
button inventory (US-001).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Hide the toolbar
  Given the toolbar is visible below the menu bar
  When I choose "View" and toggle the toolbar off
  Then the toolbar is hidden and the tree panes reclaim its vertical space
  And the "View" menu shows the toolbar item as unchecked

Scenario: Show the toolbar again
  Given the toolbar is hidden
  When I choose "View" and toggle the toolbar on
  Then the toolbar reappears in its original position with its full button inventory (US-001)

Scenario: Hide and show the status bar
  Given the status bar spans the bottom of the window
  When I choose "View" and toggle the status bar off, then on again
  Then the status bar is hidden and then restored, and the "View" menu reflects its checked state each time
```

### AC illustrations

- Toggling the toolbar off collapses the strip of New/Open/Save… buttons and the panes grow upward;
  toggling it on restores the exact same strip. The *View* menu item's check mark tracks the current
  state.

### Constraints

- Verification method — **Demonstration** of each toggle and **Inspection** that the *View* menu check
  state matches the visible/hidden state.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-063 — Start into a ready workspace with safe diagnostics

**As an** IHC installer, **I want** IHC OpenVisual to start straight into a ready workspace and to
record its own diagnostics safely, **so that** I can begin work immediately and get support when
something goes wrong without exposing my project data.

**Scope excludes:** the on‑screen content and chrome of the workspace (US-001) and the crash‑recovery
backup mechanics (US-005); this story covers the start‑up path and the diagnostics/telemetry
foundation only.

### Acceptance criteria (Checklist)

- [x] MUST: Launching the application opens the main window on a standard new, empty project (US-002)
  without requiring a connected controller, a network connection, or any prior IHC software installation.
- [x] MUST: At start‑up the application establishes one shared logging/telemetry pipeline and one
  `ProjectAppService`, both used by the whole window for the rest of the session.
- [x] MUST: The application reads its logging and telemetry configuration from its settings; it exports
  logs and traces to independently configured OTLP endpoints — a logs endpoint and a traces endpoint,
  each enabled only when its URL is set and sharing optional authentication headers — and when neither
  endpoint is configured it starts and runs normally with local logging only.
- [~] MUST: An unhandled error is recorded to the diagnostics pipeline (logged and attached to the
  active trace) and does not terminate the application silently or leave a partially written `.vis`
  file.
  *(The `AppDomain` handler attaches the exception to the active `Activity` chain but records it via
  `Trace` only, not `ILogger`; command‑scoped errors do go through `ILogger`. Atomic write / no‑partial‑`.vis`
  is provided by the SDK's atomic Save but is not asserted by an app‑level test.)*
- [~] MUST: Exported diagnostics and telemetry contain no `.vis` project content and no controller
  credentials.
  *(True by omission — the app tags no span with credentials or project content — but there is no active
  scrubbing and no test asserting their absence; SDK span contents not independently verified.)*
- [x] SHOULD: When a telemetry self-check endpoint is configured, the application probes it once at
  start-up in the background (without delaying the workspace from opening) and reports an unreachable or
  rejecting endpoint to diagnostics, so a misconfigured collector fails visibly instead of dropping
  telemetry silently; leaving the self-check endpoint unset skips the probe.
- [x] SHOULD: The current effective settings and an entry point to telemetry diagnostics are reachable
  from the *Help* menu.

### AC illustrations

- Starting the app with no telemetry endpoints in its settings still opens the empty‑project workspace
  and writes local logs; configuring the OTLP logs and traces endpoints later causes those logs and
  traces to also appear at the collector.
- Pointing the self‑check endpoint at a collector that is down surfaces a start‑up diagnostic naming the
  unreachable endpoint while the workspace still opens normally; with the collector reachable the
  self‑check records a success instead.
- If an edit handler throws, the error surfaces in the log and the open project on disk remains its
  last consistent state — the target `.vis` file is never left half‑written.

### Constraints

- Verification method — **Test** (start‑up with and without the telemetry endpoints configured; point
  the self‑check endpoint at a down collector and confirm the start‑up diagnostic names it; inject an
  unhandled error and confirm it is captured and the file stays intact) and **Inspection** (confirm
  exported payloads carry no project content or credentials).
- Foundation note: follow the established `ihc_lab` bootstrap — configuration loaded at start‑up; an
  `ILoggerFactory` composed with an OpenTelemetry OTLP log exporter and a separately‑configured OTLP
  `TracerProvider` (logs and traces have independent endpoints, each enabled only when its URL is set,
  sharing optional authentication headers); the `TracerProvider` and logger factory are held for the
  session and disposed on shutdown so the final batch of spans/logs is flushed; Avalonia framework logs
  forwarded into the same `ILogger` pipeline; a background start‑up probe of the configured self‑check
  endpoint that reports an unreachable or rejecting collector (the OTLP exporter otherwise drops
  rejected batches silently); and an `AppDomain` unhandled‑exception handler that attaches the exception
  to the current `Activity`. Per repo rules the SDK emits traces via `ActivitySource` only and takes no
  logging‑implementation dependency; telemetry wiring lives in the app's composition root. (See
  `utilities/ihc_lab/App/AppSetup.cs`, `App/Program.cs`, `Configuration/Telemetry.cs`, and the SDK's
  `TelemetryConfiguration`.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (MUSTs), with two verification gaps.

---

## US-064 — Quit the application

**As an** IHC installer, **I want** to quit IHC OpenVisual and be warned about unsaved work first,
**so that** I never lose changes by closing the application.

**Scope excludes:** closing a project while keeping the application open — that is the *File > Close* /
*New* / *Open* save‑prompt path (US-002, US-004).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Quit with no unsaved changes
  Given the open project has no unsaved changes
  When I choose "File" > "Exit" (or press Alt+F4, or use the window close button)
  Then the application closes
  And the project's automatic backup is deleted (US-005)

Scenario: Quit with unsaved changes prompts to save
  Given the open project has unsaved changes
  When I choose "File" > "Exit"
  Then the application asks whether to save before quitting
  And it quits only after I answer, saving first if I choose to save

Scenario: Cancel the quit
  Given I have been asked whether to save before quitting
  When I cancel the prompt
  Then the application stays open with the project and its unsaved changes intact
```

### AC illustrations

- Pressing `Alt+F4` on a freshly opened, unmodified project closes the app immediately and removes the
  backup file; pressing it after an edit first raises the save prompt.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-065 — Application About dialog

**As an** IHC installer, **I want** to open an *About* dialog that names the application and shows its
version, source repository and author, **so that** I can confirm exactly which build I am running and
reach the project's source when I need support or want to report a problem.

**Scope excludes:** context‑sensitive topic help (US-049) and the *Help* menu's diagnostics/settings
entry (US-063); this story covers only the About dialog and the *Help* menu command that opens it.

### Acceptance criteria (Checklist)

- [x] MUST: Choosing *Help* > *About…* opens a single modal About dialog titled `About IHC OpenVisual`,
  centred on the main window and blocking interaction with the main window until it is dismissed.
- [x] MUST: The dialog shows the application name **IHC OpenVisual** as its heading.
- [x] MUST: The dialog shows two labelled version lines — the application version and the SDK
  (`ihcclient`) version — matching the versions of the application and the bundled SDK assembly.
- [x] MUST: The dialog shows the source‑repository URL, the author/attribution, and a one‑line
  description of the application.
- [x] MUST: A *Close* button dismisses the dialog and returns focus to the main window, and pressing
  `Esc` does the same.
- [x] SHOULD: Activating the repository URL opens it in the operating system's default browser; if the
  browser cannot be launched the dialog stays open and the failure is recorded to diagnostics instead of
  terminating the application.
- [x] MAY: The dialog is fixed‑size (not resizable), consistent with the application's other modal dialogs.

### AC illustrations

- Choosing *Help* > *About…* in the application opens a centred, fixed‑size window titled
  `About IHC OpenVisual` showing the heading `IHC OpenVisual`, the lines `App Version: <x.y.z>` and
  `SDK Version: <a.b.c>`, the author `Morten Christensen (mmc41)`, a short description, and the link
  `https://github.com/mmc41/IHCClientSDK`; the main window cannot be clicked until the dialog closes.
- Clicking the `https://github.com/mmc41/IHCClientSDK` link opens that page in the default browser;
  pressing `Esc` (or *Close*) returns to the workspace with the open project unchanged.

### Constraints

- Verification method — **Demonstration** of opening and dismissing the dialog and **Inspection** that
  the shown application and SDK versions match the built assemblies' version metadata.
- Foundation note: an equivalent About dialog already exists in `utilities/ihc_lab`
  (`Windows/AboutWindow.axaml`, launched from *Help* > *About…*), reading `Ihc.VersionInfo` for the SDK
  version and the app's own `VersionInfo` for the application version and opening the repository link via
  the OS shell; IHC OpenVisual should follow the same pattern. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
