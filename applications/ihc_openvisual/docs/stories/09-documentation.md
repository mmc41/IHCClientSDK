---
version: 0.4.0
last-updated: 2026-08-02
status: draft
---

# E9 — Documentation & reporting

> **Scope:** Partly in scope. Entering project information (US-039), editing user-defined data-table
> texts (US-049) and viewing the Wired module address map (US-050) are project-metadata / read-only CRUD.
> Report generation reads the project to produce output: one **navigable project-documentation report**
> whose content sections and detail options the installer can switch on and off (US-040 + US-071), a
> **documentation-completeness / issues report** (US-072), and a deep **function-block logic report**
> (US-041). All report output is **image-free** — product identity, wire colours and module addressing are
> rendered as text/tables, never as pictures, icons, diagrams or logos.

**Goal:** Let an IHC installer capture project- and product-level documentation and generate a complete,
tailorable set of project reports — technical/installation, end-user/function, cabling and addressing
cross-references, a documentation-completeness check, and the function-block logic — so the delivered
installation is fully and consistently documented.

**Scope:** entering project information (*Documentation ▸ Project info*); viewing and editing the
project's data tables — the read-only system tables and the editable user-defined texts
(*Documentation ▸ Data tables*); viewing the Wired input/output module address map
(*Documentation*); and the reporting view under *Documentation ▸ Reports* — the project-documentation
report and its selectable purposes (US-040), the per-report **content-section and detail-option
switches** (US-071), the **documentation-completeness / issues** report (US-072), and the
**function-block logic** report (US-041). **Scope excludes:** any picture/icon/diagram/logo in report
output (images are out of scope — reports are redesigned to convey the same information as text and
tables); the per-product documentation *fields* (US-011) and the note text on function-block inputs
(authored in E7), which *feed* these reports.

**Acceptance criteria (epic level):**

- MUST: The installer can enter project / customer / installer information and generate the project
  documentation report from the data entered while building the project.
- MUST: The report is presented as **one navigable document** whose **content sections** (project
  identity, installer, customer, cabling & addressing cross-references, per-locality wiring & function
  detail, documentation-completeness issues, function blocks) and **detail options** (empty fields,
  internal ids, wire colours, link display, function documentation, all-vs-connected terminals, end-user
  filter) the installer can switch **on and off**, with selectable purpose presets (installation /
  technical, end-user / function, function-block, full).
- MUST: A **documentation-completeness** report lists, per product and terminal, what documentation is
  missing or inconsistent (unlinked terminal, missing identification code / light group / cable type /
  cable number / wire colour / placement / data-line address), so the installer gets a punch-list.
- MUST: In the **end-user / function** purpose, products not flagged for end-user documentation are
  omitted; in the **installation / technical** purpose every product is listed and un-filled fields render
  as blank placeholders (omission is end-user-purpose-only; see the US-040 appendix).
- MUST: The installer can add, edit and delete user-defined data-table texts, while the built-in system
  tables and the Wired module address map are shown read-only.
- MUST: All report output is **image-free**: no product icons, no graphical module diagrams, no installer
  logo, no external manual/help pictures — the same information is conveyed as text and tables.

**Readiness:** Ready — the report content, the section/option switches, the completeness report and the
per-field function-block layout are all specified in this epic (see the US-040 appendix and US-041/071/072).

---

## US-039 — Enter project information

**Scope:** In scope — project-metadata CRUD (writes project / customer / installer info into the project).

**As an** IHC installer, **I want** to record project, customer and installer information, **so that**
the reports identify the installation and its parties.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Open the project-info dialog
  Given a project is open
  When I choose "Documentation" > "Project info"
  Then a dialog opens for project-level information

Scenario: Project info feeds the reports
  Given I have filled in the project/customer/installer details
  When I later generate a report (US-040)
  Then the installation report's installer and customer section (name, address, phone) reflects what I entered
