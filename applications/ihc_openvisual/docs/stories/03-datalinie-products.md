---
version: 0.3.1
last-updated: 2026-07-18
status: draft
---

# E3 — Wired (data-line) products

**Goal:** Let an IHC installer place wired *data-line* products into localities, document them, and
address their inputs and outputs to physical data lines and I/O-module terminals — so the installation
model matches the real wiring.

**Scope:** inserting wired products (the product categories the catalog defines) via context menu
or the *Insert* menu; the product-properties (documentation) dialog; per-terminal configuration of
inputs and outputs (data line + module terminal, in-use indication, output initial value); and the
special-products path for a `<product>` special product. **Scope excludes:** wireless products (E4),
function-block links (E6), reporting (E9), and the remaining *Special products* (discontinued,
third-party and misc products) beyond that `<product>`.

**Acceptance criteria (epic level):**
- MUST: The installer can insert any wired product into a selected locality and see it nested under
  that locality with the input/output/scenario pins the catalog defines for it (US-010).
- MUST: Each product exposes a documentation-properties dialog and per-terminal addressing to a data
  line and I/O-module terminal.
- SHOULD: The status bar confirms each insertion by product name and target locality.
- MUST: At most one modem can exist in a project.

**Readiness:** Ready.

---

## US-010 — Insert a wired product

**As an** IHC installer, **I want** to insert a wired product into a chosen locality from a categorised
menu, **so that** the product appears in the installation tree ready to document and address.

**Scope excludes:** filling the properties dialog (US-011) and addressing terminals (US-012).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Insert a product via the locality context menu
  Given the "Installation" pane shows the locality "Living room"
  When I right-click "Living room" and follow "Products" > "Wired products" > <group> > <group-detail> > <product>
  Then the product is inserted as a child of "Living room"
  And the status bar reads: Product '<product>' inserted under Living room
  And the product node can be expanded to reveal its input, output and scenario pins

Scenario: Insert the same product via the menu bar
  Given the locality "Living room" is selected (highlighted)
  When I use "Insert" > "Products" > "Wired products" > <group> > <group-detail> > <product>
  Then the product is inserted under "Living room" identically to the context-menu route

Scenario: Product categories come from the catalog
  Given the "Wired products" submenu is open
  Then it offers the product categories the catalog defines
