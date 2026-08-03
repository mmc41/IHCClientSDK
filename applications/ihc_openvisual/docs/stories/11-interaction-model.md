---
version: 0.6.0
last-updated: 2026-08-03
status: draft
---

# E11 — Interaction & activation model

> **Scope:** In scope (foundational, cross-cutting) — the activation and keyboard model underpins every
> CRUD interaction across E1–E10.

**Goal:** Give every IHC OpenVisual user a consistent way to invoke functions — by right-click, menu bar,
double-click, keyboard shortcut **or drag-and-drop** — and to navigate/edit the trees with the keyboard, so
the same command is reachable several ways and power users can work quickly. **Drag-and-drop is the primary
gesture** for creating links (US-022/US-023), moving and reordering nodes (US-054/US-055) and
building programs (US-028); the keyboard and menu routes are **supplements** that keep every such command
reachable without the mouse (route-parity).

**Scope:** the activation methods and their equivalence; the context-menu / menu-bar / shortcut
conventions; **what double-clicking a node does, per node type**; **what each node type's context menu
contains**; the **modal-dialog keyboard conventions** (dismissal and default focus); the **preservation of
the tree's expand/collapse state across edits** (US-070); and the full keyboard shortcut set (navigation,
edit, mode, simulation, help). **Scope excludes:** the semantics of the individual
commands (documented in their own epics) and the *content* of the properties dialogs each route opens
(E2–E7).

**Acceptance criteria (epic level):**
- MUST: A function can be activated by right-clicking its target, by the menu bar, and (where one
  exists) by a keyboard shortcut, with equivalent results.
- MUST: The documented shortcut set behaves as specified for navigation, editing, mode switching,
  simulation and help.
- MUST: Double-clicking a node opens that node's properties, and does not also expand or collapse it
  (US-067).
- MUST: A node's context menu offers the actions valid for **that node type** — not a single generic list —
  and includes the clipboard commands (US-068).
- MUST: Every modal dialog can be dismissed from the keyboard, and a destructive confirmation defaults to
  its safe option (US-069).
- MUST: A project edit preserves the tree's expand/collapse state — only navigation or a mode switch moves
  the tree, never a mutation (US-070).

**Readiness:** Ready.

---

## US-044 — Activate functions via right-click, menu, or shortcut

**As an** IHC installer, **I want** three equivalent ways to trigger a function — context menu, menu
bar, and shortcut — **so that** I can work whichever way suits the moment.

### Acceptance criteria (Checklist)

- MUST: **Right-click on a node** opens a context menu of the actions valid for that node (e.g.
  right-click a locality to insert a product); `Shift+F10` opens the same context menu for the selected
  node without the mouse. **Which actions are valid per node type is specified in US-068.**
- MUST: The **menu bar** offers the same actions (e.g. *Insert > Products > …* mirrors the
  right-click insertion); `F10` activates the menu bar at *File*, after which the arrow keys navigate
  it.
- MUST: The **menu bar is not filtered by which pane has focus**, and not by what is selected — every
  command stays **present** at all times. But a command that **cannot apply to the current selection is
  disabled (greyed)**, never enabled-and-refusing: the menu must not promise more than it delivers, and
  enablement updates as the selection changes. The two surfaces stay deliberately different: the
  context menu answers *"what can I do to this?"* by **omitting** the irrelevant (US-068); the menu bar
  answers *"what can this app do?"* by showing everything and **greying** the inapplicable. The pane
  split is a **context-menu rule only**; it is not carried into the menu bar.
- MUST: **Keyboard shortcuts** trigger functions directly (e.g. `Ctrl+S` to save); the app's
  guidance presents the "most obvious" method first and the alternative(s) in brackets.
- MUST: `F1` shows help text for the selected element; `F2` shows the properties of the selected
  element. **Double-click is a fourth route to the same properties** — see US-067.
- MUST: The routes are genuinely equivalent — IHC OpenVisual must not implement an action in
  one route only (e.g. an insertion available on right-click must also exist under *Insert* and, where
  documented, on a shortcut). **This applies to the clipboard commands too:** *Cut*, *Copy* and *Paste*
  MUST be reachable from a node's **context menu**, not only from the toolbar and `Ctrl+X`/`Ctrl+C`/`Ctrl+V`
  (US-068 fixes the inventory).

