---
version: 0.2.0
last-updated: 2026-07-16
status: draft
---

# E3 — Wired (data-line) products

> **Implementation status:** 🟡 Partly implemented — insertion works; **terminal addressing (US-012) does
> not exist**, and four product‑dialog rules diverge from the vendor (US-011). See each story.

> **Current scope:** ✅ **In scope** — inserting, documenting and addressing wired products is
> project CRUD.

**Goal:** Let an IHC installer place wired *data-line* products into localities, document them, and
address their inputs and outputs to physical data lines and I/O‑module terminals — so the installation
model matches the real wiring.

**Scope:** inserting wired products (the product categories the catalog defines) via context menu
or the *Insert* menu; the product‑properties (documentation) dialog; per‑terminal configuration of
inputs and outputs (data line + module terminal, in‑use indication, output initial value); and the
special‑products path for a `<product>` special product. **Scope excludes:** wireless products (E4),
function‑block links (E6), reporting (E9), and the remaining *Special products* (discontinued,
third‑party and misc products) beyond that `<product>`.

**Acceptance criteria (epic level):**
- MUST: The installer can insert any wired product into a selected locality and see it nested under
  that locality with the input/output/scenario pins IHC Visual shows for it (US-010).
- MUST: Each product exposes a documentation‑properties dialog and per‑terminal addressing to a data
  line and I/O‑module terminal.
- SHOULD: The status bar confirms each insertion by product name and target locality.
- MUST: At most one modem can exist in a project.

**Readiness:** Not Ready — two open measurements, neither blocking the bulk of the epic:
- [R3] US-012 — the terminal‑row open **gesture** (single‑ vs double‑click) is TBD (pending capture).
- [R5] US-011 — **which product types lock the *Name* field** is TBD (pending capture).

---

## US-010 — Insert a wired product

**As an** IHC installer, **I want** to insert a wired product into a chosen locality from a categorised
menu, **so that** the product appears in the installation tree ready to document and address.

**Scope excludes:** filling the properties dialog (US-011) and addressing terminals (US-012).

### Acceptance criteria (Given‑When‑Then)

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

The insert menu's categories are IHC Visual's, and the vendor's structure is the spec:

- MUST: The menu offers a **`Bus Produkter`** category holding the bus products — the SMS modem and the
  **IHC LED Dimmer 2 kanaler**. A bus product is not a wired data‑line product and is not a special
  product.
- MUST: The **special‑products** category holds the vendor's full set — `Modificeret Wireless produkter`,
  `Vinduer` and `Udgaet produkter` (discontinued), plus the loose specials `S0 Device`,
  `Controller Link OUT`, `Controller Link IN` and the signal‑strength test equipment.
- MUST: Category names are presented in **one** language throughout the menu — a category name is not left
  in the vendor's Danish among otherwise‑translated siblings.

> **Corrected 2026‑07‑16 (was: "it offers the product categories the catalog defines", which asserted
> nothing checkable).** Walking both menus found three concrete gaps: (1) **no `Bus Produkter` category** —
> IHC OpenVisual re‑homes `SMS Modem` under *Special products*, and **`IHC LED Dimmer 2 kanaler` appears
> nowhere at all**; (2) **`Special products` holds only `SMS Modem`**, missing the three sub‑categories and
> the four loose specials; (3) **`Generelle` is untranslated** among English siblings (*Input*/*Output*/
> *Dimmer*) — a localisation slip, not a vendor‑parity issue (Danish‑vs‑English wording is otherwise an
> allowed difference). `Wired products` ↔ `Datalinie produkter` and the whole wireless subtree already
> match. Evidence: `RESULTS.md` **F‑028** (vendor `catalog.products` = **100** products with category
> paths); backlog **A‑11**.
>
> ⚠ **Leaf‑level parity is unverified** — the menu walk was bounded at depth 3, so the vendor's 100
> products were never matched leaf‑for‑leaf (`RESULTS.md` **E‑7**). The missing categories imply missing
> products, so expect this rule to grow once the leaves are diffed; size **A‑11** after E‑7, not before.

### Business rules — how the tree renders a product

**The product's own label.** IHC Visual renders the placement descriptor into the label:

- MUST: When the product carries a **`position`**, the label is `name (position) ` — **including the
  trailing space**. When `position` is absent or empty the label is the bare `name`, with **no empty
  parens**.
- MUST: The source is the **`position`** attribute, **not `note`**. The same element also carries a
  `note=` holding a long description (e.g. *"Til styring af Silent Gliss 4760/10522 gardin…"*) that IHC
  Visual never puts in the label — it surfaces as the hover tooltip instead (US-047).

