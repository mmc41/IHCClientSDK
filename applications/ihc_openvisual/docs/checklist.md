# Checklist when comparing OpenVisual VS IHC Visual

This file is the **scope contract** for comparing IHC OpenVisual (this project) with the vendor
IHC Visual: *which* dimensions must match, which behaviour is the reference, what counts as
evidence, and how a verdict is reached. It deliberately does **not** say how to drive the two
applications, nor which concrete scenarios to run — both are derived per campaign.

## Before comparing — read these

| Source | What it settles |
| --- | --- |
| [Differences from the Original IHC Visual](product.md#differences-from-the-original-ihc-visual) | The register of **deliberate** divergences. An item on that list is **not a finding**. |
| [stories/](stories/) | The behavioural spec (acceptance criteria) OpenVisual is measured against. |
| The two drivers' own documentation | How each application is driven, and what a driver can and cannot observe. |

Drivers: **OpenVisual** via the `aui-openvisual` skill, **vendor** via the `ihcvisual` MCP server
(or its `app.exe` CLI sibling). Both need one *elevated* Windows desktop session with both apps
running; a driver reporting itself blind means *record nothing*, not *no difference*. The run
procedure — driver preflight, how each dump is normalized, evidence layout, safety gates — belongs
to the campaign that runs the comparison, not to this file.

## Which behaviour is the reference (oracle)

Stories, including those marked `Readiness: Ready`, are the current requirements, not independent
proof that the requirements are complete or correct. They are hypotheses to verify against the
union of user-visible behaviour discovered in both applications. A conflict between a story and
verified vendor behaviour is **unresolved**: neither source wins automatically, and the comparison
must not pass until evidence supports one of the divergence resolutions under Verdict. A faulty or
incomplete story is corrected; an intentional OpenVisual difference requires owner acknowledgement
and registration in both `product.md` and the affected story.

Registered deliberate differences define expected OpenVisual behaviour. Where no registered
difference applies, observed vendor behaviour is the reference unless evidence establishes a clear
vendor defect. Story wording alone is not such evidence.

The `.vis` project oracles in [`tests/testdata/projects/`](../../../tests/testdata/projects/) can
serve as ready-made vendor-side references for equivalent OpenVisual edits — but that folder holds
vendor-authored **and** synthetic fixtures, so confirm a file's provenance before treating it as
vendor truth.

## Completeness of a derived plan

A derived comparison plan must cover every applicable acceptance criterion in the Ready stories
**and** the union of user-visible behaviour discovered in both applications. Anything omitted must
map to the Differences register or to the Non-checklist below — an omission with no such mapping is
a gap in the plan, not a passed comparison.

**Every discovered interactive member must be exercised.** This includes buttons, links, selectable
or activatable rows, context actions, and implicit gestures such as single-click, double-click,
right-click and keyboard activation, even when the surface has no visible edit button or affordance.
A screenshot or control inventory proves presentation only; it does not prove interaction behaviour.

**The plan is enumerated from the Ready stories' acceptance criteria before comparing — not from what
exploring the two applications happens to surface.** Exploration finds what is visible; it cannot find
what is absent, so a criterion nobody thought to look at is never exercised and silently reads as passed.
Every MUST in a Ready story is a plan item until it is demonstrated, covered by a test, or mapped to a
registered difference. A findings list orders discovered work; it never states coverage.

**A criterion that cannot be exercised because the driver lacks the verb is a driver gap, not a pass.**
Record it unresolved and fix the driver. Dimension 13 needs a verb for activating a row *inside a
dialog* — which is not the same as clicking a button, or a row in a tree.

**Set-valued dimensions require member-level completeness.** Where a dimension ranges over a set of
instances — the dialogs, each node type's menus and actions, the report types),
the product families / variable types / themes / text scales — the plan is complete for that
dimension only when **each member is individually resolved** (matched, or mapped to a registered
difference or the Non-checklist). A result generalized from a sample is not completeness; an
un-addressed member is **unresolved** (see Verdict), whatever the members that *were* checked
showed. Which members exist, and how they are discovered and driven, is the plan's to determine —
this file only sets the bar.

## Checklist (goal)

Each line names the evidence that decides it. A *dump* is a driver's structured read of that
surface, normalized the same way on both sides before it is compared.

| # | Dimension | Evidence |
| --- | --- | --- |
| 1 | Are saved IHC Visual files identical (binary the same) for same content (critical importance - applies to all features) | `.vis` / `.ifb` pair |
| 2 | Feature and workflow coverage — every in-scope capability is present, completes, and takes the same user-visible route: which action triggers which response (dialog, mode change, selection), and the order of steps in multi-step flows | driver transcript |
| 3 | Model correctness — operation results, side effects, rejected operations, invariants, and the state after save and reopen | `.vis` pair + reopened tree dump |
| 4 | Two pane tree layout | screenshot, window title |
| 5 | Configuration and programming modes | pane-root probe, status text |
| 6 | Tree content: nodes present, order, nesting, expansion and selection state | tree dump |
| 7 | Menus and toolbars options and when they are enabled/disabled | menubar / context-menu dump |
| 8 | Treenode actions | context-menu dump + `.vis` pair |
| 9 | Drag drop — both what can be dragged and what can not (between panes and inside a pane) | gesture transcript + `.vis` pair |
| 10 | Mouse/pointer behavior, including activation, navigation and scrolling, with and without modifiers | gesture transcript |
| 11 | Keyboard behavior, including shortcuts, navigation, focus, menus and dialogs (Windows only — different on Mac/Linux) | gesture transcript |
| 12 | Overall layout/shape of dialogs so they are recognizable for a user | screenshot |
| 13 | All dialog functionality (buttons, links, clickable/activatable rows and resulting subdialogs) | dialog dump + gesture transcript + resulting dialog dump |
| 14 | Validation, errors, warnings, confirmations, recovery and the resulting state | dialog dump, status text, resulting state |
| 15 | Tooltips and in-place help text | tooltip dump |
| 16 | Generated output: documentation reports and validation results | report / validation text |
| 17 | Undo/redo history: what is undoable, how steps are grouped and labelled, when undo/redo is available, and the saved/dirty-state behaviour | menubar dump + `.vis` pair + dirty marker |
| 18 | Visual/UI semantics: text, icons, decorations, focus, and enabled/disabled/read-only states — holding in every OpenVisual theme and text scale, without requiring pixel equality | screenshot + control-state dump |
| 19 | UX and accessibility: feedback, continuity, safe cancellation and recovery, keyboard-only use, and accessible roles/names/states | driver transcript + accessibility tree |

**Read-only describes mutation, not navigability.** A read-only table may still select or activate a
row, navigate to details, or open another dialog. Do not infer that a surface is non-interactive from
the absence of inline editing, an edit button, selection styling or an obvious pointer affordance;
exercise every discovered activation route and compare its resulting state.

## Non-checklist (not a goal)

- Noted enhancements, changes, limitations — [Differences from the Original IHC Visual](product.md#differences-from-the-original-ihc-visual)
- Layout format does not match at pixel level; coordinates and geometry
- Machine-local state: recent-file/MRU lists and remembered window placement — compare that the
  mechanism exists, never the machine-dependent contents
- Quantitative timing and performance — but a user-visible freeze, missing progress or blocked
  interaction is UX behaviour (dimension 19), not a performance measurement
- Branding, OpenVisual help, packaging and installation, about dialog
- Clear IHC Visual bugs

## Verdict

A comparison **passes** when every required comparison either **matches** or **conforms to a
registered deliberate difference**, and any saved-file pair produced is equal under the fixed
masks — the volatile clock stamps only (for a `.vis`: the root `id2` attribute and the
`<modified>` element, which every save rewrites; also `id1` where the scenario creates the
project fresh in both applications). Nothing else may be masked without an owner decision.

Missing evidence is not a pass. A dimension that was not exercised, whose evidence was not
recorded, or whose driver reported itself blind, is **unresolved**, and an unresolved dimension
fails the comparison just as a mismatch does — including a set-valued dimension with any member
unaddressed.

A contradiction between a story and verified user-visible behaviour is also unresolved. Recording
the contradiction in notes does not close it, and calling a discovered gesture hidden, implicit or
undocumented does not remove it from scope.

Anything else is a **divergence**, resolved in exactly one of three ways:

1. **OpenVisual or its story is wrong** → correct the story when necessary, reproduce the required
   behaviour with a failing test first, then fix and re-run the comparison from a fresh copy.
2. **The difference is intentional** → record it in the Differences register and in the affected
   story, and obtain owner acknowledgement. Until then it remains unresolved. Never record it here.
3. **The vendor is wrong, or a driver misread** → record the ruling with its evidence; a driver
   defect is fixed in the driver, not worked around in the comparison.

A divergence is never closed by editing this file.

## Maintenance rule

This file lists **dimensions and rules only** — no command syntax, no scenario lists, no per-run
results, no findings. Command syntax belongs to the drivers, scenarios and results to the campaign
that runs them, behaviour to the stories, and deliberate differences to `product.md`.
If it grows past one page, the new content belongs in one of those places instead.
