# Test Data Overview

The `.vis` files in this directory are the byte-oracles for `safe_project_tests`. Every file in the
root and in `LiveAuthored/` is **authentic**: it was authored and saved by the real IHC Visual
application (03.04.72.03) against a real controller catalog, so its exact bytes — id allocation
order, enum dedup holes, header stamps, ISO-8859-1 encoding, attribute ordering and elision — are
vendor ground truth. Tests assert byte-identity against these files, both for pure load/save
round-trips (`ProjectByteFidelityTests`) and for from-scratch reconstruction through the public
builder API (`AuthoringByteFidelityTests`, install-dir gated). Files under `Synthetic/` are
hand-crafted, not authentic (see bottom).

## Authentic oracles (root)

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
editing actions in one session (single save). Full provenance and the exact replay spec live in
`project3-KompleksWired-mutated.actions.md`. Its purpose is the **authoring-pipeline byte-fidelity**
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

## Authentic oracles (`LiveAuthored/`)

Minimal single-purpose projects captured live in IHC Visual during experiment B3, each isolating
one enum-deduplication behavior for the M1 byte-fidelity milestone.

### LiveAuthored/step02-pir2.vis (121 KB, 1,204 lines)
Two inserts of the same PIR function block (1.4.02) into Stue and Entré. The second block's three
enum definitions are allocated in document order but **discarded** because they duplicate the
first block's enums by name+content, leaving the permanent 9-id hole 407–415 and rewired
references. Oracle for repeated-FB-insert enum dedup (`EnumDedup_RepeatedPirInsert…`).

### LiveAuthored/step06-luxtemp.vis (12 KB, 238 lines)
Three "med logning" sensor products (`_0x2125` ×2, `_0x2139` ×1). Each embeds a "Logning" enum
that dedups against the seed global `_0x4747`, leaving one 2-id hole per insert (82/83, 94/95,
106/107). Oracle for product-embedded enum dedup (`EnumDedup_LogningProducts…`).

## Synthetic fixtures (`Synthetic/`) — not authentic

Hand-crafted files for `OpenWorldTests`; they exercise behavior no vendor file can, and are *not*
IHC Visual output.

- **OpenWorldCustomComponent.vis** — declares a registry-unknown element (`custom_widget`) in its
  own inline DTD; must load/edit/save byte-identically from the file's DTD alone.
- **EncodingMismatchSwedish.vis** — UTF-8 bytes under an ISO-8859-1 declaration; must round-trip
  verbatim (never "repaired"), with the logical value read as mojibake.
- **OpenWorldUndeclaredAttr.vis** — carries an attribute declared by neither registry nor inline
  DTD; serialization must throw rather than silently emit it.

## Notes

- Files are copied to the test output via the csproj `testdata\**\*.vis` glob; access them through
  the `TestData` helper (relative paths like `LiveAuthored/step02-pir2.vis`).
- All oracles share `programmer="Morten Christensen"`, format `version 4.0`, ISO-8859-1 encoding.
- Do not re-save these files with any editor — a single normalized byte invalidates every
  byte-identity assertion. Treat them as read-only ground truth.
