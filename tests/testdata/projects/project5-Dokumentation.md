# `project5-Dokumentation.vis` — the documentation-report oracle

**What it is.** A single vendor-written IHC Visual project whose *documentation report* exercises every
field, element kind and text form the report can render — including a deliberate punch-list of errors.
It is the fixture for report/US-040/US-041/US-072 work and a byte-fidelity oracle for the `.vis` engine.

**Provenance (A-1).** Authored **exclusively by driving LK IHC Visual** (`C:\Program Files (x86)\LK IHC
Control\IHC Visual`, catalog 100 products / 72 function blocks) through the `ihcvisual` UI-automation
MCP over six sittings (2026-07-30 … 2026-07-31). No byte of it was hand-edited — no text editor, no
script, no SDK write path ever touched it. The authoring plan is `tmp/reportoracle-authoring.md`, the
requirement spec `tmp/reportoracle.md`, and the full measurement log `tmp/reportoracle/runlog.md`.

| Property | Value |
|---|---|
| Size | 73 884 bytes · 1 231 lines · 343 element instances (95 distinct types) · 338 ids · `last_unique_id="_0x1c2"` |
| Encoding | ISO-8859-1, **no BOM**, CRLF throughout incl. trailing, 3-space-per-depth indent |
| Numeric entities | only `&#xD;` / `&#xA;` (A-6) |
| Content | 13 localities · 9 products (5 families) · 3 function blocks · 23 FB pins covering all 22 variable types · 10 link pairs · 3 scene members · 8 programs · 3 enum types |

---

## 1. Design — what each element is here to prove

### Localities (13, all flat)
`Stue` holds P1–P6; nine further untouched template rooms exercise the "empty locality still listed"
path; **`Etage 1`** holds FB-Z only (**C-2.3**: a locality with blocks but no products, different from
the locality of the products those blocks drive — this drives the end-user report's differing-locality
suffix); **`Teknik`** holds P7–P9 + FB-L + FB-T; **`Tom lokalitet`** stays permanently empty (**C-2.2**).
Three carry a `@note` (**C-2.4**).

> ⛔ **The report's nested-locality path is NOT covered by this oracle.** IHC Visual offers no route to
> nest a `group` inside a `group` — all four candidate routes were falsified and 0 of 239 `group`
> elements in the whole corpus are nested. Requirement C-2.1 was dropped by owner ruling and lives in
> the spec's out-of-reach register as **X-9**. Anything that renders nested localities must be covered
> by a hand-built or synthetic fixture instead, which is why this file cannot be that oracle.

### Products (9, five element families)
| # | Element | Purpose |
|---|---|---|
| P1 | `product_dataline` *LK FUGA Tryk 4 tast 2 dioder* | 6 terminals — the addressing zoo and **4 of the 8 punch-list gaps** |
| P2 | `product_dataline` *Lampeudtag* | **the issue-free product (C-8.10)** — every field set; it must stay **absent** from the punch-list |
| P3 | `product_dataline` *Egen kontakt & "test"* | `locked` default + user-edited name (**C-4.3**); name carries `&amp;` and `&quot;` (**C-11.1**) |
| P4 | `product_dataline` *Lux / Temperatur sensor med logning* | terminal wrapped in a `settings` container + product-level `resource_*` children (**C-4.2**) |
| P5 | `product_airlink` *Jalousi 2 tast (lokal lås)* | shutter terminal kinds; `serialnumber` sentinel (**C-4.4**, R-7) |
| P6 | `product_airlink` *Modtager relæ* | `airlink_relay`; also has **no `documentation_tag`** at file level |
| P7 | `product_rs485_led_dimmer` | 2 channels × (4 doc fields + 4 `..._error_state_*` + 6 `dimmer_settings`); `load_mode` = `rc` on ch.1, default `auto` on ch.2 (**C-4.5**) |
| P8 | `product_rs485_sms_modem` | 4 `cablecolour_*` + non-default pincode + 3 of 30 phone rows (**C-4.6**) |
| P9 | `s0_device` | `cable_colour_minus/_plus`, `ticks="1000"` (**C-4.7**); `note` keeps the untranslated key `PRODUCT_2315_NOTE` verbatim (**C-11.4**) |

