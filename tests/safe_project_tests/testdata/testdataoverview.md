# Test Data Overview

The `.vis` files under `projects/` are the byte-oracles for `safe_project_tests`. Every file directly in
`projects/` and in `projects/LiveAuthored/` is **authentic**: it was authored and saved by the real IHC Visual
application (03.04.72.03) against a real controller catalog, so its exact bytes — id allocation
order, enum dedup holes, header stamps, ISO-8859-1 encoding, attribute ordering and elision — are
vendor ground truth. Tests assert byte-identity against these files, both for pure load/save
round-trips (`ProjectByteFidelityTests`) and for from-scratch reconstruction through the public
builder API (`AuthoringByteFidelityTests`, install-dir gated). Files under `projects/Synthetic/` may be
hand-crafted, not necessarily authentic (see bottom).

## Authentic oracles (`projects/`)

### Project0-Tomt.vis (5.6 KB, 130 lines)

The empty baseline: a freshly created project with the 10 default rooms, seed enums/modules and no
content. `last_unique_id _0x50` (80). Designed to pin down `ProjectAppService.CreateNew` — the
template seed, header stamping (`id1`/`id2` decode via PackedStamp) and default-save re-stamp must
reproduce it byte-for-byte (`CreateNewTests`, `BL_E0`). Also the lightweight fixture for group
edits and escaping tests.

### Project1-SimpelWired.vis (88 KB, 985 lines)

A small but fully-wired project: 4 dataline products (FUGA switch, lamp outlet, socket outlet, PIR)
across Stue/Entré, 2 catalog function blocks (Kip 1.1.01, PIR-styring 1.4.02), 5 link pairs, and
the widest attribute variety among the small oracles (notes, positions, cable data, documentation
tags, power groups, scenes, backup flags). Designed as the workhorse fixture: byte round-trip,
edit sessions, attribute editing, link navigation, copy-subtree (FB with internal + cross-room
links), delete cascade, allocator monotonicity, program/enum authoring and validator tests. Its
builder reconstruction (`BL_E2`) encodes the key discovery that ids are allocated in **user-action
order** (the vendor wired the Kip links before starting the next room).

### project2-CustomBlock.vis (37 KB, 653 lines)

No products — instead a catalog **AutoProof** function block (user-saved `.ifb`, name-keyed lookup,
no `master_type`) plus a hand-authored **"Custom blok"** exercising the full resource palette: the
canonical 18 value types across settings/internalsettings/inputs/outputs, a program with
`event_power` and nested `program_sub`/conditions/actions, and a user enum. Uses the legacy
`ModulesFirst` seed layout (modules 65–67 before enums). Designed for custom/locked function-block
behavior: FB resource management, unlock semantics, insert legality, and the incremental `BL_E3`
builder reconstruction including reorder/delete replay (add-then-delete counter burns,
`MoveSubtree`).

### project3-KompleksWired.vis (237 KB, 2,499 lines)

