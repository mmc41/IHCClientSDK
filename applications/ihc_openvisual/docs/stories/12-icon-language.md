---
version: 0.3.1
last-updated: 2026-07-17
status: draft
---

# E12 — Icon & visual language

> **Current scope:** ✅ **In scope (foundational)** — the icon / state visual language is needed to read
> the project trees. *(A few glyphs it catalogues — breakpoint, simulation red/green — belong to the
> out-of-scope simulation epic, E8.)*

**Goal:** Let a user read a node’s **type and state** from its icon and colour alone, so the two trees
and the program view are legible at a glance. This epic is cross‑cutting and critical to the UI: the
icon set is a first‑class part of the UI that IHC OpenVisual must provide to keep the trees readable.

**Scope:** the distinct icon per node category (localities, function blocks, product pins, links,
variable types, program elements, scenario, breakpoint) and the simulation state colours; and the boundary
between what an **icon** carries and what a **label** carries. **Scope
excludes:** the exact bitmap artwork (IHC OpenVisual supplies equivalent glyphs); this story fixes *which
categories are visually distinguished and what each means*. The **text of a tree row** — which rows are
drawn at all, and what each label reads — belongs to the stories that own those rows: **US-010** (a
product's row set, its `name (position) ` label, and `name = value` state rows) and **US-022** (link‑row and
pin labels).

> **Division of labour, fixed 2026‑07‑16.** Direction, type and state markers are the **icon's** job; the
> **label** carries text only. IHC OpenVisual had drifted across that line in both directions — rendering a
> `→` and a `(saved)` into labels the vendor keeps bare (F‑019/F‑020), while omitting the `(position)` and
> `= value` text the vendor does render (F‑003/F‑004). Neither is an icon‑artwork question, so neither is
> covered by this epic's artwork exception.

> **Artwork‑level spec:** the visual style, technical construction and functional behaviour of the actual
> icons — enough to build the whole set in a modern style — is documented separately in
> [`../icons_design.md`](../icons_design.md) with codes in [`../icon_codes.md`](../icon_codes.md).
This story stays at the "which categories / what they mean" level.

**Acceptance criteria (epic level):**

- MUST: Every node category listed below is rendered with a distinct, consistent icon so its type is
  identifiable without reading the label.
- MUST *(⛔ **catalogued but out of scope pending E8**)*: In **simulation mode**, input/output state is
  shown by colour: **red = OFF, green = ON**.

  > **Ruling R‑5, 2026‑07‑17 — catalogued, not exercisable.** ⛔ **E8 (simulation) is out of scope** by the
  > 2026‑07‑16 ruling, **exception #2**, so there is no simulation mode in which to show a colour and this
  > MUST cannot be exercised. It is the **epic‑level twin** of US-046's simulation MUST (`:106-108`), which
  > carries the same marker; the two must agree, or the epic re‑imposes at epic level exactly what the story
  > sheds. The glyph documentation **stays** — here, in US-046 and in the *Simulation* row of the reference
  > table — as the **catalogue for when/if E8 is ever built**. Tracked as ruling **R‑5** in
  > `tmp\research3.md` §7.
- SHOULD: State/decoration markers (unlinked "!", library badge, breakpoint) are visually distinct
  from type icons.

**Readiness:** Ready.

---

## US-046 — Distinguish node types by icon and state colour

**As an** IHC installer, **I want** each node type and each simulated state to have a distinct icon or
colour, **so that** I can read the installation, function and program trees at a glance.

### Acceptance criteria (Checklist)

- [ ] MUST: **Structure nodes** are distinct: *Localities*/locality (container), **library function block**
  vs. editable **function block** (two different icons — the library badge signals a locked, supplied block),
  the four block sections *Input*, *Output*, *Settings*, *Internal variables* each with their own
  icon, and the *RS485 modules* container node.
- [ ] MUST: **Product pins** are distinct by direction: a **product input** (an *icon* of an arrow pointing
  into the block) and a **product output** (an *icon* of an arrow pointing out), in the *Installation*
  pane. The direction is the **glyph**, not a character in the label text.
- [ ] MUST: **Link rows** are distinct by direction, carried by the row's **icon**: an **outgoing** row sits
  on a **source** pin (which owns the `link_from_resource` half) and an **incoming** row on a **sink** pin
  (which owns the `link_to_resource` half); both appear in both panes, and each row's **label is the bare
  full path** of the other end (US-022). ⚠ The element names read backwards from the roles — a source owns
  the *from* half — so never map "source" to the *to* half (the F‑066 trap).