### Business rules (menu-bar enablement — what greys, and when)

- MUST: With the **localities root** selected, the bar greys everything that cannot apply to it:
  *Cut*, *Copy*, *Paste*, *Properties*, *Configuration view*, *Show program*, *Jump to opposite link*
  and *Empty function block* are all disabled.
- MUST: *Insert > Locality* is disabled whenever the selection is a locality's content (a product, a
  function block or a pin) — a locality can only be inserted at the root level (US-008).
- MUST: **A locked function block is not a bar-enablement discriminator.** *Cut*, *Copy*, *Delete* and
  *Show program* are **enabled** on a locked block in the **menu bar** exactly as they are in its context
  menu — the two surfaces give the same answer, on every project. The lock governs what may be changed
  **inside** the block (US-020, US-026), not whether the block itself can be cut, copied, deleted or read.
  Any rule that greys these four on the bar because the block is locked is wrong and must not be
  reintroduced.
- MUST: In the bar, *Copy* is enabled on **any pin** even where *Cut* is not — Copy reaches strictly
  further than Cut (a pin can be duplicated with its product, never cut out of it).
- MUST: The menu bar and the context menu apply **different, independently specified enablement
  rules** where the surfaces genuinely differ (US-068 lists the context side): *Show program* needs a
  **block selected directly** in the bar, while the flyout also accepts a **pin** and opens the owning
  block's program; and *Copy* is bar-enabled on any pin but context-offered on product terminals only.
  Each surface reproduces its own rule — they are not to be "reconciled" into one. **Lockedness is not
  one of these divergences** — see the locked-block rule above.
- MUST: **Keyboard shortcuts follow the menu bar's enablement.** Where the two surfaces deliberately
  diverge (previous rule), the shortcut refuses exactly when the bar item is greyed — `F3` opens a
  program only where the bar enables *Show program* (a block selected directly, **locked or not**),
  while the flyout keeps the pin route as well. Because the bar enables *Cut* and *Delete* on a locked
  block, `Ctrl+X` stages the cut and `Delete` removes it, matching the flyout. A command whose shortcut
  has no menu-bar item (`Ctrl+I` / `Ctrl+U`, US-045) is governed by its own availability rule.
- MUST: **A refused shortcut explains itself in the status bar.** Pressing the shortcut of a command
  that is currently unavailable leaves the project unchanged and shows the reason as the status-bar
  hint (e.g. `Nothing to undo.`, `Select a function block in the tree.`).

### AC illustrations

- Saving a document is offered as *File > Save* first, with `[Ctrl+S]` shown as the alternative.
- A locality product insertion is reachable by right-click the locality **and** via
  *Insert > Products > Wired products > … > <product>*.
- Copying a product is reachable by right-click > *Copy*, by the toolbar *Copy* button, and by `Ctrl+C` —
  all three name the copied product in the status bar.
