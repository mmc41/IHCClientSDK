---
version: 0.2.0
last-updated: 2026-07-16
status: draft
---

# E9 — Documentation & reporting

> **Current scope:** ◑ **Partly in scope.** Entering project information (US-039), editing
> user‑defined data‑table texts (US-049) and viewing the Wired module address map (US-050) are
> project‑metadata / read‑only CRUD → **✅ in scope**. Report generation (US-040 installation + end‑user;
> US-041 function‑block) reads the project to produce output → **✅ implemented**, output format fully
> specified (see the US-040 appendix).

**Goal:** Let an IHC installer capture project‑ and product‑level documentation and generate
installation and end‑user reports, so the delivered installation is properly documented.

**Scope:** entering project information (*Documentation > Project info*); viewing and editing the
project's data tables — the read‑only system tables and the editable user‑defined texts
(*Documentation > Data tables*); viewing the Wired input/output module address map
(*Documentation*); and the report types under *Documentation > Reports* — installation / technical
(US-040), end‑user / function (US-040), and the function‑block listing (US-041). **Scope excludes:**
the installer‑logo feature (out of scope); the per‑product documentation *fields* (US-011) and the note
text on function‑block inputs (authored in E7), which *feed* these reports.

**Acceptance criteria (epic level):**

- MUST: The installer can enter project/customer/installer information and generate an installation
  report and an end‑user report from the data entered while building the project.
- MUST: The installer can add, edit and delete user‑defined data‑table texts, while the built‑in
  system tables and the Wired module address map are shown read‑only.
- MUST: In the **end‑user report**, products not flagged for end‑user documentation are omitted; the
  **installation report** lists every product and renders un‑filled fields as blank placeholders
  (omission is end‑user‑report‑only; see the US-040 appendix).

**Readiness:** Ready.

---

## US-039 — Enter project information

**Status:** ✅ **In scope** — project‑metadata CRUD (writes project / customer / installer info into
the project).

**As an** IHC installer, **I want** to record project, customer and installer information, **so that**
the reports identify the installation and its parties.

### Acceptance criteria (Given‑When‑Then)

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

- MUST: The dialog carries a **Project** group — *Description*, *Number* and *Programmer* — and a
  **Customer** group — *Name*, *Address*, *City*, *Zip code* and *Country* — with OK/Cancel.
- MUST: It is reachable from the *Documentation* menu.

> **Confirmed 2026‑07‑16 — aligned by construction.** IHC OpenVisual's *Project information* dialog was
> **built to IHC Visual's field set** (traced field‑by‑field to the vendor's report XSLTs when US-039 was
> written), and the comparison found no divergence. Evidence: `RESULTS.md` **F‑044**
> (`S09\80-project-info-ov.png`). ⚠ **The vendor's dialog was not captured live** — it opens only via menu
> navigation the driver could not script — so this is aligned *by construction and by the XSLT trace*,
> not by a side‑by‑side screenshot. Closing the loop means opening the vendor's dialog from its menu and
> diffing field labels and order.

### AC illustrations

- The installation report’s header lists installer and customer information (name, address, telephone)
  drawn from *Project info*.
- *Documentation ▸ Project info…* opens a dialog whose **Project** group holds `Description`, `Number` and
  `Programmer`, and whose **Customer** group holds `Name`, `Address`, `City`, `Zip code` and `Country`.

### Constraints

- A *Project info* dialog exists and precedes report generation. The **reports** render only the
  installer/customer **Navn / Adresse / Telefon** (name / address / phone) — the other project‑info fields
  (city, zip, country, mobile, email, udf) are captured by the dialog but appear in **no** report (see the
  US-040 appendix). This asymmetry is deliberate: the dialog is the project's record, the report is a
  subset of it.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the dialog's field set is confirmed against IHC Visual's
(F‑044); a live vendor capture would close the last of the loop.

---

## US-040 — Generate installation and end‑user reports

**Status:** 🕒 **Deferred** — report generation reads the project to produce output; not project CRUD.

**As an** IHC installer, **I want** to generate the technical installation report and the end‑user
function report, **so that** I can hand over complete documentation.

### Acceptance criteria (Checklist)

- [ ] MUST: *Documentation > Reports* opens the reporting view offering two report types:
  **Installation documentation** (technical) and **Function documentation / End-user documentation**
  (end‑user).
