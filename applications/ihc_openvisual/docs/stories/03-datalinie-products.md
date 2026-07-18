---
version: 0.3.1
last-updated: 2026-07-18
status: draft
---

# E3 — Wired (data-line) products

> **Implementation status:** 🟡 Partly implemented — **three divergences, not one**: the **insert menu is
> 12 products short** (US-010 — IHC Visual offers 21 categories / 100 leaves against IHC OpenVisual's
> 17 / 88, a **strict subset: 12 missing, 0 extras**; `RESULTS.md` **F‑055** → backlog **A‑11**);
> **terminal addressing does not exist** (US-012 → **A‑12**, the largest single gap the comparison found);
> and **six product‑dialog rules diverge** from the vendor (US-011). ⭐ **A‑11 is a menu‑building gap, not a
> catalog gap** — the SDK's embedded catalog already carries all 100 products, including all 12 missing
> ones, so no catalog work is implied. See each story.
>
> **Corrected 2026‑07‑17 (was: "insertion works; terminal addressing (US-012) does not exist, and six
> product‑dialog rules diverge from the vendor (US-011)").** ⚠ *"Insertion works"* is falsified: **US-010's
> status went ✅ → 🟡 the same day**, when the insert menu was re‑walked to the leaves and measured a strict
> subset of the vendor's (**F‑055**). The header named the two divergent stories and US-010 was not one of
> them — it is now.

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

**Readiness:** Ready.

> **Both open measurements are now closed (2026‑07‑17).** This epic was *Not Ready* on two captures; the
> second comparison session drove both, so every rule below is specified from a measurement rather than a
> guess:
> - **[R3] US-012 — the terminal‑row open gesture is DOUBLE‑click**, and the *Configure* button opens the
>   **same** sub‑dialog: two routes, one dialog. Evidence: `RESULTS.md` **F‑056**.
> - **[R5] US-011 — the *Name* lock predicate is the `locked` attribute on the project element**, resolved
>   against the project's own inline DTD. Confirmed 9 captures / 0 falsifications. Evidence: `RESULTS.md`
>   **F‑054**.
>
> The same session also closed the epic's leaf‑parity gap (**E‑7** → **F‑055**) and found two dialog
> divergences nobody had recorded — the vendor's MRU combo boxes and its *end‑user report* checkbox
> (**F‑056**). Both are written into US-011.

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
- **Resolved 2026‑07‑17 (R‑1 — Full English).** Every **structural / chrome** label renders in English —
  the three top‑level category names (`Datalinie produkter` → *Wired products*) **and** the subcategories
  (`Generelle` → *General*; `Indgang`/`Udgang`/`Dimmer` → *Input*/*Output*/*Dimmer*). Only **user‑entered**
  content (product names, notes, `Placering`, program names) stays Danish — as do the **function‑block
  library** category names, which US-018 keeps verbatim as vendor‑catalog data. This **reopens F‑028** (the
  earlier "no slip to fix" reading held under the *old* mixed policy; the ruling changes the policy) →
  backlog **A‑29** (complete the subcategory label map). **The structural category rules above are
  unaffected** — they define which categories exist; only the rendered labels change.

  > **Corrected 2026‑07‑17 (was: a MUST that category names be in one language, on the reading that
  > "`Generelle` is untranslated among English siblings — a localisation slip").** ⚠ **That reading was a
  > misdiagnosis, and walking the menu to the leaves disproved it.** The vendor's *own* subcategories under
  > `Datalinie produkter` are literally `Input`, `Output`, `Dimmer`, **`Generelle`** — so `Generelle`'s
  > siblings are not "otherwise‑translated" at all; IHC OpenVisual carries all four **verbatim**, exactly as
  > the function‑block catalog does (US-018). There is no slip to fix. What *is* real is the inconsistency
  > one level up, and it is a **decision, not a defect** — which is why the MUST is demoted to a ruling.
  > Evidence: `RESULTS.md` **F‑055**.

> **Corrected 2026‑07‑16 (was: "it offers the product categories the catalog defines", which asserted
> nothing checkable).** Walking both menus found two concrete structural gaps: (1) **no `Bus Produkter`
> category** — IHC OpenVisual re‑homes `SMS Modem` under *Special products*, and **`IHC LED Dimmer 2
> kanaler` appears nowhere at all**; (2) **`Special products` holds only `SMS Modem`**, missing the three
> sub‑categories and the four loose specials. `Wired products` ↔ `Datalinie produkter` and the whole wireless subtree already
> match. Evidence: `RESULTS.md` **F‑028** (vendor `catalog.products` = **100** products with category
> paths); backlog **A‑11**.
>
> **Leaf parity is now measured — the gap is exact, and it is smaller than feared (2026‑07‑17).** The menu
> was re‑walked to the leaves on both apps: IHC Visual offers **21 categories / 100 leaves**, IHC OpenVisual
> **17 / 88** — a **strict subset: 12 products missing, 0 extras**, and the 12 are exactly the four
> categories above (1 bus + 11 special). ✅ `Wired products` and `IHC Wireless products` are **structurally
> identical** to the vendor's, subcategory for subcategory — **do not touch them**. ⭐ **This is a
> menu‑building gap, not a catalog gap**: the SDK's embedded catalog already carries all 100 products,
> including all 12 missing ones, so no catalog work is implied. Evidence: `RESULTS.md` **F‑055** (supersedes
> F‑028's category‑only reading and closes **E‑7**); backlog **A‑11**.
>
> **Refined 2026‑07‑18 (comparereal, F‑088).** The VM now builds `BusProductsMenu`, and the tree
> **context menu** exposes the `Bus Produkter` category — `IHC LED Dimmer 2 kanaler` and `SMS Modem` are
> reachable via right‑click, so the "appears nowhere at all" reading above is now closed for the
> context‑menu route. ✅ **Closed 2026‑07‑18 (A‑35):** the **menu‑bar** *Insert > Products* submenu now
> also binds `BusProductsMenu` after Wireless (`MainWindow.axaml`), so both routes expose the Bus category —
> the route‑equivalence violation (US-044, `11-interaction-model.md`) is resolved. Evidence: **F‑088**
> (comparereal study, `tmp\comparereal\out\RESULTS.md`); extends **F‑055** / backlog **A‑11**, **A‑35**.

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

  > ⚠ **Two rules fit this evidence equally well, and the app implements one of them. Read before
  > extending it.** The hidden pins are identified here **by tag**, but they *also* **duplicate a sibling's
  > `address_channel`** — and no measured case separates the two rules. The tag rule shipped because it is
  > what was measured; the duplicate-channel rule was noted and not adopted. **The discriminator is a
  > product that hides a pin *without* a duplicate channel (or the reverse)** — until one is found, this is
  > a coin-flip that happens to be green. ⚠ Relatedly, the rule was **deliberately not generalised** to
  > `airlink_shutter_lock` (the *Lås* pin on `Jalousi 2 tast`): the vendor **does show** the *Lås* pin, so
  > the measured tag list is **confirmed correct** — inserting `Jalousi 2 tast (lokal lås)` renders
  > *Tryk (venstre) · Tryk (højre) · Lås · Tilstand · Scenarier/regulering*, hiding only *Op*/*Ned*, so
  > `airlink_shutter_lock` must **not** be added to the hide list (**C19a closed**). **C19b** (the
  > duplicate‑`address_channel` rival) stays formally unfalsified but practically irrelevant: here *Op*/*Ned*
  > reuse *Tryk (venstre)*'s `_0x01` while *Lås* carries a unique `_0x03`, so tag rule and channel rule
  > agree and the discriminating case does not exist in the catalog. Measured 2026‑07‑17; evidence:
  > `RESULTS.md` **F‑085**; backlog **A‑1** (confirmed correct).
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
- MUST: `resource_enum` is **not the only** row kind that does this. The vendor renders the literal on a
  function block's **`Indstillinger`** (settings) rows too — `Timertid = 00:10:00`,
  `Sluk Tidspunkt = 00:00:00` — and neither of those is an enum. ⚠ **IHC OpenVisual renders those rows
  bare, so it is short a value there**; backlog **A‑21**.
- MUST: A row the tree **hides** carries no label either way — the calibration rows' `0.00` never reaches
  one, because the row itself is suppressed (the `setting="yes"` rule above). On a **product** the
  `resource_flag` `on`/`off` literal does stay bare; ⚠ the same tag's **function‑block** behaviour is
  **unmeasured** — do not extend the rule to it on inference.

> **Corrected 2026‑07‑17 (was: "Only `resource_enum` does this … those rows stay bare").** ⚠ **The scope
> was too narrow — the vendor's rule reaches rows that are not enums at all.** The deep TV2 diff measured
> IHC Visual rendering `Timertid = 00:10:00` and `Sluk Tidspunkt = 00:00:00` on a **non‑enum**
> function‑block `Indstillinger` row in configuration mode, with **IHC OpenVisual rendering the same rows
> bare** — measured on build `b2f1933`, i.e. **after A‑3 shipped**, so this is a live divergence and not a
> pre‑A‑3 artefact. `Timertid` **cannot** be a `resource_enum`: A‑3 renders every one of those as
> `name = value`, and this row came out bare. ⇒ IHC OpenVisual is **short a value** on FB variable/settings
> rows — an **unrecorded B**, now tracked as backlog **A‑21**. Evidence: `RESULTS.md` **F‑062**
> (`RESULTS.md:220`).
>
> **Why nobody noticed — and this is the instructive part.** F‑062 is closed in the ledger as *"✅ RESOLVED
> 2026‑07‑17 by F‑068"*, but **F‑068 resolved only the row‑COUNT delta** (+525 = 30 empty containers + 495
> `Internal variables`) and reports *"0 pin‑count mismatches"*. **A label difference does not move a node
> count**, so F‑062's label observation was carried into a closure that never covered it — F‑062's own
> adjudication walks three candidates (a)/(b)/(c), and the bare state rows are none of them.
>
> ✅ **The product side is unaffected — do not "fix" it.** Across TV1's 640‑node label sweep IHC OpenVisual
> matched the vendor **639/639**, its one exception being F‑051's link‑path leak — **not** a state row. That
> is the positive evidence that on a product `resource_enum` really is the only row kind that takes a value,
> and that the literal‑carrying rows really do stay bare (`RESULTS.md` **F‑051**). The falsified word is
> *"Only"*. ⚠ **Residual — the exact cell the note below names is still open**: an **enum** variable in
> **programming** mode remains uncensused (`RESULTS.md` **E‑4**), scheduled as **C1.2** in
> `tmp\compare3.md` §3.3. F‑062 measured **configuration** mode.

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
- A scene‑capable product (e.g. `Lampeudtag`, `Stikkontakt`, `Dimmer Universal`) also auto‑creates a
  **`Scenarier`** scene container (rendered `Scenarier/regulering` on dimmers) on insert — a node ready
  to hold scenes, with no scene members until authored.

### Constraints

- Verification method — **Demonstration** that both the context‑menu and *Insert*‑menu routes insert
  the product under the selected locality with the confirming status‑bar string.

**Readiness:** Ready.

**Implementation status:** 🟡 **Partly implemented** — insertion, the `(position)` label and the
row‑suppression rules work, but **two of this story's own MUSTs are measured unmet**: there is **no
`Bus Produkter` category** and `Special products` holds 1 leaf against the vendor's 7, so IHC OpenVisual's
menu is a **strict subset — 21 categories / 100 leaves on the vendor vs 17 / 88; 12 products missing, 0
extras** (`RESULTS.md` **F‑055**); backlog **A‑11**. ⭐ **A menu‑building gap, not a catalog gap** — the SDK's
embedded catalog already carries all 100 products, including all 12 missing ones; the root cause is
`MainWindowViewModel.BuildProductMenu()`'s modem‑only filter. (The FB‑side `= <value>` gap above,
**A‑21**, is a third.)

