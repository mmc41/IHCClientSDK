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

## How to read a cell

| Mark | Meaning |
| --- | --- |
| **✓** | Measured on both sides and resolved — matched, or divergence recorded and closed |
| **~** | Partially measured: some members or some states done, the set incomplete |
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
| Checklist dimensions | 2 | 11 | 6 | — | 19 |
| Dialogs × dimensions 12/13/14 | 11 | 14 | 16 | 4 | 45 |

*Last updated 2026-08-11.*

## 1. Checklist dimensions

| # | Dimension | State | Evidence that exists | What is missing |
| --- | --- | --- | --- | --- |
| 1 | Byte-identical saved files | ✓ | The `tests/testdata/` oracle corpus, replayed byte-for-byte by `safe_project_tests` — vendor-authored projects and recorded vendor mutations | — |
| 2 | Feature/workflow coverage and route | ~ | Product insert, block landing, report hand-off, section flyouts, the one-modem refusal | No pass enumerated from the Ready stories' acceptance criteria; the route half is measured only where a finding happened to raise it |
| 3 | Model correctness (results, side effects, state after reopen) | ~ | `safe_project_tests`; the decimal/weekday round-trip findings | Not exercised per edit family from the GUI side; rejected-operation set unenumerated |
| 4 | Two-pane tree layout | ✗ | — | Never compared in a record that survives |
| 5 | Configuration and programming modes | ~ | The section flyout's mode dependence | The mode surfaces themselves (pane roots, status text, what each mode permits) never compared |
| 6 | Tree content, order, nesting, expansion, selection | ~ | Product/terminal/scenario rows, link and program rows, what the tree opens when a block lands, decimal and weekday row text | Expansion and selection state not compared as such; no whole-tree dump pair in a surviving record |
| 7 | Menus and toolbars, and their enablement | ✓ | Menu bar and toolbar member by member, context menus, the product menu's ~100 members and its two ordering rules, greying rules | — |
| 8 | Tree-node actions | ~ | Copy/paste on terminals and pins, link rows, program rows, section flyouts, the enum picker | Not every node type's action set enumerated and individually resolved |
| 9 | Drag and drop (what can and cannot be dragged) | ✗ | — | No gesture transcript on either side. Both the permitted and the **refused** halves are unmeasured, and the refused half cannot be found by exploring |
| 10 | Mouse/pointer behaviour, with and without modifiers | ✗ | — | No gesture transcript. Modifier-qualified gestures have never been driven |
| 11 | Keyboard behaviour, shortcuts, focus, menus, dialogs | ✗ | — | No gesture transcript. Real-input keys are needed for modifiers (a posted modifier does not exist to the target) |
| 12 | Dialog layout and shape | ~ | See §2 — 11 of 41 comparable cells | 3 dialogs never opened |
| 13 | Dialog functionality (buttons, links, activatable rows, sub-dialogs) | ~ | See §2. The activatable-row half was **driver-blind on the OpenVisual side until 2026-08-11** | Row activation only exercised for the module map; `Avanceret` never pressed |
| 14 | Validation, errors, warnings, confirmations, recovery | ~ | One instance: the one-modem refusal | 14 of 15 dialogs have no evidence at all. No refusal, no confirmation, no recovery path compared as a set |
| 15 | Tooltips and in-place help | ✗ | — | Never compared. `stories/13-tooltips.md` is Ready and undemonstrated against the original |
| 16 | Generated output: reports and validation results | ~ | The 24 report oracles in `tests/testdata/reports/`, derived from the original's own output and regenerated byte-identically | Validation/finding output never compared against the original's |
| 17 | Undo/redo history, grouping, labels, dirty state | ✗ | Only the label difference registered as F-8b | What is undoable, how steps group, when each is available, and the saved/dirty behaviour are unmeasured on both sides |
| 18 | Visual/UI semantics: text, icons, decorations, states | ~ | Dialog titles as a set, captions, greying rules, toolbar grouping, terminal row wording | Not held across themes or text scales — see *State axes* |
| 19 | UX and accessibility | ~ | Menu grouping, dialog captions, the accessible names of terminal rows, keyboard-shortcut reasons | Keyboard-only operation never demonstrated end to end; no accessibility-tree pair |

## 2. Dialogs — dimensions 12 / 13 / 14

The checklist makes "the dialogs" a set-valued dimension: it is complete only when **each** member is
individually resolved.

