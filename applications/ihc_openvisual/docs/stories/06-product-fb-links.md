---
version: 0.3.0
last-updated: 2026-07-17
status: draft
---

# E6 — Product ↔ function‑block links

> **Implementation status:** 🟡 Partly implemented — link create/remove, reciprocal rendering, **legality**
> and **half orientation** work (the last two landed 2026‑07‑17 — US-022), but **`F4` does not actually
> jump** (US-025) and the link/pin **labels diverge** from the vendor (US-022). The vendor comparison found
> the link model the weakest area in the app, and the two worst defects it found — links with no legality
> check at all, and **every link written backwards** — were both here.

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
- MUST: **Only a legal link can be created.** A drag that would produce a link IHC Visual cannot produce is
  refused, and the refusal is explained. The rule is specified once, in US-022, and governs **every** link
  this epic and US-033b create.
- MUST: A created link is shown reciprocally: the source pin shows a "link to" child and the target pin
  a "link from" child, each naming the **bare** full path of the other end — direction is carried by the
  row's icon, not by a prefix in the label (US-022).
- MUST: The installer can **remove** an existing link from either end, deleting both reciprocal halves as
  one undoable step (US-057).
- SHOULD: Dragging onto a scenario output opens a dialog to set the scene’s level/ramp (dimmer) or state
  (relay/socket), the value of an existing scenario link can be **edited** later (US-058), and `F4` jumps
  between the two ends of a link.

**Readiness:** Ready.

> **Vendor‑alignment note (2026‑07‑17) — this epic had no legality rule at all, and that was the gap.**
> Every story here specified how to *make* a link and none specified which links are **possible**. IHC
> Visual's rule was measured over **15 drag cells across 3 families with 0 falsifications** and is now
> written into US-022. It found two real divergences (**F‑058**, **F‑059**) and — while they were being
> fixed — a third that no tree‑based comparison could ever have seen (**F‑066**: IHC OpenVisual wrote every
> link's two halves **backwards**). Evidence: `RESULTS.md` **F‑058**–**F‑061**, **F‑066**, **F‑070**;
> backlog **A‑16**.

---

## US-022 — Link a product input to a function‑block input

**As an** IHC installer, **I want** to drag a product’s input pin onto a function‑block input, **so
that** actuating the sensor/button triggers the block.

> **Gesture (2026‑07‑18) — drag is primary, the two‑step is its supplement.** The link is created by
> **dragging one pin onto another**, matching IHC Visual so vendor‑experienced installers meet no surprise.
> IHC OpenVisual also offers a non‑drag **supplement** — *Link from here* on the source pin, then
> *Link to here* on the target (context menu, US-044 route‑parity) — reaching the identical result. Both use
> the same legality rule and orientation specified below; neither is a substitute for the other. ✅ The
> **drag gesture ships** (A‑33): dropping one pin onto another creates the link — equal to the two‑step
> supplement, under the SDK's `LinkRoles`/`CanLink` legality and F‑066 orientation. Creating a link (and
> deleting one, US-057) **leaves the tree expanded exactly as it was** — the installer keeps their place while
> wiring (US-070).

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

A drag is **refused only when it hits a measured prohibition; every other shape — including pin kinds the
corpus never exercised — is permitted.** The SDK's `LinkRoles` encodes the **negatives**, not an allow‑list,
precisely so unmeasured kinds (`resource_flag`, the wireless‑output family `airlink_relay` / `airlink_dimming`
/ …) stay legal rather than being silently forbidden (see the permissive MUST below). The rule is keyed on
the pin's **role in the drag** — what is dragged is the source, what it is dropped on is the target — and its
**element kind**, never its tree label, pane, or name. The three measured prohibitions:

1. **A consumer is never a source.** A function‑block input (`resource_input`) is a trigger the block
   consumes, so it can never be the dragged pin (0 of 314 corpus halves).
