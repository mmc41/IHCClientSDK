---
version: 0.2.0
last-updated: 2026-07-16
status: draft
---

# E2 — Locality management

> **Implementation status:** ✅ Implemented — and measured **fully aligned** with IHC Visual across insert
> (F‑025), the rename/note dialog (F‑037), and both delete paths (F‑023, F‑038). **This epic is the
> comparison's regression baseline**: it is the one editing area where every measured cell came back
> aligned, so a future change that diverges here is a regression, not a decision.

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

**Implementation status:** ✅ Implemented.

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

### Business rules (the dialog's field set)

- MUST: The dialog carries **exactly two** fields — **Name** and **Note** — plus its OK/Cancel buttons.
  Nothing else: a locality has no placement, no addressing and no type of its own.
- MUST: The dialog title follows the pattern `Edit <current name> properties`.
- MUST: `F2` on a selected locality opens it, and so does double‑click (US-067) and right‑click >
  *Properties*.

> **Confirmed 2026‑07‑16 — regression baseline, fully aligned.** IHC Visual's `Rediger <name> egenskaber`
> is exactly Navn + Note + OK/Annuller, and IHC OpenVisual's *Edit `<name>` properties* is exactly Name +
> Note + OK/Cancel — **same field set, same title pattern**, translated (language is an allowed difference).
> This is the epic's regression baseline: it is worth stating as a measured fact because the **product**
> dialog, which looks like the same kind of dialog, diverges hard (US-011/US-012) — so the divergence there
> is product‑specific, not a general dialog problem. Evidence: `RESULTS.md` **F‑037**
> (`S02b\50-locality-props-vis.png` vs `50-locality-props-ov.png`, F2 on the same locality) and **F‑014**.

### AC illustrations

- Renaming `Living room` to `Living room & Kitchen "open"` with a note updates the node in both panes to
  `Living room & Kitchen "open"`; special characters (`&`, `"`, Danish/Swedish letters) are accepted as typed
  and shown verbatim in the tree.
- The dialog title always follows the pattern `Edit <current name> properties`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — measured **fully aligned** with IHC Visual (F‑037).

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

### Business rules (what an insert does, and does not, do)

- MUST: The new locality is appended **last** among its siblings — not inserted at the caret, not sorted
  into place.
- MUST: It is created with a **default name**, ready to rename.
- MUST: **No properties dialog opens** on insert. The installer renames it on demand via US-007.

> **Confirmed 2026‑07‑16 — regression baseline, fully aligned.** Insert on both apps appends at the same
> index (24 of 24), gives the new node a default name (IHC Visual `Lokalitet`, IHC OpenVisual `Locality` —
> language is an allowed difference), and opens **no dialog**. Evidence: `RESULTS.md` **F‑025** (placement
> verified by index probe on both). ⚠ The **no‑dialog‑on‑insert** rule is the one to hold on to: the
> equivalent *product* story asserted the opposite and was wrong (US-011's corrected auto‑open MUST, F‑027)
> — localities were right all along.

### AC illustrations

- With `Localities` selected, inserting a locality yields a new node named `Locality` at the bottom
  of the tree (below `Outdoors`), selected, with **no dialog opening**, and the status bar showing
  `Locality was inserted under Localities`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — placement, default name and the no‑dialog rule are measured
**aligned** with IHC Visual (F‑025).

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

**Implementation status:** ✅ Implemented. Epic E2 complete.