- [ ] MUST: The direction the icon states is the link's **real** direction: a **`→` (outgoing) glyph means
  the row's own pin is the signal's SOURCE**, and **`←` (incoming) means it is the SINK**. The icon is the
  only thing that says so — so if the underlying orientation is wrong, the icon is wrong and **nothing else
  on screen contradicts it** (US-022).

  > **Confirmed 2026‑07‑16, and it earned its keep 2026‑07‑17.** *(Was: the "link from" row "rendered with a
  > leading `←` and the other end's full path".)* This epic once **mandated the defect**: IHC Visual renders
  > the bare path and puts direction in the icon only, while IHC OpenVisual rendered `→ Entré/Gang / … /
  > Udgang` — an arrow prefix in the **label text** *on top of* a direction icon on the same row. An
  > implementer building from that criterion would have been told to duplicate the direction. The `→`/`←`
  > here mean the **glyph**; the label is bare. (Icon *artwork* remains an allowed difference — that was
  > label text, and the glyph semantics are unaffected.) Evidence: `RESULTS.md` **F‑020**; backlog **A‑7**
  > (done); the rule lives in US-022.
  >
  > ⭐ **IHC Visual's own link rows were later read and they carry exactly this mapping** — a button shows
  > `→` (it is the source), a product output shows `←` (it is the sink), mirrored in the *Functions* pane.
  > That reading arrived from a screenshot, owing nothing to the file format, and it **independently
  > confirmed a serious defect found the same day**: IHC OpenVisual had been writing every link's two halves
  > backwards (US-022, F‑066), so **before that fix these icons would have pointed the wrong way** — with no
  > label text left to contradict them, precisely because A‑7 removed it. The second MUST above exists to
  > name that dependency: dropping the redundant prefix was right, and it made the icon load‑bearing.
  > Evidence: `RESULTS.md` **F‑070**.
- [ ] MUST: **Variable types** each have a distinct icon: Input, Output, Flag, Date, Weekday,
  Time of day, Counter, Integer, Decimal, Timer, Timer value, Enumerator, Light level, Temperature, Holiday,
  Humidity, Light, and the S0 power/energy type *Energy / Power* (kW/kWh/W/Wh).
- [ ] MUST: **Program elements** each have a distinct icon: Program, Sub‑program (sub‑program),
  Event group (event group), Event (event), Conditions‑AND vs Conditions‑OR (the two
  logic‑group operators), Condition (single condition), Command group (command group), Command
  (single command).
- [ ] MUST: **Scenario** pins carry the scenario icon (used to identify scenario‑capable outputs,
  US-024) in both *Installation* and *Functions*.
- [ ] MUST *(⛔ **catalogued but out of scope pending E8** — not part of US-046's implemented scope)*: In
  **simulation mode**, input/output state is coloured **red = OFF** and **green = ON** (shown as red/green
  arrows in the program view); a **Breakpoint** shows a full‑stop icon at the start of a line.

  > **Ruling R‑5, 2026‑07‑17 — catalogued, not exercisable.** ⛔ **E8 (simulation) is out of scope** by the
  > 2026‑07‑16 ruling, **exception #2**. There is therefore no simulation mode to inspect, so this MUST can
  > never be exercised by this story's **Inspection** verification — and a ✅ **Implemented** story must not
  > carry an obligation that cannot be met. The glyph documentation **stays** (here and in the *Simulation*
  > row of the reference table) as the **catalogue for when/if E8 is ever built**; it is not a live
  > requirement on US-046. Tracked as ruling **R‑5** in `tmp\research3.md` §7.
- [ ] SHOULD: An **unlinked wireless product** shows a leading yellow **!**; an unconfigured product
  keeps the **!** until configured — this decoration is distinct from the product’s type icon.
- [ ] SHOULD: A variable node shows its value inline as `Name = <value>` next to its type icon (e.g.
  `Counter = 0`, `Temperature = 0.0 °C`).

### AC illustrations

- In the *Functions* pane, a library `<function block>` shows the **library block** badge; its input pins
  (`<pin>`) use the **FB‑input** icon; its scene pins use the **scenario** icon.
- Filling a block’s internals shows type‑icon + inline value rows: `Weekday = Monday`, `Flag = OFF`,
  `Counter = 0`, `Timer = 00:00:00.000`, `Humidity = 0.0% RH`.
- During simulation, an input pin driven OFF is red and turns green when toggled ON (US-035).

### Reference — icon categories

| Category | Members | Where |
|---|---|---|
| Function blocks | Library function block; Function block | Functions |
| Containers | Localities; Settings; Internal variables (programming mode); RS485 modules | both / Functions |
| Product pins | Product input; Product output | Installation |
| Links | Link to…; Link from… | both |
| FB pins | Input to function block; Output from function block | Functions |
| Variables | Flag, Date, Weekday, Time of day, Counter, Integer, Decimal, Timer, Timer value, Enumerator, Light level, Temperature, Holiday, Humidity, Light, Energy / Power (kW/kWh/W/Wh) | Functions |
| Program elements | Program, Sub-program, Event group, Event, Conditions (AND), Conditions (OR), Condition, Command group, Command | Programming |
| Scenario | Scene | Functions / Installation |
| Simulation | Breakpoint; red = OFF / green = ON | all windows, sim mode |

### Constraints

- Verification method — **Inspection** of the rendered node icons in the application.
- **Settled exclusion — the artwork is an allowed difference.** The **artwork itself is not specified**
  beyond the descriptions above; IHC OpenVisual must provide the *distinctions and meanings*, choosing
  equivalent glyphs. This is a **ruling, not a deferred confirmation** — it needs no closing and should not
  be re‑raised as an open item. ⚠ **The exception covers artwork only.** By the **division of labour fixed
  2026‑07‑16** (see the epic head), direction / type / state markers are the **icon's** job and the label
  carries text only — *"neither is an icon‑artwork question, so neither is covered by this epic's artwork
  exception"*. Dropping the marker does not widen the exception.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
