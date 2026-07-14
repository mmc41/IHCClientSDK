---
version: 0.1.0
last-updated: 2026-07-12
status: draft
---

# E15 — Structural editing (delete / move / reorder / copy‑paste)

> **Current scope:** ✅ **In scope (foundational, cross‑cutting)** — delete, move, reorder and
> copy/paste are the Delete‑and‑relocate half of project CRUD that every editing epic (E2–E9) needs but
> none owns. They generalise the single‑type stories already written (locality delete, US-009; block
> library‑folder copy, US-021) into node‑agnostic operations the way E11–E14 generalise interaction,
> icons, tooltips and undo.

**Goal:** Let an IHC installer remove, relocate, reorder and duplicate **any** project node — a
locality, a product, a function block, a variable, a program element or a link — with one consistent
set of gestures, so a mistake or a change of plan can be fixed without rebuilding the tree by hand and
without leaving orphaned logic behind.

**Scope:** the general *Delete* of any node and its reference cascade; moving a node to another
container (across localities/sections); reordering siblings within a container (which drives report
order, US-040); and copy/paste of a node subtree within the project via the toolbar Cut/Copy/Paste and
`Ctrl+X`/`Ctrl+C`/`Ctrl+V` (US-001, US-045). **Scope excludes:** the single‑type instances already
specified — locality delete/cascade (US-009) and saving a block into an on‑disk library/*Favourites*
folder (US-021); removing or editing a *link* (US-057, US-058, E6); undo/redo of these operations, which
is the general E14 guarantee (US-052); and the placement‑legality rules that gray out illegal insert
targets (surfaced by the engine's `CanInsert`/`GetInsertableAt`, noted where relevant).

**Acceptance criteria (epic level):**
- MUST: Any project node can be deleted; deleting a node that other logic references is confirmed first
  and cascades the dependent link halves and program rows as **one** undoable step, generalising US-009.
- MUST: Any node can be moved to another legal container, and siblings can be reordered within a
  container; a move/reorder preserves the node's identity (its IHC resource ids do not change).
- SHOULD: Any node subtree can be copied and pasted elsewhere in the project as an independent duplicate
  with fresh resource ids; links whose other end lies outside the copy are not carried into the paste.
- MUST: Every operation here confirms in the status bar and is reversible via *Undo* (`Ctrl+Z`,
  US-052).

**Readiness:** Ready — with one open item on paste's cross‑epic reach (see US-056) and the delete
reference‑policy confirmation copy (US-053), both flagged below.

---

## US-053 — Delete any project node

> **Cross‑cutting:** this generalises **US-009** (delete a locality with contents) to every deletable
> node — product, function block, variable/resource, program element, and link row — with one
> confirm‑and‑cascade rule. US-009 remains the worked locality instance; this story fixes the behaviour
> for the other node types so each does not get an ad‑hoc, inconsistent delete.

**As an** IHC installer, **I want** to delete any node I select — being warned when other logic still
depends on it — **so that** I can remove a product, block, variable or program element without silently
orphaning the links and commands that referenced it.

**Scope excludes:** the locality‑specific worked example (US-009); removing a *link* row, which has its
own story because it deletes a reciprocal pair rather than a subtree (US-057).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Delete a leaf node with no references
  Given a node with no dependent links or program references is selected
    (e.g. a freshly inserted product not yet linked, or an unused variable)
  When I right-click it and choose "Delete", or press Delete, or choose "Edit" > "Delete"
  Then the node and its subtree are removed from both panes
  And the status bar confirms the deletion by node name

Scenario: Delete a node that other logic references (confirm + cascade)
  Given a node that is referenced elsewhere is selected
    (e.g. a product output linked to a function block, or a variable used in a command/condition)
  When I choose "Delete"
  Then a confirmation dialog appears naming what will also be removed, and I must accept it to proceed
  And on acceptance the node, the reciprocal "link to"/"link from" halves that pointed into it, and the
    command/condition/event rows that referenced it are all removed together
  And the removal is a single step on the undo history (US-052)

Scenario: Decline the confirmation
  Given the delete confirmation is shown
  When I decline it
  Then nothing is deleted

Scenario: Delete is equivalent across all three activation routes
  Given a deletable node is selected
  Then "Delete" is reachable by right-click, by "Edit" > "Delete", and by the Delete key,
    with identical results (US-044)
```

### Business rules (reference policy)

- MUST: Deleting a node removes its **whole subtree**; the retired IHC resource ids are not reused.
- MUST: The reciprocal halves of any follow‑link or scene link that pointed **into** the deleted subtree
  are removed automatically, so no dangling `link to`/`link from` row is left behind (the link bijection
  stays intact and the project stays saveable).
- MUST: Program rows (a command, condition or event) whose only reference was into the deleted subtree
  are removed **whole** as part of the cascade — matching the US-009 locality semantics — while their
  parent groups are kept (an emptied *Commands*/*Conditions* group survives).
