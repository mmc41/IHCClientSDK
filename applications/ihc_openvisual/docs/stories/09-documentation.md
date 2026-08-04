---
version: 0.4.0
last-updated: 2026-08-02
status: draft
---

# E9 — Documentation & reporting

> **Scope:** Partly in scope. Entering project information (US-039) and viewing the data-line modules
> (US-050) are project-metadata / read-only CRUD; the data tables (US-049) are application state shared
> across projects.
> Report generation reads the project to produce a finished document: **three documentation reports** —
> Funktionsdokumentation (end-user functions), Installationsdokumentation (installer) and Functionsblok
> dokumentation (function-block logic) — each in a **Standard** or **Fuld** mode and as **HTML** or
> **plain text** (US-040), with the installation content specified in US-073, the function-block content
> in US-041, and the Fuld-mode "Fejl i dokumentation" section fed by the project verification checks
> (US-072). Report output carries **no images apart from the app's icon language**: product identity,
> wire colours and module addressing render as text/tables; the function-block report renders its logic
> tree with the same icon set the app uses (as inline vector glyphs in HTML, unicode stand-ins in text).

**Goal:** Let an IHC installer capture project- and product-level documentation and generate the three
project reports — end-user functions, installation, and function-block logic — so the delivered
installation is fully and consistently documented for each reader.

**Scope:** entering project information (*Documentation ▸ Project info*); viewing and editing the
project's data tables — the read-only system tables and the editable user-defined texts
(*Documentation ▸ Data tables*); viewing the input/output data-line modules (*Dokumentation*);
and generating the three documentation reports from the *Dokumentation* menu through one shared picker
(US-040) — the report content per type in US-073 (installation) and US-041 (function blocks), and the
Fuld-mode documentation-issues section in US-072. **Scope excludes:** any report option beyond
type × mode × format (the former section/detail switches and purpose presets are retired — US-071); any
navigation apparatus in the output (no table of contents, anchors or back-to-top); pictures, diagrams or
logos beyond the icon glyphs; the per-product documentation *fields* (US-011) and the note text on
function-block inputs (authored in E7), which *feed* these reports.

**Acceptance criteria (epic level):**

- MUST: The installer can enter project / customer / installer information and generate each of the
  three documentation reports from the data entered while building the project.
- MUST: Every report generates in six user-selectable combinations — three types × Standard/Fuld — and
  in both output formats, where **Fuld** is the Standard content plus additions only (generation
  timestamp + programmer line, the Projekt identity block, inline `(ID …)` element ids at definition
  sites, the "Fejl i dokumentation" section, and the installation-only terminal-connections table).
- MUST: The Fuld-mode **"Fejl i dokumentation"** section lists, per locality → product → terminal, what
  documentation is missing or inconsistent, fed by the project verification checks (US-072), so the
  installer gets a punch-list.
- MUST: The **Funktionsdokumentation** report lists only products flagged for end-user documentation;
  the **Installationsdokumentation** report lists every product, with un-filled fields rendered as
  `--` placeholders in its masthead/per-locality blocks and as blank cells in its flat tables.
- MUST: The installer can add, edit and delete the user-defined texts of any of the eighteen data tables
  (application state, shared across projects), while the data-line modules are shown read-only.
- MUST: Report output carries no images apart from the icon glyphs: no product photos, no graphical
  module diagrams, no installer logo image, no external manual/help pictures — module addressing and
  wiring are tables.

**Readiness:** Ready — the per-report content is specified in US-073/US-041, the generate/view/save flow
in US-040, and the issues section in US-072; the committed report oracles pin the exact output.

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
- MUST: All sixteen contact fields are **editable drop-downs**, each offering its data table (US-049) —
  *Firma* behind the installer's *Name*, *Kunder* behind the customer's, and one shared table behind
  each of the other seven, which is why the vendor offers the same street/phone/postal-code/city/
  country/email/mobile list on both sides. A value typed here joins that table.
