# `Project6-Errors.vis` — the finding-catalogue oracle

**What it is.** A vendor-written IHC Visual project carrying a **deliberate instance of every non-fatal
condition** in [`ihcclient/docs/problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md)
that IHC Visual will actually let a user author — plus the catalogue's **deliberate non-findings**, and
an **issue-free control product** that must appear in no finding list at all.

**Why it exists.** The catalogue's §5 proposes ~87 *user-sourced* rows on the strength of §3's decision
procedure: a row is User-sourced when *"the state is reachable by ordinary authoring"*. That claim was
never tested against the vendor application. This fixture tests it one row at a time: a row IHC Visual
authors is **confirmed**, a row it **refuses** is not user-sourced at all and belongs in §4 (file-sourced).
Eleven rows were falsified that way — see §5 below.

**Provenance (A-1).** Authored **exclusively by driving LK IHC Visual 03.04.72.03** (`C:\Program Files
(x86)\LK IHC Control\IHC Visual`, catalog 100 products / 72 function blocks) through the
`IHCVisualAutomation` CLI (`app.exe`, published self-contained win-x86), elevated, on 2026-08-09.
**No byte was hand-edited** — no text editor, no script, no SDK write path ever touched it. This
document is the record of that authoring, and [`problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md)
§8 names it as the evidence of record for every row §5 below falsifies. The `M-n` labels used
throughout are the authoring run's own measurement numbers, kept as citations.

| Property | Value |
|---|---|
| Size | 77 347 bytes · 1 206 lines · 410 ids · 81 distinct element types · `last_unique_id="_0x216"` |
| Encoding | ISO-8859-1, **no BOM**, CRLF throughout |
| Content | 5 localities · 14 products (8 families) · 5 function blocks · 7 follow-link pairs · 21 programs · 5 scene resources with 5 member rows · 6 enum definitions (2 catalogue, 4 authored) |

---

## 1. Structure

| Locality | Holds | Rows witnessed |
|---|---|---|
| `Stue` | P1 Lampeudtag, P2 LK FUGA Tryk 4 tast 2 dioder, P3 Stikkontakt, FB `Kobling`, FB `Gennemgang` | — |
| `Køkken` | P4 Stikkontakt, P6a/P6b LK FUGA Tryk 2 tast, P5 (unnamed), P5b Brugerdefineret udgangsprodukt | — |
| `Teknik` | P7 LED Dimmer 2 kanaler, P9 S0 Device, P10 Jalousi, P11a/P11b Modtager relæ, P8 SMS Modem | — |
| `Logik` | FB `Tom blok`, FB `Zoo`, FB `Kip tænd sluk (lokalt tilpasset)` — **no products** | `struct-locality-no-devices` |
| `Lokalitet` | nothing — inserted and never touched | `struct-locality-empty`, `name-default` |

`Stue`/`Køkken`/`Teknik` hold no function blocks, which is the §6 *"a locality holding no blocks"*
non-finding.

Project-level: `<project_info/>`, `<customer_info/>` and `<installer_info/>` are all empty →
`doc-project-info-blank`. That one had to be **cleared**, not merely left alone — IHC Visual pre-fills
`programmer` with the Windows user name on a new project.

## 2. Products

**P1 `Lampeudtag` (Stue) is the control and carries NO row.** Every documentation field filled
(Placering `Loft midt`, Note, Kabeltype `5x1,5`, Kabelnummer `K-1`, Id-kode `ID-1`, Lysgruppe `Stue`),
its output addressed `Datalinie 1.01`, wire colour `Hvid`, and linked from `Kobling`. A documentation
check that fires on P1 is over-reporting, and `ErrorsFixtureFindingsTests` fails on it.

