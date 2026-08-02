---
version: 0.4.0
last-updated: 2026-08-02
status: draft
---

# E5 — Function blocks: insert & structure

**Goal:** Let an installer add ready-made library or empty function blocks into localities in the
*Functions* pane, understand a function block's internal structure, unlock library blocks for editing, and
organise blocks into their own and favourite folders — so control functions can be assembled and
reused.

**Scope:** inserting preprogrammed library blocks from the library folders; inserting an empty block (which
enters programming mode); the four variable sections and the program subtree of a block; unlocking library
blocks; and managing custom / *Favourites* folders on disk. **Scope excludes:** the actual logic authoring
inside a block (E7) and product↔block links (E6).

**Acceptance criteria (epic level):**
- MUST: The installer can insert a preprogrammed library block or an empty block into a selected locality in
  the *Functions* pane, confirmed in the status bar.
- MUST: A function block **owns** four variable sections (Input, Output, Settings, Internal variables) and a
  program subtree (Programs > Program > Events / Commands). **Which of them the tree draws depends on the
  mode and on whether the section is empty** — see US-018's rendering rules; do not read this criterion as
  "all four are always visible".
- SHOULD: Library blocks are marked with a library badge and must be *unlocked* before their internals can be
  edited; custom (own) folders persist across software updates.

**Readiness:** Ready.

---

## US-018 — Insert a preprogrammed library function block

**As an** IHC installer, **I want** to insert a ready-made library function block into a locality from the
library folders, **so that** I get a tested sub-program without writing logic.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Insert a library block from the library folders
  Given the "Functions" pane shows a locality (e.g. "Living room")
  When I right-click the locality and choose a block from the library folder list
  Then the block is inserted as a child of that locality
  And the status bar reads: Function block '<block>' has been inserted under <locality>
  And the block node carries the library function-block icon and can be expanded to show its structure

Scenario: Library folders are the standard set
  Given the function-block folder list is shown
  Then it presents the library folders the catalog defines, named as the catalog names them

Scenario: A block bundles its variables and program
  Given a library block has been inserted
  When I expand it
  Then it shows its Input/Output/Settings sections with typed pins carrying default values
  And its program can be opened for reading in programming mode while it is still locked (US-026)
  And unlocking (US-020) is required only to edit it, not to view it
