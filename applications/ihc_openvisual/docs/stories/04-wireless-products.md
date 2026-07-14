---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E4 — Wireless products & controller linking

> **Implementation status:** ✅ In-scope stories implemented (US-016/US-017 blocked, need controller).

> **Current scope:** ◑ **Partly in scope.** Inserting wireless products and editing their properties
> (US-014, US-015) are project CRUD → **✅ in scope**. Linking / commissioning wireless products to the
> controller (US-016, US-017) is wireless *wiring* → **⛔ Blocked** pending the wireless API.

**Goal:** Let an installer place IHC Wireless products into localities and let a commissioning
technician link them to the controller (and unlink / signal‑test them), so wireless senders and
receivers communicate through the controller.

**Scope:** inserting wireless products (from the product categories the catalog defines); the advanced
dimmer properties; the *Link Wireless products* dialog (link all / link one, sound feedback, product
list); unlink; the signal/battery test and the wireless test kit. **Scope excludes:** wired products (E3),
function‑block links (E6), and controller project transfer (E10).

**Acceptance criteria (epic level):**
- MUST: A wireless product can be inserted into a locality and is shown with a yellow **!** until it is
  linked to the controller.
- MUST: The technician can link all products in sequence or one at a time from *Controller > Link
  Wireless products*, using the product’s programming button, with per‑attempt success/error sound.
- SHOULD: The technician can unlink products and read live signal strength and battery level via *Test*.

**Readiness:** Not Ready — US-016 and US-017 describe live‑hardware behaviour not yet confirmed against
a running installation; the observable *dialog* structure is documented but the exact per‑step dialog
states are partly unverified (see the stories).

---

## US-014 — Insert a wireless product

**Status:** ✅ **In scope** — project CRUD (adds a wireless product node to the project; needs no
wireless API).

**As an** IHC installer, **I want** to insert an IHC Wireless product into a locality, **so that**
it appears in the tree, flagged as not‑yet‑linked until I commission it.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a wireless product via the context menu
  Given the "Installation" pane shows a locality (e.g. "Kitchen")
  When I right-click the locality and follow "Products" > "IHC Wireless products" > <group> > <product>
  Then the product is inserted under the locality with its input/output pins
  And the status bar reads: Product '<product>' inserted under <locality>

Scenario: A newly inserted wireless product shows an unlinked marker
  Given I have just inserted a wireless product and closed its properties with "OK"
  Then the product is marked with a yellow "!" indicating it is not yet linked to the controller

Scenario: Wireless categories come from the catalog
  Given the "IHC Wireless products" submenu is open
  Then it offers the product categories the catalog defines, each listing its products (<product>)
```

### AC illustrations

- Inserting a wireless product (`<product>`) under `Bedroom` yields a product node named by the catalog
  and exposing its catalog-defined pins (`<pin>`); the status bar reads
  `Product '<product>' inserted under Bedroom`.
- The wireless properties dialog is the same for all wireless products and has **Name, Location, Note,
  Identification code, Light group** (light group MAY be absent for some products) — note there is **no
  cable/terminal addressing**, since wireless products talk directly to the controller.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-015 — Set advanced wireless‑dimmer properties

**Status:** ✅ **In scope** — project CRUD (edits the wireless dimmer's stored properties).

**As an** IHC installer, **I want** to tune a wireless dimmer’s soft‑start, ramp and level limits,
**so that** the light behaves as the room requires.

**Scope excludes:** wired dimmers (this dialog applies to wireless dimmers only).

### Acceptance criteria (Business Rules)

**Access rule:**
- MUST: For a wireless dimmer, right‑click > *Properties* > *Advanced* opens the advanced properties
  box.

**Input fields (each numeric, with the stated unit, range and default):**
- MUST: **Soft on‑time** (soft on‑time) — integer milliseconds, **200–60000 ms**, default **700 ms**;
  time to turn on when no scenario call is used.
- MUST: **Soft off‑time** (soft off‑time) — integer milliseconds, **200–60000 ms**, default **700 ms**.
- SHOULD: **Manual ramp time** (manual ramp time) — integer seconds, **2–10 s**; time to ramp from min to
  max (or back).
- SHOULD: **Minimum value** (minimum level) — integer percent, **0–100 %** (e.g. 30 %).
- SHOULD: **Maximum value** (maximum level) — integer percent, **0–100 %**, default **100 %**.
- SHOULD: **Load characteristic** (load characteristic) — enumeration: **Inductive | Capacitive |
  Auto** (Auto detects the connected load).

**Output:**
- Dimmer behaviour parameters stored with the product and used at runtime.

### AC illustrations

- Setting *Soft on‑time* = `700`, *Minimum value* = `30`, *Load characteristic* = `Auto` gives a
  dimmer that fades on in 0.7 s, never dims below 30 %, and auto‑detects the load.
- Entering `100` ms for *Soft on‑time* is below the 200 ms minimum and must be rejected or clamped.

### Constraints

- Verification method — **Test** each field against its documented range/default; **Analysis** of the
  Auto load‑characteristic behaviour.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-016 — Link wireless products to the controller

**Status:** ⛔ **Blocked** — wireless *wiring*; requires the wireless API (not available yet). Kept and
specified for when the API lands.

**As a** commissioning technician, **I want** to link the wireless products to the controller — all in
sequence or one at a time — with audible feedback, **so that** each sender/receiver communicates
through the controller and loses its unlinked marker.

**Scope excludes:** inserting the products (US-014) and unlink/test (US-017).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Open the linking dialog and read the product list
  Given wireless products have been inserted (each showing a yellow "!")
  And the PC has a connection to the controller (shown in the bottom-right corner of IHC OpenVisual)
  When I choose "Controller" > "Link Wireless products"
  Then a dialog lists the inserted wireless products on the left, unlinked ones marked with a yellow "!"
  And a component IHC OpenVisual does not recognise is shown with a distinct marker (not the yellow "!")
  And selecting a product shows its Name, Note, Identification code, Product type and Serial number
    (serial number appears only after linking) plus a signal-strength indicator
  When I click "Product list" I get a printable list giving the order in which to link products

Scenario: Link all products in sequence
  Given the linking dialog is open
  When I click "Link all"
  Then the dialog prompts for the first product to link
  And for each product I press its physical programming button A (its LED B lights red),
    then within 5 seconds press its button 1 (LED B blinks red, then stops)
  And after a successful link the product's yellow "!" disappears
  And the dialog advances to the next product until the list is exhausted
  And products already linked, or products IHC OpenVisual does not recognise, are skipped

Scenario: Link a single product
  Given the linking dialog is open
  When I select one product (its "Link" action becomes enabled) and click "Link"
  Then I perform the same button-A then button-1 sequence for that one product
  And its yellow "!" is removed

Scenario: Linking clears a product's stand-alone programming
  Given a wireless product that carries existing stand-alone programming
  When it is linked to the controller
  Then its existing stand-alone programming is erased
  And on a multi-key switch every key becomes an input in IHC OpenVisual (so its keys cannot be split
    between stand-alone use and IHC Control)

Scenario: Audible feedback per attempt
  Given "Sound on linking" (sound on linking) is enabled
  Then the PC plays one sound for a successful link and a different sound on error
  And a "Test" button lets me preview both sounds
```