| # | Product | Rows witnessed |
|---|---|---|
| P2 | `LK FUGA Tryk 4 tast 2 dioder` | all five product-level documentation blanks: `doc-documentation-tag`, `doc-power-group`, `doc-cabletype`, `doc-cablenumber`, `doc-position`. `Tryk (øverst venstre)`: unaddressed **but coloured**, so it witnesses `doc-address`/`addr-unassigned` alone. `Tryk (øverst højre)`: addressed `2.01`, **no** wire colour → `doc-cable-colour`. `Tryk (nederst venstre)`: addressed + coloured, **unlinked** → `doc-not-linked`, `link-input-unconnected`. `Tryk (nederst højre)`: fans out to two FB inputs → §6 fan-out non-finding |
| P3 | `Stikkontakt` (Stue) | `Datalinie 1.02`; Id-kode `ID-7`, Kabelnummer `K-7`; its output is driven by **two** FB outputs → `link-output-multidriven`, and is also a member of the `Modstrid` scene → `scene-output-also-linked` |
| P4 | `Stikkontakt` (Køkken) | `Datalinie 1.03`; same `ID-7` → `name-id-code-duplicate`; same `K-7` → `name-cable-number-duplicate`; Lysgruppe `stue` vs P1's `Stue` → `name-power-group-variant`; output unlinked → `link-output-undriven`. Module 1 now spans Stue+Køkken → `addr-module-mixed-locality` |
| P5 | *(name cleared)* | `name-empty` — a **user-defined** product's Navn field is editable and IHC Visual accepts empty. Kabeltype filled, Kabelnummer blank → §6 one-sided-documentation non-finding |
| P5b | `Brugerdefineret udgangsprodukt` | left wholly untouched → `name-default` (product arm). Its output is the target of `Zoo`'s never-assigned pin (§3) |
| P6a/P6b | `LK FUGA Tryk 2 tast` ×2 | identical catalogue names as siblings → `name-duplicate-siblings`. P6a addressed on module 3 → `addr-module-partial` |
| P7 | `IHC LED Dimmer 2 kanaler` | **ch.1**: Minimum 80 % ≥ Maksimum 40 % → `dev-dimmer-range-inverted`; Manuel rampetid left at factory 5 s in an otherwise configured channel → `dev-setting-default`. **ch.2**: Maksimum 0 % → `dev-dimmer-max-zero`; Belastnings karakteristik `Auto detection` → `dev-dimmer-load-mode-auto`. Both channels keep the `channel_id` sentinel → `addr-dimmer-channel-unassigned`. ch.1 also carries the long-ramp scene member (§4) |
| P8 | `SMS Modem` | all 30 phone slots blank → `addr-modem-phonenumber-blank`; pincode left at factory `1234`. Carries **no terminals** → `struct-product-no-terminals` |
| P9 | `S0 Device` | fully configured; `ticks` **restored to 100** — see §5, the blank-ticks row is falsified |
| P10 | `Jalousi 2 tast (lokal lås)` | travel time up = 0, down = 120 → `dev-shutter-traveltime-zero` on one side only; serial left at the `_0x0` sentinel → `addr-wireless-not-commissioned` |
| P11a/P11b | `Modtager relæ` ×2 | wireless outputs, documented, uncommissioned; P11a is a member of `Alt slukket` (§4) |

`enduser_report` was **cleared on every product that exposes the checkbox** (`Inkluder produktet i
slutbruger rapport`, control 303). One product still carries it and cannot be cleared — see §5.

## 3. Function blocks and links

| Block | Rows witnessed |
|---|---|
| `Tom blok` (Logik) | no pins, no links, referenced by nothing, and its default `Program` **deleted** → `logic-block-empty`, `logic-block-no-pins`, `struct-orphan-block`, `name-default` (block arm) |
| `Kobling` (Stue) | In0 fed twice from P2 (fan-out); **In1 fed by nothing** → `link-fb-input-unfed`; its default `Program` deleted, so a link genuinely ends on a program-less block → `link-through-empty-block`; pins keep default names and carry no note → `name-note-missing`, `name-duplicate-siblings` (two pins both `Indgang`) |
| `Gennemgang` (Stue) | fed from `Køkken` → `link-crosses-locality`; two programs (ON→ON, OFF→OFF) copying its one input straight to its one output → `link-pass-through` |
| `Zoo` (Logik) | the variable / program / scene zoo — see §4 |
| `Kip tænd sluk (lokalt tilpasset)` (Logik) | inserted from the library, then **renamed, re-noted and re-timed while still locked** — `Nummer 1.1.01`, `Version e`, `Oprettet 17/05/2017`, `Udviklet af Schneider Electric` all survive → `logic-master-block-modified`; its `Timer` setting edited from 3 min to 5 min under `locked="yes"` → `logic-block-locked-content` |