```

### Business rules — the catalog's category structure

The insert menu's categories come from the catalog; that catalog structure is the spec:

- MUST: The menu offers a **`Bus Produkter`** category holding the bus products — the SMS modem and the
  **IHC LED Dimmer 2 kanaler**. A bus product is not a wired data-line product and is not a special
  product.
- MUST: The **special-products** category holds the full set — `Modificeret Wireless produkter`,
  `Vinduer` and `Udgaet produkter` (discontinued), plus the loose specials `S0 Device`,
  `Controller Link OUT`, `Controller Link IN` and the signal-strength test equipment.
- MUST: Every **structural / chrome** label renders in English — the three top-level category names
  (`Datalinie produkter` → *Wired products*) **and** the subcategories (`Generelle` → *General*;
  `Indgang`/`Udgang`/`Dimmer` → *Input*/*Output*/*Dimmer*). Only **user-entered** content (product names,
  notes, `Placering`, program names) stays Danish — as do the **function-block library** category names,
  which US-018 keeps verbatim as catalog data. The structural category rules above define which
  categories exist; only the rendered labels change.

### Business rules — how the tree renders a product

**The product's own label.** IHC OpenVisual renders the placement descriptor into the label:

- MUST: When the product carries a **`position`**, the label is `name (position) ` — **including the
  trailing space**. When `position` is absent or empty the label is the bare `name`, with **no empty
  parens**.
- MUST: The source is the **`position`** attribute, **not `note`**. The same element also carries a
  `note=` holding a long description (e.g. *"Til styring af Silent Gliss 4760/10522 gardin…"*) that IHC
  OpenVisual never puts in the label — it surfaces as the hover tooltip instead (US-047). `position` is often
  the only thing distinguishing a project's many same-type siblings (*Lampeudtag* ×10+), so omitting it
  would show repeated identical rows; the trailing space is part of the format's label convention and is reproduced deliberately.

**Which of a product's child rows the tree shows.** A product's `.vis` body may hold resources that IHC
OpenVisual deliberately does **not** draw. The tree shows only some of them, by two disjoint criteria
(neither catches the other's case):

- MUST: A shutter product's `airlink_shutter_up` / `airlink_shutter_down` pins (*Op* / *Ned*) are
  **not shown**. They are identified by **element tag alone**. The rule is **not** generalised to
  `airlink_shutter_lock` (the *Lås* pin on `Jalousi 2 tast`): the *Lås* pin **is shown**, so
  `airlink_shutter_lock` must **not** be added to the hide list — inserting `Jalousi 2 tast (lokal lås)`
  renders *Tryk (venstre) · Tryk (højre) · Lås · Tilstand · Scenarier/regulering*, hiding only *Op*/*Ned*.
- MUST: A resource carrying **`setting="yes"`** (a sensor/thermostat calibration row such as
  *Kalibrering af temperaturføler*) is **not shown**. Tag cannot decide this one: it shares its
  `resource_temperature` tag with the *visible* *Temperatur* / *Dugpunkt* rows of the same product.
- MUST: Suppression is **display-only**. Both row kinds stay in the `.vis` and are written back
  verbatim on save — hiding them must not change a single byte the engine emits.
- MUST: A hidden row is **not offered as a link source or target** either (US-022), since it has no
  tree row to drag from or drop on.

**State rows show their value.** IHC OpenVisual appends the value to a state row's label:

- MUST: A **`resource_enum`** row renders `name = <state>`, where `<state>` is the **name of the
  `enum_value` its `inivalue` points at** — e.g. `Tilstand = Ukendt`, `Log Fugt = Off`.
- MUST: This is the **initial** value (the enum's index-0 member), read through the project's enum
  definitions — **not** live controller state.
- MUST: `resource_enum` is **not the only** row kind that does this. IHC OpenVisual renders the literal on a
  function block's **`Indstillinger`** (settings) rows too — `Timertid = 00:10:00`,
  `Sluk Tidspunkt = 00:00:00` — and neither of those is an enum.

### AC illustrations

- Inserting a push-button product under a locality yields a product node named by the catalog
  (`<product>`), exposing its catalog-defined input pins (`<pin>`); an output product exposes an
  output pin plus a scene pin.
- A sensor product with logging sub-resources expands to its catalog-defined pins (`<pin>`), some
  carrying a catalog default value shown inline as `name = value` — i.e. a product's fixed
  sub-resources and their default values are displayed.
- A scene-capable product (e.g. `Lampeudtag`, `Stikkontakt`, `Dimmer Universal`) also auto-creates a
  **`Scenarier`** scene container (rendered `Scenarier/regulering` on dimmers) on insert — a node ready
  to hold scenes, with no scene members until authored.

### Constraints

- Verification method — **Demonstration** that both the context-menu and *Insert*-menu routes insert
  the product under the selected locality with the confirming status-bar string.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — insertion, the `(position)` label and the
row-suppression rules work; the insert menu is a **subset** of the catalog's (the `Bus Produkter` and
several *Special products* categories are short), and function-block settings rows do not yet render
their `= <value>`.

---

## US-011 — Fill product documentation properties

**As an** IHC installer, **I want** to fill in a product's documentation fields when I insert it (or
later via properties), **so that** the generated reports (E9) describe the installation accurately.

### Acceptance criteria (Business Rules)

**Presentation rules:**
- MUST: When a product is inserted, it is added under the selected locality and **no dialog opens
  automatically**. The installer opens the *Product properties* dialog on demand by selecting the
  product and pressing `F2` (or right-click > *Properties*). (IHC OpenVisual's silent insert is an
  intentional design choice — insertion never interrupts the installer with a dialog.)
- MUST: The dialog is **titled with the product type** — e.g. `Lampeudtag` — not a generic *Product
  properties*. This is how two open product dialogs are told apart.

**Input fields (each a labelled control; free-text unless a list is noted):**
- MUST: **Name** — shown pre-filled. **Editability is gated by the placed product's own `locked`
  attribute**, not by its type: the field is **disabled** (shown, greyed, not editable) exactly when the
  `locked` attribute **on that product's element in the project** resolves to `yes`, and editable
  otherwise. When it is disabled, the name shown equals the catalog's type name.
- MUST: `locked` is resolved against **the project's own inline DTD**, which defaults it to **`no`** — so a
  product element that simply **omits** `locked` is **editable**. The attribute is only *seeded* onto an
  element from the catalog when the product is first inserted; from then on the element is the truth.
  (Resolving by catalog *type* would get this wrong, because the catalog's grammar defaults `locked` to
  `yes` — the two disagree for any element that omits the attribute.)
- MUST: **Placering** — the product's **placement descriptor**: where in the room it physically sits, e.g.
  `i loft` ("in the ceiling"). This is **not** the parent room — the room is implied by the product's
  position in the tree and is not shown in the dialog. It is **free text with suggestions**, not a fixed
  list, and there is **no `Location` room dropdown** (moving a product is a tree operation, US-054, not a
  dialog field).
- SHOULD: **Note** — free text; in some products a list of standard notes is offered. The note surfaces as
  the product's hover tooltip (US-047) and in reports — **not** in the tree label, which renders `Placering`
  (US-010).
- SHOULD: **Cable type** — pick from a list or free text.
- SHOULD: **Cable numbering** (group) — drop-down or free text.
- SHOULD: **Identification code** — free text; the unique product number.
- SHOULD: **Light group** — drop-down; **MAY be absent** for products with no light-group relationship.
- SHOULD: **Include this product in the end-user report** — the UI for the product's `enduser_report`
  attribute, feeding US-040's end-user report. IHC OpenVisual **hides this checkbox** — the attribute
  still round-trips through the (hidden) control, but there is no toggle affordance.
- MUST: The dialog also carries the product's **terminal-addressing section** — the `Indgange` / `Udgange`
  grids and their per-terminal address editor, specified in **US-012**.

**How the text fields behave — the drop-down question:**
- All seven of the fields above (*Placering*, *Note*, *Kabeltype*, *Kabelnummer*, *Identifikationskode*,
  *Lysgruppe*, and *Navn* when unlocked) are **plain textboxes** in IHC OpenVisual — a deliberate design
  decision (C): a drop-down of prior values would depend on a machine-local MRU store that is not part of
  the `.vis` and does not travel with the project, so that affordance is dropped, and any offered list is
  treated as **suggestions, not constraints**.

**Output:**
- The product node carries the entered documentation, which later feeds the installation and end-user
  reports; fields left blank are simply omitted from reports (US-040).

### AC illustrations

- For a `Lampeudtag` (whose element carries `locked="yes"`), the dialog is titled `Lampeudtag`, shows *Name*
  = `Lampeudtag` **greyed and not editable**, *Placering* = `i loft`, the note/cable/identification/
  light-group fields, and an `Udgange` terminal grid (US-012). No room dropdown appears — the product's room
  is `Entré/Gang` because that is where it sits in the tree.
- For a `Bevægelsessensor 1873 Bobby-AM` in the same project — whose element **omits** `locked` — the very
  same dialog shows *Name* **editable**. The two products differ in nothing but that attribute, which is why
  the gate reads the element and not the type.
- Setting *Placering* = `i loft` documents where in the room the product sits, and the tree row becomes
  `Lampeudtag (i loft) ` (US-010).

### Constraints

- Verification method — **Inspection** of the dialog fields, and **Test** of the *Name* gate: an element
  carrying `locked="yes"` → Name **disabled**; an element that **omits** `locked` → Name **enabled**; and a
  freshly **inserted** product → Name reflects the catalog-seeded `locked`.
- None of the free-text fields has a length limit or validation specified: the app accepts free text and
  treats any offered list as suggestions. *Name*'s editability is gated by `locked`.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-012 — Configure product input/output terminals & initial value

**As an** IHC installer, **I want** to map each product input to a data line and input-module terminal
(and each output to an output-module terminal, with an initial value), **so that** the model reflects
the physical wiring and the controller can drive real hardware.

**Scope excludes:** the product's documentation fields (US-011). Wireless products are **not** excluded:
the wireless dialog is the *same* dialog with the *same* `Indgange`/`Udgange` grids, enabled by the
product's shape. US-014 owns the wireless side; the grid and address spec below applies to both families.

### Acceptance criteria (Business Rules)

**Terminal grids — in the product properties dialog (US-011):**
- MUST: The dialog lists the product's terminals in **two grids that are both always present** — one for
  inputs (`Indgange`) and one for outputs (`Udgange`) — with one row per terminal the catalog defines for
  that product type. A product with no inputs shows an **empty** `Indgange` grid whose *Configure input*
  button is **disabled** (never a *missing* grid); likewise for outputs. This holds for **wireless** products
  too: an input-only wireless sensor has an enabled `Indgange` grid and a disabled `Udgange` grid (US-014).
- MUST: Each terminal row shows four columns: **name**, **address**, **wire colour** and **note**.
- MUST: A terminal that has not been addressed shows an empty address cell, so the installer can see at a
  glance which terminals still need wiring.
- MUST: A terminal's address editor opens by **double-clicking its grid row** *and* from a *Configure input*
  / *Configure output* button — **two routes onto the same sub-dialog** (US-044 route equivalence). A
  **single** click only selects the row.
- MUST: Each *Configure* button is **disabled when its grid is empty** — a product with no inputs offers no
  way to configure one.
- SHOULD: Each grid carries a hint that its rows are configurable — IHC OpenVisual heads each grid with the
  literal text `<klik for at konfigurere>` ("click to configure").

**Address editor — a sub-dialog, one terminal at a time:**
- MUST: The editor offers an **address picker of two lists**: the **data line / module** (module 1–16, each
  shown with its module type, e.g. *Output 230/10*) and the **terminal** on that module (port 1–8).
  Choosing one from each addresses the terminal.
- MUST: The module list offers an explicit **not-configured** entry (`ikke konfigureret`), so an addressed
  terminal can be returned to unaddressed rather than only ever moved to another port.
- MUST: The editor offers the terminal's **name**, **note** and **wire colour** (`Ledningsfarve`). The
  terminal's **name is read-only** — it comes from the product's catalog type.
- MUST: An **output** terminal's editor offers an **initial value** (`Initial værdi`): `OFF` configures the
  output as normally-open (NO), `ON` as normally-closed (NC).
- MUST: An **output** terminal's editor offers a **power-fail behaviour** — whether the output's current
  value is saved and restored after a power failure (`Ved strømsvigt` → `Gem aktuel værdi`). This is the
  same save-current-value flag US-033 backs up.
- MUST: The terminal list marks which ports of the chosen module are **already in use** (IHC OpenVisual renders
  them `1 (i brug)` … `8 (i brug)`), so a port is not double-booked.
- SHOULD: The editor offers **Apply** alongside OK/Cancel, and OK stays **disabled until something changes**
  — so an editor opened to read an address cannot accidentally rewrite it.
- MAY: The same editor is reachable from the tree by selecting the pin and opening its properties.

**Output:**
- Each addressed terminal carries a module-and-port address that round-trips to the `.vis` file and renders
  in the installation report (US-040); an unaddressed terminal stays unaddressed rather than silently
  defaulting to a port.

### AC illustrations

- `Lampeudtag`'s properties dialog shows an `Udgange` grid with one row — name `Udgang`, address
  `Datalinie 2.01`, wire colour `Brun`. Opening that row's address editor shows *Datalinie* = module 2
  (*Output 230/10*), *Udgang* = port 1, *Initial værdi* = `OFF`, and the power-fail *save current value*
  option.
- Leaving an output's initial value at `OFF` configures it normally-open; switching it to `ON` makes it
  normally-closed.

### Constraints

- Verification method — **Test**: opening a product's properties shows its terminal rows with addresses and
  wire colours, and an address set in the editor round-trips to the `.vis` as `Datalinie N.PP`.
- The `.vis` model already carries terminal addresses (they render in the installation report today), so
  this story adds a dialog surface over data the engine already holds.

**Readiness:** Ready.

**Implementation status:** ⛔ Not implemented — the *Product properties* dialog carries no terminal grid,
per-terminal address, wire colour, initial value, power-fail option or configure affordance, so a product's
terminals cannot be addressed yet.

---

## US-013 — Insert a special product (modem)

**As an** IHC installer, **I want** to insert a modem `<product>` from the special-products menu and
set its properties, **so that** the installation can notify by phone/SMS — subject to the one-modem
rule.

**Scope excludes:** the scene/notification setup that is configured outside this app (out of scope).

### Acceptance criteria (Business Rules)

**Insertion & constraint rules:**
- MUST: A modem is inserted via right-click a locality > *Products* > *Bus Produkter* > `<product>`
  (US-010's category structure; the category label renders in English — *Bus Products* — per the
  Full-English rule). **No properties dialog opens on insert** (US-011).
- MUST: A project may contain **at most one** modem, regardless of `<product>`.
- SHOULD: A refused second-modem insertion **tells the installer why** rather than appearing to do nothing.
  (The explanatory feedback is a deliberate design decision, per the ruling in the product
  constraints.)

**Property groups (dialog "Modem properties"):**
- SHOULD: **Modem properties** — Name (type name), Note (appended in parentheses after Name), Location,
  Identification code.
- SHOULD: **Cabling** — wire colours for 0 V, 24 V, RS485 minus, RS485 plus.
- SHOULD: **Telephone numbers** — Number 1–4, dialled in priority order (Number 2 is dialled only if
  Number 1 is unanswered, Number 3 if Number 2 is unanswered, Number 4 if Number 3 is unanswered).
- SHOULD: **Settings** — Access code (4-digit access code, default `1234`); Call pause
  (integer, **1–99 minutes**); Call delay (integer, **1–99 seconds**); ID code (alarm-centre
  identifier, text); Number of rings (integer, **0–9**; `0` means the modem never answers).

**Property groups (dialog "SMS modem properties"):**
- SHOULD: **SMS modem properties** — Name, Note, Location, Identification code.
- SHOULD: **Cabling** — 0 V / 24 V / RS485 minus / RS485 plus wire colours.
- SHOULD: **Settings** — PIN code (SIM PIN; irrelevant if the SIM has none).
- SHOULD: **Telephone numbers** — Number 1–30; each **3–20 characters**, no spaces, must start with a
  country code (e.g. `+45` for Denmark).

**Output:**
- A `<product>` node exposes its catalog-defined pins (`<pin>`), enabling telephone control to be wired into function blocks.
- A `<product>` node stores its documentation, cabling, PIN and phone-number list; direct SMS
  control/notification setup is configured in separate IHC administration tools, outside this app.

### AC illustrations

- Inserting a modem `<product>` in `Utility room` with *Number of rings* = `0` documents a modem that dials out on
  alarms but never answers incoming calls; setting *Call pause* = `1` gives a one-minute wait between
  the four numbers.
- Attempting to add a second modem is blocked by the one-modem rule regardless of type.

### Constraints

- Verification method — **Inspection** of the two modem dialogs, and **Test** of the one-modem constraint.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented (SMS modem) — the dialog fields and the one-modem rule
exist; the *Products > Bus Produkter > `<product>`* insert route depends on the Bus category being present in
the menu (US-010).