- MUST: The *Description* caption carries **no trailing colon**, alone among the dialog's captions —
  mirrored from the vendor, whose own dialog is inconsistent here.
- MUST: **Editing project info never erases stored project-information values.** Every
  project-information attribute the file carries survives an edit round-trip — including any the
  dialog does not show. (A field that is shown must be written back as edited; a value the file
  carries but the dialog does not surface must be carried through unchanged, not dropped when the
  dialog is committed.)
- MUST: It is reachable from the *Dokumentation* menu.

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

- The **installation report** renders the installer/customer **Navn / Adresse / Telefon** (name /
  address / phone) in its masthead blocks, and Fuld-mode reports render the **Projekt** identity
  (description, number, programmer). The other project-info fields (city, zip, country, mobile, email,
  udf) are captured by the dialog but appear in **no** report section (US-073/US-040). This asymmetry is
  deliberate: the dialog is the project's record, the report is a subset of it.
- Fuld-mode reports also carry a **generation timestamp** beside the programmer, so a printed copy is
  dated (US-040).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (the Fuld-mode generation-timestamp line is specified in US-040).

---

## US-040 — Generate a documentation report (type × mode × format)

**Scope:** In scope — report generation reads the open project to produce a finished document. The three
report types' content is specified per type: the functions content below, the installation content in
US-073, the function-block content in US-041; the Fuld-mode issues section is US-072.

**As an** IHC installer, **I want** to pick one of the three documentation reports, a Standard or Fuld
mode and an output format, and view or save the generated document, **so that** I can hand each reader —
end user or installer — exactly the documentation they need without maintaining report options.

**Scope excludes:** any report option beyond type × mode × format (US-071 is retired); navigation
apparatus in the output (no table of contents, anchors or back-to-top in either mode).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: The Documentation menu lists the three reports
  Given a project is open
  When I open the "Documentation" menu
  Then it lists Funktionsdokumentation, Installationsdokumentation and Functionsblok dokumentation as separate entries

Scenario: A report entry opens the shared picker pre-selected
  Given a project is open
  When I choose one of the three report entries
  Then the one shared report picker opens with that report pre-selected in its type dropdown
  And the picker offers a Standard/Fuld mode choice, an output-format dropdown listing HTML and TXT
  And the actions "Vis" and "Gem som…"

Scenario: HTML is the default output format
  Given a project is open
  When I choose one of the three report entries
  Then the format dropdown is pre-selected on HTML

Scenario: View and print the generated report
  Given the report picker is open
  When I choose "Vis"
  Then the picked report generates in the picked format — a self-contained HTML page, or a plain-text
    document when TXT is picked — and opens in the system's default application for that format
  And printing is that application's own print function (US-063)

Scenario: Save as a file in the picked format
  Given the report picker is open
  When I choose "Gem som…"
  Then the save dialog suggests a file name in the picked format (.html or .txt)
  And the picked report generates to the chosen file in that format

Scenario: No project open
  Given no project is open
  When I open the "Documentation" menu
  Then the three report entries are disabled

Scenario: A generation or save failure is reported
  Given the report picker is open
  When generating or writing the picked report fails
  Then the standard error dialog reports the failure and the app stays responsive