Seven follow-link pairs: P2→`Kobling`.In0, P2→`Gennemgang`.In (the fan-out), P6a→`Zoo`.In0
(cross-locality), `Kobling`.Out0→P1, `Gennemgang`.Out→P3, `Kobling`.Out1→P3 (the multi-driver), and
`Zoo`.`Aldrig tilskrevet`→P5b.

## 4. `Zoo` — variables, programs, scenes, enums

**Variables** (`Interne variable` unless noted). `Flag` set ON by a program and cleared by none →
`logic-flag-never-cleared`. `Tal` read by nothing and unlinked → `logic-variable-unused`. `Timer`
started by nothing → `logic-timer-unused`. `Tæller` incremented, never reset → `logic-counter-never-reset`.
`Selvudløser` triggers the program that assigns it → `logic-self-trigger`. `Kun skrevet` assigned by
two programs, never read → `logic-variable-write-only` + `logic-contending-writers`. `Kun læst` read as
an event, never assigned → `logic-variable-read-only`. `Ugedag` left unreferenced. `Startværdi` carries
`Initial værdi = ON` and is re-assigned ON by the Powerup program → `dev-inivalue-overwritten`.
`Gemt tilstand` is the **only** variable marked `Gem aktuel værdi` (`backup="yes"`), which is what makes
every other state variable's unmarked state a choice rather than an oversight → the contrast for
`dev-backup-missing`, whose witness is `Tæller` (incremented, never reset, unmarked). Output pin
`Udgang` owns no link → `link-fb-output-unused`; output pin `Aldrig tilskrevet` **is** linked (to P5b)
and no program assigns it → `logic-output-never-assigned`.

⚠ The two sitting-5 variables are deliberately minimal and each adds one extra instance of a row that
is already witnessed elsewhere: `Gemt tilstand` is unreferenced (`logic-variable-unused`, as `Tal` and
`Ugedag` are) and `Startværdi` is assigned but never read (`logic-variable-write-only`, as `Kun skrevet`
is). Neither disturbs an existing witness.

**Programs** (12, in order):

| # | Shape | Rows |
|---|---|---|
| 0 | event, no commands | `logic-program-no-actions` |
| 1 | commands, no events | `logic-program-no-events` |
| 2 | `Flag = ON`, `Tæller = Tæller + 1` | the flag/counter rows above |
| 3 | `Selvudløser -> ON` ⇒ `Selvudløser = OFF` | `logic-self-trigger` |
| 4 | `Kun læst -> 0` ⇒ `Kun skrevet = 0` | `logic-variable-read-only`, `logic-variable-write-only` |
| 5 | `Indgang -> OFF` ⇒ `Kun skrevet = 0` | `logic-contending-writers` (with 4) |
| 6, 7 | byte-identical event and command rows | `logic-duplicate-program` |
| 8 | `program_sub` with `<conditions/>` empty | `logic-subprogram-no-conditions` |
| 9, 10 | `program_case` on `Tæller` carrying only the Else group | `logic-case-no-branches` |
| 11 | `event_power` (*Powerup*) ⇒ `Startværdi = ON`, which is that flag's own `Initial værdi` | `dev-inivalue-overwritten` |

**Scenes** (`resource_scene` pins in `Zoo`'s Output; members are `scene_relay`/`scene_dimmer` rows on
the products):

| Scene | Rows |
|---|---|
| `Tom scene` | no members, reachable from nothing → `scene-empty`, `scene-unreferenced` |
| `Alt slukket` | three members (P4, P11a, P3), every one committed OFF → `scene-all-off` |
| `Modstrid` | P3's output, which already carries two follow-links → `scene-output-also-linked`; a dimmer member on P7 ch.1 with `ramptime_ms="1801000"` (30 min 1 s) → `scene-long-delay` |

**Enum definitions** (project-level, four authored on top of the two catalogue types):

| Type | Rows |
|---|---|
| `Tom enum` | created, no values → `enum-def-empty` |
| `Enkelt` | exactly one value `Kun` → `enum-def-single-value` |
| `Ubrugt` | two values, referenced by no variable → `enum-def-unused` |
| `Brugt` | two values, neither tested nor assigned anywhere → `enum-value-unused` |