> **Update 2026‑07‑18 (F‑088):** the VM builds all four category menus and **both** the tree context menu
> and the **menu‑bar** *Insert > Products* submenu now bind `BusProductsMenu` (LED dimmer + SMS modem
> reachable via either route) — ✅ **A‑35** closed the menu‑bar omission. See the F‑088 note under
> *Business rules* above. Evidence: **F‑088** (comparereal).

---

## US-011 — Fill product documentation properties

**As an** IHC installer, **I want** to fill in a product’s documentation fields when I insert it (or
later via properties), **so that** the generated reports (E9) describe the installation accurately.

### Acceptance criteria (Business Rules)

**Presentation rules:**
- MUST: When a product is inserted, it is added under the selected locality and **no dialog opens
  automatically**. The installer opens the *Product properties* dialog on demand
  by selecting the product and pressing `F2` (or right‑click > *Properties*).

  > **Corrected 2026‑07‑16 (was: dialog opens automatically on insert).** The earlier MUST required the
  > dialog to auto‑open, on the belief that IHC Visual does so. The side‑by‑side comparison measured the
  > opposite — the vendor does **not** auto‑open on insert (product lands under the caret, no modal) — so
  > OpenVisual matches the vendor. Evidence: `RESULTS.md` **F‑027**; backlog **A‑14** tracks removing the
  > auto‑open from the implementation.
  >
  > **Corrected 2026‑07‑18 (comparereal, F‑088 run).** The vendor **does** auto‑open — every product insert
  > raises a "Classic" properties dialog (its OK button id is 3, 436 or 1 by product family) that must be
  > dismissed. IHC OpenVisual's silent insert is therefore an **intentional simplification** (an accepted,
  > cleaner class‑C divergence), not a vendor‑match; the earlier F‑027 reading is superseded on the vendor
  > point. The MUST above (no auto‑open) stands on its own merit. Evidence: **F‑088** run (comparereal),
  > class‑C "Product‑insert UX".

