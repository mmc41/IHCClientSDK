---
version: 0.5.0
last-updated: 2026-08-02
status: draft
---

# E15 — Structural editing (delete / move / reorder / copy-paste)

> **Scope:** In scope (foundational, cross-cutting) — delete, move, reorder and copy/paste are the
> Delete-and-relocate half of project CRUD that every editing epic (E2–E9) needs but none owns. They
> generalise the single-type stories already written (locality delete, US-009; block library-folder copy,
> US-021) into node-agnostic operations.

**Goal:** Let an IHC installer remove, relocate, reorder and duplicate **any** project node — a
locality, a product, a function block, a variable, a program element or a link — with one consistent
set of gestures, so a mistake or a change of plan can be fixed without rebuilding the tree by hand and
without leaving orphaned logic behind.

**Scope:** the general *Delete* of any node and its reference cascade; moving a node to another
container (across localities/sections); reordering siblings within a container (which drives report
order, US-040); and copy/paste of a node subtree within the project via the toolbar Cut/Copy/Paste and
`Ctrl+X`/`Ctrl+C`/`Ctrl+V` (US-001, US-045). **Scope excludes:** the single-type instances already
specified — locality delete/cascade (US-009) and saving a block into an on-disk library/*Favourites*
folder (US-021); removing or editing a *link* (US-057, US-058, E6); undo/redo of these operations, which
is the general E14 guarantee (US-052); and the placement-legality rules that gray out illegal insert
targets.

**Acceptance criteria (epic level):**
- MUST: Any **deletable** node can be deleted; deleting a node that other logic references cascades the
  dependent link halves and program rows as **one** undoable step — silently, since the confirmation
  rule is containment-based, not reference-based (US-053). **Not every node is deletable** — a
  product's pins come from its catalog type and are not the installer's to remove (US-053).
- MUST: A delete confirmation is triggered by **contents**, not by node type and not by references: an
  empty container deletes silently, a container with contents is guarded, a referenced-but-empty node
  deletes silently, and declining aborts the whole delete.
- MUST: Any node can be moved to another legal container, and siblings can be reordered within a
  container; a move/reorder preserves the node's identity (its IHC resource ids do not change).
- SHOULD: Any node subtree can be copied and pasted elsewhere in the project as an independent duplicate
  with fresh resource ids, appended last; **no link is carried into the paste** (the duplicate arrives
  fully unwired), and the copy keeps its scenes.
- MUST: Every operation here confirms in the status bar and is reversible via *Undo* (`Ctrl+Z`,
  US-052).

**Readiness:** Ready.

---

## US-053 — Delete any project node

> **Cross-cutting:** this generalises **US-009** (delete a locality with contents) to every deletable
> node — product, function block, variable/resource, program element, and link row — with one
> confirm-and-cascade rule.

**As an** IHC installer, **I want** to delete any node I select — being warned when other logic still
depends on it — **so that** I can remove a product, block, variable or program element without silently
orphaning the links and commands that referenced it.

**Scope excludes:** the locality-specific worked example (US-009); removing a *link* row, which has its
own story because it deletes a reciprocal pair rather than a subtree (US-057).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Delete a leaf node with no references
  Given a node with no dependent links or program references is selected
    (e.g. a freshly inserted product not yet linked, or an unused variable)
  When I right-click it and choose "Delete", or press Delete, or choose "Edit" > "Delete"
  Then the node and its subtree are removed from both panes
  And the status bar confirms the deletion by node name

Scenario: Delete a node that other logic references (silent cascade)
  Given a node that is referenced elsewhere is selected
    (e.g. a product output linked to a function block, or a variable used in a command/condition)
  When I choose "Delete"
  Then the node is removed WITHOUT a confirmation — being referenced never triggers a prompt
  And the reciprocal "link to"/"link from" halves that pointed into it, and the
    command/condition/event rows that referenced it, are removed together with it
  And the removal is a single step on the undo history (US-052) — undo is the protection

Scenario: Delete a container with contents (confirm)
  Given a container node holding contents is selected (the locality case is US-009's worked example)
  When I choose "Delete"
  Then a confirmation appears naming what the container holds, and I must accept it to proceed

Scenario: Decline the confirmation
  Given the delete confirmation is shown
  When I decline it
  Then nothing is deleted

Scenario: Delete is equivalent across all three activation routes
  Given a deletable node is selected
  Then "Delete" is reachable by right-click, by "Edit" > "Delete", and by the Delete key,
    with identical results (US-044)
```

### Business rules (what is deletable at all)

- MUST: **A product's pins are not deletable.** A pin exists because the product's catalog type declares it,
  so it is not the installer's to remove — *Delete* is **absent** from a pin's context menu (US-068), and the
  `Delete` key on a pin does nothing. This holds whether or not the pin is linked. (Deleting a pin would
  produce a product that contradicts its own catalog type: a six-button switch carrying five inputs, the
  sixth button unaddressable, unwireable, and invisible in the tree — and for an unlinked pin the delete is
  silent, since the delete guard is link-triggered.)
- MUST: An **engine guard** refuses to remove a catalog-declared pin even when asked directly, so a project
  written by any route stays conformant with its own catalog. The menu gate protects one GUI; the engine
  guard protects the file.
- MUST: The **locality root** is not deletable by any route (US-009).

### Business rules (reference policy)

- MUST: Deleting a node removes its **whole subtree**; the retired IHC resource ids are not reused.
- MUST: The reciprocal halves of any follow-link or scene link that pointed **into** the deleted subtree
  are removed automatically, so no dangling `link to`/`link from` row is left behind (the link bijection
  stays intact and the project stays saveable).
- MUST: Program rows (a command, condition or event) whose only reference was into the deleted subtree
  are removed **whole** as part of the cascade — matching the US-009 locality semantics — while their
  parent groups are kept (an emptied *Commands*/*Conditions* group survives).