⚠ `Brugt` is unbound rather than bound-with-one-value-unused, because **IHC Visual cannot bind a
user-created enum type to a block variable at all** — `Indsæt ▸ Variable` has a fixed 21 entries and
none of them is an enumerator. See M-14.

## 5. Rows this fixture **falsifies** — do not "fix" these by editing the file

Each was measured against the live application, one row at a time. **This table is the record**: the
*Evidence* column names the `M-n` measurement each row was logged under and the A/B that settled it, and
reproducing one means re-driving the dialog the row names. All eleven are carried independently by
[`problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md) §8, which cites this document as
its per-row evidence: nine are reclassified there as refusals into its §4, and `name-helpfile-missing`
and `struct-modified-stale` left the finding set entirely for §6's deliberate non-findings.

| Row | What IHC Visual does | Evidence |
|---|---|---|
| `dataline-address-duplicate` (**Error**) | lists an already-claimed address as `N (i brug)` and lets you select it, then **disables OK** so it cannot be committed | M-1, A/B'd four times in one dialog |
| `addr-s0-ticks-missing` | clearing the S0 pulse count is accepted by the field, but OK raises *"Antallet af pulser skal være mellem 1 og 10000"* and commits nothing | M-7 |
| `dev-dimmer-fade-zero` | Soft tænd/sluk tid clamps to a 200 ms minimum (typed `0050`→`200`, `0199`→`200`, `0201`→`201`) | M-6 ⚠ *family-scoped:* measured on the RS485 LED dimmer only |
| `dev-write-to-read-only` (**Error**) | no variable dialog carries an accessibility control, so a block variable cannot be marked read-only; and programs are block-local, so none can reference a product's read-only resource | M-12 |
| `enum-def-duplicate-name` | a second value with an existing name raises *"Vælg et andet navn"* and the create dialog stays open | M-13, A/B'd against a unique name in the same dialog |
| `enum-def-duplicate-index` (**Error**) | the enum editor has no reorder and no index field; values append and their indices follow insertion order | M-14 |
| `scene-duplicate-target` | adding an output already in a scene to that same scene is rejected; the same output in a *different* scene is accepted | M-15, A/B'd in one pane state |
| `scene-member-unwired` | neither member dialog carries an output selector, and a member exists only as one half of a reciprocal pair | M-15 — §6's claim that the vendor tooling authors this has no evidence behind it |
| `struct-modified-stale` | `<modified>` is re-stamped on every save | M-16, two consecutive saves |
| `struct-icon-default` | not one element-properties dialog in the application carries an icon picker | M-17 |
| `name-helpfile-missing` | `helpfile` is supplied by a library `.ifb`; no dialog writes or clears it | M-17 — whether the PDF resolves is a property of the machine, not of the authoring |

Rows that are **untestable or unwitnessable here**, not falsified:

- `addr-wireless-channel-shared`, `addr-dimmer-channel-duplicate` — need a controller and real
  hardware; no product dialog exposes a serial number or channel address (M-8).
- `logic-case-duplicate-value`, `logic-case-value-foreign` — `Ny case værdi` writes its `case_action`
  under the **left pane's** caret rather than into the selected `program_case`. Four routes were driven,
  including the vendor's own documented gesture (right-click the Case row → *Ny case-værdi...*, delivered
  as real keys), and all three that insert at all obey the same rule; a one-variable A/B moved the parent
  from `<functionblock>` to `<resource_counter>` to `<resource_flag>` by moving only the TV1 caret. Since
  the left pane holds no `program_case` in any view — the vendor's own help screenshot confirms our pane
  layout is theirs — no caret position exists that would land it correctly (M-11, M-22).
  `project5-Dokumentation.vis` carries correctly nested branches authored through this same driver, so
  this is an **unfound route, not a refusal**.
- `doc-no-enduser-products` — **mutually exclusive with `dev-shutter-traveltime-zero`.** The catalogue
  holds exactly two products with shutter travel times, both airlink, and IHC Visual itself writes
  `enduser_report="yes"` on each at insert time (no `.def` declares it); no airlink dialog carries the
  checkbox. So any project witnessing a shutter travel time necessarily carries a product that cannot be
  unflagged. This fixture keeps the shutter row; the DOC row needs a fixture with no shutter product
  (M-18, M-21).
- `capacity-modules-exceeded`, `capacity-wireless-exceeded`, `capacity-resources-high` — out of reach
  at any practical fixture size (hundreds of products).

One row's premise is wrong without being false: `dev-dimmer-load-mode-auto` reads as though automatic
were the factory state. It is not — `Belastnings karakteristik` defaults to **`RC`**, so leaving it on
automatic is a deliberate choice rather than an oversight.

## 6. Maintenance

- The file is a **byte oracle**: `ProjectByteFidelityTests` pins it through both `ProjectSerializer`
  and `ProjectAppService.Save(PreserveExistingMetadata)`. Regenerate it only by driving IHC Visual —
  never by hand-editing, and never through an SDK write path.
- `ErrorsFixtureFindingsTests` pins the two properties that make it an oracle: **no structural finding**
  (every condition here is user-sourced and non-fatal), and the eight implemented documentation checks
  firing exactly where authored — including the control product staying silent.
- When a catalogue row moves from unimplemented to implemented in the SDK, add its assertion to
  `ErrorsFixtureFindingsTests` against this fixture rather than introducing another one.
- Four mechanics cost real time and will cost it again:
  - A drag verb **mis-aims once its target pane has scrolled** and still reports `Ok` — collapse the
    pane first, and probe with a bogus `--method`, whose refusal names the live option family (M-10).
  - `Indsæt ▸ Ny case værdi` follows the **left** pane's caret, not the selected case row — on every
    route, including the vendor's own context-menu gesture (M-11, M-22).
  - `Powerup hændelse` is reachable **only** through `menu invoke-bar`, and only with the events group
    both selected and pane-focused (M-19).
  - A drop-popup item ending in a bare `=` is an **inter-variable** assignment, not an inline constant:
    `Startværdi =` committed `Startværdi = Tæller`. Only `= 0` / `= ON` / `= OFF` are literals (M-19).

## 7. What the implemented checks report today

The eight implemented US-072 checks produce **44 findings** on this fixture, all `Warning`, all in the
Documentation category; the five naming rows in §7.1 add **10 more**, of the same severity and category.
The exact counts are pinned by `ErrorsFixtureFindingsTests`, because
"fires at least once" is satisfied by both failure modes that matter — a check that collapses onto one
element, and one that fans out over the whole project.

⚠ **Two different units.** The five product-level rules count **products** (a six-button product carries
one missing Id-kode, not six); the three terminal-level rules count **terminals**.

| Rule | Count | Unit | Where |
|---|---|---|---|
| `doc-documentation-tag` | 4 | products | P2, P6a, P6b, P5b |
| `doc-power-group` | 4 | products | P2, P6a, P6b, P5b |
| `doc-cabletype` | 4 | products | P2, P6a, P6b, P5b |
| `doc-position` | 4 | products | P2, P6a, P6b, P5b |
| `doc-cablenumber` | 5 | products | the four above **+ P5**, whose Kabeltype is filled — the §6 one-sided-documentation case |
| `doc-not-linked` | 10 | terminals | P2 ×5 (all but the fan-out), P6a ×1, P6b ×2, P4's output, P5's output |
| `doc-cable-colour` | 8 | terminals | P2 ×2, P6a ×2, P6b ×2, P5, P5b |
| `doc-address` | 5 | terminals | P2's `Tryk (øverst venstre)`, P6b ×2, P5, P5b |

Most rows fire on **more** elements than §2 names as their witness, and that is expected: §2 records the
*designed* witness, while the extra instances fall out of other deliberate choices (P6b is left wholly
unaddressed to witness `addr-module-partial`, which necessarily leaves both its terminals unaddressed
and uncoloured too). Do not "fix" the surplus — it is the cost of the witnesses that produce it.

**Independent corroboration (2026-08-09).** The unofficial third-party reporter `jemi.dk/ihc/docs` was
run over this fixture. It names **the same elements** for all eight kinds, reports **no ninth kind**,
leaves the control product `Lampeudtag` silent, reproduces the one-sided-documentation case on P5, and —
like this implementation — reports nothing for the RS485/airlink/S0/bus products, whose
`documentation_tag` is empty. Its own totals are larger on the five product-level rules (79 findings
against 44) purely because it repeats a product-level gap under every terminal of the product; see the
appendix of [`problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md).