### Function blocks (3)
- **FB-L** `6.3.03.a. Overfaldstryk` — a **library** block: `locked="yes"`, all `master_*`, multi-line
  `master_programmer`, non-empty `helpfile`, note containing `&quot;` (**C-7.1**).
- **FB-T** *Tom blok* — from-scratch and **never touched again** (**C-7.2**); its free default
  `program_simple` with empty `events` and empty `actions` is the **C-9.2** witness.
- **FB-Z** `Doku zoo` — carries the variable zoo (23 pins, all 22 vendor types, spread across both
  `settings` and `internalsettings`) and the program zoo (4 authored programs).

### Programs in FB-Z
| Program | Shape | Covers |
|---|---|---|
| `Program` (PZ-2) | empty `events` + empty `actions` (catalogue default, untouched) | C-9.2 |
| `Hændelser` (PZ-1) | `event` + **`event_power`** → 10 actions spanning every statement form | C-9.1, C-9.9, C-9.10 (named) |
| `Betingelser` (PZ-3) | `Under program` (default-AND conditions incl. an inline int and a nested `type="or"` group; non-empty true **and** typeless false branch; a nested `program_sub` with an **empty `<conditions/>`**) + a second sub carrying the **`Betinget kommando`** wording | C-9.3–C-9.6, **C-9.8** |
| `Case` (PZ-4) | `program_case` over the enum (2 `case_action`s + trailing Else, one branch holding a nested `program_sub`) **and** a second `program_case` over an **integer** | C-9.7, C-9.10 (unnamed), `%LT` |

### The deliberate error punch-list (C-8) — do not "fix" these
All eight US-072 checks fire, on four different elements, and every blanked field is non-empty on ≥1
other element. **Verified against the report model** (`ReportBuilder.BuildProjectDocumentation`), not by
eye: 9 rows, and `Lampeudtag` (P2) produces **0** rows.

| Check | Where the gap sits |
|---|---|
| Mangler Id-kode | P1 (product level) |
| Mangler Lysgruppe | P4 |
| Mangler Placering | P4 |
| Mangler Kabeltype | P3 |
| Mangler Kabelnummer | P3 |
| Mangler Adresse | P1 `Tryk (nederst højre)` |
| Ikke forbundet | P1 `Tryk (nederst højre)` |
| Mangler Ledningsfarve | P1 `Tryk (nederst venstre)` (addressed but colourless) + P1 `Tryk (nederst højre)` |

> ⚠ **The Id-kode gap must sit on a `product_dataline`.** `ReportBuilder.BuildCompleteness` walks only
> wired products, so the spec's original placement of this gap on the *airlink* P6 left the check
> silently unfired — caught by the V7 report-model assertion. P6 still has no `documentation_tag`, so
> the file-level witness survives too.
> ⚠⚠ **The vendor auto-assigns sibling terminal addresses.** After addressing P1's first three inputs,
> the untouched `Tryk (nederst højre)` had silently acquired `address_dataline="_0x4"`, destroying three
> requirements at once. It was cleared by re-opening the terminal and choosing `ikke konfigureret`. Any
> future edit to P1 must re-assert that this terminal is still address-less.

---

## 2. Coverage: C-id → witness

Machine-checked against the landed bytes — **46 of 46 assertions pass**. Highlights:

| Requirement | Witness in the file |
|---|---|
| C-1.1–1.4 | `project_info` 5/5; `installer_info` 8/8; `customer_info` 8/8 |
| C-3.1–3.4 | 3 input + 2 output modules fully documented; creation order `2, 1, 8` ≠ sorted (**C-3.3**); every referenced module documented |
| C-5.2 | `dataline_output@type` = `led` **and** absent (= `unspecified` default) |
| C-5.3 | `backup="yes"` and `inivalue="on"`, both on **outputs** (see O-4) |
| C-5.4 | 8 addressed terminals — `_0x1` (linear min), `_0x9`, `_0x11`, `_0x20`, `_0x3`, `_0x71`, `_0x72`, `_0x80` (linear max) across 3 input + 2 output modules |
| C-5.8 | all wireless/bus terminal kinds + **8** `rs485_led_dimmer_error_state_*` (4 per channel) + `W` + `kWh` |
| C-6.6 | `scene_shutter@shutter_position="down"`, `scene_relay@relay_value="on"`, `scene_dimmer@dimming_value="60" @ramptime_ms="3000"`, 3 `scene_link` back-references |
| C-7.4 | **22/22** vendor variable types present |
| C-9.1–9.10 | see the program table above; all ten met |
| C-10.1–10.2 | 3 enum types — 2 template-seeded `typeid` built-ins + 1 user-defined with 3 values |
| C-11.1–11.6 | `&quot;`, `&amp;`, `&apos;`, multi-line values, `PRODUCT_2315_NOTE`, Latin-1 clean |
| C-12.1 | `enduser_report="yes"` present and absent across products |