| Dialog (OpenVisual view) | 12 layout | 13 functionality | 14 validation | Reason for any ✗ |
| --- | --- | --- | --- | --- |
| `PropertiesWindow` (locality / block) | ✓ | ~ | ✗ | 13: OK/Cancel only — it has no sub-dialogs. 14: no invalid input ever submitted |
| `VariablePropertiesWindow` | ✓ | ✓ | ✗ | 14: no invalid input ever submitted |
| `ProductPropertiesWindow` | ~ | ~ | ✗ | 12: fields and terminal grid seen, not field-for-field. 13: **`Avanceret` never pressed**. 14: never attempted |
| `PinPropertiesWindow` (*Klemme adressering*) | ✓ | ~ | ~ | 13: the two address lists driven, not every control. 14: read-only refusal proven, nothing else |
| `ModemPropertiesWindow` | ~ | ✗ | ✗ | 12: both sides now inventoried and the divergence recorded — **F-52**, open: 91 controls vs 39, four captioned groups vs flat labels, `Nummer 1–30` vs `Nummer 1–4`, `Navn` disabled vs editable (OpenVisual side re-read live 2026-08-11 via `product insert` → `dialog read`). Open, so not ✓; the phone count is dimension **2**, not 12 — 26 absent recipient fields are an absent capability. Whether its combo-vs-edit rows fall under the registered suggestion-drop-down difference is unruled. 13/14: no control in it has been driven on either side |
| `ModuleMapWindow` (*Datalinie moduler*) | ~ | ✓ | ✗ | 13 settled 2026-08-11: all four columns open the same per-module dialog in the original; OpenVisual's rows realize no cells and double-click is `NoEffect` → **unimplemented story 09/US-050**, not a difference. 12: the surrounding dialog not compared field-for-field. 14: the per-module dialog's cascading enablement not exercised on the OpenVisual side, which has no such dialog |
| `AdvancedDimmerWindow` (*Avanceret*) | ✗ | ✗ | ✗ | Never opened. One unpressed button away — reached from `ProductPropertiesWindow` |
| `SceneContainerWindow` (*Scenarier*) | ✓ | ~ | ✗ | 13: rows read, activation not driven. 14: never attempted |
| `SceneValueWindow` | ✓ | ~ | ✗ | 13: combo read and committed; other controls not driven. 14: never attempted |
| `EnumTypeManagerWindow` | ✓ | ✓ | ~ | 14: the read-only greying rule seen; no invalid input submitted |
| `EnumDefinitionWindow` | ~ | ✗ | ✗ | Seen only as the target of the type editor route; never driven in its own right |
| `ProjectInfoWindow` | ✓ | ~ | ✗ | 13: field groups read, controls not driven. 14: never attempted |
| `ReportPickerWindow` | ✓ | ~ | n/a | 13: the vendor side is driver-blind here — the original hands off to an external browser (registered difference). 14: no vendor counterpart to validate against |
| `NamePromptWindow` | ~ | ✗ | ✗ | Seen in passing while doing other work; never the subject of a measurement |
| `AboutWindow` | n/a | n/a | n/a | Branding — Non-checklist |

## 3. Other set-valued dimensions

Each is complete only when every member is individually resolved. Member lists belong to the campaign
that enumerates them; this table records whether that enumeration has happened.

| Set | State | Note |
| --- | --- | --- |
| Node types × their menus and actions | ~ | Product, terminal, pin, link, program, block section and enum covered; the set was never enumerated first, so completeness is unknown |
| Product families (catalog menu) | ✓ | All ~100 members and both ordering rules compared |
| Variable value types | ✓ | All 18 driven through the variable dialog |
| Report types × mode × format | ✓ | 24 combinations pinned byte-for-byte by oracle |
| Themes | ✗ | Dimension 18 measured in one theme only |
| Text scales | ✗ | Dimension 18 measured at one text scale only |
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
| `dialog.clickRow` (activate a row, optionally a column, inside a dialog) | both | Dimension 13's activatable-row half, for every dialog with a list | Added to both drivers 2026-08-11. The **vendor** side's `--column` refinement requires `/mcp reconnect` and is **not yet published** — until it is, per-column questions are answerable on the OpenVisual side only |
| Anything needing the app in the **foreground** | OpenVisual | `fb.insertTemplate`; every `key`-mechanism verb (`edit.undo/redo`, `node.cut/copy/paste`, `programming.enter`, `fb.insertEmpty`, …); `dialog.cancel`; the hover-driven `menu.dumpBar` / `catalog.products` / `catalog.functionBlocks` | A scripted process cannot take the Windows foreground, so these need a session where a person fronts the window. **Pattern-driven verbs do not** — `product.insert`, `locality.insert`, `tree.*`, `menu.invoke --id`, `dialog.read/click/setText` all ran headless-ish on 2026-08-11. Prefer `menu invoke --id <row id>` over the `key` twin when the foreground is unavailable |

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
