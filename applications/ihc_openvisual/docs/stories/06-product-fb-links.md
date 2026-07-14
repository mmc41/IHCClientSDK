---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E6 — Product ↔ function‑block links

> **Implementation status:** ✅ Implemented.

> **Current scope:** ✅ **In scope** — creating and navigating product↔function‑block links is
> project CRUD.

**Goal:** Let an IHC installer wire physical products to function blocks by dragging pins — a product
input to a block input, a block output to a product output — and create scenario links, then jump
between the two ends of any link, so the installation’s inputs drive its outputs through function
blocks.

**Scope:** drag‑and‑drop linking across the two panes (product pin ↔ function‑block pin), the automatic
dialog for scenario links (light level / ramp time or ON/OFF), how a link is rendered under each pin,
`F4` navigation to the opposite end, removing an existing link, and editing a scenario link's stored
value. **Scope excludes:** the internal program logic of a block (E7) and
function‑block‑to‑function‑block variable links (covered in E7’s programming story); deleting the
*node* a link hangs off (US-053).

**Acceptance criteria (epic level):**
- MUST: The installer can create a product‑input→block‑input link and a block‑output→product‑output
  link by dragging one pin onto another.
- MUST: A created link is shown reciprocally: the source pin shows a "link to" child and the target pin
  a "link from" child, each naming the full path of the other end.
- MUST: The installer can **remove** an existing link from either end, deleting both reciprocal halves as
  one undoable step (US-057).
- SHOULD: Dragging onto a scenario output opens a dialog to set the scene’s level/ramp (dimmer) or state
  (relay/socket), the value of an existing scenario link can be **edited** later (US-058), and `F4` jumps
  between the two ends of a link.

**Readiness:** Ready.

---

## US-022 — Link a product input to a function‑block input

**As an** IHC installer, **I want** to drag a product’s input pin onto a function‑block input, **so
that** actuating the sensor/button triggers the block.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Drag a product input onto a block input
  Given the "Installation" pane shows a product input pin (e.g. <pin> of <product>)
    and the "Functions" pane shows a function-block input (e.g. <pin>)
  When I press and hold the mouse on the product input, drag it onto the block input, and release
  Then a link is created between them

Scenario: The link is shown reciprocally
  Given the link has been created
  Then expanding the block input <pin> shows a "link from" child naming the source's full path,
    e.g. "<- Living room & Kitchen \"open\" / <product> / <pin>"
  And the product input correspondingly shows a "link to" child pointing at the block input

Scenario: Unconfigured products remain flagged
  Given a product involved in a link has not been fully configured/linked
  Then it keeps its leading yellow "!" until configuration is complete (linking does not clear it)
```

### AC illustrations

- Under the `<function block>` block, `Input > <pin>` shows a child
  `<- Living room & Kitchen "open" / <product> / <pin>`, and `<pin>` shows
  `<- Living room & Kitchen "open" / <product> / <pin>` — the "link from" rows read as
  `<source locality> / <product> / <pin>`.

### Constraints

- Link-row glyphs and path rendering are cross-checked against the icon/artwork evidence in
  [`../icon_codes.md`](../icon_codes.md) (§4 Links).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (link create + reciprocal rendering).

---

## US-023 — Link a function‑block output to a product output

**As an** IHC installer, **I want** to drag a function‑block output onto a physical product output,
**so that** the block’s result drives real hardware (a lamp, socket, etc.).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Drag a block output onto a product output
  Given the "Functions" pane shows a block output (e.g. <pin>)
    and the "Installation" pane shows a product output (e.g. <pin> of a <product>)
  When I drag the block output onto the product output and release
  Then a reciprocal link is created (the block output shows a "link to" child; the product output a "link from" child)

Scenario: Mixed configuration state is allowed
  Given a link connects a fully configured source and a not-yet-configured target product
  Then the link is still created, but the unconfigured product keeps its yellow "!"
    until it is configured (a physical install will not work until all products are configured)

Scenario: End-to-end path through a block
  Given a product input is linked to a block input (US-022) and the block output is linked to a product output
  Then actuating the input product will (once configured and deployed) drive the output product via the block
```

### AC illustrations

- A `<product>` button linked to a block’s `<pin>` input, and the block’s `<pin>` linked to a
  `<product>`’s `<pin>`, forms a complete input→block→output chain even though the lamp still shows a
  `!` until addressed.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-024 — Create a scenario link

**As an** IHC installer, **I want** to link a function block’s scenario output to a product’s scenario
output and set the scene value in the dialog that appears, **so that** one press recalls a defined
light setting across several outputs.

**Scope excludes:** authoring the block that provides the scenario output (E7).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Only scenario-capable outputs accept a scene link
  Given I want to build a scene
  Then valid scene targets are outputs marked with the scenario icon: all wireless products with
    outputs (relay/dimmer), and the wired <product> and <product> under the "Output" group
  And a function block's scenario outputs are likewise marked with the scenario icon
  And the triggering input may be any product — normally a low-voltage button or a remote control

Scenario: Create a dimmer scene link with level and ramp
  Given a function block with a scenario output and a dimmer product with a <pin> output
  When I drag the block's scenario output onto the dimmer's scenario output
  Then a dialog opens automatically for the scene value
  And I set "Light level" (light level, e.g. 0% for off, 80% for bright) and
    "Ramp time" (ramp time, minutes and seconds) and confirm
  Then the scene link is created