- Selecting the localities root and opening *Edit* shows *Undo*/*Redo* enabled (history permitting)
  and *Cut*, *Copy*, *Paste* and *Properties* greyed; selecting a product re-enables them.
- Selecting a **locked** library block and opening *Edit* shows *Cut*, *Copy* and *Delete* enabled, and
  *View* shows *Show program* enabled — the same four the block's own right-click menu offers. Selecting
  an unlocked block in the same pane shows exactly the same four enabled: the lock makes no difference to
  this set on either surface.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the double-click route exists, *Cut*/*Copy*/*Paste* are
reachable from the node context menu, and the menu bar greys selection-dependent commands per the
enablement rules above (including the deliberate bar-vs-context differences). The status-bar explanation
for refused shortcuts is in place, and shortcuts now follow the **bar's** availability on every route —
including `Delete`, which the trees service themselves. The locked-block bar rule this story previously
carried (bar greys *Cut*/*Delete*/*Show program*) was **retired**: re-measurement showed the two surfaces
agree, and all four commands are enabled on the bar for a locked block.

---

## US-045 — Navigate and edit the tree with the keyboard

**As an** IHC installer, **I want** a complete, predictable set of keyboard shortcuts, **so that** I
can navigate, edit, switch modes, and simulate without reaching for the mouse.

### Acceptance criteria (Checklist)

The following shortcuts MUST behave as specified (grouped for readability; all are single acceptance
conditions):

- MUST — **Help & properties:** `F1` help for selected element; `F2` properties of selected element.
- MUST — **Function blocks:** `F3` show the selected block's program; `F4` jump to the opposite end
  of a link; `Ctrl+G` save a function block; `Ctrl+Shift+B` insert an empty function block.
- MUST — **Project & app:** `Ctrl+N` new project; `Ctrl+O` open project; `Ctrl+S` save project;
  `F5` send project; `Alt+F4` quit IHC OpenVisual.
- MUST — **Windows/menus:** `F6` switch between the two windows; `F10` activate the menu bar at
  *File*; `Shift+F10` context menu for the selected element.
- MUST — **Edit clipboard/undo:** `Ctrl+Z` undo; `Ctrl+Y` redo; `Ctrl+X` cut; `Ctrl+C` copy;
  `Ctrl+V` paste; `Delete` delete selected; `Ctrl+I` insert input; `Ctrl+U` insert output.
- MUST — **Simulation** *(documents the simulation shortcuts; the simulation feature itself is out of
  scope — see E8 — so these bindings are specified for completeness, not for implementation):* `F8` start
  simulation; `F7` end simulation; `F9` step (line-by-line); `Esc` return to configuration mode;
  `Ctrl+E` simulation time/date dialog; `Ctrl+L` toggle the simulation log; `Ctrl+M` insert/remove a log
  mark; `Break` insert/remove a breakpoint; `Space` = *follow* (element ON while held); `Ctrl+Space` =
  *toggle* the selected input/output.
- MUST — **Tree navigation:** `Up`/`Down` move the selection one line; the `Left`/`Right` arrows follow
  the Windows Explorer convention in all four cases — `Right` on a **collapsed** node expands it and leaves
  the caret in place; `Right` on an **expanded** node moves the caret to its first child; `Left` on an
  **expanded** node collapses it and leaves the caret in place; `Left` on a **collapsed** node moves the
  caret to its parent.
- MUST — **Home/End:** `Home` moves the selection to the focused tree's first row (its root); `End` to
  its last **visible** row — `End` walks the last-child chain only through **expanded** nodes, so a
  collapsed node's children are unreachable by it. Both keys act only on the focused tree and keep
  their normal text-editing meaning inside a text field.
- MAY: IHC OpenVisual adds `Ctrl+Shift+Up` / `Ctrl+Shift+Down` (*Move up* / *Move down*) — a deliberate
  addition, consistent with the non-drag reorder supplement (US-055/US-068).

### AC illustrations

- With a function block selected, `F3` opens its program (programming mode); `Esc` returns to
  configuration mode.
- `Right` on the collapsed locality `Living room` expands it with `Living room` still selected; pressing
  `Right` again moves the selection to its first product. `Left` then collapses it again, and a further
  `Left` selects the `Localities` root.
- With every locality collapsed, `End` selects the last locality (e.g. `Udendørs`), not a product
  hidden inside it; `Home` returns to the root row.
- During simulation, `Ctrl+Space` on a selected input toggles it and `Space` holds it ON only while
  pressed.

### Constraints

- Verification method — **Demonstration** of each in-scope binding, and **Test** of the four arrow-key
  quadrants against the Explorer convention.
- `Esc` is bound here to leaving programming mode; its dialog-dismissal role is specified in US-069.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (in-scope shortcuts) — **`F6` now swaps keyboard focus** between the two
tree panes (and back), preserving each pane's selection. The arrow-key quadrants, clipboard shortcuts, and undo/redo
are implemented, and the history is unlimited (E14/US-052). The **simulation** half is out of scope (E8) — specified
for completeness, not for implementation.

---

## US-067 — Open a node's properties by double-clicking it

**As an** IHC installer, **I want** double-clicking a node to open that node's properties, **so that** I can
edit what I just pointed at without reaching for `F2` or the context menu, and without the tree shifting
under me.

**Scope excludes:** the *content* of each properties dialog (owned by E2–E7); single-click selection; and
the expander caret, which continues to toggle expansion when clicked directly.

### Acceptance criteria (Business Rules)

**Activation rules:**
- MUST: Double-clicking a node opens the properties dialog listed for its type in the table below — the
  **same** dialog the `F2` and right-click > *Properties* routes already open (US-044 route equivalence).
- MUST: Double-clicking a node **does not** expand or collapse it. The gesture is handled by the
  application, which suppresses the toolkit's expand-toggle default; a node's expansion state is
  unchanged by opening (and cancelling) its dialog.
- MUST: Double-clicking a node type whose cell reads *nothing* leaves the application unchanged — it opens
  no dialog **and** does not toggle expansion.
- MUST: Double-clicking the **expander caret** itself continues to toggle expansion; only the node's own
  header strip activates it.

**The per-node-type matrix:**

| Node type | Double-click opens |
|---|---|
| Installation root (*Localities*) | **nothing** |
| Locality | its *Edit `<name>` properties* dialog (Name + Note — US-007) |
| Product (wired or wireless) | the product properties dialog, titled with the **product type** (US-011) |
| Product pin (input or output) | **its parent product's** dialog — *not* a pin-specific one |
| Link row | **nothing** |
| Scene container (*Scenarier*) | the scene dialog (name read-only, note, and the scene table; OK only) |
| Function block | the function-block properties dialog |

**Output:**
- Every node type has a defined, predictable double-click outcome, and no node type both opens a dialog and
  moves the tree.

### AC illustrations

- Double-clicking the locality `Entré/Gang` opens *Edit Entré/Gang properties*; cancelling it leaves
  `Entré/Gang` **still collapsed** — the gesture that opened the dialog did not also expand it.
- Double-clicking the output pin `Udgang` under `Lampeudtag` opens **`Lampeudtag`'s** dialog, not a dialog
  for the pin.
- Double-clicking a link row does nothing at all: no dialog, and the row's parent does not collapse.

### Constraints

- Verification method — **Test**: one case per node type asserting both halves — that the expected dialog
  opens (or no dialog does), **and** that the node's expansion state is unchanged.
- This is largely a **routing** requirement, not new UI: the properties dialogs already exist and `F2`
  already opens the locality one.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the per-node-type matrix is live, the toggle is suppressed, and
the scene-container dialog the matrix needed exists.

---

## US-068 — Offer a context menu tailored to the node type

**As an** IHC installer, **I want** a node's right-click menu to list only the commands that make sense for
that node, **so that** I can act on what I clicked without reading past irrelevant entries or hunting the
toolbar for a command the menu omits.

**Scope excludes:** the semantics of the individual commands (their own epics); the double-click route
(US-067); the menu-bar inventory (US-001, US-044).

### Acceptance criteria (Business Rules)

**Inventory rules:**
- MUST: A node's context menu contains the commands valid for **its own node type** — the menu is not one
  generic list reused across node types. In particular, a command that cannot apply to the clicked node
  (e.g. *Insert product* on a link row, or on a **pin**) is **absent**, not merely disabled.
- MUST: The **insert vocabulary is split by pane**: *Insert product* appears on a locality in the
  *Installation* pane only; *Insert function block* and *Empty function block* on a locality in the
  *Functions* pane only. Each appears in exactly one pane — mirroring the split the **tree itself already
  makes**, which shows products on the left and blocks on the right.
- MUST: **A pin is not a container and not a sibling.** A product's pins come from its catalog type, so a
  pin's menu offers **no insert command, no *Move up*/*Move down*, and no *Delete*.** (Deleting a pin would
  produce a product that contradicts its own catalog type — e.g. a six-button switch carrying five inputs,
  with the sixth button unaddressable and invisible in the tree.) An **engine guard** is required in
  addition to this menu gate, so a project written by any route stays conformant with its own catalog
  (US-053).