```

### Business rules (the function-block catalog)

- MUST: The function-block catalog is **embedded in the app** — the installer gets the stock library
  blocks self-contained, with no separate installation (US-063).
- MUST: The library **categories keep the catalog's own names, verbatim** — not renamed, re-homed or
  translated (`00. Foretrukne`, `01. Lysstyring`, `02. Tid, ur og kalender`, `03. Persienne og vindue`,
  `04. Specielle funktioner`, `05. Klimastyring`, `06. Alarm`, `08. Viewer`, `AutoProof`). This differs
  from the **product** catalog (US-010), whose structural labels render in English — the FB library
  categories are treated as catalog data.
- MAY: IHC OpenVisual's own catalog-import entries (US-059/US-060) sit alongside the stock catalog categories.

### Business rules (the function-block properties dialog)

- MUST: A function block's properties dialog (F2 / right-click > *Properties* / double-click) carries
  **Name** and **Note** plus OK/Cancel — the same two-field pattern as a locality (US-007).
- MUST: For a block that is a **library instance** (it carries a library-identity key, see US-020),
  the dialog additionally shows a read-only **original properties** group — the origin's name, number,
  version, created date (rendered `dd/MM/yyyy`) and developer — so the installer can see **which**
  library block, at **which** version, they have. The fields are genuinely disabled, not merely
  read-only, and the data comes from the block's stored master metadata.
- MUST: A block authored from scratch (or unlocked, US-020) shows **no** origin group — Name + Note
  only.

### Business rules — which of a block's sections the tree draws

In **configuration mode** (the *Functions* pane's normal view), IHC OpenVisual does not draw every section a
block owns. Two rules decide it:

- MUST: A section with **no members is not drawn at all**. An `Input` or `Settings` section that holds no
  pins is absent from the tree, not shown empty.
- MUST: **`Internal variables` is not drawn in configuration mode**, whether or not it holds members. It is
  a programming-mode section (US-026, US-027) — internals are the block author's business, not the
  installer's.
- MUST: Suppression is **display-only** — every hidden section stays in the `.vis` and is written back
  verbatim on save. This is the same discipline US-010's hidden product rows follow.
- MUST: A drawn section renders **the caption stored in the project file on the container itself**
  (e.g. `Indstillinger`, `Interne variable`, and in programming mode `Programmer` / `Hændelser` /
  `Kommandoer`); a fixed default caption stands in only when the file leaves the container unnamed.
  This is the same rule as the locality-root caption (US-006) — a section caption is the container
  element's stored name, never a hard-coded UI string.

### AC illustrations

- Inserting a `<function block>` under `Room` gives a block whose **Input** section holds its
  catalog-defined input pins (`<pin>`), and whose **Output** section holds its catalog-defined
  output pins (`<pin>`).
- Inserting a `<function block>` under `Utility room` shows its catalog-defined **Input**, **Output**
  and **Settings** pins (`<pin>`), each Settings pin carrying its catalog default value; the
  status bar reads `Function block '<block>' has been inserted under Utility room`.
- A block with no input pins shows only `Output` and `Settings` — no empty `Input` row — and shows no
  `Internal variables` row until its program is opened (US-026).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — insert, the catalog, and the configuration-mode section-rendering
rules all work: empty sections and `Internal variables` are hidden in configuration mode as a display-only
projection (the `.vis` is untouched) and shown in programming mode.

---

## US-019 — Insert an empty function block

**As an** IHC installer, **I want** to insert an empty function block, **so that** I can author a
custom function from scratch.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Insert an empty block into a locality
  Given a locality is selected in the "Functions" pane, in configuration mode
  When I right-click it and choose "Empty function block", or press Ctrl+Shift+B
  Then an empty block named "Tom blok" is inserted under the locality — the placeholder name is
    the file format's own, written into the project as data (the same rule as "Lokalitet", US-008)
  And the view switches straight into programming mode for the new block (US-026):
    both panes re-root at it, and the status bar shows the programming-mode hint

Scenario: An empty block exposes the four variable sections
  Given an empty block is open in programming mode
  When I expand it
  Then it shows exactly its four section containers, labelled by their stored names —
    the seed writes "Input", "Output", "Indstillinger" and "Interne variable" —
    each with its own icon

Scenario: Returning to configuration mode
  Given the just-inserted empty block is open in programming mode
  When I press Esc
  Then the view returns to configuration mode with the tree intact (US-026)
  And I can name the block by selecting it and pressing F2
```

### Business rules (the empty-block seed)

- MUST: A freshly inserted empty block ships with the **four section containers all empty — zero pins**
  (inputs / outputs / settings / internal variables). The Input/Output pins are **user-added, not seeded**:
  an empty block has none until the author inserts them (US-027, US-026).
- MUST: The right pane auto-creates **one** program (stored name `Program`) holding empty events and
  commands groups (stored names `Hændelser` / `Kommandoer`) — no more, no less.
