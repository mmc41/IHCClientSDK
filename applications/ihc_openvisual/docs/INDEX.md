---
scope: Navigation hub for the IHC OpenVisual documentation — the product specification, the epic/user-story behavioural spec, vendor-alignment/verification docs, and icon references
exclusions: These docs specify WHAT the app must do, not HOW (implementation) or WHEN (plans); no spec content lives in this hub
research_date: 2026-08-11
latest_update: 2026-08-11 — hub created (7 content files + stories/ sub-hub indexed)
---

# IHC OpenVisual Documentation Index

> Specification and reference docs for the IHC OpenVisual desktop app: the product spec, the
> E1–E16 user-story collection, vendor-comparison/verification ledgers, and the icon language.

**Total Documents:** 7 content files here (plus this index), and 16 story files under `stories/`

## Collection Structure

| Location | Contents | Entry point |
| --- | --- | --- |
| `docs/` (this directory) | Product spec, alignment/verification docs, icon references | this file |
| `docs/stories/` | Behavioural spec: epics E1–E16 with user stories (US-NNN) and acceptance criteria | [stories/INDEX.md](stories/INDEX.md) |

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
| [error-list.md](error-list.md) | Findings catalogue of `.vis` project error conditions: categories, severities, sources, live-vendor verification status | corruption findings, user-sourced findings, severity, live verification, draft | 449 |

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

#### [error-list.md](error-list.md) — project findings catalogue

1. How to read a row / Categories / Severity / Source
2. File-sourced findings — corruption
3. User-sourced findings — action or lack of action
4. Deliberate non-findings / Behavioural requirements
5. Verification against the live application
6. Appendix — field evidence: the documentation set of eight is closed

#### [icon_codes.md](icon_codes.md) — icon selection reference

1. Quick lookup — icon code → `Assets/*.svg`
2. Structure & sections / Programs & logic / Resources (function-block variables) / Links
3. Products — code is **not** a reliable discriminator
4. Elements with no `icon` attribute (structural / config)
5. Text-only rendering — Unicode stand-ins

## Related Collections

- [stories/INDEX.md](stories/INDEX.md) — sub-hub for the E1–E16 user-story files (start there for any feature behaviour)
- [../../../ARCHITECTURE.md](../../../ARCHITECTURE.md) — repo-wide layers and invariants (the HOW these docs deliberately omit)