### AC illustrations

- After *Link all* completes, no wireless product in the tree shows a yellow **!**, and each now
  displays a serial number in the linking dialog.
- Approximate capacity: up to ~64 wireless products depending on usage pattern.

### Constraints

- Verification method — **Demonstration** against a live controller and physical products.

**Readiness:** Not Ready.
- [R4] External dependency: requires a live controller and physical wireless devices; the exact dialog
  states during the button‑press handshake are described in prose only and not yet confirmed against a
  running installation.
- [R5] The precise contents of the per‑step link prompt (beyond the button‑A/button‑1 instruction and
  sound feedback) need confirmation on hardware.

---

## US-017 — Unlink and signal‑test wireless products

**Status:** ⛔ **Blocked** — wireless *wiring*; requires the wireless API (not available yet).

**As a** commissioning technician, **I want** to unlink products and test their signal strength and
battery level, **so that** I can re‑commission a product or verify coverage.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Unlink one or all products
  Given the "Controller" > "Link Wireless products" dialog is open
  When I select a product and click "Unlink" (or click "Unlink all")
  Then the link to the controller is removed for that product (or all products)
  And "Unlink all" proceeds without a confirmation prompt and cannot be undone (use with care)

Scenario: Test a product's signal and battery
  Given a linked product is selected in the linking dialog
  When I click "Test" and then actuate the product (e.g. press a button input)
  Then the dialog shows the communication signal strength (e.g. 45; max 48, minimum usable 10),
    the battery level, and the time the message was received
  And for a mains-powered (receiver) product the battery field is inactive (it is not battery-powered)
  And if the battery is removed, the last measured value is shown

Scenario: Wireless test kit range check
  Given a wireless signal-strength test product ("<product>") has been inserted (via Special products)
  When I choose "Controller" > "Link/test IHC Wireless products"
  And I select the test unit and click "Link"
  And I switch the physical test unit on with its I-button
  Then the link status shows a green check mark
  When I click "OK" and do not press the physical test unit again
  And I click "Test" in IHC OpenVisual
  Then the test unit's LED indicates link quality (a brief blink about every 5 seconds) while IHC OpenVisual
    shows the RSSI value, letting me walk the unit around to survey coverage
  And clicking "OK" ends the test screen and switches the test unit off
```

### AC illustrations

- A *Test* reading of `45/48` with a recent receive timestamp confirms good coverage (well above the
  minimum of 10); a reading near 10 indicates a marginal link.

### Constraints

- Verification method — **Demonstration** on hardware.

**Readiness:** Not Ready.
- [R4] External dependency: requires a live controller, physical products, and (for the survey) the
  Wireless test kit; RSSI/battery readouts are not yet confirmed against a running installation.
