---
version: 0.2.0
last-updated: 2026-07-16
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
- MUST: A delete confirmation is triggered by **contents**, not by node type: an empty container deletes
  silently, a container with contents is guarded, and declining aborts the whole delete.
- MUST: Any node can be moved to another legal container, and siblings can be reordered within a
  container; a move/reorder preserves the node's identity (its IHC resource ids do not change).
- SHOULD: Any node subtree can be copied and pasted elsewhere in the project as an independent duplicate
  with fresh resource ids, appended last; links whose other end lies outside the copy are not carried into
  the paste, and the copy keeps its scenes.
- MUST: Every operation here confirms in the status bar and is reversible via *Undo* (`Ctrl+Z`,
  US-052).

> **Vendor‑alignment note (2026‑07‑16).** This epic's destructive semantics were measured against IHC
> Visual. The **delete trigger** and the **copy/paste semantics** came back **aligned on both apps** and are
> recorded here as regression baselines (F‑023, F‑038, F‑035). Two rules are **deliberate divergences**
> where the vendor is silent and IHC OpenVisual is not — its linked‑product delete confirm (F‑017/F‑033) and
> its `Cannot paste` refusal (F‑036). Both are granted by the 2026‑07‑16 ruling, written into US-053 and
> US-056 as such, and **must not be "aligned" away**.

**Readiness:** Ready — with one open item on paste's cross‑epic reach (see US-056). The delete
reference‑policy confirmation copy (US-053) is now measured and closed.

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

### Business rules (when the confirmation appears)

- MUST: The confirmation is **triggered by contents, not by node type**. A container with **no** contents
  deletes **silently** — no dialog. A container **with** contents raises the confirmation.
- MUST: Declining the confirmation **aborts the whole delete** — nothing is removed, including the
  container itself. Declining is not a "delete the container but keep its contents" choice.
- MUST: Dismissing the confirmation with `Esc` has the same effect as declining it (US-069).

> **Confirmed 2026‑07‑16 — regression baseline, both apps measured aligned.** Deleting an **empty**
> locality is silent on both; deleting a locality **with contents** raises a guard on both, and declining
> aborts on both. So the guard's **trigger condition matches at the boundary** — a fact worth pinning,
> because IHC Visual's guard is *not* uniform: it deletes a **linked product** silently (see the deliberate
> exception below). Evidence: `RESULTS.md` **F‑038** (empty → both silent) and **F‑023** (with contents →
> both guard); `RESULTS.md` **E‑6** notes the products‑only vs blocks‑only sub‑case is not isolated.

### Business rules (deliberate exceptions — IHC OpenVisual keeps its guards)

IHC Visual is the authoritative spec for this epic, with one bounded exception the user set explicitly on
**2026‑07‑16**: *IHC OpenVisual keeps its safety guards and error feedback even where the vendor is silent
— they change nothing about **what** happens, only warn or explain.* Two rules here are granted by that
ruling. They are **deliberate and justified — do not "align" them away**:

