# Checklist when comparing OpenVisual VS IHC Visual

This file is the **scope contract** for comparing IHC OpenVisual (this project) with the vendor
IHC Visual: *which* dimensions must match, what counts as evidence, and how a verdict is reached.
It deliberately does **not** say how to drive the two applications — see Method below.

## Before comparing — read these

| Source | What it settles |
| --- | --- |
| [Differences from the Original IHC Visual](product.md#differences-from-the-original-ihc-visual) | The register of **deliberate** divergences. An item on that list is **not a finding**. |
| [stories/](stories/) | The behavioural spec (acceptance criteria) OpenVisual is measured against. |
| `docs/comparison-method.md` | The method: driver preflight, facet normalization, evidence layout, safety gates. |

Drivers: **OpenVisual** via the `aui-openvisual` skill, **vendor** via the `ihcvisual` MCP server
(or its `app.exe` CLI sibling). Both need one *elevated* Windows desktop session with both apps
running; a driver reporting itself blind means *record nothing*, not *no difference*.

## Checklist (goal)

Each line names the evidence that decides it. Facet names are defined in the method doc.

| # | Dimension | Evidence |
| --- | --- | --- |
| 1 | Are saved IHC Visual files identical (binary the same) for same content | `.vis` / `.ifb` pair |
| 2 | Two pane tree layout | screenshot, window title |
| 3 | Konfiguration and Programming mode | pane-root probe, status text |
| 4 | Tree content: nodes present, order, nesting, expansion and selection state | `tree` facet |
| 5 | Menus and toolbars options and when they are enabled/disabled | `menubar` / `ctxmenu` facet |
| 6 | Treenode actions | `ctxmenu` facet + `.vis` pair |
| 7 | Drag drop — both what can be dragged and what can not (between panes and inside a pane) | gesture transcript + `.vis` pair |
| 8 | Mouse click behavior (left, right) with/without modifiers | gesture transcript |
| 9 | Keyboard shortcuts (Windows only — different on Mac/Linux) | gesture transcript |
| 10 | Overall layout/shape of dialogs so they are recognizable for a user | screenshot |
| 11 | All dialog functionality (buttons, clickable elements) | `dialog` facet |
| 12 | Errors, Warnings and confirmations produced | `dialog` facet, status text |
| 13 | Tooltips and in-place help text | `tooltips` facet |
| 14 | Generated output: documentation reports and validation results | report / validation text |
| 15 | Edit history: what is undoable, and how the steps are grouped and labelled | `menubar` facet + `.vis` pair |

## Non-checklist (not a goal)

- Noted enhancements, changes, limitations — [Differences from the Original IHC Visual](product.md#differences-from-the-original-ihc-visual)
- Layout format does not match at pixel level; coordinates and geometry
- Timing and performance
- Branding, OpenVisual help, packaging and installation
- Clear IHC Visual bugs

## Verdict

A comparison **passes** when every facet pair is byte-equal after the method doc's normalization,
**and** the saved-file pair is equal under the method doc's fixed masks (the volatile save stamps
only — nothing else may be masked without an owner decision).

Anything else is a **divergence**, resolved in exactly one of three ways:

1. **OpenVisual is wrong** → reproduce with a failing test first, then fix (engine issues in
   `safe_project_tests`/`safe_unit_tests`, GUI issues in `safe_visual_tests`), then re-run the
   comparison from a fresh copy.
2. **The difference is intentional** → record it in the Differences register and in the affected
   story. Never record it here.
3. **The vendor is wrong, or a driver misread** → record the ruling with its evidence; a driver
   defect is fixed in the driver, not worked around in the comparison.

A divergence is never closed by editing this file.

## Maintenance rule

This file lists **dimensions and rules only** — no command syntax, no scenario lists, no per-run
results, no findings. Command syntax belongs to the drivers, scenarios and results to the campaign
that runs them, behaviour to the stories, and deliberate differences to `product.md`.
If it grows past one page, the new content belongs in one of those places instead.
