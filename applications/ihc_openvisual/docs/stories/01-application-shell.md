---
version: 0.7.0
last-updated: 2026-08-03
status: draft
---

# E1 — Application shell & project lifecycle

> **Scope:** In scope (foundational) — this epic is the base every other epic plugs into: the
> application's start-up/shutdown, its configuration/logging/telemetry bootstrap, the two-pane window
> shell and its menu-bar host, and the core project-file CRUD.

**Goal:** Give IHC OpenVisual a stable foundation — a reliably starting main window (its menu-bar
host, toolbar, two side-by-side tree panes and status bar), its configuration/logging/telemetry
bootstrap, and the project-file lifecycle (create, name, save, reopen, quit) — so that every
later capability area (E2–E16) has a predictable home to plug into.

**Scope:** application start-up and shutdown (loading configuration, establishing the
logging/telemetry and project-service composition root, capturing unhandled errors, and quitting with
an unsaved-changes prompt); the main window and its chrome (title bar, the eight-title menu bar
*as the extensible host for every epic's commands*, toolbar, the two tree panes, status bar);
light/dark theme; showing/hiding the toolbar and status bar from *Vis*; and the *Filer* project
operations: new, open, save, save-as, close (with its save prompt), recent projects, and
the single-project constraint. **Scope excludes:** the *command inventory* inside
*Edit/Insert/Library/Controller/Documentation* (owned by E2–E7, E9–E10, E14–E16),
locality/product/function-block content (E2–E7), controller send/retrieve (E10), the detailed
keyboard model (E11), and **the *Simulation* menu/toolbar entirely** — simulation (E8) is out of scope
for IHC OpenVisual, so the shell carries **eight** menu titles (no *Simulation*) and the toolbar has no
simulation Start/Stop pair.

**Acceptance criteria (epic level):**
- MUST: On launch the app presents a single top-level window titled *`<project> - IHC OpenVisual`*
  with an eight-item menu bar (no *Simulation*), a toolbar, two headed tree panes (*Installation*,
  *Funktioner*), and a status bar.
- MUST: The application starts without a controller, network or prior IHC software installation, establishes one
  shared logging/telemetry pipeline and one project service for the whole window, and captures
  unhandled errors to diagnostics rather than terminating silently or leaving a corrupt file.
- MUST: The eight-title menu bar is a stable host — the shell owns *Filer*, *Vis* and *Hjælp*; the
  remaining titles are always present and populated by their owning epics.
- MUST: A project can be created, named and saved to a `.vis` file, and exactly one project is open at
  a time.
- SHOULD: The four most recent projects are reachable from the *Filer* menu.
- SHOULD: The installer can show or hide the toolbar and the status bar from the *Vis* menu, and the
  menu reflects each element's current visibility.
- SHOULD: The workspace renders in either a light or a dark theme.
- SHOULD: An *About* item on the *Hjælp* menu identifies the application, its author and source repository, and its application and SDK versions.

**Readiness:** Ready.

---

## US-001 — Two-pane configuration workspace

**As an** IHC installer, **I want** the application to open onto a single configuration workspace with
a menu bar, toolbar, two labelled tree panes and a status bar, **so that** I can see the installation
and its functions side by side and reach every command from a predictable place.

**Scope excludes:** programming-mode layout (US-026), simulation-mode colouring (US-034), and the
per-menu command inventory beyond the top-level menu titles.

### Acceptance criteria (Checklist)

- MUST: The window title bar shows `<document> - IHC OpenVisual`, where `<document>` is the
  application's own name for an unsaved document (`unavngivet` — the vendor's own token, lowercase as the vendor shows it; amended 2026-08-09, alignment F-14) before the first save and the file name
  (e.g. `project3.vis`) afterwards; the
  IHC OpenVisual application icon appears as the window icon, with standard Minimize/Maximize/Close buttons at top-right.
- MUST: The title bar carries a **dirty marker** while the open project has unsaved changes: a bullet (`•`)
  appended directly to the document name — `project3.vis• - IHC OpenVisual` — and nothing else in the title.
  The marker appears on the first unsaved change, disappears on save, and also disappears when undo returns
  the project to its last saved state. A clean project (just created, opened, or saved) shows the plain
  title. The unsaved-changes guard (US-002) remains the authoritative protection; the marker is the
  at-a-glance cue.