- SHOULD: When a node cannot be safely cascaded because another element still binds it in a way the
  cascade does not cover (e.g. a *scenes* binding or an enumerator type still in use), the app **refuses
  the delete and explains what to rewire first**, rather than leaving a broken reference.

### AC illustrations

- Deleting a `<product>` whose `<pin>` a block drove removes the product **and** the block's
  `link to`/`link from` rows and the command that switched it — one `Ctrl+Z` brings all of it back
  (mirrors the US-009 cascade, one level down at the product instead of the locality).
- Deleting an unused *Internal variable* `Flag = OFF` that no program references removes just that row,
  no confirmation needed.

### Constraints

- Verification method — **Demonstration**: delete a referenced product and an unused variable, and
  confirm the confirm‑gate, the cascade of link halves + program rows, the single‑step undo, and the
  three‑route equivalence.
- Note: the confirm‑and‑cascade behaviour and the "refuse when a binding cannot be cascaded" guard
  are grounded in the project engine's delete contract (the US-009 cascade generalised); the exact
  confirmation‑dialog wording per node type is to be confirmed during implementation. (R‑note —
  does not block the story.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-054 — Move a node to another container

**As an** IHC installer, **I want** to move a product, function block or variable to a different
locality or section, **so that** I can correct where something lives without deleting and re‑creating it
(and losing its documentation, addressing and links).

**Scope excludes:** reordering siblings within the same container (US-055); copying rather than moving
(US-056).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Move a product to another locality
  Given a product sits under one locality in the "Installation" pane
  When I move it onto another locality
    (drag it onto the target locality, or Cut it with Ctrl+X and Paste with Ctrl+V onto the target)
  Then the product is re-parented under the target locality, keeping its documentation, terminal
    addressing and every link it participates in
  And the same relocation is reflected in the "Functions" pane
  And the status bar confirms the move

Scenario: Identity is preserved on a move
  Given a moved product/output that maps to an IHC resource id (shown in its tooltip, US-048)
  Then its resource id is unchanged after the move, so existing links and controller cross-references
    still resolve

Scenario: Illegal targets are not accepted
  Given I am moving a node
  Then I cannot drop it into itself or into one of its own descendants, and I cannot drop it into a
    container that may not hold that node type (the target does not accept the drop)

Scenario: Move is available without drag
  Given a node is selected
  Then Cut (Ctrl+X) then Paste (Ctrl+V) onto a legal target performs the same move as dragging,
    so the operation is not drag-only (US-044)
```

### AC illustrations

- Moving a `<product>` from `Living room` to `Kitchen` leaves its two input pins, their terminal addressing
  and the block link on `<pin>` intact; the block's `link from` row still reads the button's
  path, now under `Kitchen`.
- Dragging a locality onto itself is rejected; the tree does not change.

### Constraints

- Verification method — **Demonstration** of both the drag route and the Cut/Paste route, that ids and
  links survive the move, and that self/descendant and illegal‑container targets are refused.
- Note: id‑preserving reparent and the self/descendant guard are grounded in the engine's move
  contract ("ids never change on a move"); the drag affordance and drop‑target highlighting are to be
  confirmed during implementation. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (Cut/Paste move route).

---

## US-055 — Reorder nodes within a container

**As an** IHC installer, **I want** to change the order of siblings under a container — localities under
*Localities*, products under a locality, variables within a section — **so that** the tree and the
generated reports present components in the order I choose (US-040 documents products *in the order they
appear* in the *Installation* pane).

**Scope excludes:** moving a node to a *different* container (US-054).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Reorder siblings by moving one up or down
  Given several siblings under one container (e.g. the ten default localities under "Localities")
  When I move one sibling to a new position among its siblings
    (drag it above/below another sibling, or Cut and Paste it at the target position)
  Then the sibling takes the new position and the others close up around it
  And the new order is reflected identically in both panes
  And the status bar confirms the reorder

Scenario: Report order follows tree order
  Given products have been reordered under a locality
  When an installation report is generated (US-040)
  Then the products are documented in the new order

Scenario: Reorder preserves identity and links
  Given a reordered node
  Then its resource ids and its links are unchanged (reordering only changes position)
```

### AC illustrations

- With `Outdoors` last under `Localities`, moving it above `Garage` reorders the locality list in both
  panes; a later installation report lists the localities in the new order.

### Constraints

- Verification method — **Demonstration** that a reorder changes sibling position in both panes and in
  report output, and preserves ids/links.
- Note: sibling reordering is the same id‑preserving move as US-054 with an in‑container target
  index; whether reorder is offered by drag, by a *Move up/down* command, or both is to be
  confirmed during implementation. IHC OpenVisual SHOULD offer at least one non‑drag route
  (US-044). (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (Move up / Move down non-drag route).

---

## US-056 — Copy and paste a node

> The toolbar carries **Cut / Copy / Paste** and the shortcut set documents `Ctrl+X` / `Ctrl+C` /
> `Ctrl+V` (US-001, US-045), but no story defined what Copy/Paste *do* to a project node. This story
> fixes that. (Cut+Paste as a **move** is covered by US-054/US-055; this story is the **duplicate**.)

**As an** IHC installer, **I want** to copy a node and paste it elsewhere as an independent duplicate,
**so that** I can reuse a configured product, block or subtree without rebuilding it.

**Scope excludes:** saving a block into an on‑disk library/*Favourites* folder (US-021, a different,
disk‑level copy); moving a node (US-054/US-055).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Copy a product and paste it into another locality
  Given a configured product is selected
  When I Copy it (Ctrl+C or toolbar Copy) and Paste (Ctrl+V or toolbar Paste) onto a target locality
  Then an independent duplicate is inserted under the target, carrying the source's documentation and
    structure, but with its own fresh IHC resource ids (the original is left unchanged)
  And the status bar confirms the paste

Scenario: Paste target must be legal
  Given something is on the clipboard
  When I paste onto a container that may not hold that node type
  Then the paste is refused (or the Paste action is disabled for that target)

Scenario: Links to nodes outside the copy are not carried over
  Given the copied node had a link whose other end lies outside the copied subtree
  When I paste the copy
  Then the paste does not include that external link half (the duplicate starts unlinked on that pin);
    links wholly inside the copied subtree are duplicated and remain connected within the copy

Scenario: Paste is available three ways
  Given a node is on the clipboard and a legal target is selected
  Then Paste is reachable by toolbar, by "Edit" > "Paste", and by Ctrl+V (US-044)
```

### AC illustrations

- Copying a `<product>` configured under `Living room` and pasting it under `Room` yields a
  second, independent button with its own resource ids; editing the copy does not affect the original,
  and the copy shows no `link from`/`link to` rows for links the original had to blocks outside the copy.

### Constraints

- Verification method — **Demonstration** that a paste produces an independent duplicate with fresh ids,
  refuses illegal targets, and drops external links.
- **Open item — cross‑epic paste reach:** copy/paste of a single product/subtree is engine‑verified;
  copy/paste of a **function block** and of **program elements** is exercised less and its exact
  behaviour there is not fully pinned. Confirm block/program paste during implementation before
  treating it as fixed. (R‑note.)

**Readiness:** Ready (product/subtree paste); block/program paste carries the open item above.

**Implementation status:** ✅ Implemented. Epic E15 complete.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-053 | Delete any project node | Ready | E15 | Must | US-009, US-052 |
| US-054 | Move a node to another container | Ready | E15 | Must | US-044, US-052 |
| US-055 | Reorder nodes within a container | Ready | E15 | Should | US-040, US-054 |
| US-056 | Copy and paste a node | Ready | E15 | Should | US-001, US-044, US-052 |
