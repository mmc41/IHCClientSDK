# `Project6-Errors.vis` — the finding-catalogue oracle

**What it is.** A vendor-written IHC Visual project carrying a **deliberate instance of every non-fatal
condition** in [`applications/ihc_openvisual/docs/error-list.md`](../../../applications/ihc_openvisual/docs/error-list.md)
that IHC Visual will actually let a user author — plus the catalogue's **deliberate non-findings**, and
an **issue-free control product** that must appear in no finding list at all.

**Why it exists.** `error-list.md` §5 proposes ~87 *user-sourced* rows on the strength of §3's decision
procedure: a row is User-sourced when *"the state is reachable by ordinary authoring"*. That claim was
never tested against the vendor application. This fixture tests it one row at a time: a row IHC Visual
authors is **confirmed**, a row it **refuses** is not user-sourced at all and belongs in §4 (file-sourced).
Eleven rows were falsified that way — see §5 below.

**Provenance (A-1).** Authored **exclusively by driving LK IHC Visual 03.04.72.03** (`C:\Program Files
(x86)\LK IHC Control\IHC Visual`, catalog 100 products / 72 function blocks) through the
`IHCVisualAutomation` CLI (`app.exe`, published self-contained win-x86), elevated, on 2026-08-09.
**No byte was hand-edited** — no text editor, no script, no SDK write path ever touched it. The live
measurement log is `tmp/p6/measurements.md`; the design is `tmp/p6/design.md`.

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

Each was measured against the live application; the evidence is in `tmp/p6/measurements.md`.

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
Documentation category. The exact counts are pinned by `ErrorsFixtureFindingsTests`, because
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
appendix of [`error-list.md`](../../../applications/ihc_openvisual/docs/error-list.md).

⚠ **What that agreement is worth.** The tool is unofficial, may be incomplete, and **has no severity
model** — it emits a flat list. So it corroborates *detection* (which conditions, on which elements) and
says nothing about severity. Two implementations can also share a blind spot. The oracle is this
fixture, whose content was authored deliberately and is recorded above — not the third-party report.
