---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E11 — Interaction & activation model

> **Current scope:** ✅ **In scope (foundational)** — the activation and keyboard model underpins every
> CRUD interaction.

**Goal:** Give every IHC OpenVisual user a consistent way to invoke functions — by right‑click, menu bar,
or keyboard shortcut — and to navigate/edit the trees with the keyboard, so the same command is
reachable three ways and power users can work quickly. This epic is cross‑cutting: it underlies every
capability area (E1–E10).

**Scope:** the three activation methods and their equivalence; the context‑menu / menu‑bar / shortcut
conventions; and the full keyboard shortcut set (navigation, edit, mode, simulation, help). **Scope
excludes:** the semantics of the individual commands (documented in their own epics).

**Acceptance criteria (epic level):**
- MUST: A function can be activated by right‑clicking its target, by the menu bar, and (where one
  exists) by a keyboard shortcut, with equivalent results.
- MUST: The documented shortcut set behaves as specified for navigation, editing, mode switching,
  simulation and help.

**Readiness:** Ready.

---

## US-044 — Activate functions via right‑click, menu, or shortcut

**As an** IHC installer, **I want** three equivalent ways to trigger a function — context menu, menu
bar, and shortcut — **so that** I can work whichever way suits the moment.

### Acceptance criteria (Checklist)

- [ ] MUST: **Right‑click on a node** opens a context menu of the actions valid for that node (e.g.
  right‑click a locality to insert a product); `Shift+F10` opens the same context menu for the selected
  node without the mouse.
- [ ] MUST: The **menu bar** offers the same actions (e.g. *Insert > Products > …* mirrors the
  right‑click insertion); `F10` activates the menu bar at *File*, after which the arrow keys navigate
  it.
- [ ] MUST: **Keyboard shortcuts** trigger functions directly (e.g. `Ctrl+S` to save); the app’s
  guidance presents the "most obvious" method first and the alternative(s) in brackets.
- [ ] MUST: `F1` shows help text for the selected element; `F2` shows the properties of the selected
  element.
- [ ] SHOULD: The three routes are genuinely equivalent — IHC OpenVisual must not implement an action in
  one route only (e.g. an insertion available on right‑click must also exist under *Insert* and, where
  documented, on a shortcut).

### AC illustrations

- Saving a document is offered as *File > Save* first, with `[Ctrl+S]` shown as the alternative.
- A locality product insertion is reachable by right‑click the locality **and** via
  *Insert > Products > Wired products > … > <product>*.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** The three-route equivalence is architecturally guaranteed: every action
binds to a single view-model command, and the **context menus** (TreeView-level, gated on `SelectedNode`), the **menu
bar** (Insert / Library / Documentation / Controller / Help), and the **keyboard shortcuts** all invoke that same
command — so no action lives in only one route (e.g. product insertion is on the locality's right-click menu *and*
under *Insert ▸ Products*). Added this turn to close the gaps: **`Shift+F10`** opens the selected node's context menu
without the mouse (code-behind); **`F1`** shows help for the selected element (its note, or a generic message) — wired
to a keybinding and a *Help ▸ Help for selection* menu item; **`F2`** already opens Properties. `F10` activates the menu
bar via Avalonia's native menu access-key handling. Tests: the F1 help command (note vs generic) is covered in
`MainWindowViewModelTests`. Suites: `safe_visual_tests` **163**, byte-fidelity `safe_project_tests` **663** green.
OpenObserve 0 errors.

---

## US-045 — Navigate and edit the tree with the keyboard

**As an** IHC installer, **I want** a complete, predictable set of keyboard shortcuts, **so that** I
can navigate, edit, switch modes, and simulate without reaching for the mouse.

### Acceptance criteria (Checklist)

The following shortcuts MUST behave as specified (grouped for readability; all are single acceptance
conditions):

- [ ] MUST — **Help & properties:** `F1` help for selected element; `F2` properties of selected element.
- [ ] MUST — **Function blocks:** `F3` show the selected block’s program; `F4` jump to the opposite end
  of a link; `Ctrl+G` save a function block; `Ctrl+Shift+B` insert an empty function block.
- [ ] MUST — **Project & app:** `Ctrl+N` new project; `Ctrl+O` open project; `Ctrl+S` save project;
  `F5` send project; `Alt+F4` quit IHC OpenVisual.