- SHOULD: When a node cannot be safely cascaded because another element still binds it in a way the
  cascade does not cover (e.g. a *scenes* binding or an enumerator type still in use), the app **refuses
  the delete and explains what to rewire first**, rather than leaving a broken reference.

### Business rules (when the confirmation appears)

- MUST: The confirmation asks about what a node **contains**, never about what **points at** it. A
  container with **no** contents deletes **silently** — no dialog. A container **with** contents (a
  locality holding products, US-009) raises the confirmation. A node that is merely **referenced** by
  other logic — a linked product, or a function block containing its own program tree and wired to
  several product terminals, locked or not — deletes **silently**, with the full reference cascade;
  the single-step undo is the protection, not a prompt. (A block's programs and pins are its own
  definition, not "contents" in this rule's sense — containment means elements the user placed inside,
  as in a locality.)
- MUST: Declining the confirmation **aborts the whole delete** — nothing is removed, including the
  container itself. Declining is not a "delete the container but keep its contents" choice.
- MUST: Dismissing the confirmation with `Esc` has the same effect as declining it (US-069).
- SHOULD: The confirmation's wording states the cascade as a **consequence** of the delete ("*deleting it
  also removes …*"), matching what declining actually does. Because declining (*No*) aborts the **entire**
  delete rather than making a cascade *choice* ("should the function blocks be deleted?"), the wording is
  phrased as a consequence and must not be phrased as a choice the behaviour does not offer.
- The reference-cascade silence is a deliberate ruling: when the outcome is identical either way and
  the operation is a single undoable step, an extra question guards nothing — the app must not ask it.

### AC illustrations

- Deleting a `<product>` whose `<pin>` a block drove removes the product **and** the block's
  `link to`/`link from` rows and the command that switched it — one `Ctrl+Z` brings all of it back
  (mirrors the US-009 cascade, one level down at the product instead of the locality).
- Deleting an unused *Internal variable* `Flag = OFF` that no program references removes just that row,
  no confirmation needed.

### Constraints

- Verification method — **Demonstration**: delete a referenced product (silent, full cascade), a
  contents-holding locality (confirm-gated), and an unused variable (silent), and confirm the cascade
  of link halves + program rows, the single-step undo, and the three-route equivalence.
- The `Delete` confirm's keyboard behaviour (accepting `Esc`, focusing the safe button) is US-069's, fixed
  **without** weakening the guard.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — the containment-triggered confirm, the silent
reference cascade and the single-step undo work. ⚠ **Except the deletability rule**: a product **pin**
can still be deleted (silently when unlinked), producing a product that contradicts its own catalog type —
the menu gate and the engine guard are both unbuilt.

---

## US-054 — Move a node to another container

**As an** IHC installer, **I want** to move a product, function block or variable to a different
locality or section, **so that** I can correct where something lives without deleting and re-creating it
(and losing its documentation, addressing and links).

**Scope excludes:** reordering siblings within the same container (US-055); copying rather than moving
(US-056).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Move a product to another locality by dragging it (primary gesture)
  Given a product sits under one locality in the "Installation" pane
  When I drag the product onto another locality and release
  Then the product is re-parented under the target locality, keeping its documentation, terminal
    addressing and every link it participates in
  And the same relocation is reflected in the "Functions" pane
  And the status bar confirms the move

Scenario: Move a product to another locality with Cut/Paste (non-drag supplement)
  Given a product sits under one locality in the "Installation" pane
  When I Cut it with Ctrl+X and Paste it with Ctrl+V onto another locality
  Then the same id-preserving re-parent happens, with results identical to the drag
  And the status bar confirms the move

