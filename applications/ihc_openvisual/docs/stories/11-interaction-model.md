---
version: 0.3.0
last-updated: 2026-07-18
status: draft
---

# E11 — Interaction & activation model

> **Current scope:** ✅ **In scope (foundational)** — the activation and keyboard model underpins every
> CRUD interaction.

**Goal:** Give every IHC OpenVisual user a consistent way to invoke functions — by right‑click, menu bar,
double‑click, keyboard shortcut **or drag‑and‑drop** — and to navigate/edit the trees with the keyboard, so
the same command is reachable several ways and power users can work quickly. **Drag‑and‑drop is the primary,
vendor‑aligned gesture** for creating links (US-022/US-023), moving and reordering nodes (US-054/US-055) and
building programs (US-028); the keyboard and menu routes are **supplements** that keep every such command
reachable without the mouse (route‑parity), so installers fluent in IHC Visual meet no surprise. This epic is
cross‑cutting: it underlies every capability area (E1–E10).

**Scope:** the activation methods and their equivalence; the context‑menu / menu‑bar / shortcut
conventions; **what double‑clicking a node does, per node type**; **what each node type's context menu
contains**; the **modal‑dialog keyboard conventions** (dismissal and default focus); the **preservation of
the tree's expand/collapse state across edits** (US-070); and the full keyboard shortcut set (navigation,
edit, mode, simulation, help). **Scope excludes:** the semantics of the individual
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
- MUST: A project edit preserves the tree's expand/collapse state — only navigation or a mode switch moves
  the tree, never a mutation (US-070).

**Readiness:** Ready.

> **Vendor‑alignment note (2026‑07‑16, extended 2026‑07‑17).** US-067, US-068 and US-069 were added, and
> US-045's arrow‑key constraint resolved, from the measured side‑by‑side comparison with IHC Visual — which
> is the authoritative spec for this epic. Before that measurement this epic specified activation *routes*
> (US-044/US-045) but never what a double‑click does, what each context menu contains, or how a dialog
> behaves under the keyboard. Evidence: `RESULTS.md` **F‑006**–**F‑013**, **F‑018**, **F‑024**; backlog
> **A‑4**, **A‑5**, **A‑9**, **A‑10**.
>
> **2026‑07‑17: US-068's two open captures are closed and the epic is Ready.** The vendor's *Functions*-pane
> locality menu (**F‑048**) and its output‑pin / scene‑container menus (**F‑063**) were dumped, adding the
> **pane** and **pin** dimensions to US-068 — including the one context‑menu defect that reaches the file
> rather than the screen (**F‑067**, *Delete* on a catalog‑owned pin). US-067 also shipped in that window
> (**A‑4** — double‑click now opens properties without toggling).

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
- [ ] SHOULD: The **menu bar is not filtered by which pane has focus**, and not by what is selected. It
  offers the whole vocabulary at all times — unlike a context menu, which is tailored to the node clicked
  (US-068). The two are deliberately different surfaces: the context menu answers *"what can I do to
  this?"*, the menu bar answers *"what can this app do?"*.

  > **Added 2026‑07‑17 — recorded because the spec assumed neither branch, and US-068's pane rule invites
  > the wrong generalisation.** IHC Visual's *Insert* menu is **item‑for‑item identical with focus in either
  > pane, with nothing disabled**, and identical across a working, a mis‑targeting and a refusing caret. So
  > **the pane split is a context‑menu rule only** — do not carry it into the menu bar. ⚠ IHC OpenVisual's
  > menu bar has **not** been dumped per‑pane‑focus, so this is specified from the vendor with the app side
  > unmeasured; it is a SHOULD until that comparison runs. Evidence: `RESULTS.md` **F‑049** (an open **E**).
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

**Implementation status:** 🟡 Implemented — ✅ the **double‑click route now exists** (US-067, backlog
**A‑4**). ⚠ **Except the clipboard route parity**: *Cut*/*Copy*/*Paste* are missing from every context menu
(F‑009), so they are reachable only by toolbar and shortcut. Backlog **A‑5** closes it; the inventory is
specified in US-068.

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

**Implementation status:** 🟡 Implemented (in‑scope shortcuts) — **except `F6`, which is bound but dead**
(backlog **A‑28**). The arrow‑key quadrants are implemented and measured aligned (F‑013). **`Ctrl+C` /
`Ctrl+X` / `Ctrl+V` are bound and measured working**, and undo/redo is effect‑verified.