2. **A producer is never a sink.** A product input (`dataline_input` / `airlink_input` — a button the world
   drives, 0 of 160) and a function‑block output (`resource_output` — the block's own result, 0 of 237) are
   never a drop target.
3. **Two product pins never link directly** — at least one end must be a function‑block pin, because routing
   product logic through a function block *is* the IHC programming model.

The table below is an **inventory of the measured pin kinds**, not an exhaustive type allow‑list: a kind
absent from it is **unmeasured, therefore permitted**, not forbidden.

| Pin kind | May be a source? | May be a target? | Why, physically |
|---|---|---|---|
| Product input (`dataline_input` — a button, `Tryk`) | ✅ | ❌ | driven by the world, never by software |
| Product output (`dataline_output` — `Udgang`, `LED`) | ✅ | ✅ | **the only both**: drivable, *and* its state is readable |
| Block input (`resource_input` — `Kip`) | ❌ | ✅ | a block's trigger |
| Block output (`resource_output` — `ON puls`) | ✅ | ❌ | a block's result |

- MUST: A drag that hits any of the three prohibitions above is **refused — nothing is written to the
  project** — and the refusal says so. IHC Visual simply declines the drop silently; IHC OpenVisual's
  *Incompatible link* message is a
  **granted exception** under the 2026‑07‑16 ruling (feedback where the vendor is silent) and **stays**.
- MUST: The rule lives in **the SDK**, not in the view‑model, so a `.vis` stays valid whoever drives the
  editor — the GUI asks it before offering the drop, and the editor enforces it before writing anything.
- MUST: Where a pin kind has **not been measured**, the rule stays **permissive** rather than guessing. It
  refuses only what IHC Visual was seen to refuse.

> **⚠⚠ Do NOT implement this as "inputs link to inputs, outputs link to outputs".** That is the intuitive
> reading, it was the pre‑measurement hypothesis, **and IHC Visual falsifies it** — kind‑matching mispredicts
> **3 of the 15** measured cells: a product output → block input is **legal** (an output's state is a valid
> signal source); product output → block **output** is **refused** though both are "outputs"; and block input
> → product input is **refused** though both are "inputs".
>
> ⭐ **The cell that settles it: the *same pin pair* is accepted one drag direction and refused the other.**
> `LED` ↔ `ON puls` links when dragged block→product and is refused when dragged product→block. **Direction
> decides; the pair alone does not.** Any rule that looks only at the two pins' kinds — without asking which
> one was dragged — is wrong by construction.

> **Added 2026‑07‑17 (was: no legality rule anywhere in this epic).** IHC OpenVisual checked **one of the
> three link families** — block↔block (US-033b) — and let the other two through unchecked, so it silently
> accepted links IHC Visual cannot make, including **a button wired straight to a lamp with no function
> block in between**. Measured: **15 cells / 3 families / 0 falsifications**. Evidence: `RESULTS.md`
> **F‑058** (product↔block), **F‑059** (product↔product), **F‑060** (block↔block — **already correct, and
> the template the other two were extended from**); backlog **A‑16**.
>
> ✅ **Implemented 2026‑07‑17** — the predicate is in the SDK (`Ihc.Vis.Schema.LinkRoles` + `ProjectEditor.
> CanLink`), the app asks it for all three families, and the 15‑cell matrix is the test oracle.
>
> ✅ **The flag case is resolved (F‑082, M2 measured 2026‑07‑18): flags are NOT follow‑link endpoints.**
> `resource_flag` is a programming‑mode **internal variable** (the *Interne variable* section, rendered only in
> programming mode), not one of the Input/Output pins the vendor's follow‑link mechanism connects — config‑mode
> FB sections carry no flags, and the vendor corpus has **zero** flag links. There is no vendor flag‑link gesture
> to accept or refuse, so `LinkRoles`' permissive treatment of `resource_flag` is **correct‑by‑omission** and
> needs no negative (encoding one would be the guess this doc warns against). Keep the SDK permissive on flags.
> The other two previously‑scheduled cells are also **closed**: a block's output feeding **its own** input — the vendor
> **allows** it, so the same‑block refusal is dropped (**F‑080** amends A‑16, see US-033b); and **scene
> links** (US-024) are a fourth family gated at the call site, not by this predicate — no **legality** divergence
> (**F‑081**). ⚠ But M3 (2026‑07‑18) measured one **authoring** gap in that family: the vendor authors **shutter**
> scenes (`scene_shutter`) which OpenVisual renders but cannot create — recorded as a known gap in US-024, not built
> (user ruling). Evidence: `RESULTS.md` **F‑080**/**F‑081**/**F‑082**; `tmp\comptest\out\M\M3-scenes.json`.

### Business rules (which half is which)

- MUST: A link's two halves are written in the vendor's measured orientation, keyed on the pin's **role in
  the drag**: the **source** (the dragged producer) owns the **`link_from_resource`** half; the **sink** (the
  drop target consumer) owns the **`link_to_resource`** half. ⚠ **The element names read backwards from the
  roles** — the producer owns the *from* half — so never derive the orientation from the intuitive meaning of
  *from* / *to*; that misreading is exactly the F‑066 defect. The corpus is unanimous (product inputs own a
  *from* half 160/160 and a *to* half never; block inputs 314/314 *to*; block outputs 237/237 *from*), and
  the SDK's `LinkRoles` encodes it (a `resource_input` sink never owns a *from* half; a `dataline_input` /
  `resource_output` source never owns a *to* half).