- MUST: A single menu bar shows exactly these **eight** titles, left to right: **Filer** (File),
  **Rediger** (Edit), **Vis** (View), **Indsæt** (Insert), **Bibliotek** (Library), **Controller**,
  **Dokumentation** (Documentation), **Hjælp** (Help) — *Simulation* is out of scope (E8) and is
  omitted. The Danish word is the shipped label; the English gloss is how the rest of these stories name
  a menu or a command for readability. **Naming a menu or command in English in a story is a reference to
  it, never a specification of its label** — the label's language is governed by the next rule.
- MUST: **The application's own chrome is written in one language — Danish.** Every caption the
  application itself invents — menu titles and menu items, the default names it supplies for containers
  it creates, the unsaved-document name, dialog labels and status-bar sentences — belongs to that one
  language; the workspace must not mix two languages in text it authors. (The two pane headers are the
  fixed words specified above and stand as written.)
- MUST: **Text that comes from the project file or the component catalog is rendered verbatim.** The
  application never restates a stored caption or a catalog name in another language — a container whose
  stored name reads `Betingelser` is shown as `Betingelser`, not translated (US-018's stored-caption
  rule; catalog names per US-010 and US-063). Where the application supplies a default for text the file
  does not carry, that default is its own chrome and follows the previous rule.
- MUST: The menu bar is a stable host for the whole application: all eight titles are present at
  all times; the shell populates *Filer*, *Vis* and *Hjælp* itself, while *Rediger* (E14–E15), *Indsæt*
  (E2–E7), *Bibliotek* (E5, E16), *Controller* (E10) and *Dokumentation* (E9) are
  populated by their owning epics and remain visible even before those epics land.
- MUST: A toolbar sits below the menu bar with, left to right, New / Open / Save, a separator,
  Help, and a controller send/retrieve pair, then Cut / Copy / Paste. (The simulation Start/Stop pair
  is out of scope (E8) and omitted.)
- MUST: The client area is split into two vertical panes of equal prominence; the left pane has a
  blue header reading **Installation** and the right a blue header reading **Funktioner**.
- MUST: In configuration mode both panes show a tree rooted at the project's locality-container node
  (see US-006); the left tree is the installation view and the right tree is the functions view of the
  same localities.
- MUST: A status bar spans the bottom; its left region shows the result/hint of the last action as
  a short sentence, and a locale indicator (Danish flag) sits at the far right.
