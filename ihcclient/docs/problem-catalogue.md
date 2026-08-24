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

| Code | Fatal error | Error | Warning | Total |
| --- | --- | --- | --- | --- |
| **INT** | 18 | 15 | 8 | 41 |
| **WIR** | 0 | 1 | 8 | 9 |
| **LOG** | 0 | 10 | 26 | 36 |
| **SCN** | 0 | 2 | 7 | 9 |
| **ADR** | 0 | 5 | 9 | 14 |
| **DEV** | 0 | 1 | 8 | 9 |
| **DOC** | 0 | 0 | 18 | 18 |
| **PRJ** | 0 | 5 | 8 | 13 |
| **Total** | **18** | **39** | **92** | **149** |
<!-- END GENERATED -->

## 2. Severity

The three levels are separated by **what the user can still do**, not by how the condition is detected.

| Severity | Definition | The operation | Who decides |
| --- | --- | --- | --- |
| **Fatal error** | The project **cannot be opened, saved, exported or imported**. The tool cannot carry the operation through without losing or inventing content. | Refused. Nothing is opened, and nothing is written or overwritten. The fault is named, with the file and position where that is known. | Nobody — the operation is impossible |
| **Error** | The operation succeeds, but this is **very likely a mistake, and it has a negative consequence**: a state IHC Visual or the controller rejects, or an installation that demonstrably cannot work. | Proceeds. The finding is reported for repair. | The tool — it is wrong regardless of intent |
| **Warning** | This **might** be a mistake and might not. The project is well-formed and usable; the *installation* may be incomplete, contradictory or pointless — or deliberately so. | Proceeds. The finding is a punch-list item. | The user — only the author of the installation can judge |

The dividing lines: a Fatal error is about the **file operation** — it cannot be carried through. An
Error's negative consequence **holds whatever the author intended**, so the tool can call it wrong on
its own. A Warning's consequence **depends on that intent**, so only the user can call it: the *Why
it may be fine* column states the legitimate reading, and that column is why those rows are advisory.

Every Fatal row names the operation it refuses in the **Blocks** column: Open, Save, Import or Export.

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
| `root-version` | INT | Fatal error | Open | `version_major` is above the highest supported version (4) | Written by a newer tool in a format this one does not model; opening it would misread content and saving would destroy it |
| `load-character-data` | INT | Fatal error | Open | An element contains character data | The `.vis` model is attribute-only; opening would silently drop the text at the next save |
| `load-depth` | INT | Fatal error | Open | Element nesting exceeds the supported depth | Corrupt or hostile file; a legal project never nests that deep |
| `element-undeclared` | INT | Fatal error | Save · Export | An element type is declared neither in the file's inline DTD nor in the schema registry | The element has no declared rendering — writing the file would lose it |
| `attr-undeclared` | INT | Fatal error | Save · Export | An attribute is declared neither in the element's inline-DTD block nor in the registry | The value has no declared rendering — writing the file would lose it |
| `attr-latin1` | INT | Fatal error | Save · Export | An attribute value carries text outside ISO-8859-1 | The `.vis` encoding cannot represent it; writing would mangle or drop characters |
| `save-target-unwritable` | INT | Fatal error | Save · Export | The destination cannot be written (locked, read-only, missing, or out of space) | The write is abandoned before any existing file is touched |
| `save-roundtrip-mismatch` | INT | Fatal error | Save · Export | Re-reading the just-written bytes does not reproduce the project | The file would not say what the project says; the write is rolled back |
| `import-catalog-unparsable` | INT | Fatal error | Import | A `.def` / `.ifb` catalog file cannot be parsed | Nothing can be taken from it; the import is abandoned whole |
| `import-catalog-wrong-kind` | INT | Fatal error | Import | The imported file is not the catalog kind it is offered as | A product definition and a function block are not interchangeable |
| `import-controller-no-project` | INT | Fatal error | Import | The controller holds no stored project to download | There is nothing to import |
| `export-controller-declined` | INT | Fatal error | Export | The controller refused to store the uploaded project | The upload did not complete; the controller's project state must be re-checked before retrying |
| `id-wellformed` | INT | Error | — | An `id` is not a well-formed `_0x` hex token in the legal packed range | Nothing can reference the element reliably; id allocation cannot account for it |
| `id-duplicate-token` | INT | Error | — | Two elements carry the same id token | Every reference to that id is ambiguous |
| `id-duplicate-counter` | INT | Error | — | Two ids share a counter | The id space is no longer a bijection; the next minted id may collide |
| `id-typecode` | INT | Error | — | An id's type-code disagrees with its element tag | IHC Visual resolves the element to the wrong kind |
| `idref-dangling` | INT | Error | — | A reference attribute names an id no element carries | The reference resolves to nothing (the null token is a legal unwired state and is not this) |
| `attr-required` | INT | Error | — | A `#REQUIRED` attribute is missing | IHC Visual rejects the element |
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

