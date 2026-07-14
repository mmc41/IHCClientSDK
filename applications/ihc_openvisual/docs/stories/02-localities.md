---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E2 — Locality management

> **Implementation status (2026-07-13):** ✅ **Implemented** — US-006 (default tree), US-007 (rename via
> Properties), US-008 (add), US-009 (delete + confirmation/cascade) are all done and covered by
> `safe_visual_tests` (36 green) with the SDK cascade in `safe_project_tests`. Per-story detail below.

> **Current scope:** ✅ **In scope** — locality create / rename / delete is project CRUD.

**Goal:** Let an IHC installer model the rooms and places of the installation as a *Localities*
tree — renaming the defaults, adding new ones, and deleting ones not needed — so that
every product and function block has a meaningful location.

**Scope:** the *Localities* root and its child locality nodes in both panes; rename via the
*Properties* dialog; add; delete (including the cascade when a locality holds products).
**Scope excludes:** the products/function blocks placed *inside* localities (E3–E5).

**Acceptance criteria (epic level):**
- MUST: The installer can rename any locality, add a new locality under *Localities*, and delete a
  locality.
- MUST: Renaming and adding are reflected identically in both the *Installation* and *Functions* panes
  and confirmed in the status bar.
- SHOULD: Deleting a locality that contains products requires explicit confirmation and cascades to the
  commands/conditions that referenced those products.

**Readiness:** Ready.

---

## US-006 — View the default locality tree

**As an** IHC installer, **I want** the new project to open with a set of default localities shown in
both panes, **so that** I have realistic starting rooms to adapt to my installation.

### Acceptance criteria (Checklist)

- [x] MUST: Both panes show a root node **Localities** with an expand/collapse control, expanded by
  default.
- [x] MUST: Under *Localities* are exactly these ten localities, in this order: **Living room, Hall, Kitchen,
  Bedroom, Room, Bathroom, Utility room, Garage, Basement, Outdoors**.
- [x] MUST: Each locality renders as a node with a small square (checkbox‑style) icon followed by its
  bold name; the same ten localities appear in the *Functions* pane as in the *Installation* pane.
- [x] SHOULD: A locality is a container: expanding it reveals the products (Installation pane) or
  function blocks (Functions pane) placed in it; when empty it has no expand control.
  *(Avalonia `TreeView` shows the expander only when a node has children, so an empty room has none;
  product/FB children arrive with E3–E5.)*
- [x] MAY: The *Functions* pane groups a locality’s function blocks under the same locality node used
  in the *Installation* pane, keeping one shared locality structure across the two views.

### AC illustrations

- A freshly created project shows `Localities > {Living room, Hall, Kitchen, Bedroom, Room, Bathroom,
  Utility room, Garage, Basement, Outdoors}` identically in both panes.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** A new project OpenVisual authors seeds the ten English
default localities — English is the product language (product.md), while a *loaded* file keeps its own
names verbatim. The SDK `CreateNew` still produces the vendor's byte-identical Danish template; the app
renames the ten defaults by position in `DefaultLocalities.ApplyEnglish` (an attribute-only edit via
`project.Edit()`, traced on `Telemetry.ActivitySource`, applied only on New — never on Load), leaving the
SDK byte-fidelity oracles untouched (662 project tests green). Both panes render a `Localities`-rooted,
expanded tree of the ten rooms with the square `locality.svg` glyph and **bold** labels
(`TreeNodeViewModel.IsBold` → `BoolToFontWeightConverter`). Tested: `MainWindowViewModelTests`
(names + order + bold, both panes) and `SmokeTests.MainWindow_RendersDefaultLocalities_InInstallationTree`
(headless Skia render). Visual render verified; OpenObserve reported no errors.

---

## US-007 — Rename a locality via Properties

**As an** IHC installer, **I want** to rename a locality and attach a note, **so that** the tree
reflects the real rooms of the installation and carries documentation text.

**Scope excludes:** renaming products or function blocks (same dialog pattern, different stories).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Rename a locality from its context menu
  Given the "Installation" pane shows the locality "Living room"
  When I right-click "Living room" and choose "Properties"
  Then a dialog titled "Edit Living room properties" opens
  And it shows a "Name" single-line field pre-filled with "Living room" and selected,
    and a multi-line "Note" field below it, with "OK" and "Cancel" buttons
  When I change "Name", optionally type a "Note", and click "OK"
  Then the locality node's label updates to the new name in both panes
  And the status bar confirms the change

Scenario: Open the same dialog by keyboard
  Given the locality "Living room" is selected
  When I press F2
  Then the "Edit Living room properties" dialog opens (same as the context-menu route)

Scenario: Cancel discards the edit
  Given the "Edit Living room properties" dialog is open with edits typed
  When I click "Cancel"
  Then the locality keeps its original name and note
```

### AC illustrations

- Renaming `Living room` to `Living room & Kitchen "open"` with a note updates the node in both panes to
  `Living room & Kitchen "open"`; special characters (`&`, `"`, Danish/Swedish letters) are accepted as typed
  and shown verbatim in the tree.
