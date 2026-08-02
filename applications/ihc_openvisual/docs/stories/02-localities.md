---
version: 0.3.0
last-updated: 2026-08-02
status: draft
---

# E2 — Locality management

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

- MUST: Both panes show a root node with an expand/collapse control, expanded by default, labelled
  with the **name the project file itself gives its locality container** (the standard template names it
  *Lokaliteter*); *Localities* stands in only when a file leaves that container unnamed.
- MUST: Under the root are exactly the ten localities of the standard template, in its order:
  **Stue, Entré, Køkken, Soveværelse, Værelse, Bad, Bryggers, Garage, Kælder, Udendørs**. Locality
  names are project *data*, not UI text, so a new project starts from the file format's own default
  names and a project authored here is interchangeable with one authored in any other IHC editor.
  Renaming them to suit the installation is the user's first edit, not the app's.
- MUST: Each locality renders as a node with a small square (checkbox-style) icon followed by its
  bold name; the same ten localities appear in the *Functions* pane as in the *Installation* pane.
- SHOULD: A locality is a container: expanding it reveals the products (Installation pane) or
  function blocks (Functions pane) placed in it; when empty it has no expand control.
- MUST: When a project is opened, every locality starts **collapsed** — only the root is expanded.
  A populated locality is never auto-expanded by the act of opening: the whole-installation overview
  is the initial state, and drilling in is the installer's move.
- SHOULD: A collapsed locality opens automatically around its **first** inserted child, so the
  arrival of content is visible; gaining further children does not re-open a locality the installer
  has closed (expansion-state rules: US-070).
- MAY: The *Functions* pane groups a locality's function blocks under the same locality node used
  in the *Installation* pane, keeping one shared locality structure across the two views.

### AC illustrations

- A freshly created project shows `Lokaliteter > {Stue, Entré, Køkken, Soveværelse, Værelse, Bad,
  Bryggers, Garage, Kælder, Udendørs}` identically in both panes.
- Reopening a saved project in which `Stue` holds products shows `Stue` closed like every other
  locality; expanding it is a click, not something the open did.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-007 — Rename a locality via Properties

**As an** IHC installer, **I want** to rename a locality and attach a note, **so that** the tree
reflects the real rooms of the installation and carries documentation text.

**Scope excludes:** renaming products or function blocks (same dialog pattern, different stories).

### Acceptance criteria (Given-When-Then)

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

### Business rules (the dialog's field set)

- MUST: The dialog carries **exactly two** fields — **Name** and **Note** — plus its OK/Cancel buttons.
  Nothing else: a locality has no placement, no addressing and no type of its own.
- MUST: The dialog title follows the pattern `Edit <current name> properties`.
- MUST: `F2` on a selected locality opens it, and so does double-click (US-067) and right-click >
  *Properties*.

### AC illustrations

- Renaming `Living room` to `Living room & Kitchen "open"` with a note updates the node in both panes to
  `Living room & Kitchen "open"`; special characters (`&`, `"`, Danish/Swedish letters) are accepted as typed
  and shown verbatim in the tree.
- The dialog title always follows the pattern `Edit <current name> properties`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-008 — Add a new locality

**As an** IHC installer, **I want** to add a locality under *Localities*, **so that** I can represent
a room the defaults do not cover.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Insert a new locality under the root
  Given the "Localities" root is selected in the "Installation" pane
  When I right-click "Localities" and choose to insert a locality
  Then a new locality node is appended under the locality root at the bottom of the list
  And it carries the template's placeholder name "Lokalitet" — a name is project data, so the
    placeholder is the file format's own and the installer renames it next
  And the status bar names the container the tree shows, e.g. "Lokalitet was inserted under Lokaliteter"
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

### Business rules (what an insert does, and does not, do)

- MUST: The new locality is appended **last** among its siblings — not inserted at the caret, not sorted
  into place.
- MUST: It is created with a **default name**, ready to rename.
- MUST: **No properties dialog opens** on insert. The installer renames it on demand via US-007.

### AC illustrations

- With the locality root selected, inserting a locality yields a new node named `Lokalitet` at the
  bottom of the tree (below `Udendørs`), selected, with **no dialog opening**, and the status bar
  showing `Lokalitet was inserted under Lokaliteter`.
- *Insert locality* is offered only where a locality can go: a locality's own context menu does not
  carry it, because a locality is not a container for other localities.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-009 — Delete a locality with contents

**As an** IHC installer, **I want** to delete a locality, being warned when it still holds products,
**so that** I can remove a room without silently orphaning the logic that used its products.

### Acceptance criteria (Given-When-Then)

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

### Business rules (when and how the confirmation is asked)

- MUST: Deleting an **empty** locality proceeds silently — no confirmation is shown.
- MUST: The confirmation for a non-empty locality offers exactly **two** answers — proceed or
  decline — and its message names the locality and states what deleting it also removes.
- MUST: The confirmation is about what the locality **contains**; it is the containment that
  triggers it, not the locality's type (the general rule is US-053's).
- MUST: The locality **root** itself is not deletable — no route (menu, context menu or Delete key)
  offers or performs it.

- Deleting a locality that holds a lamp output which a function block switched removes the locality, the
  product, and the function-block command/condition that referenced that output — the installer is
  warned before this cascade happens.

### Constraints

- Verification method — **Demonstration**: delete a non-empty locality and confirm both the
  confirmation gate and the cascade removal of dependent commands/conditions.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