Scenario: Identity is preserved on a move
  Given a moved product/output that maps to an IHC resource id (shown in its tooltip, US-048)
  Then its resource id is unchanged after the move, so existing links and controller cross-references
    still resolve

Scenario: Illegal paste targets are not accepted
  Given a node is on the clipboard after a Cut
  When I paste it onto itself, onto one of its own descendants, or onto a container that may not
    hold that node type
  Then nothing is moved, and the app says so rather than failing silently (US-056)

Scenario: The move route is reachable without drag
  Given a node is selected
  Then Cut and Paste are each reachable by right-click, by the "Edit" menu, and by Ctrl+X / Ctrl+V,
    with identical results (US-044, US-068)
```

### Business rules (the gesture)

- MUST: A product is **moved by dragging** it onto a target locality in either pane; the drop performs the
  **same id-preserving re-parent** as
  Cut/Paste, with identical results and status feedback. Cut/Paste is the non-drag **supplement** (US-044
  route-parity), not a substitute.
- MUST: While a drag is in progress a **legal drop target is highlighted**; an illegal target — the node
  itself, one of its own descendants, or a container that may not hold it (the same rules as paste, US-056)
  — is **not** highlighted and **refuses** the drop, the app saying why rather than failing silently.

### AC illustrations

- Moving a `<product>` from `Living room` to `Kitchen` leaves its two input pins, their terminal addressing
  and the block link on `<pin>` intact; the block's `link from` row still reads the button's
  path, now under `Kitchen`.
- Cutting a locality and pasting it onto itself is rejected; the tree does not change.

### Constraints

- Verification method — **Demonstration** of the drag and Cut/Paste move routes, that ids and links survive
  the move, and that self/descendant and illegal-container targets are refused.
- A move — like every edit — leaves the tree's expand/collapse state intact (US-070).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the primary drag-to-move gesture and the Cut/Paste supplement
re-parent with the same id-preserving result; an illegal target is not highlighted and the drop is refused
with a reason.

---

## US-055 — Reorder nodes within a container

**As an** IHC installer, **I want** to change the order of siblings under a container — localities under
*Localities*, products under a locality, variables within a section — **so that** the tree and the
generated reports present components in the order I choose (US-040 documents products *in the order they
appear* in the *Installation* pane).

**Scope excludes:** moving a node to a *different* container (US-054).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Reorder siblings by dragging one to a new position (primary gesture)
  Given several siblings under one container (e.g. the ten default localities under "Localities")
  When I drag one sibling to a new position among its siblings and release
  Then the sibling takes the new position and the others close up around it
  And the new order is reflected identically in both panes, and in report output (US-040)
  And the status bar confirms the reorder

Scenario: Reorder siblings with Move up / Move down (non-drag supplement)
  Given several siblings under one container (e.g. the ten default localities under "Localities")
  When I move one sibling with "Move up" / "Move down" (or Ctrl+Shift+Up / Ctrl+Shift+Down)
  Then the sibling takes the new position and the others close up around it, identically to the drag
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
- MUST: Reordering is offered **primarily by dragging a sibling to a new position**,
  **with *Move up* / *Move down* (and cut/paste) as the non-drag supplements** that satisfy US-044
  route-parity. A drag reorder is the same id-preserving move as US-054 with an in-container target index;
  while dragging, the legal insertion position is indicated and a drop outside the container's own sibling
  list is refused. *Move up*/*Move down* stay **off the link row** and **off a pin** (US-068).
- MUST: At the container's ends the move command is **unavailable rather than a silent no-op**: *Move up*
  on the first sibling and *Move down* on the last (both, for an only child) are omitted from the context
  menu and greyed in the menu bar (US-044/US-068); a middle sibling offers both.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the primary drag-to-reorder gesture moves a sibling to a new
position (reflected in both panes and in report order), the move commands gate on reorderability at the
container's ends; *Move up* / *Move down* and cut/paste stay as
supplements.

---

## US-056 — Copy and paste a node

> The toolbar carries **Cut / Copy / Paste** and the shortcut set documents `Ctrl+X` / `Ctrl+C` /
> `Ctrl+V` (US-001, US-045), but no story defined what Copy/Paste *do* to a project node. This story
> fixes that. (Cut+Paste as a **move** is covered by US-054/US-055; this story is the **duplicate**.)

**As an** IHC installer, **I want** to copy a node and paste it elsewhere as an independent duplicate,
**so that** I can reuse a configured product, block or subtree without rebuilding it.

**Scope excludes:** saving a block into an on-disk library/*Favourites* folder (US-021, a different,
disk-level copy); moving a node (US-054/US-055).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Copy a product and paste it into another locality
  Given a configured product is selected
  When I Copy it (Ctrl+C or toolbar Copy) and Paste (Ctrl+V or toolbar Paste) onto a target locality
  Then an independent duplicate is appended as the LAST child of the target, carrying the source's
    documentation and structure, but with its own fresh IHC resource ids (the original is left unchanged)
  And the status bar confirms the paste

Scenario: Paste target must be legal
  Given something is on the clipboard
  When I paste onto a container that may not hold that node type
  Then nothing is pasted, and the app says so rather than failing silently

Scenario: No link survives a paste
  Given the copied subtree took part in links — some with their other end outside the subtree,
    some wholly inside it
  When I paste the copy
  Then the duplicate carries NO link halves at all: every link is dropped, including link pairs that
    lay wholly inside the copied subtree — the duplicate arrives completely unwired
  And the copy keeps its scene container

Scenario: A copied locality pastes onto the locality root
  Given a locality subtree is on the clipboard
  When I paste it onto the locality root
  Then the copy is appended as the last locality; pasting a locality onto another locality is refused
    (localities do not nest, US-008)

Scenario: Paste is available three ways
  Given a node is on the clipboard and a legal target is selected
  Then Paste is reachable by right-click, by "Edit" > "Paste", and by Ctrl+V (US-044, US-068)
```

