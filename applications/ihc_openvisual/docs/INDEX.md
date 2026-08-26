---
scope: Navigation hub for the IHC OpenVisual documentation — the product specification, the epic/user-story behavioural spec, vendor-alignment/verification docs, and icon references
exclusions: These docs specify WHAT the app must do, not HOW (implementation) or WHEN (plans); no spec content lives in this hub
research_date: 2026-08-11
latest_update: 2026-08-26 — error-list.md retired; error_catalog.md added (problem catalogue authoring requirements)
---

# IHC OpenVisual Documentation Index

> Specification and reference docs for the IHC OpenVisual desktop app: the product spec, the
> E1–E17 user-story collection, vendor-comparison/verification ledgers, and the icon language.

**Total Documents:** 7 content files here (plus this index), and 17 story files under `stories/`

## Collection Structure

| Location | Contents | Entry point |
| --- | --- | --- |
| `docs/` (this directory) | Product spec, alignment/verification docs, icon references | this file |
| `docs/stories/` | Behavioural spec: epics E1–E17 with user stories (US-NNN) and acceptance criteria | [stories/INDEX.md](stories/INDEX.md) |

## Document Index

### Product specification

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [product.md](product.md) | Product spec (WHAT): vision, features F1–F11, quality attributes, data requirements, glossary, test oracles | F1–F11, vendor differences, quality attributes, glossary, test data | 572 |
| [todo.md](todo.md) | (Stub) One-line note of known multi-instance issues (concurrent `.vis` editing, settings stores, shared catalog dir) | multi-instance, lost saves, settings races | 1 |

### Vendor alignment & verification

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [checklist.md](checklist.md) | Scope contract for comparing OpenVisual with vendor IHC Visual: dimensions, oracle choice, evidence rules, verdicts | comparison dimensions, reference oracle, evidence, verdict, maintenance rule | 172 |
| [alignment-coverage.md](alignment-coverage.md) | Coverage ledger recording what has been measured per checklist dimension — answers "what has nobody looked at?" | coverage cells, dialogs, set-valued dimensions, state axes, driver gaps | 162 |

### Problem catalogue

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [error_catalog.md](error_catalog.md) | Authoring requirements for the problem catalogue: the data, formats and wiring needed to add a fatal error, error, warning, information item or host operation outcome. Covers the SDK's `.vis` catalogue and this app's reserved `app.openvisual.*` family; the declarations are the truth, and per-row evidence lives in [`ihcclient/docs/problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md) | entry fields, code families, categories, dispositions, Danish templates, argument slots, thresholds, list tiers, gates, oracles | 505 |

### Icon language

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [icons_design.md](icons_design.md) | Flat-line SVG icon design guidelines: 24-unit grid, `currentColor` theming, legibility at 16 px | design principles, canvas rules, stroke, visual grammar, authoring workflow | 211 |
| [icon_codes.md](icon_codes.md) | Mapping of `.vis`/`.ifb` elements and vendor `_0xNN` codes to `Assets/*.svg`, plus Unicode text stand-ins | icon codes, element → SVG, products, links, Unicode stand-ins | 340 |

## Section Outlines

#### [product.md](product.md) — IHC OpenVisual

1. Vision and Purpose / Key Features / Architecture Overview / Key Differentiators
2. Differences from the Original IHC Visual
3. What This Product Is Not / Success Metrics / Product Context
4. User Classes and Characteristics / Operating Environment / Constraints and Dependencies
5. System Features (F1–F11) / External Interface Requirements / Quality Attributes
6. Data Requirements / Glossary
7. Test Oracles / Test Data / Source Code / Companion Specifications / Standards and Specifications

#### [error_catalog.md](error_catalog.md) — problem catalogue authoring requirements

1. Which catalogue owns the item — SDK findings and refusals vs. this app's reserved `app.openvisual.*` family
2. The item kinds and the axes each sets — fatal error (cause and operation head), edit precondition, error, warning, information, host outcome; what "fatal" does and does not mean; the eight categories
3. Required data — every field of a catalogue entry, with formats and allowed values
4. The identifier / 5. The Danish message template / 6. Declared argument slots / 7. Thresholds / 8. The predicate
9. Wiring — the code edits an item needs, per kind
10. Gates a new item must pass, the exact population pins it moves, and the two committed oracle sets
11. The four list tiers — why Fatal needs a declared `RefusedOperations` rather than a fourth severity, and Information a fourth `CatalogDisposition` member
12. Changing, retiring and ruling out

> No row inventory lives here. The compiled declarations are the truth —
> `ihcclient/src/vis/validation/ProblemCatalogEntries.*.cs` and `Services/HostProblemCatalog.cs` — and the per-row
> evidence and rationale are in [`ihcclient/docs/problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md).

#### [icon_codes.md](icon_codes.md) — icon selection reference

1. Quick lookup — icon code → `Assets/*.svg`
2. Structure & sections / Programs & logic / Resources (function-block variables) / Links
3. Products — code is **not** a reliable discriminator
4. Elements with no `icon` attribute (structural / config)
5. Text-only rendering — Unicode stand-ins

## Related Collections

- [stories/INDEX.md](stories/INDEX.md) — sub-hub for the E1–E17 user-story files (start there for any feature behaviour)
- [../../../ARCHITECTURE.md](../../../ARCHITECTURE.md) — repo-wide layers and invariants (the HOW these docs deliberately omit)