```

### Business rules (the dialog's field set)

- MUST: The dialog carries a **Project** group of **five** fields, in this reading order — *Number*,
  *Project type*, *Programmer*, *Drawing* and *Description* — with OK/Cancel. All five are
  project-level attributes the file format declares; *Project type* and *Drawing* are fields like the
  others, not extras.
- MUST: The dialog carries two **contact** groups — **Installer** and **Customer** — each with the
  same eight fields: *Name*, *Street*, *Phone*, *Postal code*, *Mobile*, *City*, *Email* and
  *Country*.
- MUST: **Editing project info never erases stored project-information values.** Every
  project-information attribute the file carries survives an edit round-trip — including any the
  dialog does not show. (A field that is shown must be written back as edited; a value the file
  carries but the dialog does not surface must be carried through unchanged, not dropped when the
  dialog is committed.)
- MUST: It is reachable from the *Documentation* menu.

### AC illustrations

- The installation report's header lists installer and customer information (name, address, telephone)
  drawn from *Project info*.
- *Documentation ▸ Project info…* opens a dialog whose **Project** group holds `Number`,
  `Project type`, `Programmer`, `Drawing` and `Description` in that order, and whose **Installer** and
  **Customer** groups each hold the eight contact fields.
- Opening a project whose file records a project type of `Villa` and a drawing of `Tegning 4b`,
  editing only the project number, and committing the dialog leaves `Villa` and `Tegning 4b` in the
  saved file — committing the dialog rewrites nothing it did not change.

### Constraints

- The **report** renders the installer/customer **Navn / Adresse / Telefon** (name / address / phone) in
  their masthead blocks, and the **Projekt** identity (description, number, programmer). The other
  project-info fields (city, zip, country, mobile, email, udf) are captured by the dialog but appear in
  **no** report section (see the US-040 appendix). This asymmetry is deliberate: the dialog is the
  project's record, the report is a subset of it.
- The report also carries a **generation / last-updated timestamp** so a printed copy is dated; the
  installer identity that fills the report's installer masthead is part of the project's captured
  identity. Whether a masthead block (installer / customer) appears at all is governed by the report's
  content switches (US-071).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (report generation-timestamp surfacing follows US-040/071).

---

## US-040 — Generate the project documentation report

**Scope:** In scope — report generation reads the open project to produce output. The report is one
navigable document that carries the **installation / technical** content and the **end-user / function**
content as selectable purposes; its sections and detail options are switched on and off in US-071, its
completeness section is US-072, and its function-block section is US-041.

**As an** IHC installer, **I want** to generate the project documentation report — its technical /
installation content and its end-user / function content — from the data I entered while building the
project, **so that** I can hand over complete, dated documentation of the installation.

### Acceptance criteria (Checklist)

- MUST: *Documentation ▸ Reports* opens the reporting view. It offers the project documentation report
  with selectable **purpose presets** — **Installation / technical**, **End-user / function**,
  **Function-block** (US-041) and **Full** — each preset a predefined combination of the content-section
  and detail-option switches (US-071).
- MUST: The report renders as **one navigable document** for the whole open project, with a
  section-jump / back-to-top **overview** so the reader can move between sections (screen), and a
  **printer-friendly** variant (see appendix).
- MUST: The **installation / technical** content contains the project / installer / customer identity;
  the cabling & addressing cross-references (US-073 section content); and, per locality, each product's
  Locality, Placement, Component type, Identification code, Cable no., Cable type, Light group, and its
  input/output terminals with decoded data-line address and wire colour.
- MUST: Products and localities are documented **in the order they appear** in the *Installation* pane;
  function blocks in *Functions*-pane order (US-041).
- MUST: The **end-user / function** content lists localities and, per locality, the input functions
  (e.g. buttons), where each input's text comes from the *Name* + *Placement* of the product and the
  *Note* on the function-block input it drives (note propagation — see appendix).
- MUST: In the **end-user / function** purpose, a product not flagged for end-user documentation is
  **omitted**; in the **installation / technical** purpose every product is listed and any un-filled field
  renders as a blank placeholder (`--`), not suppressed. (Omission is end-user-purpose-only; see the
  output-format appendix.)
- MUST: The report is **image-free** — a product is identified by its **type name + name/placement text**,
  not by an icon; module addressing is a **table**, not a diagram; there is **no installer logo** and **no
  external manual/help picture**. (See the appendix "Image-free redesign".)
- SHOULD: Every purpose is available in a **printer-friendly** variant (compact, black-on-white, gridded,
  tables kept whole across page breaks; screen-only navigation dropped — see appendix).

### AC illustrations

- An end-user row "<product> By door" is formed from the product's *Name* ("<product>") + *Placement*
  ("By door"); the sub-line under a terminal comes from the *Note* on the function-block input that button
  drives — writing that note once propagates it to every physical terminal linked to the block.
- Selecting the **Installation / technical** preset shows every product with its full terminal/cable
  detail and the cross-reference tables; selecting **End-user / function** hides the technical columns and
  shows only end-user-flagged products with their behaviour notes — the **same** underlying report, two
  switch combinations (US-071).

### Constraints

- Verification method — **Demonstration** that the report renders entered data in installation order, that
  each purpose preset shows the right sections, that the end-user purpose omits undocumented products, and
  that no report output contains an image.
- **Report content comes from the engine.** The engine supplies a render-ready report model — sections in
  order, resolved field values, blank→`--` decisions, the end-user omission filter, note propagation, and
  decoded data-line addresses — and this story covers the model→HTML transform, the switch application
  (US-071), the print variant, and standard-browser display (see the appendix "OpenVisual approach"). The
  field / order / omission spec in the appendix is the contract that model satisfies.

**Readiness:** Ready.

**Implementation status:** 🟡 Installation and end-user content implemented; the SDK builds ONE combined
project-documentation model (all three sections in fixed order, with the switch-supporting data — per-section and
per-element internal ids + inclusion flags, raw-blank-beside-display values, a unified locality view); and a single
**Reports…** view renders it as ONE navigable HTML document (a screen overview / section-jump / back-to-top that the
printer variant drops), replacing the former six direct report commands. The heading carries the report **generation
timestamp** (from an injected clock, fixed format) and the **programmer**, a **Projekt** identity section
(description / number / programmer) renders near the top, the technical terminal detail now carries the **link
display** (`→ FB input → function block → its locality`) and the **function note** of each linked terminal's driving
FB input, a consolidated **Kabler** cabling table lists one row per addressed terminal in address order, and the
module section is a **per-terminal address map** (which product terminal occupies each address, per input/output
module). The content-section and detail-option switches over that model, and the **purpose presets** (Installation /
technical, End-user / function, Function-block, Full — each a named starting combination of the switches, selectable
in the Reports… view), are implemented. The **image-free redesign** is also in place: a product is identified by its
**resolved catalog type-name text** (beside its name/placement) rather than a product-image key or icon, the module
section is a tabular per-terminal address map (no diagram), and the document carries no logo or banner image.

<!-- BEGIN appendix — report output format (delimited; removable wholesale) -->

### Appendix — report output format

This appendix specifies the content and layout of the project documentation report over the reference
project `project3-KompleksWired.vis`. The report is **not a dialog and not an export step**: choosing
*Documentation ▸ Reports* renders a static HTML page for the whole open project, which the user views and
prints in a standard browser. The rules below (sections, order, columns, blank handling, omission, note
propagation, switch behaviour and the image-free rule) fully specify the output.

> **OpenVisual approach.** Two layers, matching the app's architecture (business logic in the engine,
> GUI thin):
> - **Engine.** The engine returns a render-ready **report model** with *all* content already computed:
>   the sections in order, each product's resolved field values, the blank→`--` decisions, the end-user
>   omission filter applied, note propagation resolved, the documentation-completeness issues found
>   (US-072), and data-line addresses decoded. **The field / order / omission spec below is the contract
>   that model satisfies.**
> - **This story (US-040).** IHC OpenVisual transforms the report model into HTML (a mechanical template —
>   no business logic), applies the active content/detail switches (US-071) and the print CSS variant, and
>   displays it in the user's **standard browser**. The only runtime dependency is a standard browser able
>   to display static HTML — the app is self-contained and needs no prior IHC software installation
>   (US-063).

> **Image-free redesign.** Reports carry **no images of any kind** — the same information is conveyed as
> text and tables:
> - A product is identified by its **type name** (a heading) plus its **name / placement** text — never by
>   a product icon or photo.
> - Input/output **module addressing** is presented only as **tables** (US-073); there is **no graphical
>   module diagram**.
> - There is **no installer logo**, **no title-banner image** (a text heading replaces it), and **no
>   external manual / help / PDF picture or link**.
> - A **wire colour** is shown by its **colour name** (text); an optional inline colour chip is a CSS
>   swatch, not an image file, and the report remains fully legible in black-and-white without it.

**Output mechanism / view.** *Documentation ▸ Reports* presents the reporting view titled "Projekt
dokumentation" with the **purpose presets** (Installation / technical, End-user / function,
Function-block, Full) and the content/detail switches (US-071). Choosing a purpose renders one HTML page.
There is **no on-screen-preview vs. direct-print vs. export distinction** — every choice renders an HTML
page; printing is the browser's own Ctrl+P. No app-supplied page header/footer/page-number; the report's
**generation timestamp** is rendered once near the top; the only app page-break hint is "avoid breaking
inside a table".

**Report structure (top→bottom).** Each numbered block is a **content section** that US-071 can switch on
or off; the order is fixed.

1. **Heading + metadata** — text heading "Projekt dokumentation" and the **generation / last-updated
   timestamp** and programmer. Always present.
2. **Projekt** — project description, number, programmer.
3. **Installatør** masthead — `Navn / Adresse / Telefon`. Blank → `--`.
4. **Kunde** masthead — `Navn / Adresse / Telefon`. Blank → `--`. (Other captured identity fields — city,
   zip, country, mobile, email, udf — are **not** shown.)
5. **Cabling & addressing cross-references** (US-073) — the *Kabler* table (one row per addressed terminal,
   by data-line address, with wire colour), the *Datalinie indgange* / *Datalinie udgange* flat tables
   (all inputs / all outputs, by address), the *Datalinie input/output-moduler* address map, and the
   *Specielle Produkter* / *S0 Device* tables. In these flat tables a blank field is an **empty cell**.
6. **Per-locality wiring & function detail** — **every** locality in Installation-pane order (empty
   localities included). Under each, each product renders as a heading (**type name + name/placement**, no
   icon) with label→value rows `Lokalitet, Placering, Komponent, Identifikationskode, Kabelnummer,
   Kabeltype, Lysgruppe` and a terminal sub-block. Per terminal: the terminal name, its `Adresse`
   (`Indgang`/`Udgang` + decoded data-line address, unassigned → `?`), its wire colour, and — when the
   detail options are on — the **link display** (`→ <FB input> → <function block> → <its locality>`, the
   terminal(s) it drives) and the **function documentation** (the behaviour note resolved from the driving
   FB input). Airlink, RS485-LED-dimmer and RS485-modem products use reduced field sets (serial number /
   cable-colour rows). Blank → `--`.
7. **Fejl i dokumentation** — the documentation-completeness / issues section (US-072).
8. **Funktionsblokke** — the function-block logic section (US-041).

**Purpose presets = switch combinations.**
- **Installation / technical** — sections 1–6 on; every product listed; blank fields shown as `--`; no
  end-user filter; technical detail (cable/terminal/address) on.
- **End-user / function** — a locality list, and under each locality **only products flagged for end-user
  documentation**; per input terminal a bullet `• <terminal name>` and, **per link on that terminal**, a
  sub-line `- <Note of the FB input it drives>`. The note lives on the FB input and is reached through the
  link, so **one note propagates to every physical terminal linked to the block**, and a terminal with
  several links repeats the note once per link. **[screen only]** when the driving FB sits in a different
  locality than the product, the note sub-line is suffixed `(<that locality>)`. Technical columns
  (cable/address/id) are hidden.
- **Function-block** — the *Funktionsblokke* section only (US-041).
- **Full** — every section and detail option on.

**Omission rule.** In the **end-user / function** purpose a product not flagged for end-user documentation
is dropped; localities are never dropped. In every other purpose no product is dropped — undocumented
fields appear blank (`--` / empty).

**Printer variant.** A CSS swap: black text, compact `xx-small`, gridded borders, tables kept whole
across page breaks. It drops the screen-only navigation (overview / section-jump / collapsibility) and the
end-user differing-locality suffix, and — like the whole report — contains no image.

<!-- END appendix -->


---

## US-041 — Generate the function-block logic report

**Scope:** In scope — report generation reads the project to produce output; the **function-block** purpose
of the project documentation report (US-040), specified here as a **deep, per-block logic listing**.

**As an** IHC installer, **I want** to generate a report that documents each function block in full — its
purpose, its inputs and outputs, its settings and internal variables, and its programmed logic — **so
that** I can hand over and review the control logic alongside the installation and end-user content.

**Scope excludes:** the installation / end-user content (US-040); the section/detail switches (US-071).

### Acceptance criteria (Checklist)

- MUST: *Documentation ▸ Reports* offers a **Function-block** purpose (section heading *"Funktionsblokke"*)
  alongside the other purposes, in both a screen and a **printer** variant.
- MUST: Produced from the engine's report model and transformed into HTML, shown in the standard browser
  for viewing/printing — same mechanism as US-040 (see the US-040 appendix "OpenVisual approach"), and
  **image-free** (no block diagram; the logic is rendered as text and an indented outline).
- MUST: Blocks are documented **in Functions-pane order** (document order — no re-sort), consistent with
  US-040's ordering rule.
- MUST: **Each function block** renders the fields specified in the "Function-block layout" appendix below —
  its name, application text, inputs, outputs, settings, internal variables and programs.
- SHOULD: The **printer** variant is the same layout with the print stylesheet (black text, compact
  `xx-small`, gridded borders, blocks/tables kept whole across page breaks) — a CSS swap.

### AC illustrations

- Over `project3-KompleksWired.vis` the report documents each function block (e.g. the *Kip tænd sluk*
  and *PIR styring* blocks) with its application text, its inputs/outputs and their notes, its settings
  (e.g. `Timer = 00:03:00,000`), and its program as an indented event → command outline; an unprogrammed
  block renders as *Tom blok* with no internals.

### Constraints

- Verification method — **Demonstration** that the report documents the project's function blocks in
  document order with the appendix's field layout, screen and print variants, and no image.

**Readiness:** Ready — the per-field layout is itemised in the appendix below.

**Implementation status:** ✅ Implemented — the combined report renders the deep per-block layout: the block
description, input/output notes, settings and internal variables as `name = value`, and a flattened program outline
(events → commands, sub-programs with conditions and commands, scene invocations); an unprogrammed block renders as
*Tom blok*.

<!-- BEGIN appendix — function-block layout (delimited; removable wholesale) -->

### Appendix — function-block layout

Per function block, in Functions-pane document order, the report renders the following, as headings and an
indented outline (no tables of images; internal element ids appear only when the *internal ids* option is
on, US-071):

1. **Block heading** — the block's name / catalogue designation.
2. **Application** — the block's descriptive purpose text (the "Anvendelse" text).
3. **Input** — one entry per block input: the input name and its **note / behaviour text** (the note an
   installer fills to explain what that input does).
4. **Output** — one entry per block output: the output name and its note / behaviour text.
5. **Indstillinger** (settings) — each configurable setting as `name = value` (e.g. a timer duration).
6. **Interne variable** (internal variables) — each internal variable as `name = value`.
7. **Programmer** (programs) — each program in the block, rendered as an **indented outline**:
   - **Hændelser** (events / triggers) — the events that start the program (e.g. `<input> -> ON`).
   - **Kommandoer** (commands) — the actions, including nested **Under program** groups with their
     **Betingelser** (conditions) and **Kommandoer ved betingelser sande** (commands run when the
     conditions hold), and any scene invocations (`Fremkald Scenarie <name>`).
8. **Empty block** — a block with no programmed logic renders as **Tom blok** with the heading only and no
   internals.

<!-- END appendix -->

---

## US-071 — Tailor the report: switch content sections and detail options on/off

**Scope:** In scope — the on/off model that lets one report serve several purposes. Governs which
sections of US-040 / US-072 / US-073 render and how much detail each shows.

**As an** IHC installer, **I want** to switch the report's content sections and detail options on and off,
**so that** I can produce a document focused on exactly what a given reader needs — a technical hand-over,
an end-user guide, a cabling list, or the full record — without maintaining separate report files.

### Acceptance criteria (Checklist)

- MUST: **Content-section switches** — each of these sections can be individually turned on or off, and the
  report re-renders with the section shown or hidden while the top-to-bottom order stays fixed: *Projekt*,
  *Installatør*, *Kunde*, the cabling & addressing cross-references (US-073), the per-locality wiring &
  function detail, the documentation-completeness issues (US-072), and the function blocks (US-041).
- MUST: A switched-**off** section produces **no output at all** (not an empty heading).
- MUST: **Detail-option switches** apply within the sections that are on:
  - **Show empty fields/columns** — when off, a field or column that is blank for every row is dropped and
    blank cells render as nothing; when on, blanks render as placeholders (`--` / empty cell).
  - **Show internal ids** — reveal each element's internal id beside its name.
  - **Show wire colours** — show or hide the wire-colour column/annotation (as colour-name text).
  - **Link display** — show or hide, per terminal, the path to the function-block input it drives.
  - **Function documentation** — show or hide the behaviour notes resolved from the driving FB input.
  - **Show all inputs/outputs** — include unconnected terminals, or restrict to terminals in use.
  - **End-user filter** — restrict the per-locality content to products flagged for end-user documentation.
- MUST: The US-040 **purpose presets** (Installation / technical, End-user / function, Function-block,
  Full) are named starting combinations of these switches; changing a switch adjusts from the chosen preset.
- SHOULD: On screen, an **overview** control lists the sections that are on and jumps to them / back to the
  top; sections MAY be collapsible. The printer variant drops these navigation aids (US-040 appendix).
- SHOULD: Switch state applies to the current report view and persists for the session; it MAY persist per
  project.

### AC illustrations

- Turning off *Installatør*, *Kunde* and the completeness section, and turning off the end-user filter,
  yields a pure cabling-and-wiring document; turning the end-user filter on and the technical columns off
  yields an end-user guide — one report, two switch sets.
- Turning **Show internal ids** on adds each terminal's internal id beside its name throughout; turning it
  off removes them everywhere.

### Constraints

- Verification method — **Demonstration** that toggling each switch shows/hides the corresponding section,
  column or annotation, and that an off section emits nothing.
- The switch surface (menu grouping, checklist, presets) is a fixed requirement; its exact placement and
  wording are not itemised here.
- A **graphical module overview** option is deliberately **excluded** — images are out of scope, so the
  module addressing is always tabular (US-073), and there is no picture/diagram toggle.

**Readiness:** Ready.

**Implementation status:** 🟡 Implemented in the Reports view — the three **content sections** toggle on/off (an
off section emits nothing), and the render-level **detail options** (show empty fields, internal ids, wire colours,
link display, function documentation) apply within the sections that are on; the view-model owns the toggles for the
session. ⚠ The two rebuild-level options (all-vs-in-use terminals, end-user omission filter) remain, as they need
both the filtered and unfiltered data carried on the combined model rather than a render toggle.

---

## US-072 — Documentation-completeness (issues) report

**Scope:** In scope — report generation reads the project to produce output; a read-only validation
section that reports gaps, it never edits the project.

**As an** IHC installer, **I want** a report that lists what documentation is missing or inconsistent
across the project, **so that** I get a punch-list of everything to finish before hand-over.

### Acceptance criteria (Checklist)

- MUST: A content section headed **"Fejl i dokumentation"** — a switchable section (US-071) and a report
  purpose in its own right — grouped **per locality → product → terminal**, listing **only** the products
  and terminals that have an issue (fully documented ones are omitted).
- MUST: The checks MUST include, per terminal / product:
  - terminal **not linked** to anything;
  - missing **identification code**;
  - missing **light group**;
  - missing **cable type**;
  - missing **cable number**;
  - missing **wire colour**;
  - missing **placement**;
  - missing **data-line address** (terminal left unaddressed).
- MUST: Each issue is a plain-text line naming the product, the terminal (where applicable) and the missing
  or inconsistent item.
- MUST: When the project has **no** issues, the section states that none were found (rather than rendering
  empty).
- MUST: **Image-free**; available in a screen and a **printer** variant like the rest of the report.

### AC illustrations

- Over a project where a socket has no light group and no wire colour and one button terminal drives no
  block, the section lists that locality → that socket → *Mangler Lysgruppe*, *Mangler ledningsfarve*, and
  that button → *Er ikke forbundet/linked til noget*; a fully documented lamp in the same locality does not
  appear.

### Constraints

- Verification method — **Demonstration** over a project with known gaps that each gap is reported and
  fully documented elements are omitted.
- The checks read the same documentation fields the product / terminal properties dialogs write (US-011,
  US-012); this section **reports**, it never edits.
- The listed check set is a fixed requirement; the exact per-issue wording / localisation and any checks
  beyond the listed set are not itemised here.

**Readiness:** Ready.

**Implementation status:** 🟡 Implemented in the combined report — a **Fejl i dokumentation** section lists every
missing/blank item (unlinked terminal; missing id-code / light group / cable type / cable number / wire colour /
placement / data-line address) located by locality → product → terminal, listing only elements with an issue and
rendering "Ingen fejl fundet." when the project is clean. Surfaced through the single Reports view (US-040).

---

## US-073 — Cabling and addressing cross-references

**Scope:** In scope — report generation reads the project to produce output; the report's cabling and
data-line addressing cross-reference tables. Distinct from the interactive in-app module map (US-050).

**As an** IHC installer, **I want** the report to include cross-reference tables of the cabling and the
data-line addressing, **so that** I can trace every wire and every occupied module terminal in one place.

### Acceptance criteria (Checklist)

- MUST: A **cabling** table headed **"Kabler"** — one row per **addressed** terminal, **sorted by data-line
  address**, with columns *Ledningsfarve* (wire colour), *Adresse*, *Modul*, *Modul-lokation*, *Lysgruppe*,
  *Id-kode*, *Lokalitet*, *Placering*, *Produkt*, *Ind-/Udgang*. Unaddressed terminals are excluded.
- MUST: Flat data-line cross-references **"Datalinie indgange"** / **"Datalinie udgange"** — all inputs /
  all outputs, **sorted by address**, with columns *Adresse, Produkt, Indgang|Udgang, Note, Lokalitet,
  Placering, Id-kode, Kabeltype, Kabelnummer, Lysgruppe, Ledningsfarve*. A blank field is an empty cell.
- MUST: A **module address map** **"Datalinie input/output-moduler"** — per input and per output module,
  the terminals in use and which product terminal occupies each address, rendered **as a table** (there is
  **no graphical module diagram**).
- MUST: The *Specielle Produkter* (special products, e.g. modems) and *S0 Device* tables where applicable.
- MUST: These are content sections switchable via US-071 and honour the wire-colour, empty-column and
  internal-id options.
- MUST: **Image-free**; available in a screen and a **printer** variant.

### AC illustrations

- A wired lamp output addressed to output module 3, terminal 2, with a brown wire, appears once in *Kabler*
  as a row `brun | 3.02 | Output 230/10 | … | <locality> | <placement> | Lampeudtag | Udgang`, and again in
  *Datalinie udgange* at address `3.02`; an unaddressed terminal appears in neither.

### Constraints

- Verification method — **Demonstration** that each addressed terminal appears once, sorted by address,
  with its wire colour; that unaddressed terminals are excluded from *Kabler*; and that the module map is a
  table, not a diagram.
- The precise column set MAY vary by product family (wired / airlink / RS485); the graphical module
  overview is **excluded** (images out of scope).
- This is the **report's** addressing cross-reference; the interactive, in-app module-map **view** is
  US-050 — the two present the same addressing, one as a printable report section, the other as a live
  read-only view.

**Readiness:** Ready.

**Implementation status:** 🔴 Planned.

---

## US-074 — Printer-friendly report layout and pagination

**Scope:** In scope — the print/paper rendering quality of the reports specified in US-040 (project
documentation report), US-041 (function-block logic), US-072 (documentation-completeness) and US-073
(cabling/addressing cross-references); this story is the print CSS/layout contract those reports' printer
variants must satisfy.

**As an** IHC installer, **I want** every report to print cleanly across page boundaries, **so that** the
paper hand-over documentation stays complete and legible regardless of the reader's printer or browser
print settings.

**Scope excludes:** the reports' field content and section/detail switches (US-040/041/071/072/073); any
app-supplied page header/footer/page-number (explicitly excluded by the US-040 appendix).

### Acceptance criteria (Checklist)

- MUST: Heading and banner text remains legible in **black-and-white** when printed, even when the
  browser's "print background graphics" setting is off — no heading or label is white-on-white or otherwise
  illegible without a coloured background.
- MUST: A table row is never split across a page break; a product's label rows and its terminal sub-table
  stay together on one page wherever they fit within a single page.
- MUST: A heading is never printed as the last line on a page with all of its content pushed to the
  following page.
- MUST: A wide table (e.g. the *Kabler* / *Datalinie indgange* / *Datalinie udgange* cross-reference
  tables) reflows to the printed page width; any horizontal-scroll affordance used on screen has no effect
  in print and never clips a column.
- MUST: A table that spans more than one printed page repeats its column-header row at the top of each
  subsequent page.
- SHOULD: Page margins are set explicitly (not left to the browser default), so the layout is consistent
  across browsers and printers.

### AC illustrations

- The report's "IHC OpenVisual" banner uses white text on a coloured background on screen; printed with
  "print background graphics" off, the heading still prints — in black text with a border — instead of
  disappearing.
- The *Datalinie indgange* cross-reference table, which scrolls horizontally on screen inside a wide
  container, prints at the page's own width with its columns fitted to it, instead of being cut off past
  the right margin.
- The *Fejl i dokumentation* issues table repeats its `Lokalitet / Produkt / Terminal / Fejl` header row at
  the top of each page it spans, instead of showing it only once at the very top of the whole table.

### Constraints

- Verification method — **Demonstration**: print-preview (or print-to-PDF) each report purpose and each of
  US-040/041/072/073's sections and tables, and confirm no clipped column, no split row, no orphaned
  heading, and a legible heading with default browser print settings (background graphics off).
- This story specifies the print CSS/layout contract only; report field content and the section/detail
  switches are specified in US-040/041/071/072/073.

**Readiness:** Ready.

**Implementation status:** 🟡 Partially implemented — `ReportHtmlRenderer`'s print variant already applies a
compact black-on-white stylesheet with `page-break-inside:avoid` on tables; banner-contrast fallback,
heading break-after avoidance, wide-table print reflow and repeating table headers are not yet ported into
the renderer. The full print-safety CSS contract is captured in the
`tests/testdata/reports/std-*/full-*.html` report-format oracles (2026-07-30).

---

## US-049 — View and edit data tables (user-defined texts)

**Scope:** In scope — editing user-defined texts is project-content CRUD; the system tables are read-only
reference data.

**As an** IHC installer, **I want** to open the project's data tables and add, edit or delete my own
user-defined texts, **so that** I can maintain the reusable text strings the installation refers to
without leaving the app.

**Scope excludes:** editing the built-in system tables (they are read-only); how a user-defined text is
*referenced* from elsewhere in the project.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Open the data tables dialog
  Given a project is open
  When I choose "Documentation" > "Data tables"
  Then a dialog opens listing the system tables and a separate list of user-defined texts

Scenario: System tables are read-only
  Given the data tables dialog is open
  When I select an entry in the system-tables list
  Then its rows are shown for reference only and offer no Add / Edit / Delete action

Scenario: Add a user-defined text
  Given the data tables dialog is open with the user-defined-texts list selected
  When I choose "Add", type the text in the edit dialog, and confirm with "OK"
  Then the new text is appended to the user-defined-texts list

Scenario: Edit a user-defined text
  Given a user-defined text is selected
  When I choose "Edit", change the text, and confirm with "OK"
  Then the list shows the updated text

Scenario: Delete a user-defined text without a confirmation prompt
  Given a user-defined text is selected
  When I choose "Delete"
  Then the text is removed immediately with no confirmation dialog
  And the removal cannot be undone from within the dialog
```