⚠ **What that agreement is worth.** The tool is unofficial, may be incomplete, and **has no severity
model** — it emits a flat list. So it corroborates *detection* (which conditions, on which elements) and
says nothing about severity. Two implementations can also share a blind spot. The oracle is this
fixture, whose content was authored deliberately and is recorded above — not the third-party report.

### 7.0 Logic rows — the enum set (T054)

The `Zoo` locality's four authored enumerator types produce **6 findings**, all `Warning`, all in the Logic
category, counted by `Fixture_CarriesExactlyTheseLogicConditions`.

| Rule | Count | Where |
|---|---|---|
| `enum-def-empty` | 1 | `Tom enum` |
| `enum-def-single-value` | 1 | `Enkelt`, whose only value is `Kun` |
| `enum-def-unused` | 4 | **all four authored types**, `Brugt` included |

⚠ **Why `enum-def-unused` fires four times and not once.** §4 records (M-14) that IHC Visual cannot bind a
user-created enumerator type to a variable at all — `Indsæt ▸ Variable` offers a fixed 21 entries and none of
them is an enumerator. So every user-created type in every project is necessarily unreferenced; `Brugt` is
named for the intent it was authored to carry (`enum-value-unused`), not for a binding the application can
make. The row is still correct: the type really is dead in the project and in the reports. The two shipped
`typeid` tables are excluded — they are read-only furniture, unreferenced in most authentic files.

