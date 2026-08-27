# `.vis` project findings catalogue

> ⚠️ **MOSTLY UNCONFIRMED DRAFT.** Rows may still be unreal, duplicated or missing, and most
> severities and sources remain per-row judgement calls. Ids are real only where the engine already
> checks them; the rest are proposed. Two kinds of row now carry evidence: the ✅ rows in §5 (an
> outside documentation report — see the appendix), and the rows marked **✔ authored** / **⊘ refused**,
> which were tested one at a time against the live LK IHC Visual 03.04.72.03. Of the 87 rows §5 first
> proposed, **77 are now settled** (68 authored, 9 refused) and the remaining 8 are classified as
> unreachable offline, by an unfound route, or by a catalogue constraint. **No row is untouched.**
> The campaign ran while authoring
> [`tests/testdata/projects/Project6-Errors.vis`](../../tests/testdata/projects/Project6-Errors.vis)
> — see [its authoring record](../../tests/testdata/projects/Project6-Errors.md) and §8 below.

Every condition the SDK can report about a project file, in one catalogue, classified on three
axes: **category** (what part of the project it is about), **severity** (what it costs the user), and
**source** (who or what produced it). One row per finding, each with a permanent id.

The **authority** for a row's id, category, severity, kind, status and Danish label is the compiled
declaration in `ihcclient/src/vis/validation/ProblemCatalogEntries.*.cs`, not this file. Those fields are
rendered from the declarations into the [generated catalogue index](#appendix--catalogue-index-generated-from-the-declarations)
at the end of this document and compared by a test, so they cannot drift. What the sections below add is the
evidence and the rationale — what a condition costs, why it may be fine, and how it was verified — which is
prose, and which no generator can produce.

## How to read a row

| Column | Meaning |
| --- | --- |
| **Id** | Stable kebab-case rule id — the finding's identity, used for filtering and grouping. Permanent once published; a retired check keeps its id reserved. It is NOT a suppression key: suppression is foreclosed (see §7). |
| **Cat** | The category code from §1. |
| **Sev** | Fatal error / Error / Warning — see §2. |
| **Finding** | The condition, stated as what is observed — never as an accusation. |
| **Why it may matter** | The consequence in the finished installation. |
| **Why it may be fine** | The legitimate reason a user would knowingly leave it. Present only for user-sourced rows; this column is why those checks are advisory. |

User-facing wording follows the convention already used by the report findings: a short fixed Danish
label (*Mangler Id-kode*, *Ikke forbundet*), never a sentence assembled at render time.

**Where a number lives.** An entry's `Thresholds` are RULE-ONLY facts: a bound that only the whole-project
run reads belongs on the entry, as data, so it can be cited and changed without a code change. A number needed
by BOTH a gesture and a rule does not belong there — it lives below both, and each reads it. The pattern is
already in the tree twice: `ElementView.DeclaredBounds` comes off the grammar and reaches the dialogs and the
commit check alike, and `DatalineAddress.MaxDataLine` is the addressing model's own constant that the address
rules and the address editor both consult. Putting either on an entry would have made the catalogue the
authority for a fact a gesture must enforce before any validation runs.

The line is not permanent per row, and one row is worth naming because it is about to move. The S0 pulse
range (`addr-s0-ticks-missing`'s `MinimumTicks`/`MaximumTicks`) is a rule-only fact TODAY — nothing in this
codebase gates it at entry — but the vendor's own dialog refuses an out-of-range count at OK, so the first
task that gives the S0 dialog that parity moves the pair below both and leaves the entry reading it rather
than declaring it. That is a relocation, not a second copy: two owners of one number is the state the drift
pin on the telephone lengths exists to prevent.

**Two rows, one number.** Where two rows compare against the SAME physical fact, each still declares its own
threshold — a rule reads its OWN entry, never a neighbour's, so a row can be read on its own and its evidence
can cite its own source. What the two declarations must not do is hold the figure TWICE: they bind one
constant declared beside them in `ProblemCatalogEntries`, and a test pins the pair equal. Two rows are two
statements about one fact, so an edit that moved one of them alone would not be a smaller change — it would
silently re-classify the other. Two pairs stand this way: `capacity-rs485-exceeded`'s `MaximumRs485Components`
and `rs485-bus-installation`'s `Rs485MaxComponents` are one bus ceiling, and `root-version` and
`root-version-minor` each declare the `SupportedVersionMajor` they partition between them. This is not the
"lives below both" relocation above — nothing outside the whole-project run reads either figure, so both stay
rule-only facts on their entries.

---

## 1. Categories

| Code | Category | Covers |
| --- | --- | --- |
| **INT** | File integrity, format and transfer | Container, encoding, XML/DTD, ids, IDREFs, schema conformance, root invariants, the open/save/import/export operations |
| **WIR** | Wiring and signal flow | Follow-links between products and function blocks |
| **LOG** | Logic and data model | Function-block shape, programs, variables, flags, timers, enums |
| **SCN** | Scenes | Scene resources and their member rows |
| **ADR** | Addressing and commissioning | Data-line addresses, wireless binding, dimmer channels, meters, modem |
| **DEV** | Device settings | Dimmer, shutter, backup, initial-value and accessibility settings |
| **DOC** | Naming, identity and documentation | Names, identification codes, cable data, placement, report completeness |
| **PRJ** | Project structure and capacity | Localities, orphan blocks, housekeeping, controller fit |

The **Covers** column above is prose and stays hand-written. How many rows each category actually holds is
not — that count is a fact about the declarations, it went stale here once already, and it is now rendered
from them:

<!-- GENERATED: category counts — rendered from the declarations; do not edit by hand -->
The **Fatal error** column is the `Refusal` disposition under the name §2 gives it; the code has
no `Fatal` value. Only CATEGORISED entries are counted, so this total is smaller than the
catalogue's own: the operation-outcome heads carry no category, by design.

**Information** is the advisory tier below Warning: those rows report a fact worth knowing about
a correct project, so they ask for no repair and no judgement.

| Code | Fatal error | Error | Warning | Information | Total |
| --- | --- | --- | --- | --- | --- |
| **INT** | 18 | 16 | 9 | 0 | 43 |
| **WIR** | 0 | 1 | 6 | 1 | 8 |
| **LOG** | 0 | 13 | 24 | 6 | 43 |
| **SCN** | 0 | 2 | 8 | 0 | 10 |
| **ADR** | 0 | 5 | 11 | 4 | 20 |
| **DEV** | 0 | 1 | 10 | 4 | 15 |
| **DOC** | 0 | 0 | 18 | 0 | 18 |
| **PRJ** | 0 | 10 | 6 | 5 | 21 |
| **Total** | **18** | **48** | **92** | **20** | **178** |
<!-- END GENERATED -->

## 2. Severity

The four levels are separated by **what the user can still do**, not by how the condition is detected.

| Severity | Definition | The operation | Who decides |
| --- | --- | --- | --- |
| **Fatal error** | The project **cannot be opened, saved, exported, imported, or transferred to or from the controller**. The tool cannot carry the operation through without losing or inventing content. | Refused. Nothing is opened, and nothing is written or overwritten. The fault is named, with the file and position where that is known. | Nobody — the operation is impossible |
| **Error** | The operation succeeds, but this is **very likely a mistake, and it has a negative consequence**: a state IHC Visual or the controller rejects, or an installation that demonstrably cannot work. | Proceeds. The finding is reported for repair. | The tool — it is wrong regardless of intent |
| **Warning** | This **might** be a mistake and might not. The project is well-formed and usable; the *installation* may be incomplete, contradictory or pointless — or deliberately so. | Proceeds. The finding is a punch-list item. | The user — only the author of the installation can judge |
| **Information** | Nothing is wrong. Something about the project is worth **knowing** — no repair is implied, and no judgement is being asked for. | Proceeds. The finding is not a punch-list item. | Nobody — nothing is being called a mistake |

The dividing lines: a Fatal error is about the **operation** — it cannot be carried through. An
Error's negative consequence **holds whatever the author intended**, so the tool can call it wrong on
its own. A Warning's consequence **depends on that intent**, so only the user can call it: the *Why
it may be fine* column states the legitimate reading, and that column is why those rows are advisory.
Information is the level below that: a Warning asks the author for a judgement, and this asks for
nothing.

The bottom level is in use: the rows that declare it are listed together in **§5b**, separately from
§5's advisory rows, because the two ask the reader for different things. Reclassifying an EXISTING row
into it stays a separate change with its own oracle diff.

Every Fatal row names the operation it refuses in the **Blocks** column, and every operation the catalogue
knows has a word there: **Open**, **Save · Export**, **Edit-open**, **Import**, **Download**, **Upload**. The
column is generated from each row's declaration, so it cannot drift from what a site actually refuses.

> **"Fatal error" names three different populations in this document, and they do not nest.** §1's generated
> counts column is the `Refusal` DISPOSITION — 18 rows that refuse and report nothing, so none of them can
> ever be a row in a findings list. §4's Severity cell is a different set of 21: the 17 of those 18 it
> publishes (`load-truncated` is `RuledOut` and is argued in §6), plus four whose disposition is `Error`
> because they also REPORT. It follows the definition at the head of this section, so it counts the file
> lifecycle and the two transfers and nothing else — which is why `id-duplicate-token` carries a **Blocks**
> cell there while its Severity cell reads *Error*: it refuses `edit.open` alone, and the project it describes
> opens, saves and transfers normally. And OpenVisual's *Fatal fejl* tier is a third set of 5 — every `Error`
> row that refuses ANYTHING, so those four plus `id-duplicate-token`. A reader computing "fatal" from the
> disposition gets the complement of what the application shows.

## 3. Source

| Source | Definition — the decision procedure | Typical origin |
| --- | --- | --- |
| **File** — project file corruption | **No sequence of legal edits in this editor can produce the state.** The bytes came from outside the tool's own invariants. | a foreign or older writer, a hand edit, a truncated or re-encoded transfer, a wrong file passed to open, a newer IHC Visual version |
| **User** — action or lack of action | The state is reachable by ordinary authoring, and is simply unfinished, contradictory or pointless. | terminal not yet addressed, block inserted but not programmed, documentation field left blank, two blocks driving one lamp |

The two axes are independent: a user-sourced finding can be an **Error** (a duplicate data-line
address is authored, not corrupt, and still breaks the installation), and a file-sourced finding can
be a **Warning** (a vendor file whose root children are ordered unusually still loads and works).

---

## 4. File-sourced findings — corruption


The file did not come out of a legal edit. The user cannot "fix" these by authoring; the remedy is a
better copy of the file, a re-export, or a repair.

| Id | Cat | Sev | Blocks | Finding | Why it matters / how it arises |
| --- | --- | --- | --- | --- | --- |
| `load-empty` | INT | Fatal error | Open | The stream holds no bytes | Not a project file; a zero-length copy or a failed transfer |
| `load-gzip` | INT | Fatal error | Open | The content is gzip-compressed | A raw controller project blob that was never decompressed |
| `load-bom-utf8` | INT | Fatal error | Open | A UTF-8 byte-order mark precedes the document | Re-saved by a text editor; `.vis` is ISO-8859-1 with no BOM |
| `load-bom-utf16` | INT | Fatal error | Open | A UTF-16 byte-order mark precedes the document | Re-saved in a foreign encoding; every byte offset is wrong |
| `load-encoding-declared` | INT | Fatal error | Open | The XML declaration names an encoding other than ISO-8859-1 | Text would be read in one encoding and written back in another, silently changing it |
| `load-not-xml` | INT | Fatal error | Open | The document is not well-formed XML | Truncation, a partial write, or not a project file at all |
| `load-dtd-malformed` | INT | Fatal error | Open | The inline DTD block cannot be parsed | The schema the file carries about itself is unusable, so nothing can be validated or written back |
| `load-root-tag` | INT | Fatal error | Open | The root element is not `<utcs_project>` | Another XML file (a `.def`, `.ifb` or unrelated document) was opened as a project |
| `load-version-missing` | INT | Fatal error | Open | The root carries no `version_major` | The file cannot be identified as an IHC project of any version |
| `root-version` | INT | Error | — | `version_major` is above the highest supported major, declared as `SupportedVersionMajor` | Written by a newer tool in a format this one does not model; opening it would misread content and saving would destroy it. **Not a Fatal error:** nothing refuses an open under this cause — `StructureRules` reports it, and the row is declared `Error`. **The supported major is declared data, and it is the same declaration `root-version-minor` reads:** the two rows partition that number between them — above it is this row, at it is that one — so both bind one constant rather than each writing a 4 of its own. An unparseable `version_major` is passed over rather than guessed at |
| `root-version-minor` | INT | Warning | — | `version_minor` is above the supported one, on a file of the supported major | The file may carry content this model does not know, and a save can drop it **silently** — which is what the vendor's own load-time prompt warns about (*"Projekt information kan muligvis gå tabt ved indlæsning"*). Reported as a finding rather than as a load question, so the same contract is honoured without inventing an interactive load surface. **Excludes a major ahead** (that is `root-version` above) **and a major behind** (current-or-older is accepted input, so a minor on top of a superseded major says nothing) — which leaves a deliberate coverage edge: a v3 file with a minor ahead reports nothing. Its value over `element-undeclared` is that it names the *cause* even when the newer minor adds nothing the registry trips over |
| `load-character-data` | INT | Fatal error | Open | An element contains character data | The `.vis` model is attribute-only; opening would silently drop the text at the next save |
| `load-depth` | INT | Fatal error | Open | Element nesting exceeds the supported depth | Corrupt or hostile file; a legal project never nests that deep |
| `element-undeclared` | INT | Fatal error | Save · Export | An element type is declared neither in the file's inline DTD nor in the schema registry | The element has no declared rendering — writing the file would lose it |
| `attr-undeclared` | INT | Fatal error | Save · Export, Edit-open | An attribute is declared neither in the element's inline-DTD block nor in the registry | The value has no declared rendering — writing the file would lose it |
| `attr-latin1` | INT | Fatal error | Save · Export | An attribute value carries text outside ISO-8859-1 | The `.vis` encoding cannot represent it; writing would mangle or drop characters |
| `attr-required` | INT | Fatal error | Save · Export | A `#REQUIRED` attribute is missing | The file would violate the DTD it declares inline — IHC Visual rejects the element |
| `save-target-unwritable` | INT | Fatal error | Save · Export | The destination cannot be written (locked, read-only, missing, or out of space) | The write is abandoned before any existing file is touched |
| `save-roundtrip-mismatch` | INT | Fatal error | Save · Export | Re-reading the just-written bytes does not reproduce the project | The file would not say what the project says; the write is rolled back |
| `import-catalog-unparsable` | INT | Fatal error | Import | A `.def` / `.ifb` catalog file cannot be parsed | Nothing can be taken from it; the import is abandoned whole |
| `import-catalog-wrong-kind` | INT | Fatal error | Import | The imported file is not the catalog kind it is offered as | A product definition and a function block are not interchangeable |
| `import-controller-no-project` | INT | Fatal error | Download | The controller holds no stored project to download | There is nothing to import |
| `export-controller-declined` | INT | Fatal error | Upload | The controller refused to store the uploaded project | The upload did not complete; the controller's project state must be re-checked before retrying |
| `id-wellformed` | INT | Error | — | An `id` is not a well-formed `_0x` hex token in the legal packed range | Nothing can reference the element reliably; id allocation cannot account for it |
| `id-duplicate-token` | INT | Error | Edit-open | Two elements carry the same id token | Every reference to that id is ambiguous |
| `id-duplicate-counter` | INT | Error | — | Two ids share a counter | The id space is no longer a bijection; the next minted id may collide |
| `id-typecode` | INT | Error | — | An id's type-code disagrees with its element tag | IHC Visual resolves the element to the wrong kind |
| `idref-dangling` | INT | Error | — | A reference attribute names an id no element carries | The reference resolves to nothing (the null token is a legal unwired state and is not this) |
| `attr-enum-range` | INT | Error | — | An enumerated attribute holds a value outside its declared set | The value has no defined meaning for reader or controller |
| `luid-malformed` | INT | Error | — | `last_unique_id` is not a `_0x` hex token | No further id can be minted from a value that cannot be read |
| `luid-low` | INT | Error | — | `last_unique_id` is absent or below the highest counter present | The next minted id collides with an existing element |
| `luid-ceiling` | INT | Error | — | `last_unique_id` exceeds the 24-bit counter ceiling | The id space is exhausted; nothing further can be inserted |
| `root-children` | INT | Warning | — | The root's children are not the seven fixed children in the fixed order | Loads and works; deviates from every vendor-authored file |
| `containment` | INT | Warning | — | An element sits outside the modeled containment rules | The placement model is deliberately permissive where the vendor format is unmodeled |
| `link-bijection` | WIR | Error | — | A follow-link half is unwired, names a missing partner, has a partner of the wrong kind, or is not linked back reciprocally | A one-sided link is not a wire; the signal path is broken (an unwired half is never authored — the editor writes both ends or neither) |
| `fb-shape` | LOG | Error | — | A function block does not hold exactly the five containers in their fixed order | The block cannot be read or written as a function block |
| `fb-programs` | LOG | Error | — | A `programs` container holds something other than `program_simple` | The program list is not the shape the controller executes |
| `fb-pin-container` | LOG | Error | — | A pin sits under the wrong variable container | The pin's direction and kind no longer follow from where it lives |
| `fb-local-ref` | LOG | Error | — | A programming reference points outside its own function block | Programs are block-local by construction; the reference cannot be executed |
| `inline-constant` | LOG | Error | — | An embedded constant is not referenced by its parent's `link2` / `value` | The constant is orphaned inside the node that should own it |
| `enum-typedef` | LOG | Error | — | A `resource_enum`'s `typedef` references something that is not an `enum_definition` | The variable has no value domain |
| `enum-inivalue` | LOG | Error | — | A `resource_enum`'s `inivalue` is not a value of its own typedef | The variable starts at a state its type does not admit |
| `program-shape` | LOG | Warning | — | A program does not carry the vendor skeleton (`events`/`actions`, or `conditions`/`actions`/`actions`) | Vendor tooling loads deviants without complaint |
| `scene-bijection` | SCN | Error | — | A scene row names a missing partner, a partner of the wrong kind, or is not linked back reciprocally | The row cannot activate anything (an *unwired* row is legal and is not this — see `scene-member-unwired`) |
| `dataline-address-malformed` | ADR | Error | — | `address_dataline` is not a `_0x` hex token | The address cannot be decoded at all |
| `dataline-address-range` | ADR | Error | — | `address_dataline` is outside the legal 1–128 module range | No module can answer to it |
| `dataline-address-duplicate` ⊘ | ADR | Error | — | Two terminals of the same direction claim the same data-line address | Both react to the same command and neither can be addressed alone. **Reclassified from §5:** IHC Visual offers an already-claimed address as `N (i brug)` but disables OK, so no legal edit produces this. **Oracle:** `tests/testdata/projects/Synthetic/DuplicatedAdressErrors.vis` — necessarily synthetic, since the state cannot be authored; it carries two inputs on 1.01, two on 1.02 and two outputs on 1.01 |
| `addr-s0-ticks-missing` ⊘ | ADR | Warning | — | An S0 meter device has no pulses-per-unit value | Energy readings cannot be scaled. **Reclassified from §5:** the field is validated on commit — *"Antallet af pulser skal være mellem 1 og 10000"* — so a blank value cannot be authored |
| `dev-dimmer-fade-zero` ⊘ | DEV | Warning | — | An RS485 LED-dimmer channel's fade-up and fade-down rates are both zero | The dimmer switches hard instead of fading. **Reclassified from §5:** the field clamps to a 200 ms minimum, so zero cannot be authored on this family |
| `dev-write-to-read-only` ⊘ | DEV | Error | — | A program assigns a variable declared read-only | The assignment is refused or ignored at runtime. **Reclassified from §5:** no variable dialog carries an accessibility control, so a block variable cannot be marked read-only; and programs are block-local, so none can reference a product's read-only resource |
| `enum-def-duplicate-name` ⊘ | LOG | Warning | — | Two values of one enum share a name | The two states are indistinguishable to a reader. **Reclassified from §5:** the editor answers *"Vælg et andet navn"* and refuses the commit |
| `enum-def-duplicate-index` ⊘ | LOG | Error | — | Two values of one enum share an index | The stored value is ambiguous. **Reclassified from §5:** the enum editor has no reorder and no index field; values append and their indices follow insertion order |
| `scene-duplicate-target` ⊘ | SCN | Warning | — | The same output appears in more than one member row of one scene | The rows contradict each other for that output. **Reclassified from §5:** adding an output already in a scene to that same scene is rejected (the same output in a *different* scene is accepted) |
| `scene-member-unwired` ⊘ | SCN | Warning | — | A scene member row references no output | That row has no effect. **Reclassified from §5:** neither member dialog carries an output selector, and a member exists only as one half of a reciprocal pair — see the §6 note |
| `struct-icon-default` ⊘ | PRJ | Warning | — | An element is left with the default icon in a project where icons are otherwise chosen | Inconsistent reading of the tree and the reports. **Reclassified from §5:** not one element-properties dialog in the application carries an icon picker, so there is no "otherwise chosen" to deviate from |
| `scene-dimming-out-of-range` ⊘ | SCN | Warning | — | A scene member's light level is outside 0–100 % | No dimmer can act on it, and the vendor tool **silently zeroes it** the first time the member's dialog is committed — the value the author wrote quietly becomes 0. **Reclassified from §5:** the *Lysniveau* spinner cannot express an out-of-range value (it stops at 100 and does not wrap), while the file layer carries and renders one, so the state arrives by hand-edit or a defective writer. **A Warning, not an Error:** the demonstrated harm depends on which tool touches the row next, and controller behaviour is untested — an Error's consequence must hold whatever the author intended. The floor is declared `Authored` and carries a TODO: no source probed the lower bound |
| `capacity-voicemodem-dimmer-conflict` ⊘ | PRJ | Error | — | The project contains both a Voice Modem and an RS485 LED Dimmer | The two cannot share a controller, so one of them can never operate. **Reclassified from §5:** IHC Visual 3.4 refuses the insert with *"Kan ikke indsætte Voice Modem og RS485 LED Dimmer i det samme projekt."* **An incompatibility, not a capacity** — it declares no threshold and binds no arguments, because one of each is already the whole condition. **The SMS modem is a different product and is excluded**, which is what keeps the row silent on the three committed projects that carry a dimmer beside one. The Voice Modem is identified by its device-root tag rather than by a catalog lookup, because the built-in catalog ships no voice-modem product at all |
| `capacity-rs485-exceeded` ⊘ | PRJ | Error | — | The project holds more than 32 RS-485 bus components, the SMS modem included | The bus takes 32; past that the project cannot be fully commissioned however it is wired. **Reclassified from §5:** IHC Visual 3.4 refuses the insert that would exceed the limit with *"Det maksimalt antal tilladte RS485 komponenter er 32 inkl. SMS modem"*. **An Error, not a Warning**, on the vendor's own wording — *"maksimalt antal tilladte"* is a hard maximum, where `capacity-wireless-exceeded` says *"bør"* and was corrected down to Warning for exactly that reason. **The SMS modem counts**, because the guard sentence names it. The threshold's evidence records that the boundary is uncited: the box was driven, but no run established that 32 commits and 33 does not |
| `capacity-s0-multiple` ⊘ | PRJ | Error | — | The project contains more than one S0 metering product | The controller binds exactly one, so the extra products can never be commissioned and the file misdocuments the installation. **Reclassified from §5:** IHC Visual 3.4 refuses the second insert with *"Der kan kun være et S0 produkt i Visual projektet"*, so a file carrying two arrived by import or by hand. **Unlike its sibling `capacity-modem-multiple`, it declares its limit as data** — that row keeps "one" in its predicate, this one has a citable vendor sentence behind the number, and the divergence is deliberate. The measurement's own limit is recorded in the threshold's evidence: the box was driven, the boundary was not |
| `logic-statement-unlinked` ⊘ | LOG | Error | — | A program statement — `<event>`, `<condition>` or `<action>` — carries no `link1`, so it references nothing | The statement does nothing that can be modelled, and IHC Visual 3.4 handed such a project **terminates outright** when the program runs: no dialog, no error, no `.BAK`. **Reclassified from §5:** the vendor editor always writes `link1` on these three tags — present on all 6,441 statement elements of the format specification's sample and on every statement across this repository's byte-oracle corpus — so the state arrives only by hand-editing or a third-party writer. **Not `attr-required`:** `link1` is `#IMPLIED` in the vendor DTD, and making it required in the registry would refuse Save and Export, turning a repairable project into one that opens and can never be saved again. **Excludes `event_power`**, which carries no `link1` by design and shares `<event>`'s id type code and icon — so the rule matches on the tag alone |

> The engine currently emits the three `dataline-address-*` conditions under the single rule id
> `dataline-address`; splitting them is a catalogue-level refinement, and the duplicate case is
> user-sourced (see §5).

---

## 5. User-sourced findings — action or lack of action

The file is well-formed and every reference resolves. Most rows here say the *installation* is
incomplete, contradictory or pointless. A second kind sits beside them: the HARDWARE and FIRMWARE
errata, where the project is authored perfectly well and the EQUIPMENT misbehaves — a block revision
the manufacturer confirmed defective, a device needing firmware no upload can apply, a controller
ceiling the project passes. Those are user-sourced too, because what a reader does about one is a
decision about the installation rather than a repair to the file. **Nothing here blocks an operation**
— no row in this table is a Fatal error, so the project always opens, saves, exports and imports.

The **Error** rows are errors on §2's axis: their negative consequence holds whatever the author
intended, so the tool can call them wrong on its own. For most that is because the described
installation cannot work as written, even where the *Why it may be fine* column gives the reason the
author accepted it. For an erratum it is because the defect belongs to the equipment, which no
authoring intent changes — a revision confirmed defective is defective in every project that embeds
it, and a block that can reach itself never runs on any controller. That an erratum's consequence is
produced by the controller rather than by the authoring does not move it off the axis, which separates
intent-independent consequences from intent-dependent ones; §2's definition already admits it, naming
"a state IHC Visual **or the controller** rejects". Those rows are exactly the ones whose *Why it may
be fine* cell is an em-dash: there is no legitimate reading to give. The **Warning** rows are advisory
throughout: the user reads the finding, decides whether it is a mistake or a deliberate state of a
half-finished installation, and acts or ignores it.

| Id | Cat | Sev | Finding | Why it may matter | Why it may be fine |
| --- | --- | --- | --- | --- | --- |
| `link-product-unwired` ✔ | WIR | Warning | A product no input or output pin of which owns a link | The device is installed and the project does nothing with it — nothing can reach it and nothing can switch it | Device installed ahead of the logic, held in reserve, or driven only from a controller-side integration |
| `link-output-multidriven` ✔ | WIR | Warning | A product output is driven by more than one source | Two blocks assign the same physical output; the last writer wins and behaviour depends on timing | Deliberate multi-path control (a manual path and an automation path) where the author accepts last-writer-wins |
| `link-fb-input-unfed` ✔ | WIR | Warning | No input pin of a function block owns a link, and the block has no autonomous start | The block's trigger never arrives from the physical installation | The block is still being built |
| `link-fb-output-unused` ✔ | WIR | Warning | A function-block output pin owns no link | The block computes a result nothing consumes | Result used only as an internal state, or read from the controller's own API/app |
| `link-through-empty-block` ✔ | WIR | Warning | A link ends on a block that carries no programs | The signal enters the block and stops there | The block is a placeholder for logic to be written |
| `link-pass-through` ✔ | WIR | Warning | A block whose only logic copies one input straight to one output | The block adds nothing; the two devices could be linked through a simpler path | Intentional naming/documentation indirection, or a stub kept for a later extension |
| `logic-case-duplicate-value` | LOG | **Error** | Two case branches of the same switch test the same value | The second branch is unreachable — whichever of the two the author meant, one of them never runs | — |
| `logic-block-empty` ✔ | LOG | Warning | A function block declares no programs | The block never does anything | Newly inserted block; a block used only as a named collection of variables |
| `logic-block-no-pins` ✔ | LOG | Warning | A function block declares no inputs and no outputs | Nothing outside the block can reach it | Block driven entirely by timers/internal state |
| `logic-program-no-events` ✔ | LOG | Warning | A program carries commands or a branch and declares no events | The commands are written and nothing can ever run them | Program under construction, its trigger not yet chosen |
| `logic-program-no-actions` ✔ | LOG | Warning | A program declares events but no commands | The program starts and does nothing | Trigger reserved for later |
| `logic-subprogram-no-conditions` ✔ | LOG | Warning | A sub-program declares no conditions | The conditional branch always takes the same path | The author wants an unconditional else-branch |
| `logic-variable-write-only` ✔ | LOG | Warning | A variable is assigned by programs but never read or linked | The value is computed and thrown away | Value read externally (controller API, app, scene) |
| `logic-variable-read-only` ✔ | LOG | Warning | A variable is read by programs but never assigned and never linked | The logic always sees its initial value | Deliberate constant expressed as an initial value |
| `logic-output-never-assigned` ✔ | LOG | Warning | An output pin is linked to a product output but no program ever assigns it | The physical output can never change state | Output driven by a scene or by another block through the same link |
| `logic-flag-never-cleared` ✔ | LOG | Warning | A flag is set by some program but cleared by none | The flag latches on and the logic never returns to its earlier state | One-shot latch is the intent (e.g. "alarm has fired") |
| `logic-counter-never-reset` ✔ | LOG | Warning | A counter is incremented but never reset or assigned | The count grows without bound and never returns to a known state | Lifetime counter (operating hours, pulse totals) is the intent |
| `logic-self-trigger` ✔ | LOG | Warning | A program is triggered by a variable it also assigns, and that variable is neither a timer nor a counter | Risk of an oscillating or endlessly retriggering loop | Deliberate self-terminating pattern (assign a different value than the trigger) |
| `logic-duplicate-program` ✔ | LOG | Warning | Two programs in the same block carry identical events and commands | One of them is redundant | Deliberate duplication kept for readability |
| `logic-case-no-branches` ✔ | LOG | Warning | A case/switch node carries no case branches | The switch does nothing | Under construction |
| `logic-case-value-foreign` | LOG | Warning | A case branch tests a value that is not one of the switch variable's enum values | The branch can never be taken | Enum has been re-typed and the branch is kept for a future value |
| `logic-block-locked-content` ✔ | LOG | Warning | A locked block or product carries content edited after locking | The lock no longer reflects the state it was meant to protect | Lock applied after the edit, deliberately |
| `enum-def-empty` ✔ | LOG | Warning | An enum definition declares no values | No variable of that type can hold a meaningful value | Type being built |
| `enum-def-single-value` ✔ | LOG | Warning | An enum definition declares exactly one value | The variable can never change | Deliberate constant |
| `scene-unreferenced` ✔ | SCN | Warning | A scene resource is not reachable from any program or link | The scene can never be activated from the installation | Activated from the controller app or an external integration |
| `scene-all-off` ✔ | SCN | Warning | Every member of a scene sets its output off / zero | The scene is an "all off" scene, or an unfinished one | "All off" is a normal, deliberate scene |
| `scene-long-delay` ✔ | SCN | Warning | A member row carries an unusually long delay or ramp time | The installation appears unresponsive when the scene runs | Deliberate slow fade or staged sequence |
| `addr-dimmer-channel-duplicate` | ADR | **Error** | Two LED-dimmer channels claim the same channel id | The two channels are indistinguishable to the controller | — (nearly always a mistake, still the user's call) |
| `addr-unassigned` ✔ | ADR | Warning | A wired terminal has no data-line address | The terminal cannot be reached by the controller (also reported as *Mangler Adresse*) | Product placed but not yet addressed on site |
| `addr-module-partial` ✔ | ADR | Warning | A data-line module is only partly used | Often correct, but a nearly-empty module can mean a mis-addressed product | Spare capacity reserved on purpose |
| `addr-module-mixed-locality` ✔ | ADR | Warning | One module serves terminals in many distant localities | Makes fault-finding on site harder | Unavoidable in a retrofit |
| `addr-wireless-channel-shared` | ADR | Warning | Two wireless elements use the same channel address | Both devices react to the same command | Deliberate ganging of two devices |
| `addr-wireless-not-commissioned` ✔ | ADR | Warning | A wireless product carries no serial number, or a placeholder one | The device cannot be bound to the installation | Product entered during planning, commissioned later |
| `addr-dimmer-channel-unassigned` ✔ | ADR | Warning | An RS485 LED-dimmer channel has no channel id | The channel cannot be addressed | Channel reserved / assigned during commissioning |
| `addr-modem-phonenumber-blank` ✔ | ADR | Warning | An SMS modem entry carries no phone number | The alarm/notification path is dead | Recipient list filled in at hand-over |
| `addr-modem-phonenumber-malformed` | ADR | Warning | A modem telephone slot holds a number that is not 3–20 characters, without whitespace, beginning with a country code | The modem cannot dial that recipient, so an alarm never reaches them | Number written for a local dialling plan: the length ceiling, the whitespace ban and the country-code requirement are OpenVisual strictnesses the vendor itself does not enforce, so authentic vendor files carrying country-code-less numbers now warn |
| `dev-dimmer-range-inverted` ✔ | DEV | Warning | A dimmer's minimum level is at or above its maximum | The dimming range is empty or inverted | Deliberate fixed-level operation |
| `dev-dimmer-max-zero` ✔ | DEV | Warning | A dimmer's maximum level is zero | The load can never be lit | Channel disabled on purpose |
| `dev-dimmer-load-mode-auto` ✔ | DEV | Warning | A dimmer with a known load type is left on automatic load mode | Automatic detection can mis-drive LED loads | Automatic is the correct choice for the installed load |
| `dev-shutter-traveltime-zero` ✔ | DEV | Warning | A shutter's travel time up or down is zero | Position control cannot work | Times measured and entered during commissioning |
| `dev-setting-default` ✔ | DEV | Warning | A device setting is still at its factory default in an otherwise configured product | The device may not have been commissioned at all | The default is the correct value |
| `dev-backup-missing` ✔ | DEV | Warning | A state variable that must survive a power failure is not marked for backup | The installation returns to its initial state after an outage | Restart-to-initial-state is the intent |
| `dev-inivalue-overwritten` ✔ | DEV | Warning | An initial value is assigned by a program on every start | The initial value is meaningless | Harmless redundancy |
| `fb-short-press-below-default` | LOG | Warning | Revision `1.2.03.d` with *Max tid for kort tryk* set BELOW the block's own 0,4 s default | Short presses stop registering reliably. **The trap worth shipping:** `1.2.03.d` is the revision that `1.2.03.c`'s own remedy recommends as its fix, so a user following one piece of advice lands squarely on this one — which is why the row exists at all. That cross-reference is in the English diagnostic and not in the Danish sentence, which states the condition: a user-facing message that explains the catalogue's internal cross-references is telling the reader about the tool rather than about their project. **A conjunction, so both halves are excluded separately** — the revision alone is a perfectly good block, and the low value alone is unremarkable on any other revision. **The boundary is inclusive:** 0,4 s IS the default, so only something strictly below it reports. Value and default are quoted in MILLISECONDS, the unit the file stores, which also keeps the sentence on whole numbers | The installation wants a shorter short-press window and accepts the unreliability |
| `fb-revision-defective-confirmed` | LOG | **Error** | The project embeds a block revision the MANUFACTURER confirmed defective — `1.1.01.c`, `6.3.02.d`, or `6.3.04` below revision `b` | A defective revision embedded in the project is defective on every firmware: no controller upgrade rewrites it, which is why the row declares no `FixedIn` and no target can withhold it. **Error while the community-reported set is a Warning, and the EVIDENCE axis is the difference** — the two grades are the only thing that tells a reader which population a finding came from. **What "confirmed" means, so the grade is not taken for more than it is:** LK acknowledged the defect, and for `6.3.02.d` supplied the fix; nobody measured the behaviour on v3, and the source labels all three generation-unknown. **`6.3.04` is not a bare type:** the source names it without a letter, but its remedy — replace with `6.3.04b` or later — makes the affected revisions everything BELOW `b`, namely `a` and the version-less form. **No `RequiresLibrary`, which is what makes the row shippable:** a placed block carries `master_type` and `master_version` in the `.vis`, so the embedded revision is decidable with no library; comparing a block's BODY is `logic-block-locked-content`'s job. Matched as exact pairs, never as types — the corpus embeds `1.1.01/e` ten times, one letter from the affected `1.1.01/c` | — (the manufacturer confirmed the revision defective; the remedy is to replace the placed block) |
| `fb-revision-defective-reported` | LOG | Warning | The project embeds one of the eight block revisions the COMMUNITY reported defective | Same subject as `fb-revision-defective-confirmed`, different evidence, and that is exactly why they are two rows: one row would have to grade both populations at one confidence, either overstating eight field reports or understating three the manufacturer acknowledged. **The Danish sentences differ too** — one says *bekræftet*, this one *rapporteret* — so a reader knows which population a finding came from without opening this catalogue. **Why a row no authentic file triggers still ships:** these reports are mostly v2-only, so a v3 project reaches such a revision only by having been MIGRATED from v2, which is precisely the case where nobody remembers which revisions came along. Eight rather than eleven — one source row names no revision letter, one is a CO-OCCURRENCE of two types rather than a revision, and one names a revision the library currently ships and the corpus carries. Matched as exact pairs: `1.2.03.c` sits one digit from the corpus's `1.2.04`, and `1.4.03.b`/`1.4.06.a` one from its `1.4.02` | The block works in this installation, which is the ordinary case for a community report |
| `fb-holiday-input-custom-block` | LOG | Warning | A **user-authored** function block carries a holiday input pin | One field report has the upload to the controller failing against an HW 7.1 controller, and no fixed release is established. Warning rather than Error on the single-report row, the same grading `product-3key-upload-abort` takes. **Not `logic-holiday-schedule-firmware`:** that row is the project depending on the holiday schedule at all and narrows away on firmware 3.3.21; this one is a custom block carrying a holiday INPUT and has no fix to narrow on. A project can draw both, and they say different things. **"Custom" is `fb-user-authored`'s population, read through the same predicate** — a vendor block whose flag was stripped keeps its `master_name` and is not custom. **The input container only:** an authentic file carries a holiday resource in each of a block's four containers, so walking the block rather than its input pins would report one whose inputs hold none | The upload works, which is the ordinary case for a single-report defect |
| `rs485-dimmer-scene-multi-off` | SCN | Warning | One scene commands **several** affected RS-485 LED dimmers off at the same time | Only one of them can respond: the quick successive channel commands cross-talk, so the rest silently stay on. **"Off" is decided from the VALUE, not from a word** — a `scene_dimmer` row carries a `dimming_value` and never an on/off token, so off means zero, the same reading `scene-all-off` uses. Zero is also the legal floor `scene-dimming-out-of-range` accepts, so every row involved is a perfectly valid row; the condition is how many valid rows fire together, not that any one of them is wrong. **"Several" is counted over DIMMERS, not over member rows:** a dimmer has two channels and each can carry its own row, so a row count would report one device commanded off on both channels, which is one device responding and is the case that works. One finding per scene, with the count, because the scene is the thing to split up | The installation tolerates only one dimmer answering, or the dimmers have been re-flashed |
| `rs485-dimmer-scenario-recall` | SCN | Warning | An affected RS-485 LED dimmer is driven through scenario recall | **The user cannot fix this from the application, which is exactly why the row exists.** The fix is *dimmer* firmware 01.01.40, which itself needs controller CTR.R.03.03.44, and an upload from Visual never applies dimmer firmware — so without the finding nobody learns the device needs re-flashing. **It declares NO `FixedIn`, and the absence is the point:** the narrowing context compares a CONTROLLER version, and a controller at CTR.R.03.03.44 still has an unpatched dimmer, so narrowing on that release would withhold a finding that still holds. Both versions are in the sentence instead. **Driven means a scene MEMBER ROW exists** under one of the dimmer's channels — mere placement is `rs485-dimmer-firmware-link-errors`, and an authentic file carries a dimmer whose scene containers are empty, so the distinction is measured. One finding per dimmer: two scene rows on one device are still one device to re-flash | The dimmer has already been re-flashed, which the project file cannot record |
| `rs485-dimmer-firmware-link-errors` | DEV | Warning | The project places the RS-485 LED dimmer `_0x4409` | The vendor reports persistent link and upload errors on controller firmware below 03.03.33, so the installation can fail to commission or to stay connected. **It narrows** on a declared firmware target, inclusive at 03.03.33, at `VendorRecommendation` confidence — the vendor states the release fixed it and this repository has not verified that. **Three rows can fire on one dimmer, and that is design rather than duplication:** `rs485-bus-installation` is a statement about the BUS the project puts something on, `rs485-dimmer-powerfail-level` about how THIS dimmer is configured, and this one about the CONTROLLER FIRMWARE the installation runs. A reader who fixes one has not addressed the others. The catalogue holds only one RS-485 LED dimmer, so the identifier check guards against a future second rather than discriminating among today's | The controller already runs 03.03.33 or newer, which the caller can state as a profile target |
| `logic-holiday-schedule-firmware` | LOG | Warning | The project uses the v3 holiday (*helligdag*) schedule | The vendor states it did not work **at all** below controller firmware 3.3.21, so an installation on older firmware silently gets no holiday behaviour whatsoever. **This is the catalogue's first row that narrows on a declared firmware target:** it reports with no target stated, and a profile declaring 3.3.21 or newer withholds it — narrowing context withholds, it never enables. The bound is inclusive, and its confidence is `VendorRecommendation` rather than a measurement, because LK *claims* the release fixed it and this repository has not verified that. One finding per project: the decision is a single firmware upgrade for the installation, which four holiday resources do not make four of. The version is in the sentence rather than in an argument slot, being a constant of the defect rather than a fact read from this project | The controller already runs 3.3.21 or newer, which the caller can state as a profile target |
| `logic-block-recursive` | LOG | **Error** | A function block's program path leaves the block and comes back — a recursive call | The recursion works perfectly in the simulator and does **nothing at all** on the controller — the worst shape a defect can take, because it tests clean and then fails silently in the field. **Not `logic-self-trigger`:** that row reports one program triggered by a variable it also assigns, and the ring it finds *runs* and is aborted; this one never starts. A reader meeting both tells them apart by exactly that. The path must LEAVE the block: a block's own programs signalling each other over its internal settings are how the vendor's shipped library blocks are built, and are not a call. Both exclusions come from one rule — an edge joins two different blocks — so nothing is reported twice. No firmware fixes it: the bound is declared with no `FixedIn`, so no target withholds it | — (a block that can call itself cannot be intended: the controller simply does not run it) |
| `dev-inivalue-out-of-range` | DEV | Warning | A percent-typed resource's initial value is outside 0–100 % | An initial value no physical unit can reach — 150 % relative humidity — is carried, rendered and shipped to the controller without a word from any layer of the vendor tool. Measured on this family: `inivalue="150.00"` loads, renders as *Fugtighed = 150,0% RH* and survives a plain resave | Vendor help states the range but **nothing enforces it** — not on load, display, commit or save — so both bounds are declared `VendorRecommendation` and a deliberate out-of-range placeholder is the author's to keep. Scoped to the two percent-typed kinds only: `resource_light` is a 0–60,000 lux value and is not checked here |
| `name-empty` ✔ | DOC | Warning | A locality, product, terminal, block or variable has no name | The element is unidentifiable in the reports and on site | Structural container the user never sees |
| `name-default` ✔ | DOC | Warning | A name is still the generated/template name from insertion or catalogue import | The reports read as unfinished; two products share a meaningless name | Template name is descriptive enough |
| `name-duplicate-siblings` ✔ | DOC | Warning | Two siblings share a name (two localities, two products in one locality, two pins on one block) | Ambiguous references in reports, and on site | Deliberate: two identical devices in one room |
| `name-id-code-duplicate` ✔ | DOC | Warning | Two products carry the same identification code | The code no longer identifies one product in the documentation | Codes intentionally per product *type* rather than per unit |
| `name-cable-number-duplicate` ✔ | DOC | Warning | Two products or terminals carry the same cable number | The wiring documentation is ambiguous | One physical cable legitimately serves several terminals |
| `name-power-group-variant` ✔ | DOC | Warning | Light-group values differ only by case/spacing (`Stue` vs `stue`) | The reports group the same physical circuit under two headings | Deliberately distinct group names |
| `name-note-missing` ✔ | DOC | Warning | A function-block input that appears in the reports carries no note | The function report cannot describe what the function does | Name alone is self-explanatory |
| `doc-documentation-tag` ✅ | DOC | Warning | *Mangler Id-kode* — a product carries no identification code | The product cannot be identified in the documentation | Codes not used in this installation |
| `doc-power-group` ✅ | DOC | Warning | *Mangler Lysgruppe* — a product carries no light group | The circuit cannot be grouped in the reports | Not a lighting circuit |
| `doc-cabletype` ✅ | DOC | Warning | *Mangler Kabeltype* — a product carries no cable type | The wiring documentation is incomplete | Cable type documented elsewhere |
| `doc-cablenumber` ✅ | DOC | Warning | *Mangler Kabelnummer* — a product carries no cable number | Cables cannot be traced from the documentation | Cables not numbered in this installation |
| `doc-position` ✅ | DOC | Warning | *Mangler Placering* — a product carries no placement text | The product cannot be found on site from the reports | Placement obvious from the locality |
| `doc-not-linked` ✅ | DOC | Warning | *Ikke forbundet* — a wired terminal owns no link | The terminal does nothing | Spare terminal |
| `doc-cable-colour` ✅ | DOC | Warning | *Mangler Ledningsfarve* — a wired terminal carries no wire colour | The wire cannot be identified in the enclosure | Single-colour cable in use |
| `doc-address` ✅ | DOC | Warning | *Mangler Adresse* — a wired terminal has no decodable data-line address | The terminal cannot be commissioned | Not yet addressed |
| `doc-project-info-blank` ✔ | DOC | Warning | Project, customer or installer information is empty | Every report masthead renders `--` | Internal project never handed over |
| `doc-no-enduser-products` | DOC | Warning | No product is flagged for end-user documentation | The Funktionsdokumentation report comes out empty | Only installer documentation is wanted |
| `product-3key-upload-abort` | PRJ | Warning | The project places the 3-key push button `_0x106` "Mini Modul 3 tryk" | One field report has the upload to the controller aborting partway and leaving it in *fejltilstand*, recoverable only by reloading factory-default firmware. **Warning and not Error, deliberately:** no fixed release is known, which on its own argues Error — but the report is single-source, and suppression is foreclosed, so an Error would be permanent and undismissable for every installation that demonstrably works, with not even a narrowing firmware context to escape through. Revisited on a second report or a known fix. **The subject was identified by measurement, not by name:** the catalogue holds two 3-key products, and the other — `_0x2132`, the FUGA *Betjeningstryk* — is the one the English source name points at. The reporter's own fix decides it, three separate 1-key push buttons in its place: `_0x104` "Mini Modul 1 tryk" is the catalogue's only 1-key product, and the FUGA family runs 2/4/6 keys with no 1-key member, so the swap is possible only inside Mini Modul. The recovery procedure is installation advice and stays out of the sentence | The installation works, which is the ordinary case for a single-report defect |
| `capacity-input-modules` | PRJ | **Error** | More INPUT data lines are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| `capacity-output-modules` | PRJ | **Error** | More OUTPUT data lines are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| `capacity-input-addresses` | PRJ | **Error** | More INPUT terminals are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| `capacity-output-addresses` | PRJ | **Error** | More OUTPUT terminals are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| ~~`capacity-addresses`~~ | PRJ | **Error** | **RETIRED — split into the two address rows above.** It counted the terminals of ONE direction but named neither, so a project over on both produced two findings a reader could tell apart only by their numbers — and a number cannot say which direction it counts. Its own entry argued the direction is not an argument "because a direction is a word and an argument is data", which is right and is why the fix was to split the row rather than to put a word in a slot. The id stays reserved and is never re-pointed at a successor | — |
| ~~`capacity-modules-exceeded`~~ | PRJ | **Error** | **RETIRED — split into the three rows above.** It covered all three quantities under one Danish sentence, *"Projektet bruger {used} af {limit} moduler."* | That sentence was false of the terminals count: 200 terminals over a 128 limit read as "uses 200 of 128 modules". Its entry claimed the arguments said which quantity, but the only arguments were `used` and `limit`. The rule also looped per direction, so it could emit two findings against a declared `OneFinding`. The id stays reserved and is never re-pointed at a successor | — |
| `capacity-wireless-exceeded` | PRJ | Warning | More than 64 wireless products are bound to one controller | Response time degrades. The vendor states a RECOMMENDATION, not a hard limit — *"En IHC controller bør maksimalt forbindes til 64 IHC Wireless produkter"*, explicitly *"af hensyn til en fornuftig responstid"*. **Corrected from Error:** an Error's consequence must hold whatever the author intended, and the devices do bind — the system merely answers more slowly | Planning document, not an upload; a deliberately large installation whose response time the author accepts |
| `capacity-wireless-links-per-unit` | ADR | Warning | One wireless unit carries more links than the controller supports — 32 ordinarily, 64 on a combi unit | Response time degrades on that unit. **The one row of this group with an ENABLING posture, and it is the only such row among the errata batch:** every other one reports with no context and can only be WITHHELD by a declared firmware target, because its condition is in the file. This is not an erratum — the ceiling is a CONTROLLER capability — so with no controller declared there is no ceiling to be over and the row is ABSENT rather than measuring against a guess. That is also why the limit is a member on `ControllerCapabilityLimits` rather than a `DeclaredThreshold`: a threshold is for a project-only cap that needs no controller, and this is the converse. **The combi ceiling is its own declared number**, not a multiple of the ordinary one — that today's two figures differ by a factor of two is an observation, not a rule the vendor states. Warning for the same reason as `capacity-wireless-exceeded`: a recommendation rather than a refusal, and the field evidence is contradictory, with degradation reported well below the published figure. One finding per unit, because two overloaded units are two units to re-plan | The installation accepts the response time, or the controller in use carries more |
| `capacity-scenarios-per-receiver` | ADR | Warning | One wireless receiver takes part in more scenarios than the controller carries — 32 | The other half of the same vendor recommendation as the row above, and it shares its ENABLING posture: with no controller declared there is no ceiling to be over, so the row is absent rather than measuring against a guess. **A receiver is a wireless product that OWNS a scene container**, which the file decides rather than a product list to keep current — a wireless unit with no container cannot be commanded into a scene at all, so it is not a receiver and has no ceiling; the corpus carries one such product. **Counted in scene MEMBER ROWS, not containers:** a two-channel receiver has two containers and can still take part in one scenario, so containers are not the quantity the controller bounds. One finding per receiver | The installation accepts the response time, or the controller in use carries more |
| `capacity-modem-multiple` ⊘ | PRJ | **Error** | The project contains more than one modem | The controller binds one modem, so the extra entries can never be commissioned. **Neither editor will author this state** (measured live 2026-08-11): IHC Visual refuses the second insert with *"Modem er allerede indsat. Der kan kun indsættes et modem i projektet"* and OpenVisual with *"Et projekt må højst indeholde ét modem…"*, each leaving the tree unchanged — so a file carrying two can only have arrived by import or by hand, which is exactly why the file-level check still earns its place | — (the limit is the controller's; no intent makes a second modem work) |
| `capacity-resources-high` | PRJ | Warning | The project's resource count reaches or passes the controller's limit | Further growth will fail late, at upload time | Deliberately near-full installation |
| `struct-product-no-terminals` ✔ | PRJ | Warning | A product carries no terminals at all | Nothing on the product can be wired | Product family that genuinely has none |

✅ = implemented today, with the fixed Danish label shown; these eight are the seed set already
reported in the Fuld-mode reports' *Fejl i dokumentation* section.

---

## 5b. Informational findings — worth knowing, nothing to repair

Every row here is an **Information** finding, and nothing in this section is being called a mistake.
The project is well-formed and every one of these rows would fire on a project an installer is
entirely happy with. What each reports is something true of the installation that the file does not
make visible on its own, and that costs more to discover later than to read now.

They report four kinds of fact, which is worth knowing before reading the table as one list:

- **What a placed device is or does** — a datasheet property, a signalling polarity, a wiring
  requirement, a behaviour after a power failure. These are the largest group.
- **What the vendor has said about a device's future** — discontinued, phased out, or not currently
  convertible. A lifecycle statement can go stale; each row's entry says where it came from.
- **What the project has asked for or declined** — how large a retention budget it wants, or a
  fault-reporting capability it has not wired up.
- **Where a function block came from** — user-built, provenance stripped, or claiming a library
  revision the installed library does not hold. These carry an archiving consequence rather than a
  repair.

**This section carries no *Why it may be fine* column, and the omission is the point.** §5's rows are
advisory because the author has to judge them: the column states the legitimate reading that makes a
finding acceptable. An Information row asks for no such judgement, so there is nothing for that column
to say — a cell reading "it is always fine" beside every row would be noise dressed as analysis. What
replaces it is the reason the fact is worth carrying at all.

None of these rows blocks anything: an Information finding never sets
`ProjectValidationResult.IsValid` false, so no save, export or upload is withheld on account of one.
Several of them fire together on one device, deliberately — a smart sensor reports both what it needs
in order to work and what becomes of it in a KNX conversion — and each entry names the rows it
overlaps with, so a reader meeting two findings about one product can tell they are two facts rather
than one repeated.

| Id | Cat | Sev | Finding | Why it is worth knowing |
| --- | --- | --- | --- | --- |
| `fb-pulse-constant-default` | LOG | Information | A pulse-counting block still carries the library's default scaling constant | The constant must match the physical meter's rating plate, which the project cannot verify — an unchanged default silently mis-scales every reading if the meter differs. **An instance whose constant was changed reports nothing:** somebody has already made the decision this row asks for, and there is no fallback that reports it anyway. **The number in the sentence is the instance's own**, not the declared default: binding the threshold would render *"regner med 100 impulser"* at a project that set 250. The constant is read from the block's `settings` group, not `internalsettings`, which on this block holds only timers and scratch integers |
| `fb-pir-dusk-gated` | LOG | Information | A PIR block's twilight input is wired | The block reacts to motion only while that input is ON, so a source that never turns ON makes it appear dead — a wired-but-inert `Skumring` pin reads in the field as a broken PIR, and nothing is broken. **The message is a consequence to verify, not a fault:** whether the linked source ever turns ON is a runtime question about another part of the installation, which the file cannot answer. An *unwired* pin gates nothing and is not reported — every instance of this block type ships the pin, so wiring rather than existence is the condition. **Silent by construction on a block whose `master_type` was stripped**, which is `fb-provenance-rewritten`'s population: the rule cannot know which master an unlocked block came from, and guessing from pin names would report any block naming a pin `Skumring` |
| `fb-master-version-differs` | LOG | Information | A block is frozen at a revision the library does not hold, while holding the type | Behaviour can change materially between revisions of the same nominal block, and swapping is a manual re-commissioning job rather than a drop-in. **It fires in both directions** — older *and* newer than the library are the same finding, since what matters is that the two disagree; reporting only "behind" would say nothing about a project carrying a revision the installed library has since dropped. **The freeze rule itself is deliberately not a row:** "a library upgrade never touches a placed instance" is true of every project and would report every block in every file. A library may hold several revisions of one type, so a block matching *any* of them is in sync, and the message names them all |
| `fb-master-missing-from-library` | LOG | Information | A block references a master type the available library does not contain at any version | Whole block types are dropped between Visual releases with no announcement, so a project depending on one that is gone cannot be rebuilt from a clean install. **Skipped, never guessed:** the row declares `RequiresLibrary`, so a caller with no library to compare against gets silence rather than a finding derived from an absent fact — the same posture the capacity rows take without controller limits. A type the library holds at a *different* version is `fb-master-version-differs`, never this row; a block claiming no type at all belongs to the two provenance rows |
| `fb-provenance-rewritten` | LOG | Information | A vendor block's provenance trio has been stripped while its name survived | Without the trio the block cannot be checked against errata or against a fixed revision, and the operation is irreversible — so its `.ifb` should be archived with the project. **The exact complement of `fb-user-authored`:** that row needs both provenance halves absent, this one needs the name present and flag, type and version all gone. No block reports both, and together they cover every block that arrived as a *file* — a downloaded block, or one exported with *Gem funktionsblok*, keeps `master_name` and signs as this row. **The cause is named as likely, never as certain:** unlock and save-as both produce this shape and the file does not always distinguish them |
| `fb-user-authored` | LOG | Information | A function block was built from scratch | No Visual install will ever re-supply it, so losing its `.ifb` means the block can never be re-inserted elsewhere — the `.vis` carries its contents but not a reusable file, and the `.ifb` is worth archiving with the project. **Both provenance halves must be absent:** unlocking a vendor block or saving one to the library strips the vendor flag but *keeps* `master_name`, so the flag alone does not mean "not an LK block" — a surviving `master_name` is `fb-provenance-rewritten`'s population instead. Expect a large corpus load: user-built blocks are ordinary rather than exceptional, which is the Information tier doing its job |
| `backup-retained-count` | DEV | Information | The project asks the controller to retain resource values across a power failure | The retention budget is a controller-side ration, and this count is what will be measured against it at upload. **No verdict and no threshold:** the ceiling is a controller question this row's source does not establish, so no limit is declared and no controller context is asked for — the row states the number and stops, and does not claim the project exceeds anything. One finding for the project, because the count is the fact and anchoring per resource would nag. **A terminal is not counted:** an output terminal ships `backup="yes"` too, but it is not a `resource_*` element and whether its value draws on the same ration is unestablished |
| `rs485-dimmer-fault-unwired` | WIR | Information | An RS-485 LED dimmer has no linked fault resource | The product can report overcurrent, overvoltage, overheating and load failure, and this project throws that capability away — a fault never surfaces in the program. **Keyed on the element tags, never on the Danish names:** the four tags are language-independent and not user-editable, while the *Fejl - …* strings beside them are ordinary `name` values, so a name-keyed rule would both miss a renamed flag and report a dimmer whose ordinary resource happened to be named like one. **The resources are per channel, not per product** — a two-channel dimmer exposes eight, and the condition is "none of the eight". One linked flag is enough to make the row silent: partial wiring is a design choice |
| `rs485-bus-installation` | ADR | Information | The project puts something on an RS-485 bus | The bus carries installation rules no part of the file records: at most 32 components, termination at the end of the string, and beyond about 10 m the cable shield bonded to the supply's 0 V. Sporadic dimmer log entries usually mean cabling rather than a failing module. **Termination is a disjunction and both branches stay in the sentence** — the SMS module's built-in terminator if one sits last, *or* a ≈120 Ω resistor; dropping the SMS branch would tell an SMS-modem project to fit a resistor the vendor says it does not need. **The 10 m governs bonding the shield, not whether the cable is shielded** — shielded cable is required unconditionally. Deliberately overlaps `capacity-rs485-exceeded`: this row publishes the ceiling as a fact, that one reports the breach — **one population and one number behind both**, so the bus a project is told about is the bus it is measured against. The population is the shared `Rs485Products` walk, the LED dimmer, the **voice modem** and the SMS modem alike; the ceiling is one declared constant the two thresholds bind |
| `rs485-dimmer-powerfail-level` | DEV | Information | The project places an RS-485 LED dimmer | It does **not** retain on/off across a longer power failure: its channels come back at the configured level, factory default 100 %. A program that assumes "off after an outage" has to assert it explicitly — the reverse of what every other output does. **No exclusion is available** and the entry says so: the behaviour belongs to the product, not to a setting the file could inspect, so every placed dimmer reports. The factory level is bound from the declared threshold rather than written into the sentence, so the number cannot exist in two places and disagree |
| `controller-link-budget` | ADR | Information | The project uses a Controller Link | It moves at most 16 on/off signals per direction, occupies terminals on both controllers whether or not the signals are used, and **cannot carry an analog value at all** — so a design needing a measurement on the other controller needs a different mechanism entirely. **The sentence does not quantify the terminals:** the familiar "16 in and 16 out on each controller" holds only once a direction is fully populated (two OUT products against one IN, since an input module has 16 inputs and an output module 8), and the file cannot tell the reader whether it is. No symmetry is claimed either — the two products declare 8 and 16 respectively. The 16 is declared `Authored` and carries its module arithmetic: two community reports, no vendor publication |
| `product-sensor-pulse-input` | ADR | Information | The project places a smart sensor | It is **not an analog input**: it encodes its reading as a timed pulse train on a plain 24 V line and needs the 24 V/3 mA input module, so pairing it with the older 24/24 module silently fails — that module does not speak the pulse protocol. **The row states the requirement and checks nothing:** which physical module the sensor lands on is not in the file (the documentation modules are optional and bind nothing), so compliance is not decidable and the row does not pretend it is. The same six devices also report `migration-untested-product` — what a sensor needs to work, and what becomes of it in a conversion, are different questions with different readers |
| `product-pir-alarm-polarity` | DEV | Information | The project places an alarm-grade PIR | It **breaks** its output on motion — normally-closed by design — which is the opposite of what a lighting block expects, so reusing one for lighting silently inverts the trigger sense and the signal typically needs inverting in the program. **The ordinary PIR `_0x210e` is deliberately not reported:** normally-open is the expected case for it, and it is in two committed projects, so anything looser than an exact identifier match would report vendor-authored output. The device's lag and daisy-chain clauses are installation advice and stay out of the message |
| `product-keypad-codes-local` | DEV | Information | The project places a code keypad | Its access codes live in the keypad itself, not in the project or the controller — so a project backup does not carry them, and a handover or disaster-recovery plan that assumes otherwise is wrong. The device's recovery folklore (a second keypad further along the daisy chain) is installation advice and stays out of the message: the row corrects an assumption rather than telling an installer how to work. The same device also reports `migration-untested-product`, which is a second, independent statement about it |
| `migration-untested-product` | PRJ | Information | A placed product is one the vendor states cannot currently be reused in a conversion to KNX | The conversion cost of a project is decided by exactly these products, so the reader sizing that job needs them named. **The vendor statement is provisional and the sentence keeps it so:** the source calls its own contents *"foreløbige konklusioner, som vi arbejder videre med at forbedre og validere"*, and its clauses read "cannot currently be replaced or used, still being investigated". Only the sensor group carries even a recommendation to convert. The reusable half — pushbuttons, link-10 cabling, PIR on/off — is not a finding and fires nothing. The sensor identifiers are a **proxy** for the letter's *"lux value from PIR"*, which names a capability rather than a product list |
| `product-sounder-not-alarm-approved` | PRJ | Information | A placed sounder is one the vendor records as not approved for statutory warning systems | If programs drive it as life-safety signalling, that is a compliance question the reader owns. **The row does not decide whether the sounder is used for life safety** — the file cannot: a sounder driven by a program is a sounder driven by a program, whatever the installer meant it to signal, and no attribute records that intent. So it states the approval status and stops, which is also why it is Information rather than a Warning: a Warning would ask the author to judge the very thing this row is not asking about |
| `product-ir-generations-mixed` | PRJ | Information | The project declares both IR transmitter generations | They need mutually incompatible receivers — 507N0034 against 506D6501 — and no one receiver serves both, so the file is silently declaring a conflict the installation cannot resolve. **Co-occurrence is the only mechanisable form of the question**, and that is the format's doing rather than a compromise: the receiver is what the two disagree about, and a receiver is not a product, so it never appears in a project file. Either transmitter alone is an ordinary installation with an ordinary receiver behind it. The `Beo4`/`Beolink` keymap products are not yet in the trigger — their identifiers have to be resolved first, and an under-scoped set would report some mixed installations and not others |
| `product-discontinued` | PRJ | Information | A placed device is one of the nine the vendor records as discontinued | A like-for-like replacement may be unobtainable, so it has to be planned rather than assumed. One finding per instance, because each device is separately replaceable — unlike the family phase-out below, which is one decision for the whole project. **Keyed on (root element, `product_identifier`) together**, since an identifier alone is not unique in this catalogue. **`_0x210d` is deliberately NOT in the set:** its page records that the *receiver* `507N0034` is a spare part only, and a receiver never appears in a project file — the remote is covered by `product-ir-generations-mixed` and `migration-untested-product` instead |
| `product-wireless-phaseout` | PRJ | Information | The project holds IHC Wireless products | The vendor has announced a sales stop for the whole IHC Wireless family during 2026, with the execution date still to be announced — so a project standing on that hardware owns a procurement decision before spares stop being orderable. **Installed devices are not said to stop working:** the announcement is a sales stop, and the harm is to replacements. One finding for the project, carrying how many, because the decision is the project's rather than any one device's. A lifecycle statement can go stale — re-read the vendor status page before changing the wording or the year |
| `product-s0-instrument-only` | ADR | Information | The project places an S0 metering input | The terminal is a galvanically separate read-out instrument: its count cannot feed a function block, and its pulse pair cannot share a terminal with an ordinary 24 V input. Automation designed on that count is unexpressible, and the limitation is the terminal's rather than anything the project got wrong |

---

## 6. Deliberate non-findings

Conditions that look wrong but are normal in IHC projects, and must **not** be reported:

- **Fan-out** — one product input feeding several block inputs, or one block output feeding several
  product outputs. Vendor-authored projects do this routinely.
- **An unwired scene member row** — the file format admits it, so a file carrying one still loads and
  is never a `scene-bijection` error. ⚠ The claim that *the vendor tooling authors it* is now
  **unsupported**: neither member dialog carries an output selector, a member exists only as one half
  of a reciprocal pair, and no oracle in `tests/testdata` contains one. `scene-member-unwired` moved to
  §4 on that evidence.
- **A one-sided documentation field** — a filled cable type with a blank cable number is covered by
  the per-field documentation checks; the *combination* is not itself a finding.
- **A locality holding no blocks**, or a project holding no scenes at all — absence of an optional
  feature is not a finding.
- **Unused catalogue products** — the catalogue is a library, not an inventory of what must be used.
- **An unassigned data-line address** — legal while unconfigured; `addr-unassigned` is a warning, not
  a `dataline-address-*` error.
- **A `helpfile` attribute naming a file that does not resolve** — the stated consequence is FALSE. `helpfile`
  is never read: help resolves the document from the block's own `master_type`, proven by two tamper oracles in
  which a nonexistent path and a different existing path both opened the same correct document. On products the
  attribute is populated zero times across every committed project and all 100 catalog `.def` templates. The
  help action works regardless, so `name-helpfile-missing` is not a finding at all — it is not withheld, it is
  wrong.
- **A `<modified>` stamp that looks old** — no predicate can be written for it. The stamp is re-stamped on every
  save and no edit route touches it, so in any saved file it is current by construction and the condition the
  row described cannot arise. This is not an unauthorable state that arrives by import or hand-editing; there is
  no state to detect. `struct-modified-stale` is therefore ruled out rather than deferred.
- **A file that ends inside an open element** — real, but not separately decidable, and already reported.
  An XML parser refuses an unclosed document before the reader can look at it: MEASURED, a project cut off
  inside `<groups>` refuses as `load-not-xml`, whose own text already names truncation as a cause. Telling
  the two apart afterwards would mean matching the parser's exception message, which is a localized .NET
  resource string — a refusal that changes with the UI culture, bought for one Danish sentence. The reader
  keeps its own end-of-document guard and that guard refuses under `load-not-xml` with its precise English
  diagnostic intact, so `load-truncated` is ruled out rather than withheld.

### The eleven conditions deleted in 2026-08 — measured noise

These eleven **shipped**, were measured over the corpus and over an independent real-world project,
and were **deleted by owner ruling** because each condemns the ordinary state of a healthy project.
None of them is to be re-proposed as a finding, and none of the eleven names is to be re-pointed at a
different condition — no entry reserves them any more, so this record is the only guard.

The names are listed so a search for one lands here. **A name in this table is spent**: it identified this
condition, the condition is not a finding, and pointing the name at a different condition would make every
older report and exported findings file lie about what it said.

| Deleted id | Condition | Why it was noise |
| --- | --- | --- |
| `struct-locality-empty` | A locality holding neither products nor blocks | The vendor's fresh template ships ten empty localities; the empty starter project warned about all ten |
| `struct-locality-no-devices` | A locality holding only function blocks | The standard "logic room" pattern |
| `struct-orphan-block` | A function block nothing links to and nothing references | Self-contained clock/timer logic is normal |
| `scene-empty` | A scene with no members | Template-named scenario slots ship inside library blocks |
| `scene-output-also-linked` | An output a scene drives that a follow-link also drives | Scene preset plus follow-link is how combined control is built |
| `link-crosses-locality` | A follow-link whose ends sit in different localities | Central blocks serve several rooms; the row's own disagreement column already said "usually intended" |
| `logic-timer-unused` | A declared timer no program starts | Default-named spare timers inside inserted library blocks |
| `logic-contending-writers` | Two programs assigning one variable from unrelated triggers | Manual plus automatic control of one output is the idiom |
| `logic-variable-unused` | A declared state variable nothing touches | Spare variables ship inside library blocks |
| `enum-value-unused` | A declared enum value nothing references | Fires on vendor-stock enum types the author never wrote |
| `enum-def-unused` | An authored enum type no variable declares | The same stock-type mechanism, and no dialog can bind such a type at all |

**The measurement.** Every one of the eleven fired on files IHC Visual itself authored and accepts:
between two and eight of the eight distinct-lineage normal projects witnessed each id, and together
they were 907 of the 1506 warnings — 60% — in the one independent real-world project measured. A
reader who dismisses six rows in ten stops reading the tenth, which is the cost these rows were
charging the rows that are worth reading.

**What was NOT deleted with them**, and why: the vendor-witnessed `doc-*` rows (the Fuld report's
appendix renders them and vendor parity governs), every Information-tier row (§5b fires on healthy
projects by design and says so), and the configuration-specific rows — `addr-*`, `rs485-*` and the
firmware guardrails — whose volume follows the hardware in the project rather than the shape of
ordinary authoring.

### The 2026-08 Tier-2 pass — three more deleted, four narrowed

The Tier-1 table above removed rows whose whole condition was noise. A second pass over the same
evidence found rows whose condition was **partly** real: each is narrowed by an exclusion decidable
from the file, and the two whose SUBJECT was wrong were replaced by one row over the right subject.

The three ids below are **spent** on the same terms as the eleven: no entry reserves them, and
pointing one at a different condition would make every older report lie about what it said.

| Deleted id | Condition | Why it was noise | Answered now by |
| --- | --- | --- | --- |
| `link-input-unconnected` | A product input pin owns no link | Sixteen spare `Tryk (…)` buttons on plates that were wired and working — a plate ships more terminals than an installation uses | `link-product-unwired`, per product |
| `link-output-undriven` | A product output pin owns no link and no scene names it | Seven pushbutton `LED (…)` indicators on wired plates; those pins are the plate's only outputs, so an outputs-only row reports every such plate | `link-product-unwired`, per product |
| `logic-master-block-modified` | A library block whose `name` differs from the insert name its master identity implies | It compared NAMES, never content: every hit was a descriptive rename, which is what the vendor's own naming guidance asks for. Paired with `name-default` it also gave each reconstructible library block exactly one advisory whatever the author did | Nothing on names. Content divergence is `logic-block-locked-content`'s (library-compared) and a version difference `fb-master-version-differs`' |

And these four conditions are now deliberate non-findings, each carved out of a row that survives:

- **A shipped empty default program** — a block inserted from the library brings a program with
  neither trigger nor command. `logic-program-no-events` now asks for a program that carries WORK a
  trigger could have run, because the finding is about work stranded. A block empty all the way down
  is still `logic-block-empty`'s.
- **A timer re-armed, or a counter stepped, by the program it starts** — a delay and a tally, which
  is what those two kinds are for. Neither oscillates, so `logic-self-trigger`'s stated consequence
  was false of them; it excludes the two kinds and still reports a flag, an output or an ordinary
  variable feeding itself back. `logic-block-recursive` excludes every direct self-edge and is
  deliberately NOT widened to pick these up.
- **A block that starts itself** — a clock, a *Powerup - Altid tændt*, a block woken by its own
  internal timer. `link-fb-input-unfed` says the trigger never arrives, which is simply false of one
  carrying an `event_power` or an `<event>` bound outside its own `inputs` container.
- **A spare terminal on a partially wired product** — the condition the two deleted rows above
  reported. It is not withheld pending better evidence; a device with one wire in it is installed.

## 7. Behavioural requirements

- MUST: A **Fatal error** aborts the operation, naming which one was refused and why, and leaves
  nothing opened, written or overwritten. §4's **Blocks** column publishes that name, and its
  vocabulary is the whole operation set rather than a chosen four — so this requirement cannot
  outgrow the words available to state it.
- MUST: An **Error** and a **Warning** never abort an operation — the project still opens, saves,
  exports and imports.
- MUST: A finding's severity does not depend on which command surfaced it: the same condition is the
  same severity whether it is found at open, at save, at upload or on an explicit verification run.
- MUST: Findings are reproducible and ordered deterministically over the same project, so two runs
  produce the same list in the same order.
- MUST: Each finding names the element it is about, so the user can navigate to it.
- MUST: A warning is phrased as an observation the user judges, not as an accusation.
- MUST: A project with none of these conditions produces an empty list, not a "no problems found"
  pseudo-finding.
- SHOULD: Findings can be filtered and grouped by id, by category and by severity, so a reader can
  work through one class at a time.
- **NOT offered: suppression.** There is no rule-level disable and no per-element "accepted" store, and
  that is a decision rather than an omission — a silenced finding is invisible to the next reader of the
  project, and neither the file nor this SDK has anywhere to record WHO accepted WHAT and why. The
  consequence is stated plainly: most rows are Warnings, and an installation that knowingly accepts a
  whole class of them cannot silence it. The place to revisit this is the findings UI, where the noise
  can be measured on real projects instead of predicted here.

---

## 8. Verification against the live application

Rows below were tested **one at a time against LK IHC Visual 03.04.72.03** while authoring
[`tests/testdata/projects/Project6-Errors.vis`](../../tests/testdata/projects/Project6-Errors.vis)
(2026-08-09). The test is §3's own decision procedure: a state the editor will author is User-sourced;
a state it **refuses** is not, and the row moves to §4. Full evidence per row is in the fixture's
[authoring record](../../tests/testdata/projects/Project6-Errors.md).

**In §5, a row marked ✔ was authored and a row with no mark was not reached.** The eight ✅ rows were
all authored too; they keep the ✅ alone so the implemented-today set stays legible.

**⊘ Refused — reclassified to §4 (9 rows).** `dataline-address-duplicate` (OK disabled on an
`(i brug)` address, A/B-verified), `addr-s0-ticks-missing` (validated on commit, 1–10000),
`dev-dimmer-fade-zero` (clamps to a 200 ms minimum — *family-scoped:* measured on the RS485 LED
dimmer only, a dataline Dimmer product is unmeasured), `dev-write-to-read-only` (no dialog marks a
block variable read-only, and programs are block-local so none can reach a product's read-only
resource), `enum-def-duplicate-name` (*"Vælg et andet navn"*, A/B-verified against a unique name in
the same dialog), `enum-def-duplicate-index` (append-only values, no reorder and no index field),
`scene-duplicate-target` (the same output twice in one scene is rejected; into a different scene it
is accepted — A/B-verified), `scene-member-unwired` (no member dialog carries an output selector; a
member exists only as one half of a reciprocal pair) and `struct-icon-default` (no element-properties
dialog in the application carries an icon picker). Two further rows that had been counted here left the
finding set entirely and are now §6 deliberate non-findings: `name-helpfile-missing`, whose stated
consequence is false, and `struct-modified-stale`, for which no predicate can be written.

**✔ Authored — confirmed User-sourced (47 rows + the eight ✅).** Marked per row in §5. The number
fell with the 2026-08 deletions: eleven Tier-1 rows and three Tier-2 rows were authored here before
they were removed, and `link-product-unwired` — the fixture's three untouched products — replaced two
of the three. Sitting 5 added
the last two device rows: `dev-inivalue-overwritten` (a `Powerup hændelse` program re-asserting a flag's
own non-default `Initial værdi` at every start) and `dev-backup-missing` (control 216 *Gem aktuel værdi*
demonstrably **writes** `backup="yes"`, so the unmarked state of every other variable is a choice, not a
limitation — the fixture carries both sides of that contrast). Sitting 4
added the program-logic set (`logic-program-no-events`, `logic-program-no-actions`,
`logic-subprogram-no-conditions`, `logic-variable-write-only`, `logic-variable-read-only`,
`logic-output-never-assigned`, `logic-flag-never-cleared`, `logic-counter-never-reset`,
`logic-self-trigger`, `logic-duplicate-program`, `logic-case-no-branches`),
the scene set (`scene-unreferenced`, `scene-all-off`, `scene-long-delay`), the enum set
(`enum-def-empty`, `enum-def-single-value`), `logic-block-empty` and
`link-through-empty-block` (both need the default
`Program` **deleted** — every inserted block ships with one), `link-pass-through`,
`logic-block-locked-content` (a locked library block's Navn is disabled but its Initial værdi is not),
and `doc-project-info-blank` (which had to be **cleared**: IHC Visual pre-fills `programmer` with the
Windows user name).

**◐ Untestable or unreachable, not falsified (8 rows).**

- `addr-wireless-channel-shared`, `addr-dimmer-channel-duplicate` — no product dialog exposes a serial
  number or channel address, and `Controller ▸ Link/test LK IHC Wireless produkter` is greyed without
  a controller.
- `logic-case-duplicate-value`, `logic-case-value-foreign` — `Indsæt ▸ Ny case værdi` writes its
  `case_action` under the **left pane's** caret instead of into the selected `program_case`. Four routes
  were driven, including the vendor's own documented gesture (right-click the Case row → *Ny
  case-værdi...*, delivered as real keyboard input); the three that insert at all obey that same rule,
  and a one-variable A/B moves the parent by moving only the left-pane caret. The left pane holds no
  `program_case` in any view, so no caret position lands it correctly.
  `project5-Dokumentation.vis` carries correctly nested branches, so the state *is* reachable in IHC
  Visual: this is an unfound route, not a refusal.
- `doc-no-enduser-products` — **mutually exclusive with `dev-shutter-traveltime-zero`**, which this
  fixture witnesses. The catalogue holds exactly two products with shutter travel times, both airlink;
  IHC Visual itself writes `enduser_report="yes"` on each at insert time (no `.def` declares it), and no
  airlink dialog carries the checkbox that clears it. Any project witnessing a shutter travel time
  therefore carries a product that cannot be unflagged; this row needs a fixture with no shutter product.
- the five controller-limited `capacity-*` rows — out of reach at any practical fixture size.

Two premises the exercise corrected, neither of which changes a row's severity:

- **`dev-dimmer-load-mode-auto` is not a default.** `Belastnings karakteristik` ships as `RC`;
  automatic has to be chosen, so the *"why it may be fine"* is a deliberate choice, not an oversight.
- **`dev-dimmer-max-zero` and `dev-dimmer-range-inverted` are not independent.** A maximum of zero
  forces minimum ≥ maximum, so any witness of the first also satisfies the second.

Three structural constraints worth recording, because each one limits how a row can ever be witnessed —
and the last two also **scope the check an implementer should write**:

- **Addressing one terminal of a multi-button product auto-assigns its siblings** to consecutive
  positions in the same commit. "One terminal addressed, its sibling deliberately not" is therefore not
  authorable on such a product.
- **`dev-backup-missing` is about block variables, not terminals.** *Gem aktuel værdi* (control 216)
  appears on both, but **output terminals ship `backup="yes"`** — every `dataline_output` and
  `airlink_relay` in the fixture carries it, while input terminals have no such attribute and block
  variables default to `backup="no"`. A check that walks every backup-capable element would report the
  whole project; the condition lives on the block variables alone.
- **A user-created enum type can never be bound to a block variable.** `Indsæt ▸ Variable` offers a
  fixed 21 entries and none of them is an enumerator; the list is identical under Input, Output,
  Indstillinger and Interne variable, and it does not grow when project enum types are created. Every
  `resource_enum` in a vendor file therefore comes from a product `.def` or a library `.ifb`. This is
  the measurement that eventually removed the two "unused enum" rows in §6's Tier-1 record: a
  user-authored enum type is unused **by construction**, so those rows fired on every one of them and
  the reader could do nothing about any of it.

---

## Appendix — field evidence: the documentation set of eight is closed

An independent third-party documentation report (`jemi.dk/ihc/docs`) over a real 2022 single-family
installation — 27 pages, ~530 findings across some 25 localities — reports its *Fejl i dokumentation*
section with **exactly these eight kinds and no ninth**. That is the strongest available evidence
that the ✅ set above is complete for real projects, not merely for the fixtures.

Its wording differs from ours in three rows; the mapping is one-to-one, and our shorter labels stand:

| Observed label | This catalogue | Findings observed |
| --- | --- | --- |
| *Mangler Identifikationskode* | `doc-documentation-tag` — *Mangler Id-kode* | 166 |
| *Mangler Lysgruppe* | `doc-power-group` | 123 |
| *Mangler ledningsfarve* | `doc-cable-colour` — *Mangler Ledningsfarve* | 95 |
| *Mangler Kabeltype* | `doc-cabletype` | 69 |
| *Er ikke forbundet/linked til noget* | `doc-not-linked` — *Ikke forbundet* | 32 |
| *Mangler Kabelnummer* | `doc-cablenumber` | 24 |
| *Mangler Placering* | `doc-position` | 11 |
| *Mangler datalinie adresse* | `doc-address` — *Mangler Adresse* | 11 |

Two further observations from that report, both consistent with this catalogue:

- **Findings are presented per terminal, not per product.** The report groups locality → product
  (name plus its placement note) → terminal (*Tryk (øverst venstre)*, *LED (nederst)*, *Udgang*,
  *Indgang*, *Udgang ÅBNE*) → the findings for that terminal, and it repeats the five product-level
  items under every terminal of the product. Attachment stays product-level for those five (a
  six-button product carries one missing Id-kode, not six); only the *presentation* fans them out.
- **Its per-terminal order is the reverse grouping of ours** — address, wire colour, not-linked,
  placement, cable type, cable number, Id-kode, light group. Nothing depends on matching another
  tool's order; the requirement is only that ours is deterministic and stable.

The frequency column is a rough guide to which ids DOMINATE a report: an installation that does not
use identification codes or light groups draws roughly half of all its findings from two ids. (It is a
guide to volume, not to suppression — see §7, which forecloses that.)

### Second run — the same tool over a fixture whose content is known (2026-08-09)

The 2022 report is field evidence, but its ground truth was unknowable: a ninth kind could have been
missing simply because no project in it exercised one. So the same tool was run over
[`Project6-Errors.vis`](../../tests/testdata/projects/Project6-Errors.vis), every gap in which was
authored deliberately and is listed in its authoring record. It again reported **exactly these eight
kinds and no ninth** — and, element for element, the same verdicts as this project's own implementation:

| Rule | This implementation | `jemi.dk/ihc/docs` |
| --- | --- | --- |
| `doc-address` | 5 | 5 |
| `doc-cable-colour` | 8 | 8 |
| `doc-not-linked` | 10 | 10 |
| `doc-position` | 4 | 11 |
| `doc-cabletype` | 4 | 11 |
| `doc-documentation-tag` | 4 | 11 |
| `doc-power-group` | 4 | 11 |
| `doc-cablenumber` | 5 | 12 |
| **Total** | **44** | **79** |

The three terminal-level rules match exactly. The five product-level rules name the **same products**;
the larger numbers are the per-terminal presentation described above, multiplied out (6 + 2 + 2 + 1
terminals, and one product more for cable number). The two implementations therefore disagree about
nothing except attribution — which the bullet above already settles in favour of per-product.

Three further confirmations from the same run:

- **The issue-free control product is silent in both.** `Lampeudtag` appears in neither report.
- **The §6 one-sided-documentation non-finding holds.** The product carrying a cable type but no cable
  number is reported for the number alone, by both.
- **Both scope these checks to data-line products.** The fixture's RS485, airlink, S0 and bus products
  carry an empty `documentation_tag` — and, on the airlink pair, an empty `power_group` — yet neither
  implementation reports any of them.

⚠ **What this evidence cannot support.** The tool is unofficial, may be incomplete or wrong, and **has
no severity model at all** — it emits one flat list. Its agreement is therefore evidence about
*detection*: which conditions are worth reporting, and on which elements. It says nothing about §2's
Fatal / Error / Warning split, and neither what it reports nor what it omits should be read as
endorsing a severity. Nor is agreement proof — two implementations can share a blind spot. The oracle
is the fixture, whose content is authored and recorded; the report is a second opinion.

<!-- GENERATED: catalogue index — rendered from the declarations; do not edit by hand -->
## Appendix — catalogue index, generated from the declarations

Every governed code, as the code itself declares it. This section is RENDERED from
`ihcclient/src/vis/validation/ProblemCatalogEntries.*.cs` and compared by a test, so it cannot
fall behind the declarations. Edit the declarations, not this table.

The evidence and rationale columns of the sections above are deliberately absent here: they are
prose, and they live as doc-comments on each declaration.

### Project findings (167)

| Id | Cat | Costs | Kind | Status | Danish label |
| --- | --- | --- | --- | --- | --- |
| `addr-dimmer-channel-duplicate` | ADR | Error | UserContentRule | Active | Kanalerne '{channel}' og '{other}' deler kanal-id {id}. |
| `addr-dimmer-channel-unassigned` | ADR | Warning | UserContentRule | Active | Kanalen '{channel}' har ingen kanal-id. |
| `addr-modem-phonenumber-blank` | ADR | Warning | UserContentRule | Active | Modemet '{modem}' har intet telefonnummer i nogen af sine {slots} pladser. |
| `addr-modem-phonenumber-malformed` | ADR | Warning | UserContentRule | Active | Telefonnummeret '{value}' skal være på 3-20 tegn uden mellemrum og begynde med en landekode, f.eks. +45. |
| `addr-module-mixed-locality` | ADR | Warning | UserContentRule | Active | Datalinje {line} betjener klemmer i {localities} lokaliteter. |
| `addr-module-partial` | ADR | Warning | UserContentRule | Active | Datalinje {line} bruger kun {used} af {capacity} klemmer. |
| `addr-s0-ticks-missing` | ADR | Warning | UserContentRule | Active | Måleren '{meter}' mangler et antal pulser mellem {minimum} og {maximum}. |
| `addr-unassigned` | ADR | Warning | UserContentRule | RuledOut | *(to author)* |
| `addr-wireless-channel-shared` | ADR | Warning | UserContentRule | Active | Klemmerne '{pin}' og '{other}' deler kanal {channel}. |
| `addr-wireless-not-commissioned` | ADR | Warning | UserContentRule | Active | Produktet '{product}' har intet serienummer. |
| `attr-enum-range` | INT | Error | SchemaSerializationGuard | Active | Ugyldig værdi '{value}' i attributten '{attribute}' på <{tag}>. Tilladte værdier: {allowed}. |
| `attr-latin1` | INT | Error | SchemaSerializationGuard | Active | Tegn kan ikke gemmes i attributten '{attribute}' på <{tag}>. |
| `attr-required` | INT | Error | SchemaSerializationGuard | Active | Den påkrævede attribut '{attribute}' mangler på <{tag}>. |
| `attr-undeclared` | INT | Error | SchemaSerializationGuard | Active | Ukendt attribut '{attribute}' på <{tag}>. |
| `backup-retained-count` | DEV | Info | UserContentRule | Active | Projektet beder controlleren huske værdien af {count} ressourcer ved strømsvigt, et antal controlleren begrænser ved overførsel. |
| `capacity-addresses` | PRJ | Error | UserContentRule | Retired | *(to author)* |
| `capacity-input-addresses` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} indgangsklemmer. |
| `capacity-input-modules` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} indgangsmoduler. |
| `capacity-modem-multiple` | PRJ | Error | UserContentRule | Active | Projektet indeholder {used} modemer; controlleren binder ét. |
| `capacity-modules-exceeded` | PRJ | Error | UserContentRule | Retired | *(to author)* |
| `capacity-output-addresses` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} udgangsklemmer. |
| `capacity-output-modules` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} udgangsmoduler. |
| `capacity-resources-high` | PRJ | Warning | UserContentRule | Active | Projektet bruger {used} af {limit} ressourcer. |
| `capacity-rs485-exceeded` | PRJ | Error | UserContentRule | Active | Projektet har {used} RS485-komponenter inkl. SMS-modem; det tilladte maksimum er {limit}. |
| `capacity-s0-multiple` | PRJ | Error | UserContentRule | Active | Projektet indeholder {used} S0-produkter; controlleren binder ét. |
| `capacity-scenarios-per-receiver` | ADR | Warning | UserContentRule | Active | Modtageren '{product}' indgår i {used} scenarier; anbefalingen er højst {limit} på én modtager. |
| `capacity-voicemodem-dimmer-conflict` | PRJ | Error | UserContentRule | Active | Projektet indeholder både et Voice Modem og en RS485 LED-dæmper; de kan ikke anvendes i samme projekt. |
| `capacity-wireless-exceeded` | PRJ | Warning | UserContentRule | Active | Projektet har {used} trådløse produkter; anbefalingen er højst {limit}. |
| `capacity-wireless-links-per-unit` | ADR | Warning | UserContentRule | Active | Den trådløse enhed '{product}' har {used} links; anbefalingen er højst {limit} på én enhed. |
| `containment` | INT | Warning | UserContentRule | Active | Uventet placering: <{tag}> ligger under <{parent}>. |
| `controller-link-budget` | ADR | Info | UserContentRule | Active | Controller Link-forbindelsen overfører højst {signals} tænd/sluk-signaler i hver retning, optager faste ind- og udgange på begge controllere uanset hvor mange af signalerne der bruges, og kan ikke overføre analoge værdier. |
| `dataline-address` | ADR | Error | UserContentRule | Retired | *(to author)* |
| `dataline-address-duplicate` | ADR | Error | UserContentRule | Active | Dobbelt klemmeadresse '{value}': {count} klemmer på <{tag}> deler adressen. |
| `dataline-address-malformed` | ADR | Error | UserContentRule | Active | Ugyldig klemmeadresse '{value}' på <{tag}>. |
| `dataline-address-range` | ADR | Error | UserContentRule | Active | Klemmeadressen '{value}' på <{tag}> er uden for det gyldige område 1-{maximum}. |
| `dev-backup-missing` | DEV | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' gemmes ikke ved strømsvigt. |
| `dev-dimmer-fade-zero` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' skifter hårdt i begge retninger. |
| `dev-dimmer-load-mode-auto` | DEV | Warning | UserContentRule | Active | LED-dæmperen '{product}' står på automatisk lastregistrering. |
| `dev-dimmer-max-zero` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' har maksimum 0 % og kan aldrig tænde. |
| `dev-dimmer-range-inverted` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' har minimum {minimum} % og maksimum {maximum} %. |
| `dev-inivalue-out-of-range` | DEV | Warning | UserContentRule | Active | Startværdien {value} på '{variable}' er uden for det gyldige område {minimum}-{maximum}. |
| `dev-inivalue-overwritten` | DEV | Warning | UserContentRule | Active | Startværdien '{value}' på '{variable}' sættes af et program ved hver opstart. |
| `dev-setting-default` | DEV | Warning | UserContentRule | Active | Produktet '{product}' har {untouched} af {settings} indstillinger på fabriksværdien. |
| `dev-shutter-traveltime-zero` | DEV | Warning | UserContentRule | Active | Gardinet '{product}' har en køretid på 0 sekunder. |
| `dev-write-to-read-only` | DEV | Error | UserContentRule | Active | Kommandoen '{action}' skriver til den skrivebeskyttede variabel '{variable}'. |
| `doc-address` | DOC | Warning | UserContentRule | Active | Mangler Adresse |
| `doc-cable-colour` | DOC | Warning | UserContentRule | Active | Mangler Ledningsfarve |
| `doc-cablenumber` | DOC | Warning | UserContentRule | Active | Mangler Kabelnummer |
| `doc-cabletype` | DOC | Warning | UserContentRule | Active | Mangler Kabeltype |
| `doc-documentation-tag` | DOC | Warning | UserContentRule | Active | Mangler Id-kode |
| `doc-no-enduser-products` | DOC | Warning | UserContentRule | Active | Ingen produkter til slutbrugerdokumentation |
| `doc-not-linked` | DOC | Warning | UserContentRule | Active | Ikke forbundet |
| `doc-position` | DOC | Warning | UserContentRule | Active | Mangler Placering |
| `doc-power-group` | DOC | Warning | UserContentRule | Active | Mangler Lysgruppe |
| `doc-project-info-blank` | DOC | Warning | UserContentRule | Active | Mangler projektoplysninger |
| `element-undeclared` | INT | Error | SchemaSerializationGuard | Active | Ukendt elementtype <{tag}>. |
| `enum-def-duplicate-index` | LOG | Error | UserContentRule | Active | Enumerator typen '{enum}' har to værdier med indeks {index}. |
| `enum-def-duplicate-name` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har to værdier med navnet '{value}'. |
| `enum-def-empty` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har ingen værdier. |
| `enum-def-single-value` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har kun én værdi, '{value}'. |
| `enum-inivalue` | LOG | Error | UserContentRule | Active | Ugyldig starttilstand '{inivalue}' på enumerator-variablen '{name}': den findes ikke i enumeratortypen '{typedef}'. |
| `enum-typedef` | LOG | Error | UserContentRule | Active | Enumeratortype mangler: typedef='{typedef}' på enumerator-variablen '{name}' peger på <{tag}>, ikke på en enumeratortype. |
| `export-controller-declined` | INT | Refusal | OperationOutcome | Active | Controlleren afviste projektet |
| `fb-holiday-input-custom-block` | LOG | Warning | UserContentRule | Active | Den egenudviklede funktionsblok '{name}' har en helligdagsindgang, som er rapporteret at få overførslen til controlleren til at mislykkes. |
| `fb-local-ref` | LOG | Error | UserContentRule | Active | Reference uden for blokken: {attribute}='{value}' på <{tag}> peger uden for funktionsblokken. |
| `fb-master-missing-from-library` | LOG | Info | UserContentRule | Active | Funktionsblokken '{name}' bygger på mastertypen {master}, som ikke findes i det tilgængelige blokbibliotek, og projektet kan derfor ikke genskabes fra en nyinstallation. |
| `fb-master-version-differs` | LOG | Info | UserContentRule | Active | Funktionsblokken '{name}' er indsat som version {frozen}, mens blokbiblioteket nu indeholder version {library}, og en indsat blok opdateres aldrig automatisk. |
| `fb-pin-container` | LOG | Error | UserContentRule | Active | Klemme i forkert beholder: <{tag}> i funktionsblokken '{id}' skal ligge under <{expected}>, ikke under <{actual}>. |
| `fb-pir-dusk-gated` | LOG | Info | UserContentRule | Active | PIR-blokken '{name}' reagerer kun på bevægelse, mens dens skumringsindgang er tændt, så en tilslutning der aldrig bliver tændt får blokken til at virke død. |
| `fb-programs` | LOG | Error | UserContentRule | Active | Ugyldigt programindhold i funktionsblokken '{id}': programbeholderen indeholder <{tag}>, men må kun indeholde simple programmer. |
| `fb-provenance-rewritten` | LOG | Info | UserContentRule | Active | Funktionsblokken '{name}' var en leverandørblok, men dens oprindelsesoplysninger er fjernet, typisk ved oplåsning eller 'Gem funktionsblok', så leverandørens version ikke længere kan spores, og blokkens .ifb-fil bør arkiveres sammen med projektet. |
| `fb-pulse-constant-default` | LOG | Info | UserContentRule | Active | Impulsblokken '{name}' regner med {pulses} impulser pr. kWh, og konstanten skal stemme overens med den fysiske målers mærkeplade. |
| `fb-revision-defective-confirmed` | LOG | Error | UserContentRule | Active | Funktionsblokken '{name}' er indsat som revision {master}, som leverandøren har bekræftet er fejlbehæftet; den skal udskiftes med en nyere revision. |
| `fb-revision-defective-reported` | LOG | Warning | UserContentRule | Active | Funktionsblokken '{name}' er indsat som revision {master}, som er rapporteret fejlbehæftet af andre brugere; overvej at udskifte den med en nyere revision. |
| `fb-shape` | LOG | Error | UserContentRule | Active | Forkert blokopbygning i funktionsblokken '{id}': forventet [{expected}], men fandt [{actual}]. |
| `fb-short-press-below-default` | LOG | Warning | UserContentRule | Active | Funktionsblokken '{name}' har 'Max tid for kort tryk' sat til {value} ms, som er under blokkens standardværdi på {default} ms, og korte tryk registreres derfor ikke pålideligt. |
| `fb-user-authored` | LOG | Info | UserContentRule | Active | Funktionsblokken '{name}' er egenudviklet og følger ikke med nogen installation af IHC Visual, så dens .ifb-fil bør arkiveres sammen med projektet. |
| `id-duplicate-counter` | INT | Error | UserContentRule | Active | Dobbelt id-tæller i '{id}' på <{tag}>: {count} id'er deler samme tæller. |
| `id-duplicate-token` | INT | Error | UserContentRule | Active | Dobbelt id '{id}' på <{tag}>: {count} elementer deler dette id. |
| `id-typecode` | INT | Error | UserContentRule | Active | Forkert id-typekode i '{id}' på <{tag}>: typekoden er {actual}, men skulle være {expected}. |
| `id-wellformed` | INT | Error | UserContentRule | Active | Ugyldigt id '{id}' på <{tag}>. |
| `idref-dangling` | INT | Error | UserContentRule | Active | Reference uden mål: {attribute}='{value}' på <{tag}> peger ikke på noget element. |
| `import-catalog-unparsable` | INT | Refusal | OperationOutcome | Active | Ugyldig katalogfil |
| `import-catalog-wrong-kind` | INT | Refusal | OperationOutcome | Active | *(to author)* |
| `import-controller-no-project` | INT | Refusal | OperationOutcome | Active | Intet projekt på controlleren |
| `inline-constant` | LOG | Error | UserContentRule | Active | Ubrugt indlejret konstant <{tag}> '{id}' i <{parent}>: forælderens {attribute} er '{value}' og peger ikke på den. |
| `link-bijection` | WIR | Error | UserContentRule | Active | Forbindelsen er ensidig: <{tag}> '{id}' er ikke forbundet begge veje til en partner af den modsatte type. |
| `link-fb-input-unfed` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' har ingen forbundne indgange. |
| `link-fb-output-unused` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' har ingen forbundne udgange. |
| `link-output-multidriven` | WIR | Warning | UserContentRule | Active | Udgangen '{pin}' styres af {drivers} kilder. |
| `link-pass-through` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' kopierer kun én indgang til én udgang. |
| `link-product-unwired` | WIR | Warning | UserContentRule | Active | Produktet '{product}' har ingen forbundne ind- eller udgange. |
| `link-through-empty-block` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' har ingen programmer, men modtager signaler. |
| `load-bom-utf16` | INT | Refusal | OperationOutcome | Active | Filen har et UTF-16-BOM |
| `load-bom-utf8` | INT | Refusal | OperationOutcome | Active | Filen har et UTF-8-BOM |
| `load-character-data` | INT | Refusal | OperationOutcome | Active | Filen indeholder tekst i et element |
| `load-depth` | INT | Refusal | OperationOutcome | Active | For dyb elementstruktur |
| `load-dtd-malformed` | INT | Refusal | OperationOutcome | Active | Ugyldig indbygget DTD |
| `load-empty` | INT | Refusal | OperationOutcome | Active | Filen er tom |
| `load-encoding-declared` | INT | Refusal | OperationOutcome | Active | Forkert tegnkodning |
| `load-gzip` | INT | Refusal | OperationOutcome | Active | Filen er komprimeret |
| `load-not-xml` | INT | Refusal | OperationOutcome | Active | Filen er ikke gyldig XML |
| `load-root-tag` | INT | Refusal | OperationOutcome | Active | Ikke en projektfil |
| `load-truncated` | INT | Refusal | OperationOutcome | RuledOut | Filen er afkortet |
| `load-version-missing` | INT | Refusal | OperationOutcome | Active | Mangler projektversion |
| `logic-block-empty` | LOG | Warning | UserContentRule | Active | Blokken '{block}' har ingen programmer. |
| `logic-block-locked-content` | LOG | Warning | UserContentRule | Active | Den låste blok '{block}' har ændret '{variable}'. |
| `logic-block-no-pins` | LOG | Warning | UserContentRule | Active | Blokken '{block}' har hverken ind- eller udgange. |
| `logic-block-recursive` | LOG | Error | UserContentRule | Active | Funktionsblokken '{name}' kan nå sig selv gennem programmerne, og en sådan rekursion udføres slet ikke på controlleren, selv om den virker i simulatoren. |
| `logic-case-duplicate-value` | LOG | Error | UserContentRule | Active | Case-noden '{program}' tester den samme værdi i to grene. |
| `logic-case-no-branches` | LOG | Warning | UserContentRule | Active | Case-noden '{program}' har ingen case-værdier. |
| `logic-case-value-foreign` | LOG | Warning | UserContentRule | Active | Case-grenen '{program}' tester en værdi, der ikke findes i '{enum}'. |
| `logic-counter-never-reset` | LOG | Warning | UserContentRule | Active | Tælleren '{variable}' tælles op, men nulstilles aldrig. |
| `logic-duplicate-program` | LOG | Warning | UserContentRule | Active | Blokken '{block}' har to identiske programmer. |
| `logic-flag-never-cleared` | LOG | Warning | UserContentRule | Active | Flaget '{variable}' sættes, men nulstilles aldrig. |
| `logic-holiday-schedule-firmware` | LOG | Warning | UserContentRule | Active | Projektet bruger helligdagsskemaet, som ifølge leverandøren først virker fra controllerfirmware 3.3.21. |
| `logic-output-never-assigned` | LOG | Warning | UserContentRule | Active | Udgangen '{variable}' tilskrives ikke af noget program. |
| `logic-program-no-actions` | LOG | Warning | UserContentRule | Active | Programmet '{program}' har hændelser, men ingen kommandoer. |
| `logic-program-no-events` | LOG | Warning | UserContentRule | Active | Programmet '{program}' har kommandoer, men ingen hændelser. |
| `logic-self-trigger` | LOG | Warning | UserContentRule | Active | Programmet '{program}' udløses af '{variable}', som det selv tilskriver. |
| `logic-statement-unlinked` | LOG | Error | UserContentRule | Active | Programlinjen <{tag}> i blokken '{block}' peger ikke på nogen ressource. |
| `logic-subprogram-no-conditions` | LOG | Warning | UserContentRule | Active | Underprogrammet '{program}' har ingen betingelser. |
| `logic-variable-read-only` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' læses, men tilskrives aldrig. |
| `logic-variable-write-only` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' tilskrives, men læses aldrig. |
| `luid-ceiling` | INT | Error | UserContentRule | Active | Id-tælleren er opbrugt: last_unique_id '{value}' overskrider loftet for 24-bit id-tællere. |
| `luid-low` | INT | Error | UserContentRule | Active | Id-tælleren er for lav |
| `luid-malformed` | INT | Error | UserContentRule | Active | Ugyldig id-tæller: last_unique_id '{value}' er ikke et _0x-hextoken. |
| `migration-untested-product` | PRJ | Info | UserContentRule | Active | Produktet '{product}' kan ifølge leverandøren for nuværende ikke genbruges ved en konvertering til KNX, og leverandøren undersøger fortsat, om der kommer en erstatning. |
| `name-cable-number-duplicate` | DOC | Warning | UserContentRule | Active | Dobbelt Kabelnummer |
| `name-default` | DOC | Warning | UserContentRule | Active | Uændret standardnavn |
| `name-duplicate-siblings` | DOC | Warning | UserContentRule | Active | Dobbelt navn |
| `name-empty` | DOC | Warning | UserContentRule | Active | Mangler Navn |
| `name-helpfile-missing` | DOC | Warning | UserContentRule | RuledOut | *(to author)* |
| `name-id-code-duplicate` | DOC | Warning | UserContentRule | Active | Dobbelt Id-kode |
| `name-note-missing` | DOC | Warning | UserContentRule | Active | Mangler Note |
| `name-power-group-variant` | DOC | Warning | UserContentRule | Active | Afvigende stavning af lysgruppe |
| `product-3key-upload-abort` | PRJ | Warning | UserContentRule | Active | Produktet '{product}' er rapporteret at afbryde overførslen til controlleren undervejs og efterlade den i fejltilstand. |
| `product-discontinued` | PRJ | Info | UserContentRule | Active | Produktet '{product}' er udgået hos leverandøren, og en tilsvarende erstatning kan være svær eller umulig at skaffe. |
| `product-ir-generations-mixed` | PRJ | Info | UserContentRule | Active | Projektet indeholder både den ældre IR-fjernbetjening med 16 tryk og den B&O-kompatible IR-fjernbetjening med 8 tryk, som forudsætter hver sin indbyrdes inkompatible generation af IR-modtager. |
| `product-keypad-codes-local` | DEV | Info | UserContentRule | Active | Adgangskoderne til kodetastaturet '{product}' er gemt i selve tastaturet og hverken i projektet eller controlleren, så de følger ikke med en sikkerhedskopi af projektet. |
| `product-pir-alarm-polarity` | DEV | Info | UserContentRule | Active | Alarm-PIR'en '{product}' bryder sit signal ved bevægelse, så indgangen går fra tændt til slukket modsat en almindelig PIR, og signalet skal derfor typisk inverteres i programmet. |
| `product-s0-instrument-only` | ADR | Info | UserContentRule | Active | S0-måleindgangen '{product}' er en særskilt instrumenteringsindgang, hvis tælling ikke kan indgå i funktionsblokke og ikke kan deles med et almindeligt indgangsmodul. |
| `product-sensor-pulse-input` | ADR | Info | UserContentRule | Active | Sensoren '{product}' er ikke en analog indgang, men sender sin måling som impulser på en almindelig 24 V-linje, og den kræver derfor indgangsmodulet 24 V/3 mA. |
| `product-sounder-not-alarm-approved` | PRJ | Info | UserContentRule | Active | Lydgiveren '{product}' er ifølge leverandøren ikke godkendt til varslingsanlæg og må derfor ikke anvendes som lovpligtig varsling. |
| `product-wireless-phaseout` | PRJ | Info | UserContentRule | Active | Projektet indeholder {count} IHC Wireless-produkter, og leverandøren har varslet, at hele IHC Wireless-familien udfases i 2026 på en dato, der endnu ikke er meldt ud, hvorefter erstatningsenheder ikke længere kan købes. |
| `program-shape` | LOG | Warning | UserContentRule | Active | Uventet programopbygning i <{tag}> '{id}': forventet [{expected}], men fandt [{actual}]. |
| `root-children` | INT | Warning | UserContentRule | Active | Uventet rækkefølge i roden: rodens børn er [{actual}]; forventet [{expected}]. |
| `root-version` | INT | Error | UserContentRule | Active | Nyere projektversion: version_major='{version}' er nyere end version 4, som dette værktøj understøtter. |
| `root-version-minor` | INT | Warning | UserContentRule | Active | Projektets formatversion 4.{minor} er nyere end den understøttede 4.{supported}; ukendte oplysninger kan gå tabt ved gemning. |
| `rs485-bus-installation` | ADR | Info | UserContentRule | Active | RS-485-bussen, som projektets busprodukter sidder på, må højst bære {maxdevices} komponenter og skal termineres for enden af strengen — enten af SMS-modulets indbyggede terminering eller med en modstand på cirka {termination} ohm — og over cirka {shieldlength} meter forbindes kabelskærmen til forsyningens 0 V. |
| `rs485-dimmer-fault-unwired` | WIR | Info | UserContentRule | Active | LED-lysdæmperen '{name}' stiller fejlressourcer for overstrøm, overspænding, overophedning og belastningsfejl til rådighed, men ingen af dem er forbundet, så en fejl i dæmperen bliver aldrig synlig i programmet. |
| `rs485-dimmer-firmware-link-errors` | DEV | Warning | UserContentRule | Active | LED-dæmperen '{product}' har vedvarende forbindelses- og overførselsfejl på controllerfirmware under 03.03.33. |
| `rs485-dimmer-powerfail-level` | DEV | Info | UserContentRule | Active | LED-lysdæmperen '{name}' husker ikke tænd/sluk-tilstanden efter et længere strømsvigt, men vender tilbage på sit konfigurerede niveau, fra fabrikken {level} %. |
| `rs485-dimmer-scenario-recall` | SCN | Warning | UserContentRule | Active | LED-dæmperen '{product}' styres via scenarier, hvilket kræver dæmperfirmware 01.01.40 (som selv kræver CTR.R.03.03.44) — og dæmperfirmware overføres ikke fra programmet. |
| `rs485-dimmer-scene-multi-off` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' slukker {dimmers} LED-dæmpere samtidig, men kun én af dem når at svare. |
| `save-roundtrip-mismatch` | INT | Refusal | OperationOutcome | Active | Projektet kan ikke gemmes uden tab |
| `save-target-unwritable` | INT | Refusal | OperationOutcome | Active | Filen kunne ikke skrives |
| `scene-all-off` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' slukker alle {members} medlemmer. |
| `scene-bijection` | SCN | Error | UserContentRule | Active | Scenerækken er ensidig: <{tag}> '{id}' er ikke forbundet begge veje til en partner af den modsatte type. |
| `scene-dimming-out-of-range` | SCN | Warning | UserContentRule | Active | Scenemedlemmet '{member}' har lysniveauet {value} %; det gyldige område er {minimum}-{maximum} %. |
| `scene-duplicate-target` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' styrer udgangen '{output}' i flere rækker. |
| `scene-long-delay` | SCN | Warning | UserContentRule | Active | Ramptiden {seconds} sekunder er længere end de tilladte {limit}. |
| `scene-member-unwired` | SCN | Warning | UserContentRule | Active | Scenarierækken i '{product}' peger ikke på nogen udgang. |
| `scene-unreferenced` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' kaldes ikke fra noget program. |
| `struct-icon-default` | PRJ | Warning | UserContentRule | Active | Elementet '{element}' har ikke fået et ikon. |
| `struct-modified-stale` | PRJ | Warning | UserContentRule | RuledOut | *(to author)* |
| `struct-product-no-terminals` | PRJ | Warning | UserContentRule | Active | Produktet '{product}' har ingen klemmer. |