- MUST: *Cut*, *Copy* and *Paste* appear in the context menu of every node type that supports them
  (locality, product, function block), satisfying the US-044 route-parity MUST.
- MUST: *Paste* is shown **conditionally on clipboard state** — it is absent when the clipboard is empty and
  present when it holds a node. (A locality's menu is 6 items with an empty clipboard and 7 with a
  full one; the delta is exactly *Paste*.)
- MUST: A **link row**'s menu offers exactly two commands: *jump to the opposite end of the link* (US-025)
  and *Delete* (US-057). It offers no properties item.
- MUST: An **unlocked function block**'s menu includes a *show program* command — a context-menu route into
  programming mode (US-026).
- MUST: A **locked** function block's menu offers **both** *show program* **and** *Unlock* — they are
  **additive, not alternatives**. The locked menu is the unlocked one **plus** *Unlock* (8 items vs 7);
  *show program* is on both. ⇒ **A locked library block's program CAN be opened for reading**: the lock
  gates *editing*, never *viewing*, and no unlock is needed to read a block's program.
- MUST: **Inside a *locked* block's program (programming mode), every program node is view-only.** Its context
  menu offers *Properties* (Egenskaber) only; **Delete and *Move up*/*Move down* are removed** — the locked-block program menu is fully view-only (see `07-fb-programming.md`, US-026).
- MUST: A **product pin**'s menu offers a **log mark** toggle — the command behind the `Log …` state rows
  US-010 renders. (This is a missing feature, not just a missing menu entry; IHC OpenVisual offers no
  equivalent anywhere today.)
- MUST: A **product terminal**'s menu offers *Copy* (while *Cut* is never offered on a pin); a
  **function-block pin**'s menu offers **no** *Copy*. The context menu's Copy scope (product terminals
  only) is deliberately **narrower than the menu bar's** (any pin, US-044) — each surface keeps its own
  rule.
- MUST: A **locked** function block's flyout offers *Cut* and *Delete* — and they really run. So does the
  menu bar: the two surfaces **agree** on a locked block, for *Cut*, *Copy*, *Delete* and *Show program*
  alike (US-044). *Show program* is additionally offered from a **pin** in the flyout (opening the owning
  block's program, US-026), where the bar requires a block selected directly — that one, and *Copy*'s
  narrower context scope above, are the real bar-vs-context differences and are specified behaviour, not
  inconsistencies to fix.
- MUST: A **scene container**'s menu offers *Copy*.
- SHOULD: *Move up* / *Move down* remain on the node types that can be reordered (locality, product,
  function block) and are **absent** from a link row **and from a pin**. They are IHC OpenVisual's non-drag
  supplement to drag reorder (US-055) and are kept — but only on reorderable nodes.

**Target inventories** (the user stories are the authoritative spec; IHC OpenVisual's wording is its own
English — the *language* of a label is an allowed difference, the *inventory* is not).

**A locality's menu depends on which pane it is in. Every other node type's does not.**

| Node type | Pane | Commands |
|---|---|---|
| Installation root | **both** (identical) | insert locality — **1 item** |
| Locality | *Installation* | **insert product** (submenu), Cut, Copy, Delete, separator, Properties — **6 items**; **+ Paste** when the clipboard is full |
| Locality | *Functions* | **insert function block** (submenu), Cut, Copy, Delete, **empty function block**, separator, Properties — **7 items** |
| Product | *Installation* | Cut, Copy, Delete, separator, Properties — **5 items** |
| Product pin (input or output) | *Installation* | **log mark**, **Copy**, separator, Properties — **4 items** |
| Scene container (*Scenarier*) | *Installation* | Copy, separator, Properties — **exactly 3 items** |
| Link row | either | jump to opposite end, Delete — **exactly 2 items** |
| Function block (unlocked) | *Functions* | Save block…, Cut, Copy, Delete, **show program**, separator, Properties — **7 items** |
| Function block (locked) | *Functions* | Save block…, Cut, Copy, **Unlock**, Delete, **show program**, separator, Properties — **8 items** — the unlocked row **plus** *Unlock* |

**Output:**
- Every node type's right-click menu is a valid, minimal command set for that node, and no command is
  reachable by toolbar or shortcut alone.

### AC illustrations

- Right-clicking a link row offers exactly *jump to the opposite end* and *Delete* — not the seven generic
  items, and not *Insert product*.
- Right-clicking a locality with an empty clipboard offers no *Paste*; copying a product first and
  right-clicking the same locality now offers *Paste*.

### Constraints

- Verification method — **Test**: one inventory assertion per node type **× pane**. **The pane axis is the
  part that matters.** Include the **clipboard-state-dependent** *Paste* item (which needs a test that copies
  something first) and a case asserting that a **pin** offers no *Delete*.
- **The gate to replace is a pane-blind, type-blind "can this node be edited?" condition.** That single
  condition is why the same list appears on a locality in both panes *and* on a pin — the two defects have
  one cause, and the pin case is not a cosmetic one.

**Readiness:** Ready.

**Implementation status:** 🟡 Largely implemented — the per-node-kind inventories (room, product, product
pin, function block, function-block pin), the flyout ordering, *Copy* on product terminals, the
locked-block flyout offering *Cut*/*Delete*/*Show program*, and *Show program* from a pin (resolving the
owning block) are all in place, including the two surviving bar-vs-context enablement differences (US-044).
The locked block is no longer one of them — the bar was brought into line with the flyout on all four
commands.
One item still needs **owner confirmation**: the exact **log-mark scope** — whether a per-pin log-mark
command must exist for loggable **value** resources (e.g. a temperature sensor), where a boolean pin's
equivalent is inert; today the toggle is offered wherever a `Logning` log row is projected.

---

## US-069 — Dismiss and default dialogs from the keyboard

**As an** IHC installer, **I want** every dialog to close on `Esc` and every destructive confirmation to
start on its safe answer, **so that** I can back out of a prompt with the keyboard and never destroy work by
reflexively pressing Enter.

**Scope excludes:** the *content* and field set of the individual dialogs (their own epics); which actions
raise a confirmation at all (US-009, US-053).

### Acceptance criteria (Checklist)

- MUST: Pressing `Esc` dismisses **any** modal dialog, taking the negative/cancelling outcome — the
  same result as clicking *Cancel* / *No*. This includes confirmation dialogs, not only editing dialogs.
  (Standard caveat: if a field's combo/dropdown is open, the first `Esc` closes that popup and the next
  closes the dialog.)
- MUST: A dialog that confirms a **destructive** action (delete, cascade, discard) opens with its
  **negative** button focused, so `Enter` cancels rather than destroys. IHC OpenVisual
  MUST default to the safe option.
- MUST: Every modal dialog opens with keyboard focus on one of its own controls — never on the dialog
  window itself, which leaves the dialog with no `Enter` default at all.
- SHOULD: A non-destructive dialog (an editing/properties dialog) opens with focus on its first editable
  field, and `Enter` accepts it.

### AC illustrations

- Deleting a product raises the confirmation with **No** focused; pressing `Enter` cancels the delete and
  the product survives, and pressing `Esc` does the same.
- The *Edit `<name>` properties* dialog opens with the *Name* field focused and selected; `Esc` closes it
  and discards the edit (US-007).

### Constraints

- Verification method — **Test**: assert the negative button holds focus when a destructive confirm opens,
  and that `Esc` closes it, on **every** confirm dialog — not only *Delete*.
- **This story fixes the guard's ergonomics; it never removes a guard.** IHC OpenVisual deliberately
  confirms these actions (US-053, US-056) — those confirmations **stay**. What is
  wrong today is that the confirmation cannot be answered without the mouse, which is an accessibility
  defect.

**Readiness:** Ready.

**Implementation status:** ⛔ Not implemented for confirmations — the *Delete* confirm ignores `Esc` and
focuses neither button, leaving focus on the dialog window itself.

---

## US-070 — An edit keeps the tree's expansion state

**As an** IHC installer, **I want** the tree to stay expanded exactly as I left it when I edit the project,
**so that** I keep my place while wiring — an edit changes *what* I edited, not *where I am* in the tree.

> **Sibling of US-067.** US-067 says opening a node's *dialog* must not move the tree under you; this says the
> same of an *edit*. Together they are one principle: **only a deliberate navigation or mode change moves the
> tree — never a mutation.**

**Scope excludes:** which gestures cause an edit (their own epics); the *selection* an edit may reset (not
specified here); and the deliberate reveal defaults below, which are kept.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Connecting two pins keeps the branches open
  Given I have expanded a product and a function block down to their pins in order to wire them
  When I connect two pins (by dragging one onto the other, or with the two-step Link supplement)
  Then the product, the block and its section stay expanded exactly as they were
  And I am not thrown back to a collapsed tree

Scenario: Deleting a link keeps the branches open
  Given I have expanded the branches around a link
  When I delete the link (US-057)
  Then the surrounding branches stay expanded

Scenario: Any edit preserves each surviving node's state
  Given nodes I have expanded and nodes I have deliberately collapsed
  When I make any project edit (insert, delete, move, reorder, rename, link, program-build, undo/redo)
  Then every node that still exists keeps the expand/collapse state I gave it
  And a node I deliberately collapsed is not forced back open

Scenario: A node revealing its first child still opens (US-006 kept)
  Given an empty, collapsed locality
  When I insert the first product into it
  Then the locality opens to reveal the new product — its reveal default wins, it had no state to keep

Scenario: A mode switch opens fresh
  Given I am in configuration mode
  When I enter a block's programming mode (or leave it)
  Then that view opens at its own defaults (the program fully expanded), not carried over from the other mode
```

### Business rules

- MUST: A project mutation preserves each **surviving** node's expand/collapse state — keyed by the node's
  stable element id, and **per pane** (the same locality appears in both panes with independent expansion).
- MUST: A node the edit **creates** takes its build-time default; a node the edit **removes** drops out.
  Preservation applies to the nodes that persist across the edit.
- MUST: The **reveal-on-first-child** default (US-006 — a locality with contents opens) is kept: a node that
  gains its *first* child opens by default rather than inheriting a stale collapsed state.
- MUST: After a successful **drag-drop**, the **drop-target row is left expanded together with its
  entire subtree**, and that expansion **persists** — it is a drop rule making the landing place
  visible, not a hover artifact. The keyboard reorder supplement (*Move up*/*Move down*, US-055) does
  **not** touch expansion state.