- [ ] MUST: The **installation report** contains installer/customer info; the connected *Wired*
  modules (type, location, description); and, per locality, each component’s Locality, Location,
  Component type, Identification code, Cable no., Cable type, Light group, and (wired only) input/output
  terminal numbers.
- [ ] MUST: Products are documented **in the order they appear** in the *Installation* pane.
- [ ] MUST: The **end‑user report** lists all localities and, per locality, the input functions
  (e.g. buttons), where each input’s text comes from the *Name* + *Location* of the product and the
  *Note* on the function‑block input it drives.
- [ ] MUST: In the **end‑user report**, a product not flagged for end‑user documentation is **omitted**;
  in the **installation report** every product is listed and any un‑filled field renders as a blank
  placeholder (`--`), not suppressed. *(Omission is end‑user‑report‑only; see the output‑format
  appendix.)*
- [ ] SHOULD: Both report types are available in a **printer‑friendly** version (installation report =
  same layout, print stylesheet; end‑user report = a distinct print layout — see appendix).

### AC illustrations

- An end‑user report row "<product> By door" is formed from the product’s *Name* ("<product>") +
  *Location* ("By door"); the sub‑line under "<pin>" comes from the *Note* on
  the function‑block input that button drives — writing that note once propagates it to every physical
  input linked to the block.

### Constraints

- Verification method — **Demonstration** that the two reports render entered data in installation
  order and omit undocumented products.
- **Report content comes from the SDK.** `ihcclient` supplies the render‑ready report model
  (`ProjectAppService.GenerateInstallationReport` / `GenerateEndUserReport`); US-040 covers only the
  model→HTML 1‑to‑1 transform + standard‑browser display (see the appendix "OpenVisual approach"). The
  field / order / omission spec in the appendix is the contract that model satisfies.

**Readiness:** Ready.

- The report output mechanism and layout are itemised in the appendix below; the field / order /
  omission rules there form the specification the SDK report model satisfies. Verification remains
  **Demonstration**.

**Implementation status:** ✅ Implemented.

<!-- BEGIN appendix — report output format (delimited; removable wholesale) -->

### Appendix — report output format

This appendix specifies the exact content and layout of the generated reports over the reference
project `project3-KompleksWired.vis`. Reports are **not a dialog and not an export step**: choosing a
report renders a static HTML page for the whole open project, which the user views and prints in a
standard browser. The rules below (sections, order, columns, blank handling, omission and note
propagation) fully specify each report's output.

> **OpenVisual approach.** Two layers, matching this repo's architecture (business
> logic in the SDK, GUI thin):
> - **SDK enabler — delivered.** `ihcclient` exposes an API returning a render‑ready **report model** with
>   *all* content already computed: the sections in order, each product's resolved field values, the
>   blank→`--` decisions, the end‑user omission filter applied, note propagation resolved, and data‑line
>   addresses decoded (`ProjectAppService.GenerateInstallationReport` / `GenerateEndUserReport`, model types
>   in namespace `Ihc.Vis.Reporting`). **The field / order / omission spec below is the contract that model
>   satisfies.**
> - **This story (US-040).** IHC OpenVisual transforms the report model **1‑to‑1 into HTML** (a mechanical
>   template — no business logic), applies the print CSS variant, and opens it in the user's **default /
>   standard browser** for viewing and printing. The only runtime dependency is a standard browser able to
>   display static HTML.

