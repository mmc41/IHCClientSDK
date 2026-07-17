---
version: 0.2.0
last-updated: 2026-07-16
status: draft
---

# E11 — Interaction & activation model

> **Current scope:** ✅ **In scope (foundational)** — the activation and keyboard model underpins every
> CRUD interaction.

**Goal:** Give every IHC OpenVisual user a consistent way to invoke functions — by right‑click, menu bar,
double‑click or keyboard shortcut — and to navigate/edit the trees with the keyboard, so the same command is
reachable several ways and power users can work quickly. This epic is cross‑cutting: it underlies every
capability area (E1–E10).

**Scope:** the activation methods and their equivalence; the context‑menu / menu‑bar / shortcut
conventions; **what double‑clicking a node does, per node type**; **what each node type's context menu
contains**; the **modal‑dialog keyboard conventions** (dismissal and default focus); and the full keyboard
shortcut set (navigation, edit, mode, simulation, help). **Scope excludes:** the semantics of the individual
commands (documented in their own epics) and the *content* of the properties dialogs each route opens
(E2–E7).

**Acceptance criteria (epic level):**
- MUST: A function can be activated by right‑clicking its target, by the menu bar, and (where one
  exists) by a keyboard shortcut, with equivalent results.
- MUST: The documented shortcut set behaves as specified for navigation, editing, mode switching,
  simulation and help.
- MUST: Double‑clicking a node opens that node's properties, and does not also expand or collapse it
  (US-067).
- MUST: A node's context menu offers the actions valid for **that node type** — not a single generic list —
  and includes the clipboard commands (US-068).
- MUST: Every modal dialog can be dismissed from the keyboard, and a destructive confirmation defaults to
  its safe option (US-069).

**Readiness:** Ready.

> **Vendor‑alignment note (2026‑07‑16).** US-067, US-068 and US-069 were added, and US-045's arrow‑key
> constraint resolved, from the measured side‑by‑side comparison with IHC Visual — which is the
> authoritative spec for this epic. Before that measurement this epic specified activation *routes*
> (US-044/US-045) but never what a double‑click does, what each context menu contains, or how a dialog
> behaves under the keyboard. Evidence: `RESULTS.md` **F‑006**–**F‑013**, **F‑018**, **F‑024**; backlog
> **A‑4**, **A‑5**, **A‑9**, **A‑10**.

---

## US-044 — Activate functions via right‑click, menu, or shortcut

**As an** IHC installer, **I want** three equivalent ways to trigger a function — context menu, menu
bar, and shortcut — **so that** I can work whichever way suits the moment.

### Acceptance criteria (Checklist)

- [ ] MUST: **Right‑click on a node** opens a context menu of the actions valid for that node (e.g.
  right‑click a locality to insert a product); `Shift+F10` opens the same context menu for the selected
  node without the mouse. **Which actions are valid per node type is specified in US-068.**
- [ ] MUST: The **menu bar** offers the same actions (e.g. *Insert > Products > …* mirrors the
  right‑click insertion); `F10` activates the menu bar at *File*, after which the arrow keys navigate
  it.
- [ ] MUST: **Keyboard shortcuts** trigger functions directly (e.g. `Ctrl+S` to save); the app’s
  guidance presents the "most obvious" method first and the alternative(s) in brackets.
- [ ] MUST: `F1` shows help text for the selected element; `F2` shows the properties of the selected
  element. **Double‑click is a fourth route to the same properties** — see US-067.
- [ ] MUST: The routes are genuinely equivalent — IHC OpenVisual must not implement an action in
  one route only (e.g. an insertion available on right‑click must also exist under *Insert* and, where
  documented, on a shortcut). **This applies to the clipboard commands too:** *Cut*, *Copy* and *Paste*
  MUST be reachable from a node's **context menu**, not only from the toolbar and `Ctrl+X`/`Ctrl+C`/`Ctrl+V`
  (US-068 fixes the inventory).

  > **Tightened 2026‑07‑16 (was: SHOULD).** Route equivalence was a SHOULD, and the one place it is
  > actually broken is the clipboard: *Cut*/*Copy*/*Paste* exist on the toolbar and on `Ctrl+C` (the status
  > bar confirms *"Copied Lampeudtag"*) but appear in **no** context menu on **any** node type — while
  > IHC Visual offers `&Klip`/`&Kopier` on locality, product and function block. This is a **route gap, not
  > a missing feature**, which is exactly what this criterion exists to prevent — so it is raised to MUST
  > and made explicit rather than left to inference. Evidence: `RESULTS.md` **F‑009**; backlog **A‑5**.