> **Corrected 2026‑07‑17 (was: "clipboard/undo and simulation bindings deferred").** The "deferred" claim
> contradicted **this file's own `:88‑92`** (US-044), which records `Ctrl+C` as measured working — *the
> status bar confirms "Copied Lampeudtag"* — and the ledger backs `:88‑92`: **F‑009** (`RESULTS.md:168`)
> measured *Cut*/*Copy*/*Paste* working **on the toolbar and on `Ctrl+C`**, and **F‑045**
> (`RESULTS.md:193`) effect‑verified undo/redo. **The contradiction is resolved in favour of `:88‑92`.**
> ⇒ ⭐ **The gap is the context‑menu route (backlog A‑5), not the binding** — it is US-044's route‑parity
> MUST, not a missing shortcut. Residue: `Ctrl+I` / `Ctrl+U` remain **unverified**; the **simulation** half
> is ⛔ **E8 / out of scope** — specified for completeness, not for implementation, exactly as the
> simulation criterion above states.

> **Added 2026‑07‑17 (F‑083).** `F6` (switch panes) is **bound but dead**: the handler at
> `MainWindow.axaml.cs:71‑76` calls `.Focus()` on the sibling `TreeView`, but focus does not take —
> `focusedPane` is unchanged across two sessions and before/after screenshots are pixel‑identical (the probe
> is not blind). So `F6` is a **measured‑unmet MUST** → backlog **A‑28**. Two positives came out of the same
> measurement: (a) the rest of the non‑simulation shortcut set is **parity** with the vendor; and (b) IHC
> OpenVisual **adds** `Ctrl+Shift+Up` / `Ctrl+Shift+Down` (*Move up* / *Move down*) — shortcuts the vendor
> has no equivalent for — a **granted C**, consistent with the non‑drag reorder **supplement** in
> US-055/US-068 (the keyboard route alongside the primary drag). Evidence: `RESULTS.md` **F‑083**.

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

**Implementation status:** ✅ **Implemented** (backlog **A‑4**, 2026‑07‑16) — the per‑node‑type matrix is
live, the toggle is suppressed, and the scene‑container dialog the matrix needed was built. Verified against
the real application, not only headlessly.

> ✅ **Resolved 2026‑07‑17 (F‑084) — measured parity, no defect.** C16 was driven with an explicit
> x‑offset: **both** IHC Visual and IHC OpenVisual activate on the **label only**, and a double‑click in the
> blank strip right of a short label is a **no‑op on both, with no accidental toggle**. The earlier concern —
> that IHC OpenVisual falls through and toggles — is **superseded**, so **F‑052 is closed (E→A)** and this
> route needs no change from this side. Evidence: `RESULTS.md` **F‑084**.
>
> ⚠ **Two implementation traps are recorded in backlog A‑4 and are worth reading before touching this** —
> handling the pointer event does *not* stop the toggle, and a handler on the TreeView is too late. This
> story's second MUST is only satisfiable at one point in the event chain.

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
  (e.g. *Insert product* on a link row, or on a **pin**) is **absent**, not merely disabled.
- MUST: The **insert vocabulary is split by pane**: *Insert product* appears on a locality in the
  *Installation* pane only; *Insert function block* and *Empty function block* on a locality in the
  *Functions* pane only. Each appears in exactly one pane — mirroring the split the **tree itself already
  makes**, which shows products on the left and blocks on the right.