### Not covered, and why (each is a measured impossibility, not an omission)
| Item | Status |
|---|---|
| **C-2.1** nested localities | **X-9** — no vendor route exists; the report's nested path is uncovered |
| **C-10.3** enum `@note` | enum editor (dialog 24588, 11 controls) exposes **no** note field → spec §7 |
| **C-5.8** `light_indication@note-2` | no vendor dialog exposes a second note field → spec §7 |
| **C-6.6** `scene_dimmer@delay_ms` | the dimmer scene chooser has no delay control; ramp time is whole seconds only |
| **C-1.3** "all eight distinct" | 7/8 distinct — spec §5.1 itself specifies `country="Danmark"` for **both** parties |
| P5 `shutter_settings` travel times | present-but-empty (legal vendor output). The two pickers are outside the dialog's tab chain, so no keystroke can reach them; needs a `dialog.focus` driver verb. **No C-requirement depends on it.** |
| `resource_date@year` / inline date `@year` | **unauthorable** — the vendor's date picker has no year field, so both commit `year="2000"` |

---

## 3. Open questions answered while authoring (spec §10)

| # | Answer |
|---|---|
| **O-1** | The `+2` display offset **is** a real vendor convention, but it applies to **input** terminals with logical number ≥9 only, and it lives in the **chooser, not the file**: the input chooser lists `1…8, 11…18` (9 and 10 skipped) while the output chooser is contiguous `1…8`. The file stores the un-offset logical value (display `11` → `_0x9`). |
| **O-2** | `enduser_report` **is** user-togglable (control 303), though it is `visible:false` on some dialog layouts. |
| **O-3** | Locality note: **yes** (`group@note`). Enum editor note: **no** — dialog 24588 has 11 controls and no note field on either the type or the value. |
| **O-4** | The **output** terminal dialog exposes `inivalue` (droplist 370) and *Ved strømsvigt* (369); the **input** dialog exposes **neither**. So `inivalue` on an input is not authorable; C-5.3 is witnessed on an output. |
| **O-5** | The S0 dialog **does** expose `ticks` — *Antal pulser pr 1 kW*, control 525, a plain Edit (default 100, set to 1000). |
| **O-6** | **All three stretch items succeeded** — first-ever on-disk observations of an inline `resource_temperature` constant (`inivalue="18.50"`), an inline `resource_floating_point` constant (`"2.72"`), and an **integer** `program_case`. A `resource_counter` inline constant also appeared. |
| **O-7** | *Funktionsblok egenskaber* = Navn 416 + multiline Note 417; there is no separate *Anvendelse* field — the Note is it, and it is editable on a from-scratch block. `resource_scene@note-2` is **not** authorable. |

---

## 4. Maintenance rules

1. **Do not "tidy" the punch-list.** Every blank in §1's gap table is load-bearing; filling one empties
   the report's error section and breaks A-4.
2. **Byte-locked.** Registered in `ProjectByteFidelityTests` (both `[TestCase]` lists). Any change to
   the `.vis` writer that alters these bytes is a regression, not a fixture update.
3. **Vendor idempotence is *not* byte-exact, by design.** An IHC Visual open+save re-hoists the two
   template-seeded catalogue enums (`Persienne tilstand`, `Logning`) to fresh ids **on every cycle**,
   burning 13 ids each time. The measured delta is exactly `id2` + `last_unique_id` + `<modified>` +
   those two `enum_definition` blocks + their 3 `typedef`/`inivalue` references — no structural or
   content change. Expect that, not equality.
4. **If it must be re-authored,** drive IHC Visual — never edit the XML. `tmp/reportoracle-authoring.md`
   is self-resuming and carries the verified dialog control maps.
5. Recommended follow-up (not part of this fixture): make the A-4/A-7 assertions permanent — a test
   asserting all eight punch-list checks fire and that P2 is absent. They were verified once, by hand,
   at authoring time.