- MUST: The dialog is **titled with the product type** — e.g. `Lampeudtag` — not a generic *Product
  properties*. This is how two open product dialogs are told apart.

  > **Corrected 2026‑07‑16 (was: a generic "Product properties" title).** IHC Visual titles the dialog with
  > the product **type**; IHC OpenVisual titles every product dialog identically. Small, but it is the only
  > thing distinguishing two open dialogs. Evidence: `RESULTS.md` **F‑015**; backlog **A‑8**.

**Input fields (each a labelled control; free‑text unless a list is noted):**
- MUST: **Name** — shown pre‑filled. **Editability is gated by the placed product's own `locked`
  attribute**, not by its type: the field is **disabled** (shown, greyed, not editable) exactly when the
  `locked` attribute **on that product's element in the project** resolves to `yes`, and editable
  otherwise. When it is disabled, the name shown equals the catalog's type name.
- MUST: `locked` is resolved against **the project's own inline DTD**, which defaults it to **`no`** — so a
  product element that simply **omits** `locked` is **editable**. The attribute is only *seeded* onto an
  element from the catalog when the product is first inserted; from then on the element is the truth.

  > **Corrected 2026‑07‑16 (was: Name always editable), predicate closed 2026‑07‑17 (was: "gated by product
  > type — TBD which types").** IHC Visual **disables** Navn for `Lampeudtag` while IHC OpenVisual offers an
  > editable textbox for every product. Per the 2026‑07‑16 ruling the vendor is authoritative, so the gate is
  > adopted — and the second comparison session named the predicate outright, so **no sampling is needed and
  > nothing here is a guess**: `Navn` is disabled ⟺ the element's `locked` resolves to `yes`, confirmed over
  > **9 live captures with 0 falsifications**. Evidence: `RESULTS.md` **F‑032** → **F‑054**; backlog **A‑15**.
  >
  > ⚠⚠ **"Gated by product type" — the rule this story used to state — is the one implementation that gets
  > it wrong.** The project's inline DTD defaults `locked` to **`no`**; the *catalog's* grammar defaults it
  > to **`yes`**. They disagree, so a catalog lookup by type greys every product whose element omits the
  > attribute — in the measured project that is **all the wireless sensors**. Measured both ways:
  > `Lampeudtag`'s element carries `locked="yes"` explicitly → **greyed**; `Bevægelsessensor 1873 Bobby-AM`'s
  > element omits it → inherits the project default `no` → **editable**. The two datasets agree only for
  > *freshly inserted* products, which is why the type‑based reading survived the first session.
  >
  > ⚠ **Do not "fix" the vendor's typo.** Four catalog products (`Mini Modul 1/2/3 tryk`, `Diode`) carry a
  > misspelled `loced="no"`. It is undeclared in every DTD and therefore **inert**, so they are
  > *accidentally* `locked="yes"` and IHC Visual greys them **against their author's plain intent** — verified
  > live. Resolving `locked` through the DTD reproduces the vendor exactly; correcting the typo would diverge.
  >
  > **Rivals falsified — do not re‑litigate.** `enduser_report`: the decisive same‑grammar pair shares its
  > value yet differs in editability. `category`: `S0 Device` (greyed) and `Controller Link OUT` (editable)
  > sit in the *same* category.

- MUST: **Placering** — the product's **placement descriptor**: where in the room it physically sits, e.g.
  `i loft` ("in the ceiling"). This is **not** the parent room — the room is implied by the product's
  position in the tree and is not shown in the dialog. It is **free text with suggestions**, not a fixed
  list (see the combo‑box rule below).

  > **Corrected 2026‑07‑17 (was: "One of a fixed list").** ⚠ **The fixed‑list claim was wrong, and building
  > to it would have been a divergence in its own right.** `Placering` is an **editable combo box** whose
  > drop‑down is a *most‑recently‑used* list, not an enumeration: IHC Visual backs it with a machine‑local
  > `Data\*.txt` file that accumulates whatever installers have typed on that PC. **No fixed list exists to
  > reproduce.** Evidence: `RESULTS.md` **F‑054**/**F‑056**.

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
- SHOULD: **Include this product in the end‑user report** — a **checkbox** that is the UI for the product's
  `enduser_report` attribute and feeds US-040's end‑user report. The vendor **never shows this checkbox**
  (measured C15, below), so IHC OpenVisual **hides it** to match — the attribute still round‑trips through
  the (hidden) control, but there is no toggle affordance, exactly like the vendor.

  > **Added 2026‑07‑17 (was: absent from this story, and from the app).** The vendor's dialog carries a
  > checkbox *"Inkluder produktet i slutbruger rapport"* which **no story had ever recorded** and IHC
  > OpenVisual has **no equivalent of** — so the `enduser_report` attribute round‑trips through the file
  > with no way to set it. **Resolved 2026‑07‑18 (C15 measured, elevated vendor session).** Control 303 is
  > `visible=False` in the full product dialog and **absent entirely** from the compact (handheld) dialog —
  > measured across **13 products / 6 families** (datalinie output/input, dimmer, window‑shutter, wireless,
  > handheld), **0 showing it**. The vendor never exposes it (not a per‑product gate; the twice‑burned
  > wrong predicate never existed). **User ruling: hide the checkbox to match the vendor** (`IsVisible=False`),
  > keeping the `enduser_report` round‑trip. Evidence: `tmp\comptest\out\M\M1-enduser.json`, superseding the
  > earlier 7/7 reading (`RESULTS.md` **F‑078**).

- MUST: The dialog also carries the product's **terminal‑addressing section** — the `Indgange` / `Udgange`
  grids and their per‑terminal address editor, specified in **US-012**.

**How the text fields behave — the drop‑down question:**

- **Resolved 2026‑07‑17 (R‑2 — plain textboxes, granted exception C).** In IHC Visual **all seven** fields
  above (*Placering*, *Note*, *Kabeltype*, *Kabelnummer*, *Identifikationskode*, *Lysgruppe*, and *Navn*
  when unlocked) are **editable combo boxes with a drop‑down** backed by machine‑local MRU files
  (`Data\*.txt`) on the vendor install — **not data in the `.vis`**, so there is nothing portable to read
  them from. The ruling: **IHC OpenVisual keeps plain textboxes** for all seven fields, as a granted **C**.
  The MRU affordance is dropped because its data source does not exist outside a vendor install, and a
  project‑derived MRU was rejected as an invention (one PC's accumulated list is not a spec).

  > **Added 2026‑07‑17.** A whole class of affordance — **7 fields × every product** — that neither this
  > story nor the backlog had noticed: the earlier comparison read the field *set* and never the field
  > *kind*. It was recorded as a ruling because option 1 (a project‑derived MRU) is an **invention**
  > and option 2 (plain textboxes) is a **loss** — both diverge, so it needed a decision. **Decided
  > 2026‑07‑17: option 2 — plain textboxes, granted C.** Evidence: `RESULTS.md` **F‑056** (the lists are IHC Visual's machine‑local `Data\*.txt` MRU
  > files — cumulative, per‑PC, and absent from the project file).

**Output:**
- The product node carries the entered documentation, which later feeds the installation and end‑user
  reports; fields left blank are simply omitted from reports (US-040).

### AC illustrations

- For a `Lampeudtag` (whose element carries `locked="yes"`), the dialog is titled `Lampeudtag`, shows *Name*
  = `Lampeudtag` **greyed and not editable**, *Placering* = `i loft`, the note/cable/identification/
  light‑group fields, and an `Udgange` terminal grid (US-012). No room dropdown appears — the product's room
  is `Entré/Gang` because that is where it sits in the tree.
- For a `Bevægelsessensor 1873 Bobby-AM` in the same project — whose element **omits** `locked` — the very
  same dialog shows *Name* **editable**. The two products differ in nothing but that attribute, which is why
  the gate reads the element and not the type.
- Setting *Placering* = `i loft` documents where in the room the product sits, and the tree row becomes
  `Lampeudtag (i loft) ` (US-010).

### Constraints

- Verification method — **Inspection** of the dialog fields in the application, and **Test**
  (`safe_visual_tests`) of the *Name* gate — three cases, each pinning a different failure: an element
  carrying `locked="yes"` → Name **disabled**; an element that **omits** `locked` → Name **enabled** (*this
  is the case a catalog‑by‑type implementation fails*); and a freshly **inserted** `Mini Modul 1 tryk` →
  Name **disabled** (the `loced` typo is inert, so the catalog seeds `locked="yes"`), which pins both the
  insert‑seed path and the typo's inertness.
- Data‑type note: none of the free‑text fields has a length limit or validation specified, so IHC OpenVisual
  should accept free text and treat any offered list as **suggestions, not constraints** — `Placering`
  included (its vendor list is an MRU, not an enumeration). *Name*'s editability is gated by `locked`.
  (R‑note.)

**Readiness:** Ready.

> **[R5] closed 2026‑07‑17.** The *Name* gate's predicate is measured, not sampled — see the `locked` rule
> above (`RESULTS.md` **F‑054**). Both earlier **product rulings** are now resolved — the combo‑box
> affordance is **R‑2** (plain textboxes, granted C) and US-010's category language is **R‑1** (Full
> English) — so nothing here blocks building the story.

**Implementation status:** ✅ Implemented — the note/cable/identification/light‑group fields exist, and the
previously‑pending rules have all landed:
- ✅ insert is silent — no dialog auto‑opens (source `MainWindowViewModel.InsertProductAsync`; confirmed by
  comparereal, 0 modals across 13 inserts); **A‑14** done;
- ✅ the dialog is titled with the product type (source `OpenProductPropertiesAsync`, catalog `DisplayName`); **A‑8** done;
- ✅ **`Placering`** is present as a plain `Placement` textbox, with **no** `Location` room dropdown (source `ProductPropertiesWindow.axaml`); **A‑13** done;
- ✅ **Name is gated by the element's `locked`** (`NameBox.IsEnabled = !NameLocked`, fed from the `locked` attribute); **A‑15** done;
- ✅ **plain textboxes** are the specified affordance (**R‑2**, granted exception C) — no longer a divergence to fix;
- ✅ the **end‑user‑report flag round‑trips** (`enduser_report`); the checkbox is hidden per **C15** to match the vendor; **A‑23** done;
- ✅ the **input/output terminal grids exist** (US-012, `ProductPropertiesWindow.axaml`); **A‑12** done.

---

## US-012 — Configure product input/output terminals & initial value

**As an** IHC installer, **I want** to map each product input to a data line and input‑module terminal
(and each output to an output‑module terminal, with an initial value), **so that** the model reflects
the physical wiring and the controller can drive real hardware.

**Scope excludes:** the product's documentation fields (US-011). ⚠ **Wireless products are *not* excluded on
the grounds of "no terminal addressing"** — F‑057 measured the wireless dialog to be the *same* dialog with
the *same* `Indgange`/`Udgange` grids, enabled by the product's shape. US-014 owns the wireless side; the
grid and address spec below applies to both families.

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
- MUST: The dialog lists the product's terminals in **two grids that are both always present** — one for
  inputs (`Indgange`) and one for outputs (`Udgange`) — with one row per terminal the catalog defines for
  that product type. A product with no inputs shows an **empty** `Indgange` grid whose *Configure input*
  button is **disabled** (never a *missing* grid); likewise for outputs. This holds for **wireless** products
  too: F‑057 measured the wireless dialog to be the same dialog with the same two grids, enabled by the
  product's shape (an input‑only wireless sensor has an enabled `Indgange` grid and a disabled `Udgange`
  grid — US-014).
- MUST: Each terminal row shows four columns: **name**, **address**, **wire colour** and **note**.
- MUST: A terminal that has not been addressed shows an empty address cell, so the installer can see at a
  glance which terminals still need wiring.
- MUST: A terminal's address editor opens by **double‑clicking its grid row** *and* from a *Configure input*
  / *Configure output* button — **two routes onto the same sub‑dialog** (US-044 route equivalence). A
  **single** click only selects the row.
- MUST: Each *Configure* button is **disabled when its grid is empty** — a product with no inputs offers no
  way to configure one.
- SHOULD: Each grid carries a hint that its rows are configurable — IHC Visual heads each grid with the
  literal text `<klik for at konfigurere>` ("click to configure").

  > **[R3] closed 2026‑07‑17 (was: "TBD — single‑ or double‑click?").** Both routes were driven on the
  > vendor and they converge on the same `Udgang` dialog: the grid is a list view that opens its row on
  > **double**‑click, and `Konfigurer udgang` opens the identical dialog. The `<klik for at konfigurere>`
  > header hint — the affordance the first session reasoned from — is **corroboration, not a measurement**,
  > and taken alone it points at single‑click, which is wrong. Evidence: `RESULTS.md` **F‑056**.
  >
  > ✅ **Updated 2026‑07‑17 (was: "the double‑click route only works once US-067 exists, so US-012's primary
  > gesture depends on E11's double‑click handler landing first").** **The handler landed, and it is measured
  > at parity — this route is not blocked.** Backlog **A‑4** shipped 2026‑07‑16
  > (`MainWindowViewModel.ActivateNodeCommand` + `OnNodeDoubleTapped` on the item template's root
  > `StackPanel`; 6 VM matrix cases + 1 headless effect‑verified case; visual **216** green —
  > `alignment-backlog.md:139`), US-067 now reads ✅ Implemented, and **F‑052** — an F‑048+ row, i.e. one
  > measuring **today's** build — records **A‑4 itself is PARITY**: a live product double‑click opens
  > `Product properties` on a fresh app. Evidence: `RESULTS.md` **F‑052** (`RESULTS.md:199`).
  >
  > ✅ **Residual resolved 2026‑07‑17 (F‑084).** C16 was measured with an explicit x‑offset: **both** apps
  > activate on the **label only**, and a double‑click in the blank strip right of the label is a **no‑op on
  > both, with no accidental toggle** — parity. The earlier "OV falls through and toggles" concern is
  > superseded, so **F‑052 is closed (E→A)** and this route carries no residual defect. Evidence:
  > `RESULTS.md` **F‑084**.
  >
  > ⚠ Note what this story's dependency actually is: the terminal **grid row is in the properties dialog**,
  > not the tree, so E11's node handler was never the thing gating it. The blocker is that **the terminal
  > section does not exist at all** — backlog **A‑12**.

**Address editor — a sub‑dialog, one terminal at a time:**
- MUST: The editor offers an **address picker of two lists**: the **data line / module** (module 1–16, each
  shown with its module type, e.g. *Output 230/10*) and the **terminal** on that module (port 1–8).
  Choosing one from each addresses the terminal.
- MUST: The module list offers an explicit **not‑configured** entry (`ikke konfigureret`), so an addressed
  terminal can be returned to unaddressed rather than only ever moved to another port.
- MUST: The editor offers the terminal's **name**, **note** and **wire colour** (`Ledningsfarve`). The
  terminal's **name is read‑only** — it comes from the product's catalog type.
- MUST: An **output** terminal's editor offers an **initial value** (`Initial værdi`): `OFF` configures the
  output as normally‑open (NO), `ON` as normally‑closed (NC).
- MUST: An **output** terminal's editor offers a **power‑fail behaviour** — whether the output's current
  value is saved and restored after a power failure (`Ved strømsvigt` → `Gem aktuel værdi`). This is the
  same save‑current‑value flag US-033 backs up.
- MUST: The terminal list marks which ports of the chosen module are **already in use** (IHC Visual renders
  them `1 (i brug)` … `8 (i brug)`), so a port is not double‑booked.

  > **Raised from SHOULD to MUST 2026‑07‑17.** The in‑use indication was a SHOULD written from the report
  > data; the vendor's list was then read control‑by‑control and it marks **every** port's occupancy inline.
  > It is how the installer avoids double‑booking a port, and the information is already in the project.
  > Evidence: `RESULTS.md` **F‑056**.

- SHOULD: The editor offers **Apply** alongside OK/Cancel, and OK stays **disabled until something changes**
  — so an editor opened to read an address cannot accidentally rewrite it.
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

**Readiness:** Ready.

> **[R3] closed 2026‑07‑17** — the row gesture is a **double**‑click and the *Configure* button is its
> equivalent route (`RESULTS.md` **F‑056**). The whole story is now specified from the vendor's dialog
> captured **control by control**, so there is nothing left to guess at build time.

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
  (US-010's corrected category structure; the category label renders in English — *Bus Products* — under
  the R‑1 Full‑English ruling). **No properties dialog opens on insert** (US-011).

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

**Implementation status:** 🟡 **Partly implemented (SMS modem)** — the dialog fields and the one‑modem rule
exist, but **this story's own insert route does not**: *Products* > **`Bus Produkter`** > `<product>` is
**unbuildable until A‑11 lands**, because IHC OpenVisual has no `Bus Produkter` category and re‑homes the
modem under *Special products* (the corrected MUST above; `RESULTS.md` **F‑055**). The modem can be inserted
today — just not by the route this story specs.

> **Corrected 2026‑07‑17 (was: "✅ Implemented (SMS modem). Epic E3 complete.").** ⚠ **"Epic E3 complete" was
> false on this document's own evidence** — the epic header reads 🟡, US-011 reads 🟡 and US-012 reads ⛔ *"the
> largest single gap the vendor comparison found"*. It appears to be a per‑story sign‑off copied from
> `02-localities.md:220`, where it **was** true. **E3 remains 🟡**, pending **A‑11** (the menu, which this
> story's route waits on), **A‑12** (terminal addressing), **A‑13** (`Placering`), **A‑14** (auto‑open) and
> **A‑15** (the `Name` gate).