- MUST: **A pin is not a container and not a sibling.** A product's pins come from its catalog type, so a
  pin's menu offers **no insert command, no *Move up*/*Move down*, and no *Delete*.**

  > **Added 2026‑07‑17 — this is the one inventory gap that corrupts the project, not just the menu.** IHC
  > OpenVisual offers *Delete* on a product pin and **it works**: deleting an unlinked pin is **silent** (no
  > confirm — the delete guard is link‑triggered, so it does not fire) and drops the product from 9 pins to
  > 8. The saved file then holds a `LK FUGA Tryk 6 tast 3 dioder` — a **six**-button switch — carrying
  > **five** `dataline_input`s. The sixth physical button has no element at all, so it can never be addressed
  > or wired, and **the tree cannot show the discrepancy** (the row is simply absent). IHC Visual offers no
  > such command on any pin. ✅ Link integrity does survive (the SDK cascades both halves — 740 halves, 0
  > dangling); what does not survive is **catalog conformance**. ⚠ The *unlinked* case is the dangerous one
  > precisely because it is silent: an accidental `Delete` removes a button with no feedback at all.
  > **An SDK guard is required in addition to this menu gate** — the gate only protects this one GUI, while
  > US-053 already mandates the engine refusal. The precedent is **A‑16**, which put link legality in the SDK
  > (not the view‑model) on exactly this "valid whoever drives the editor" reasoning; the engine half here is
  > backlog **A‑24**. Evidence: `RESULTS.md` **F‑067**; see also US-053.
- MUST: *Cut*, *Copy* and *Paste* appear in the context menu of every node type that supports them
  (locality, product, function block), satisfying the US-044 route‑parity MUST.
- MUST: *Paste* is shown **conditionally on clipboard state** — it is absent when the clipboard is empty and
  present when it holds a node. (IHC Visual's locality menu is 6 items with an empty clipboard and 7 with a
  full one; the delta is exactly `&Indsæt`.)
- MUST: A **link row**'s menu offers exactly two commands: *jump to the opposite end of the link* (US-025)
  and *Delete* (US-057). It offers no properties item.
- MUST: An **unlocked function block**'s menu includes a *show program* command — a context‑menu route into
  programming mode (US-026). IHC OpenVisual offers no such route today.
- MUST: A **locked** function block's menu offers **both** *show program* **and** *Unlock* — they are
  **additive, not alternatives**. The locked menu is the unlocked one **plus** *Unlock* (8 items vs 7);
  *show program* is on both. ⇒ **A locked library block's program CAN be opened for reading**: the lock
  gates *editing*, never *viewing*, and no unlock is needed to read a block's program.