```

### Business rules (modes and formats)

- MUST: **Standard** mode is the report's standard information scope; **Fuld** mode is Standard plus
  additions only — a `Fuld rapport — Genereret: <timestamp> — Programmør: <name>` line under the title,
  the **Projekt** identity block (description / number / programmer), inline `(ID …)` element ids where
  an element is defined, the **"Fejl i dokumentation"** section (US-072), and — installation report
  only — the **Terminal-forbindelser** table (US-073).
- MUST: The **Funktionsdokumentation** content lists every locality in Installation-pane order; under a
  locality, the products flagged for end-user documentation with their *Name* + *Placement*; per product
  its terminals (inputs before outputs); and per linked terminal the *Note* of the function-block input
  it drives — one note written on the block propagates to every linked terminal, and a terminal with
  several links shows one note line per link. In Fuld mode a note line whose function block sits in a
  locality with a different name is suffixed `(<that locality>)`.
- MUST: Both formats convey the same content: the HTML page is self-contained (styles and icon glyphs
  inline, screen and print variants in one page); the plain-text file renders the same structure with
  unicode icon stand-ins and aligned columns.
- MUST: The generation timestamp shown in Fuld mode is the generation time; Standard output carries no
  timestamp.
- MUST: The output format is the installer's explicit choice in the picker — HTML by default, TXT the
  alternative — and it governs both actions and the suggested save file name; the format is never inferred
  from a typed file name.
- MUST: The view action's label names no specific viewer ("Vis", not "Vis i browser"): which application
  opens the generated document depends on the picked format and on the system's file associations.

### AC illustrations

- Choosing *Documentation ▸ Installationsdokumentation…*, mode **Fuld**, format **TXT**, then "Gem som…"
  writes the plain-text installation report including the Projekt block, `(ID …)` ids and the
  "Fejl i dokumentation" section; the same picker choice with format **HTML** produces the identical
  content as an HTML page.
- An end-user row "Tryk (venstre)" under a button product shows the note of the block input it drives —
  e.g. "Kort tryk < 1 sek. Tænd / sluk: Loftlampe i stue" — once per link on that terminal.

### Constraints

- Verification method — the committed report oracles (`tests/testdata/reports/`, 24 files: 3 types × 2
  modes × 2 formats × 2 reference projects) are the executable output contract; generating each
  combination reproduces its oracle byte-for-byte.
- Report generation, content and formatting live in the engine; the app only offers the picker, hands
  over type × mode × format, and shows or saves the returned document.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the three Documentation-menu entries open the shared picker
pre-selected, with an HTML/TXT format dropdown defaulting to HTML; view-in-browser and save-as generate
the picked format through the engine for all 3 × 2 × 2 combinations; the 24 report oracles regenerate
byte-identically in the test suites.

---

## US-041 — The function-block report content (Functionsblok dokumentation)

**Scope:** In scope — the content specification of the **Functionsblok dokumentation** report type
generated via US-040: a deep, per-block logic listing.

**As an** IHC installer, **I want** the function-block report to document each block in full — its
purpose, its variables with values, and its programmed logic as an icon tree — **so that** I can hand
over and review the control logic alongside the installation and end-user documentation.

**Scope excludes:** the generate/view/save flow (US-040); the installation and functions content
(US-073 / US-040).

### Acceptance criteria (Checklist)

- MUST: Blocks are documented per locality in Installation-pane order, each as its own section: the
  block's **name** as the heading, then — when the block carries a description — the fixed
  **"Anvendelse"** label and the description's lines (a repeated first line equal to the block name is
  not shown; trailing library boiler-plate lines render as small print in HTML).
- MUST: Each block renders its four variable sections and its programs per the layout appendix below,
  as an indented tree whose rows carry the app's **icon language** — vector glyphs in HTML, the unicode
  stand-ins in plain text.
- MUST: Variable rows show `= value` **only** under *Indstillinger* and *Interne variable*, with
  per-type value formats (timer `HH:MM:SS,mmm`, time of day `HH:MM:SS`, date as day + real month name,
  weekday in Danish, on/off as `On`/`Off`, temperature with ` C`, light level with `%`, enum by its
  value name); inputs/outputs show the pin's note text instead.
- MUST: Statement rows (events, conditions, commands) render their text with the referenced variable
  names substituted into the stored statement template.
- MUST: In **Fuld** mode each block section additionally carries its `(ID …)` chip on the heading and an
  identity grid (Lokalitet / Type / Version / Låst).
- MUST: A block with no description renders its heading directly; a block with no programmed logic
  still lists its (empty) sections and its empty program skeleton.

### AC illustrations

- Over the reference projects, the *Doku zoo* block renders `⧖ Timer = 00:03:00,000` under
  Indstillinger, `→ Kip  Tænd/sluk af stuelys` under Input, and its Case program as nested
  `↳◆ Case (Tilstand)` / `✓✓ Case Tilstand = Tilstand A` rows; the unprogrammed *Tom blok* renders its
  empty sections and program skeleton.

### Constraints

- Verification method — the committed function-block report oracles pin the exact output byte-for-byte
  in both formats and modes.

**Readiness:** Ready — the per-field layout is itemised in the appendix below.

**Implementation status:** ✅ Implemented — the engine builds the block sections (heading/description
rules, vendor-scope variable sections with per-type value formats, statement substitution, program-tree
nesting) and both format writers render them; pinned by the four function-block report oracles per
reference project.

<!-- BEGIN appendix — function-block layout (delimited; removable wholesale) -->

### Appendix — function-block layout

Per function block, in Installation-pane document order, the report renders the following as an
icon-tree outline (element `(ID …)` chips appear on the block heading in Fuld mode only):

1. **Block heading** — the block's name; in Fuld mode followed by the identity grid
   (Lokalitet / Type / Version / Låst).
2. **Anvendelse** — the block's descriptive purpose text, line by line.
3. **Input** — one row per input pin: icon, name, and its **note / behaviour text**.
4. **Output** — one row per output pin or scene: icon, name, and its note text.
5. **Indstillinger** (settings) — each variable as icon, `name = value` (and its note when present).
6. **Interne variable** (internal variables) — each variable as icon, `name = value`.
7. **Programmer** (programs) — each program as an indented icon tree:
   - **Hændelser** (events) — the events that start the program (e.g. `<input> -> ON`).
   - **Kommandoer** (commands) — the actions, including nested **Under program** groups with their
     **Betingelser** (and/or condition groups) and their true/false command groups, case groups with
     their case values, and scene invocations (`Fremkald <scene>`).
8. **Empty block** — a block with no programmed logic renders its heading, its empty sections and the
   empty program skeleton.

<!-- END appendix -->

---

## US-071 — Tailor the report with section/detail switches (RETIRED)

**Status: Retired (2026-08-02).** This story's content-section and detail-option switches, and the
purpose presets built from them, were removed from the product. A report is now fully specified by
**type × mode × format** alone (US-040): the three report types replace the sections-as-switches model,
the Fuld mode replaces the detail options and the "Full" preset, and the committed report oracles pin
the exact output of every combination. The completeness content this story could toggle lives on as the
Fuld-mode "Fejl i dokumentation" section (US-072). No switch or preset behaviour described here is
current product behaviour.

**Readiness:** Retired — no open work.

**Implementation status:** ✅ Retired — the switch/preset surface was removed with the reporting
redesign (2026-08-02).

---

## US-072 — Documentation-issues section fed by project verification

**Scope:** In scope — a read-only section of every Fuld-mode report that reports documentation gaps; it
never edits the project. The checks themselves belong to the project verification capability, which is
also callable on its own (without generating a report).

**As an** IHC installer, **I want** every Fuld-mode report to end with a list of what documentation is
missing or inconsistent across the project, **so that** I get a punch-list of everything to finish
before hand-over.

### Acceptance criteria (Checklist)

- MUST: Each Fuld-mode report's final section is headed **"Fejl i dokumentation"**: a table with the
  columns *Lokalitet / Produkt / Terminal / Fejl*, one row per documentation finding, in project scan
  order (a product's own findings before its terminals'). Cells that do not apply — the Terminal cell of
  a product-level finding — stay blank. Standard mode carries no such section.
- MUST: The section is fed by the project **verification checks** (documentation category), whose seed
  set MUST cover, per product / terminal: terminal **not linked**; missing **identification code**;
  missing **light group**; missing **cable type**; missing **cable number**; missing **wire colour**;
  missing **placement**; missing or undecodable **data-line address**.
- MUST: Each finding renders its fixed Danish label (e.g. *Mangler Id-kode*, *Ikke forbundet*,
  *Mangler Ledningsfarve*, *Mangler Adresse*); fully documented products and terminals produce no rows.
- MUST: The section lists the whole project's findings in every report type — the same rows in the
  functions, installation and function-block reports of the same project.
- MUST: Documentation findings are advisory: they never block saving the project or affect its validity.
- SHOULD: A clean project renders the section with its header and no rows.

### AC illustrations

- Over a project where a button product lacks an identification code and its lower-right terminal is
  unlinked, uncoloured and unaddressed, the section lists `<locality> / <product> / / Mangler Id-kode`
  followed by three rows for that terminal — *Ikke forbundet*, *Mangler Ledningsfarve*,
  *Mangler Adresse* — and a fully documented lamp in the same locality contributes no rows.

### Constraints

- Verification method — the committed Fuld-mode report oracles pin the section's exact rows over the
  reference projects; the verification checks are additionally covered by their own engine tests,
  independent of reporting.
- The checks read the same documentation fields the product / terminal properties dialogs write (US-011,
  US-012); this section **reports**, it never edits.
- The seed check set is a fixed requirement; checks beyond it may be added to the verification
  capability over time and then appear in this section without a report-side change.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the engine's categorized verification supplies the
documentation findings (the 8 seed checks, advisory warnings that never affect validity or saving), and
every Fuld-mode report renders them as the final "Fejl i dokumentation" table; pinned by the six full-*
report oracles.

---

## US-073 — The installation report content (Installationsdokumentation)

**Scope:** In scope — the content specification of the **Installationsdokumentation** report type
generated via US-040. Distinct from the interactive in-app module map (US-050).

**As an** IHC installer, **I want** the installation report to cover the mastheads, the data-line
modules, every product's wiring detail and the flat addressing cross-references, **so that** I can trace
every wire and every occupied module terminal in one printable document.

### Acceptance criteria (Checklist)

- MUST: **Mastheads** — *Installatør* and *Kunde* blocks with `Navn / Adresse / Telefon`; a blank value
  renders `--`.
- MUST: **Module tables** — *Datalinie inputmoduler* and *Datalinie outputmoduler*, sorted numerically
  by data-line number, with columns *Datalinie, Modul type, Lokalitet, Beskrivelse*; blanks render `--`;
  an empty table still renders its headers.
- MUST: **Per-locality component blocks** under *"Lokaliteter og komponenter"* — every product in
  Installation-pane order with its family's field set (wired: identification code, cable number, cable
  type, light group; wireless: identification code, serial number, light group; LED dimmer: serial
  number; RS485 modem: identification code and the four wire colours), a wired product additionally
  carrying its terminal sub-table (*Terminal / Adresse / Ledning*, document order, nested terminals
  included). Modem blocks list after all other products. Blanks render `--`.
- MUST: **Flat cross-reference tables** under *"Datalinjer"* — *Datalinie indgange* / *Datalinie
  udgange* (all inputs / all outputs, unaddressed rows first then sorted numerically by address, columns
  *Adresse, Produkt, Indgang|Udgang, Note, Lokalitet, Placering, Id-kode, Kabeltype, Kabelnummer,
  Lysgruppe, Ledningsfarve*), plus *Specielle Produkter* (RS485 modems) and *S0 Device*. A blank field
  is an empty cell; an unaddressed or undecodable address renders `?`.
- MUST: A data-line address displays as `module . position`, decoded from the stored address with the
  input/output terminal-per-module division; an unaddressed terminal shows `?`.
- MUST: In **Fuld** mode the report additionally carries the **Terminal-forbindelser** table before the
  issues section — one row per linked wired terminal (*Produkt, Terminal, Forbindelse, Funktion*), the
  connection rendered as `-> <block input> -> <function block> -> <its locality>` with the input's note
  as *Funktion* — and `(ID …)` ids at each element's defining row.
- MUST: Module addressing and wiring render as **tables** (no graphical module diagram).

### AC illustrations

- A wired lamp output addressed to output module 1, terminal 3, with a brown wire appears in its
  product's block as `Udgang | Udgang 1 . 03 | Brun` and again in *Datalinie udgange* at address
  `1 . 03`; an unaddressed button terminal appears in its product block and cross-reference row with
  address `?`.

### Constraints

- Verification method — the committed installation report oracles pin the exact tables, sorting, blank
  conventions and Fuld additions byte-for-byte over the reference projects.
- This is the **report's** addressing cross-reference; the interactive, in-app module-map **view** is
  US-050 — the two present the same addressing, one as a printable report section, the other as a live
  read-only view.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the engine builds the full installation content (mastheads,
module tables, per-locality component blocks with terminal sub-tables, flat cross-references, special
products and S0 devices, Fuld terminal-connections) and both format writers render it; pinned by the
four installation report oracles per reference project.

---

## US-074 — Printer-friendly report layout and pagination

**Scope:** In scope — the print/paper rendering quality of the reports specified in US-040 (project
documentation report), US-041 (function-block logic), US-072 (documentation-completeness) and US-073
(cabling/addressing cross-references); this story is the print CSS/layout contract those reports' printer
variants must satisfy.

**As an** IHC installer, **I want** every report to print cleanly across page boundaries, **so that** the
paper hand-over documentation stays complete and legible regardless of the reader's printer or browser
print settings.

**Scope excludes:** the reports' field content (US-040/041/072/073); any app-supplied page
header/footer/page-number.

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

- Verification method — **Demonstration**: print-preview (or print-to-PDF) each report type and mode and
  confirm no clipped column, no split row, no orphaned heading, and a legible heading with default
  browser print settings (background graphics off).
- This story specifies the print CSS/layout contract only; report field content is specified in
  US-040/041/072/073.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — every generated HTML report embeds the one shared
screen+print stylesheet: explicit page margins, banner print fallback (black text with a border when
backgrounds are off), heading break-after avoidance, row/table break-inside avoidance, repeating table
headers, and wide-table print reflow. Pinned byte-for-byte by the twelve `.html` report oracles.

---

## US-049 — View and edit the data tables (user-defined texts)

**Scope:** In scope — maintaining the installer's reusable documentation texts. These are **application**
state, not project content: they are shared across every project the installer opens.

**As an** IHC installer, **I want** to maintain my own reusable texts per data table, **so that** the
documentation fields I fill in over and over offer what I typed last time instead of making me retype it.

**Scope excludes:** how a text is *referenced* from elsewhere (the offering side is US-039's contact
fields and the per-product documentation fields, US-011/US-012).

### Business rules (the table set)

- MUST: The dialog lists **eighteen** named tables, in this order — *Kunder, Firma, Mobil telefonnumre,
  Telefon numre, email adresser, Vejnavne, By, Post numre, Land, Ledningsfarver, Kabelnummer, Kabeltyper,
  Produkt position, Note tekster, Lysgrupper, Projekt typer, Datalinie modul lokationer, Produkt
  identifikationskoder*. This is IHC Visual's own set and order, declared in its
  `Data\userEditableTables.txttables` manifest as `|caption|backing-file|` rows.
- MUST: The tables are **application state, shared across projects** — never written into the `.vis`.
  Measured on the vendor: the values its dialog listed under *Kunder* appear nowhere in the open
  project's file, and several were entered while entirely different projects were open.
- MUST: A text typed into a documentation field joins that field's table, so the next project offers it.
  This is how the tables fill up — the vendor's *Kunder* rows were all typed into *Projektinfo*, not into
  this editor.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Open the data tables dialog
  Given a project is open
  When I choose "Dokumentation" > "Rediger data tabeller…"
  Then a dialog opens with the eighteen tables on the left and the first table selected
  And the right list shows that table's user-defined texts

Scenario: The texts follow the selected table
  Given the data tables dialog is open
  When I select a different table
  Then the right list shows that table's texts, not the previous table's

Scenario: Edit and Delete need a selected text
  Given a table is selected and no text row is picked
  Then "Rediger" and "Slet" are unavailable, and "Tilføj" is available

Scenario: Add a user-defined text
  When I choose "Tilføj", type the text, and confirm with "OK"
  Then the new text is appended to the selected table's list

Scenario: Edit a user-defined text
  Given a text is selected
  When I choose "Rediger", change it, and confirm with "OK"
  Then the list shows the updated text

Scenario: Delete a user-defined text without a confirmation prompt
  Given a text is selected
  When I choose "Slet"
  Then the text is removed immediately with no confirmation dialog

Scenario: OK commits, Annuller discards
  Given I have added, edited or deleted texts
  When I choose "OK"
  Then the changes are saved and are there the next time the app runs
  But when I choose "Annuller" instead, none of them are kept
```

