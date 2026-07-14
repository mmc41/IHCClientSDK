---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E5 — Function blocks: insert & structure

> **Implementation status (2026-07-13):** ✅ **Implemented** — US-018 (insert library block), US-019 (empty block),
> US-020 (unlock) done; US-021 core (Save block) done with folder/favourites adapted for the install-free design.
> Covered by `safe_visual_tests` (86 green). Per-story detail below.

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

**Implementation status:** ✅ **Implemented.** Library function blocks insert from **Library ▸ Insert function
block** and the locality right‑click **Insert function block** — a catalog‑driven nested menu of the library
folders (`FunctionBlocksMenu`, from `GetAvailableFunctionBlocks()` grouped by the `NN.`‑prefixed `CategoryPath`
via `CatalogMenu.BuildFunctionBlocks`, keyed by `MasterType`). The block inserts under the selected locality via
`ProjectSession.AddFunctionBlockAsync` → `ProjectEditor.Group(id).AddFunctionBlock(def)` (fresh ids; variable
sections + program materialized by the SDK), traced, marks dirty; status reads
`Function block '<block>' has been inserted under <locality>`. It nests in the **Functions** pane only, carries
the **library FB icon** (`fb-lk.svg`; an unlocked block would use `fb-editable.svg`), and expands to its four
variable sections — **Input / Output / Settings / Internal variables** — each holding its typed pins with inline
default values (`name = value`). Tested: `MainWindowViewModelTests` (nests in Functions pane with the four
sections + pins, Installation‑only exclusion; menu leaf targets selection + exact status; menu has catalog folders)
and `SmokeTests.MainWindow_AfterInsertFunctionBlock_RendersBlockWithSections`. Render verified (block expands to
Input/Output/Settings/Internal variables with pins); live app + OpenObserve no errors. *(US-019 empty block,
US-020 unlock, US-021 folders next.)*

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

**Implementation status:** ✅ **Implemented.** An empty function block inserts from the locality right‑click
**Empty function block** and **Ctrl+Shift+B**; it is named **Empty block**, scaffolded from the catalog's
`Tom blok` template via `ProjectSession.AddEmptyFunctionBlockAsync` → `ProjectEditor.Group(id).AddEmptyFunctionBlock`
(new SDK accessor `ProjectAppService.GetEmptyFunctionBlockTemplate`), traced, marks dirty; the status bar reads
`Empty block was inserted under <locality>`. It appears in the **Functions** pane with the **editable** FB icon
(`fb-editable.svg`) and expands to exactly the four sections — **Input / Output / Settings / Internal variables**
(empty, no pins). The block renames via the Properties route (F2 / right‑click) using the shared Name/Note dialog
(`Edit <name> properties`). Tested: `MainWindowViewModelTests` (four sections + name; command targets selection +
exact status; F2 renames the block) and `SmokeTests.MainWindow_AfterInsertEmptyBlock_RendersEmptyBlock`; SDK addition
kept `safe_project_tests` green (663). Render verified (Empty block expands to the four empty sections); live app +
OpenObserve no errors. *(F3 programming‑mode switch is US-026, deferred. US-020 unlock, US-021 folders next.)*

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

**Implementation status:** ✅ **Implemented.** A locked library function block (all catalog blocks are inserted
`locked="yes"`) is unlocked via the right‑click **Unlock** item — offered only on a locked block
(`TreeNodeViewModel.IsLockedFunctionBlock`). `ProjectSession.UnlockFunctionBlockAsync` clears the `locked` flag by id
(`SetAttribute("locked","no")`, dropped as the DTD default), traced, marks dirty; status reads `Unlocked <name>.`. On
the tree rebuild the block's icon switches from the **library** glyph (`fb-lk.svg`) to the **editable** glyph
(`fb-editable.svg`) — `BuildFunctionBlockNode` picks the icon from the live `locked` attribute. An empty block (already
`locked="no"`) is never offered Unlock. Tested: `MainWindowViewModelTests` (unlock clears the lock + switches the icon;
command confirms and an empty block is not lockable). Render verified (locked library icon vs unlocked editable icon
side by side); live app + OpenObserve no errors. *(Locked‑block read‑only enforcement on internals is inherent — the
internal‑edit surfaces are E7, gated on the same `locked` flag. US-021 own/favourite folders next.)*

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

**Implementation status:** 🟡 **Core implemented (Save block); folder/favourites management adapted for the
install‑free design.** The high‑value **Save a block for reuse** (scenario 2) is done: right‑click a function block
▸ **Save block…** (or **Ctrl+G**) opens the Name/Note dialog (the note becomes the saved block's tooltip on
re‑import), then a native `.ifb` save picker; `ProjectSession.SaveFunctionBlockAsync` lifts the placed block to a
keyless user‑block definition (new SDK `ProjectEditor.FunctionBlock(id)` → `FunctionBlockRef.ExportDefinition`) and
writes it with `CatalogFileWriter` — a read‑only export (no project mutation), traced; status `Saved function block
'<name>'.`. The written `.ifb` re‑imports cleanly (`ProjectAppService.ImportCatalogFile`), which the test asserts by
round‑trip. **Scenarios 1 (own on‑disk folders) and 3 (Favourites)** are **adapted / deferred**: OpenVisual is
*install‑free* (embedded `BuiltInCatalog`, product.md) so it has no fixed on‑disk library‑folder tree to create
folders in or pin favourites to — the installer instead saves reusable blocks to any `.ifb` via the picker and can
re‑import them; a full library‑folder/Favourites manager presupposes an IHC‑Visual‑style on‑disk catalog install and
is out of scope for the current design. Tested: `MainWindowViewModelTests` (Save writes a re‑importable `.ifb`;
command prompts + writes + confirms). This turn also fixed a **per‑pane selection** defect so a Functions‑pane
function block becomes the active node (its Unlock/Save/Properties context commands now act on it live) — verified by
`SmokeTests.FunctionsTree_SelectingFunctionBlock_MakesItTheActiveNode`. SDK addition kept `safe_project_tests` green
(663); live app + OpenObserve no errors.