### Catalog-definition findings (11)

| Id | Cat | Costs | Kind | Status | Danish label |
| --- | --- | --- | --- | --- | --- |
| `block-identity-missing` | INT | Error | UserContentRule | Active | Mangler blokidentitet |
| `grammar-dangling-idref` | INT | Warning | SchemaSerializationGuard | Active | Reference uden mål |
| `grammar-duplicate-id` | INT | Warning | SchemaSerializationGuard | Active | Dobbelt id |
| `grammar-enum-value` | INT | Warning | SchemaSerializationGuard | Active | Værdi uden for listen |
| `grammar-missing-required` | INT | Warning | SchemaSerializationGuard | Active | Manglende påkrævet attribut |
| `grammar-undeclared-attribute` | INT | Warning | SchemaSerializationGuard | Active | Ukendt attribut |
| `grammar-undeclared-type` | INT | Warning | SchemaSerializationGuard | Active | Ukendt elementtype |
| `identity-missing` | INT | Error | UserContentRule | Active | Mangler produktidentitet |
| `program-empty` | LOG | Warning | UserContentRule | Active | Program uden hændelser |
| `resource-enum-unwired` | LOG | Error | UserContentRule | Active | Enumerator ikke forbundet |
| `scenes-without-output` | SCN | Error | UserContentRule | Active | Scener uden udgang |

