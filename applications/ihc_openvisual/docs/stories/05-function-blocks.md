---
version: 0.2.0
last-updated: 2026-07-16
status: draft
---

# E5 — Function blocks: insert & structure

> **Implementation status:** ✅ Implemented — the embedded FB catalog is measured **aligned** with IHC
> Visual (F‑042). ⚠ One open measurement: whether *Unlock* warns before it irreversibly unlocks (US-020,
> F‑043).

> **Current scope:** ✅ **In scope** — inserting, structuring and unlocking function blocks and
> managing FB folders is project CRUD.

**Goal:** Let an installer add ready‑made library or empty function blocks into localities in the
*Functions* pane, understand a function block’s internal structure, unlock library blocks for editing, and
organise blocks into their own and favourite folders — so control functions can be assembled and
reused.

**Scope:** inserting preprogrammed library blocks from the library folders; inserting an empty block (which
enters programming mode); the four variable sections and the program subtree of a block; unlocking library
blocks; and managing custom / *Favourites* folders on disk. **Scope excludes:** the
actual logic authoring inside a block (E7) and product↔block links (E6).

**Acceptance criteria (epic level):**
- MUST: The installer can insert a preprogrammed library block or an empty block into a selected locality in
  the *Functions* pane, confirmed in the status bar.
- MUST: A function block exposes four variable sections (Input, Output, Settings, Internal variables)
  and a program subtree (Programs > Program > Events / Commands).
- SHOULD: Library blocks are marked with a library badge and must be *unlocked* before their internals can be
  edited; custom (own) folders persist across software updates.

**Readiness:** Ready.

---

## US-018 — Insert a preprogrammed library function block

**As an** IHC installer, **I want** to insert a ready‑made library function block into a locality from the
library folders, **so that** I get a tested sub‑program without writing logic.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a library block from the library folders
  Given the "Functions" pane shows a locality (e.g. "Living room")
  When I right-click the locality and choose a block from the library folder list
  Then the block is inserted as a child of that locality
  And the status bar reads: Function block '<block>' has been inserted under <locality>
  And the block node carries the library function-block icon and can be expanded to show its structure

Scenario: Library folders are the standard set
  Given the function-block folder list is shown
  Then it presents the library folders the catalog defines, named as the vendor names them

Scenario: A block bundles its variables and program
  Given a library block has been inserted
  When I expand it
  Then it shows its Input/Output/Settings sections with typed pins carrying default values
  And (after unlocking, US-020) its program can be opened in programming mode (US-026)
```

### Business rules (the function‑block catalog)

- MUST: The function‑block catalog is **IHC Visual's**, embedded in the SDK — the installer gets the
  vendor's blocks without a vendor installation (US-063).
- MUST: The library **categories keep the vendor's own names, verbatim** — not renamed, re‑homed or
  translated.
- MAY: IHC OpenVisual's own catalog‑import entries (US-059/US-060) sit alongside the vendor's categories.

> **Confirmed 2026‑07‑16 — aligned.** The embedded catalog carries **72 function blocks** under the
> vendor's own Danish category names, preserved verbatim (`00. Foretrukne`, `01. Lysstyring`, `02. Tid, ur
> og kalender`, `03. Persienne og vindue`, `04. Specielle funktioner`, `05. Klimastyring`, `06. Alarm`,
> `08. Viewer`, `AutoProof` — 07 is absent in the vendor's set too). **Contrast with the product catalog**
> (US-010), which IHC OpenVisual partly re‑homed and partly translated and which therefore diverges
> (F‑028): the FB half is what "embedded verbatim" looks like, and is the reason the two catalogs need
> different treatment rather than one rule. Evidence: `RESULTS.md` **F‑042**.
> ⚠ Not a leaf‑exact match — the vendor's Insert‑FB **menu** categories were not dumped and the 72 blocks
> were not diffed leaf‑for‑leaf.

### AC illustrations

- Inserting a `<function block>` under `Room` gives a block whose **Input** section holds its
  catalog-defined input pins (`<pin>`), and whose **Output** section holds its catalog-defined
  output pins (`<pin>`).
- Inserting a `<function block>` under `Utility room` shows its catalog-defined **Input**, **Output**
  and **Settings** pins (`<pin>`), each Settings pin carrying its catalog default value; the
  status bar reads `Function block '<block>' has been inserted under Utility room`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-019 — Insert an empty function block

**As an** IHC installer, **I want** to insert an empty function block, **so that** I can author a
custom function from scratch.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert an empty block into a locality
  Given a locality is selected in the "Functions" pane, in configuration mode
  When I right-click it and choose "Empty function block", or press Ctrl+Shift+B
  Then an empty block named "Empty block" is inserted under the locality
  And the status bar reads: Empty block was inserted under <locality>

Scenario: An empty block exposes the four variable sections
  Given an empty block exists
  When I expand it
  Then it shows exactly: "Input", "Output", "Settings" and "Internal variables",
    each with its own icon

Scenario: Editing the block enters programming mode
  Given an empty block is selected
  When I press F3 (or otherwise open it)
  Then the view switches to programming mode (US-026), where the right pane shows
    "Programs" > "Program" > { "Events", "Commands" }
  And I can name the block by selecting it and pressing F2
```

### AC illustrations