> The engine currently emits the three `dataline-address-*` conditions under the single rule id
> `dataline-address`; splitting them is a catalogue-level refinement, and the duplicate case is
> user-sourced (see §5).

---

## 5. User-sourced findings — action or lack of action

The file is well-formed and every reference resolves; the *installation* is incomplete, contradictory
or pointless. **Nothing here blocks an operation** — no row in this table is a Fatal error, so the
project always opens, saves, exports and imports.

The **Error** rows are errors because their negative consequence holds whatever the author intended:
the described installation cannot work as written, even where the *Why it may be fine* column gives
the reason the author accepted it. The **Warning** rows are advisory throughout: the user reads the
finding, decides whether it is a mistake or a deliberate state of a half-finished installation, and
acts or ignores it.

| Id | Cat | Sev | Finding | Why it may matter | Why it may be fine |
| --- | --- | --- | --- | --- | --- |
| `link-input-unconnected` ✔ | WIR | Warning | A product input (wired or wireless) owns no link | The physical button/sensor does nothing — pressing it has no effect anywhere in the project | Spare terminal on an installed product; input reserved for a later stage |
| `link-output-undriven` ✔ | WIR | Warning | A product output owns no link | The lamp/relay can never be switched by the installation | Output reserved, or driven only from a scene or from a controller-side integration |
| `link-output-multidriven` ✔ | WIR | Warning | A product output is driven by more than one source | Two blocks assign the same physical output; the last writer wins and behaviour depends on timing | Deliberate multi-path control (a manual path and an automation path) where the author accepts last-writer-wins |
| `link-fb-input-unfed` ✔ | WIR | Warning | A function-block input pin owns no link | The block's trigger never arrives from the physical installation | The pin is driven from a program inside another block, or the block is still being built |
| `link-fb-output-unused` ✔ | WIR | Warning | A function-block output pin owns no link | The block computes a result nothing consumes | Result used only as an internal state, or read from the controller's own API/app |
| `link-crosses-locality` ✔ | WIR | Warning | A link runs between elements in different localities | Usually intended, but a surprising cross-locality wire is a common copy/paste slip | Central logic blocks legitimately serve several rooms |
| `link-through-empty-block` ✔ | WIR | Warning | A link ends on a block that carries no programs | The signal enters the block and stops there | The block is a placeholder for logic to be written |
| `link-pass-through` ✔ | WIR | Warning | A block whose only logic copies one input straight to one output | The block adds nothing; the two devices could be linked through a simpler path | Intentional naming/documentation indirection, or a stub kept for a later extension |
| `logic-case-duplicate-value` | LOG | **Error** | Two case branches of the same switch test the same value | The second branch is unreachable — whichever of the two the author meant, one of them never runs | — |
| `logic-block-empty` ✔ | LOG | Warning | A function block declares no programs | The block never does anything | Newly inserted block; a block used only as a named collection of variables |
| `logic-block-no-pins` ✔ | LOG | Warning | A function block declares no inputs and no outputs | Nothing outside the block can reach it | Block driven entirely by timers/internal state |
| `logic-program-no-events` ✔ | LOG | Warning | A program declares no events | The program never starts | Program under construction |
| `logic-program-no-actions` ✔ | LOG | Warning | A program declares events but no commands | The program starts and does nothing | Trigger reserved for later |
| `logic-subprogram-no-conditions` ✔ | LOG | Warning | A sub-program declares no conditions | The conditional branch always takes the same path | The author wants an unconditional else-branch |
| `logic-variable-unused` ✔ | LOG | Warning | A declared variable is referenced by no program and carries no link | Dead declaration; noise in the block and in the reports | Variable kept for documentation or planned use |
| `logic-variable-write-only` ✔ | LOG | Warning | A variable is assigned by programs but never read or linked | The value is computed and thrown away | Value read externally (controller API, app, scene) |
| `logic-variable-read-only` ✔ | LOG | Warning | A variable is read by programs but never assigned and never linked | The logic always sees its initial value | Deliberate constant expressed as an initial value |
| `logic-output-never-assigned` ✔ | LOG | Warning | An output pin is linked to a product output but no program ever assigns it | The physical output can never change state | Output driven by a scene or by another block through the same link |
| `logic-flag-never-cleared` ✔ | LOG | Warning | A flag is set by some program but cleared by none | The flag latches on and the logic never returns to its earlier state | One-shot latch is the intent (e.g. "alarm has fired") |
| `logic-counter-never-reset` ✔ | LOG | Warning | A counter is incremented but never reset or assigned | The count grows without bound and never returns to a known state | Lifetime counter (operating hours, pulse totals) is the intent |
| `logic-timer-unused` ✔ | LOG | Warning | A timer variable is declared but no program starts it | The timer never runs | Timer reserved for later |
| `logic-self-trigger` ✔ | LOG | Warning | A program is triggered by a variable it also assigns | Risk of an oscillating or endlessly retriggering loop | Deliberate self-terminating pattern (assign a different value than the trigger) |
| `logic-contending-writers` ✔ | LOG | Warning | Two programs assign the same variable from unrelated triggers | Which value survives depends on event order | Manual and automatic control of the same lamp, knowingly |
| `logic-duplicate-program` ✔ | LOG | Warning | Two programs in the same block carry identical events and commands | One of them is redundant | Deliberate duplication kept for readability |
| `logic-case-no-branches` ✔ | LOG | Warning | A case/switch node carries no case branches | The switch does nothing | Under construction |
| `logic-case-value-foreign` | LOG | Warning | A case branch tests a value that is not one of the switch variable's enum values | The branch can never be taken | Enum has been re-typed and the branch is kept for a future value |
| `logic-master-block-modified` ✔ | LOG | Warning | A block carrying vendor/master identity has been edited locally | The block no longer matches the library version it claims to be | Deliberate local adaptation of a library block |
| `logic-block-locked-content` ✔ | LOG | Warning | A locked block or product carries content edited after locking | The lock no longer reflects the state it was meant to protect | Lock applied after the edit, deliberately |
| `enum-def-unused` ✔ | LOG | Warning | An enum definition is referenced by no variable | Dead type in the project and in the reports | Type kept for a planned function |
| `enum-def-empty` ✔ | LOG | Warning | An enum definition declares no values | No variable of that type can hold a meaningful value | Type being built |
| `enum-def-single-value` ✔ | LOG | Warning | An enum definition declares exactly one value | The variable can never change | Deliberate constant |
| `enum-value-unused` ✔ | LOG | Warning | An enum value is never tested or assigned anywhere | A declared state the logic never uses | State reserved for later |
| `scene-empty` ✔ | SCN | Warning | A scene carries no members | Activating the scene changes nothing | Scene being built |
| `scene-unreferenced` ✔ | SCN | Warning | A scene resource is not reachable from any program or link | The scene can never be activated from the installation | Activated from the controller app or an external integration |
| `scene-output-also-linked` ✔ | SCN | Warning | An output in a scene is also driven by a follow-link | The scene value and the link fight over the output | Intended: scene sets a preset, the link overrides on demand |
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
| `capacity-input-modules` | PRJ | **Error** | More INPUT data lines are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| `capacity-output-modules` | PRJ | **Error** | More OUTPUT data lines are addressed than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| `capacity-addresses` | PRJ | **Error** | More terminals are addressed in one data-line direction than the target controller supports | The project cannot be uploaded as it stands | Project covers a future expansion |
| ~~`capacity-modules-exceeded`~~ | PRJ | **Error** | **RETIRED — split into the three rows above.** It covered all three quantities under one Danish sentence, *"Projektet bruger {used} af {limit} moduler."* | That sentence was false of the terminals count: 200 terminals over a 128 limit read as "uses 200 of 128 modules". Its entry claimed the arguments said which quantity, but the only arguments were `used` and `limit`. The rule also looped per direction, so it could emit two findings against a declared `OneFinding`. The id stays reserved and is never re-pointed at a successor | — |
| `capacity-wireless-exceeded` | PRJ | Warning | More than 64 wireless products are bound to one controller | Response time degrades. The vendor states a RECOMMENDATION, not a hard limit — *"En IHC controller bør maksimalt forbindes til 64 IHC Wireless produkter"*, explicitly *"af hensyn til en fornuftig responstid"*. **Corrected from Error:** an Error's consequence must hold whatever the author intended, and the devices do bind — the system merely answers more slowly | Planning document, not an upload; a deliberately large installation whose response time the author accepts |
| `capacity-modem-multiple` ⊘ | PRJ | **Error** | The project contains more than one modem | The controller binds one modem, so the extra entries can never be commissioned. **Neither editor will author this state** (measured live 2026-08-11): IHC Visual refuses the second insert with *"Modem er allerede indsat. Der kan kun indsættes et modem i projektet"* and OpenVisual with *"Et projekt må højst indeholde ét modem…"*, each leaving the tree unchanged — so a file carrying two can only have arrived by import or by hand, which is exactly why the file-level check still earns its place | — (the limit is the controller's; no intent makes a second modem work) |
| `capacity-resources-high` | PRJ | Warning | The project's resource count reaches or passes the controller's limit | Further growth will fail late, at upload time | Deliberately near-full installation |
| `struct-locality-empty` ✔ | PRJ | Warning | A locality contains no products and no blocks | Empty room in the tree and in the reports | Room planned but not yet fitted |
| `struct-locality-no-devices` ✔ | PRJ | Warning | A locality contains only function blocks | The room has logic but no hardware — often a mis-drop | Deliberate "logic room" holding central blocks |
| `struct-product-no-terminals` ✔ | PRJ | Warning | A product carries no terminals at all | Nothing on the product can be wired | Product family that genuinely has none |
| `struct-orphan-block` ✔ | PRJ | Warning | A function block is neither linked nor referenced from any other block | The block is isolated from the rest of the installation | Self-contained timer/clock logic |

✅ = implemented today, with the fixed Danish label shown; these eight are the seed set already
reported in the Fuld-mode reports' *Fejl i dokumentation* section.

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
- **A block with more variables than its programs read** while it is being authored — reported once,
  as `logic-variable-unused`, never per program.
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

## 7. Behavioural requirements

- MUST: A **Fatal error** aborts the operation, naming which of Open / Save / Import / Export was
  refused and why, and leaves nothing opened, written or overwritten.
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

**✔ Authored — confirmed User-sourced (60 rows + the eight ✅).** Marked per row in §5. Sitting 5 added
the last two device rows: `dev-inivalue-overwritten` (a `Powerup hændelse` program re-asserting a flag's
own non-default `Initial værdi` at every start) and `dev-backup-missing` (control 216 *Gem aktuel værdi*
demonstrably **writes** `backup="yes"`, so the unmarked state of every other variable is a choice, not a
limitation — the fixture carries both sides of that contrast). Sitting 4
added the program-logic set (`logic-program-no-events`, `logic-program-no-actions`,
`logic-subprogram-no-conditions`, `logic-variable-write-only`, `logic-variable-read-only`,
`logic-output-never-assigned`, `logic-flag-never-cleared`, `logic-counter-never-reset`,
`logic-self-trigger`, `logic-contending-writers`, `logic-duplicate-program`, `logic-case-no-branches`),
the scene set (`scene-empty`, `scene-unreferenced`, `scene-all-off`, `scene-output-also-linked`,
`scene-long-delay`), the enum set (`enum-def-empty`, `enum-def-single-value`, `enum-def-unused`,
`enum-value-unused`), `logic-block-empty` and `link-through-empty-block` (both need the default
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
  `resource_enum` in a vendor file therefore comes from a product `.def` or a library `.ifb`. The
  consequence for `enum-def-unused` and `enum-value-unused`: a user-authored enum type is unused **by
  construction**, so those rows will fire on every one of them — that is correct behaviour, not
  over-reporting.

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

### Project findings (139)

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
| `attr-enum-range` | INT | Error | SchemaSerializationGuard | Active | Ugyldig værdi |
| `attr-latin1` | INT | Error | SchemaSerializationGuard | Active | Tegn kan ikke gemmes |
| `attr-required` | INT | Error | SchemaSerializationGuard | Active | Mangler påkrævet attribut |
| `attr-undeclared` | INT | Error | SchemaSerializationGuard | Active | Ukendt attribut |
| `capacity-addresses` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} klemmer på én datalinjeretning. |
| `capacity-input-modules` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} indgangsmoduler. |
| `capacity-modem-multiple` | PRJ | Error | UserContentRule | Active | Projektet indeholder {used} modemer; controlleren binder ét. |
| `capacity-modules-exceeded` | PRJ | Error | UserContentRule | Retired | *(to author)* |
| `capacity-output-modules` | PRJ | Error | UserContentRule | Active | Projektet bruger {used} af {limit} udgangsmoduler. |
| `capacity-resources-high` | PRJ | Warning | UserContentRule | Active | Projektet bruger {used} af {limit} ressourcer. |
| `capacity-wireless-exceeded` | PRJ | Warning | UserContentRule | Active | Projektet har {used} trådløse produkter; anbefalingen er højst {limit}. |
| `containment` | INT | Warning | UserContentRule | Active | Uventet placering |
| `dataline-address` | ADR | Error | UserContentRule | Retired | *(to author)* |
| `dataline-address-duplicate` | ADR | Error | UserContentRule | Active | Dobbelt klemmeadresse |
| `dataline-address-malformed` | ADR | Error | UserContentRule | Active | Ugyldig klemmeadresse |
| `dataline-address-range` | ADR | Error | UserContentRule | Active | Klemmeadresse uden for området |
| `dev-backup-missing` | DEV | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' gemmes ikke ved strømsvigt. |
| `dev-dimmer-fade-zero` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' skifter hårdt i begge retninger. |
| `dev-dimmer-load-mode-auto` | DEV | Warning | UserContentRule | Active | LED-dæmperen '{product}' står på automatisk lastregistrering. |
| `dev-dimmer-max-zero` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' har maksimum 0 % og kan aldrig tænde. |
| `dev-dimmer-range-inverted` | DEV | Warning | UserContentRule | Active | Lysdæmperen '{product}' har minimum {minimum} % og maksimum {maximum} %. |
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
| `element-undeclared` | INT | Error | SchemaSerializationGuard | Active | Ukendt elementtype |
| `enum-def-duplicate-index` | LOG | Error | UserContentRule | Active | Enumerator typen '{enum}' har to værdier med indeks {index}. |
| `enum-def-duplicate-name` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har to værdier med navnet '{value}'. |
| `enum-def-empty` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har ingen værdier. |
| `enum-def-single-value` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' har kun én værdi, '{value}'. |
| `enum-def-unused` | LOG | Warning | UserContentRule | Active | Enumerator typen '{enum}' bruges ikke af nogen variabel. |
| `enum-inivalue` | LOG | Error | UserContentRule | Active | Ugyldig starttilstand |
| `enum-typedef` | LOG | Error | UserContentRule | Active | Enumeratortype mangler |
| `enum-value-unused` | LOG | Warning | UserContentRule | Active | Værdien '{value}' i enumerator typen '{enum}' bruges ikke. |
| `export-controller-declined` | INT | Refusal | OperationOutcome | Active | Controlleren afviste projektet |
| `fb-local-ref` | LOG | Error | UserContentRule | Active | Reference uden for blokken |
| `fb-pin-container` | LOG | Error | UserContentRule | Active | Klemme i forkert beholder |
| `fb-programs` | LOG | Error | UserContentRule | Active | Ugyldigt programindhold |
| `fb-shape` | LOG | Error | UserContentRule | Active | Forkert blokopbygning |
| `id-duplicate-counter` | INT | Error | UserContentRule | Active | Dobbelt id-tæller |
| `id-duplicate-token` | INT | Error | UserContentRule | Active | Dobbelt id |
| `id-typecode` | INT | Error | UserContentRule | Active | Forkert id-typekode |
| `id-wellformed` | INT | Error | UserContentRule | Active | Ugyldigt id |
| `idref-dangling` | INT | Error | UserContentRule | Active | Reference uden mål |
| `import-catalog-unparsable` | INT | Refusal | OperationOutcome | Active | Ugyldig katalogfil |
| `import-catalog-wrong-kind` | INT | Refusal | OperationOutcome | Active | *(to author)* |
| `import-controller-no-project` | INT | Refusal | OperationOutcome | Active | Intet projekt på controlleren |
| `inline-constant` | LOG | Error | UserContentRule | Active | Ubrugt indlejret konstant |
| `link-bijection` | WIR | Error | UserContentRule | Active | Forbindelsen er ensidig |
| `link-crosses-locality` | WIR | Warning | UserContentRule | Active | Følg-linket går mellem lokaliteterne '{from}' og '{to}'. |
| `link-fb-input-unfed` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' har ingen forbundne indgange. |
| `link-fb-output-unused` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' har ingen forbundne udgange. |
| `link-input-unconnected` | WIR | Warning | UserContentRule | Active | Indgangen '{pin}' er ikke forbundet. |
| `link-output-multidriven` | WIR | Warning | UserContentRule | Active | Udgangen '{pin}' styres af {drivers} kilder. |
| `link-output-undriven` | WIR | Warning | UserContentRule | Active | Udgangen '{pin}' styres ikke af noget. |
| `link-pass-through` | WIR | Warning | UserContentRule | Active | Funktionsblokken '{block}' kopierer kun én indgang til én udgang. |
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
| `logic-case-duplicate-value` | LOG | Error | UserContentRule | Active | Case-noden '{program}' tester den samme værdi i to grene. |
| `logic-case-no-branches` | LOG | Warning | UserContentRule | Active | Case-noden '{program}' har ingen case-værdier. |
| `logic-case-value-foreign` | LOG | Warning | UserContentRule | Active | Case-grenen '{program}' tester en værdi, der ikke findes i '{enum}'. |
| `logic-contending-writers` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' tilskrives af {writers} programmer med uafhængige udløsere. |
| `logic-counter-never-reset` | LOG | Warning | UserContentRule | Active | Tælleren '{variable}' tælles op, men nulstilles aldrig. |
| `logic-duplicate-program` | LOG | Warning | UserContentRule | Active | Blokken '{block}' har to identiske programmer. |
| `logic-flag-never-cleared` | LOG | Warning | UserContentRule | Active | Flaget '{variable}' sættes, men nulstilles aldrig. |
| `logic-master-block-modified` | LOG | Warning | UserContentRule | Active | Blokken '{block}' er ændret lokalt i forhold til biblioteksblokken '{master}'. |
| `logic-output-never-assigned` | LOG | Warning | UserContentRule | Active | Udgangen '{variable}' tilskrives ikke af noget program. |
| `logic-program-no-actions` | LOG | Warning | UserContentRule | Active | Programmet '{program}' har hændelser, men ingen kommandoer. |
| `logic-program-no-events` | LOG | Warning | UserContentRule | Active | Programmet '{program}' har ingen hændelser. |
| `logic-self-trigger` | LOG | Warning | UserContentRule | Active | Programmet '{program}' udløses af '{variable}', som det selv tilskriver. |
| `logic-subprogram-no-conditions` | LOG | Warning | UserContentRule | Active | Underprogrammet '{program}' har ingen betingelser. |
| `logic-timer-unused` | LOG | Warning | UserContentRule | Active | Timeren '{variable}' startes ikke af noget program. |
| `logic-variable-read-only` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' læses, men tilskrives aldrig. |
| `logic-variable-unused` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' bruges ikke af noget program. |
| `logic-variable-write-only` | LOG | Warning | UserContentRule | Active | Variablen '{variable}' i '{block}' tilskrives, men læses aldrig. |
| `luid-ceiling` | INT | Error | UserContentRule | Active | Id-tælleren er opbrugt |
| `luid-low` | INT | Error | UserContentRule | Active | Id-tælleren er for lav |
| `luid-malformed` | INT | Error | UserContentRule | Active | Ugyldig id-tæller |
| `name-cable-number-duplicate` | DOC | Warning | UserContentRule | Active | Dobbelt Kabelnummer |
| `name-default` | DOC | Warning | UserContentRule | Active | Uændret standardnavn |
| `name-duplicate-siblings` | DOC | Warning | UserContentRule | Active | Dobbelt navn |
| `name-empty` | DOC | Warning | UserContentRule | Active | Mangler Navn |
| `name-helpfile-missing` | DOC | Warning | UserContentRule | RuledOut | *(to author)* |
| `name-id-code-duplicate` | DOC | Warning | UserContentRule | Active | Dobbelt Id-kode |
| `name-note-missing` | DOC | Warning | UserContentRule | Active | Mangler Note |
| `name-power-group-variant` | DOC | Warning | UserContentRule | Active | Afvigende stavning af lysgruppe |
| `program-shape` | LOG | Warning | UserContentRule | Active | Uventet programopbygning |
| `root-children` | INT | Warning | UserContentRule | Active | Uventet rækkefølge i roden |
| `root-version` | INT | Error | UserContentRule | Active | Nyere projektversion |
| `save-roundtrip-mismatch` | INT | Refusal | OperationOutcome | Active | Projektet kan ikke gemmes uden tab |
| `save-target-unwritable` | INT | Refusal | OperationOutcome | Active | Filen kunne ikke skrives |
| `scene-all-off` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' slukker alle {members} medlemmer. |
| `scene-bijection` | SCN | Error | UserContentRule | Active | Scenerækken er ensidig |
| `scene-duplicate-target` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' styrer udgangen '{output}' i flere rækker. |
| `scene-empty` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' har ingen medlemmer. |
| `scene-long-delay` | SCN | Warning | UserContentRule | Active | Ramptiden {seconds} sekunder er længere end de tilladte {limit}. |
| `scene-member-unwired` | SCN | Warning | UserContentRule | Active | Scenarierækken i '{product}' peger ikke på nogen udgang. |
| `scene-output-also-linked` | SCN | Warning | UserContentRule | Active | Udgangen '{output}' styres både af et scenarie og af et følg-link. |
| `scene-unreferenced` | SCN | Warning | UserContentRule | Active | Scenariet '{scene}' kaldes ikke fra noget program. |
| `struct-icon-default` | PRJ | Warning | UserContentRule | Active | Elementet '{element}' har ikke fået et ikon. |
| `struct-locality-empty` | PRJ | Warning | UserContentRule | Active | Lokaliteten '{locality}' indeholder hverken produkter eller blokke. |
| `struct-locality-no-devices` | PRJ | Warning | UserContentRule | Active | Lokaliteten '{locality}' indeholder kun funktionsblokke. |
| `struct-modified-stale` | PRJ | Warning | UserContentRule | RuledOut | *(to author)* |
| `struct-orphan-block` | PRJ | Warning | UserContentRule | Active | Blokken '{block}' er ikke forbundet til resten af installationen. |
| `struct-product-no-terminals` | PRJ | Warning | UserContentRule | Active | Produktet '{product}' har ingen klemmer. |

### Catalog-definition findings (10)

| Id | Cat | Costs | Kind | Status | Danish label |
| --- | --- | --- | --- | --- | --- |
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

### Operation outcomes (46)

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
| `edit.field-not-offered` | — | Refusal | EditPrecondition | Active | Produktets dialog har ikke feltet {field}. |
| `edit.field-out-of-range` | — | Refusal | EditPrecondition | Active | Feltet {field} skal være mellem {minimum} og {maximum}. |
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
| `io.save` | — | Refusal | OperationOutcome | Active | Projektet kunne ikke gemmes |

**Total: 195 entries.** 188 active, 3 retired, 4 ruled out.
<!-- END GENERATED -->