Scenario: Create a relay/socket scene link with a state
  Given a function block scenario output and a socket (<product>) scenario output
  When I drag one onto the other
  Then the dialog asks for the socket state ON or OFF, and confirming creates the scene link
```

### AC illustrations

- A "Go-to-bed light" scene set on a `<product>` with *Light level* = `0 %` and *Ramp time* = 0 min
  1 s dims the ceiling light off over one second; the same block’s scene link to a `<product>` set to
  `ON` turns the bedside socket on — one button press recalls both.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-025 — Navigate between the two ends of a link

**As an** IHC installer, **I want** to jump from one end of a link to the other, **so that** I can
follow a signal path across the two panes without hunting for the matching pin.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Jump to the opposite end of a link
  Given a "link to" or "link from" row is selected (e.g. under a block input or a product output)
  When I press F4
  Then the selection moves to the pin at the other end of the link (in the other pane)

Scenario: Read a link's other end without jumping
  Given a linked pin is expanded
  Then its "link to"/"link from" child spells out the full path of the opposite pin
    (locality / product-or-block / pin), so the connection is legible in place
```

### AC illustrations

- Selecting the `<- … / <product> / <pin>` row under a block input and pressing `F4`
  selects the `<pin>` pin of that push‑button in the *Installation* pane.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-057 — Remove a link

> Links have create (US-022/023/024) and read (US-025) stories, but no way to **remove** one. This story
> supplies the Delete half of link CRUD. It is separate from US-053 (delete a *node*) because removing a
> link deletes a reciprocal **pair** of rows — the "link to" and "link from" halves — not a subtree.

**As an** IHC installer, **I want** to remove a link I created — a product↔function‑block link, a
function‑block‑to‑function‑block variable link, or a scenario link — **so that** I can rewire a
connection I made by mistake or that the design no longer needs.

**Scope excludes:** deleting the products/blocks/pins themselves (US-053, whose cascade already removes
the link halves that point into a deleted node); wireless controller *unlink* (US-017, a commissioning
operation, not a project link).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Remove a follow-link from either end
  Given a link exists between a product pin and a function-block pin
    (shown as reciprocal "link to"/"link from" rows, US-025)
  When I select either the "link to" or the "link from" row and choose "Delete" (or press Delete)
  Then both halves of that link are removed together, from both panes
  And the two pins return to their unlinked state, keeping all their other links
  And the status bar confirms the removal

Scenario: Remove a scenario link
  Given a scenario link connects a function-block scenario output to a product's <pin> output
    (US-024)
  When I select the scene link row and choose "Delete"
  Then the scene membership and its back-reference are both removed, and the scene value set at
    creation is discarded

Scenario: Removing one link leaves the others intact
  Given a pin that participates in several links
  When I remove one of them
  Then only that link's pair is deleted; the pin's remaining links still resolve (F4 still jumps them)

Scenario: Remove is undoable
  Given I have just removed a link
  When I press Ctrl+Z (US-052)
  Then the link is restored with both halves and, for a scene link, its original value
```

### AC illustrations

- Under an `<function block>` input `<pin>`, selecting the `<- … / <product> / <pin>`
  row and pressing `Delete` removes both that row and the `<pin>` pin's matching "link to"
  row; `<pin>`'s link to `<pin>` is untouched.

### Constraints

- Verification method — **Demonstration** that removing a link from either end deletes exactly its pair,
  leaves sibling links intact, and undoes/redoes cleanly.
- Note: pair‑exact removal (never "first half of the tag") and the throw‑when‑not‑linked guard are
  grounded in the engine's unlink contract; the tree gesture (select the link row and *Delete* vs. a
  dedicated *Remove link* command) is to be confirmed during implementation. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-058 — Edit a scenario link's value

**As an** IHC installer, **I want** to change the light level / ramp time (or ON/OFF state) of a
scenario link I already created, **so that** I can tune a scene without deleting and re‑dragging the
link.

**Scope excludes:** creating the scene link (US-024); removing it (US-057); non‑scenario follow‑links,
which carry no editable value.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Re-open a dimmer scene value
  Given a scenario link to a dimmer's <pin> output exists with a level and ramp time (US-024)
  When I select the scene link row and open its "Properties" (F2 or right-click > Properties)
  Then the same dialog that set the value on creation re-opens, pre-filled with the current
    "Light level" and "Ramp time"
  When I change a value and confirm
  Then the scene link stores the new value, and the change is confirmed in the status bar and undoable

Scenario: Re-open a relay/socket scene state
  Given a scenario link to a <product>/relay scene output exists with an ON/OFF state
  When I open its "Properties"
  Then the dialog offers the ON/OFF state pre-set to the current value, and confirming stores the change
```

### AC illustrations

- A "Go-to-bed light" scene set to `Light level = 0 %`, `Ramp time = 0 min 1 s` (US-024) can be reopened and
  changed to `20 %` over `3 s` without removing the link; `Ctrl+Z` restores the previous value.

### Constraints

- Verification method — **Demonstration** that an existing scene link's value can be re‑opened, changed,
  confirmed and undone.
- Note: US-024 fixes the value dialog only at *creation*; whether a later edit
  of the stored scene value (vs. remove‑and‑recreate) is offered is to be confirmed during implementation.
  If no in‑place edit is offered, IHC OpenVisual SHOULD still provide one, as re‑dragging to change
  a single value is a poor experience. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented. Epic E6 complete.