### Business rules (paste placement and link handling)

- MUST: The pasted copy is **appended last** among the target's children — not inserted at the caret or
  sorted into position.
- MUST: **Every link is dropped on paste** — both halves of every link the copied subtree took part
  in, **including link pairs wholly inside the subtree**. The duplicate arrives completely unwired,
  ready to be wired into its own context. (Ruled explicitly; a keep-internal-links behaviour was
  considered and rejected.)
- MUST: The duplicate's **wired data-line terminal addresses are cleared** — a wired terminal is
  exclusive and cannot be double-booked — but a **wireless channel binding is kept** (it belongs to
  the physical device the product models, not to the wiring). These are two deliberate rules, not one;
  do not "simplify" them into each other. Postal `address` attributes on contact-info elements are
  untouched.
- MUST: The copy keeps its **scene container**.
- MUST: Paste legality follows the format's **placement rules** — a paste is accepted exactly into a
  parent the placement model says may contain the pasted kind (the locality root accepts a locality;
  unknown parents refuse).
- MUST: The pasted subtree is **revealed fully expanded** (US-070) — an arrival the installer caused is
  visible, not one collapsed row.
- MUST: A paste does not consume the clipboard — the same copy can be pasted repeatedly.
- MUST: An illegal-target paste **changes nothing** and **tells the user why**. IHC OpenVisual's explicit
  *Cannot paste — "That container cannot hold this node."* is a deliberate design decision (the app gives
  feedback rather than failing silently) and **stays**. (The *Paste* command may instead be absent for that
  target, US-068, but a paste that is attempted and cannot proceed is never a silent no-op.)

### AC illustrations

- Copying a `<product>` configured under `Living room` and pasting it under `Room` yields a
  second, independent button with its own resource ids, **appended after `Room`'s existing children**;
  editing the copy does not affect the original, and the copy shows **no** `link from`/`link to` rows
  at all — its wired terminal address cell is empty — but it does keep its scene container.
- Copying a whole locality with wired products and pasting it onto the locality root yields a
  duplicate room whose products are all unwired and unaddressed — the room's *shape* is reused, its
  wiring is authored fresh.
- Pasting that product onto **another product** pastes nothing and raises *Cannot paste*; the tree is
  unchanged.

### Constraints

- Verification method — **Demonstration** that a paste produces an independent duplicate with fresh ids,
  appends it last, drops external links, keeps scenes, and refuses illegal targets with a message.
- **Verify a Cut by the status bar, not by a tree diff.** *Cut* only **stages** a move — the tree is
  deliberately unchanged until *Paste* — so a tree diff proves nothing about whether Cut acted on the right
  node. The status bar names the node acted on.
- Copy/paste of a **function block** and of **program elements** is exercised less than the single-product
  case; confirm block/program paste before treating it as fully pinned.

**Readiness:** Ready (product/subtree paste); block/program paste carries the open item above.

**Implementation status:** ✅ Implemented (product/subtree paste) — paste placement by the placement
rules (including a locality onto the root), the drop-all-links rule, the wired-cleared /
wireless-kept address rule, scene handling, the expanded reveal, and the *Cannot paste* refusal are in
place, and *Paste* is reachable from the context menu (US-044). Block/program paste remains the open
item noted above.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-053 | Delete any project node | Ready | E15 | Must | US-009, US-052 |
| US-054 | Move a node to another container | Ready | E15 | Must | US-044, US-052 |
| US-055 | Reorder nodes within a container | Ready | E15 | Should | US-040, US-054 |
| US-056 | Copy and paste a node | Ready | E15 | Should | US-001, US-044, US-052 |