- MUST: The seed's container names are **project data written into the file** (`Input`, `Output`,
  `Indstillinger`, `Interne variable`, `Programmer`, `Hændelser`, `Kommandoer`), and the tree renders
  whatever names the file stores (US-018's stored-caption rule) — not fixed UI captions.
- MUST: In configuration mode a brand-new empty block shows **zero** sections (the empty-section rule of
  US-018 applies to every container).
- MUST: Inserting an empty block **enters programming mode for it immediately** — a blank block exists
  only to be authored, so creating one opens it; `Esc` returns to configuration mode (US-026).

### AC illustrations

- After inserting an empty block under `Garage`, both pane headers read `Tom blok`; the left pane
  shows `Tom blok > {Input, Output, Indstillinger, Interne variable}` and the right shows `Tom blok >
  Programmer > Program > {Hændelser, Kommandoer}`. Pressing `Esc` returns to configuration mode, where
  the block sits under `Garage` with no sections shown and the plain editable function-block icon —
  **no library badge** (contrast US-020's locked templates).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — programming-mode structure, and the configuration-mode view applies
US-018's section-rendering rules (empty sections and `Internal variables` hidden).

---

## US-020 — Unlock a library function block for editing

**As an** IHC installer, **I want** to unlock a supplied library block, **so that** I can modify a tested
block instead of starting from an empty one — and undo the unlock if I did not mean it.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Unlock a library block
  Given a block carrying the library function-block icon is selected
  When I right-click it and choose "Unlock"
  Then the block is unlocked immediately, with no warning dialog
  And the icon changes to the editable function-block icon
  And I can now work with the block as with any custom block (edit variables and program)
  And "Unlock" no longer appears on the block's context menu

Scenario: An unlock can be taken back
  Given I have just unlocked a library block
  When I press Ctrl+Z (US-052)
  Then the block is locked again, its library icon returns, and "Unlock" reappears on its context menu
  And the application keeps running normally

Scenario: Locked blocks resist internal edits
  Given a library block that has not been unlocked
  Then its internals are treated as read-only until "Unlock" is applied
```

### Business rules (unlocking transfers ownership)

- MUST: Unlocking rewrites the block's **identity**, not just its lock flag: the three library-identity
  attributes — `master_schneider_electric`, `master_type` and `master_version` — are **removed**;
  `master_name` is **kept** (it records which library block this came from); `master_programmer` is
  re-stamped to the current user and the `master_date_*` stamps to today; and the icon switches from
  the library glyph to the editable-block glyph. The block's own **name and note are untouched** —
  unlocking is not a rename.
- MUST: The rationale is provenance: once unlocked the logic may be edited, so the block is no longer
  that library block at that version — keeping the identity would misattribute the installer's edits.
  (Consequence: unlock deliberately discards information — re-locking later does not reproduce the
  original library instance; only undo does.)
- MUST: Whether a block presents as a library instance (e.g. the origin group in its properties
  dialog, US-018) is decided by the **library-identity key (`master_type`)**, not by
  `master_name` — an unlocked block still carries `master_name`, and must present as the installer's
  own block: after an unlock its properties dialog drops back to Name + Note only.

### Business rules (reversibility and the view-only gate)

- MUST: Unlocking is an **ordinary undoable edit** — one *Undo* restores the lock **and the entire
  library identity** (US-052). It is not a one-way door.
- MUST: Unlocking raises **no warning**. It needs none: undo is the protection, and it also covers the
  installer who meant to unlock and changed their mind afterwards. **Do not add a confirmation to "protect"
  this action** — a confirmation would guard nothing.
- MUST: **A locked (library) block is view-only until unlocked, and the guard is real.** Its structure and
  program **render for reading**, but every internal edit — inserting/removing pins or variables, editing the
  program — is **refused** on a `locked="yes"` block, both by removing the authoring commands (US-068) and by
  an **engine guard**, so a library block keeps matching its master whoever drives the editor.

### AC illustrations

- A library block shows a distinct library badge (a red-outlined square marker in the tree). Choosing
  *Unlock* switches the badge to the plain function-block icon, signalling it is now editable, and removes
  *Unlock* from its context menu. `Ctrl+Z` puts the badge and the menu item back.
- In a large project most blocks ship locked, so unlock is a routine step on the way to editing a
  library block — not a rare, dangerous one.

### Constraints

- Verification method — **Test**: unlock a locked block, assert it unlocked with **no dialog**, then
  `Ctrl+Z` and assert the block is **locked again** and the application is still running.
- A block becomes locked **either** by shipping from the catalog **or** by being saved to the library
  (US-021); the view-only guard must hold for both. Saving to the library also keeps `Show program` enabled
  on the locked result — the lock gates *editing*, never *viewing*.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the unlock is silent and undoable, undo re-locks the block with the
app still running, and the view-only **UI** gate withdraws the insert/delete/move commands on a locked block.
A single central **engine-level** guard now refuses **every** mutation targeting a locked block's subtree,
whoever drives the editor: the **structural** edits (insert variable/enum/program-row/pin, reorder, and move/copy
whose target parent is inside the locked subtree) and the **in-place** edits (AND/OR condition toggle,
save-current-value, log-mark, enum-state edit, and the function-block rename). A direct engine call throws; a
session command surfaces a clean refusal.

---

## US-021 — Manage own and favourite FB folders

**As an** IHC installer, **I want** to create my own folders, save blocks into them, and keep
frequently used blocks in *Favourites*, **so that** I can reuse blocks quickly and keep
them across software updates.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Create an own folder on disk
  Given IHC OpenVisual's function-block library (`<catalog folder>`) folder
  When I right-click the empty area and choose "New" > "Folder" and name it (e.g. "My_blocks")
  Then a new folder is created; empty folders do not appear in IHC OpenVisual's block list,
    and own folders are preserved when IHC OpenVisual is updated

Scenario: Save a block into a folder
  Given a block is selected in the "Functions" pane
  When I right-click it and choose "Save..." — or press Ctrl+G — give it a name and optional note, and pick a target folder
  Then the block is stored there and its note is shown as a tooltip when hovering the block later

Scenario: Saving a block to the library locks the in-project copy
  Given an unlocked block is selected in the "Functions" pane
  When I save it to a library folder under a name
  Then the in-project block is renamed to that library name and becomes a locked library block
    (its master name/author/date are stamped, its icon switches to the library badge, and the note is applied)
  And it is view-only until unlocked (US-020) — no re-insertion happens

Scenario: Add and use a favourite
  Given a block that lives in a library folder (`<catalog folder>`)
  When I copy it and paste it into the "Favourites" folder
  Then it becomes available under "Favourites" when inserting blocks into a locality
```

### Business rules (saving to the library locks the copy)

- MUST: Saving a block into a library folder (**Save…**, `Ctrl+G`) **transforms the in-project block into a
  locked library instance** — it **renames** the block to the saved name,
  writes `master_name` / `master_programmer` / `master_date_*`, sets `locked="yes"`, applies the library
  badge and the note — **in place, with no re-insertion**. The saved block is thereafter **view-only until
  unlocked** (US-020, US-026), the same as a catalog block.
- MUST: The save also **drops the block's previous library identity** — `master_schneider_electric`,
  `master_type` and `master_version` are removed, the same ownership-transfer rule as US-020's unlock:
  the block is the installer's own library instance now and stops advertising the catalog identity it
  may have come from.
- MUST: This is the **same locked shape** a catalog block carries, so the view-only guard, and the
  byte-fidelity of the `master_*` / `locked` attributes, apply identically whether the block came from the
  catalog or from a user *Save…*.

### Business rules (the save dialog and the saved file)

- MUST: The *Save…* dialog's affirmative button is labelled **Save** — it goes on to write a file —
  unlike ordinary properties dialogs, which keep **OK**.
- MUST: The saved library file contains the block's **definition without any project wiring** — links
  are project data, not part of the block type — and remains a complete, valid library file that can
  be inserted into any project.

### AC illustrations

- Hovering a saved block shows the note entered in its *Save...* dialog as a tooltip.

### Constraints

- Verification method — **Demonstration** of folder creation, block save, and favourite reuse; and
  **Inspection** that empty folders are hidden and own folders survive an update.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — Save block works and folder/favourites management is adapted for the
install-free design, the written `.ifb` master always carries `locked="yes"` even when exported from an unlocked
block, and saving now **auto-locks the in-project copy** in place: after the `.ifb` write, the block is renamed to
the saved name, `master_*`-stamped, badged and set `locked="yes"` via an undoable command — so the T003/T004
guard makes it view-only (Show program stays), and one undo restores the prior unlocked block. The export runs
first, so a failed export leaves the project unmutated.
