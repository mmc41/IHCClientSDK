# IHC OpenVisual

> A modern, cross-platform, open-source desktop application called IHC OpenVisual for creating and editing IHC
> home-automation project files (`.vis`) — reading and writing the `.vis` project format used by IHC
> controllers with byte-exact fidelity.

## Vision and Purpose

IHC OpenVisual exists to allow owners and installers of IHC installations to keep
maintaining them for the long term — on any modern desktop OS, in Danish, using an open codebase.

## Key Features

| Feature | Benefit |
| --------- | --------- |
| Binary-compatible open/save of `.vis` projects | Existing project files open and re-save with zero risk of corruption — byte-identical round-trips of the `.vis` format. |
| Full project editing: localities, products, function blocks, links | The complete authoring workflow — model rooms, place wired/wireless devices, add logic blocks, wire inputs to outputs — in one two-pane workspace: **what is installed on the left, what it does on the right**, linked across the middle. |
| Function-block programming | Author control logic (typed variables, events, conditions, commands, enums, case structures) so installations do exactly what the household needs. |
| Built-in component catalog | The stock products and function blocks are embedded, so nothing else needs to be installed to create or extend a project. |
| Modern flat-line SVG icon language + help | A themeable (light/dark), accessible UI that new users can actually read: purpose-designed glyphs plus context-sensitive help in the application's own language. |

## Architecture Overview

IHC OpenVisual is a cross-platform desktop front-end over a shared project engine; all `.vis` parsing,
editing, validation, catalog, and controller communication live in that engine, and the UI never
hand-rolls the file format.

**Deployment model:** a locally installed desktop application for Windows, macOS, and Linux.
**Key integrations:** `.vis` project files (the byte-exact format contract); optional
live IHC controller access for project transfer.

## Key Differentiators

IHC OpenVisual is an open-source, cross-platform editor for IHC `.vis` project files with byte-exact
format fidelity, enabling installers and homeowners to maintain their installations on any modern
desktop OS.

| Differentiator | What It Delivers |
| --------------- | ---------------- |
| Binary compatibility | Generic XML editors break the format; IHC OpenVisual reproduces unchanged `.vis` files byte-for-byte and stamps save metadata exactly as the format requires. |
| Cross-platform | Runs natively on Windows, macOS, and Linux. |
| Self-contained catalog | The stock product and function-block catalog is embedded; no separate catalog installation is required. |
| Open source (Apache-2.0) | The full source is open for inspection, extension, and long-term maintenance. |
| Modern, accessible UX | A Danish UI and help with themeable flat-line SVG icons designed for legibility, using non-color-alone state cues. |

## Differences from the Original IHC Visual

IHC OpenVisual mostly matches the original Windows authoring tool's behaviour, except for the following:

> **Every entry carries its pin.** A registered difference is a promise about behaviour, so each one ends with
> either ***Pinned by:*** naming the test that would fail if the behaviour drifted back, or ***No test:*** with
> the reason none is possible. A difference nobody tests is a difference that can be silently undone by the next
> alignment pass and re-registered by the one after — which is how a register becomes a record of round trips.
> `RegisterPinTests` enforces that every entry has one marker or the other; it cannot check that a named test
> really pins the behaviour, so name the test that would actually fail.

**Enhancements**

- Runs on Windows, macOS, and Linux; the original is Windows-only.
  *No test:* a build and CI property, not app behaviour — every suite runs on all three operating systems in CI.
- Refuses to save text the `.vis` character repertoire cannot store — naming the offending element and
  character — where the original writes an unparsable file.
  *Pinned by:* `Latin1SaveRefusalTests`.
- A permanent **Problemer panel** lists the project's validation findings and keeps them current as the project
  is edited, where the original validates on demand and reports into a dialog. The difference is when a fault is
  found rather than what counts as one: a dialog reports only when asked, so a fault surfaces at save or transfer
  time — the most expensive moment to learn about it — while a panel that revalidates in the background surfaces
  it while the work that caused it is still in hand. Each row navigates to the element it is about when it is
  activated, the four tiers filter independently with live counts, and Error findings withhold controller
  transfer. The panel deliberately offers no way to suppress or acknowledge a finding: a rule id is a filtering
  and grouping key, and a silenced finding is invisible to the next reader with nothing recording who accepted it.
  *Pinned by:* `ProblemsPanelSkeletonTests`
  (`ThePanelSitsBetweenTheTreesAndTheStatusBarNotBelowIt`, `TheVisRowTogglesThePanelAndIsAlwaysAvailable`),
  `ProblemsListTests.TheBoundRowsAreExactlyTheEnginesFindingsWithTheirMessagesVerbatim`.
- Suggestion drop-downs (*Placering*, *Identifikationskode*, *Kabeltype* and the cable-colour fields) offer the
  **values already used in the open project**, where the original offers a **machine-local history** of what was
  typed on that installation. The original's list therefore differs between two people opening the same project
  and is empty on a fresh machine; ours is a property of the work rather than of the workstation, travels with the
  file, and is reproducible in a test. Both are open combos — a value used nowhere yet is still typeable, so the
  list never becomes a constraint.
  *Pinned by:* `ProductDialogComposerTests`
  (`AComboSuggestField_OffersTheProjectsOwnValuesForThatAttribute`, `SuggestionsDoNotConstrainTheValue`).
- A product whose family the SDK does not recognise still opens a **minimal dialog** — Navn, Placering, Note and
  Identifikationskode, the four attributes every known family declares — instead of failing to open. The original
  has no equivalent: its dialogs are per-family, and a product outside them is not something it can show.
  Inserting an unrecognised product is therefore never blocked here. The fallback deliberately does not walk the
  grammar and caption fields by raw attribute name — English DTD identifiers on a Danish screen would be worse
  than the four known ones, and the real answer to an unknown family arriving is a sixth measured preset.
  *Pinned by:* `ProductDialogComposerTests.TheMinimalFallback_OffersTheFourUniversalFields`.
- Modem telephone numbers are validated as **3–20 characters, no spaces, leading country code** (US-013) and
  refused inline, naming the offending slot. **Only the 3-character minimum matches the original.** Measured
  2026-08-12 against LK IHC Visual: it refuses a 2-character number (*"Ugyldigt telefonnummer på talværdi 1 /
  skal være mere end 3 cifre"* — a message that misstates its own rule, since exactly 3 characters is
  accepted) but it accepts a **60-digit** number, accepts a number with **no country code**, and **silently
  strips spaces** at input rather than refusing them. The 20-character maximum, the whitespace ban and the
  country-code requirement are therefore deliberate OpenVisual strictnesses: a number the modem cannot dial
  is worth catching at entry rather than at the alarm.
  *Pinned by:* `DialogValueRuleTests` (the rule and its boundaries), `ModemPhoneValidationTests` (the dialog
  consults that rule and states the refusal).
