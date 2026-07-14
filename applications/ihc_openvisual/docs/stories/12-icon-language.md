---
version: 0.1.0
last-updated: 2026-07-03
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
variable types, program elements, scenario, breakpoint) and the simulation state colours. **Scope
excludes:** the exact bitmap artwork (IHC OpenVisual supplies equivalent glyphs); this story fixes *which
categories are visually distinguished and what each means*.

> **Artwork‑level spec:** the visual style, technical construction and functional behaviour of the actual
> icons — enough to build the whole set in a modern style — is documented separately in
> [`../icons_design.md`](../icons_design.md) with codes in [`../icon_codes.md`](../icon_codes.md).
This story stays at the "which categories / what they mean" level.

**Acceptance criteria (epic level):**

- MUST: Every node category listed below is rendered with a distinct, consistent icon so its type is
  identifiable without reading the label.
- MUST: In simulation mode, input/output state is shown by colour: **red = OFF, green = ON**.
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
- [ ] MUST: **Product pins** are distinct by direction: a **product input** (arrow pointing into the
  block, shown as `→` at the pin) and a **product output** (arrow pointing out, `←`), in the
  *Installation* pane.
- [ ] MUST: **Link rows** are distinct: **Link to…** ("link to", source side) and **Link from…** ("link
  from", target side, rendered with a leading `←` and the other end’s full path), appearing in both
  panes.
- [ ] MUST: **Variable types** each have a distinct icon: Input, Output, Flag, Date, Weekday,
  Time of day, Counter, Integer, Decimal, Timer, Timer value, Enumerator, Light level, Temperature, Holiday,
  Humidity, Light, and the S0 power/energy type *Energy / Power* (kW/kWh/W/Wh).
- [ ] MUST: **Program elements** each have a distinct icon: Program, Sub‑program (sub‑program),
  Event group (event group), Event (event), Conditions‑AND vs Conditions‑OR (the two
  logic‑group operators), Condition (single condition), Command group (command group), Command
  (single command).
- [ ] MUST: **Scenario** pins carry the scenario icon (used to identify scenario‑capable outputs,
  US-024) in both *Installation* and *Functions*.
- [ ] MUST: In **simulation mode**, input/output state is coloured **red = OFF** and **green = ON**
  (shown as red/green arrows in the program view); a **Breakpoint** shows a full‑stop icon
  at the start of a line.
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
- The **artwork itself is not specified** beyond the descriptions above; IHC OpenVisual must
  provide the *distinctions and meanings*, choosing equivalent glyphs. (R‑note, not a blocker.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