### 7.0c Logic rows — the program-shape set (T056)

| Rule | Count | Where |
|---|---|---|
| `logic-program-no-events` | 1 | the `Zoo` program that carries commands and no trigger |
| `logic-program-no-actions` | 1 | the `Zoo` program that carries a trigger and no commands |
| `logic-subprogram-no-conditions` | 1 | the sub-program whose `conditions` container is empty |
| `logic-case-no-branches` | 2 | the two case nodes with no `case_action` at all |

⚠ **The two "empty program" rows never both fire on one program.** `logic-program-no-actions` requires events
to be PRESENT — the row's own wording — so the empty default program every inserted block ships is the events
row's finding alone. Measured over the corpus: exactly one program has events and no commands, and it is here.

⚠ **`logic-case-duplicate-value` is implemented and cannot be witnessed here.** §5 records the measurement:
`Indsæt ▸ Ny case værdi` writes its branch under the LEFT pane's caret instead of into the selected case node,
and the left pane never holds a `program_case`. Four routes were driven, including the vendor's own documented
right-click gesture. So a duplicate case value only reaches a file by hand-editing, and the rule is tested
against hand-built trees in `ProgramShapeRulesTests`.

### 7.0a Logic rows — the variable-usage set (T057)

Thirteen findings, all `Warning`, all over the shared program read model.

| Rule | Count | Where |
|---|---|---|
| `logic-variable-unused` | 4 | `Zoo`'s declared state variables no program touches and no link reaches |
| `logic-variable-write-only` | 3 | assigned by a program, never read, never linked |
| `logic-variable-read-only` | 1 | read by a program, never assigned — an internal variable, not a setting |
| `enum-value-unused` | 5 | every value of the four AUTHORED enumerator types |

⚠ **`enum-value-unused` counts five because of M-14, not because the fixture is odd.** The application
cannot bind a user-created enumerator type to a variable at all, so no value of one can ever be referenced;
the row states a true fact the GUI offers no way to fix. The two shipped `typeid` tables are excluded — they
are read-only furniture whose 11 values are unreferenced in every project, the empty one included.

⚠ **A PIN is never counted by these three rows.** An input's producer and an output's consumer live outside
the block, and the wiring rows own them (`link-fb-input-unfed`, `link-fb-output-unused`). Measured on
`project3`: including pins takes the set from 9 findings to 64. A `settings` variable is likewise never
reported as read-only, because a dialog-set value is *supposed* to keep the value it was given.

⚠ **`logic-case-value-foreign` cannot be witnessed here or anywhere in the corpus.** The chain is
branch → inline operand → `inivalue`, and every committed branch tests a value its switch's type declares.