- The dialog title always follows the pattern `Edit <current name> properties`.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** A modal `PropertiesWindow` (title `Edit <name> properties`,
single-line `Name` pre-filled + selected, multi-line `Note`, OK/Cancel) opens from the tree node's
right-click **Properties** item and from **F2** on the selected node (both route through
`MainWindowViewModel.PropertiesCommand`; view wiring in `MainWindow.axaml.cs`). OK commits via
`ProjectSession.RenameLocalityAsync` — an id-addressed `ElementRef.SetAttribute("name"/"note")` edit on
`project.Edit()`, traced on `Telemetry.ActivitySource`, that marks the project dirty and records the change
(now the first production caller of the every-Nth-change crash backup, closing the US-005 wiring gap); the
rename shows in **both** panes and the status bar confirms `Renamed to <name>.`. Cancel keeps the original
name and note. Special characters (`&`, `"`, Danish/Swedish letters) round-trip verbatim (verified in the
render). Tested: `MainWindowViewModelTests` (rename both panes + status + dirty; Cancel keeps original; note
pre-fill on reopen) and `SmokeTests.PropertiesWindow_ShowsNameAndNoteFields`. Visual render verified;
live app run + OpenObserve reported no errors.

---

## US-008 — Add a new locality

**As an** IHC installer, **I want** to add a locality under *Localities*, **so that** I can represent
a room the defaults do not cover.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a new locality under the root
  Given the "Localities" root is selected in the "Installation" pane
  When I right-click "Localities" and choose to insert a locality
  Then a new locality node is appended under "Localities" at the bottom of the list
  And the status bar reads "Locality was inserted under Localities"
  And the new node appears in both panes

Scenario: Name the new locality
  Given a newly inserted locality is selected
  When I open its properties (right-click > "Properties", or F2) and set "Name"
  Then the node label updates to the chosen name (US-007)

Scenario: Insertion targets the current selection
  Given some other node (not "Localities") is selected
  When I intend to add a locality
  Then I first select the "Localities" root, because a new locality is added under the
    currently selected container
```

### AC illustrations

- With `Localities` selected, inserting a locality yields a new node named `Locality` at the bottom
  of the tree (below `Outdoors`), selected, with the status bar showing
  `Locality was inserted under Localities`.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** Right-click the *Localities* root → **Insert locality**
(a context-menu item gated by `TreeNodeViewModel.CanInsertLocality`, so it appears only on the root; room
nodes show *Properties* instead). It commits via `ProjectSession.AddLocalityAsync` → the new SDK primitive
`ProjectEditor.AddGroup(name)` (always appends a fresh room, unlike find-or-seed `Group`), traced on
`Telemetry.ActivitySource`, marking the project dirty and recording the change. The new node — named
`Locality`, bold, addressable — is appended **last** (below *Outdoors*) in **both** panes, is **selected**
in the Installation pane (`SelectedNode` two-way binding), and the status bar reads exactly
`Locality was inserted under Localities`. It is immediately renamable via US-007. Repeated inserts yield
distinct same-named rooms (as IHC Visual does). Tested: engine `GroupEditTests.AddGroup_AlwaysAppendsNewRoom…`
(safe_project_tests, 663 green — byte-fidelity intact), `MainWindowViewModelTests` (append + name + select +
status + dirty, and insert-then-rename), and `SmokeTests.MainWindow_AfterInsertLocality_RendersNewNode`.
Visual render verified; OpenObserve reported no errors.

---

## US-009 — Delete a locality with contents

**As an** IHC installer, **I want** to delete a locality, being warned when it still holds products,
**so that** I can remove a room without silently orphaning the logic that used its products.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Delete an empty locality
  Given a locality with no products is selected
  When I right-click it and choose "Delete"
  Then the locality is removed from both panes

Scenario: Delete a locality that contains products
  Given a locality that contains one or more products is selected
  When I choose "Delete"
  Then a confirmation dialog appears and I must accept it to proceed
  And on acceptance the locality and its products are removed
  And the commands, conditions and other references that used those products are also removed automatically

Scenario: Decline the confirmation
  Given the delete confirmation for a non-empty locality is shown
  When I decline it
  Then nothing is deleted
```

### AC illustrations

- Deleting a locality that holds a lamp output which a function block switched removes the locality, the
  product, and the function‑block command/condition that referenced that output — the installer is
  warned before this cascade happens.

### Constraints

- Verification method — **Demonstration**: delete a non‑empty locality and confirm both the
  confirmation gate and the cascade removal of dependent commands/conditions.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** Right-click a locality → **Delete** (context-menu item gated by
`TreeNodeViewModel.CanDelete`). It commits via `ProjectSession.DeleteLocalityAsync`: an **empty** room is removed
silently; a room whose model still holds products/function blocks first raises a confirmation
(`IDialogService.ConfirmAsync`) and, on acceptance, deletes with
`ProjectEditor.DeleteById(id, DeleteReferencePolicy.CascadeReferences)` — the vendor US-009 row-only cascade
(ENG2-A5) that also removes the referencing commands/conditions; declining deletes nothing. Traced on
`Telemetry.ActivitySource`, errors logged; the removal shows in **both** panes and the status bar reads
`Deleted <name>.`. Tested: `MainWindowViewModelTests` — empty delete (both panes + dirty, no prompt), and the
confirm/decline gate over a locality made non-empty via the built-in-catalog empty function block (no
controller); `SmokeTests.MainWindow_AfterDeleteLocality_RemovesNode`; the SDK cascade itself is covered by
`DeleteCascadeTests`. Visual render verified; OpenObserve reported no errors. *(Note: the in-app non-empty
cascade will only be reachable through the UI once product/FB insertion lands in E3–E5; the gate + cascade are
already exercised at the session/SDK layer.)* **Epic E2 (US-006–US-009) complete.**