### AC illustrations

- Saving a document is offered as *File > Save* first, with `[Ctrl+S]` shown as the alternative.
- A locality product insertion is reachable by right‑click the locality **and** via
  *Insert > Products > Wired products > … > <product>*.
- Copying a product is reachable by right‑click > *Copy*, by the toolbar *Copy* button, and by `Ctrl+C` —
  all three name the copied product in the status bar.

**Readiness:** Ready.

**Implementation status:** 🟡 Implemented — ⚠ **except the clipboard route parity**: *Cut*/*Copy*/*Paste*
are missing from every context menu (F‑009), and the properties route exists on `F2` but not on
double‑click (F‑006). Backlog **A‑5** and **A‑4** close these; the rules are specified in US-068 and
US-067.

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
- [ ] MUST — **Tree navigation:** `Up`/`Down` move the selection one line; the `Left`/`Right` arrows follow
  the Windows Explorer convention in all four cases — `Right` on a **collapsed** node expands it and leaves
  the caret in place; `Right` on an **expanded** node moves the caret to its first child; `Left` on an
  **expanded** node collapses it and leaves the caret in place; `Left` on a **collapsed** node moves the
  caret to its parent.

  > **Corrected 2026‑07‑16 (was: an unresolved "open discrepancy" telling the team not to implement the
  > arrow keys blindly).** The contradiction is settled by measurement, not by a decision: IHC Visual is
  > **plain Explorer in all four quadrants**, and IHC OpenVisual already matches it exactly — this
  > criterion is a **regression baseline**, not new work. The vendor's own CHM help *claims* the opposite
  > ("*Pil venstre … **ekspanderer** et markeret element*") while glossing it as "like opening a folder in
  > Windows Explorer" — it contradicts itself and its own app. That is a **vendor documentation defect**,
  > and per the ruling's exception #3 a vendor defect is not authoritative: do **not** spec the CHM's
  > claim. Evidence: `RESULTS.md` **F‑013** (measured live in all four quadrants on both apps);
  > `census.md` §G10/G11.

### AC illustrations

- With a function block selected, `F3` opens its program (programming mode); `Esc` returns to
  configuration mode.
- `Right` on the collapsed locality `Living room` expands it with `Living room` still selected; pressing
  `Right` again moves the selection to its first product. `Left` then collapses it again, and a further
  `Left` selects the `Localities` root.
- During simulation, `Ctrl+Space` on a selected input toggles it and `Space` holds it ON only while
  pressed.

### Constraints

- Verification method — **Demonstration** of each in‑scope binding, and **Test** of the four arrow‑key
  quadrants against the Explorer convention (the regression baseline established by F‑013).
- `Esc` is bound here to leaving programming mode; its dialog‑dismissal role is specified in US-069.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (in-scope shortcuts; clipboard/undo and simulation bindings
deferred). The arrow‑key quadrants are implemented and measured aligned (F‑013).

---

## US-067 — Open a node's properties by double‑clicking it

> **Added 2026‑07‑16** from the vendor comparison. IHC Visual handles double‑click on **every** node type;
> IHC OpenVisual has **no double‑click handler at all**, so the toolkit's default (toggle expand/collapse)
> fires instead. Evidence: `RESULTS.md` **F‑006**, **F‑007**; `census.md` §G2; backlog **A‑4**.

**As an** IHC installer, **I want** double‑clicking a node to open that node's properties, **so that** I can
edit what I just pointed at without reaching for `F2` or the context menu, and without the tree shifting
under me.

**Scope excludes:** the *content* of each properties dialog (owned by E2–E7); single‑click selection; and
the expander caret, which continues to toggle expansion when clicked directly.