- MUST: **Inside a *locked* block's program (programming mode), every program node is view‑only.** Its context
  menu offers *Properties* (Egenskaber) only; **Delete and *Move up*/*Move down* are removed**, matching the
  vendor's fully view‑only locked‑block program menu. (The `!IsProgrammingBlockLocked` gate that A‑27 applied to
  the insert/add commands is extended to Delete/Move — see `07-fb-programming.md`, F‑087.)

  > **Added 2026‑07‑18 (F‑087, M4 census).** The vendor's locked‑FB program menu offers `&Egenskaber` on every
  > node (leaf nodes add `&Logmærke`/`&Stoppunkt`; an internal variable adds `&Kopier`) but **never Delete or
  > Move** — a locked library block cannot be mutated at all. OpenVisual had left *Delete* + *Move up/down* active
  > on locked program nodes (each carries an ElementId ⇒ `CanDelete`/`CanEditNonLink` true), letting a user delete
  > or reorder inside a locked block (a D10 file‑integrity break). Evidence: `tmp\comptest\out\M\M4-census.json`.

  > **Closed 2026‑07‑17 (was a `[TBD]` calling this "contested between two records").** Settled at the desk
  > from a stored vendor dump — no drive needed; see the closure note under the inventory table. Evidence:
  > `out\P1-census\vendor-gesture-findings.md:90‑91` (**8 items**, carrying both `&Oplås` **24766** *and*
  > `&Vis program` **24768**); `RESULTS.md` **F‑011**.
- MUST: A **product pin**'s menu offers a **log mark** toggle — the command behind the `Log …` state rows
  US-010 renders. IHC OpenVisual has no such command **on any route**.

  > **Added 2026‑07‑17.** A missing *feature*, not just a missing menu entry: `&Logmærke` is on the vendor's
  > 3‑item output‑pin menu, and IHC OpenVisual offers no equivalent anywhere. Raise it as its own backlog
  > item — backlog **A‑22** — rather than smuggling it in with the inventory fix. Evidence: `RESULTS.md`
  > **F‑063**.

- MUST: A **scene container**'s menu offers *Copy*. IHC OpenVisual's scene container currently offers eight
  commands and *Copy* is **not** among them, so a scene container is the one node type where the clipboard
  gap (US-044) is a missing route to a command the vendor **does** have here.
- SHOULD: *Move up* / *Move down* remain on the node types that can be reordered (locality, product,
  function block) and are **absent** from a link row **and from a pin**.

  > **Deliberate addition, kept.** *Move up*/*Move down* have no counterpart in IHC Visual, whose reorder
  > gesture is a **drag**. IHC OpenVisual supports **both**: drag reorder for vendor alignment (US-055) and
  > *Move up*/*Move down* as the non‑drag **supplement** that gives US-044 a keyboard/menu route to the same
  > result. They are **kept** — but the supplement is for *reorderable* nodes: it does not belong on a link
  > row, and not on a **pin**, whose order is its catalog type's (F‑067).

**Target inventories** (IHC Visual's, as the authoritative spec; IHC OpenVisual's wording is its own
English — the *language* is an allowed difference, the *inventory* is not).

**A locality's menu depends on which pane it is in. Every other node type's does not.**

| Node type | Pane | Commands |
|---|---|---|
| Installation root | **both** (identical) | insert locality — **1 item** *(already aligned — F‑016, regression baseline)* |
| Locality | *Installation* | **insert product** (submenu), Cut, Copy, Delete, separator, Properties — **6 items**; **+ Paste** when the clipboard is full |
| Locality | *Functions* | **insert function block** (submenu), Cut, Copy, Delete, **empty function block**, separator, Properties — **7 items** |
| Product | *Installation* | Cut, Copy, Delete, separator, Properties — **5 items** |
| Product pin (input or output) | *Installation* | **log mark**, separator, Properties — **exactly 3 items** |
| Scene container (*Scenarier*) | *Installation* | Copy, separator, Properties — **exactly 3 items** |
| Link row | either | jump to opposite end, Delete — **exactly 2 items** |
| Function block (unlocked) | *Functions* | Save block…, Cut, Copy, Delete, **show program**, separator, Properties — **7 items** |
| Function block (locked) | *Functions* | Save block…, Cut, Copy, **Unlock**, Delete, **show program**, separator, Properties — **8 items** — the unlocked row **plus** *Unlock* |

> **Closed 2026‑07‑17 — the two records were never in conflict: they measure DIFFERENT NODE TYPES.**
> *(Was: a warning not to implement this node type until a re‑dump settled "7 items vs 8".)* The note's
> premise — *"no vendor dump for this node type is stored anywhere"* — was **wrong**. It was verified by
> grepping **filenames** for `ov-*`, which misses the file. **`out\P1-census\vendor-gesture-findings.md`
> IS the vendor record**: Win32 command ids + `&`‑prefixed Danish labels (IHC OpenVisual's menus are English
> and carry no command ids), taken with an **empty clipboard** — the file says so at `:82`. At **`:90‑91`**
> it dumps the **function block** `Lamper v. hoveddør` (TV2) as **8 items**: `&Gem...` 24765 · `&Klip` 24583
> · `&Kopier` 24584 · **`&Oplås` 24766** · `&Slet` 24586 · **`&Vis program` 24768** · sep ·
> `&Egenskaber...` 30503. That block is **locked** — verified in the project itself, not inferred from its
> menu: `realprj-VisCopy.vis` holds `<functionblock id="_0x3de328" name="Lamper v. hoveddør" … locked="yes"
> …>`. *(The project carries **117** `functionblock` tags — **109 `locked="yes"`, 8 with the attribute
> absent** ⇒ resolved against the project's inline DTD default `no` ⇒ unlocked.)* F‑069's bycatch measured
> the **other arm**: **N7, the UNLOCKED block — 7 items**, also carrying `&Vis program`.
>
> | Node | Items | `&Oplås`? | `&Vis program`? |
> |---|---|---|---|
> | **N6 locked FB** | **8** | ✅ 24766 | ✅ **24768** |
> | **N7 unlocked FB** | **7** | — *(nothing to unlock)* | ✅ **24768** |
>
> ⭐ **The delta is exactly `&Oplås`, and *show program* is present in BOTH.** 8 − 1 = 7 — the two dumps
> agree perfectly. ⇒ **F‑011 (`RESULTS.md:170`) and A‑5's 8‑item table (`alignment-backlog.md:297`) are
> CORRECT and ship as written.** The one wrong statement is **F‑069's parenthetical** — that the locked
> variant carries `&Oplås` *instead of* `&Vis program` — which is an **inference, not a dump**, and is
> hereby **retracted**. ⛔ **`tmp\compare3.md` §6.1's C13.1–C13.3 are struck: no vendor run is needed.**
>
> **Corroborated from a third direction:** **F‑043** (`RESULTS.md:207`) later unlocked *this same block* and
> watched **`&Oplås` vanish** from its context menu — the 8→7 transition, measured by effect.
>
> ⭐ **This also confirms `tmp\compare3.md` §4.3's C12 — a locked block is *view‑only* in programming
> mode** (now measured, **F‑076**/**F‑077**): the vendor offers *Vis program* on a locked block **on
> purpose** but refuses edits to it. The authoring gate is backlog **A‑27** (specified in US-026/US-020).
>
> ⚠ **One honest residual: n=1 per arm.** A second block of each kind would harden the rule — free while any
> FB menu is open, and blocking nothing.

