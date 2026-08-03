---
version: 0.4.0
last-updated: 2026-08-02
status: draft
---

# E6 — Product ↔ function-block links

**Goal:** Let an IHC installer wire physical products to function blocks by dragging pins — a product
input to a block input, a block output to a product output — and create scenario links, then jump
between the two ends of any link, so the installation's inputs drive its outputs through function
blocks.

**Scope:** drag-and-drop linking across the two panes (product pin ↔ function-block pin), the automatic
dialog for scenario links (light level / ramp time or ON/OFF), how a link is rendered under each pin,
`F4` navigation to the opposite end, removing an existing link, and editing a scenario link's stored
value. **Scope excludes:** the internal program logic of a block (E7) and
function-block-to-function-block variable links (covered in E7's programming story); deleting the
*node* a link hangs off (US-053).

**Acceptance criteria (epic level):**
- MUST: The installer can create a product-input→block-input link and a block-output→product-output
  link by dragging one pin onto another.
- MUST: **Only a legal link can be created.** A drag that would produce an illegal link is
  refused, and the refusal is explained. The rule is specified once, in US-022, and governs **every** link
  this epic and US-033b create.
- MUST: A created link is shown reciprocally: the source pin shows a "link to" child and the target pin
  a "link from" child, each naming the **bare** full path of the other end — direction is carried by the
  row's icon, not by a prefix in the label (US-022).
- MUST: The installer can **remove** an existing link from either end, deleting both reciprocal halves as
  one undoable step (US-057).
- SHOULD: Dragging onto a scenario output opens a dialog to set the scene's level/ramp (dimmer) or state
  (relay/socket), the value of an existing scenario link can be **edited** later (US-058), and `F4` jumps
  between the two ends of a link.

**Readiness:** Ready.

---

## US-022 — Link a product input to a function-block input

**As an** IHC installer, **I want** to drag a product's input pin onto a function-block input, **so
that** actuating the sensor/button triggers the block.

**Gesture:** the link is created by **dragging one pin onto another**. A non-drag
**supplement** — *Link fra her* on the source pin, then *Link til her* on the target (context menu,
US-044 route-parity) — reaches the identical result. Both use the same legality rule and orientation
specified below; neither is a substitute for the other. Creating a link (and deleting one, US-057)
**leaves the tree expanded exactly as it was** (US-070).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Drag a product input onto a block input
  Given the "Installation" pane shows a product input pin (e.g. <pin> of <product>)
    and the "Funktioner" pane shows a function-block input (e.g. <pin>)
  When I press and hold the mouse on the product input, drag it onto the block input, and release
  Then a link is created between them

Scenario: The link is shown reciprocally
  Given the link has been created
  Then expanding the block input <pin> shows a "link from" child naming the source's full path,
    e.g. "Living room & Kitchen \"open\" / <product> / <pin>"
  And the product input correspondingly shows a "link to" child pointing at the block input

Scenario: Unconfigured products remain flagged
  Given a product involved in a link has not been fully configured/linked
  Then it keeps its leading yellow "!" until configuration is complete (linking does not clear it)

Scenario: An illegal drag is refused and explained
  Given I drag a pin onto a pin the link rule does not allow (e.g. a button onto a lamp output,
    with no function block in between)
  When I release
  Then no link is created, in either pane
  And the app tells me the link is not allowed
```

### Business rules (which links are legal)

A drag is **refused only when it hits a listed prohibition; every other shape — including pin kinds not
covered here — is permitted.** The engine encodes the **negatives**, not an allow-list, precisely
so uncovered kinds (`resource_flag`, the wireless-output family `airlink_relay` / `airlink_dimming` / …)
stay legal rather than being silently forbidden. The rule is keyed on the pin's **role in the drag** — what
is dragged is the source, what it is dropped on is the target — and its **element kind**, never its tree
label, pane, or name. The three prohibitions:

1. **A consumer is never a source.** A function-block input (`resource_input`) is a trigger the block
   consumes, so it can never be the dragged pin.
2. **A producer is never a sink.** A product input (`dataline_input` / `airlink_input` — a button the world
   drives) and a function-block output (`resource_output` — the block's own result) are never a drop target.
3. **Two product pins never link directly** — at least one end must be a function-block pin, because routing
   product logic through a function block *is* the IHC programming model.

The table below is an **inventory of the known pin kinds**, not an exhaustive type allow-list: a kind
absent from it is **not covered, therefore permitted**, not forbidden.

| Pin kind | May be a source? | May be a target? | Why, physically |
|---|---|---|---|
| Product input (`dataline_input` — a button, `Tryk`) | ✅ | ❌ | driven by the world, never by software |
| Product output (`dataline_output` — `Udgang`, `LED`) | ✅ | ✅ | **the only both**: drivable, *and* its state is readable |
| Block input (`resource_input` — `Kip`) | ❌ | ✅ | a block's trigger |
| Block output (`resource_output` — `ON puls`) | ✅ | ❌ | a block's result |

- MUST: A drag that hits any of the three prohibitions is **refused — nothing is written to the project** —
  and the refusal says so. IHC OpenVisual's *Incompatible link* message is a deliberate design decision
  (the app explains the refusal rather than failing silently) and **stays**.
- MUST: The rule lives in **the engine**, not in the view-model, so a `.vis` stays valid whoever drives the
  editor — the GUI asks it before offering the drop, and the editor enforces it before writing anything.
- MUST: Where a pin kind is **not covered by the rule**, it stays **permissive** rather than guessing. It
  refuses only the three prohibitions listed above.
- MUST: **Do NOT implement this as "inputs link to inputs, outputs link to outputs".** The legality rule falsifies
  that: a product output → block input is **legal**; product output → block **output** is **refused** though
  both are "outputs"; and block input → product input is **refused** though both are "inputs". The *same pin
  pair* is accepted one drag direction and refused the other (`LED` ↔ `ON puls` links block→product, is
  refused product→block). **Direction decides; the pair alone does not** — any rule that ignores which pin
  was dragged is wrong by construction.

### Business rules (which half is which)

- MUST: A link's two halves are written in the format's canonical orientation, keyed on the pin's **role in the drag**:
  the **source** (the dragged producer) owns the **`link_from_resource`** half; the **sink** (the drop target
  consumer) owns the **`link_to_resource`** half. ⚠ **The element names read backwards from the roles** — the
  producer owns the *from* half — so never derive the orientation from the intuitive meaning of *from* / *to*.
  (Product inputs own a *from* half and never a *to* half; block inputs own *to*; block outputs own *from*.)
- MUST: The **same** orientation the legality rule reads is the orientation the file is written in. Source
  and target must not mean one thing to the check and the opposite to the write.

### Business rules (how a link row and its pins read)

- MUST: A link row's label is the **bare path** of the opposite end — `<locality> / <product-or-block> /
  <pin>`. It carries **no `→`/`←` prefix**: the link's direction is shown by the row's **icon** (US-046),
  and must not be duplicated in the label text.
- MUST: A pin's label is the **bare pin name**. It carries no state suffix — in particular no `(saved)`
  marker for the save-current-value flag (US-033), which the tooltip and the terminal editor (US-012)
  surface instead.
- MUST: The path's product segment renders **exactly as US-010 renders that product in the tree** — i.e.
  `name (position) ` when the product carries a `position`. This applies in **both panes**: a link row in
  the *Funktioner* pane names its product the same way the *Installation* pane does.
- MUST: **Every segment of a link path is bare.** The `= <value>` decoration US-010 puts on a state row
  belongs to the *state row itself*, never to a path segment that happens to name one. A path segment also
  renders the **node's own name** as the project holds it — never a translated or derived value token.

### AC illustrations

- Under the `<function block>` block, `Input > <pin>` shows a child
  `Living room & Kitchen "open" / <product> / <pin>` — the bare source path, with the "link from"
  direction carried by the row's icon.

### Constraints

- Link-row glyphs and path rendering follow the icon/artwork reference in
  [`../icon_codes.md`](../icon_codes.md) (§4 Links).
- Verification method — **Test**, at two levels:
  - *Legality*: the accepts and refusals form the oracle — the kind-matching counter-examples and the
    product↔product case are the regression guards (a naive rule passes the easy cells).
  - *Orientation*: assert the **written file**, not the tree — a tree assertion cannot see an inversion, which
    is how one went unnoticed. Read the halves back from the saved `.vis`.
  - *Labels*: assert a link row's label is the bare path (no arrow character, no `= value` on any segment,
    product segment carrying its `(position)`) and a pin's label is the bare name (no `(saved)`), in **both
    panes**.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — link create, reciprocal rendering, legality across all
three families, and the correct half orientation work; the `→`/`←` prefix and `(saved)` suffix are gone.
⚠ Two narrower label gaps remain: the *Funktioner*-pane path drops the product's `(position)` (making
same-named products ambiguous), and a `Scenarier/regulering` path renders a value token instead of the
node's own name.

---

## US-023 — Link a function-block output to a product output

**As an** IHC installer, **I want** to drag a function-block output onto a physical product output,
**so that** the block's result drives real hardware (a lamp, socket, etc.).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Drag a block output onto a product output
  Given the "Funktioner" pane shows a block output (e.g. <pin>)
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

- A `<product>` button linked to a block's `<pin>` input, and the block's `<pin>` linked to a
  `<product>`'s `<pin>`, forms a complete input→block→output chain even though the lamp still shows a
  `!` until addressed.

### Constraints

- **This story's direction is the legal one; its reverse is not.** Dragging a block **output** onto a product
  **output** is legal (US-022 clause 1+2: a block output produces, a product output consumes). Dragging the
  **product output onto the block output** is **refused** — the same two pins, the other way round. That
  asymmetry is why US-022's rule is keyed on the drag's roles rather than on the pair
  of kinds.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — gated by US-022's legality rule.

---

## US-024 — Create a scenario link

**As an** IHC installer, **I want** to link a function block's scenario output to a product's Scenarier
(scenes) container and set the scene value in the dialog that appears, **so that** one press recalls a
defined light setting across several outputs.

**Scope excludes:** authoring the block that provides the scenario output (E7).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Only scenario-capable outputs accept a scene link
  Given I want to build a scene
  Then valid scene targets are outputs marked with the scenario icon: all wireless products with
    outputs (relay/dimmer), and the wired <product> and <product> under the "Output" group
  And a function block's scenario outputs are likewise marked with the scenario icon
  And the triggering input may be any product — normally a low-voltage button or a remote control

Scenario: Create a dimmer scene link with level and ramp
  Given a function block with a scenario output and a dimmer product with a <pin> output
  When I drag the block's scenario output (a `resource_scene` pin) onto the dimmer's Scenarier (scenes) container
  Then a dialog opens automatically for the scene value
  And I set "Light level" (e.g. 0% for off, 80% for bright) and
    "Ramp time" (minutes and seconds) and confirm
  Then the scene link is created

Scenario: Create a relay/socket scene link with a state
  Given a function block scenario output (a `resource_scene` pin) and a socket (<product>) with a Scenarier container
  When I drag the block's scene output onto the socket's Scenarier container
  Then the dialog asks for the socket state ON or OFF, and confirming creates the scene link
```

### Business rules (the scene family)

- MUST: Scene links are a **fourth** link family, distinct from the three data-flow families US-022 governs.
  The scene family is constrained at the **call site** — a scene value dialog is reached only when the source
  is a `resource_scene` pin and the target is a scene container — so an illegal scene-link shape is not
  expressible, and US-022's data-flow predicate neither covers it nor needs to.
- **Known gap:** the `.vis` format supports a **third** scene kind — a shutter/blind product takes a shutter scene
  (`shutter_position` = up|down + `delay_ms`). IHC OpenVisual **renders** shutter scenes but cannot yet
  **author** them; dragging onto a shutter product's Scenarier does not offer the up/down + delay dialog.
  Recorded as a gap; scene authoring is relay/dimmer-only today.

### AC illustrations

- A "Go-to-bed light" scene set on a `<product>` with *Light level* = `0 %` and *Ramp time* = 0 min
  1 s dims the ceiling light off over one second; the same block's scene link to a `<product>` set to
  `ON` turns the bedside socket on — one button press recalls both.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (relay/dimmer scenes); shutter-scene authoring is a recorded gap.

---

## US-025 — Navigate between the two ends of a link

**As an** IHC installer, **I want** to jump from one end of a link to the other, **so that** I can
follow a signal path across the two panes without hunting for the matching pin.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Jump to the opposite end of a link
  Given a "link to" or "link from" row is selected (e.g. under a block input or a product output)
  When I press F4
  Then the caret in the OTHER pane lands on the RECIPROCAL LINK ROW — the other half of the same
    wire — not on the pin that owns that row
  And that row's collapsed ancestors are expanded and it is scrolled into view, so the caret is visible
  And keyboard focus follows into the destination pane, so arrow keys and a second F4 act there
  And the status bar names where it jumped to

Scenario: A jump is itself jumpable
  Given F4 just landed on the reciprocal link row
  When I press F4 again
  Then the caret jumps back onto the link row I started from — the landing row is a link row like
    any other, and it can also be deleted directly (US-057)

Scenario: Jump to a target that is not yet realised
  Given the opposite link row lies under collapsed ancestors that have never been expanded
  When I press F4 on the link row
  Then the jump still lands on it — the ancestor chain is expanded first

Scenario: Read a link's other end without jumping
  Given a linked pin is expanded
  Then its "link to"/"link from" child spells out the full path of the opposite pin
    (locality / product-or-block / pin), so the connection is legible in place

Scenario: The jump is reachable from the link row's context menu
  Given a link row is selected
  Then "jump to the opposite end" is offered on its context menu as well as on F4 (US-068)
```

### Business rules (what a jump must actually do)

- MUST: The jump lands on the **reciprocal link row** — the other half of the same wire — which is
  itself F4-able and deletable; it does not land on the pin that owns the row.
- MUST: The jump **expands the target's ancestor chain, scrolls the target into view, moves the caret onto
  it, and focuses that pane** — both halves matter: a selection that moves while keyboard focus stays
  in the pane the user left is nearly right and still wrong (a second `F4` or an arrow key would act
  on the wrong pane). A target that cannot be realised leaves the caret where it was.
- MUST: The status bar names **where it jumped to**. It never reports a successful jump that did not happen.

### AC illustrations

- Selecting the `… / <product> / <pin>` row under a block input and pressing `F4`
  selects that pin's reciprocal link row in the *Installation* pane, expanding and scrolling to it if
  needed; keyboard focus is now in the *Installation* pane, and pressing `F4` again returns to the
  original link row.

### Constraints

- Verification method — **Test**: assert the **other pane's** selection lands on the reciprocal link
  row, **and** that keyboard focus moved to that pane, **and** that the status text matches. A
  single-pane assertion cannot see this defect. The **false success message** is the tell, and the
  cheapest regression guard.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the jump lands on the reciprocal link row, expands and
scrolls to it, and moves keyboard focus into the destination pane.

---

## US-057 — Remove a link

> Links have create (US-022/023/024) and read (US-025) stories, but no way to **remove** one. This story
> supplies the Delete half of link CRUD. It is separate from US-053 (delete a *node*) because removing a
> link deletes a reciprocal **pair** of rows — the "link to" and "link from" halves — not a subtree.

**As an** IHC installer, **I want** to remove a link I created — a product↔function-block link, a
function-block-to-function-block variable link, or a scenario link — **so that** I can rewire a
connection I made by mistake or that the design no longer needs.

**Scope excludes:** deleting the products/blocks/pins themselves (US-053, whose cascade already removes
the link halves that point into a deleted node); wireless controller *unlink* (US-017, a commissioning
operation, not a project link).

### Acceptance criteria (Given-When-Then)

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

- Under an `<function block>` input `<pin>`, selecting the `… / <product> / <pin>`
  row and pressing `Delete` removes both that row and the `<pin>` pin's matching "link to"
  row; `<pin>`'s link to `<pin>` is untouched.

### Constraints

- Verification method — **Demonstration** that removing a link from either end deletes exactly its pair,
  leaves sibling links intact, and undoes/redoes cleanly.
- A link is removed by **selecting the link row and deleting it** — there is **no** dedicated *Remove link*
  command; a link row's context menu is exactly two commands — *jump to the opposite end* and *Slet*
  (US-068).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-058 — Edit a scenario link's value

**As an** IHC installer, **I want** to change the light level / ramp time (or ON/OFF state) of a
scenario link I already created, **so that** I can tune a scene without deleting and re-dragging the
link.

**Scope excludes:** creating the scene link (US-024); removing it (US-057); non-scenario follow-links,
which carry no editable value.

### Acceptance criteria (Given-When-Then)

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

- Verification method — **Demonstration** that an existing scene link's value can be re-opened, changed,
  confirmed and undone.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