### Acceptance criteria (Business Rules)

**Activation rules:**
- MUST: Double‑clicking a node opens the properties dialog listed for its type in the table below — the
  **same** dialog the `F2` and right‑click > *Properties* routes already open (US-044 route equivalence).
- MUST: Double‑clicking a node **does not** expand or collapse it. The gesture is handled by the
  application, which suppresses the toolkit's expand‑toggle default; a node's expansion state is
  unchanged by opening (and cancelling) its dialog.
- MUST: Double‑clicking a node type whose cell reads *nothing* leaves the application unchanged — it opens
  no dialog **and** does not toggle expansion.
- MUST: Double‑clicking the **expander caret** itself continues to toggle expansion; only the node's own
  header strip activates it.

**The per‑node‑type matrix:**

| Node type | Double‑click opens |
|---|---|
| Installation root (*Localities*) | **nothing** |
| Locality | its *Edit `<name>` properties* dialog (Name + Note — US-007) |
| Product (wired or wireless) | the product properties dialog, titled with the **product type** (US-011) |
| Product pin (input or output) | **its parent product's** dialog — *not* a pin‑specific one |
| Link row | **nothing** |
| Scene container (*Scenarier*) | the scene dialog (name read‑only, note, and the scene table; OK only) |
| Function block | the function‑block properties dialog |

**Output:**
- Every node type has a defined, predictable double‑click outcome, and no node type both opens a dialog and
  moves the tree.

### AC illustrations

- Double‑clicking the locality `Entré/Gang` opens *Edit Entré/Gang properties*; cancelling it leaves
  `Entré/Gang` **still collapsed** — the gesture that opened the dialog did not also expand it.
- Double‑clicking the output pin `Udgang` under `Lampeudtag` opens **`Lampeudtag`'s** dialog, not a dialog
  for the pin.
- Double‑clicking a link row does nothing at all: no dialog, and the row's parent does not collapse.

### Constraints

- Verification method — **Test** (headless UI, `safe_visual_tests`): one case per node type asserting both
  halves — that the expected dialog opens (or no dialog does), **and** that the node's expansion state is
  unchanged. The second half is what pins the toggle suppression, and it is a distinct user‑visible defect:
  even a user who never wants the dialog currently sees the tree move under them.
- This is largely a **routing** gap, not new UI: the properties dialogs already exist and `F2` already opens
  the locality one.

**Readiness:** Ready.

**Implementation status:** ⛔ **Not implemented** — IHC OpenVisual has no double‑click handler on any node
type; every expandable node toggles instead, and no node opens its properties. Backlog **A‑4** implements
this story.

---

## US-068 — Offer a context menu tailored to the node type

> **Added 2026‑07‑16** from the vendor comparison. IHC Visual tailors every context menu; IHC OpenVisual
> shows **one generic 7‑item menu** on locality, product **and** link row — offering *Insert product* on a
> link row, where it is meaningless. Evidence: `RESULTS.md` **F‑008**, **F‑009**, **F‑010**, **F‑011**;
> `census.md` §G3; backlog **A‑5**.

**As an** IHC installer, **I want** a node's right‑click menu to list only the commands that make sense for
that node, **so that** I can act on what I clicked without reading past irrelevant entries or hunting the
toolbar for a command the menu omits.

**Scope excludes:** the semantics of the individual commands (their own epics); the double‑click route
(US-067); the menu‑bar inventory (US-001, US-044).

### Acceptance criteria (Business Rules)

**Inventory rules:**
- MUST: A node's context menu contains the commands valid for **its own node type** — the menu is not one
  generic list reused across node types. In particular, a command that cannot apply to the clicked node
  (e.g. *Insert product* on a link row) is **absent**, not merely disabled.
- MUST: *Cut*, *Copy* and *Paste* appear in the context menu of every node type that supports them
  (locality, product, function block), satisfying the US-044 route‑parity MUST.
- MUST: *Paste* is shown **conditionally on clipboard state** — it is absent when the clipboard is empty and
  present when it holds a node. (IHC Visual's locality menu is 6 items with an empty clipboard and 7 with a
  full one; the delta is exactly `&Indsæt`.)