- After inserting an empty block under `Garage` and entering programming mode, both pane headers read
  `Empty block`; the left pane shows `Empty block > {Input, Output, Settings, Internal variables}` and the
  right shows `Empty block > Programs > Program > {Events, Commands}`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-020 — Unlock a library function block for editing

**As an** IHC installer, **I want** to unlock a supplied library block — after being told the unlock cannot
be taken back — **so that** I can modify a tested block instead of starting from an empty one, without
discovering too late that I cannot restore it.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Unlock a library block
  Given a block carrying the library function-block icon is selected
  When I right-click it and choose "Unlock" and accept the warning
  Then the icon changes to the editable function-block icon
  And I can now work with the block as with any custom block (edit variables and program)
  And "Unlock" no longer appears on the block's context menu

Scenario: The unlock is warned about first, because it cannot be undone
  Given a locked library block is selected
  When I choose "Unlock"
  Then a warning states that unlocking the block cannot be reversed, and I must accept it to proceed

Scenario: Decline the warning
  Given the unlock warning is shown
  When I decline it
  Then the block stays locked

Scenario: Locked blocks resist internal edits
  Given a library block that has not been unlocked
  Then its internals are treated as read-only until "Unlock" is applied
```

### Business rules (irreversibility)

- MUST: Unlocking is **irreversible** — *Undo* does not restore the lock. Because the edit history cannot
  reverse it, the warning is the only protection the installer gets.
- MUST: Attempting to undo an unlock **degrades gracefully** — the application stays running and says the
  action cannot be reversed (US-052).

> **Added 2026‑07‑16 (was: unlock with no warning specified).** **IHC Visual unlocks silently** — no
> dialog — and the action is **irreversible**: undo does not restore the lock. That combination is a
> candidate quirk, and it is not copied. Per the ruling's exception #1, IHC OpenVisual **warns first** where
> the vendor is silent: the warning changes nothing about what a confirmed unlock does, and this is exactly
> the case the exception exists for — an irreversible destruction of state with no other guard. ⚠ Undoing
> the unlock is also what **crashed IHC Visual outright** during the comparison (US-052, F‑046), which is
> why the graceful‑degradation MUST is here too. Evidence: `RESULTS.md` **F‑043** (vendor silent, verified
> by effect: `&Oplås` vanished from the context menu) and **F‑046**.
>
> **TBD (pending capture): whether IHC OpenVisual warns today is unmeasured.** Its FB context menu has an
> *Unlock* item, but the measured unlock attempt reported success while the block **stayed locked**, so no
> warning could be observed either way — a driver limitation, not a known app defect (`RESULTS.md` F‑043 is
> an open **E**). This story specs the **intent**; drive the unlock through a reliable route and record
> what the app actually does before treating its current behaviour as known.

### AC illustrations

- A library block shows a distinct library badge (a red‑outlined square marker in the tree). Choosing
  *Unlock* warns that the block cannot be re‑locked; accepting switches the badge to the plain
  function‑block icon, signalling it is now editable, and removes *Unlock* from its context menu.

### Constraints

- Verification method — **Demonstration** that the warning appears, that declining leaves the block locked,
  and that accepting unlocks it and removes the *Unlock* route.
- The warning is a **deliberate divergence** from IHC Visual's silence, granted by the 2026‑07‑16 ruling —
  do not remove it to match the vendor.

**Readiness:** Ready — the target behaviour is fully specified. (What IHC OpenVisual does *today* is
unmeasured, but that is an implementation question, not a gap in this story.)

**Implementation status:** 🟡 Implemented (unlock itself) — ⚠ whether it **warns** first is **TBD (pending
capture)**: the unlock could not be driven through a reliable route during the comparison, so no warning
could be observed either way (F‑043, an open **E**). Confirm against the app before treating either answer
as known. ⚠ Also unverified: that undo‑after‑unlock **degrades gracefully** — the path was unreachable for
the same reason, and it is what crashed IHC Visual (US-052, F‑046).

---

## US-021 — Manage own and favourite FB folders

**As an** IHC installer, **I want** to create my own folders, save blocks into them, and keep
frequently used blocks in *Favourites*, **so that** I can reuse blocks quickly and keep
them across software updates.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Create an own folder on disk
  Given IHC OpenVisual's function‑block library (`<catalog folder>`) folder
  When I right-click the empty area and choose "New" > "Folder" and name it (e.g. "My_blocks")
  Then a new folder is created; empty folders do not appear in IHC OpenVisual's block list,
    and own folders are preserved when IHC OpenVisual is updated

Scenario: Save a block into a folder
  Given a block is selected in the "Functions" pane
  When I right-click it and choose "Save..." — or press Ctrl+G — give it a name and optional note, and pick a target folder
  Then the block is stored there and its note is shown as a tooltip when hovering the block later

Scenario: Add and use a favourite
  Given a block that lives in a library folder (`<catalog folder>`)
  When I copy it and paste it into the "Favourites" folder
  Then it becomes available under "Favourites" when inserting blocks into a locality
```

### AC illustrations

- Hovering a saved block shows the note entered in its *Save...* dialog as a tooltip.

### Constraints

- Verification method — **Demonstration** of folder creation, block save, and favourite reuse; and
  **Inspection** that empty folders are hidden and own folders survive an update.

**Readiness:** Ready.

**Implementation status:** 🟡 Core implemented (Save block); folder/favourites management adapted for the install‑free design.
