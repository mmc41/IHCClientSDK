---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E3 — Wired (data-line) products

> **Implementation status:** ✅ Implemented.

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
  that locality with its input/output/scenario pins.
- MUST: Each product exposes a documentation‑properties dialog and per‑terminal addressing to a data
  line and I/O‑module terminal.
- SHOULD: The status bar confirms each insertion by product name and target locality.
- MUST: At most one modem can exist in a project.

**Readiness:** Ready.

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
- MUST: When a product is inserted, a *Product properties* dialog appears
  automatically; the installer can reopen it any time by selecting the product and pressing `F2`
  (or right‑click > *Properties*).

**Input fields (each a labelled control; free‑text unless a list is noted):**
- MUST: **Name** — the product type name; shown pre‑filled.
- MUST: **Location** — drop‑down of localities *or* free text (e.g. "Living room").
- SHOULD: **Note** — free text; in some products a list of standard notes is offered.
- SHOULD: **Cable type** — pick from a list or free text.
- SHOULD: **Cable numbering** (group) — drop‑down or free text.
- SHOULD: **Identification code** — free text; the unique product number.
- SHOULD: **Light group** — drop‑down; **MAY be absent** for products with no light‑group
  relationship.

**Output:**
- The product node carries the entered documentation, which later feeds the installation and end‑user
  reports; fields left blank are simply omitted from reports (US-040).

### AC illustrations

- For a `<product>`, the dialog shows *Name* = the product type name, a *Location* drop‑down
  listing localities, and the cable/identification/light‑group fields; setting *Location* = "Living room"
  documents the product as located in the living room.

### Constraints

- Verification method — **Inspection** of the dialog fields in the application.
- Data‑type note: all fields are text (with drop‑down assistance where noted); the requirements do not
  specify length limits or validation, so IHC OpenVisual should accept free text and treat list options
  as suggestions, not constraints. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-012 — Configure product input/output terminals & initial value

**As an** IHC installer, **I want** to map each product input to a data line and input‑module terminal
(and each output to an output‑module terminal, with an initial value), **so that** the model reflects
the physical wiring and the controller can drive real hardware.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Address an input terminal during insertion
  Given the product properties dialog is open with an "Inputs <click to configure>" section
  When I double-click a specific input (e.g. `<pin>`)
  Then a configuration control lets me assign the input to a data line (e.g. data line 1)
    and to an input-module terminal (e.g. input 1)
  And the dialog indicates which terminals are already in use (e.g. terminals 1–4)

Scenario: Address an input later from the tree
  Given a product with a "Configure input" section exists in the tree
  When I right-click a specific input pin (e.g. `<pin>`) and choose "Properties"
  Then I can select a different input terminal and/or data line
  And the same edit is reachable via the "Inputs" window: select an input pin (e.g. `<pin>`)
    then right-click > "Properties"

Scenario: Configure an output and its initial value
  Given a product with an "Outputs <click to configure>" section
  When I configure an output like an input and open its "Properties"
  Then an "Initial value" field is available:
    OFF means the output is normally-open (NO); ON means normally-closed (NC)
```

### AC illustrations

- Assigning a `<pin>` input to data line 1, terminal 1 marks terminal 1 as in use; the next input can
  then be seen not to reuse it.
- Leaving an output’s *Initial value* at the default `OFF` configures it as normally‑open; switching
  to `ON` makes it normally‑closed.

### Constraints

- Verification method — **Demonstration** of both the in‑dialog (double‑click) and in‑tree (right‑click
  > Properties) addressing routes, and the NO/NC meaning of *Initial value*.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (in‑tree route).

---

## US-013 — Insert a special product (modem)

**As an** IHC installer, **I want** to insert a modem `<product>` from the special‑products menu and
set its properties, **so that** the installation can notify by phone/SMS — subject to the one‑modem
rule.

**Scope excludes:** the scene/notification setup that is configured outside this app (out of scope).

### Acceptance criteria (Business Rules)

**Insertion & constraint rules:**
- MUST: A modem is inserted via right‑click a locality > *Products* > *Special products* >
  `<product>`; a properties dialog then opens.
- MUST: A project may contain **at most one** modem, regardless of `<product>`.

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
