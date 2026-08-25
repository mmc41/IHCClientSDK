---
scope: Navigation hub for the IHC OpenVisual behavioural specification — epics E1–E18 and their user stories (US-NNN) with Given-When-Then acceptance criteria
exclusions: No spec content lives here; behaviour is defined only in the story files. Excludes product.md and the reference docs (see ../INDEX.md)
research_date: 2026-08-11
latest_update: 2026-08-25 — E18 US-085 (export the findings list) added
---

# OpenVisual User Stories (Epics E1–E18) Index

> Per-epic behavioural spec files for IHC OpenVisual: each file holds one epic's user stories with
> Given-When-Then acceptance criteria — start here to find which epic owns a behaviour.

**Total Documents:** 18 content files (plus this index)

Files are numbered by epic (`01-*.md` = E1 … `18-*.md` = E18). Each has YAML frontmatter
(version, last-updated, status) and a scope note saying whether the epic is in scope,
partly in scope, or out of scope.

## Quick Navigation

- [Foundations & cross-cutting interaction](#foundations--cross-cutting-interaction) (8 docs)
- [Localities & products](#localities--products) (3 docs)
- [Function blocks & logic](#function-blocks--logic) (3 docs)
- [Outputs, controller & catalog exchange](#outputs-controller--catalog-exchange) (3 docs)
- [Out of scope](#out-of-scope) (1 doc)
- [Quick Reference](#quick-reference) (routing + cross-references)

## Document Index

### Foundations & cross-cutting interaction

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [01-application-shell.md](01-application-shell.md) | E1 application shell: start-up/shutdown, two-pane window, menu-bar host, project-file CRUD | US-001–004, US-051, US-063–065, recent projects, toolbar/status bar | 547 |
| [11-interaction-model.md](11-interaction-model.md) | E11 activation and keyboard model underpinning every CRUD interaction across all epics | US-044/045, US-067–070, context menus, shortcuts, dialogs, tree expansion state | 528 |
| [14-edit-history.md](14-edit-history.md) | E14 undo/redo capability every project-mutating operation must honour across editing epics | US-052, undo, redo, edit history, labelled commands | 126 |
| [15-structural-editing.md](15-structural-editing.md) | E15 node-agnostic delete, move, reorder and copy-paste generalised over all node types | US-053–056, structural editing, containers, clipboard, node relocation | 414 |
| [12-icon-language.md](12-icon-language.md) | E12 icon and colour vocabulary letting users read node type and state at a glance | US-046, icons, state colours, tree nodes, visual grammar | 118 |
| [13-tooltips.md](13-tooltips.md) | E13 hover tooltips exposing node documentation notes and IHC resource IDs in both trees | US-047/048, tooltips, resource ID, documentation note | 110 |
| [17-integrated-help.md](17-integrated-help.md) | E17 reading the catalog's own component and terminal descriptions where the installer chooses, identifies, documents and asks for help | US-075–079, catalog description, help action, insert lists, properties dialogs, hover | 268 |
| [18-problems-panel.md](18-problems-panel.md) | E18 a permanent panel listing the project's validation findings, kept current in the background, with one-click navigation to the offending element | US-080–085, validation findings, severity filters, sorting, navigation, transfer gate, findings export | 164 |

### Localities & products

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [02-localities.md](02-localities.md) | E2 modelling rooms and places as the *Lokaliteter* tree: rename, add, delete localities | US-006–009, locality tree, Properties dialog, delete with contents | 222 |
| [03-datalinie-products.md](03-datalinie-products.md) | E3 placing wired data-line products, documenting them, addressing terminals to I/O modules | US-010–013, wired products, data lines, terminals, initial value, modem | 424 |
| [04-wireless-products.md](04-wireless-products.md) | E4 wireless products: insert/properties in scope; controller linking deferred until wireless API exists | US-014–017, wireless dimmer, controller linking, signal test, partial scope | 250 |

### Function blocks & logic

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [05-function-blocks.md](05-function-blocks.md) | E5 inserting library or empty function blocks, unlocking library blocks, organising folders | US-018–021, *Funktioner* pane, library blocks, unlock, own/favourite folders | 350 |
| [06-product-fb-links.md](06-product-fb-links.md) | E6 wiring product pins to function-block pins by dragging, scenario links, link navigation | US-022–025, US-057/058, pin links, scenario links, drag legality | 441 |
| [07-fb-programming.md](07-fb-programming.md) | E7 authoring control logic inside a block: variables, events, conditions, enumerators, arithmetic | US-026–033b, programming mode, logic groups, case statements, power-up events | 775 |

### Outputs, controller & catalog exchange

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [09-documentation.md](09-documentation.md) | E9 project information entry, documentation reports (kind × mode × format), data-line module view | US-039–041, US-072–074, US-050, reports, verification appendix, pagination | 591 |
| [10-controller-transfer.md](10-controller-transfer.md) | E10 sending a finished project to the controller and retrieving one; dialogs need live hardware | US-042/043, transfer, upload, download, live-controller confirmation | 124 |
| [16-catalog-import.md](16-catalog-import.md) | E16 runtime import of product and function-block definition files extending the component library | US-059–062, catalog import, Library menu, app-data persistence, import errors | 250 |

### Out of scope

| Document | Description | Key Topics | Lines |
| --- | --- | --- | --- |
| [08-simulation.md](08-simulation.md) | E8 offline simulation and debugging — out of scope; retained as documentation if ever taken on | US-034–038, simulation, breakpoints, stepping, simulated time, log | 203 |

## Quick Reference

### I want to...

| Goal | Start with |
| --- | --- |
| Find which epic owns a user story id | [Epic → user stories](#epic--user-stories-owned) below |
| Add or change a menu/toolbar/shortcut behaviour | [01-application-shell.md](01-application-shell.md), then [11-interaction-model.md](11-interaction-model.md) |
| Change how a tree node is edited, moved, or deleted | [15-structural-editing.md](15-structural-editing.md) + [11-interaction-model.md](11-interaction-model.md) |
| Touch anything that mutates the project | [14-edit-history.md](14-edit-history.md) (undo/redo contract applies) |
| Work on products or localities | [02-localities.md](02-localities.md), [03-datalinie-products.md](03-datalinie-products.md), [04-wireless-products.md](04-wireless-products.md) |
| Work on function blocks or their links | [05-function-blocks.md](05-function-blocks.md), [06-product-fb-links.md](06-product-fb-links.md), [07-fb-programming.md](07-fb-programming.md) |
| Change reports or project verification output | [09-documentation.md](09-documentation.md) |
| Change node icons, colours, or tooltips | [12-icon-language.md](12-icon-language.md), [13-tooltips.md](13-tooltips.md) |
| Change what help or component descriptions the user can read | [17-integrated-help.md](17-integrated-help.md) (tooltip content also [13](13-tooltips.md)) |
| Change how validation findings are listed, filtered or navigated | [18-problems-panel.md](18-problems-panel.md) (the rules themselves are the SDK's) |
| Import catalogs or talk to the controller | [16-catalog-import.md](16-catalog-import.md), [10-controller-transfer.md](10-controller-transfer.md) |

### Epic → user stories owned

Stories are listed where they are **defined** (H2 heading); many files also reference stories
owned elsewhere. Ids in the files use U+2011 non-breaking hyphens — copy, don't retype.

| Document | Owns user stories |
| --- | --- |
| [01-application-shell.md](01-application-shell.md) | US-001–004, US-051, US-063, US-064, US-065 |
| [02-localities.md](02-localities.md) | US-006–009 |
| [03-datalinie-products.md](03-datalinie-products.md) | US-010–013 |
| [04-wireless-products.md](04-wireless-products.md) | US-014–017 |
| [05-function-blocks.md](05-function-blocks.md) | US-018–021 |
| [06-product-fb-links.md](06-product-fb-links.md) | US-022–025, US-057, US-058 |
| [07-fb-programming.md](07-fb-programming.md) | US-026–033, US-033b |
| [08-simulation.md](08-simulation.md) | US-034–038 |
| [09-documentation.md](09-documentation.md) | US-039–041, US-050, US-071 (retired), US-072–074 |
| [10-controller-transfer.md](10-controller-transfer.md) | US-042, US-043 |
| [11-interaction-model.md](11-interaction-model.md) | US-044, US-045, US-067–070 |
| [12-icon-language.md](12-icon-language.md) | US-046 |
| [13-tooltips.md](13-tooltips.md) | US-047, US-048 |
| [14-edit-history.md](14-edit-history.md) | US-052 |
| [15-structural-editing.md](15-structural-editing.md) | US-053–056 |
| [16-catalog-import.md](16-catalog-import.md) | US-059–062 |
| [17-integrated-help.md](17-integrated-help.md) | US-075–079 |
| [18-problems-panel.md](18-problems-panel.md) | US-080–085 |

### Scope status at a glance

| Status | Documents |
| --- | --- |
| In scope (foundational, cross-cutting) | [01](01-application-shell.md), [11](11-interaction-model.md), [12](12-icon-language.md), [13](13-tooltips.md), [14](14-edit-history.md), [15](15-structural-editing.md), [17](17-integrated-help.md), [18](18-problems-panel.md) |
| In scope (feature epics) | [02](02-localities.md), [03](03-datalinie-products.md), [05](05-function-blocks.md), [06](06-product-fb-links.md), [07](07-fb-programming.md), [16](16-catalog-import.md) |
| Partly in scope | [04](04-wireless-products.md) (linking needs wireless API), [09](09-documentation.md) (data-table editing excluded), [10](10-controller-transfer.md) (dialogs need live controller) |
| Out of scope | [08](08-simulation.md) |

## Related Collections

- [../INDEX.md](../INDEX.md) — parent hub: product spec (`product.md`), vendor-alignment docs, icon references
