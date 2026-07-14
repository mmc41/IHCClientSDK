---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E5 — Function blocks: insert & structure

> **Implementation status:** ✅ Implemented.

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
  Then it presents the library folders the catalog defines

Scenario: A block bundles its variables and program
  Given a library block has been inserted
  When I expand it
  Then it shows its Input/Output/Settings sections with typed pins carrying default values
  And (after unlocking, US-020) its program can be opened in programming mode (US-026)
```

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

**As an** IHC installer, **I want** to unlock a supplied library block, **so that** I can modify a tested
block instead of starting from an empty one.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Unlock a library block
  Given a block carrying the library function-block icon is selected
  When I right-click it and choose "Unlock"
  Then the icon changes to the editable function-block icon
  And I can now work with the block as with any custom block (edit variables and program)

Scenario: Locked blocks resist internal edits
  Given a library block that has not been unlocked
  Then its internals are treated as read-only until "Unlock" is applied
```

### AC illustrations

- A library block shows a distinct library badge (a red‑outlined square marker in the tree). After *Unlock*, the
  badge switches to the plain function‑block icon, signalling it is now editable.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

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