> **Deliberate divergence (C) — the rendering mechanism, granted by design decision, reaffirmed
> 2026‑07‑16.** IHC Visual generates its reports by copying the whole `.vis` to `%TEMP%` and rendering it
> through **XSLT stylesheets from its install directory into Internet Explorer**. IHC OpenVisual renders
> **in‑app HTML into the standard browser** instead. This is an **intentional exception, not a gap**: the
> vendor's mechanism depends on legacy MSXML behaviour that is **dead in modern Chromium‑based browsers**
> (reproduced offline — the vendor's own XSLTs no longer render there), and on a vendor installation the
> app deliberately does not require (US-063). The **content** is what must match, and it is: the report
> model was traced field‑by‑field to the vendor's XSLTs. Compare **entry points and scope** against the
> vendor, never the rendering technology. Evidence: `RESULTS.md` **F‑044**; the capture that established the
> vendor's mechanism and this decision.

**Output mechanism / view.** *Documentation ▸ Reports* presents a small menu ("Projekt dokumentation")
listing report choices, each in a **screen** and a **printer** variant. Three report types exist:
**Installationsdokumentation** (technical), **Funktionsdokumentation** (end‑user), and a third
**Functionsblok dokumentation** (function‑block listing) — the latter is out of the initial two‑type
scope (US-041); include or drop it explicitly. There is **no on‑screen‑preview vs. direct‑print vs. export
distinction** — every choice renders an HTML page; printing is the browser's own Ctrl+P. No app‑supplied
page header/footer/page‑number/date; the only app page‑break hint is "avoid breaking inside a table".

**Installation report — structure (top→bottom) and columns.**
1. Title banner + heading "Installationsdokumentation".
2. **Installer** masthead table, then **Customer** masthead table — each three rows
   `Navn / Adresse / Telefon` ← `installer_info|customer_info @name / @address / @phone`. (The other
   project‑info fields — city, zip, country, mobile, email, udf — are **not** shown.) Blank → `--`.
3. **Per‑product detail tables**, one per product, **in Installation‑pane order** (MUST). Label→value rows;
   `product_dataline` shows `Lokalitet, Placering, Komponent, Identifikationskode, Kabelnummer, Kabeltype,
   Lysgruppe` then a terminal sub‑table `Terminal | Adresse | Ledning` (one row per input/output; *Adresse*
   = `Indgang`/`Udgang` + decoded data‑line address, unassigned → `?`; *Ledning* = wire colour). Airlink,
   RS485‑LED‑dimmer and RS485‑modem products use reduced field sets (serial number / cable‑colour rows).
   Blank → `--`.
4. **Flat cross‑reference tables**: *Datalinie indgange* and *Datalinie udgange* (all inputs / outputs,
   sorted by address) with columns `Adresse, Produkt, Indgang|Udgang, Note, Lokalitet, Placering, Id‑kode,
   Kabeltype, Kabelnummer, Lysgruppe, Ledningsfarve`; then *Datalinie input/output‑moduler*, *Specielle
   Produkter* (RS485 modems) and *S0 Device* tables. In these flat tables a blank field is an **empty
   cell** (not `--`).
5. **Omission:** the installation report lists **every** product; undocumented fields appear blank (`--` /
   empty). It does **not** drop undocumented products.
6. **Printer variant:** identical HTML, print stylesheet only → black text, compact `xx‑small`, gridded
   borders, tables kept whole across page breaks.

**End‑user report — structure and note propagation.**
1. Title + heading "Funktionsdokumentation"; **[screen only]** a locality table‑of‑contents (anchor links).
2. **Every locality** renders as a section header (empty localities included — localities are never
   omitted), in Installation‑pane order.
3. Under each locality, only products **flagged for end‑user documentation** are shown (this is the
   omission MUST — end‑user‑report‑only). Each shows the product image, **Name + Location**, then per input
   terminal a bullet line `• <terminal name>` and, **per link on that terminal**, a sub‑line
   `- <Note of the function‑block input it drives>`. The note lives on the FB input and is reached through
   the link, so **one note propagates to every physical terminal linked to the block**, and a terminal with
   several links repeats the note once per link. **[screen only]** when the driving FB sits in a different
   locality than the product, the note sub‑line is suffixed `(<that locality>)`.
4. **Printer variant** = a **separate layout**: drops the table‑of‑contents and the differing‑locality
   suffix, and switches product/terminal/note text to black compact styles.

<!-- END appendix -->


---

## US-041 — Generate the function‑block documentation report

**Status:** 🕒 **Deferred** — report generation reads the project to produce output; not project CRUD.
Identified as a **third** report type beyond the initial US-040 scope.

**As an** IHC installer, **I want** to generate a report that lists the project's function blocks and
their internals, **so that** I can hand over documentation of the control logic alongside the
installation and end‑user reports.

**Scope excludes:** the installation and end‑user reports (US-040); the per‑field internal layout of the
function‑block report, which is to be specified in detail when this story is implemented
(see Constraints).

### Acceptance criteria (Checklist)

- [ ] MUST: *Documentation ▸ Reports* offers a **Function‑block documentation** choice
  (heading *"Functionsblok dokumentation"*) alongside the installation and end‑user reports, in
  both a screen and a **printer** variant.
- [ ] MUST: Produced from the SDK report‑model enabler and transformed **1‑to‑1 to HTML**, shown in the
  standard browser for viewing/printing — same two‑layer mechanism as US-040 (see the
  US-040 appendix "OpenVisual approach").
- [ ] MUST: The report renders the project's function blocks, assembled like the installation report —
  a title banner, an `<h2>Functionsblok dokumentation</h2>` heading, then the transformed block content.
- [ ] MUST: Blocks are documented **in Installation/Functions‑pane order** (document order — no re‑sort
  is applied), consistent with US-040's ordering rule.
- [ ] SHOULD: The **printer** variant is the same layout with the print stylesheet only (black text,
  compact `xx‑small`, gridded borders, tables kept whole across page breaks) — a CSS swap, like the
  installation report and unlike the end‑user report's separate print layout.

### AC illustrations

- Over `project3-KompleksWired.vis` the report lists each function block (e.g. the Kip and PIR blocks)
  with its internals.

### Constraints

- Verification method — **Demonstration** that the report renders the project's function blocks in
  document order, screen and print variants.
- Note: this third report type is **not yet deep‑specced** — the detailed section/column layout of the
  function‑block report is to be itemised when this story is implemented. (Open item.)

**Readiness:** Not Ready — the per‑field internal table layout is not yet itemised and must be specified
before implementation (the mechanism, menu placement, ordering and print‑variant behaviour are settled;
the internal table layout is not).

**Implementation status:** ✅ Implemented (minimal listing; deep per-field layout deferred).

---

## US-049 — View and edit data tables (user‑defined texts)

**Status:** ✅ **In scope** — editing user‑defined texts is project‑content CRUD; the system tables are
read‑only reference data.

**As an** IHC installer, **I want** to open the project's data tables and add, edit or delete my own
user‑defined texts, **so that** I can maintain the reusable text strings the installation refers to
without leaving the app.

**Scope excludes:** editing the built‑in system tables (they are read‑only); how a user‑defined text is
*referenced* from elsewhere in the project.

### Acceptance criteria (Given‑When‑Then)

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

- With the user‑defined‑texts list selected, *Add* → typing `By main door` → *OK* appends
  `By main door`; selecting it and *Delete* removes it at once with no "are you sure?" prompt.
- Selecting a system table shows its rows greyed for reference; *Add*/*Edit*/*Delete* are
  unavailable for it.

### Constraints

- Verification method — **Demonstration** of the add/edit/delete flow on the user‑defined‑texts list
  and **Inspection** that the system tables are read‑only.
- Because *Delete* deletes with no confirmation, IHC OpenVisual SHOULD guard the action (e.g. an
  app‑level confirm). (R‑note.)
- Note: the read‑only‑system‑tables vs editable‑user‑texts split and the no‑confirm *Delete* are fixed
  requirements; the exact set and contents of the system tables are not itemised here and are to be
  confirmed during implementation. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ Implemented.

---

## US-050 — View the Wired module address map

**As an** IHC installer, **I want** to open a consolidated list of the Wired input and output
modules and the terminals in use, **so that** I can review the whole installation's module addressing
in one place instead of opening each product.

**Scope excludes:** *assigning* a terminal address (that is per‑product, US-012); wireless products
(they carry no module addressing).

### Acceptance criteria (Checklist)

- [ ] MUST: A menu action under **Documentation** opens a modules view showing two lists — the Wired
  **input** modules and the Wired **output** modules.
- [ ] MUST: Each list shows, per addressed terminal, the module/terminal address and the product
  terminal that occupies it, so an installer can see which addresses are taken.
- [ ] MUST: The view is **read‑only** — it presents the addressing entered via US-012 and offers no
  editing action.
- [ ] SHOULD: The view closes back to the workspace without changing the project.
- [ ] MAY: The two lists are visually grouped or labelled as input vs output modules.

### AC illustrations

- After addressing `Push (left)` to data line 1 / input terminal 1 (US-012), opening the modules
  view shows input‑module terminal 1 occupied by that pin; an unaddressed product does not appear
  against any terminal.

### Constraints

- Verification method — **Inspection** that the view lists the input and output module addressing and
  mutates nothing.
- Note: the input/output module‑map view (*Documentation* module list) is a fixed requirement; the
  precise columns shown are to be confirmed during implementation. (R‑note.)

**Implementation status:** ✅ Implemented. Epic E9 complete.

**Readiness:** Ready.

---