- MUST: A **link row**'s menu offers exactly two commands: *jump to the opposite end of the link* (US-025)
  and *Delete* (US-057). It offers no properties item.
- MUST: A **function block**'s menu includes a *show program* command — a context‑menu route into
  programming mode (US-026).
- SHOULD: *Move up* / *Move down* remain on the node types that can be reordered (locality, product,
  function block) and are **absent** from a link row.

  > **Deliberate addition, not a divergence to remove.** *Move up*/*Move down* have no counterpart in IHC
  > Visual, whose reorder gesture is a **drag**. They are IHC OpenVisual's non‑drag substitute and are
  > **kept** — US-055 requires at least one non‑drag reorder route (US-044). They simply do not belong on a
  > link row, which cannot be reordered.

**Target inventories** (IHC Visual's, as the authoritative spec; IHC OpenVisual's wording is its own
English — the *language* is an allowed difference, the *inventory* is not).

> ⚠ **These inventories are *Installation*-pane (TV1) samples — do not read them as pane-independent.**
> Every vendor menu below was dumped on **TV1** except the function block, which was dumped on **TV2**. The
> vendor's **TV2 locality** menu — the one place a *function-block* insert would live — **has never been
> dumped on either app**. So "the vendor's locality menu offers no function-block insert" is **not** an
> established fact; what is established is *"no function-block insert **on the left tree**"*.
>
> **This matters for exactly one rule:** do **not** conclude from this table that *Insert function block* /
> *Empty function block* should be removed from the locality menu outright — that would strip them from
> **both** panes on a one-pane sample. The live hypothesis is that the vendor **splits the insert vocabulary
> by pane** (products left, function blocks right), which is also what IHC OpenVisual's *tree* already does
> (`BuildTree` filters products to *Installation* and blocks to *Functions*) while its *menu* does not — it
> gates those two items on a pane-blind condition, so they appear on a locality in both panes.
>
> **TBD (pending capture):** whether the vendor's TV2 locality menu offers a function-block insert.
> `tmp\compare2.md` **C11** is that capture and rewrites this rule from the answer: if the split holds,
> **pane-gate** the two items (mirroring `BuildTree`); if TV2 offers no FB insert either, drop them from the
> context menu (the *Library* menu keeps the capability, and it is already vendor-aligned — F‑042).
> Evidence: `census.md` §G3 (per-pane attribution); `RESULTS.md` **F‑008**/**F‑011**.

| Node type | Commands |
|---|---|
| Installation root | insert locality — **1 item** *(already aligned — F‑016, regression baseline)* |
| Locality | insert product (submenu), Cut, Copy, Delete, separator, Properties — **6 items**; **+ Paste** when the clipboard is full |
| Product | Cut, Copy, Delete, separator, Properties — **5 items** |
| Link row | jump to opposite end, Delete — **exactly 2 items** |
| Function block | Save block…, Cut, Copy, Unlock, Delete, show program, separator, Properties — **8 items** |

**Output:**
- Every node type's right‑click menu is a valid, minimal command set for that node, and no command is
  reachable by toolbar or shortcut alone.

### AC illustrations

- Right‑clicking a link row offers exactly *jump to the opposite end* and *Delete* — not the seven generic
  items, and not *Insert product*.
- Right‑clicking a locality with an empty clipboard offers no *Paste*; copying a product first and
  right‑clicking the same locality now offers *Paste*.

### Constraints

- Verification method — **Test** (headless UI, `safe_visual_tests`): one inventory assertion per node type,
  including the **clipboard‑state‑dependent** *Paste* item — which needs a test that copies something first.
- The per‑node‑type mechanism already exists: the installation root's 1‑item menu is measured **aligned**
  today, so this story generalises a working mechanism rather than introducing one.
- Two node types' vendor inventories are **not yet measured** — the output pin and the scene container
  (`RESULTS.md` **E‑1**). IHC OpenVisual's current menus there (11 and 8 items) are not specified by this
  story and are left as they are pending that capture.