### 7.0a-2 Logic rows — the dataflow set (T058)

Nine findings, all `Warning`, all predicates over the shared program read model. **All six rows are
witnessed here**, which is why this fixture is the one that proves the set.

| Rule | Count | Where |
|---|---|---|
| `logic-output-never-assigned` | 3 | linked outputs no program assigns |
| `logic-flag-never-cleared` | 2 | flags written only by `%P = ON` |
| `logic-counter-never-reset` | 1 | a counter written only by `%P = %P + 1` |
| `logic-timer-unused` | 1 | a declared timer no activation command starts |
| `logic-self-trigger` | 1 | `Selvudløser`, a program triggered by the flag it assigns |
| `logic-contending-writers` | 1 | a variable written by two programs whose triggers share no ancestry |

⚠ **`logic-contending-writers` counts ONE here, and that is the whole design of the row.** Comparing trigger
variables directly makes the standard ON/OFF block shape look like a contention — one program sets the output
ON, another sets it OFF, each from its own pulse flag — and reports 8 on this fixture, 24 on `project3` and 9
on `Project1`. Both pulse flags are written by programs triggered by the same button, so their trigger
ANCESTRIES meet, and the shape is related rather than contending. Do not "fix" the fixture to make more of
them fire.

⚠ **Starting a timer is not assigning one.** The fixture's timer is written by an assignment and still
reported, because only the three activation commands (`_0xbe`/`_0xc8`/`_0xd2`) start a timer.

### 7.0b Logic rows — the function-block shape set (T055, completed by T055a)

| Rule | Count | Where |
|---|---|---|
| `logic-block-empty` | 2 | `Tom blok` and `Kobling`, both of which had their default `Program` deleted (§3) |
| `logic-block-no-pins` | 1 | `Tom blok` alone — `Kobling` has pins, which is what makes it the `link-through-empty-block` witness |
| `logic-duplicate-program` | 1 | the `Zoo` block's two identical programs — the only duplicated pair in the whole corpus |
| `logic-master-block-modified` | 1 | `Kip tænd sluk (lokalt tilpasset)`, renamed away from its insert name while keeping `Nummer`/`Version`/`Oprettet`/`Udviklet af` |
| `logic-block-locked-content` | 1 | the same block's `Timer`, moved from 3 to 5 minutes under `locked="yes"` — the note below is why it took a second task |

⚠ **`logic-block-locked-content` now HAS a rule, and this fixture is its witness** — the `Timer` setting
edited from 3 to 5 minutes under `locked="yes"`, which §3 recorded long before anything could see it. Two
things had to be true first. D27 gave the validation context a LIBRARY port (declared and skipped when
absent, exactly as the capacity rows treat controller limits), because nothing in the file distinguishes an
edited value from a library default. And the value had to be read where a timer keeps it: NOT in `value` or
`inivalue`, which a `resource_timer` does not carry at all, but in its `hour`/`minute`/`second`/`millisecond`
attributes — a first implementation that read only the two obvious attributes produced the eight authentic
findings below and missed the one designed witness. The id-ordering proxy T055 considered stays refuted; see
its entry.

⚠ **The row also reports ordinary configuration**, and that is the row rather than a defect: eight settings
across three locked library blocks in `Project1` and `project3` (`PIR styring` in both, plus `Trådløs / Bus
lysdæmper`) differ from their library defaults because an installer configured them, which the vendor lock
permits. Its reasonable-disagreement column — *lock applied after the
edit, deliberately* — is what a reader dismisses those with.

⚠ **Neither ⊘ duplicate row can be witnessed here**, and §5 already records why: the enum editor answers
*"Vælg et andet navn"* to a duplicate name and has no index field at all. Those two rows are tested against
hand-built trees in `EnumDefinitionRulesTests` — including the case that matters most, an absent `index`
colliding with an explicit `index="0"`, because the canonicalizer elides the default.

### 7.3 Project-structure rows (T060)

Five findings, all `Warning`, counted by `Fixture_CarriesExactlyTheseStructureConditions`.