The broadest and deepest oracle: 1,332 allocated ids (`last_unique_id _0x56c` = 1388), 11 rooms
(one renamed with quotes/diacritics, one "Lokalitet" added last). Contains 13 dataline **and
airlink** products (including the ambiguous `_0x4306` Dimmer Universal catalog pick), exotic
**s0** (kWh) and **RS-485** LED-dimmer devices, two "med logning" sensors that trigger enum
DEDUP holes, 6 catalog FBs (two carrying hoisted enums), a 0-value user global enum, three empty
"Tom blok" FBs (one with 9 internal variables), and 3 follow-links. Designed as the stress oracle:
byte fidelity for exotic device families (BL-E4/M4 — NormalizeTokens, InsertStamps, NormalizeEnums
including the vendor's `readwrite` accessibility typo, RS-485 error icons), plus element
resolution, move-subtree, insert-legality and FB shape-validation fixtures.

### project3-KompleksWired-mutated.vis (239 KB, 2,544 lines) — derived mutation oracle

Authentic IHC Visual output **derived from `project3-KompleksWired.vis`** by applying three recorded
editing actions in one session (single save); the actions are fully specified below (session notes in
`tmp/mutoracle/FINDINGS.md`). Its purpose is the **authoring-pipeline byte-fidelity**
gate: the SDK loads the *original*, replays these actions through the public builder API (clock pinned
to this file's stamp `id2="_0x40c1836"` / `modified 2026-07-04 12:24`), and asserts byte-identity.
`id1` is unchanged (`_0x1d0e2923`); `last_unique_id` rises `_0x56c → _0x59e`. Note a load-time
**Action 0**: a bare save of the original already re-hoists its two catalog (`[read only]`, `typeid`)
enums `Persienne tilstand`/`Logning` to the bottom of the enum block with fresh ids `_0x56d..0x579` and
rewrites their 4 `resource_enum` refs — a one-time normalization the replay must reproduce (the SDK's
passive load preserves the original's low enum ids). The three mutations (in allocation order):

- **A — new-type product insert.** Added catalog **24773 "SMS Modem"** to group **`Garage`** (`_0x2832`),
  introducing element types `product_rs485_sms_modem` / `sms_modem_settings` / `sms_modem_pincode` /
  `sms_modem_phonenumber` — all absent from project3's DTD, so this oracle uniquely exercises **DTD block
  generation** (not passthrough). Allocated ids `_0x57a..0x59d` (36), auto-creating a pincode block
  (default `1234`) and 30 phone-number slots across 4 `sms_modem_settings`.
- **B — new enum.** Authored enum `MutOracleEnum`, appended to `enum_definitions` as
  `<enum_definition id="_0x59e47" name="MutOracleEnum"/>` — **empty** (the driver creates the type with no
  values; value-id/index-stamping left for a future oracle). Standalone, not referenced.
- **C — linked-subtree delete.** Deleted `LK FUGA Tryk 2 tast` (`_0x5153`), which owned all three
  `link_from_resource` ends; the vendor **removed both paired-link rows** (the three `link_to_resource`
  in FB inputs `_0x47211/_0x47311/_0x47411`, which survive as empty self-closing) — **no dangling end** —
  and regenerated the DTD (dropped the now-unused `link_from_resource`/`link_to_resource` decls, moved
  `dataline_input` later). No ids renumbered or reused (monotone), so `last_unique_id` did not decrease.

Designed for: authoring byte-fidelity of a *mutation on a large existing project* — `InsertTransform` +
new-type DTD generation (A), enum def-id allocation/placement after the catalog-enum re-hoist (B), delete
cascade + monotone-id / no-dangling-link handling (C) — and as a diff fixture where each changed byte
region maps to exactly one recorded action (Action 0 / A / B / C / metadata). A second independent run of
A/B/C is byte-identical to this file modulo `id2`+`modified` (deterministic authoring).

### project3-KompleksWired-copied.vis (239 KB, 2,522 lines) — derived copy/paste oracle

Authentic IHC Visual output **derived from `project3-KompleksWired.vis`** by three recorded clipboard
copy→paste actions in one session (single save); the actions are fully specified below (session notes in
`tmp/cporacle/FINDINGS.md`). Its purpose is the **copy/paste byte-fidelity** gate for
`ProjectEditor.CopySubtree`: the SDK loads the *original*, replays Action 0 + these copy actions through
`CopySubtree`, saves with the clock pinned to this file's stamp `id2="_0x40f391d"` /
`modified 2026-07-04 15:57`, and asserts byte-identity. `id1` is unchanged (`_0x1d0e2923`);
`last_unique_id` rises `_0x56c → _0x590`. The same load-time **Action 0** as the `-mutated` oracle applies
first: a bare save re-hoists the two catalog (`[read only]`, `typeid`) enums `Persienne tilstand`/`Logning`
to the bottom of the enum block with fresh ids `_0x56d..0x579` and rewrites their 4 `resource_enum` refs —
reproduce it before any copy allocates (the SDK's passive load preserves the original's low enum ids).
Every pasted subtree keeps the source name **verbatim** (no "Kopi af…" rename) and preserves the catalog
`product_identifier`. The three copies (in allocation order):

- **A — from-side external-link drop.** Copied `LK FUGA Tryk 2 tast` (`_0x5153`, TV1 path `0/0`, owner of
  all three `link_from_resource` follow-link ends) → pasted into empty group **`Lokalitet`** (`_0x56c32`).
  Both `dataline_input`s are copied but their 3 `link_from_resource` children are **dropped** (inputs
  emitted self-closing) — the vendor's from-side policy equals SDK `LinkCopyPolicy.DropExternal`. Allocated
  3 ids: `_0x57a53` (product) + `_0x57b5a`/`_0x57c5a` (the two inputs); the dropped links consume no ids.
- **B — internal `scene_resource` remap.** Copied `Lampeudtag` (`_0x5453`, TV1 path `0/1`, carries
  `<scenes scene_resource="_0x555b"/>` pointing at its own `dataline_output _0x555b`) → pasted into empty
  group **`Udendørs`** (`_0x2a32`). Allocated 3 ids: `_0x57d53` (product) + `_0x57e5b` (output) + `_0x57f49`
  (scenes); the copy's `scene_resource` is **remapped to the new internal output** `_0x57e5b` (not left
  pointing at the source), proving the internal-IDREF remap through the copy's old→new map.
- **C — shared-enum reuse + non-count id allocation.** Copied `Temperatur sensor med logning` (`_0x5e53`,
  TV1 path `2/0`, two `resource_enum` rows referencing the shared `Logning` enum) → pasted into group
  **`Lokalitet`** (`_0x56c32`, appended after A). The pasted `resource_enum` rows point at the
  **re-hoisted shared** `Logning` def `_0x57347` / value `_0x57448` (`typedef`/`inivalue`) with **no new
  enum id allocated** — the enum is reused, not duplicated. Id allocation is **not** one-per-serialized
  element: the vendor lays down the product `_0x58053`, **burns 7 ids** `_0x581..0x587`, then the 9 present
  children `_0x588..0x590` — a deterministic gap the SDK replay must reproduce. The burn is the referenced
  `Logning` enum's **footprint** (def + 6 values = 7): a catalog product's `.def` body carries that enum as
  its first child, so a paste re-materializes it and the insert pipeline allocates-then-discards its def+value
  ids (they dedup against the shared enum) — verified against `product2125.def` (which has a 1-value enum
  stub, so a catalog *insert* burns 2, while a *copy* clones the project's fully-materialized 6-value enum,
  hence 7). Copies A and B reference no enum and burn 0. `last_unique_id` ends `_0x590`.

Designed for: copy/paste byte-fidelity of `CopySubtree` on a large existing project — from-side
`DropExternal` (A), `scene_resource` internal-IDREF remap (B), shared-enum reuse + non-count id allocation
with a burned-slot gap (C) — and as a diff fixture where each changed byte region maps to exactly one
action (Action 0 / A / B / C / metadata). Every hunk of `project3-KompleksWired.vis` → this file is
explained, and a second independent A/B/C run is byte-identical modulo `id2`+`modified` (deterministic).
**Not covered here:** the to-side `DropExternal` + deep internal-remap branch (a copied **function block**,
e.g. AND `_0x47028`) — IHC Visual `03.04.72.03` would not persist a pasted function block through the
driver (paste silently no-ops in memory), so that branch has no vendor oracle in this file; see
`tmp/cporacle/FINDINGS.md` and `tmp/newgaps.md` Gap 2.

### project3-KompleksWired-enumvalues.vis (237 KB, 2,505 lines) — derived enum-with-values oracle

Authentic IHC Visual output **derived from `project3-KompleksWired.vis`** by one recorded enum-with-values
authoring action in a single session (single save); session notes in `tmp/cporacle3/FINDINGS.md`. Because the
`ihcvisual` driver has **no value-add verb** (only `enum.createType`/`listTypes`/`listValues`, all valueless),
the enum was authored directly in the live **"Enumerator typer og værdier"** dialog (command 24588) via Win32
messaging (`Ny`-type button 360 → name Edit 377 → OK; then per value `Ny`-value button 361 → Edit 377 → OK; then
dialog OK 1) — so **the vendor app itself allocated every id and wrote every byte** (verbatim vendor output, never
hand-edited). Its purpose is the **enum-with-values authoring byte-fidelity** gate *in a mutation context*: the SDK
loads the *original*, replays Action 0 + `ProjectEditor.AddEnumDefinition("ValueOracleEnum","Alpha","Beta","Gamma")`,
saves with the clock pinned to this file's stamp `id2="_0x4120a2f"` / `modified 2026-07-04 18:10`, and asserts
byte-identity. `id1` is unchanged (`_0x1d0e2923`); `last_unique_id` rises `_0x56c → _0x57d`. The same load-time
**Action 0** as the `-mutated`/`-copied` oracles applies first: a bare save re-hoists the two catalog (`[read only]`,
`typeid`) enums `Persienne tilstand`/`Logning` to the bottom of the enum block with fresh ids `_0x56d..0x579` and
rewrites their 4 `resource_enum` refs — reproduce it before the enum allocates (the SDK's passive load preserves the
original's low enum ids). The single authoring action:

- **E — new enum with values.** Authored enum **`ValueOracleEnum`** with three values `Alpha`, `Beta`, `Gamma`,
  appended at the **very end** of `enum_definitions` (after the re-hoisted catalog enums), **standalone** (not
  referenced by any resource):

  ```xml
  <enum_definition id="_0x57a47" name="ValueOracleEnum">
     <enum_value id="_0x57b48" name="Alpha"/>
     <enum_value id="_0x57c48" name="Beta" index="1"/>
     <enum_value id="_0x57d48" name="Gamma" index="2"/>
  </enum_definition>
  ```

  **Vendor's answer to the open question** (does the vendor stamp value-ids/`index` differently *after* Action 0 than
  from scratch?): **no — it is byte-identical to the from-scratch reference** `NyTypeForThisProject` (project2,
  BL-E3). Ids are **contiguous, def-first, in value order** — def `_0x57a47`, then `_0x57b48`/`_0x57c48`/`_0x57d48`
  — with **no id burn** (a bare-empty-enum probe `ProbeEmpty` in this same session took exactly the def id `_0x57a47`,
  and the three values simply continue from `_0x57b`). **No `typeid`** on the def or any value; `index` is **0-based
  with index-0 elided** (first value `Alpha` has no `index`, then `index="1"`, `index="2"`). Because
  `enum_definition`/`enum_value` are already in project3's inline DTD, this action introduces **no DTD change**.
  `last_unique_id` ends `_0x57d`.

Designed for: enum-with-values authoring byte-fidelity — value-id allocation + `index` stamping + block placement
**in a mutation context** (after the Action-0 re-hoist) — closing the value-id/`index` hole the `-mutated` oracle's
empty Action B left open. Also a diff fixture where every changed byte region maps to exactly one cause (Action 0 /
Action E / metadata): the SDK loads + validates it clean (`IsValid=True`, 0 errors/warnings), DTD-conforms against
its own inline DTD, and **passively round-trips it byte-identically** — it is in the `ProjectByteFidelityTests` and
`DtdConformanceTests` batteries alongside `-copied`/`-mutated`. The *replay* test (Action 0 +
`AddEnumDefinition` → byte-identity) is **unconditional (catalog-free)** — both verbs use only the allocator +
inline DTD, so it runs on a clean checkout with no install dir; delivered by `EnumValuesReplayByteFidelityTests`.
Since the vendor block is contiguous with no burn, the SDK's single-shot `AddEnumDefinition` reproduces it directly.

## Authentic oracles (`projects/LiveAuthored/`)

Minimal single-purpose projects captured live in IHC Visual during experiment B3, each isolating
one enum-deduplication behavior for the M1 byte-fidelity milestone.

### projects/LiveAuthored/step02-pir2.vis (121 KB, 1,204 lines)

Two inserts of the same PIR function block (1.4.02) into Stue and Entré. The second block's three
enum definitions are allocated in document order but **discarded** because they duplicate the
first block's enums by name+content, leaving the permanent 9-id hole 407–415 and rewired
references. Oracle for repeated-FB-insert enum dedup (`EnumDedup_RepeatedPirInsert…`).

### projects/LiveAuthored/step06-luxtemp.vis (12 KB, 238 lines)

Three "med logning" sensor products (`_0x2125` ×2, `_0x2139` ×1). Each embeds a "Logning" enum
that dedups against the seed global `_0x4747`, leaving one 2-id hole per insert (82/83, 94/95,
106/107). Oracle for product-embedded enum dedup (`EnumDedup_LogningProducts…`).

## Synthetic fixtures (`projects/Synthetic/`) — not authentic

Hand-crafted files for `OpenWorldTests`; they exercise behavior no vendor file can, and are *not*
IHC Visual output.

- **OpenWorldCustomComponent.vis** — declares a registry-unknown element (`custom_widget`) in its
  own inline DTD; must load/edit/save byte-identically from the file's DTD alone.
- **EncodingMismatchSwedish.vis** — UTF-8 bytes under an ISO-8859-1 declaration; must round-trip
  verbatim (never "repaired"), with the logical value read as mojibake.
- **OpenWorldUndeclaredAttr.vis** — carries an attribute declared by neither registry nor inline
  DTD; serialization must throw rather than silently emit it.

## Synthetic Product oracles (`products/synthetic/`) — not authentic

Thirteen hand-authored synthetic `.def` product files (not IHC Visual output) — the committed oracles for the
code-authoring builder surface (`ProductDefinitionBuilder`) **and** for the catalog file format itself. Each
is consumed by committed tests three ways: the W1 reader→writer byte gate (`CatalogFileWriterTests`), the
grammar round-trip (`CatalogDtdParserTests` strict-parses every header and re-emits it equivalently), and a
code-authored **byte** test (`ProductBuilderOracleTests`: the product is authored entirely from code — grammar
+ body + ids — and `CatalogFileWriter` must reproduce the file under the fidelity relation). The five files
whose header is exactly a family grammar preset's rendering byte-pin that preset (`9f02` dataline, `9f04`
airlink, `9f05` RS485 LED dimmer, `9f06` RS485 SMS modem, `9f07` S0); the grammar-envelope files (`9f09`–
`9f13`) pin the corpus irregularity classes. Pick the file whose feature you need to cover.

Format is byte-identical to a real vendor `.def`: UTF-8 **BOM** followed by a *lying*
`<?xml … encoding="ISO-8859-1"?>` declaration over UTF-8 body bytes (`CatalogReader` trusts the BOM), **CRLF**
throughout including a trailing CRLF, and a full internal DTD so omitted attributes (e.g. `locked="yes"`)
materialize exactly as for a real file and `InlineDtd.Capture` records the per-type blocks. One deliberate
exception: `synthetic_9f13_utf8nobom.def` is UTF-8 **without** BOM and truthfully declares `encoding="UTF-8"`
— no vendor file has that shape, but it is a supported `CatalogTextEncoding` a user file can arrive in, so it
pins the read/write/`From` path for the third encoding. Files are **flat**
(no subfolders) and named `synthetic_9fNN_<role>.def` — the `synthetic_` prefix, the `_0x9fNN` index (not a
real `productNNNN.def` code) and the plain functional role deliberately avoid any real product-family or brand
term, so a filename can never be mistaken for a catalog product (the family each actually tests is in the
table). The flat layout means discovery reports an **empty** library category for every file — `CategoryPath`
is a trivial setter tested directly, and the `NN#` menu prefix `MenuPrefix` strips lives in each product's
`name` attribute, not a folder, so it stays covered regardless.

| File | Family root | Builder surface exercised |
|------|-------------|---------------------------|
| `synthetic_9f01_input.def` | `product_dataline` (0x53) | `Dataline()`, `Locked()`/`EnduserReport()` via DTD defaults, `Note`, `AddInput` ×2 (`Address`/`CableColour`/`Note`/`Attribute("inivalue")`), `NN#` prefix strip, æøå encoding |
| `synthetic_9f02_output.def` | `product_dataline` | `Name()` (no prefix), `Locked(false)`, install attrs (`Position`/`DocumentationTag`/`PowerGroup`/`CableType`/`CableNumber`/`Note`), `AddInput`, `AddOutput` (`Backup(false)`/`Address`/`CableColour`/`Attribute("type","led")`), `AddScenes()` bound to the output |
| `synthetic_9f03_resources.def` | `product_dataline` | `RawChild(enum_definition>enum_value)`, product-root `Attribute("icon")`, `AddResource` for `resource_temperature`/`resource_input`/`resource_enum` (`accessibility`/`typedef`/`inivalue`) and `settings` (nested `RawChild(dataline_input)`); body uses a type its own DTD omits (forgiving parse) |
| `synthetic_9f04_wireless.def` | `product_airlink` (0x54) | `Create("product_airlink", …)`, `Attribute("device_type")` (a `#REQUIRED` attr with no setter), `AddResource("airlink_relay", Attribute("address_channel"))`, `AddScenes` bound to the relay |
| `synthetic_9f05_dimmer.def` | `product_rs485_led_dimmer` (0x58) | `Create(…)`, `Attribute("serialnumber")`+icon, `AddResource("resource_flag")`, deep `AddResource("rs485_led_dimmer_channel", Icon()+Attribute)` carrying a nested `RawChild` subtree (increase/decrease/dimming/light_indication/scenes/4×error_state/dimmer_settings > 6 leaves) |
| `synthetic_9f06_modem.def` | `product_rs485_sms_modem` (0x56) | `Create(…)`+icon, `AddResource("sms_modem_settings", Icon())` with nested `RawChild(sms_modem_pincode)` and repeated `sms_modem_phonenumber` (`Attribute("address")`) |
| `synthetic_9f07_meter.def` | `s0_device` (0x57) | `Create("s0_device", …)`, `Attribute("ticks")`+icon, `AddResource` for `W`/`kWh`/`resource_date`, `Attribute("accessibility","read-write")` on kWh, `resource_date` `#REQUIRED` year/month/day + s0 `access` enum default |
| `synthetic_9f08_openworld.def` | `product_dataline` | **Open-world**: `RawChild` + a DTD declaration for an element type in **neither** the type-code table nor the schema registry |
| `synthetic_9f09_logging.def` | `product_dataline` | **Grammar envelope — orphan ATTLIST** ("med logning" class, vendor `product2125` shape): tab-indented orphan `<!ATTLIST resource_enum …>` (registry tag) *and* an orphan `<!ATTLIST resource_sample_log …>` on an invented **non-registry** tag the body uses — its `inivalue "500.00"`/`interval "300"` defaults drive insert-time materialization, and hoisting it into a project exercises the synthesized-`<!ELEMENT tag ANY>` project-block rendering; embedded `enum_definition`/`enum_value` + `resource_enum` `typedef`/`inivalue` IDREFs |
| `synthetic_9f10_superset.def` | `product_dataline` | **Grammar envelope — superset DTD**: declares `dataline_output`/`resource_temperature` the body never uses (81-file vendor class), body uses `resource_input` its own DTD never declares (7-file class), and `pulse_width (24 \| 48 \| none) "24"` pins **digit-leading NMTOKEN** enumeration tokens (legal per XML VC: Enumeration; no vendor instance) |
| `synthetic_9f11_quirks.def` | `product_dataline` | **Grammar envelope — formatting quirks**: DOCTYPE space before `[` (12-file vendor class), tab-indented declarations with irregular continuation depths and a trailing-space line, and `&apos;` escaping apostrophes in body values (the `1.2.05.ifb` class) — the comparer's D3 `&apos;` ≡ `'` forgiveness is what lets this file round-trip |
| `synthetic_9f12_caseskew.def` | `product_dataline` | **Grammar envelope — case skew** (vendor `product2136`/`2139` shape): DOCTYPE space before `[`, tab-indented ELEMENT-only `<!ELEMENT resource_Skew ANY>` beside an orphan `<!ATTLIST resource_skew …>` the body uses — the corpus's only case-insensitive tag-collision class; ordinal tag comparison is mandatory or the pair is unreproducible |
| `synthetic_9f13_utf8nobom.def` | `product_dataline` | **Grammar envelope — UTF-8 without BOM**, truthful `encoding="UTF-8"` declaration (a non-default `DeclaredEncoding` datum), non-ASCII æøå values — pins the third `CatalogTextEncoding` and `From` fidelity |

Together they cover all five family roots, both static factories (`Dataline(…)` and `Create(rootTag, …)`),
and the install-attr / resource / escape-hatch (`Attribute`, `RawChild`, plus an `ExtendGrammar` declaration
for open-world types) / discovery-wrapper surfaces, each on a UTF-8-BOM + ISO-8859-1 + Danish-letter + CRLF
file (9f13 excepted, deliberately). They are **one deliberately minimal
example per surface cluster**, not a representative sample of the corpus's breadth: reproducing that breadth
and especially its malformed-but-real quirks (within-file duplicate element ids, the `loced="no"` misspelling
of `locked`, `product_identifier`s repeated across files) is intentionally **out of scope** — a builder authors
clean products, so these oracles stay clean; corpus-variety and format-robustness coverage belongs to tests
over the real `.def` corpus, not to these builder oracles.

## Synthetic FunctionBlock oracles (`functionblocks/synthetic/`) — not authentic

Eight synthetic `.ifb` FunctionBlock files (not IHC Visual output) — the committed oracles for
`FunctionBlockDefinitionBuilder`/`FbProgramBuilder` and the `.ifb` file format. Each is consumed by committed
tests three ways: the W1 reader→writer byte gate (`CatalogFileWriterTests`), the grammar round-trip
(`CatalogDtdParserTests`), and a code-authored **byte** test (`FunctionBlockBuilderOracleTests`: the block is
authored entirely from code — grammar + body + ids — and `CatalogFileWriter` must reproduce the file).
`fb08` was generated by that exact authoring from a bare `Create(…)`, so it byte-pins the `FunctionBlock`
grammar preset. Format matches the majority vendor `.ifb`: ISO-8859-1, CRLF, no trailing newline, lean bodies
(attributes equal to their DTD default are omitted), and vendor-style suffixed ids
(`id = (counter << 8) | typeCode`; the byte tests re-stamp the builder's placeholder allocation with each
file's exact tokens, the same mechanism the generated catalog uses). Files are named
`synthetic_fbNN_<role>.ifb`, mirroring the sibling `products/synthetic/synthetic_9fNN_<role>.def`
convention — the `synthetic_` prefix and neutral functional role mark each as an invented oracle, never an
authentic install file (`fbNN` is a throwaway index like the placeholder element ids, not a real block code).

Each file exercises one region of the builder — pick the one whose feature you need to cover:

| File | Builder features exercised |
|------|----------------------------|
| `synthetic_fb01_toggle.ifb` | full master identity (`VendorMaster`/`Locked`/`MasterProgrammer`/`MasterDate`/multiline+Latin-1 `Note`); 2 inputs + 2 outputs; `resource_timer` setting + internal timer; deep program graph (`program_sub`, `and` conditions, true/false branches, leaf action in root); `link1`+`link2` wiring; multiple `program_simple` |
| `synthetic_fb02_scene.ifb` | `resource_scene` outputs + scene-recall actions; `Backup` on a setting; `.Attribute` escape hatch (`hide_dialog`/`note-2`); an empty `internalsettings` container; `VendorMaster(false)` (flag omitted); leaf-only programs |
| `synthetic_fb03_mode.ifb` | top-level `enum_definition`/`enum_value` (one indexed, one index-0 elided); `resource_enum` setting (`Enum(typedef,inivalue)`+`Backup`); embedded enum operand in a condition (`AddEnumOperand`→`link2`); nested `program_sub` in a true branch; self-closing (empty) false branch |
| `synthetic_fb04_holiday.ifb` | `resource_date` settings (`DateYmd`+`Backup`) + internal date; `resource_flag` with non-default `Inivalue`; power-up trigger (`event_power`/`AddPowerEvent`); an `or` conditions list; multiple programs |
| `synthetic_fb05_empty.ifb` | `AsEmptyTemplate` — five containers in fixed order + one empty `program_simple`, conventional container icons, block icon `_0xf`, no master identity |
| `synthetic_fb06_sensor.ifb` | `DisplayName` override (block name ≠ composed `{type}.{ver}. {name}`); `AddInput` with a value-type tag; heterogeneous registry value families (with/without icon); `Inivalue` on float/weekday-enum/integer; `link2` to a value operand |
| `synthetic_fb07_grammar.ifb` | **Grammar envelope — non-standard declaration order + per-file block variants**: `outputs`/`resource_output` declared before `inputs`/`resource_input`, and both `resource_input` and `resource_flag` declare `inivalue (on \| off) "on"` (≠ the registry/preset default `"off"`) — the body omits `inivalue`, so its *effective* value comes from this file's own grammar, proving per-file grammar beats any family preset |
| `synthetic_fb08_full.ifb` | **Preset pin — full program surface**: generated by `CatalogFileWriter` from a bare `Create(…)` (no grammar work), so its header is exactly the `FunctionBlock` grammar preset's rendering and its ids the builder's natural allocation; body exercises `AddPowerEvent`, `AddSubProgram` (conditions + true/false branches), `AddCase` (`program_case` + per-value `case_action` with embedded `resource_enum` operands + default branch), top-level `AddEnumDefinition`/`AddValue`, and the default-tag `AddInput`/`AddOutput` pins — the byte test re-authors it identically, pinning the preset's CONTENT |

Only the structural branch names (`Kommandoer ved betingelser sande`/`falske`) are reproduced verbatim —
the committed `FbProgramBuilder` doc-comments pin them as the fixed grammar the builder emits; everything
block-specific (master identity, resource names, notes, program logic) is original. `CategoryPath` is
deliberately out of scope (catalog discovery derives it from the folder; it is not stored in an `.ifb`).
The `RawResource`/`InlineDtdBlock` open-world escape hatches are also not covered here — a genuinely
non-registry element needs the project-level path, covered by `projects/Synthetic/OpenWorldCustomComponent.vis`.

## Notes

- Files are copied to the test output via the csproj `testdata\**\` globs (`*.vis` plus `*.def` and
  `*.ifb` for the synthetic catalog oracles); access them through the `TestData` helper (relative
  paths like `projects/LiveAuthored/step02-pir2.vis` or `functionblocks/synthetic/synthetic_fb01_toggle.ifb`).
- All oracles share `programmer="Morten Christensen"`, format `version 4.0`, ISO-8859-1 encoding.
- Do not re-save these files with any editor — a single normalized byte invalidates every
  byte-identity assertion. Treat them as read-only ground truth.