- MUST: The **same** orientation the legality rule reads is the orientation the file is written in. Source
  and target must not be allowed to mean one thing to the check and the opposite to the write.

> **⭐ Added 2026‑07‑17 — IHC OpenVisual had every link's two halves inverted, and nothing in this epic said
> which way round they go.** The app called the editor with the drop target as the source and the dragged pin
> as the target, so a **button** was written as the *sink* and a **block's trigger** as the *source* — a shape
> that occurs **0 times in 397 links across the 21 authored vendor projects**, where the orientation is
> unanimous (product inputs own a *from*‑half 160/160 and a *to*‑half never; block inputs 314/314 *to*; block
> outputs 237/237 *from*; **no pin kind is ever seen in both roles**).
>
> **Why nothing caught it, and why this rule is now written down.** The SDK's link primitive was correct and
> byte‑fidelity tested; the inversion lived only in the app layer, which had no test. And US-022's own label
> rule below — *drop the `→`/`←` prefix, the icon carries direction* — meant **both orientations render
> identically in the tree**, so every tree‑based check was blind to it by construction. It took saving the
> file and reading the XML to see. ⚠ **Take the lesson, not just the fix**: a check and a write that disagree
> *inside one method* is not a rare bug, and "the tree looks right" was never evidence here.
>
> ✅ **Independently confirmed from a source that owes nothing to the file format**: IHC Visual's own link
> rows carry a direction **arrow icon**, and it reads `→` (outgoing, *this pin is the source*) on a button
> and `←` (incoming, *this pin is the sink*) on a product output — exactly the mapping above. **Before the
> fix IHC OpenVisual would have drawn these arrows backwards.** Evidence: `RESULTS.md` **F‑066** (fixed and
> verified by effect) and **F‑070**.

### Business rules (how a link row and its pins read)