- The same rule is also reported by the **whole-project check**, as `addr-modem-phonenumber-malformed`
  (Warning, category Addressing) — one finding per offending slot, naming the offending value. It is the same
  object, not a second copy: the catalogue rule delegates its predicate to the dialog's own
  `DialogValueRule.PhoneNumber`, so the dialog, the commit refusal and the project finding cannot disagree
  about what a valid number is. **Consequence for files this application did not author:** because three of the
  four strictnesses above are OpenVisual's rather than the original's, an authentic project carrying a
  country-code-less, spaced or over-long number now shows a warning when it is checked. That is the intended
  reading — such a number is one the modem cannot dial — and it never blocks: a Warning does not stop opening,
  saving or uploading.
  *Pinned by:* `DeviceAddressRulesTests` (the boundaries, and that the entry's declared lengths mirror the
  dialog rule's).
- Every drag-and-drop operation is also reachable from the menus and the keyboard, so linking, moving, and reordering never require a mouse.
  *Pinned by:* `DragRouteAlternativesParityTests`.
- Unavailable commands explain themselves: pressing the keyboard shortcut of a greyed menu command shows the reason in the status bar.
  *Pinned by:* `DisabledReasonStatusBarTests`.
- The *Rediger* ▸ *Fortryd* / *Gentag* items name the action they would reverse or re-apply
  (e.g. "Fortryd Indsæt lokalitet"), where the original shows a bare "Fortryd" / "Gentag". A screen
  reader thus announces *what* will be undone, and the reader sees it before choosing — an
  accessibility/usability enhancement (E14/US-052; the status bar names the action too). (Alignment
  F-8b, 2026-08-09.)
  *Pinned by:* `EditHistoryTests`.
- Enhanced support for assistive technology and automation.
  *Pinned by:* `AutomationCoverageTests`, `AccessibilityTests`.
- Embedded stock catalog.
  *Pinned by:* `BuiltInCatalogProductDifferentialTests`, `BuiltInCatalogFunctionBlockDifferentialTests`.
- **Component help is inline, not a separate document.** A catalog component can carry its own description
  and a description per terminal, and IHC OpenVisual shows those texts where the installer is
  working — while choosing a component to place, on a placed node, in that node's properties dialog, and on
  request for the current selection — rather than leaving them in help files beside the catalog. The texts
  are read-only catalog data and stay distinguishable from the installer's own documentation note, which
  remains an editable project field. (Specified in FR-11.1 and E17, US-075–US-079; the exclusion below
  narrows to the *help files* themselves.)
  *No test:* the surface does not exist yet — this entry registers the intended difference and owes a
  *Pinned by* marker when the behaviour lands.
- Documentation reports render as self-contained static HTML that works in any modern browser, with optional enhanced variants and no dependency on a legacy browser component.
  *Pinned by:* `ReportSelfContainmentTests` (self-containment), `ReportHtmlOracleTests` (the bytes).
- **The reports are chosen in the app, not in a browser page.** The original carries a single
  *Dokumentation ▸ Rapporter…* entry which **exports the project to a temporary `.vis` and launches an
  external browser** at a bundled `entry_page.html`; the report is picked and rendered out there. IHC
  OpenVisual gives each of the three report types its own *Dokumentation* entry, opening one shared picker
  (Rapport / Format / Tilstand, with *Vis*, *Gem som…* and *Luk*) that generates the document itself — so a
  report is one step from the menu, the chooser is part of the application, and nothing is written to a
  temporary file or handed to an external component just to be selected. (FR-11.3 and story 09/US-040 specify
  the picker; registered here 2026-08-11, alignment F-40, after measuring the original's browser hand-off.)
  *Pinned by:* `ReportPickerTests`, `DocumentationMenuParityTests`.
- Menu commands that do nothing in the original are omitted rather than reproduced.
  *No test:* a rule governing other entries rather than a behaviour of its own; each concrete omission is
  registered and pinned separately (see the *Scenarie* entry below).
- Support multiple instances.
  *Pinned by:* `MultipleInstancesTests`.
- The *Indsæt ▸ Variable* menu does **not** list **Scenarie**, where the original carries it (greyed
  outside a scene context). A scene is **not a variable** — it is added through its own route
  (US-024, on a scene-capable Output) — so it does not belong on the variable menu. This is the
  "commands that do nothing are omitted" rule applied to a would-be-greyed item. (Alignment
  Scenarie/F-13, 2026-08-09; story 07/US-027 line: "a scene is not a variable and is added through
  its own route.")
  *Pinned by:* `VariablePaletteCompletenessTests`.
- The free-text fields the original backs with a **suggestion drop-down** are **plain text boxes** in IHC
  OpenVisual — **now only where the composer says so, and the product dialog is largely no longer among them
  (narrowed 2026-08-12, T030).** *Placering*, *Note*, *Kabeltype* and *Identifikationskode* are composed as
  `ComboSuggest`: an always-editable combo over the values already used elsewhere in the project (D07), which
  is the original's affordance. *Navn*, *Kabelnummer* and *Lysgruppe* stay plain `Text`. **What remains a
  difference is the terminal address editor's *Note* and *Ledningsfarve***, and the fact that a suggestion
  list here is drawn from the OPEN project rather than a machine-local history file — so it travels with the
  project instead of differing per PC.
  **None of these fields is a closed vocabulary, and none may become one** — `cable_colour` is `CDATA` in the
  format and the original's own list mixes colour names with installer-written pair descriptions ("Brun",
  "1-Hvid. 3-Sort", sourced from `DATA\noteCableColour.txt`). Constraining any of them to a fixed list would
  REFUSE values the format and the original both accept, so do not "align" them into drop-downs of a fixed
  set. The control vocabulary has no closed-list kind at all (D12), which is what makes that structural.
  (Story 03/US-011 records the decision and its reasoning; registered here 2026-08-11, alignment F-13; scope
  widened to the terminal editor 2026-08-11, alignment F-34; narrowed to the terminal editor 2026-08-12 when
  the metadata engine gave the product fields their suggestion lists.)
  *Pinned by:* `FreeTextFieldParityTests` (the kinds, and that no closed-list kind exists).
  The labels were pinned by `ProductDialogLabelParityTests`, deleted with the hand-written dialogs in T030 —
  a caption cannot now differ between two product dialogs, because there is only one, and every caption comes
  from a single shared composer fragment. `CatalogInsertionTests`' descriptor gate asserts each is non-empty
  across all 100 products.
- **An unaddressed terminal shows an EMPTY address cell**, where the original writes `ikke konfigureret`
  into every unwired row. Deliberate, and the reason is the point: a grid of eight rows all reading
  *ikke konfigureret* is eight rows of identical text, while eight blanks make the wired ones the only
  thing on the column — so "which terminals still need wiring" is answerable at a glance instead of by
  reading. The token itself is not lost: it is still the explicit **not-configured** entry in the address
  editor's module list, which is where it means an *action* (return this terminal to unaddressed) rather
  than a state. Story 03/US-012 states the blank as a MUST with this reasoning.
  (Measured on product 006 and registered 2026-08-12, T040 — the story had mandated it since it was
  written, but the divergence from the original was never recorded here, so a later comparison would have
  read it as a defect and "fixed" it.)
  *Pinned by:* `TerminalAddressListParityTests` (the `ikke konfigureret` entry in the editor's list),
  US-012's grid tests (the empty cell).
- **The wireless dimmer's advanced settings open in a SUB-DIALOG behind an *Avanceret* button**, where the
  original expands them in place inside the product dialog (a group box *Avancerede Dimmer egenskaber*).
  **Corrected 2026-08-12 (T114):** this entry used to add "no vendor capture in the 100-product oracle
  carries an *Avanceret* caption", and product 080's capture falsifies it — the vendor draws a button
  captioned exactly *Avanceret*, no ellipsis. So the BUTTON is parity; OpenVisual's ellipsis was dropped to
  match, and what stays registered is only what pressing it does. The claim was written when the oracle had
  been captured but not yet read product by product — the failure mode this sweep exists to catch.
  The settings themselves match —
  factory defaults 700/700/5/0/100, seconds in the dialog vs milliseconds in the file, and the vendor
  `auto | rc | rl` load-characteristic order. Only the containment differs. (Registered 2026-08-12, T030:
  the slot was declared on the wireless preset so that routing the family through the one generic dialog did
  not silently delete a reachable capability; reshaping it to the in-place form is separate work.)
  *Pinned by:* `ProductDialogPresetTests.Airlink_OffersAdvancedDimmerSettings_OnlyWhereDimmerSettingsExist`,
  `AdvancedDimmerLoadModeTests` (the settings themselves).
- The block-section variable popup sorts its **value types** in **correct Danish collation** (æ/ø/å after
  z, so *Tal* precedes *Tæller*), where the original collates æ as "ae" (putting *Tæller* before *Tal*).
  A clear improvement over a vendor collation quirk. (Alignment F-26, 2026-08-09; re-measured 2026-08-11
  across all four sections of an unlocked block — the original's order is reproduced exactly by an
  invariant comparer and by neither da-DK nor ordinal, which confirms the quirk. The **section's own
  signal type still leads the list**, outside the sort, as the original has it — that part is matched,
  not a difference; see alignment F-20.)
  *Pinned by:* `SectionFlyoutOrderParityTests`.
- The block-section variable popup draws **no separators**, where the original sets its leading signal
  type off with a thin rule, draws another before *Egenskaber*, and a third under *Ny type…* in the
  *Enum* submenu. The members and their order are otherwise the same; the missing rules are cosmetic
  only. (Alignment F-27, 2026-08-09; scope widened 2026-08-11 to the leading rule and the *Enum*
  submenu's, neither of which was visible until those lists' members and order matched.)
  *Pinned by:* `SectionFlyoutOrderParityTests`. Note this is the *section* flyout specifically — the
  node context flyouts do draw the original's rules, and `MenuSeparatorAccessibilityTests` pins that.
- The enum type picker offers a **"Ny selvstændig type…"** route that authors a 0-state, unreferenced
  project-global enumerator type without inserting a variable, which the original has no counterpart for.
  It decouples defining a type from using one, so a type can be prepared and referenced later. Note the
  original will not offer a **valueless** type in this picker at all — measured 2026-08-11: a type created
  with no values is absent from the submenu and appears only once a value is added — so a type authored
  this way is a genuinely new state for the picker to handle. (Story 07/US-027 records the decision;
  registered here 2026-08-11, alignment F-21.)
  *Pinned by:* `EnumPickerParityTests`.
- A **refused edit says what to do about it**, and its message box carries a descriptive title. The original
  states the rule alone under the application's own name: refusing a second modem, it titles the box
  *LK IHC Visual ®* and says *"Modem er allerede indsat. Der kan kun indsættes et modem i projektet"*, where
  IHC OpenVisual titles it *Kun ét modem* and adds the remedy — *"…Fjern det eksisterende modem, før du
  tilføjer et nyt."* The rule enforced, the moment of enforcement and the end state are identical; only the
  sentence is longer. (Alignment F-47, measured 2026-08-11 on the one-modem rule, which story 03/US-013
  already requires to "tell the installer why".)
  *Pinned by:* `RefusalMessageParityTests`.
- The **name prompt refuses a blank name out loud**, where the original refuses it silently. Creating an enumerator
  type or value (and renaming a node) with an empty or all-whitespace name: IHC OpenVisual keeps the dialog open and
  states the reason in a live region (*"Indtast et navn."*), retracting it as soon as a name is typed; the original
  simply closes the *Opret ny enumerator type* / *…værdi* dialog and creates nothing, with no message. Both refuse the
  empty name — only the feedback differs, and this is IHC OpenVisual's "keeps its error feedback" principle applied to
  a case the original handled by doing nothing. (Alignment F, measured 2026-08-11 on the enum create dialogs; story
  07/US-027 records the enumerator-authoring behaviour.)
  *Pinned by:* `NamePromptValidationTests`.
- The **save-changes guard is in Danish**, where the original leaves it in **English**. Discarding unsaved work (new
  project, open, close) raises a three-button Save / Don't-save / Cancel prompt on both apps with the same semantics,
  but the original shows a stock Windows message box titled *LK IHC Visual ®* reading *"Save changes to    unavngivet?"*
  with **Yes / No / Cancel** — an un-localized vendor gap — while IHC OpenVisual titles it *Gem ændringer?*, asks *"Gem
  ændringer i unavngivet før du fortsætter?"* and labels the buttons **Gem / Gem ikke / Annuller**. This is the
  Danish-everywhere rule (above) applied to the save guard. (Alignment F, measured 2026-08-11.)
  *Pinned by:* `DanishChromeTests` (`SaveChangesGuardIsDanish`).
- Placing a product **applies the insert and then asks** for its documentation, rolling back if the installer
  cancels; the original raises the dialog first and adds nothing until OK (measured 2026-08-11: its tree item
  count is unchanged while the dialog is up). So while that modal dialog is open, IHC OpenVisual's tree already
  shows the row the original has not yet added. **The end states are identical for both answers** — including
  the id counter, since a cancel rolls back rather than undoing (uxparity S-12) — and the tree is inert behind
  the modal either way, so the difference is visible only *during* the dialog. It follows from building the
  dialog from the placed element rather than from the catalog definition; reordering it is a real refactor with
  no effect on any committed state. The status line is **not** part of this difference: it announces the insert
  only once the dialog is committed (alignment F-14, 2026-08-11; story 03/US-010 records the decision).
  *Pinned by:* `InsertProductDialogParityTests`, `InsertStatusHonestyTests`.
- ~~The data-line modules view edits through a per-module editor, not in the table.~~ **Withdrawn
  2026-08-11 — this was never a difference.** It was registered that same day, on an owner spot check
  reporting the original's *Datalinie moduler* grid as having "editable, clickable columns", and written up
  as a deliberate choice to edit through a dialog instead. Both halves were then measured and both were
  wrong. The original does **not** edit in its grid: double-clicking a row opens
  `Indgangsmodul tilkoblet datalinie N` (`Udgangsmodul…` for outputs) — `Modul type` combo enabled,
  `Lokalitet` combo and `Note` disabled until a type is chosen, OK/Annuller — and **all four columns open
  that same dialog**, so no column is individually editable; a single click only selects. That is precisely
  the model this entry claimed as IHC OpenVisual's own departure. Meanwhile IHC OpenVisual has not built it:
  its rows realize no cells at all (the whole row is one flat string `Datalinie 1, ikke i brug` under four
  painted headers) and double-click reports `NoEffect`. So the two apps agree on the design and differ only
  in that one of them implements it — **an unimplemented story, not a registered difference**. Tracked in
  story 09/US-050, which was right all along.
  *Method note: this is what an unexercised comparison costs. The difference was registered from a
  screenshot of the original's grid; one `dialog.clickRow` on each column would have refuted it the same
  day. Both drivers gained that verb on 2026-08-11 for exactly this reason.*
  *Withdrawn:* kept as an audit trail; nothing to pin.
- A **decimal variable's tree row shows what the project holds**, immediately. The original keeps the
  value it was given at full precision in memory while the `.vis` stores only two fraction digits, so
  typing `1,555` into a kW leaves its row reading `1,555kW` until the project is reopened — whereupon the
  same row reads `1,550kW` (both measured 2026-08-11). IHC OpenVisual's row reads `1,550kW` at once,
  because its model *is* the file. The saved bytes are identical either way (`inivalue="1.55"`), and a row
  that can disagree with what will be saved is the very defect alignment F-43 was raised for.
  (Alignment F-41/F-44, 2026-08-11; story 07/US-027 records the decision.)
  *Pinned by:* `DecimalDialogParityTests`.
- Deleting a locality (or other node) that still **contains** elements asks for explicit
  confirmation before the cascading delete, where the original deletes silently. This is the US-009
  MUST safety guard (the message names the node and what the delete also removes); the delete is
  itself undoable, so the guard warns without trapping. (Alignment F-22, 2026-08-09 — the vendor's
  silent cascade delete is the divergence; OpenVisual follows its Ready story.)
  *Pinned by:* `DeletionTests`.

**Presentation**

- The user interface is in Danish, as the original is — including the menu and dialog wording, which follows the original's where the two apps offer the same command.
  *Pinned by:* `DanishChromeTests`.
- A title-bar dirty marker (`•`) shows at a glance that the project has unsaved changes.
  *Pinned by:* `TitleDirtyMarkerTests`.
- Tree-node tooltips always include the node's IHC resource ID, without holding a modifier key.
  *Pinned by:* `TooltipTests`.
- Modern flat-line SVG icon set, themeable, and never signalling state by colour alone.
  *Pinned by:* `NodeIconsTests` (a distinct glyph per node category). The never-by-colour-alone half is a
  design rule reviewed against `icons_design.md`, not a testable property of the running app.
- A light/dark theme switcher.
  *Pinned by:* `MainWindowViewModelTests.SetTheme_UpdatesCurrentTheme`.
- The undo/redo **status-bar** confirmation names the action in OpenVisual's own phrasing
  ("Fortrød: <handling>" / "Gentog: <handling>"), which satisfies the US-052 requirement to name the
  action reversed or re-applied. It reads differently from the original's present-tense nominal form
  ("Fortryder indsætning af …"); reproducing that exactly would need a hand-written nominal phrase
  per command, which is out of proportion to a status-line detail whose requirement is already met.
  (Alignment F-15, 2026-08-09 — acceptable presentation difference.)
  *Pinned by:* `EditHistoryTests`.
- The recent-projects list is a **"Seneste projekter" submenu** rather than the original's inline
  `&1…&4` entries under *Filer*. The mechanism is the same (one-click reopen of the most recent
  projects, at least four); only the machine-local *contents* differ, which the comparison scope
  treats as non-comparable. It sits in the original's own **second group** — after the file commands,
  before closing — so only its shape differs, not its place. (Alignment F-2, 2026-08-09; placement
  matched 2026-08-11, alignment F-12.)
  *Pinned by:* `RecentProjectsBindingTests` (the submenu), `FileMenuGroupingParityTests` (its place).
- *Filer* separates **"Luk projekt"** (close the open project, keep the application running) from
  **"Afslut"** (exit the application), where the original carries a single *Luk*. This follows from
  the multiple-instances / one-project-per-window model above: closing a project and closing a
  window are distinct actions here. The pair sits **together, last**, filling the original's own
  third and final group — one command became two, in the same place. (Alignment F-3, 2026-08-09;
  placement matched 2026-08-11, alignment F-12.)
  *Pinned by:* `FileMenuGroupingParityTests`.

**Exclusions**

- No simulation mode.
  *Pinned by:* `DanishChromeTests` — it pins the menu-bar title set exactly, and no *Simulering* title is in it.
- No auto backup.
  *No test:* an absence with no surface to exercise — there is no auto-backup command, setting or timer to
  assert against. The `.BAK`-on-overwrite side-file that *does* exist is a match, not this exclusion, and is
  pinned by `SaveBackupParityTests`.
- Editing rapport data tables
  *Pinned by:* `DataTableStoreTests` — the tables survive only as suggestion memory; nothing lists or edits them.
- Product help files (there is however extended context sensitive help).
  *No test:* an absence with no surface to exercise.

## What This Product Is Not

- **Unofficial project.** Not affiliated with or endorsed by LK/Schneider Electric.
- **Not a runtime administration or monitoring tool.** Live dashboards, user administration, and scene administration are out of scope.
- **Not a general smart-home hub.** No MQTT/Home Assistant/Matter integration — the scope is IHC project authoring.
- **Not a wireless commissioning tool (initially).** Placing wireless products in a project is in scope; RF linking/signal-testing of physical devices is out of scope until a wireless API exists.
- **Not a web or mobile app.** Desktop only.
- **Does not offer offline simulation.**

## Success Metrics

| Metric | Target |
| -------- | -------- |
| Round-trip fidelity | 100% byte-identical preserve-mode save across the reference project corpus, at app level. |
| Format conformance | Projects created or edited in IHC OpenVisual remain valid `.vis` files that load cleanly on IHC controllers. |
| Cross-platform health | Build and the headless UI test suite green on Windows, macOS, and Linux. |
| Authoring coverage | The core capabilities (project lifecycle, localities, products, function blocks, links, programming) are fully usable in the UI. |
| Specification conformance | Every behaviour matches its story's acceptance criteria, or is a deliberate, documented exception that a story records. |

---

# Part 2 — PRD-lite

## Product Context

IHC OpenVisual is the primary consumer of the shared project engine's project-edit capability. The
engine loads, validates, edits, creates, and saves `.vis` files with byte-exact fidelity; the embedded
catalog supplies the stock product/function-block library; the application-facing facade includes an
optional bridge for downloading and uploading projects from and to a live controller.

## User Classes and Characteristics

| User Class | Characteristics | Frequency of Use | Technical Proficiency |
| ----------- | ----------------- | ------------------- | --------------------- |
| Professional installer | Knows the IHC domain deeply (products, wiring, logic blocks); may be new to this app but already fluent in IHC project concepts | Weekly on customer projects | Domain: high · Software: medium |
| Technical homeowner | Knows software well; learns the IHC domain as they go; benefits most from clear UI, help, and validation feedback | Bursts (renovations, tweaks) | Domain: low-medium · Software: high |
| Contributor / developer | Extends the app or engine; needs strict layering so UI logic is testable without a running UI and the engine stays free of UI concerns | Ongoing | High |

## Operating Environment

- **Runtime**: a modern desktop application.
- **Client platforms**: Windows 10/11, modern macOS, mainstream Linux desktop distributions.
- **Storage**: local file system only — `.vis` project files; no database.
- **Network**: none required for authoring; optional HTTP(S)/USB access to an IHC v3.0 controller for project transfer.
- **Display**: standard desktop resolutions; light and dark themes.

## Constraints and Dependencies

### Design Constraints

- **Binary compatibility is a hard contract.** Every file the app writes must be a valid `.vis` file accepted by IHC controllers; the UI never hand-rolls the file format. Preserve-mode saves of unchanged content stay byte-identical; default saves re-stamp metadata exactly as the format requires.
- **The user stories are the authoritative behavioural spec.** Where observed behaviour and an IHC OpenVisual story disagree, the story is the thing to fix. Three principles guide behaviour the stories leave open:
  1. **IHC OpenVisual keeps its safety guards and error feedback.** They change nothing about *what* happens, only warn or explain — and they never guard an action that is already reversible (which is why an undoable unlock needs no warning — FR-5.2).
  2. **Simulation stays out of scope** (F10).
  3. **The app degrades gracefully** on malformed or self-contradictory input rather than crashing.
- **Where a behaviour is unspecified, the app stays permissive** rather than guessing — it refuses only what is known to be invalid.
- **Commands act on the selected element, never on which pane holds keyboard focus.** All mutations run on the engine's immutable model; the UI holds element ids, not object references.
- **UI logic is testable without a running UI** — view-model logic avoids UI-framework types where feasible.
- **Danish is the product language** for UI text and help. Project file content (the user's own names/notes, catalog text) is data and is preserved verbatim, whatever language it is in — the application never restates it in another language.
- **Icon rules**: one flat-line SVG family (24-unit grid, `currentColor`, legible at small sizes); state is conveyed by colour *plus* a glyph/decoration, never colour alone. See the icon design guideline (linked in Part 4).

### Assumptions

- Users have `.vis` files from existing installations and/or an IHC v3.0 controller.
- One project open per window (single-project model).
- The embedded catalog covers the stock product and function-block set; genuinely custom components can be imported from `.def`/`.ifb` files.

### Dependencies

- The shared project engine (engine, catalog, validation, controller services) — same repository, so no version skew.
- Offline simulation (out of scope — F10) would require a program-execution engine that does not exist yet — the largest open technical dependency were it ever taken on.
- Wireless RF commissioning depends on a wireless API that does not exist yet (explicitly out of scope until it does).

## System Features

### F1 — Project lifecycle

**Description**: Create, open, and save `.vis` projects safely.

**Functional Requirements**:

- FR-1.1: Create a new project from the built-in template (standard starting localities and built-in enumeration types) — self-contained, with nothing else to install.
- FR-1.2: Open an existing `.vis` file; exactly one project is open at a time; switching or closing prompts to save unsaved changes.
- FR-1.3: Save and Save-As. Saving an unchanged loaded project in preserve mode is byte-identical; a normal save re-stamps metadata exactly as the format requires. Writes are atomic — a failed save never corrupts the target file.
- FR-1.4: A recent-projects list (at least the four most recent) is available for one-click reopening.
- FR-1.5: A project file named at launch — the file the desktop hands the application when the installer opens a `.vis` with it — is the document opened, in place of the empty starting project. A file that cannot be opened is reported like any other failed open, leaving the application on the empty project rather than failing to start.

### F2 — Two-pane authoring workspace

**Description**: The main window presents the installation (physical) view and the functions
(logic) view side by side over the same locality structure.

> **The two panes are not two views of one menu model — each pane owns half the authoring vocabulary.**
> This is the workspace's central rule, and it decides where every insert
> command belongs:
>
> | | **LEFT pane — Installation** | **RIGHT pane — Functions** |
> | --- | --- | --- |
> | Shows | localities → **products** → pins | localities → **function blocks** → pins |
> | Owns the insert of | **products** (wired, wireless, special) | **function blocks** (library and empty) |
> | Answers | *what is physically installed, and where* | *what the installation does* |
>
> **The locality structure is shared** — every locality appears in **both** panes, in the same order, and
> a rename/add/delete shows up in both at once. What differs is what hangs beneath it, and therefore what
> each pane lets you insert: **a product is never inserted on the right, a function block never on the
> left.** Links are the one operation that deliberately spans the panes (F6), which is why the two are
> shown side by side rather than as tabs.

**Functional Requirements**:

- FR-2.1: Two tree panes — **Installation** (left: localities → products → pins) and **Functions** (right: localities → function blocks → pins) — over one shared locality structure, with a draggable splitter; a change to a locality reflects in both panes immediately.
- FR-2.1a: **Pane ownership of the insert vocabulary.** Products are inserted **only** from the Installation pane and function blocks **only** from the Functions pane; each pane offers exactly its own half **on the node's context menu**. A pane never offers a *context-menu* insert whose result it could not show.
- FR-2.1b: **The menu bar is deliberately NOT pane-SCOPED.** It *lists* the whole vocabulary regardless of which pane has focus or what is selected — nothing is hidden from it or removed. A context menu answers *"what can I do to this?"*, the menu bar *"what can this app do?"*. **Listing is not enablement**: a bar item whose command cannot run right now is shown **greyed**, with its reason available (see the Differences register), exactly as the original greys it — and that holds for the generated catalog leaves (products, function-block templates) on the same terms as the hand-registered commands, so a greyed item and a refused invoke can never disagree. (Owner ruling 2026-08-10, alignment F-8: the original's bar *is* enablement-gated by pane and selection — measured, including its withdrawal of the block inserts once the Installation pane takes focus. The earlier wording read as though OpenVisual's bar items were always *enabled*, which neither the original nor OpenVisual's own registry rows ever did.)
- FR-2.2: Every node renders a type icon from the flat-line set (per the icon-mapping doc) plus decorations for state (e.g. unconfigured/unlinked warning, locked block badge); variables show inline `name = value`.
- FR-2.3: Every command is reachable three equivalent ways: menu bar, context menu on the target node, and (where assigned) a keyboard shortcut; a documented keymap covers navigation, editing, properties, link-jumping, and pane switching.
- FR-2.4: A status bar confirms the result of the last action in a short sentence, and carries a controller-connection indicator whose connected and not-connected states differ in glyph shape (never colour alone) and are also stated in words.
- FR-2.5: Light and dark themes; icon ink and state colours follow the theme tokens.
- FR-2.6: **One language for the application's own text, verbatim for everyone else's.** Every caption the application invents is written in a single language (Danish); text that comes from the project file or the component catalog is rendered exactly as stored and is never translated.

### F3 — Locality management

**Description**: Model the rooms/places of the installation.

**Functional Requirements**:

- FR-3.1: Add, rename (name + note properties), and delete localities; changes appear in both panes and are confirmed in the status bar.
- FR-3.2: Deleting a locality that still contains products or blocks requires explicit confirmation and cascades cleanly: contained elements and the links/logic references that point at them are removed consistently.

### F4 — Product management

**Description**: Place wired and wireless products from the catalog into localities, document them,
and address wired terminals.

**Functional Requirements**:

- FR-4.1: Insert any catalog product into a locality selected **in the Installation (left) pane** from categorized menus; the product appears there with its pins/sub-resources and their default values, and does **not** appear in the Functions pane (FR-2.1a).
- FR-4.2: Edit product documentation properties (name, placement, note, cable data, identification code, light group where applicable, and inclusion in the end-user report) in a properties dialog titled with the **product type**, opened on demand from the tree **and as part of placing a product** — inserting one raises the same dialog, and cancelling places nothing. (Corrected 2026-08-11, alignment F-13: this line used to read "inserting a product opens no dialog", which was measured to be false of the original — its Insert **menu** raises the dialog and adds the product only on OK. The earlier reading came from driving the insert as a *posted* command, a route that skips the dialog the menu shows.) The **name** field is disabled when the placed element's `locked` attribute resolves to `yes` (resolved against the project's own inline DTD, which defaults it to `no`); the **placement** field is a free-text placement descriptor, **not** a room selector — a product's room is its position in the tree.
- FR-4.3: Configure input/output terminal addressing (data line + module terminal) with in-use indication, output initial values (normally-open/normally-closed semantics), per-terminal wire colour, and power-fail save-current-value behaviour. The address editor opens by **double-clicking a terminal row** or from a *Configure* button — two routes onto one sub-dialog. The terminal grids are enabled by the product's **shape** — whether it has inputs and/or outputs — not by its family, so a wireless product uses the same dialog and grids as a wired one.
- FR-4.4: Wireless products can be inserted and documented **through the same properties dialog and the same field set as wired products** (FR-4.2/FR-4.3); products that are not yet fully configured/commissioned carry a visible warning decoration. (RF linking itself is out of scope — see Constraints.)
- FR-4.5: Catalog/project constraints are enforced at edit time via the validator (e.g. at most one modem product per project).

### F5 — Function blocks and library

**Description**: Insert ready-made logic blocks from the built-in catalog or start from an empty
block; manage a personal library.

**Functional Requirements**:

- FR-5.1: Insert stock function blocks from the categorized built-in library, or an empty block, into a locality selected **in the Functions (right) pane** — and only there; a block does **not** appear in the Installation pane (FR-2.1a).
- FR-5.2: Stock (locked) blocks show a distinct badge and are read-only internally until explicitly unlocked, after which they behave like user blocks. **The unlock is silent and undoable** — no warning, and one *Undo* re-locks the block. (No warning is needed precisely because the unlock is undoable.)
- FR-5.3: Save own blocks for reuse and maintain a favourites collection; import external component definitions (`.def`/`.ifb`) into the session catalog. **Saving a block to the library locks the in-project copy**: the saved block is renamed, stamped with master name/author/date, marked `locked`, given the library badge, and becomes view-only until unlocked (FR-5.2), with no re-insertion.

### F6 — Product ↔ function-block linking

**Description**: Wire physical products to logic by direct manipulation across the two panes.

**Functional Requirements**:

- FR-6.1: Create links by dragging one pin onto another (product input → block input; block output → product output); invalid targets are rejected with feedback.
- FR-6.1a: **Link legality is a data-flow rule.** A link is legal iff the **source** produces a signal, the **target** consumes one, and **at least one end is a function-block pin** — two product pins never link directly, because routing product logic through a block *is* the IHC programming model. The rule is keyed on the pin's element kind and the **roles in the drag**, never on "kind matching": the *same pin pair* is accepted one drag direction and refused the other, so it must not be restated as "inputs↔inputs, outputs↔outputs". It is enforced so a `.vis` stays valid whoever drives the editor.
- FR-6.2: Links display reciprocally: each end shows a link child naming the full path of the opposite end, with **direction carried by the row's icon** and the label left bare.
- FR-6.2a: **A link's halves are written in the format's canonical orientation** — the dragged pin (the source/producer) owns the `link_from_resource` half; the pin dropped on (the target/consumer) owns the `link_to_resource` half. The element names read backwards from the roles (a producer owns the *from* half), so the check and the write must agree on which end is which.
- FR-6.3: Dropping onto a scene-capable output opens a dialog for the scene value (light level + ramp time for dimmers; on/off for relays) before the link is created.
- FR-6.4: A single action jumps from a link row to its opposite end in the other pane.

### F7 — Function-block programming

**Description**: Author the control logic inside a block.

**Functional Requirements**:

- FR-7.1: A per-block programming mode shows the block's variable sections (inputs, outputs, settings, internal variables) beside its program tree; entering/leaving it is a single action. **The configuration-mode view shows less**: a section with no members is not drawn, and **internal variables are a programming-mode section only**. **Entering programming mode on a locked (stock) block is view-only**: the program renders for reading, but every authoring command is gated on the block being unlocked and is **removed, not greyed**.
- FR-7.2: Add typed variables across the full resource palette (on/off, counters, integers, decimals, timers, time/date/weekday, temperature, light, humidity, energy, enumerations), with section placement rules enforced and per-variable name/note/initial value/persist-on-power-loss properties.
- FR-7.3: Build programs by dragging variables onto event/condition/command groups and picking the applicable operation **from a popup whose options are a function of the pin's type and the target group** — the target group decides the row family (events / conditions / commands), the pin type decides the operator list (e.g. a bool output on a Commands group offers `= ON` / `= OFF` / `Toggle`; the same pin on an Events group offers the event set). Events are OR-combined; condition groups support AND/OR/NOT and nesting; commands execute in order, with separate true/false branches for conditional sub-programs.
- FR-7.4: Define project-global enumeration types with ordered named values; use case structures keyed on eligible variable types, with an else branch.
- FR-7.5: Support arithmetic command lines (one operation per line, decimal/integer conversion rules) and power-up events for restoring state after outages.

### F8 — Validation, undo, and integrity

**Description**: Keep the project consistent and every edit reversible.

**Functional Requirements**:

- FR-8.1: Validate CONTINUOUSLY — the project is revalidated in the background as it is edited, and the findings are listed in a permanent panel with their severity, code, message, element and category, filterable per tier with live counts, sortable per column, and navigable to the offending element by ACTIVATING a row — double-click or Enter. A single click only selects: the panel is a list to read down, and moving the trees, the editing mode or a window under a reader who is merely scanning it is the panel taking a journey the reader did not ask for. The list has **four tiers** — *Fatale fejl*, *Fejl*, *Advarsler*, *Information* — each filtering independently. **Error findings withhold controller transfer**; the advisory tiers never do. A project that has not been validated yet is not treated as faulty. (A **Fatale fejl** row is an Error finding whose rule also REFUSES an operation — an undeclared attribute stops the save as well as being reported. It is a presentation tier, not a fourth severity: such a row is an Error like any other, so it withholds transfer for exactly the reason the *Fejl* tier does, and separating the two tells the user which faults must be repaired before the project can even be written. A refusal that produces no finding at all — an unopenable file — is still not a panel row, having no project to be a row in.)
- FR-8.1a: Navigate from a finding to the CONTROL that fixes it. Selecting a finding does nothing but select it (FR-8.1); ACTIVATING one — double-click or Enter, which behave identically — reveals its element and follows the finding all the way to the field: the owning element's dialog opens, a value that lives on a sub-item opens that sub-item's editor **stacked on the still-open parent**, and the caret lands in the field the finding is about. The route is **honest before the click**: the row names the depth it has — the tree, the owning dialog, or the exact field — and a value the dialog does not offer as an editable field degrades to opening the dialog rather than promising a field it cannot focus. A finding that names no field on an element the tree draws — an empty locality, a variable written but never read — lands on that row and opens **nothing**, because its repair is a gesture there and a dialog would be a modal to dismiss first. A finding about the project itself, which names no element at all, opens the one window that repairs it. Everything one activation opens is **one visit**: it commits as a single undoable change, and cancelling discards all of it. Navigation never repairs anything on the user's behalf.
- FR-8.2: Unlimited undo/redo across all edit operations within a session — no configured step cap, bounded only by process memory. **Prefer making an irreversible action undoable over guarding it with a dialog** — no project mutation currently needs the guard.
- FR-8.3: Ids of existing elements are never renumbered or reused; deletions leave holes (ids are monotonic and never recycled).
- FR-8.4: **Catalog-owned structure is not editable.** A product's pins exist because its catalog type declares them, so they cannot be deleted, reordered, or inserted into — the commands are absent, and the engine refuses them whatever route asks.

### F9 — Controller transfer

**Description**: Move projects between the PC and a live controller.

**Functional Requirements**:

- FR-9.1: Send the open project to a connected controller with explicit confirmation before overwriting the controller's existing project, and progress/success feedback.
- FR-9.2: Retrieve the project stored in a controller into the editor; disabled when the controller holds none.

### F10 — Offline simulation (out of scope)

**Description**: Validate behaviour on the PC before deployment. **Out of scope** (consistent with
*What This Product Is Not* above and `stories/08-simulation.md`): this would require a
program-execution engine that does not exist in the engine today. The requirements below are retained
as documentation only and would be refined in a separate design document if the capability is ever
taken on.

**Functional Requirements**:

- FR-10.1: Start/stop an offline simulation of the open project; while simulating, editing is disabled and input/output state is shown by color (distinct on/off colors) plus glyph cues.
- FR-10.2: Drive inputs and block outputs interactively — momentary hold and toggle — and simulate a power-loss/power-up cycle.
- FR-10.3: Set breakpoints on program lines and step execution line by line.
- FR-10.4: Set the simulated clock and date to exercise time- and calendar-driven logic.
- FR-10.5: Capture a filterable activity log (events, conditions, commands, value changes) exportable to a file.

### F11 — Help and project documentation

**Description**: Danish help and installation documentation output.

**Functional Requirements**:

- FR-11.1: Context-sensitive Danish help: one action (e.g. `F1`) opens the topic for the selected element/view; all-new, originally authored content. The topic states what the selected element's catalog component is and what the selected terminal does — from the description the component itself carries — alongside the element's own documentation note, and states plainly when the component carries no description. The same component and terminal descriptions are readable where the installer chooses, identifies and documents a component, without being editable there (E17).
- FR-11.2: Edit project-level information (project, customer, installer identity) stored in the file.
- FR-11.3: Generate the **three documentation reports** — end-user functions (Funktionsdokumentation), installation (Installationsdokumentation) and function-block logic (Funktionsblok dokumentation) — each in **Standard** or **Fuld** mode and as **HTML** or **plain text**; each report has its own Documentation-menu entry opening the one shared picker pre-selected, with view-in-browser (printing is the browser's) and save-as actions. There are no report options beyond type × mode × format, and the output carries no navigation apparatus.
- FR-11.4: **Fuld** mode is Standard plus additions only: the generation timestamp + programmer line, the Projekt identity block, inline `(ID …)` element ids at definition sites, the installation-only terminal-connections table, and a final **"Fejl i dokumentation"** section fed by the project verification checks — per locality → product → terminal, covering at least: unlinked terminal, missing identification code / light group / cable type / cable number / wire colour / placement / data-line address.
- FR-11.5: Report output carries **no images apart from the app's icon language** — product identity, module addressing and wire colours are conveyed as text and tables (no product photos, module diagrams, installer logo image, or external manual pictures); the function-block report renders its logic tree with the app's icon set (inline vector glyphs in HTML, unicode stand-ins in text).

## External Interface Requirements

### User Interfaces

Single main window with menu bar, toolbar, two tree panes — **Installation on the left** (products)
and **Functions on the right** (function blocks), over one shared locality structure (F2) — and a
status bar carrying the last action's result, a controller-connection indicator and the project-locale
indicator; modal dialogs for properties and confirmations. Keyboard-first: complete tasks are
achievable without a mouse (three-route command activation, FR-2.3). Accessibility: icons are decorative
and always accompanied by text labels; state is never signaled by color alone; both themes maintain
readable contrast at tree-row icon size.

### Software Interfaces

| System | Interface Type | Purpose | Data Format |
| -------- | --------------- | --------- | ------------- |
| Shared project engine (load/edit/validate/save, catalog, validator) | In-process API | All load/edit/validate/save/catalog operations | Immutable element model |
| `.vis` project files | File I/O (via the engine only) | Persistence; the byte-exact `.vis` format contract | XML with inline DTD and the format's encoding conventions |
| `.def` / `.ifb` catalog files | File I/O (via the engine only) | Optional import of external/custom component definitions | `.def` / `.ifb` catalog formats |
| IHC controller (v3.0) | SOAP over HTTP(S)/USB | Optional project send/retrieve | SOAP/XML (hidden by the engine) |

## Quality Attributes

| Attribute | Target | Measurement |
| ----------- | -------- | ------------- |
| Compatibility | 100% byte-identical preserve-mode round-trip over the reference corpus; authored files remain valid `.vis` files accepted by IHC controllers | Byte-comparison against the corpus; controller-acceptance check |
| Reliability | Unsaved changes are never lost silently — every path that would discard them prompts first; no partial/corrupt file is ever written | Save-prompt and atomic-save checks |
| Performance | Open + render the largest reference project (~236 KB) in < 2 s; save < 1 s, on typical developer hardware | Timed assertions |
| Usability | All authoring tasks completable via keyboard; icons legible at tree-row size; light + dark themes | UI checks + icon render checks |
| Language consistency | The application's own captions are in one language (Danish); file- and catalog-derived text is rendered verbatim and never translated | UI string checks; a tree-label check that a stored caption is not restated in another language |
| Portability | Same feature set on Windows/macOS/Linux | Cross-platform build + test |
| Maintainability | Zero build warnings; view-model logic testable without a UI; engine untouched by UI concerns | Build gates; suite layering |

## Data Requirements

### Data Model Overview

The only persistent artifact is the `.vis` project file: an XML document with an inline DTD
holding the full installation (localities, products, function blocks, links, programs, project
metadata) as one element tree with stable hexadecimal ids. In memory it is an immutable tree; edits
happen in editor sessions that produce new snapshots (enabling undo). The embedded catalog is
read-only compiled-in data.

### Data Integrity and Retention

- **Integrity**: atomic saves; validator gate before save/transfer; ids never reused.
- **Retention**: project files belong to the user on their file system; the app keeps no hidden copies of them.
- **Privacy**: project info may contain customer names/addresses. The app sends no file content anywhere; optional telemetry must not include project data. Controller credentials are handled by settings encryption, never stored in project files.

## Glossary

| Term | Definition |
| ------ | ----------- |
| IHC controller | The physical unit running a home installation; executes the deployed project. |
| `.vis` file | The XML project file (with inline DTD) holding a controller's complete configuration. |
| Locality | A room/place node organizing products and function blocks. Localities are the **shared spine of both panes** — the same locality appears in each, holding its products on the left and its blocks on the right. |
| Installation pane | The **left** tree: localities → products → pins. The physical view — what is installed and where. **Products are inserted here, and only here** (FR-2.1a). |
| Functions pane | The **right** tree: localities → function blocks → pins. The logic view — what the installation does. **Function blocks are inserted here, and only here** (FR-2.1a). |
| Product | A physical device definition (switch, lamp output, sensor, …) instantiated from the catalog into a locality. Lives in the **Installation (left)** pane. |
| Function block | A reusable logic component with typed pins, variables, and programs. Lives in the **Functions (right)** pane. |
| Pin / resource | An addressable input/output/variable on a product or block; the endpoint of links. A **product's** pins are declared by its catalog type and are **not** independently editable — not deletable, not reorderable (FR-8.4). A **block's** variables are authored (F7). |
| Link | A **directed** connection routing a signal from a **source** pin to a **target** pin. Its two halves record the direction: the **source** carries the `link_from_resource` half, the **target** the `link_to_resource` half — the element names read backwards from the roles (FR-6.2a). Legality is a data-flow rule, not a kind match (FR-6.1a). |
| Scene / scenario link | A link carrying a preset (light level + ramp, or on/off) recalled by one trigger. A **distinct link family** — the data-flow rule in FR-6.1a does not cover it. |
| Catalog | The library of stock product and function-block definitions; embedded in the app. Distinct from the **insert menu**, which is the app's *presentation* of the catalog and can differ from it. |
| Locked (stock) block | A catalog-supplied block that is read-only until explicitly unlocked. The unlock is silent and **undoable** (FR-5.2). |
| `locked` (product attribute) | Per-element flag deciding whether a placed product's *Name* is editable. Resolved against the **project's own inline DTD** (default `no`), **not** the catalog's (default `yes`) — the catalog value is only the seed written at insert time (FR-4.2). |
| Preserve save | The byte-identical save mode for unchanged content; default save re-stamps metadata as the format requires. |

---

# Part 3 — Test Information

## Test Oracles

Correctness is judged against fixed oracles rather than opinion:

| Oracle Type | Application | Example |
| ------------ | ------------- | --------- |
| Committed reference files (byte comparison) | Round-trip and authoring fidelity | Loading a reference `.vis` and preserve-saving reproduces the file byte-for-byte; scripted edit sequences reproduce the committed result files exactly. |
| IHC controller acceptance | Interop | An IHC controller loads and runs projects IHC OpenVisual wrote. |
| Invariant checking | Editing semantics | Id allocation is monotonic and never reuses freed ids; links are always reciprocal; validator findings for known-bad inputs. |
| Known-answer tests | Templates and catalog | A new empty project equals the known template output; embedded catalog components match their committed reference definitions. |
| Property-based properties | Serialization robustness | Encode/decode round-trip properties over generated text. |
| Expected UI state | Headless UI checks | Windows and view-models bind and reach the expected state after simulated user actions. |

## Test Data

The reference corpus is committed and self-contained: no controller, no external install, and no private
data are required to exercise the engine, unit, and UI checks. Fixtures contain no credentials or
personal data.

---

# Part 4 — Links and References

## Source Code

- Public repository: <https://github.com/mmc41/IHCClientSDK> — mono-repo containing the app and the shared engine.

## Companion Specifications

| Document | Location |
| ---------- | ---------- |
| Epics & user stories (E1–E16, US-NNN) — the detailed spec; **start here for any feature** | `applications/ihc_openvisual/docs/stories/` |
| Icon design guidelines (flat-line SVG family) | `applications/ihc_openvisual/docs/icons_design.md` |
| Icon selection reference (`.vis` element → SVG) | `applications/ihc_openvisual/docs/icon_codes.md` |

## Standards and Specifications

- Apache-2.0 — repository license (`LICENSE.md`).
- WCAG-informed icon rules — state never signaled by color alone; decorative icons always paired with text labels (see icon design guidelines).
- IHC `.vis` / `.def` / `.ifb` file formats — undocumented; treated as a byte-exact contract enforced by the reference test corpus rather than a written spec.