### AC illustrations

- With the user-defined-texts list selected, *Add* → typing `By main door` → *OK* appends
  `By main door`; selecting it and *Delete* removes it at once with no "are you sure?" prompt.
- Selecting a system table shows its rows greyed for reference; *Add*/*Edit*/*Delete* are
  unavailable for it.

### Constraints

- Verification method — **Demonstration** of the add/edit/delete flow on the user-defined-texts list
  and **Inspection** that the system tables are read-only.
- Because *Delete* deletes with no confirmation, IHC OpenVisual SHOULD guard the action (e.g. an
  app-level confirm).
- The read-only-system-tables vs editable-user-texts split and the no-confirm *Delete* are fixed
  requirements; the exact set and contents of the system tables are not itemised here.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-050 — View the Wired module address map

**As an** IHC installer, **I want** to open a consolidated list of the Wired input and output
modules and the terminals in use, **so that** I can review the whole installation's module addressing
in one place instead of opening each product.

**Scope excludes:** *assigning* a terminal address (that is per-product, US-012); wireless products
(they carry no module addressing).

### Acceptance criteria (Checklist)

- MUST: A menu action under **Documentation** opens a modules view showing two lists — the Wired
  **input** modules and the Wired **output** modules.
- MUST: Each list shows, per addressed terminal, the module/terminal address and the product
  terminal that occupies it, so an installer can see which addresses are taken.
- MUST: The view is **read-only** — it presents the addressing entered via US-012 and offers no
  editing action.
- SHOULD: The view closes back to the workspace without changing the project.
- MAY: The two lists are visually grouped or labelled as input vs output modules.

### AC illustrations

- After addressing `Push (left)` to data line 1 / input terminal 1 (US-012), opening the modules
  view shows input-module terminal 1 occupied by that pin; an unaddressed product does not appear
  against any terminal.

### Constraints

- Verification method — **Inspection** that the view lists the input and output module addressing and
  mutates nothing.
- The input/output module-map view is a fixed requirement; the precise columns shown are not itemised here.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