- MUST: A link row's label is the **bare path** of the opposite end — `<locality> / <product-or-block> /
  <pin>`. It carries **no `→`/`←` prefix**: the link's direction is shown by the row's **icon** (US-046),
  and must not be duplicated in the label text.
- MUST: A pin's label is the **bare pin name**. It carries no state suffix — in particular no `(saved)`
  marker for the save‑current‑value flag (US-033), which the tooltip and the terminal editor (US-012)
  surface instead.
- MUST: The path's product segment renders **exactly as US-010 renders that product in the tree** — i.e.
  `name (position) ` when the product carries a `position`. This applies in **both panes**: a link row in
  the *Functions* pane names its product the same way the *Installation* pane does.
- MUST: **Every segment of a link path is bare.** The `= <value>` decoration US-010 puts on a state row
  belongs to the *state row itself*, never to a path segment that happens to name one. A path segment also
  renders the **node's own name** as the project holds it — never a translated or derived value token.

> **Added 2026‑07‑17 — two narrow label rules, one of which makes rows genuinely ambiguous.**
> - **The `(position)` rule did not reach the link‑path renderer.** US-010's product label rule was
>   implemented and tested on the *Installation* pane only, so *Functions*-pane link rows still name products
>   bare. **This is not cosmetic**: the measured project has `Entré/Gang` holding **two** products named
>   exactly `LK FUGA Tryk 6 tast 3 dioder`, distinguished **only** by `position` — so an IHC OpenVisual user
>   reading a link row **cannot tell which of the two it points at**. Evidence: `RESULTS.md` **F‑061**.
> - **The `= <value>` rule leaked *into* a link path.** In 640 *Installation*-pane nodes exactly **one**
>   label diverges from the vendor's, and it is a link row under `Scenarier/regulering`: IHC Visual renders
>   the bare 4‑segment path `… / Regulering / Op`, IHC OpenVisual renders 3 segments with the last decorated
>   — `… / Regulering = up`. It breaks both rules at once: a segment is decorated, and the value token `up`
>   is rendered where the vendor shows the node's own name `Op`. ⚠ Also decide whether IHC OpenVisual points
>   one level **short** (at `Regulering`) or merely renders it that way — the vendor's path has one more
>   segment. Evidence: `RESULTS.md` **F‑051**.
>
> ⚠ **Note these two rules pull in opposite directions, and that is correct.** One says *put more in the
> label* (the product's position); the other says *put less* (no value token). There is **no single rule**
> about who renders state into a label — US-010's note says the same thing from the other side. Take each
> row kind from the measured oracle; do not generalise in either direction.

> **Corrected 2026‑07‑16 (was: link labels specced *with* a leading `<-`, and pins rendering `(saved)`).**
> Two label divergences, both measured, both "IHC OpenVisual renders things into the label that IHC Visual
> does not":
> - **F‑020** — IHC OpenVisual renders `→ Entré/Gang / Lampeudtag … / Udgang`, an arrow prefix **in the
>   label text** *in addition to* a direction icon on the same row (both glyphs are visible together in
>   `00-shell-and-locality-ctxmenu-ov.png`). It is redundant **and** it eats horizontal width in the pane
>   that matters most. This is **not** covered by the icon‑artwork exception — it is label text, and the
>   glyph semantics are unaffected. The old AC baked the prefix in, so the story specced the defect.
> - **F‑019** — IHC OpenVisual renders `Udgang (saved)`, `LED (øverst) (saved)`; IHC Visual renders
>   `Udgang`, `LED (øverst)`.
>
> ⚠ **These cut *against* US-010's label rules**, where the *vendor* renders state into a label (`Lampeudtag
> (i loft) `, `Tilstand = Ukendt`) and IHC OpenVisual does not. So **there is no single rule about who puts
> state in labels** — take each row kind from the oracle rather than generalising in either direction.
> Evidence: `RESULTS.md` **F‑019**, **F‑020**; backlog **A‑7**.

### AC illustrations

- Under the `<function block>` block, `Input > <pin>` shows a child
  `Living room & Kitchen "open" / <product> / <pin>` — the bare source path, with the "link from"
  direction carried by the row's icon.

### Constraints

- Link-row glyphs and path rendering are cross-checked against the icon/artwork evidence in
  [`../icon_codes.md`](../icon_codes.md) (§4 Links).
- Verification method — **Test**, at two levels:
  - *Legality* (`safe_project_tests` for the engine, `safe_unit_tests` for the view‑model): the **15‑cell
    matrix** is the oracle — 4 accepts, 11 refusals. The three kind‑matching counter‑examples and the
    product↔product case are the regression guards; **a naive rule passes the other eleven cells.**
  - *Orientation* (`safe_project_tests`): assert the **written file**, not the tree. A tree assertion cannot
    fail here — that is exactly how the inversion survived. Read the halves back from the saved `.vis`.
  - *Labels*: assert a link row's label is the bare path (no arrow character, no `= value` on any segment,
    product segment carrying its `(position)`) and a pin's label is the bare name (no `(saved)`), against
    the vendor's rendering of the same project — **in both panes**.

**Readiness:** Ready.

**Implementation status:** 🟡 Implemented — link create, reciprocal rendering, **legality across all three
families**, and the **correct half orientation** (F‑058/F‑059/F‑066, shipped 2026‑07‑17 with a 17‑case test
suite). ✅ The two corrected label rules are **done** (backlog **A‑7**): the `→`/`←` prefix and the
`(saved)` suffix are both gone, and five existing tests that had **encoded the divergence as spec** were
flipped rather than deleted. ⚠ **Two narrower label gaps remain**: the *Functions*-pane path drops the
product's `(position)`, making same‑named products ambiguous (F‑061, backlog **A‑20**), and the
`Scenarier/regulering` path renders a value token instead of the vendor's node name (F‑051, backlog **A‑19**).

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

### Constraints

- **This story's direction is the legal one; its reverse is not.** Dragging a block **output** onto a product
  **output** is legal (US-022 clause 1+2: a block output produces, a product output consumes). Dragging the
  **product output onto the block output** is **refused** — the same two pins, the other way round. That
  asymmetry is IHC Visual's, it is measured, and it is why US-022's rule is keyed on the drag's roles rather
  than on the pair of kinds. Evidence: `RESULTS.md` **F‑058**.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — and now **gated by US-022's legality rule**, which previously did
not exist for this family (F‑058).

---

## US-024 — Create a scenario link

**As an** IHC installer, **I want** to link a function block’s scenario output to a product’s Scenarier
(scenes) container and set the scene value in the dialog that appears, **so that** one press recalls a
defined light setting across several outputs.

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
  When I drag the block's scenario output (a `resource_scene` pin) onto the dimmer's Scenarier (scenes) container
  Then a dialog opens automatically for the scene value
  And I set "Light level" (light level, e.g. 0% for off, 80% for bright) and
    "Ramp time" (ramp time, minutes and seconds) and confirm
  Then the scene link is created

Scenario: Create a relay/socket scene link with a state
  Given a function block scenario output (a `resource_scene` pin) and a socket (<product>) with a Scenarier container
  When I drag the block's scene output onto the socket's Scenarier container
  Then the dialog asks for the socket state ON or OFF, and confirming creates the scene link
```