> **Added 2026‑07‑16 (was: label = `name` only).** `position` is often the *only* thing distinguishing a
> project's many same-type siblings (*Lampeudtag* ×10+), so omitting it showed repeated identical rows —
> a real usability loss, not cosmetic. The trailing space is the vendor's and is reproduced deliberately:
> invisible on screen, it keeps a label-mode tree diff against IHC Visual exact. Evidence: `RESULTS.md`
> **F‑003** (verified byte-for-byte against 4 products); backlog **A‑2**.

**Which of a product's child rows the tree shows.** A product's `.vis` body may hold resources that IHC
Visual deliberately does **not** draw. The tree shows the vendor's row set, by two disjoint criteria
(neither catches the other's case):

- MUST: A shutter product's `airlink_shutter_up` / `airlink_shutter_down` pins (*Op* / *Ned*) are
  **not shown**. They are identified by **element tag alone** — they carry no distinguishing
  attribute and reuse their first `airlink_input` sibling's `address_channel`.
- MUST: A resource carrying **`setting="yes"`** (a sensor/thermostat calibration row such as
  *Kalibrering af temperaturføler*) is **not shown**. Tag cannot decide this one: it shares its
  `resource_temperature` tag with the *visible* *Temperatur* / *Dugpunkt* rows of the same product.
- MUST: Suppression is **display-only**. Both row kinds stay in the `.vis` and are written back
  verbatim on save — hiding them must not change a single byte the SDK emits.
- MUST: A hidden row is **not offered as a link source or target** either (US-022), since it has no
  tree row to drag from or drop on. This matches IHC Visual.

**State rows show their value.** IHC Visual appends the value to a state row's label:

- MUST: A **`resource_enum`** row renders `name = <state>`, where `<state>` is the **name of the
  `enum_value` its `inivalue` points at** — e.g. `Tilstand = Ukendt`, `Log Fugt = Off`.
- MUST: This is the **initial** value (the enum's index‑0 member), read through the project's enum
  definitions — **not** live controller state. OpenVisual reads no controller here.
- MUST: Only `resource_enum` does this. `inivalue` is also used as a **literal** elsewhere
  (`resource_flag` `on`/`off`, the hidden calibration rows' `0.00`); those rows stay bare.

> **Added 2026‑07‑16 (was: state rows rendered the bare name).** Evidence: `RESULTS.md` **F‑004**;
> backlog **A‑3**. The vendor's two examples (*Tilstand*, *Log Indgang*) turn out to be **one** row kind,
> not two — a product's `Log …` rows are themselves `resource_enum` over the *Logning* enum. The rule is
> per **element type**, so it also reaches a function block's enum *variable* (`Mode = Direct`); the
> vendor side of programming-mode variable rows is uncensused (`RESULTS.md` **E‑4**), and scoping the
> rule to products would have meant inventing a distinction the evidence does not support.

> **Added 2026‑07‑16 (was: every non-structural resource rendered as a pin).** OpenVisual rendered the
> file faithfully and so showed rows the vendor hides — *Jalousi 4 tast* had 8 child rows against the
> vendor's 6, *Fugt / Temperatur sensor* 5 against 4. The vendor is the authoritative spec. Evidence:
> `RESULTS.md` **F‑001** (tag rule) and **F‑002** (attribute rule); backlog **A‑1**. The rule lives once,
> in `Ihc.Vis.Schema.ProductRows`, as a vendor-grammar fact.

### AC illustrations

- Inserting a push-button product under a locality yields a product node named by the catalog
  (`<product>`), exposing its catalog-defined input pins (`<pin>`); an output product exposes an
  output pin plus a scene pin.
- A sensor product with logging sub‑resources expands to its catalog-defined pins (`<pin>`), some
  carrying a catalog default value shown inline as `name = value` — i.e. a product’s fixed
  sub‑resources and their default values are displayed.

### Constraints

- Verification method — **Demonstration** that both the context‑menu and *Insert*‑menu routes insert
  the product under the selected locality with the confirming status‑bar string.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-011 — Fill product documentation properties

**As an** IHC installer, **I want** to fill in a product’s documentation fields when I insert it (or
later via properties), **so that** the generated reports (E9) describe the installation accurately.

### Acceptance criteria (Business Rules)

**Presentation rules:**
- MUST: When a product is inserted, it is added under the selected locality and **no dialog opens
  automatically** — matching IHC Visual. The installer opens the *Product properties* dialog on demand
  by selecting the product and pressing `F2` (or right‑click > *Properties*).

  > **Corrected 2026‑07‑16 (was: dialog opens automatically on insert).** The earlier MUST required the
  > dialog to auto‑open, on the belief that IHC Visual does so. The side‑by‑side comparison measured the
  > opposite — the vendor does **not** auto‑open on insert (product lands under the caret, no modal) — so
  > OpenVisual matches the vendor. Evidence: `RESULTS.md` **F‑027**; backlog **A‑14** tracks removing the
  > auto‑open from the implementation.

- MUST: The dialog is **titled with the product type** — e.g. `Lampeudtag` — not a generic *Product
  properties*. This is how two open product dialogs are told apart.

  > **Corrected 2026‑07‑16 (was: a generic "Product properties" title).** IHC Visual titles the dialog with
  > the product **type**; IHC OpenVisual titles every product dialog identically. Small, but it is the only
  > thing distinguishing two open dialogs. Evidence: `RESULTS.md` **F‑015**; backlog **A‑8**.

**Input fields (each a labelled control; free‑text unless a list is noted):**
- MUST: **Name** — the product type name; shown pre‑filled. **Editability is gated by product type:** for
  types whose name is fixed to the type, the field is **disabled** (shown, greyed, not editable); for the
  rest it is editable.

  > **Corrected 2026‑07‑16 (was: Name always editable).** IHC Visual **disables** Navn for `Lampeudtag` —
  > the name is fixed to the product type for that type — while IHC OpenVisual offers an editable textbox
  > for every product. Per the 2026‑07‑16 ruling the vendor is authoritative, so the gate is adopted.
  > Evidence: `RESULTS.md` **F‑032**; backlog **A‑15**.
  >
  > **TBD (pending capture):** *which* product types lock the name. Only one type (`Lampeudtag`, locked)
  > was sampled, so the rule behind the gate is unknown — do not guess it. Sample 2–3 further types on the
  > vendor (one expected editable) before implementing, and derive the predicate from what they show.

- MUST: **Placering** — the product's **placement descriptor**: where in the room it physically sits, e.g.
  `i loft` ("in the ceiling"). One of a fixed list. This is **not** the parent room — the room is implied
  by the product's position in the tree and is not shown in the dialog.

  > **Corrected 2026‑07‑16 (was: "Location — drop‑down of localities or free text").** Two divergences in
  > one field. IHC OpenVisual **dropped** the vendor's `Placering` placement descriptor (so the placement
  > text can be neither edited nor kept) and **added** a `Location` room dropdown the vendor's dialog does
  > not have — effectively a move‑to‑room control. Per the ruling the vendor is authoritative: **add
  > `Placering`, and remove the room dropdown** — moving a product is a tree operation (US-054), not a
  > dialog field. These are genuinely **different fields, not a mistranslation**: the *values* differ
  > (`i loft` vs a room name). ⚠ This field is also what US-010's tree label renders — `Lampeudtag (i loft) `
  > — so dropping it also cost the tree its only way of telling same‑type siblings apart (F‑003).
  > Evidence: `RESULTS.md` **F‑031**; backlog **A‑13**.

- SHOULD: **Note** — free text; in some products a list of standard notes is offered. The note surfaces as
  the product's hover tooltip (US-047) and in reports — **not** in the tree label, which renders `Placering`
  (US-010).
- SHOULD: **Cable type** — pick from a list or free text.
- SHOULD: **Cable numbering** (group) — drop‑down or free text.
- SHOULD: **Identification code** — free text; the unique product number.
- SHOULD: **Light group** — drop‑down; **MAY be absent** for products with no light‑group
  relationship.
- MUST: The dialog also carries the product's **terminal‑addressing section** — the `Indgange` / `Udgange`
  grids and their per‑terminal address editor, specified in **US-012**.

**Output:**
- The product node carries the entered documentation, which later feeds the installation and end‑user
  reports; fields left blank are simply omitted from reports (US-040).

### AC illustrations

- For a `Lampeudtag`, the dialog is titled `Lampeudtag`, shows *Name* = `Lampeudtag` **greyed and not
  editable**, *Placering* = `i loft` chosen from a fixed list, the note/cable/identification/light‑group
  fields, and an `Udgange` terminal grid (US-012). No room dropdown appears — the product's room is `Entré/Gang`
  because that is where it sits in the tree.
- Setting *Placering* = `i loft` documents where in the room the product sits, and the tree row becomes
  `Lampeudtag (i loft) ` (US-010).

### Constraints

- Verification method — **Inspection** of the dialog fields in the application.
- Data‑type note: the free‑text fields have no length limit or validation specified, so IHC OpenVisual
  should accept free text and treat list options as suggestions, not constraints — **except** `Placering`,
  which the vendor offers as a **fixed list**, and *Name*, whose editability is gated by type. (R‑note.)

**Readiness:** Not Ready.
- [R5] **Which product types lock the *Name* field** is **TBD (pending capture)** — one type sampled
  (F‑032). The gate itself is specified; only its predicate waits on the vendor capture.

**Implementation status:** 🟡 Partly implemented — the note/cable/identification/light‑group fields exist,
but four corrected rules do not:
- ⚠ the **auto‑open** on insert is still in the code (the old behaviour); backlog **A‑14** removes it;
- ⚠ the dialog is still titled generically *Product properties*; backlog **A‑8**;
- ⚠ **`Placering` is absent** and a **`Location` room dropdown** is present instead; backlog **A‑13**;
- ⚠ **Name is always editable**, ungated; backlog **A‑15**;
- ⛔ the **terminal section does not exist at all** (US-012); backlog **A‑12**.

---

## US-012 — Configure product input/output terminals & initial value

**As an** IHC installer, **I want** to map each product input to a data line and input‑module terminal
(and each output to an output‑module terminal, with an initial value), **so that** the model reflects
the physical wiring and the controller can drive real hardware.

**Scope excludes:** the product's documentation fields (US-011); wireless products, which talk directly to
the controller and carry no terminal addressing (E4).

> **Extended 2026‑07‑16 (was: three scenarios referring to an "Inputs \<click to configure\>" section that
> no story ever specified).** The old criteria named a UI that had never been described, so this story could
> not be built from its own text. The comparison opened IHC Visual's `Produkt egenskaber` and IHC
> OpenVisual's *Product properties* side by side on the same product (`Lampeudtag`, via F2 on both): the
> vendor carries a **full terminal‑addressing UI**, and **IHC OpenVisual's dialog has no terminal section at
> all** — so US-012 has no home in the app. The vendor's dialog is transcribed below as the spec. Evidence:
> `RESULTS.md` **F‑030** (`S03\10-product-props-vis.png` + `S03\11-terminal-config-vis.png` vs
> `S03\10-product-props-ov.png`); backlog **A‑12**.

### Acceptance criteria (Business Rules)

**Terminal grids — in the product properties dialog (US-011):**
- MUST: The dialog lists the product's terminals in **two grids**, one for inputs (`Indgange`) and one for
  outputs (`Udgange`), with one row per terminal the catalog defines for that product type. A product with
  no inputs shows no input grid; likewise for outputs.
- MUST: Each terminal row shows four columns: **name**, **address**, **wire colour** and **note**.
- MUST: A terminal that has not been addressed shows an empty address cell, so the installer can see at a
  glance which terminals still need wiring.
- SHOULD: Each grid carries a hint that its rows are configurable — IHC Visual heads each grid with the
  literal text `<klik for at konfigurere>` ("click to configure") — and a *Configure input* / *Configure
  output* button that opens the address editor for the selected row.

**Address editor — a sub‑dialog, one terminal at a time:**
- MUST: The editor offers an **address picker of two lists**: the **data line / module** (module 1–16, each
  shown with its module type, e.g. *Output 230/10*) and the **terminal** on that module (port 1–8).
  Choosing one from each addresses the terminal.
- MUST: The editor offers the terminal's **name**, **note** and **wire colour** (`Ledningsfarve`).
- MUST: An **output** terminal's editor offers an **initial value** (`Initial værdi`): `OFF` configures the
  output as normally‑open (NO), `ON` as normally‑closed (NC).
- MUST: An **output** terminal's editor offers a **power‑fail behaviour** — whether the output's current
  value is saved and restored after a power failure (`Ved strømsvigt` → `Gem aktuel værdi`). This is the
  same save‑current‑value flag US-033 backs up.
- SHOULD: The editor shows which terminals of the chosen module are **already in use**, so a port is not
  double‑booked.
- MAY: The same editor is reachable from the tree by selecting the pin and opening its properties.

**Output:**
- Each addressed terminal carries a module‑and‑port address that round‑trips to the `.vis` file and renders
  in the installation report (US-040); an unaddressed terminal stays unaddressed rather than silently
  defaulting to a port.

### AC illustrations

- `Lampeudtag`'s properties dialog shows an `Udgange` grid with one row — name `Udgang`, address
  `Datalinie 2.01`, wire colour `Brun`. Opening that row's address editor shows *Datalinie* = module 2
  (*Output 230/10*), *Udgang* = port 1, *Initial værdi* = `OFF`, and the power‑fail *save current value*
  option.
- Leaving an output's initial value at `OFF` configures it normally‑open; switching it to `ON` makes it
  normally‑closed.

### Constraints

- Verification method — **Test**: `safe_visual_tests` (opening a wired product's properties shows its
  terminal rows with addresses and wire colours) and `safe_project_tests` (an address set in the editor
  round‑trips to the `.vis` as `Datalinie N.PP`).
- **This is a UI‑surface gap, not an engine gap.** The `.vis` model already carries terminal addresses —
  they render in the installation report today — so this story adds a dialog surface over data the engine
  already holds.
- **TBD (pending capture):** whether IHC Visual opens a terminal's address editor on a **single** or a
  **double** click of the grid row. The affordances actually measured were the `<klik for at konfigurere>`
  hint and the *Configure* button; the row gesture itself was not pinned down (`RESULTS.md` F‑030). The
  *Configure* button and the pin‑properties routes are specified and unblocked; settle the row gesture
  against the vendor before fixing it. ⚠ Note the intersection with US-067: the old text specced a
  double‑click here, and IHC OpenVisual has no double‑click handler at all today (F‑006).

**Readiness:** Not Ready.
- [R3] The terminal‑row open **gesture** (single‑ vs double‑click) is **TBD (pending capture)**. It does not
  block the grids, the address editor, or the *Configure*‑button route.

**Implementation status:** ⛔ **Not implemented.** IHC OpenVisual's *Product properties* dialog carries only
Name / Location / Note / Cable type / Cable numbering / Identification code / Light group — **no terminal
grid, no per‑terminal address, no wire colour, no initial value, no power‑fail option and no configure
affordance** — so a product's terminals cannot be addressed at all. (The earlier "✅ Implemented (in‑tree
route)" claim did not survive the comparison.) This is the largest single gap the vendor comparison found;
backlog **A‑12** implements it.

---

## US-013 — Insert a special product (modem)

**As an** IHC installer, **I want** to insert a modem `<product>` from the special‑products menu and
set its properties, **so that** the installation can notify by phone/SMS — subject to the one‑modem
rule.

**Scope excludes:** the scene/notification setup that is configured outside this app (out of scope).

### Acceptance criteria (Business Rules)

**Insertion & constraint rules:**
- MUST: A modem is inserted via right‑click a locality > *Products* > *Bus Produkter* > `<product>`
  (US-010's corrected category structure). **No properties dialog opens on insert** (US-011).

  > **Corrected 2026‑07‑16 (was: "*Special products* > `<product>`; a properties dialog then opens").**
  > Two inherited errors: the modem is a **bus product**, which IHC OpenVisual re‑homes under *Special
  > products* because it has no `Bus Produkter` category (F‑028, backlog **A‑11**); and no dialog auto‑opens
  > on insert (F‑027, backlog **A‑14**).

- MUST: A project may contain **at most one** modem, regardless of `<product>`.
- SHOULD: A refused second‑modem insertion **tells the installer why** rather than appearing to do nothing.

  > **Confirmed 2026‑07‑16 — the rule matches the vendor; its *feedback* is open.** IHC Visual enforces the
  > singleton: a second SMS‑modem insert was **posted and added nothing** (item count unchanged). So this
  > story's MUST was already vendor‑true — recorded as a baseline. But the vendor's refusal is **silent** —
  > no dialog, no status text — which is a candidate quirk of exactly the kind the 2026‑07‑16 ruling's
  > exception #1 covers (a silent refusal is indistinguishable from a broken command), hence the SHOULD
  > above. ⚠ **TBD (pending capture):** IHC OpenVisual's own second‑modem refusal was **not driven**, so what
  > it does today is unmeasured. Evidence: `RESULTS.md` **F‑029** (an open **E** whose next step is exactly
  > this comparison).

**Property groups (dialog "Modem properties"):**
- SHOULD: **Modem properties** — Name (type name), Note (appended in parentheses after Name), Location,
  Identification code.
- SHOULD: **Cabling** — wire colours for 0 V, 24 V, RS485 minus, RS485 plus.
- SHOULD: **Telephone numbers** — Number 1–4, dialled in priority order (Number 2 is dialled only if
  Number 1 is unanswered, Number 3 if Number 2 is unanswered, Number 4 if Number 3 is unanswered).
- SHOULD: **Settings** — Access code (4‑digit access code, default `1234`); Call pause
  (integer, **1–99 minutes**); Call delay (integer, **1–99 seconds**); ID code (alarm‑centre
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
  alarms but never answers incoming calls; setting *Call pause* = `1` gives a one‑minute wait between
  the four numbers.
- Attempting to add a second modem is blocked by the one‑modem rule regardless of type.

### Constraints

- Verification method — **Inspection** of the two modem dialogs in the application, and **Test** of the
  one‑modem constraint.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (SMS modem). Epic E3 complete.