> **[R5] closed 2026‑07‑16 — the pane split HOLDS, and the naive reading was measurably wrong.**
> *(Was: a TV1-only sample plus a warning not to act on it.)* The vendor's *Functions*-pane locality menu was
> dumped and it carries **both** function‑block routes, mapping 1:1 onto IHC OpenVisual's existing items —
> *Insert function block* ↔ `&FunktionsBlokke`, *Empty function block* ↔ `&Tom Funktionsblok`. The root menu
> is **pane‑independent** (1 item in both).
>
> ⭐ **So the fix is to PANE‑GATE, not to delete** — and the caution the old note carried earned its keep:
> reading *"the vendor's locality menu has no function‑block insert"* off a one‑pane sample would have
> **stripped the capability from both panes**. The vendor's locality menu is not *"products but no function
> blocks"*; it is *"products **on the left tree**"*. Evidence: `RESULTS.md` **F‑048**.
>
> **[R5] closed 2026‑07‑17 — the output pin and scene container are dumped**, and both are **3 items**: far
> smaller than IHC OpenVisual's 11 and 8. Evidence: `RESULTS.md` **F‑063**.

**Output:**
- Every node type's right‑click menu is a valid, minimal command set for that node, and no command is
  reachable by toolbar or shortcut alone.

### AC illustrations

- Right‑clicking a link row offers exactly *jump to the opposite end* and *Delete* — not the seven generic
  items, and not *Insert product*.
- Right‑clicking a locality with an empty clipboard offers no *Paste*; copying a product first and
  right‑clicking the same locality now offers *Paste*.

### Constraints

- Verification method — **Test** (headless UI, `safe_visual_tests`): one inventory assertion per node type
  **× pane**. **The pane axis is the part that matters** — a single‑pane assertion cannot see the defect this
  story exists to fix, which is exactly how it survived the first comparison. Include the
  **clipboard‑state‑dependent** *Paste* item (which needs a test that copies something first) and a case
  asserting that a **pin** offers no *Delete*.
- The per‑node‑type mechanism already exists: the installation root's 1‑item menu is measured **aligned**
  today, so this story generalises a working mechanism rather than introducing one.
- **The gate to replace is a pane‑blind, type‑blind "can this node be edited?" condition.** That single
  condition is why the same list appears on a locality in both panes *and* on a pin — the two defects have
  one cause, and the pin case (F‑067) shows it is not a cosmetic one.

**Readiness:** Ready.

> **Both [R5] captures are closed** — the **pane split** on 2026‑07‑16 (**F‑048**: it holds; pane‑gate, do
> not delete) and the **output‑pin / scene‑container** inventories on 2026‑07‑17 (**F‑063**: 3 items each).
> Every node type in the table above now has a measured vendor side.
>
> **The `[R3]` is closed 2026‑07‑17 — and it was never a conflict.** The function block's "7 vs 8" was two
> records measuring **different node types**: the **locked** block (8) and the **unlocked** one (7), the
> delta being exactly *Unlock*. Settled from the stored vendor dump at the desk — **no re‑dump was needed**;
> see the closure note by the table. **Every node type in this story now ships on a measured vendor
> inventory.**