- MUST: A **pasted** subtree (US-056) and a **freshly placed product** (US-010) are revealed fully
  expanded — an arrival the installer caused is made visible rather than landing as one collapsed row.
- MUST: A deliberate **mode switch** (configuration ⇄ a block's programming view) is **not** an in-place edit
  — it opens the target view at its defaults, not carried over from the other mode.
- SHOULD: The state survives across the **binding**, so a node the installer expanded *in the UI* (not only
  one expanded programmatically) is preserved.

### AC illustrations

- Expanding `Living room` ▸ `Lampeudtag` ▸ its output pin, then dragging that pin onto a block input, leaves
  `Living room`, `Lampeudtag` and the block's `Input` section **still open** — the link appears in place.
- Collapsing a locality that has products, then inserting a locality elsewhere, leaves the first locality
  **still collapsed** — the rebuild restores state, it does not re-apply defaults.

### Constraints

- Verification method — **Test**: an expanded node survives a link and a link-deletion, a collapsed node
  survives an unrelated edit, a first child still reveals its parent, a mode switch opens fresh, and a
  UI-driven expansion survives an edit.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — expansion is snapshotted (per pane, by element id, for nodes that
have children) before each rebuild and restored after, guarded by a view key so a mode switch opens fresh.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-044 | Activate functions via right-click, menu, or shortcut | Ready | E11 | Must | -- |
| US-045 | Navigate and edit the tree with the keyboard | Ready | E11 | Must | -- |
| US-067 | Open a node's properties by double-clicking it | Ready | E11 | Must | US-007, US-011, US-012, US-044 |
| US-068 | Offer a context menu tailored to the node type | Ready | E11 | Must | US-025, US-026, US-044, US-053, US-055 |
| US-069 | Dismiss and default dialogs from the keyboard | Ready | E11 | Must | US-053 |
| US-070 | An edit keeps the tree's expansion state | Ready | E11 | Must | US-006, US-067 |