> **Known gap — shutter scenes NOT authored (F‑081, M3 measured 2026‑07‑18).** The vendor supports a **third**
> scene family: a shutter/blind product takes a `scene_shutter` member (`shutter_position` = up|down + `delay_ms`),
> confirmed by a real member in `realprj`. IHC OpenVisual **renders** shutter scenes (A‑19) and the SDK **can build**
> them (`SceneValue.Shutter`), but the authoring path (`ProjectSession.LinkSceneAsync` / `UpdateSceneValueAsync`,
> and the value dialog) is **relay/dimmer‑only** — dragging onto a shutter product's Scenarier does not offer the
> up/down + delay dialog. **User ruling (2026‑07‑18): record the gap, do not build shutter authoring yet.** To close
> later: widen `LinkSceneAsync`/`UpdateSceneValueAsync` + add a shutter mode to the scene‑value dialog + extend
> `SceneRules.PinnedMemberTagFor`. Evidence: `tmp\comptest\out\M\M3-scenes.json`.

### AC illustrations

- A "Go-to-bed light" scene set on a `<product>` with *Light level* = `0 %` and *Ramp time* = 0 min
  1 s dims the ceiling light off over one second; the same block’s scene link to a `<product>` set to
  `ON` turns the bedside socket on — one button press recalls both.

### Constraints

- ✅ **Scene links are a FOURTH link family, resolved aligned at the call site (F‑081).** US-022's data‑flow
  predicate does not cover them, and it does not need to: IHC OpenVisual constrains the scene family at the
  **call site** — `CompleteSceneLinkAsync` is reached only from `LinkToHere` when the source tag is
  `resource_scene` **and** the target `IsSceneTarget`; every other pairing falls through to `CanLink`. So an
  illegal scene‑link shape **is not expressible**, there is **no divergence**, and no A‑16 extension or
  vendor drive is needed. The lesson: *"no check inside method X"* ≠ *"ungated"* — check the callers.
  Evidence: `RESULTS.md` **F‑081**.

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
  Then the caret in the OTHER pane lands on the pin at the other end of the link
  And that pin's collapsed ancestors are expanded and it is scrolled into view, so the caret is visible
  And the status bar names the pin it jumped to

Scenario: Jump to a target that is not yet realised
  Given the opposite pin lies under collapsed ancestors that have never been expanded
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

- MUST: The jump **expands the target's ancestor chain, scrolls the target into view, moves the caret onto
  it, and focuses that pane**. Setting a selection alone is not sufficient — a target that cannot be
  realised leaves the caret where it was.
