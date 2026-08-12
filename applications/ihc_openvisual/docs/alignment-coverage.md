# Alignment coverage ledger — OpenVisual vs IHC Visual

**What has been measured**, per checklist dimension and per member of each set-valued dimension.
This file answers the one question a findings list structurally cannot: *what has nobody looked at?*

It is the durable companion to [`checklist.md`](checklist.md), which sets the dimensions and the
rules but deliberately holds no results. The division of labour:

| File | Holds | Answers |
| --- | --- | --- |
| [`checklist.md`](checklist.md) | dimensions and rules | *what must match, and what counts as evidence* |
| **this file** | **coverage state** | ***what has been measured, and what has not*** |
| [`product.md`](product.md#differences-from-the-original-ihc-visual) | registered deliberate differences | *which mismatches are decisions* |
| [`stories/`](stories/) | required behaviour | *what correct means* |
| a campaign record | findings, in discovery order | *what to do next* |

A campaign **reads this file before choosing work and updates it after measuring**. Findings order
work; only this ledger states coverage.

> **Provenance.** Seeded 2026-08-11 from the dialog matrix and dimension audit of the 2026-08-10
> campaign. Evidence from earlier campaigns was **not** carried over: those records live in untracked
> `tmp/` files that did not survive their session, so their coverage claims can no longer be checked.
> A cell here is not a claim that nothing was ever measured — it is a claim about what can still be
> *shown*. That loss is the reason this file exists; do not recreate it by recording coverage in `tmp/`.
>
> Corrected the same day after an external review: cells claiming more than the surviving evidence
> supports (dimensions 1 and 7, the module-map functionality cell, the value-type and report set rows,
> the dimension 12/14 counts, and dimension 16's provenance wording) were demoted or recounted.

## How to read a cell

| Mark | Meaning |
| --- | --- |
| **✓** | Measured on both sides and resolved — matched, or divergence recorded and closed |
| **~** | Partially measured: some members or some states done, the set incomplete — or fully measured with the divergence recorded but still **open**: an open divergence is never ✓ |
| **✗** | No evidence. **Every ✗ names its reason** in the same row |
| **n/a** | Out of scope — maps to the checklist's Non-checklist, or does not exist for this member |

Four rules decide what may be marked ✓. They are the recurring ways a cell has been marked done
while nothing was measured:

1. **Coverage is a capability, not an artefact.** "Menu label *Datalinie moduler…* — match" is true and
   proves nothing about the dialog behind it. Record what the surface *does*, never that its name
   agrees.
2. **A screenshot or a control dump proves presentation only.** Dimensions 9, 10, 11, 13 and 17 need a
   **gesture transcript** — an activation actually performed and its resulting state read back.
   `enabled: true` is not `editable`; a grid can report every row enabled and refuse every keystroke.
3. **A measurement is scoped to the state it was taken in.** See *State axes* below. One state
   measured is `~`, never ✓, for any surface whose behaviour can depend on that axis.
4. **A criterion the driver cannot exercise is a driver gap, not a pass.** Mark ✗, name the missing
   verb in *Driver gaps*, and fix the driver.

## Coverage summary

| Scope | ✓ | ~ | ✗ | n/a | Total |
| --- | --- | --- | --- | --- | --- |
| Checklist dimensions | 0 | 14 | 5 | — | 19 |
| Dialogs × dimensions 12/13/14 | 14 | 24 | 15 | 4 | 57 |

*Last updated 2026-08-12 (metadata campaign T008 — the Airlink/wireless family added as its own §2 row,
measured across ALL 24 wireless products from the recorded oracle and answering open point O3:
`serialnumber` is not surfaced in the vendor's wireless dialog at all, in any form. +1 row = +3 cells:
~ 23→24, ✗ 13→15.)*

*2026-08-11 (metadata campaign T006 — the RS485 LED dimmer and S0 device families added as
their own §2 rows from the recorded 100-product vendor oracle, answering open point O2: the LED dimmer's
dialog carries no per-channel advanced settings and no channel selector, and the S0 device's ten metering
resources do not appear in its dialog at all. +2 rows = +6 cells: ~ 21→23, ✗ 9→13. Earlier the same day
(elevated session): Phases 0–4 measured, then Phase 5 applied the owner-confirmed fixes — #2 ramp-unit and
#4 empty-product-name fixed with tests; #1 enum-feedback and #5 Danish save-guard registered as intentional
differences with pins; #3 load-mode stayed open on a newly-surfaced token contradiction. Before that run:
dimensions 0/13/6, dialogs 10/15/16/4.)*

## 1. Checklist dimensions

| # | Dimension | State | Evidence that exists | What is missing |
| --- | --- | --- | --- | --- |
| 1 | Byte-identical saved files | ~ | The `tests/testdata/` oracle corpus, replayed byte-for-byte by `safe_project_tests` — vendor-authored projects and recorded vendor mutations | The checklist scopes this dimension to **all features**, and no per-feature enumeration exists: an edit family without a recorded vendor pair is unverified, and at least one save-producing flow (09/US-050's module editor) is not built yet, so its pair cannot exist |
| 2 | Feature/workflow coverage and route | ~ | Product insert, block landing, report hand-off, section flyouts, the one-modem refusal | No pass enumerated from the Ready stories' acceptance criteria; the route half is measured only where a finding happened to raise it |
| 3 | Model correctness (results, side effects, state after reopen) | ~ | `safe_project_tests`; the decimal/weekday round-trip findings | Not exercised per edit family from the GUI side; rejected-operation set unenumerated |
| 4 | Two-pane tree layout | ✗ | — | Never compared in a record that survives |
| 5 | Configuration and programming modes | ~ | The section flyout's mode dependence; **the programming-mode surface itself compared both sides 2026-08-11** (elevated; same library FB `1.1.01.e. Kip tænd sluk` placed in TV2 on each). **Strong match**: entering programming turns **both** pane headers into the block name; the **left** pane shows the same block I/O in both — *Input* (Kip / Kip med timer / Tænd med timer / Tænd / Sluk, → icons), *Output* (Udgang / ON puls / OFF puls, ← icons; Scenarie Tænd/Sluk), *Indstillinger* (Timer = 00:03:00,000), *Interne variable* (Puls timer = 00:00:00,250); the **right** pane shows the same *Programmer* tree with the same seven sub-programs in the same order (Kip, Kip med timer, Tænd med timer, Timer udløber, Tænd, Sluk, Puls Timer = 0). Route matches: select the FB in TV2 → enter; Esc / *Konfigurationsvisning* → exit | **Divergence (open, dim 2/5 presentation):** OpenVisual's **status bar announces the mode** — *"Programmeringstilstand (skrivebeskyttet — blokken er låst). Tryk Esc for at vende tilbage."* — where the vendor shows no mode/read-only/how-to-exit message. Both open the locked library block **read-only** (matched behaviour; only the announcement differs). Deeper program-body structure (Hændelser/Kommandoer/Betingelser) not yet diffed node-for-node; **dim 8** (what each mode permits — unlock-to-edit) not yet exercised |
| 6 | Tree content, order, nesting, expansion, selection | ~ | Product/terminal/scenario rows, link and program rows, what the tree opens when a block lands, decimal and weekday row text | Expansion and selection state not compared as such; no whole-tree dump pair in a surviving record |
| 7 | Menus and toolbars, and their enablement | ~ | Menu bar and toolbar member by member, context menus, the product menu's ~100 members and its two ordering rules, greying rules | The membership half is done; the enablement half was measured without crossing the axes it depends on — selection and dirty state never, mode only where a finding forced it (§4). Rule 3: one state measured is ~, never ✓ |
| 8 | Tree-node actions | ~ | Copy/paste on terminals and pins, link rows, program rows, section flyouts, the enum picker | Not every node type's action set enumerated and individually resolved |
| 9 | Drag and drop (what can and cannot be dragged) | ✗ | — | No gesture transcript on either side. **Capability now proven** (2026-08-11: elevated session, real foreground clicks land via logical coords, `dialog.clickRow` drives real mouse) — the blocker is no longer access but that a **dedicated drag campaign** was not run this session (enumerate refusals from the Ready stories first — rule). Both permitted and refused halves still unmeasured |
| 10 | Mouse/pointer behaviour, with and without modifiers | ✗ | — | No gesture transcript. Capability proven this session (see dim 9); modifier-qualified gestures still not driven. **Not run this session** — deferred, not blocked |
| 11 | Keyboard behaviour, shortcuts, focus, menus, dialogs | ✗ | — | No gesture transcript. Real modifier keys reachable in an elevated foreground session (proven this session via `key send`/menu walks); a focus-order + shortcut + dialog-keyboard pass was **not run this session**. Deferred, not blocked |
| 12 | Dialog layout and shape | ~ | See §2. 2026-08-11 added `AdvancedDimmerWindow`, `EnumDefinitionWindow`/`NamePromptWindow` (both sides) and the vendor SMS-modem dialog inventory (OpenVisual's own `ModemPropertiesWindow` was deleted 2026-08-12, T029 — the modem renders from a composed descriptor now) | the wired-product dialog still not field-for-field on the dataline family; `SceneContainer/SceneValue`/`ProjectInfo` not re-verified this pass |
| 13 | Dialog functionality (buttons, links, activatable rows, sub-dialogs) | ~ | See §2. 2026-08-11: **`Avanceret` pressed both sides** (in-place expand vs separate window); **module-map per-column `dialog.clickRow`** resolved (every column opens the one per-module dialog); enum `Ny`/`Slet` create+delete driven; modem has no sub-dialogs | Scene dialogs' row activation, `ProjectInfo` controls, and the decimal/date dialogs' buttons still not driven |
| 14 | Validation, errors, warnings, confirmations, recovery | ~ | **§2.1 — the vendor's invalid-input behaviour measured across ALL FIVE product families 2026-08-12 (elevated)**: only two fields validate at all (modem phone slots, S0 pulse count), both by modal MessageBox at commit returning to the still-open dialog; the modem PIN coerces silently; no over-long-text refusal exists anywhere; and both refusal messages misstate their own rule. Plus: the one-modem refusal (F-47); `PinPropertiesWindow`'s read-only refusal; `EnumTypeManagerWindow`'s greying rule; **plus 2026-08-11 (elevated):** the **enum create-type/value empty-name divergence** — vendor silently discards + closes, OpenVisual keeps the dialog open with inline `Indtast et navn.` (recorded open, both sides); the vendor **enum type-delete has no confirmation** even for a type holding a value; the vendor **module-map per-module dialog's cascade** (Lokalitet/Note disabled until Modul type is chosen) | Still no evidence on: the product dialog, `VariablePropertiesWindow`, `SceneContainer/SceneValue`, `ProjectInfoWindow`, decimal/date dialogs. No invalid-commit or recovery path compared as a set. OpenVisual counterpart of the module cascade absent (unbuilt, 09/US-050) |
| 15 | Tooltips and in-place help | ✗ | — | Never compared. `stories/13-tooltips.md` is Ready and undemonstrated against the original |
| 16 | Generated output: reports and validation results | ~ | The 24 report oracles in `tests/testdata/reports/` — **generated by `ProjectAppService`, not captured from the original** (`tests/testdata/testdataoverview.md`), so they pin OpenVisual against its own output: regression evidence, one-sided | No vendor-side report capture survives to compare any kind × mode × format against (the report model's vendor derivation is unrecorded history — see Provenance); validation/finding output never compared against the original's |
| 17 | Undo/redo history, grouping, labels, dirty state | ~ | **Menu-bar state compared live both sides 2026-08-11** (each app carrying one pending edit): **availability matches** — *Fortryd*/*Undo* **enabled**, *Gentag*/*Redo* **disabled** (nothing redone), paste disabled (empty clipboard), on both. **Labels** confirm the registered **F-8b** difference live: vendor *Fortryd* is **bare** (id 57643), OpenVisual names the action — *"Fortryd Indsæt funktionsblok"* — while *Gentag* is bare on both when there is nothing to redo. Also confirmed: the vendor *Rediger* menu carries *Autobackup egenskaber…* (id 30505) which OpenVisual omits (registered "No auto backup" exclusion). Both apps are dirty (OpenVisual title `•`; vendor `dirty`) | Undo **not actually executed** — how steps group, multi-step history, and the saved↔dirty **transition** across an undo are not driven; the dirty axis itself (edit → save → undo) is uncrossed |
| 18 | Visual/UI semantics: text, icons, decorations, states | ~ | Dialog titles as a set, captions, greying rules, toolbar grouping, terminal row wording | Not held across themes or text scales — see *State axes* |
| 19 | UX and accessibility | ~ | Menu grouping, dialog captions, the accessible names of terminal rows, keyboard-shortcut reasons | Keyboard-only operation never demonstrated end to end; no accessibility-tree pair |

## 2. Dialogs — dimensions 12 / 13 / 14

The checklist makes "the dialogs" a set-valued dimension: it is complete only when **each** member is
individually resolved.

| Dialog (OpenVisual view) | 12 layout | 13 functionality | 14 validation | Reason for any ✗ |
| --- | --- | --- | --- | --- |
| `PropertiesWindow` (locality / block) | ✓ | ~ | ✗ | 13: OK/Cancel only — it has no sub-dialogs. 14: no invalid input ever submitted |
| `VariablePropertiesWindow` | ✓ | ✓ | ✗ | 14: no invalid input ever submitted |
| Wired products (was `ProductPropertiesWindow`; since T030 the composed descriptor through `ProductDialogWindow`) | ~ | ~ | ✗ | 12: wireless-dimmer family compared field-for-field 2026-08-11 (elevated session; fresh untitled template + one inserted `Lampeudtag dimmer`, config mode, display scale 1.75): the **reopened** dialogs' field sets match (Navn disabled / Placering / Note / Identifikationskode / Lysgruppe; no cable fields on either side) — but two divergences recorded, open: (a) OpenVisual's **insert-time** dialog differs from its own reopen dialog (generic title "Produkt egenskaber" vs product name; Navn **editable and empty** vs disabled; **Kabeltype/Kabelnummer shown** on a wireless product; no Avanceret) — the vendor's insert-time dialog was not captured (driver auto-commits it), so only the OpenVisual-internal inconsistency is measured; (b) **RESOLVED (Phase 5, 2026-08-11):** a real vendor `.vis` stores a product's `name` = its catalog type name at insert (`<product_dataline … name="Lampeudtag">`); OpenVisual left it empty, so an un-renamed product fell back to its raw element tag in the tree. Fixed at the SDK insert (`GroupRef.AddProduct` now stamps `name = DisplayName`), byte-matching the vendor and giving the tree the type name (`AddProduct_StoresCatalogNameAsProductName`; the whole byte-oracle corpus stayed green). Dataline/other families still not field-for-field. 13: **`Avanceret` pressed on both sides 2026-08-11** — vendor **expands the same dialog in place** (a hidden "Avancerede Dimmer egenskaber" group plus a second button row `Normal`/`OK`/`Annuller`; `Normal` collapses back; title unchanged), OpenVisual **commits the doc fields and swaps to a separate `AdvancedDimmerWindow`**; cancelling it does **not** return to the product dialog. Divergence recorded, open. 14: never attempted |
| `PinPropertiesWindow` (*Klemme adressering*) | ✓ | ~ | ~ | 13: the two address lists driven, not every control. 14: read-only refusal proven, nothing else |
| SMS modem (was `ModemPropertiesWindow`; since T029 the composed descriptor through `ProductDialogWindow`) | ~ | ~ | ~ | **F-52 CLOSED (2026-08-12, T029):** the modem now renders from its composed descriptor through the one generic dialog, so all 30 `Nummer` slots are offered and editable, and the phone rule is enforced with a stated refusal that names the slot — closing the 14 gap below as well. 12: both sides inventoried, divergence recorded. **Vendor side re-confirmed 2026-08-11 (elevated, SMS Modem inserted under Stue): 91 controls in four captioned groups** — *Modem egenskaber* (Navn disabled = "SMS Modem", Note/Placering/Identifikationskode combos), *Kabling* (Ledningsfarve 0V/24V/RS485±, four cable-colour combos), *Indstillinger* (Pin Kode edit, default "1234"), *Telefon numre* (`Nummer 1–30`, all enabled). OpenVisual side = 39 controls / `Nummer 1–4` (earlier read). Open, so not ✓; the phone count is dimension **2**, not 12 — 26 absent recipient fields are an absent capability. Combo-vs-edit rows: the vendor's Note/Placering/cable-colour are droplist combos, which is the registered suggestion-drop-down difference (`FreeTextFieldParityTests`) — but whether the *Pin Kode* and the phone fields fall under it is unruled. 13: **driven 2026-08-11** — the vendor dialog has **no sub-dialogs, links or activatable rows**; every control is an Edit/ComboBox and the only buttons are OK/Annuller, so 13 reduces to commit/cancel (same expected on OpenVisual). 14: the one-modem refusal is measured + registered (F-47); no per-field validation exists on either side to compare (free-text phone/pin), so 14 is `~` pending an explicit invalid-commit test |
| `ModuleMapWindow` (*Datalinie moduler*) | ~ | ~ | ~ | 13 re-driven 2026-08-11 (elevated, per-column `dialog.clickRow`): the vendor has **two `SysListView32` grids** — *Indgangsmoduler* (8 rows) and *Udgangsmoduler* (16 rows), each row four columns `N \| <ikke i brug> \| \| `. Double-clicking **columns 0 and 2** of an input row both open *Indgangsmodul tilkoblet datalinie N*; **column 3** of an output row opens *Udgangsmodul tilkoblet datalinie N* — so **no column is individually editable; the whole row opens one per-module dialog** (F-53 corroborated by direct per-column strikes). OpenVisual's rows realize no cells and double-click is `NoEffect` → **unimplemented story 09/US-050**, not a difference to rule on. Open until built and re-measured, so not ✓. 12: the surrounding dialog not compared field-for-field. **14 driven on the vendor 2026-08-11**: the per-module dialog cascades — *Modul type* combo live (input types `<ikke i brug>`/Controller Link/Input 230/24/24·3/IR B&O/IR Indgang; output types Output 1-10V/230·10/230·16/24/400·10/400·16), while *Lokalitet* and *Note* are **disabled until a non-`<ikke i brug>` type is chosen** (verified: selecting "Input 24" enabled both). OpenVisual has no such dialog to compare, so 14 stays `~` |
| `AdvancedDimmerWindow` (*Avanceret*) | ~ | ~ | ✗ | **Opened on both sides 2026-08-11** (elevated session; wireless `Lampeudtag dimmer`, fresh insert, config mode, scale 1.75). Inventories recorded. **Values match**: soft on/off 700/700 ms, min 22 %, max 100 %. **Divergences recorded, open:** (a) *shape* — vendor is an in-place expansion of the product dialog (group "Avancerede Dimmer egenskaber", buttons `Normal`/`OK`/`Annuller`), OpenVisual a separate window titled "Avancerede lysdæmper egenskaber" with `OK`/`Annuller` only; (b) *ramp value* — **RESOLVED (Phase 5, 2026-08-11):** it was a real OpenVisual unit bug — `dimmer_setting_dimming_rate` is stored in **milliseconds** (default 5000, range 2000–10000) and the original shows it as **seconds**, but OpenVisual fed the raw ms into a seconds-labelled box. Fixed: the command multiplies the dialog's seconds by 1000 on write and the read divides by 1000, so 5000 ms shows as 5 s and round-trips exactly (`WirelessDimmer_ManualRamp_ConvertsSecondsToMilliseconds`, `MetadataCommandTests.UpdateDimmerSettings_WritesTheSixSettingValues`); (c) *load-mode vocabulary* — **RESOLVED (Phase 5, 2026-08-11, owner ruling "match original vendor app"):** the combo now matches the original — **Auto detektion / RC / RL, Auto first**, label "Belastnings karakteristik" — where OpenVisual previously showed Induktiv / Kapacitiv / Auto (Auto last). Only presentation changed: the stored tokens were already the vendor `.vis` serialization `auto | rc | rl` (the dialog already wrote `rl`, which existing tests exercise), so no token or `.vis` change was needed. The `rl_led` in OpenVisual's *catalog* grammar is faithful to the vendor's own install-catalog DTD (a vendor-internal catalog-vs-project inconsistency) and was deliberately left untouched. Pinned by `AdvancedDimmerLoadModeTests`; (d) *label wording* — "Soft tænd/sluk tid (millisekunder)" vs "Blød opstart/nedlukning (ms)". 13: driven — vendor `Avanceret`→expand, `Normal`→collapse, cancel; OpenVisual `Avanceret…`→separate window, cancel, combo items enumerated live. **OK-commit not driven on either side.** 14: no invalid input submitted (T3.3) |
| `SceneContainerWindow` (*Scenarier*) | ✓ | ~ | ✗ | 13: rows read, activation not driven. 14: never attempted |
| `SceneValueWindow` | ✓ | ~ | ✗ | 13: combo read and committed; other controls not driven. 14: never attempted |
| `EnumTypeManagerWindow` | ✓ | ✓ | ~ | Re-driven both sides 2026-08-11 (elevated; untitled template). **Structure matches**: two panes (types / values), the same two read-only built-ins in the same order (`Logning`, `Persienne tilstand`) with identical value sets, `Ny`/`Slet`/`Omdøb` per pane, a single `OK` and **no Cancel** on either side. Greying matches: `Slet`/`Omdøb` disabled with nothing selected; value-pane `Ny` disabled until a type is picked; every button live once a non-read-only type exists (vendor exercised by creating `AlignProbeType`+value `V1`, then deleting — **type delete of a type holding a value is silent, no confirmation** on the vendor). 14: still `~` — vendor's type/value **delete** has no confirmation; empty-name handling lives in the create sub-dialogs (next row) |
| `EnumDefinitionWindow` / `NamePromptWindow` (*Opret ny enumerator type / …værdi*) | ✓ | ✓ | ~ | **Driven in its own right on both sides 2026-08-11.** Both apps open a create dialog titled *Opret ny enumerator type* (value: *…værdi*) with a single Navn field + OK/Annuller; the field starts holding the literal *Navn* (selected). **Dimension-14 divergence, OPEN**: submitting an **empty** name — vendor **silently closes the dialog and creates nothing, no message**; OpenVisual **keeps the dialog open and shows an inline error `Indtast et navn.`** (`NamePromptError` appears; NameBox stays empty). Both refuse the empty name; only the feedback differs. **RESOLVED (Phase 5, 2026-08-11): registered** as an intentional OpenVisual difference (the "keeps its error feedback" principle) in `product.md`, *Pinned by:* `NamePromptValidationTests`. Create-value path confirmed identical in shape (vendor). Rename (`Omdøb`) sub-dialog not yet driven |
| `ProjectInfoWindow` | ✓ | ~ | ✗ | 13: field groups read, controls not driven. 14: never attempted |
| `ReportPickerWindow` | ✓ | ~ | n/a | 13: the vendor side is driver-blind here — the original hands off to an external browser (registered difference). 14: no vendor counterpart to validate against |
| `NamePromptWindow` (generic name prompt — rename etc.) | ~ | ✗ | ✗ | Its **enum-create** use is measured (previous row). Its **rename** use (locality/node rename) not driven in its own right this session |
| Airlink family, composed (was `ProductPropertiesWindow`) — **Airlink / wireless family** (`product_airlink`) | ~ | ✗ | ✗ | **Vendor side measured across ALL 24 wireless products 2026-08-11** (elevated; fresh untitled 10-room template, configuration mode, scale 1.75; each catalog product inserted under *Stue*, opened by double-clicking the placed node, cancelled + undone, nothing saved). Artefacts `tmp/metadatacompare/screenshots/*.ihcvisual.json` for the 24 roster rows with `family = Airlink`, raw control dumps in `tmp/metadatacompare/raw/*.dialogread.json`. **All 24 share ONE field set, with no exceptions**: `Navn` (Edit 361, **disabled**, prefilled with the catalog type name), `Placering` (ComboBox 352), `Note` (ComboBox 351), `Identifikationskode` (ComboBox 412), `Lysgruppe` (ComboBox 355) — one group box *Produkt egenskaber*, two columns, `OK`/`Annuller`; 17 controls total, title = the bare product name. **Answers open point O3: `serialnumber` is NOT surfaced in the vendor's wireless dialog at all** — neither editable nor read-only. Checked three ways: no labelled field reports it (24/24 field JSONs), no raw control dump for any of the 24 mentions *serie*/*serial*/*nummer* in any caption, and the 17-control inventory of `Tryk 2 tast` accounts for every control (5 captions, 5 fields + their combo edit-children, 2 buttons, 1 group box) with none left over. So the wireless preset must NOT carry a serialnumber field: the attribute lives in the file and is not user-editable through this dialog. 12 `~`: vendor half only — OpenVisual's wireless dialog was compared field-for-field on 2026-08-11 for `Lampeudtag dimmer` (see the wired-products row) but not across the family. 13/14 ✗: no sub-dialogs exist to drive, no invalid input submitted |
| LED dimmer family, composed (was `ProductPropertiesWindow`) — **RS485 LED dimmer family** (`product_rs485_led_dimmer`) | ~ | ✗ | ✗ | **Vendor side measured 2026-08-11** (elevated; fresh untitled 10-room template, configuration mode, scale 1.75; catalog `_0x4409` *IHC LED Dimmer 2 kanaler* inserted under *Stue*, opened by double-clicking the placed node, cancelled + undone, nothing saved). Artefacts `tmp/metadatacompare/screenshots/002-_0x4409-IHC-LED-Dimmer-2-kanaler.{png,json}`. **This is the smallest dialog in the whole 100-product set: 1034×369 px, ONE group box *Produkt egenskaber*, THREE fields** — `Navn` (Edit 361, **disabled**, prefilled "IHC LED Dimmer 2 kanaler"), `Placering` (ComboBox 352), `Note` (ComboBox 351, prefilled "LK IHC RS485 produkter") — in a two-column layout (Navn ∥ Placering, then Note), commit row `OK`/`Annuller`. Its title is the **bare product name**, with no *Egenskaber* suffix. **Answers open point O2 for this family: the vendor dialog exposes NO per-channel advanced settings and NO channel selector at all** — no `Avanceret` button, no `dimmer_settings` control, nothing naming a channel; and no cabling group, no `Identifikationskode`, no `Lysgruppe`/`Kabeltype`/`Kabelnummer`. The two `rs485_led_dimmer_channel` containers the element carries are simply not reachable from this dialog. 12 is `~` not ✓: only the vendor half is measured, OpenVisual's counterpart not yet compared field-for-field. 13/14: not driven — no sub-dialog exists to drive, and no invalid input was submitted |
| S0 family, composed (was `ProductPropertiesWindow`) — **S0 device family** (`s0_device`) | ~ | ✗ | ✗ | **Vendor side measured 2026-08-11** (same session and state as the row above; catalog `_0x2313` *S0 Device*). Artefacts `tmp/metadatacompare/screenshots/097-_0x2313-S0-Device.{png,json}`. 1080×595 px, ONE group box *Produkt egenskaber*, **seven fields** in two columns — left: `Navn` (Edit 361, **disabled**, "S0 Device"), `Identifikationskode` (ComboBox 412), `Placering` (ComboBox 352), `Note` (ComboBox 351); right: `ledningsfarve S0-` (ComboBox 410), `ledningsfarve S0+` (ComboBox 524), `Antal pulser pr 1 kW` (Edit 525, **prefilled "100"**). Commit row `OK`/`Annuller`; title again the bare product name. **Answers the second half of O2: the S0 device's ten metering resources do NOT appear in the dialog at all** — there is no kWh row and no resource list of any kind; the dialog is documentation + two cable colours + one pulse constant. **Caption detail worth carrying into the preset:** the two cable-colour captions are **lower-case initial** — `ledningsfarve S0-` / `ledningsfarve S0+` — unlike the modem dialog's capitalized `Ledningsfarve 0V`; caption text is data, and this one is inconsistent in the vendor itself. 12 `~` (vendor half only). 13/14 ✗ as above. Note OpenVisual cannot yet open **any** dialog for this family (its tag lacks the `product_` prefix), which is the separate defect T013 fixes |
| `AboutWindow` | n/a | n/a | n/a | Branding — Non-checklist |
| Save-changes guard (new/close with unsaved edits) | ✓ | ✓ | ~ | **New dialog, measured both sides 2026-08-11** (triggered by `project.new` on a dirty project). **Same semantics** — a three-button Save / Don't-save / Cancel prompt guarding loss of unsaved work. **Divergence (open):** the **vendor prompt is in ENGLISH** — title "LK IHC Visual ®", a Windows question-icon MessageBox reading *"Save changes to    unavngivet?"* with **Yes / No / Cancel** — while **OpenVisual is fully Danish** — title *Gem ændringer?*, *"Gem ændringer i unavngivet før du fortsætter?"*, **Gem / Gem ikke / Annuller**. OpenVisual's Danish chrome is the registered "Danish is the product language" enhancement (`DanishChromeTests`); the vendor's English MessageBox is a **vendor localization gap**. **RESOLVED (Phase 5, 2026-08-11): registered** in `product.md` as an intentional difference, *Pinned by:* `DanishChromeTests` (`SaveChangesGuardIsDanish`, which pins the exact Danish strings). 14: Cancel/No paths exercised (No discards, project resets to the 10-room template); the Save path not driven |

## 2.1 Invalid-input behaviour, per product family (dimension 14 — open point O4)

**Measured 2026-08-12** on the vendor only, in an elevated session: LK IHC Visual holding a fresh
untitled 10-room template, configuration mode, display scale 1.75. Each product was inserted under
*Stue* by posting its catalog command, its dialog opened by double-clicking the placed node, the value
written with a read-back-verified `dialog.setText`, the dialog committed, and the project then undone
back to its 11-node baseline. **Nothing was saved.** All five families were exercised; none had to be
skipped.

*Route caveat, stated because D20 requires it:* the value is placed by `WM_SETTEXT`, not by typing, so
these transcripts are evidence about **what the app does when an invalid value is committed** — the
refusal mechanism, wording, moment and end state, which is what O4 asks. They are **not** evidence
about per-keystroke filtering, except where the write itself was visibly altered (the two "input-level
filter" rows below, where the read-back returned something different from what was requested — that
*is* the control rejecting input). The vendor CLI exposes no character-typing verb, so keystroke-level
filtering cannot be measured further with the current driver.

| Family | Probe | Mechanism | Exact wording | Moment | End state |
| --- | --- | --- | --- | --- | --- |
| `Rs485SmsModem` | Pin Kode `99999`, `12345`, `-5`, `abc` (catalog declares `minimum=0 maximum=9999`) | **Silent coercion — no message at all** | — | commit | Clamped into `[0,9999]` and zero-padded to 4 digits: `12345`→`9999`, `-5`→`0000`, `abc`→`0000`. Dialog closes, commit succeeds. `12345`→`9999` (not `1234`) is what proves this is a **clamp**, not a truncation |
| `Rs485SmsModem` | `Nummer 1` = `12` (2 chars) | **Modal MessageBox**, warning icon, single `OK`, title `LK IHC Visual ®`, 3 controls | `Ugyldigt telefonnummer på talværdi 1 ⏎ skal være mere end 3 cifre` | commit | `OK` returns to the **still-open** product dialog with the invalid value retained; nothing committed. Screenshot `tmp/metadatacompare/screenshots/o4-modem-phone-refusal.ihcvisual.png` |
| `Rs485SmsModem` | `Nummer 1` = `12 34 56` | **Input-level filter** | — | write | The Edit strips the spaces as they are written: the field reads `123456` |
| `Rs485SmsModem` | `Nummer 1` = 60 digits; `Identifikationskode` = 300 chars | Accepted | — | — | Stored verbatim. **No maximum length is enforced anywhere** |
| `Dataline` | `Identifikationskode` = 300 chars | Accepted | — | — | Stored verbatim (commit button `OK` id **3**/514) |
| `Airlink` | `Identifikationskode` = 300 chars | Accepted | — | — | Stored verbatim (commit button `OK` id **1**) |
| `Rs485LedDimmer` | `Placering` = 300 chars | Accepted | — | — | Stored verbatim (commit button `OK` id **436**) |
| `S0Device` | `Antal pulser pr 1 kW` = `0`, or emptied | **Modal MessageBox**, single `OK` | `Antallet af pulser skal være mellem 1 og 10000` | commit | `OK` returns to the **still-open** `S0 Device` dialog; nothing committed |
| `S0Device` | `Antal pulser pr 1 kW` = `abc` | **Input-level filter**, then the refusal above | as above | write, then commit | The Edit is numeric-only: the letters never land and the field is left **empty**, which then fails the `≥ 1` check |
| `S0Device` | `Antal pulser pr 1 kW` = `1`, `10000`, `10001` | Accepted | — | — | All three stored verbatim — **including `10001`** |

**Two findings that matter more than the individual rows.**

1. **Both refusal messages describe a stricter rule than the code enforces.** The phone message says
   *"mere end 3 cifre"* (more than 3) while `123` — exactly 3 — is **accepted**; refusal begins at 2.
   The pulse message says *"mellem 1 og 10000"* while `10001` is **accepted**; only the lower bound is
   checked. Reproducing the vendor here means reproducing a *message that misstates its own rule*, so
   any preset copying these sentences must record which half is real. Measured boundaries: phone
   refused at length 2, accepted at 3; pulses refused at 0, accepted at 1, 10000 **and** 10001.
2. **Validation is the exception, not the rule.** Across five families only two fields validate at all
   — the modem's phone slots and the S0 pulse count. Every free-text field (`Note`, `Placering`,
   `Identifikationskode`, `Kabeltype`, `Kabelnummer`, `Lysgruppe`) accepted 300 characters unchallenged
   on every family tested, and the modem's PIN coerces silently rather than refusing. There is **no
   over-long-text refusal anywhere in the vendor** to match.

*Not measured, named rather than left implied:* the OpenVisual side of these same probes (this pass was
vendor-only); per-keystroke filtering beyond the two cases where the write was visibly altered; and
whether the refusals differ when the dialog is reached by the Egenskaber route rather than by insert.

## 3. Other set-valued dimensions

Each is complete only when every member is individually resolved. Member lists belong to the campaign
that enumerates them; this table records whether that enumeration has happened.

| Set | State | Note |
| --- | --- | --- |
| Node types × their menus and actions | ~ | Product, terminal, pin, link, program, block section and enum covered; the set was never enumerated first, so completeness is unknown |
| Product families (catalog menu) | ✓ | All ~100 members and both ordering rules compared |
| Variable value types | ~ | 18 driven through the variable dialog, but the authoritative registry (`VariableTypeRegistry`, story 07/US-027) holds **19** value types; which member lacks evidence is not recorded — re-enumerate against the registry, not a remembered count |
| Report types × mode × format | ~ | All 24 combinations pinned byte-for-byte — against OpenVisual's own generated output (regression), not a vendor capture; the vendor side of dimension 16 is unmeasured, so the set is one-sided |
| Themes | ~ | 2026-08-11: `Vis ▸ Tema` = **System / Lys / Mørk** (`theme.*`). The main two-pane surface re-captured in **Mørk** — layout, tree content/order, monochrome node glyphs, the blue selection band and the not-linked **warning "!" decoration** (state by glyph, not colour) all survive the switch. **Per-dialog** surfaces not yet re-read in dark; `Lys` not separately captured. Restored to System |
| Text scales | ~ | 2026-08-11: `Vis ▸ Tekststørrelse` = **Lille / Normal / Stor / Størst** (`textScale.*`). Main surface re-captured at **Størst** — menu-bar and tree text and icons scale together, no clipping, layout and decorations intact. Intermediate scales and per-dialog surfaces not separately checked. Restored to Normal |
| Dialogs | ~ | §2 |

## 4. State axes

A surface's behaviour can depend on the state it is measured in. **A measurement carries the state it
was taken in**; generalizing from one state is how a correct measurement becomes a wrong requirement.

This is not hypothetical: the block-section flyout was measured in one mode, generalized to all modes,
built that way, and re-derived as mode-dependent a campaign later. That round trip cost more than
crossing the axis would have.

| Axis | Values | Crossed? |
| --- | --- | --- |
| Editor mode | configuration / programming | Only where a finding forced it (section flyouts) |
| Block lock | locked / unlocked | Partly — an unlocked block's sections were compared; a locked block's view-only behaviour is recorded but not compared as a set |
| Project population | empty / populated | Mostly measured on small populated projects; the empty-project state is compared only for the module map |
| Selection | none / one / multiple | ✗ never crossed |
| Dirty state | saved / modified | ✗ never crossed — bears directly on dimension 17 |
| Theme | light / dark | ✗ |
| Text scale | each supported scale | ✗ |

## 5. Driver gaps

A criterion that could not be exercised because a driver lacks the verb. Each row blocks the ledger
cells it names, and is fixed in the driver — never worked around in the comparison.

| Verb / capability | Side | Blocks | State |
| --- | --- | --- | --- |
| `dialog.clickRow` (activate a row, optionally a column, inside a dialog) | both | Dimension 13's activatable-row half, for every dialog with a list | Added to both drivers 2026-08-11. The vendor `--column` refinement is **published as of the 2026-08-11 elevated session** (schema carries `column`; live cross-check pending first grid use) |
| `dialog.click` intermittently blind on the OpenVisual product dialog (then `ProductPropertiesWindow`, now `ProductDialogWindow`) | OpenVisual | Committing/cancelling the product dialog by its buttons | Measured 2026-08-11: UIA FindAll(ControlType.Button) returned **zero buttons** on the freshly opened insert dialog (raw walk confirmed the footer peers absent from the tree) while `dialog.read` had listed them; minutes later the same call succeeded on the reopened dialog. Avalonia UIA peer materialization is unstable on this window, and its `BoundingRectangle` read stale (946 px vs 1034 px actual) — do **not** click from remembered rects (a stale-rect click in this session landed outside the app and launched an unrelated program). Foreground the modal and re-read before any coordinate use |
| Anything needing the app in the **foreground** | OpenVisual | every `key`-mechanism verb (`edit.undo/redo`, `node.cut/copy/paste`, `programming.enter`, `fb.insertEmpty`, …); `dialog.cancel`; the hover-driven `menu.dumpBar` / `catalog.products` / `catalog.functionBlocks` | A scripted process cannot take the Windows foreground, so these need a session where a person fronts the window. **Pattern-driven verbs do not** — `product.insert`, `locality.insert`, `fb.insertTemplate`, `tree.*`, `menu.invoke --id`, `dialog.read/click/setText/selectItem` all ran headless-ish on 2026-08-11. Prefer `menu invoke --id <row id>` over the `key` twin when the foreground is unavailable |
| `fb.insertTemplate` "needs a click fallback / foreground" | OpenVisual | — (was recorded as blocking programming-tree authoring) | **RESOLVED 2026-08-11 — was a false alarm.** Fully pattern-driven, **no foreground**: with a **Functions-pane (TV2) locality selected** and a valid `--menu-path` (`"00. Foretrukne/1.1.01.e. Kip tænd sluk"`), all four menu segments resolved via `pattern` and the block landed ("Funktionsblokken … er indsat under Stue"). The earlier `PreconditionMissing` came from selecting a **locality in TV1**, which correctly greys the FunktionsBlokke item. **Phase-5 action**: promote the `commands.json` row `partial` → `confirmed`, drop its "whether this raises a dialog… is NOT known" note (it raises none) |

### The declared-but-unwired verbs

Sixteen `aui-openvisual` rows are `status: planned` / `mechanism: notImplemented`, so their route is
`unimplemented` and their transcript is evidence of nothing. **Most are conveniences with a working
generic route**, and naming that route is the point of this table — an unwired verb is only a coverage
gap where nothing else reaches the surface.

| Unwired verb(s) | Generic route that does work | Genuinely blocked? |
| --- | --- | --- |
| `product.setProperties`, `product.setAddress`, `fb.setProperties`, `resource.setProperties`, `projectInfo.set` | `node.getProperties` / `projectInfo.get` → `dialog.read` → `dialog.setText` / `selectItem` / `setCheck` → `dialog.click --button OK` | No — the generic dialog verbs supersede all five |
| `variable.insert`, `scene.insert`, `enum.createType`, `enum.listTypes` | `node.rightClick` → `menu.dumpContext` → `menu.invokeContext`, then the dialog verbs | No, but unproven — the flyout route has not been driven end to end for these |
| `program.insertElement`, `program.addCondition`, `program.addAction`, `program.addCase` | `programming.enter` (needs the foreground) then the section flyout via `menu.invokeContext` | **Partly** — the entry point is a `key` verb, so programming-mode authoring needs a foreground session. Bears on dimensions 2, 5, 8 |
| `link.productToFb`, `link.fbToProduct` | `link.startFromHere` + `link.toHere` (both wired, `contextMenu`) | No |
| `controller.retrieve` | — | n/a — the live controller menu is out of comparison scope |