- [ ] MUST — **Windows/menus:** `F6` switch between the two windows; `F10` activate the menu bar at
  *File*; `Shift+F10` context menu for the selected element.
- [ ] MUST — **Edit clipboard/undo:** `Ctrl+Z` undo; `Ctrl+Y` redo; `Ctrl+X` cut; `Ctrl+C` copy;
  `Ctrl+V` paste; `Delete` delete selected; `Ctrl+I` insert input; `Ctrl+U` insert output.
- [ ] MUST — **Simulation** *(documents the simulation shortcuts; the simulation feature itself is out of
  scope — see E8 — so these bindings are specified for completeness, not for implementation):* `F8` start
  simulation; `F7` end simulation; `F9` step (line‑by‑line); `Esc` return to configuration mode;
  `Ctrl+E` simulation time/date dialog; `Ctrl+L` toggle the simulation log; `Ctrl+M` insert/remove a log
  mark; `Break` insert/remove a breakpoint; `Space` = *follow* (element ON while held); `Ctrl+Space` =
  *toggle* the selected input/output.
- [ ] MUST — **Tree navigation:** `Up`/`Down` move the selection one line; `Left`/`Right` expand or
  collapse the selected element (see note).

### AC illustrations

- With a function block selected, `F3` opens its program (programming mode); `Esc` returns to
  configuration mode.
- During simulation, `Ctrl+Space` on a selected input toggles it and `Space` holds it ON only while
  pressed.

### Constraints

- **Open discrepancy to resolve (do not implement blindly):** the keyboard shortcut set specifies
  *Arrow left* (Left) **expands** and *Arrow right* (Right) **collapses** a node, while glossing each as
  "like opening/closing a folder in Windows Explorer" — which is the opposite direction. Windows
  Explorer convention is Right = expand, Left = collapse. IHC OpenVisual should verify the app’s actual
  behaviour and follow the platform convention unless the app genuinely reverses it. (R‑note — the
  guidance is internally contradictory on arrow‑key direction.)

**Readiness:** Ready. (The arrow‑key direction is flagged above as a contradiction for the team
to resolve during implementation; it does not block the rest of the shortcut set.)

**Implementation status:** ✅ **Implemented (in-scope shortcuts; clipboard/undo bindings land with their own epics;
simulation bindings out of scope per E8).** The shortcut set now bound:
- **Help & properties:** `F1` help for the selected element; `F2` properties.
- **Function blocks:** `F3` show the block's program; `F4` jump to a link's opposite end; `Ctrl+G` save a block;
  `Ctrl+Shift+B` insert an empty block.
- **Project & app:** `Ctrl+N`/`Ctrl+O`/`Ctrl+S` new/open/save; `F5` send project; `Alt+F4` quit.
- **Windows/menus:** `F6` switch between the two tree panes (code-behind); `F10` menu bar (Avalonia native);
  `Shift+F10` context menu for the selected element.
- **Edit — insert:** `Ctrl+I` insert an input, `Ctrl+U` insert an output into the programming block's sections.
- **Tree navigation:** `Up`/`Down` move the selection and `Left`/`Right` collapse/expand use the TreeView's **native**
  behaviour — resolving the R-note discrepancy in favour of the **platform convention** (Right = expand, Left =
  collapse), which the app does not reverse.

**Deferred to their own epics** (as the epic scope states — E11 excludes the individual command semantics): `Ctrl+Z`
undo / `Ctrl+Y` redo (E14, US-052) and `Ctrl+X`/`Ctrl+C`/`Ctrl+V` cut/copy/paste (E15, US-053) — their key bindings are
wired when those commands exist. The **simulation** bindings (`F8`/`F7`/`F9`/`Ctrl+E`/`Ctrl+L`/`Ctrl+M`/`Break`/`Space`/
`Ctrl+Space`) are documented-only — the simulation feature is out of scope (E8). Tests: `Ctrl+I`/`Ctrl+U` insert-input/
output (in and outside programming mode) + `F1` help in `MainWindowViewModelTests`. Suites: `safe_visual_tests` **163**,
byte-fidelity `safe_project_tests` **663** green. OpenObserve 0 errors.