| Rule | Count | Where |
|---|---|---|
| `struct-locality-empty` | 1 | the locality still named `Lokalitet`, which holds nothing |
| `struct-locality-no-devices` | 1 | the `Logik` room, which holds blocks and no hardware |
| `struct-product-no-terminals` | 1 | **P8 `SMS Modem`**, exactly as §2 records it |
| `struct-orphan-block` | 2 | `Tom blok` and the second unwired block |

⚠ **The dimmer and the logging sensors are NOT reported as terminal-less**, and that is deliberate: an RS485
dimmer's `rs485_led_dimmer_channel` children and a bus sensor's `resource_*` measurements are what an author
wires. Reading the row as "no `dataline_*` child" reports 3 to 4 products in every project; reading it as
"nothing wirable at all" reports the modem alone.

⚠ **No capacity row fires here.** This fixture holds ONE modem (so `capacity-modem-multiple` stays silent),
and the three controller-capability rows are not evaluated at all without a declared capability profile —
which the corpus run does not supply, by design.

⚠ **`struct-icon-default` cannot be witnessed here or anywhere in the corpus**, and `struct-modified-stale`
has no rule at all. The first needs an element whose kind otherwise carries icons — no dialog offers an icon
picker, so only a hand-edited file can carry it. The second was ruled out: `modified` is re-stamped on every
save, so the condition cannot hold in a saved file.

### 7.1 Naming rows (T052)

The five NAMING rows are DOCUMENTATION too, and this fixture witnesses **all five** — 10 further findings,
counted and pinned by the same test. They are listed apart from the eight above because their unit is a
third one: a *collision* row counts the SECOND holder, so a duplicated pair is one finding, not two.

| Rule | Count | Unit | Where |
|---|---|---|---|
| `name-empty` | 1 | element | the unnamed `product_dataline` in `Lokaliteter` (position `Skab`, Id-kode `ID-9`) |
| `name-default` | 2 | element | the `Tom blok` function block, and the locality still named `Lokalitet` |
| `name-duplicate-siblings` | 5 | collision | two `Indgang` inputs in `Kobling`, two `Udgang` outputs in `Kobling`, two `Indgang` inputs in `Zoo`, two `LK FUGA Tryk 2 tast` products in one locality, two `Modtager relæ` products in one locality |
| `name-id-code-duplicate` | 1 | collision | the two `Stikkontakt` products both carrying `ID-7` |
| `name-cable-number-duplicate` | 1 | collision | the same pair, both carrying `K-7` |

⚠ **The two `Stikkontakt` products are NOT a `name-duplicate-siblings` witness**, and that is the row's
scope rather than an oversight: they sit in DIFFERENT localities, and two rooms may each hold a socket of
the same name. They collide only on the two documentation values that are supposed to identify ONE unit
project-wide — which is exactly the distinction between the sibling row and the two code rows.

⚠ **What this fixture does NOT witness for these rows:** nothing. Every naming row fires here, which is
why the naming set is the first one whose baseline count moves for all five ids at once.

### 7.2 The four remaining documentation rows (T053)

| Rule | Count | Unit | Where |
|---|---|---|---|
| `name-note-missing` | 5 | pin | the five hand-authored block inputs carrying no `note` (`Kobling` ×3, `Zoo` ×2) |
| `name-power-group-variant` | 1 | element | the `Stikkontakt` whose light group is spelled `stue` where the rest of the project says `Stue` |
| `doc-project-info-blank` | 1 | project | all three masthead blocks are blank, so every report masthead renders `--` |

⚠ **`doc-no-enduser-products` cannot be witnessed in THIS fixture, and that is structural.** IHC Visual
writes `enduser_report="yes"` on each of the catalogue's two shutter products at insert time and no airlink
dialog carries the checkbox that clears it — so any project witnessing `dev-shutter-traveltime-zero`, as
this one does, necessarily carries a flagged product. The synthetic corpus trees in
`ValidationCharacterizationTests` witness it instead; do not "fix" this by unflagging a shutter, which
would cost the shutter witness and could not be reproduced in IHC Visual anyway.

⚠ **The masthead row is deliberately the ALL-THREE reading**, not the literal *project, customer or
installer*: the vendor leaves `customer_info` blank in 15 of the 20 committed projects, so the literal
reading would report almost every authentic file. This fixture had its three blocks **cleared** on purpose
(IHC Visual pre-fills `programmer` with the Windows user name), which is what makes it the witness.