- MUST: Deleting a product that other logic references **raises a confirmation naming the cascade** —
  which links and commands will also go — and proceeds only on acceptance.

  > **Deliberate divergence (C), granted 2026‑07‑16.** **IHC Visual deletes a linked product silently — no
  > confirmation whatsoever** (verified by effect: the locality went 5 → 4 children with no dialog). That
  > silent destruction is a **vendor quirk not to copy**: the guard changes nothing about what a confirmed
  > delete does, it only warns first, and the cascade it names is exactly the surprising part. Read this
  > with the contents rule above — the vendor's silence is **product‑specific**, since it *does* guard a
  > locality delete (F‑023), so this is a narrow quirk rather than a policy. Evidence: `RESULTS.md`
  > **F‑017** (vendor silent, verified by effect + undo) and **F‑033** (IHC OpenVisual's confirm, `S03\
  > 12-delete-product-confirm-ov.png`). **Not a backlog item** — only the confirm's *ergonomics* are
  > (US-069: it ignores `Esc` and focuses no button — backlog **A‑9**/**A‑10**). Fix those; never remove
  > the confirm.

- SHOULD: The confirmation's wording states the cascade as a **consequence** of the delete ("*deleting it
  also removes …*"), matching what declining actually does.

  > **Deliberate divergence (D→ours), 2026‑07‑16.** IHC Visual asks *"Skal funktionsblokke slettes?"*
  > ("should the function blocks be deleted?") — phrased as a **cascade choice**, but **No aborts the entire
  > delete** rather than deleting the locality without its blocks. **The question it asks is not the question
  > it answers.** IHC OpenVisual's phrasing matches its behaviour and is kept. Recorded so nobody "aligns"
  > the text later. Evidence: `RESULTS.md` **F‑026**.

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
  are grounded in the project engine's delete contract (the US-009 cascade generalised). The
  confirmation's **trigger** is now measured (contents, not node type — F‑023/F‑038) and its wording is
  settled as the deliberate exception above (F‑026); the earlier "wording to be confirmed" note is closed.
- ⚠ **The `Delete` confirm is keyboard‑inert today** — it ignores `Esc` and focuses neither button, so a
  destructive dialog can only be answered with the mouse. That is US-069's defect, not this story's, and it
  is fixed **without** weakening the guard (backlog **A‑9**/**A‑10**).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the guard's trigger and cascade are measured aligned with IHC
Visual for containers (F‑023/F‑038), and the linked‑product confirm is the granted exception (F‑017/F‑033).
⚠ Two route/ergonomics gaps sit outside this story: *Delete* is reachable from the context menu but the
confirm cannot be answered by keyboard (US-069).

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
  Then an independent duplicate is appended as the LAST child of the target, carrying the source's
    documentation and structure, but with its own fresh IHC resource ids (the original is left unchanged)
  And the status bar confirms the paste

Scenario: Paste target must be legal
  Given something is on the clipboard
  When I paste onto a container that may not hold that node type
  Then nothing is pasted, and the app says so rather than failing silently

Scenario: Links to nodes outside the copy are not carried over
  Given the copied node had a link whose other end lies outside the copied subtree
  When I paste the copy
  Then the paste does not include that external link half (the duplicate starts unlinked on that pin);
    links wholly inside the copied subtree are duplicated and remain connected within the copy
  And the copy keeps its scene container

Scenario: Paste is available three ways
  Given a node is on the clipboard and a legal target is selected
  Then Paste is reachable by right-click, by "Edit" > "Paste", and by Ctrl+V (US-044, US-068)
```

### Business rules (paste placement and link handling)

- MUST: The pasted copy is **appended last** among the target's children — not inserted at the caret or
  sorted into position.
- MUST: A link whose other end lies **outside** the copied subtree is **dropped** — the duplicate starts
  unlinked on that pin. Links wholly **inside** the subtree are duplicated and stay connected within the
  copy.
- MUST: The copy keeps its **scene container**.
- MUST: An illegal‑target paste **changes nothing** and **tells the user why** (see the exception below).

> **Confirmed 2026‑07‑16 — regression baseline, both apps measured aligned.** Copy a product, paste it into
> another locality: on **both** apps the copy is appended last, its output pin's **from‑side link row is
> dropped**, and `Scenarier` is kept. This also matches the byte‑verified copy oracle the engine was built
> against. The only differences are the already‑recorded label renderings (F‑019, F‑003), not the
> semantics. Evidence: `RESULTS.md` **F‑035** (both driven live; pasted subtrees dumped and compared).

- MUST: Pasting onto a container that cannot hold the clipboard's node type shows an **explicit refusal**
  message; the *Paste* command may instead be absent for that target (US-068), but a paste that is attempted
  and cannot proceed is never a silent no‑op.

  > **Deliberate divergence (C), granted 2026‑07‑16.** **IHC Visual's illegal paste is a silent no‑op** —
  > pasting a product onto another product added nothing and showed no dialog, leaving the user with no way
  > to tell "refused" from "nothing happened". IHC OpenVisual's explicit *Cannot paste — "That container
  > cannot hold this node."* **stays**, under the ruling's exception #1: it changes nothing about *what*
  > happens, it only explains. Recorded so nobody removes it to 'match' the vendor. Evidence: `RESULTS.md`
  > **F‑036** (`S07\40-cannot-paste-ov.png`; vendor node count unchanged 643→643, no modal).

### AC illustrations

- Copying a `<product>` configured under `Living room` and pasting it under `Room` yields a
  second, independent button with its own resource ids, **appended after `Room`'s existing children**;
  editing the copy does not affect the original, and the copy shows no `link from`/`link to` rows for links
  the original had to blocks outside the copy — but it does keep its scene container.
- Pasting that product onto **another product** pastes nothing and raises *Cannot paste*; the tree is
  unchanged.

### Constraints

- Verification method — **Demonstration** that a paste produces an independent duplicate with fresh ids,
  appends it last, drops external links, keeps scenes, and refuses illegal targets with a message.
- ⚠ **Verify a Cut by the status bar, not by a tree diff.** *Cut* only **stages** a move — the tree is
  deliberately unchanged until *Paste* — so a tree diff proves nothing about whether Cut acted on the right
  node. The status bar names the node acted on.
- **Open item — cross‑epic paste reach:** copy/paste of a single product/subtree is engine‑verified **and
  now measured against the vendor** (F‑035); copy/paste of a **function block** and of **program elements**
  is exercised less and its exact behaviour there is not fully pinned (`RESULTS.md` S07 marks FB‑subtree
  copy as deferred). Confirm block/program paste during implementation before treating it as fixed.
  (R‑note.)

**Readiness:** Ready (product/subtree paste, measured aligned); block/program paste carries the open item
above.

**Implementation status:** ✅ Implemented — paste placement, link‑dropping and scene handling are measured
**aligned** with IHC Visual (F‑035), and the *Cannot paste* refusal is the granted exception (F‑036). ⚠ The
*Paste* command is not yet reachable from any context menu (US-068, backlog **A‑5**). Epic E15 complete.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-053 | Delete any project node | Ready | E15 | Must | US-009, US-052 |
| US-054 | Move a node to another container | Ready | E15 | Must | US-044, US-052 |
| US-055 | Reorder nodes within a container | Ready | E15 | Should | US-040, US-054 |
| US-056 | Copy and paste a node | Ready | E15 | Should | US-001, US-044, US-052 |