### AC illustrations

- With *Kunder* selected, *Tilføj* → typing `Kunde Bo Bæk` → *OK* appends it; selecting *Vejnavne*
  shows an empty list, and selecting *Kunder* again shows it still there.
- After committing that text with *OK*, the customer *Navn* field in *Projektinfo* offers `Kunde Bo Bæk`
  in its drop-down.

### Constraints

- Verification method — **Demonstration** of the add/edit/delete flow against a selected table, and
  **Inspection** that the committed texts outlive the dialog and the project.
- *Slet* needs no confirmation prompt (the vendor asks for none) because the deletion lives in a working
  copy until *OK* — *Annuller* is the undo.
- The eighteen captions and their order are a fixed requirement; the texts in them are the installer's.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — `DataTableStore` (app-data JSON, beside the installer
identity) behind a dialog matching the vendor's two-list shape, with the committed texts feeding the
US-039 contact fields.

> **Superseded model (2026-08-04).** This story previously described a *project*-scoped feature: the left
> pane listed the open project's `enum_definition`s as read-only "system tables", and the right pane the
> values of an enum named `User-defined texts`. Comparison against the vendor showed both halves were
> wrong — its dialog does not show function-block enum types at all, and no `.vis` in the corpus contains
> a `User-defined texts` enum, so that pane could never be anything but empty. The feature was
> unreachable, not merely differently shaped.