### Operation outcomes (49)

| Id | Cat | Costs | Kind | Status | Danish label |
| --- | --- | --- | --- | --- | --- |
| `bridge.download` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke hentes fra controlleren |
| `bridge.upload` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke sendes til controlleren |
| `edit.case-branch-invalid` | — | Refusal | EditPrecondition | Active | Ikke en gyldig case-forgrening på en kommandogruppe. |
| `edit.case-value-not-a-state` | — | Refusal | EditPrecondition | Active | Værdien '{value}' er ikke en tilstand i enumeratortypen '{type}'. |
| `edit.catalog-product-missing` | — | Refusal | EditPrecondition | Active | Intet katalogprodukt med identifikator '{identifier}'. |
| `edit.container-rejects-node` | — | Refusal | EditPrecondition | Active | Den beholder kan ikke rumme denne node. |
| `edit.deep-guard` | — | Refusal | EditPrecondition | Active | Redigeringen kunne ikke gennemføres. |
| `edit.deletion-refused` | — | Refusal | EditPrecondition | Retired | *(to author)* |
| `edit.deletion-refused-catalog-pin` | — | Refusal | EditPrecondition | Active | Klemmen '{pin}' er katalogdefineret på sit produkt og kan ikke slettes alene — slet produktet for at fjerne den. |
| `edit.deletion-refused-locked-block` | — | Refusal | EditPrecondition | Active | Denne node er inde i en låst funktionsblok og kan ikke slettes — lås blokken op først. |
| `edit.deletion-refused-structural` | — | Refusal | EditPrecondition | Active | Denne node er en del af projektets struktur og kan ikke slettes. |
| `edit.enum-type-in-use` | — | Refusal | EditPrecondition | Active | Enumeratortypen {name} bruges stadig af {users} ressource(r) og kan ikke slettes. |
| `edit.enum-type-missing` | — | Refusal | EditPrecondition | Active | Projektet har ingen enumeratortype ved navn {name}. |
| `edit.enum-type-readonly` | — | Refusal | EditPrecondition | Active | Enumeratortypen {name} er en indbygget [read only]-type og kan ikke redigeres. |
| `edit.enum-value-missing` | — | Refusal | EditPrecondition | Active | Enumeratortypen {name} har ingen værdi nummer {index}. |
| `edit.field-above-maximum` | — | Refusal | EditPrecondition | Active | Feltet '{field}' skal være højst {maximum}. |
| `edit.field-below-minimum` | — | Refusal | EditPrecondition | Active | Feltet '{field}' skal være mindst {minimum}. |
| `edit.field-not-offered` | — | Refusal | EditPrecondition | Active | Produktets dialog har ikke feltet {field}. |
| `edit.field-out-of-range` | — | Refusal | EditPrecondition | Active | Feltet '{field}' skal være mellem {minimum} og {maximum}. |
| `edit.field-outside-product` | — | Refusal | EditPrecondition | Active | Et af felterne peger på et element uden for produktet. |
| `edit.field-phonenumber-malformed` | — | Refusal | EditPrecondition | Active | Telefonnummeret '{value}' skal være på 3-20 tegn uden mellemrum og begynde med en landekode, f.eks. +45. |
| `edit.field-read-only` | — | Refusal | EditPrecondition | Active | Feltet {field} kan ikke redigeres. |
| `edit.field-target-missing` | — | Refusal | EditPrecondition | Active | Et af felterne peger på et element, der ikke findes længere. |
| `edit.field-value-rule` | — | Refusal | EditPrecondition | Active | Feltet {field} har en ugyldig værdi. |
| `edit.library-block-missing` | — | Refusal | EditPrecondition | Active | Ingen biblioteks-funktionsblok med master type '{masterType}'. |
| `edit.link-direction` | — | Refusal | EditPrecondition | Active | De to klemmer kan ikke linkes i den retning. |
| `edit.modem-limit` | — | Refusal | EditPrecondition | Active | Et projekt må højst indeholde ét modem. Fjern det eksisterende modem, før du tilføjer et nyt. |
| `edit.move-not-allowed` | — | Refusal | EditPrecondition | Active | Den flytning er ikke tilladt. |
| `edit.no-project-open` | — | Refusal | EditPrecondition | Active | Der er ikke åbnet et projekt. |
| `edit.not-a-command-group` | — | Refusal | EditPrecondition | Active | Målet er ikke en kommandogruppe. |
| `edit.not-a-log-row` | — | Refusal | EditPrecondition | Active | Ikke en Logning-række. |
| `edit.open` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke åbnes til redigering |
| `edit.scene-endpoint-missing` | — | Refusal | EditPrecondition | Active | Et endepunkt i scenariet findes ikke længere. |
| `edit.scene-member-kind` | — | Refusal | EditPrecondition | Active | Denne scenarie-beholder rummer {pinned}-medlemmer; en {produced}-værdi kan ikke tilknyttes her. |
| `edit.section-not-variables` | — | Refusal | EditPrecondition | Active | <{section}> er ikke en variabelsektion i en funktionsblok. |
| `edit.section-rejects-enum` | — | Refusal | EditPrecondition | Active | <{section}> kan ikke rumme en enumerator-variabel. |
| `edit.stale-base-version` | — | Refusal | EditPrecondition | Active | Projektet er ændret, siden denne redigering blev forberedt. |
| `edit.target-locked` | — | Refusal | EditPrecondition | Active | Funktionsblokken er låst og kan ikke redigeres. |
| `edit.target-missing` | — | Refusal | EditPrecondition | Active | {noun} findes ikke længere. |
| `edit.target-wrong-kind` | — | Refusal | EditPrecondition | Active | Målet er ikke {noun}. |
| `edit.terminal-address-range` | — | Refusal | EditPrecondition | Active | Klemmenummeret ligger uden for datalinjens område. |
| `edit.terminal-missing` | — | Refusal | EditPrecondition | Active | Klemmen findes ikke længere. |
| `edit.value-required` | — | Refusal | EditPrecondition | Active | Feltet skal udfyldes. |
| `edit.variable-not-added` | — | Refusal | EditPrecondition | Active | Variablen blev ikke tilføjet. |
| `import.catalog` | — | Refusal | OperationOutcome | Active | Katalogfilen kunne ikke indlæses |
| `import.definition-invalid` | — | Refusal | OperationOutcome | Active | Definitionen kunne ikke bygges |
| `internal.unexpected` | — | Refusal | OperationOutcome | Active | Uventet fejl |
| `io.load` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke åbnes |
| `io.save` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke gemmes: {count} fejl skal rettes først. |

**Total: 227 entries.** 219 active, 4 retired, 4 ruled out.
<!-- END GENERATED -->