**Readiness:** Not Ready — the five measured node types are specified and unblocked; two points wait on a
capture, and neither blocks the rest:
- [R5] The **pane dimension** is **TBD (pending capture)** — whether the vendor splits the insert vocabulary
  between the panes, which decides only the locality menu's function‑block items (`compare2` **C11**).
- [R5] The **output‑pin (N5)** and **scene‑container (N10)** inventories have no vendor side to specify
  from (`RESULTS.md` **E‑1**; `compare2` **C1** captures them).

**Implementation status:** ⛔ **Not implemented** — IHC OpenVisual shows one generic 7‑item menu
(*Insert product, Insert function block, Empty function block, Move up, Move down, Delete, Properties*) on
locality, product and link row alike, with no clipboard commands anywhere and no jump/show‑program routes.
The root's 1‑item menu is the one aligned case. Backlog **A‑5** implements this story.

---

## US-069 — Dismiss and default dialogs from the keyboard

> **Added 2026‑07‑16** from the vendor comparison. IHC OpenVisual's *Delete* confirmation is
> **keyboard‑inert**: it ignores `Esc` **and** focuses no button, so a destructive dialog can only be
> answered with the mouse. Evidence: `RESULTS.md` **F‑018**, **F‑024**; backlog **A‑9**, **A‑10**.

**As an** IHC installer, **I want** every dialog to close on `Esc` and every destructive confirmation to
start on its safe answer, **so that** I can back out of a prompt with the keyboard and never destroy work by
reflexively pressing Enter.

**Scope excludes:** the *content* and field set of the individual dialogs (their own epics); which actions
raise a confirmation at all (US-009, US-053).

### Acceptance criteria (Checklist)

- [ ] MUST: Pressing `Esc` dismisses **any** modal dialog, taking the negative/cancelling outcome — the
  same result as clicking *Cancel* / *No*. This includes confirmation dialogs, not only editing dialogs.
- [ ] MUST: A dialog that confirms a **destructive** action (delete, cascade, discard) opens with its
  **negative** button focused, so `Enter` cancels rather than destroys. IHC Visual focuses `&No`; IHC
  OpenVisual MUST likewise default to the safe option.
- [ ] MUST: Every modal dialog opens with keyboard focus on one of its own controls — never on the dialog
  window itself, which leaves the dialog with no `Enter` default at all.
- [ ] SHOULD: A non‑destructive dialog (an editing/properties dialog) opens with focus on its first editable
  field, and `Enter` accepts it.

### AC illustrations

- Deleting a product raises the confirmation with **No** focused; pressing `Enter` cancels the delete and
  the product survives, and pressing `Esc` does the same.
- The *Edit `<name>` properties* dialog opens with the *Name* field focused and selected; `Esc` closes it
  and discards the edit (US-007).

### Constraints

- Verification method — **Test** (headless UI, `safe_visual_tests`): assert the negative button holds focus
  when a destructive confirm opens, and that `Esc` closes it, on **every** confirm dialog — not only
  *Delete*.
- **This story fixes the guard's ergonomics; it never removes a guard.** IHC OpenVisual deliberately
  confirms in places where IHC Visual is silent (US-053, US-056) — those confirmations **stay**, per the
  2026‑07‑16 ruling. What is wrong today is that the confirmation cannot be answered without the mouse,
  which is both a vendor‑parity gap and an accessibility defect.

**Readiness:** Ready.

**Implementation status:** ⛔ **Not implemented** for confirmations — the *Delete* confirm ignores `Esc`
(the modal stays open) and focuses **neither** button, leaving focus on the dialog window itself. Backlog
**A‑9** and **A‑10** implement this story.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-044 | Activate functions via right‑click, menu, or shortcut | Ready | E11 | Must | -- |
| US-045 | Navigate and edit the tree with the keyboard | Ready | E11 | Must | -- |
| US-067 | Open a node's properties by double‑clicking it | Ready | E11 | Must | US-007, US-011, US-044 |
| US-068 | Offer a context menu tailored to the node type | Ready | E11 | Must | US-025, US-026, US-044, US-055 |
| US-069 | Dismiss and default dialogs from the keyboard | Ready | E11 | Must | US-053 |