- MUST: The status bar names **the pin jumped to**. It never reports a successful jump that did not happen.

> **Corrected 2026‑07‑16 (was: "the selection moves to the pin at the other end", which the app satisfies on
> paper and not in fact).** Measured: IHC OpenVisual's `F4` produces **no visible jump** — the source pane's
> caret is unchanged, the **other pane's selection is cleared**, neither pane expands or scrolls, and the
> status bar nevertheless claims *"Jumped to …"*. **A no‑op that reports success is the worst part of this**:
> it is why the defect survived, and it is why the status‑bar rule above is now a MUST. Reproduced with the
> target both unrealized **and** pre‑expanded/visible. IHC Visual gets this free — Win32's
> `TVM_SELECTITEM(TVGN_CARET)` expands ancestors and ensures visibility implicitly. Evidence: `RESULTS.md`
> **F‑012**; backlog **A‑6**. ⚠ Note the jump command currently has **no context‑menu or menu‑bar route at
> all** (F‑010), so with F4 broken it is unreachable by *any* working route — US-068 restores the route.

### AC illustrations

- Selecting the `… / <product> / <pin>` row under a block input and pressing `F4`
  selects the `<pin>` pin of that push‑button in the *Installation* pane, expanding and scrolling to it if
  needed; the status bar reads the **pin's** name, not the link row's.

### Constraints

- Verification method — **Test** (`safe_visual_tests`): assert the **other pane's** selection lands on the
  opposite pin, **and** that the status text names *that pin*. A single‑pane assertion cannot see this
  defect — the measuring driver had the identical blind spot until it was fixed to report every pane's
  selection.
- Reproduce the **false success message** first: it is the tell, and the cheapest regression guard.

**Readiness:** Ready.

**Implementation status:** ⛔ **Not implemented** — `F4` sets the selection properties but never expands the
ancestor chain or scrolls the target into view, so the jump silently does nothing while the status bar
reports success (F‑012). Backlog **A‑6** implements this story.

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

- Under an `<function block>` input `<pin>`, selecting the `… / <product> / <pin>`
  row and pressing `Delete` removes both that row and the `<pin>` pin's matching "link to"
  row; `<pin>`'s link to `<pin>` is untouched.

### Constraints

- Verification method — **Demonstration** that removing a link from either end deletes exactly its pair,
  leaves sibling links intact, and undoes/redoes cleanly.
- Note: pair‑exact removal (never "first half of the tag") and the throw‑when‑not‑linked guard are
  grounded in the engine's unlink contract. **The tree gesture is confirmed**: a link is removed by
  **selecting the link row and deleting it**. IHC Visual offers **no** dedicated *Remove link* command,
  and **IHC OpenVisual's shipped gesture matches**.

  > **Corrected 2026‑07‑17 (was: "the tree gesture … is to be confirmed during implementation. (R‑note.)").**
  > The ledger had already answered it — this was never an open question. IHC Visual's link‑row context menu
  > is **exactly 2 items**: `&Hop til modsat link` (**30504**) and **`&Slet`** (**24586**), with no properties
  > item. That is decisive rather than merely suggestive: a 2‑item menu has **room for exactly two commands
  > and neither of them is a *Remove link***. So *Delete* on the selected link row **is** the vendor's removal
  > gesture, and IHC OpenVisual already matches it. Evidence: `RESULTS.md` **F‑010** (`RESULTS.md:169`); the
  > stored vendor dump — Win32 command ids with `&`‑prefixed Danish labels, taken with an **empty clipboard**
  > — is `out\P1-census\vendor-gesture-findings.md:87-88`.
  >
  > ⚠ **The gesture matches; the *route* to it still diverges.** IHC OpenVisual's link row currently shows the
  > generic **7‑item** menu (`RESULTS.md` **F‑008**, `RESULTS.md:167`) — including `Insert product` on a link
  > row, where it is meaningless — where the vendor shows its 2, and it offers no jump‑to‑opposite‑end command
  > at all. Backlog **A‑5** fixes the menu; it does not change the removal gesture specced here.

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
  a single value is a poor experience. (R‑note; the vendor side is unmeasured — next step **C29**.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented. Epic E6 complete.
