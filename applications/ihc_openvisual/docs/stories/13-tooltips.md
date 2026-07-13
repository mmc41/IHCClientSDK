---
version: 0.1.0
last-updated: 2026-07-12
status: draft
---

# E13 — Tree node tooltips

> **Current scope:** ✅ **In scope (foundational)** — hover tooltips make the trees self‑documenting and
> expose each node's IHC resource ID, which installers need when cross‑referencing the controller.

**Goal:** Let an IHC installer read a tree node's documentation note **and** its IHC resource ID by simply
hovering the mouse over the node — with no modifier key — so that the note text authored in the project is
discoverable at a glance and the resource ID needed to cross‑reference the controller is always visible.

**Scope:** on‑hover tooltips for nodes in both the *Installation* and *Functions* panes (localities,
products, product inputs/outputs, function blocks, and function‑block pins); the tooltip's content (the
node's documentation note and its IHC resource ID); and the always‑on behaviour that shows the resource ID
without holding a modifier key. **Scope excludes:** editing the note or the resource ID (that is the
*Properties* dialog, E2–E5); tooltips on toolbar buttons, menus, or dialog controls; and the visual
icon/state language (E12).

**Acceptance criteria (epic level):**
- MUST: Hovering a tree node shows a tooltip containing that node's authored documentation note when one
  exists.
- MUST: For a node that maps to an IHC resource (input, output, function block), the tooltip always shows
  the node's IHC resource ID without the user holding any modifier key.
- SHOULD: The tooltip appears for nodes in both the *Installation* and *Functions* panes.

**Readiness:** Ready.

---

## US-047 — Read a node's documentation note on hover

**As an** IHC installer, **I want** a tooltip to appear when I hover a node in either project tree,
showing the documentation note authored for that node, **so that** I can read the installer's guidance for
that input, output, or function block without opening its properties.

**Scope excludes:** editing the note (done via *Properties*, E2–E5); tooltips on non‑tree UI (toolbar,
menus, dialogs).

### Acceptance criteria (Checklist)

- [ ] MUST: Hovering the mouse pointer over a tree node whose model carries a non‑empty documentation note
  shows a tooltip containing that note text, preserving the note's line breaks.
- [ ] MUST: The tooltip requires **no modifier key** — plain hover is sufficient. (Plain hover shows the
  note on a function block with no modifier — see the note under US-048; any `Ctrl`‑hover requirement
  is an unverified observation.)
- [ ] MUST: Hovering a node that has **no** documentation note and **no** resource ID (for example the
  *Localities* root or an empty locality) shows **no** tooltip.
- [ ] SHOULD: The tooltip is available for nodes in both the *Installation* and *Functions* panes.
- [ ] SHOULD: The tooltip dismisses when the pointer leaves the node.

### AC illustrations

- Hovering a product input whose note reads `Connected to presence indication from the PIR sensor.\n
  Mode is defined under settings:` shows a tooltip with that exact text, the second line beneath
  the first (the note associated with that node), on plain hover.
- Hovering the *Localities* root (no note, no resource ID) shows no tooltip.

**Readiness:** Ready.

---

## US-048 — Always see a node's IHC resource ID in its tooltip

**As an** IHC installer, **I want** every input, output, and function block to show its IHC resource ID in
the hover tooltip automatically, **so that** I can cross‑reference the node against the controller without
holding a modifier key or opening a dialog.

### Acceptance criteria (Checklist)

- [ ] MUST: Hovering an input, output, or function block node shows the node's IHC resource ID in the
  tooltip, labelled so the number is identifiable as the resource ID.
- [ ] MUST: The resource ID is shown on **plain hover** — the user holds **no** modifier key. (Showing the
  IHC resource ID in the tooltip is an IHC OpenVisual enhancement; the base requirements do not mandate a
  resource ID in any tooltip — see the note below.)
- [ ] MUST: When a node has both a documentation note (US-047) and a resource ID, the tooltip shows both,
  with the note text and the resource ID each on their own line(s).
- [ ] SHOULD: A node that has no assigned IHC resource ID shows no resource‑ID line (rather than a blank or
  placeholder ID).

### AC illustrations

- Hovering a product output that maps to IHC resource id `3954853` shows a tooltip line identifying that
  number as the resource ID, on plain hover.
- Hovering an input that carries both a note and resource id `2109445` shows the note text followed by the
  resource‑ID line in one tooltip.

### Constraints

- Verification method — **Demonstration**: hover an input, output, and function block that each have a
  resource ID and confirm the ID appears on plain hover; hover a node with no resource ID and confirm no
  resource‑ID line appears.
- Note (design basis): a documentation *note* appears on **plain** mouse‑hover for a function‑block
  group node, with no modifier key. For products the note is shown inline in the tree label, in
  parentheses after *Name* (and in reports). Notes on hover for product inputs/outputs and the IHC
  resource‑ID tooltip are IHC OpenVisual enhancements. The `Ctrl`‑hover behaviour and the resource‑ID
  tooltip are IHC OpenVisual's own design; verify them during implementation before treating
  them as fixed requirements. (R‑note.)

**Readiness:** Ready.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-047 | Read a node's documentation note on hover | Ready | E13 | Must | -- |
| US-048 | Always see a node's IHC resource ID in its tooltip | Ready | E13 | Must | -- |