**Implementation status:** ⛔ **Not implemented** — IHC OpenVisual shows one generic menu on locality,
product, link row **and pin** alike: no clipboard commands anywhere, no jump/show‑program/log‑mark routes,
the function‑block items unfiltered by pane, and *Insert product* / *Delete* / *Move up* / *Move down*
offered on a pin. The root's 1‑item menu is the one aligned case. Backlog **A‑5** implements this story.
⚠ Its **pin** half is the highest‑priority part: it is the only inventory gap that writes a project IHC
Visual cannot (F‑067).

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
  (Standard caveat: if a field's combo/dropdown is open, the first `Esc` closes that popup and the next
  closes the dialog — F‑079.)
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

## US-070 — An edit keeps the tree's expansion state

> **Added 2026‑07‑18** from a defect found while exercising the drag gestures. Every project mutation fires
> `StateChanged`; the view‑model rebuilds both panes from scratch and the fresh nodes came up at their
> build‑time defaults — so connecting two pins, deleting a link, or **any** edit **collapsed the whole tree**,
> throwing the installer back to the top after every change. This story fixes that.

**As an** IHC installer, **I want** the tree to stay expanded exactly as I left it when I edit the project,
**so that** I keep my place while wiring — an edit changes *what* I edited, not *where I am* in the tree.

> **Sibling of US-067.** US-067 says opening a node's *dialog* must not move the tree under you; this says the
> same of an *edit*. Together they are one principle: **only a deliberate navigation or mode change moves the
> tree — never a mutation.**

**Scope excludes:** which gestures cause an edit (their own epics); the *selection* an edit may reset (not
specified here); and the deliberate reveal defaults below, which are kept.

### Acceptance criteria (Given‑When‑Then)

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
- MUST: A node the edit **creates** takes its build‑time default; a node the edit **removes** drops out.
  Preservation applies to the nodes that persist across the edit.
- MUST: The **reveal‑on‑first‑child** default (US-006 — a locality with contents opens) is kept: a node that
  gains its *first* child opens by default rather than inheriting a stale collapsed state. (Implementation:
  expansion is carried only for nodes that already had children.)
- MUST: A deliberate **mode switch** (configuration ⇄ a block's programming view) is **not** an in‑place edit
  — it opens the target view at its defaults, not carried over from the other mode.
- SHOULD: The state survives across the **binding**, so a node the installer expanded *in the UI* (not only
  one expanded programmatically) is preserved — `TreeViewItem.IsExpanded` is two‑way bound to the view‑model,
  which is the single source of truth for expansion.

### AC illustrations

- Expanding `Living room` ▸ `Lampeudtag` ▸ its output pin, then dragging that pin onto a block input, leaves
  `Living room`, `Lampeudtag` and the block's `Input` section **still open** — the link appears in place.
- Collapsing a locality that has products, then inserting a locality elsewhere, leaves the first locality
  **still collapsed** — the rebuild restores state, it does not re‑apply defaults.

### Constraints

- Verification method — **Test** (headless UI, `safe_visual_tests` → `TreeExpansionTests`): an expanded node
  survives a link and a link‑deletion, a collapsed node survives an unrelated edit, a first child still
  reveals its parent, a mode switch opens fresh, and — through the real window — a UI‑driven expansion
  survives an edit (pinning the two‑way binding).
- The rebuild‑from‑scratch design (clear both panes, repopulate from the project on `StateChanged`) is kept;
  the fix is a snapshot/restore around it, not a move to in‑place tree diffing.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented** — the view‑model snapshots expansion (per pane, by element id,
for nodes that have children) before each rebuild and restores it after, guarded by a view key so a mode
switch opens fresh; the `IsExpanded` binding is two‑way so UI expansions are captured. Regression‑covered by
`TreeExpansionTests`.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-044 | Activate functions via right‑click, menu, or shortcut | Ready | E11 | Must | -- |
| US-045 | Navigate and edit the tree with the keyboard | Ready | E11 | Must | -- |
| US-067 | Open a node's properties by double‑clicking it | Ready | E11 | Must | US-007, US-011, US-012, US-044 |
| US-068 | Offer a context menu tailored to the node type | Ready | E11 | Must | US-025, US-026, US-044, US-053, US-055 |
| US-069 | Dismiss and default dialogs from the keyboard | Ready | E11 | Must | US-053 |
| US-070 | An edit keeps the tree's expansion state | Ready | E11 | Must | US-006, US-067 |