- MUST: A **controller-connection indicator** sits in the status bar, next to the locale indicator, and
  shows whether a controller is currently reachable. It has two states — connected and not connected —
  and they are told apart by **glyph shape**, never by colour alone (per the icon design guideline), with
  the state also given in words as the indicator's tooltip and accessible name. The indicator is always
  present: "not connected" is a state it displays, not a reason to hide it. (Sending and retrieving a
  project are E10's; this is only the at-a-glance state.)
- SHOULD: The vertical boundary between the two panes is a splitter the installer can drag to
  reallocate width between *Installation* and *Funktioner*.
- SHOULD: The workspace renders in either a light or a dark theme; tree icon ink and node state
  colours follow the active theme's tokens (per the icon design guideline).
- SHOULD: The installer can change the workspace **text size** from *Vis*, choosing among a small set of
  named steps with a normal default. Every piece of workspace text scales together — tree labels, menu
  and status text — so the relative hierarchy is preserved and no text is clipped or overlapped at any
  step. The choice takes effect immediately, without reopening the project or restarting.
- SHOULD: When the operating system reports a **high-contrast** display preference, the workspace adopts
  a high-contrast palette without the installer having to ask. The workspace follows the preference for
  as long as it is set, and returns to the ordinary palette when it is cleared, both while running.

### AC illustrations

- Immediately after launch with no project: title bar reads `unavngivet - IHC OpenVisual`; both panes
  list the ten default rooms; the status bar left region may show a residual hint such as
  `Undoing insertion of <product>`, and its right end shows the connection indicator in its
  not-connected form beside the locale flag.
- After saving as `project3.vis`: the title bar reads `project3.vis - IHC OpenVisual`; nothing else in
  the chrome changes.
- Editing the saved project (e.g. inserting a locality) changes the title to `project3.vis• - IHC OpenVisual`;
  `Ctrl+Z` back to the saved state clears the bullet without saving; a new edit brings it back.
- Layout reference: the target arrangement is a title bar ending in the application name, the
  eight-title menu bar (**Filer, Rediger, Vis, Indsæt, Bibliotek, Controller, Dokumentation, Hjælp** — no
  *Simulation*), a toolbar of New / Open / Save · Help · controller send/retrieve · Cut / Copy / Paste,
  two blue-headed tree panes with a central splitter, and a status bar with
  a left hint (e.g. `For help, press F1`) and a locale flag at the far right.

### Constraints

- Verification method — **Inspection** of the application window.
- Exact pixel dimensions and default window size are not specified (out of scope).
- Pane headers read the fixed words *Installation* / *Funktioner*.
- The theme, text size and contrast choices are session-scoped: nothing requires them to survive a
  restart. (Remembering workspace preferences across sessions is not specified here.)
- The named text-size steps and their exact scale factors are not specified; only that there are
  several, that *Normal* is the default, and that they are ordered smallest to largest.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the window, the eight-title menu bar, the toolbar, the two
headed panes and the status bar are all in place, and the status bar now carries the
**controller-connection indicator** (two distinct glyphs plus the state in words; the app is offline today,
so only the not-connected state is ever shown in practice). Both language rules hold: the **verbatim** rule
(a stored caption is never restated), and the **one-language** rule — the menu titles and items, the dialog
labels and captions, the status-bar sentences, the default container names and the unsaved-document name are
all Danish.

---

## US-002 — Create a new project

**As an** IHC installer, **I want** to start a new, empty project, **so that** I can begin a fresh
installation from the standard starting point.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Start a new project from the menu
  Given IHC OpenVisual is running
  When I choose "File" > "New project" (or press Ctrl+N)
  Then the workspace shows the standard empty project: both panes rooted at "Lokaliteter"
    with the ten default rooms, and the title bar shows "unavngivet - IHC OpenVisual"
  And the project records the installer contact details held in application settings,
    and the signed-in user as its programmer

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

### Business rules (the unsaved-changes guard)

This guard is raised wherever the open project would be discarded — *New*, *Open*, *Close* (US-004) and
*Exit* (US-064). It is specified once here.

- MUST: The prompt **names the file** whose changes are at stake, so a user with several projects in mind
  knows which one is being discarded.
- MUST: It offers **three** outcomes — save and continue, discard and continue, or cancel and stay.
- MUST: The three outcomes are labelled by what they **do**: **`Save`** / **`Don't save`** / **`Cancel`**.
  Labelling by action is the modern platform convention and is unambiguous, whereas a `Yes` / `No` labelling
  would require re-reading the question to know what each answer does.
- MUST: `Esc` cancels the prompt and leaves the project open, and the **`Cancel`** option is focused when it
  opens (US-069).

### AC illustrations

- The standard empty project contains ten localities — Stue, Entré, Køkken, Soveværelse, Værelse,
  Bad, Bryggers, Garage, Kælder, Udendørs — in both panes, plus the two built-in enumerator
  types available to programming — `Persienne tilstand` (blind state) and `Logning` (logging), always
  present and read-only; see US-030.
- Choosing *File > New project* with unsaved edits to `StandardHouse_1.vis` raises a prompt naming
  `StandardHouse_1.vis` and offering `Save` / `Don't save` / `Cancel`; `Cancel` returns to the project with
  its edits intact.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-003 — Save a project (Save / Save as)

**As an** IHC installer, **I want** to name and save my project to a file and re-save it quickly
thereafter, **so that** my configuration is persisted under a meaningful name.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: First save via Save As
  Given a new, unnamed project ("unavngivet")
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
  untouched, and no data is rewritten. (A no-op save therefore differs from the original only in those two
  stamps — the save is deliberately non-idempotent.)

### Business rules (what a save preserves on disk)

- MUST: Any save that overwrites an existing project file — plain *Save* and *Save as* alike — first
  preserves the file's previous version next to it, under the same name with the extension replaced by
  `.BAK`. A later overwrite replaces the `.BAK` with the newly displaced version.
- MUST: Saving to a file name that does not yet exist creates no `.BAK` file.
- MUST: The `.BAK` file is **not** deleted on a clean close — it stays on disk as the previous version
  of the file.
- MUST: A save is refused when the project contains text that cannot be stored in the project file's
  character repertoire (ISO-8859-1): the refusal message names the offending element, attribute and
  character, nothing is written to disk (the existing file and its `.BAK` are untouched), and the edit
  stays in the open document so the installer can correct it.

### AC illustrations

- Saving an untitled project as `project3.vis` changes the title bar from `unavngivet - IHC OpenVisual`
  to `project3.vis - IHC OpenVisual`; the two panes and their content are unchanged.
- Opening a project and immediately saving it without editing produces a file that differs from the
  original in exactly two places — the modified timestamp and the save id.
- Saving `renamed.vis` where a `renamed.vis` already exists leaves the displaced bytes in
  `renamed.BAK`; the first save of a brand-new name produces no `.BAK`.
- Attempting to save a project whose note contains an em dash (not representable in ISO-8859-1) shows
  a message naming the element, the attribute and the character; the file on disk is unchanged and the
  note keeps its em dash in the editor.

### Constraints

- `Ctrl+S` = *Save project*; `F2` = properties of the selected element (`F2` is properties only, not save).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-004 — Open recent and existing projects

**As an** IHC installer, **I want** to reopen a project I saved earlier — by browsing or from a recent
list — **so that** I can resume work without retyping paths.

### Acceptance criteria (Given-When-Then)

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

Scenario: Open a project by handing it to the application
  Given IHC OpenVisual is not running
  When the desktop starts it on a ".vis" file — because I opened that file with it
  Then that project is what the application opens, instead of the empty starting project
  And it is opened exactly as browsing to it would open it: same refresh, no unsaved-changes state,
      and it joins the recent list

Scenario: The handed-over file cannot be read
  Given the desktop starts IHC OpenVisual on a ".vis" file that is missing or unreadable
  When the application starts
  Then it says so, naming the file, exactly as a failed "Open project" would
  And it comes up on the empty starting project rather than failing to start
```

### Business rules (what opening does to the document)

- MUST: Opening a project refreshes the built-in enumerator definitions the file carries: each is
  re-registered at the end of the project's enumerator-definition list under a fresh id, with every
  reference updated to the fresh id. The project's content is otherwise untouched.
- MUST: The just-opened document shows **no** unsaved-changes state, and the refresh is not an
  undoable edit; the next save persists the refreshed definitions.

### AC illustrations

- Opening a saved project and saving it again immediately produces a file whose built-in enumerator
  definitions sit at the end of the definition list under new ids (plus the two save stamps, US-003);
  opening and saving that file again repeats the refresh with the next ids — the ids advance on every
  open, they are not stable.

### Constraints

- The recent-projects area holds **four** slots.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-051 — Show or hide the toolbar and status bar

**As an** IHC installer, **I want** to toggle the toolbar and the status bar on or off from the *Vis*
menu, **so that** I can reclaim screen space or restore the chrome to suit how I am working.

**Scope excludes:** switching between configuration and programming views (US-026); the toolbar's
button inventory (US-001).

### Acceptance criteria (Given-When-Then)

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
  toggling it on restores the exact same strip. The *Vis* menu item's check mark tracks the current
  state.

### Constraints

- Verification method — **Demonstration** of each toggle and **Inspection** that the *Vis* menu check
  state matches the visible/hidden state.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-063 — Start into a ready workspace with safe diagnostics

**As an** IHC installer, **I want** IHC OpenVisual to start straight into a ready workspace and to
record its own diagnostics safely, **so that** I can begin work immediately and get support when
something goes wrong without exposing my project data.

**Scope excludes:** the on-screen content and chrome of the workspace (US-001); this story covers the
start-up path and the diagnostics/telemetry foundation only.

### Acceptance criteria (Checklist)

- MUST: Launching the application opens the main window on a standard new, empty project (US-002)
  without requiring a connected controller, a network connection, or any prior IHC software installation.
- MUST: At start-up the application establishes one shared logging/telemetry pipeline and one
  project service, both used by the whole window for the rest of the session.
- MUST: The application reads its logging and telemetry configuration from its settings; it exports
  logs and traces to independently configured endpoints — a logs endpoint and a traces endpoint,
  each enabled only when its URL is set and sharing optional authentication headers — and when neither
  endpoint is configured it starts and runs normally with local logging only.
- MUST: An unhandled error is recorded to the diagnostics pipeline (logged and attached to the
  active trace) and does not terminate the application silently or leave a partially written `.vis`
  file.
- MUST: Exported diagnostics and telemetry contain no `.vis` project content and no controller
  credentials.
- SHOULD: When a telemetry self-check endpoint is configured, the application probes it once at
  start-up in the background (without delaying the workspace from opening) and reports an unreachable or
  rejecting endpoint to diagnostics, so a misconfigured collector fails visibly instead of dropping
  telemetry silently; leaving the self-check endpoint unset skips the probe.
- SHOULD: The current effective settings and an entry point to telemetry diagnostics are reachable
  from the *Hjælp* menu.

### AC illustrations

- Starting the app with no telemetry endpoints in its settings still opens the empty-project workspace
  and writes local logs; configuring the logs and traces endpoints later causes those logs and
  traces to also appear at the collector.
- Pointing the self-check endpoint at a collector that is down surfaces a start-up diagnostic naming the
  unreachable endpoint while the workspace still opens normally; with the collector reachable the
  self-check records a success instead.
- If an edit handler throws, the error surfaces in the log and the open project on disk remains its
  last consistent state — the target `.vis` file is never left half-written.

### Constraints

- Verification method — **Test** (start-up with and without the telemetry endpoints configured; point
  the self-check endpoint at a down collector and confirm the start-up diagnostic names it; inject an
  unhandled error and confirm it is captured and the file stays intact) and **Inspection** (confirm
  exported payloads carry no project content or credentials).

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented.

---

## US-064 — Quit the application

**As an** IHC installer, **I want** to quit IHC OpenVisual and be warned about unsaved work first,
**so that** I never lose changes by closing the application.

**Scope excludes:** closing a project while keeping the application open — that is the *File > Close* /
*New* / *Open* save-prompt path (US-002, US-004).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Quit with no unsaved changes
  Given the open project has no unsaved changes
  When I choose "File" > "Exit" (or press Alt+F4, or use the window close button)
  Then the application closes

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

- Pressing `Alt+F4` on a freshly opened, unmodified project closes the app immediately; pressing it
  after an edit first raises the save prompt.

### Constraints

- The *File > Exit* menu item advertises **no** keyboard accelerator — `Alt+F4` and the window close
  button are the window manager's gestures, not the command's, and both still quit through the same
  guarded path.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-065 — Application About dialog

**As an** IHC installer, **I want** to open an *About* dialog that names the application and shows its
version, source repository and author, **so that** I can confirm exactly which build I am running and
reach the project's source when I need support or want to report a problem.

**Scope excludes:** context-sensitive topic help and the *Hjælp* menu's diagnostics/settings
entry (US-063); this story covers only the About dialog and the *Hjælp* menu command that opens it.

### Acceptance criteria (Checklist)

- MUST: Choosing *Hjælp* > *About…* opens a single modal About dialog titled `About IHC OpenVisual`,
  centred on the main window and blocking interaction with the main window until it is dismissed.
- MUST: The dialog shows the application name **IHC OpenVisual** as its heading.
- MUST: The dialog shows two labelled version lines — the application version and the SDK
  version — matching the versions of the application and the bundled SDK assembly.
- MUST: The dialog shows the source-repository URL, the author/attribution, and a one-line
  description of the application.
- MUST: A *Close* button dismisses the dialog and returns focus to the main window, and pressing
  `Esc` does the same.
- SHOULD: Activating the repository URL opens it in the operating system's default browser; if the
  browser cannot be launched the dialog stays open and the failure is recorded to diagnostics instead of
  terminating the application.
- MAY: The dialog is fixed-size (not resizable), consistent with the application's other modal dialogs.

### AC illustrations

- Choosing *Hjælp* > *About…* in the application opens a centred, fixed-size window titled
  `About IHC OpenVisual` showing the heading `IHC OpenVisual`, the lines `App Version: <x.y.z>` and
  `SDK Version: <a.b.c>`, the author `Morten Christensen (mmc41)`, a short description, and the link
  `https://github.com/mmc41/IHCClientSDK`; the main window cannot be clicked until the dialog closes.
- Clicking the `https://github.com/mmc41/IHCClientSDK` link opens that page in the default browser;
  pressing `Esc` (or *Close*) returns to the workspace with the open project unchanged.

### Constraints

- Verification method — **Demonstration** of opening and dismissing the dialog and **Inspection** that
  the shown application and SDK versions match the built assemblies' version metadata.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