---

## US-050 — View the data-line modules

**As an** IHC installer, **I want** to open a consolidated list of the input and output data lines and
the module documented on each, **so that** I can review which modules the installation has, where they
sit and which data lines are still free, in one place instead of opening each product.

**Scope excludes:** *assigning* a terminal address (that is per-product, US-012); wireless products
(they carry no module addressing).

### Acceptance criteria (Checklist)

- MUST: A menu action under **Dokumentation** opens a modules view showing two groups — the
  **input** data-line modules and the **output** data-line modules, inputs first.
- MUST: Each group lists **every** data line the direction has — not only the documented ones — so the
  installer sees which lines are still free. The number of lines per direction is the addressing
  model's own (US-012), not a separate figure this view defines.
- MUST: Each row shows the line's *data line number*, *module type*, *locality* and *description*, in
  that order — the four attributes the project file records per module, and the same four the
  installation report's module tables use (US-073), so view and report agree.
- MUST: A data line carrying no documented module is shown as **not in use**, rather than as a blank
  row or omitted.
- MUST: The view is **read-only** — it presents what the project records and offers no editing action.
- SHOULD: The view closes back to the workspace without changing the project, dismissed by a single
  acknowledging button.

### AC illustrations

- A project documenting an `Input 24/3` module on input data line 1, sited `I sidetavle` and described
  `Sensorer, lavt forbrug`, shows exactly that on the first input row; input line 3, with nothing
  documented on it, is still listed and reads *not in use*.
- Modules recorded in the file in creation order 2, 1, 8 appear on lines 1, 2 and 8 — the view is
  ordered by data line, not by the order the modules were entered.

### Constraints

- Verification method — **Inspection** that the view lists both directions' data lines with their
  documented modules and mutates nothing.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.
