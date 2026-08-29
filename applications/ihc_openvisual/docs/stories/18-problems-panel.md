---
version: 0.1.0
last-updated: 2026-08-29
status: draft
---

# E18 — Problems panel

> **Scope:** In scope. The application already knows everything wrong with a project — the validation engine
> reports every finding with its severity, its category and the element it is about — and shows none of it. An
> installer therefore learns about a fault when a save or a transfer refuses, at the moment it is most expensive
> to learn. This epic gives the findings a permanent home: a panel that keeps itself up to date as the project is
> edited, and that takes the installer to the element a finding is about when a row is activated.

**Goal:** Let an IHC installer see, at any moment, what is wrong with the open project and go straight to it, so
that faults are found while the project is being built rather than when it is being handed over.

**Scope:** a bottom panel listing the current findings with severity, code, message, element and category;
continuous background revalidation as the project changes; per-tier filtering with live counts across four tiers
(*Fatale fejl*, *Fejl*, *Advarsler*, *Information*); per-column
sorting; navigation to the offending element; saving the panel's own list as a file; and withholding
controller transfer while the project carries errors. Out of scope: suppressing or acknowledging a finding
(deliberately foreclosed — an id is a filtering key, never a way to silence a finding), authoring new rules, and
the documentation reports (E9 owns those; this epic's export is the findings list itself, not a document about
the project).

**Deliberate vendor divergence:** the original authoring tool has no equivalent surface. It validates on demand
and reports into a dialog, so this epic has no vendor behaviour to match and is registered as an enhancement in
`product.md`.

---

## US-080 — See the project's findings at a glance

**As an** IHC installer, **I want** a panel that lists everything currently wrong with the project, **so that**
I can judge the state of my work without asking for a check.

**Acceptance criteria**

- MUST: **Given** a project is open, **when** the application has validated it, **then** every finding the
  engine reported is listed with its severity, its code, its Danish message, the element it is about and its
  category.
- MUST: **Given** a finding's message, **when** it is shown in the panel, **then** it is the sentence the
  catalogue authored, unchanged — the panel never re-words or re-derives a message.
- MUST: **Given** the panel, **when** the application starts, **then** it is visible; and **when** the user
  chooses *Vis ▸ Problemer*, **then** it is hidden or shown again. Its visibility and height last for the
  session only.
- MUST: **Given** a finding that names no single element — a whole-project finding, a malformed id, a duplicate
  id — **when** it is listed, **then** it is still listed, marked as leading nowhere rather than omitted.
- SHOULD: **Given** many findings, **when** the list is shown, **then** scrolling it stays responsive (one
  corpus project alone produces 150 findings).

## US-081 — Keep the findings current while I work

**As an** IHC installer, **I want** the list to follow my edits by itself, **so that** what I am looking at is
about the project as it is now.

**Acceptance criteria**

- MUST: **Given** the project is edited, **when** the edit is committed, undone or redone, **then** the panel
  revalidates and rebinds without any gesture from the user.
- MUST: **Given** a burst of rapid edits, **when** they arrive, **then** the project is validated once for the
  burst rather than once per edit.
- MUST: **Given** the project is saved, **when** nothing about the document changed, **then** nothing is
  revalidated.
- MUST: **Given** a different project is opened, created or closed, **when** it replaces the current one,
  **then** the previous project's findings are cleared immediately and the new project is validated once.
- MUST: **Given** the application has not yet produced a result for the current project, **when** the panel is
  shown, **then** it says it is validating and never that no problems were found.
- SHOULD: **Given** an edit whose revalidation takes longer than about a second, **when** the wait passes that
  point, **then** the panel marks itself as working while keeping the previous findings visible and clickable.
- SHOULD: **Given** a revalidation that completes quickly, **when** it finishes, **then** no busy indicator was
  ever shown.

## US-082 — Narrow the list to what I care about

**As an** IHC installer, **I want** to filter and sort the findings, **so that** a long list becomes the short
one I am working through.

**Acceptance criteria**

- MUST: **Given** the four tier filters — *Fatale fejl*, *Fejl*, *Advarsler*, *Information* — **when** the panel
  opens, **then** all four are on, and each shows how many findings of its tier the project has.
- MUST: **Given** a finding whose rule refuses an operation, **when** it is listed, **then** it appears under
  *Fatale fejl* and not under *Fejl*, though both tiers hold Error findings and both withhold controller
  transfer — the split says which faults stop the project being written at all.
- MUST: **Given** a tier filter, **when** it is switched off, **then** that tier's rows are hidden while
  its count is unchanged — hiding findings never suggests they were fixed.
- MUST: **Given** any column, **when** its header is chosen, **then** the list sorts by that column; **and when**
  it is chosen again, **then** the direction reverses, with the current direction shown on that header alone.
- MUST: **Given** Danish element names, **when** they are sorted, **then** Æ, Ø and Å order after Z.
- SHOULD: **Given** no sort has been chosen, **when** the list is shown, **then** the worst findings are first —
  *Fatale fejl*, then *Fejl*, then *Advarsler*, then *Information* — and findings of equal tier keep the order
  the project reads in.

## US-083 — Go from a finding to the element it is about

**As an** IHC installer, **I want** activating a finding to take me to the element, **so that** I can fix it
instead of hunting for it.

The gesture is the second tier: **double-click or Enter**, never the selection. A findings list is read down,
and a single click that moved the trees, switched the editing mode or opened a window would take a scanning
reader on a journey they did not ask for — so a single click selects the row and does nothing else.

**Acceptance criteria**

- MUST: **Given** a finding about an element, **when** its row is activated, **then** that element — or, where
  the tree draws no row for it, the nearest element above it that has one — is selected in the tree pane that
  owns it, with its ancestors opened so it is actually on screen; and the row named its destination beforehand.

  *A value inside a product is the common case of "no row of its own": a setting, a calibration row, a modem's
  telephone slot. The activation lands on the product, and US-086's route carries on into the dialog where such
  a value is actually edited. The bare ancestor fallback is what remains for an element outside any product.*
- MUST: **Given** any finding, **when** its row is merely SELECTED — a single click, or the arrow keys — **then**
  the row is selected and nothing else moves: no tree, no editing mode, no window.
- MUST: **Given** a finding about something inside a function block's program, **when** its row is activated,
  **then** the application switches into programming mode on that block first.
- MUST: **Given** a finding about a locality or product while a block's program is open, **when** its row is
  activated, **then** the application leaves programming mode first.
- MUST: **Given** a finding that leads nowhere — it names no single element, or the element it names has neither
  a row nor an ancestor with one — **when** its row is activated, **then** nothing moves and the row said so
  beforehand; and where an element was named but cannot be shown, the status line says so too.

## US-084 — Be stopped from sending a project that carries errors

**As a** commissioning technician, **I want** the application to withhold the transfer while the project has
errors, **so that** I do not commission an installation the controller will reject.

**Acceptance criteria**

- MUST: **Given** a completed validation carrying at least one error, **when** *Send projekt* is offered,
  **then** it is withheld and says the project contains errors to be fixed in the panel.
- MUST: **Given** findings that are only advisory, **when** *Send projekt* is offered, **then** it is available —
  advisory findings never withhold a transfer.
- MUST: **Given** the project has not been validated yet, **when** *Send projekt* is offered, **then** it is
  available: not having looked is not the same as having found something.
- MUST: **Given** the errors are fixed, **when** the next validation completes, **then** the transfer becomes
  available again with no other gesture.
- MUST: **Given** no controller is connected, **when** *Send projekt* is withheld, **then** the reason given is
  the missing controller — the nearer obstacle first.

**Readiness:** Ready — every criterion in US-080 to US-084 is observable in the headless suites, and the panel
is additionally driven end to end through UI Automation on Windows. The one criterion no automated test covers
is the visual quality of the ~150 ms dim animation, which is a rendering property rather than behaviour.

## US-085 — Export the findings list

**As an** IHC installer, **I want** to save the panel's list as a file, **so that** I can keep it beside the
project, compare it with a later one, or send it to whoever is helping me.

**Acceptance criteria**

- MUST: **Given** a validated project with findings, **when** the export is chosen and a destination given,
  **then** a file is written holding those findings, and it names the project, the save it describes and the
  moment it was produced.
- MUST: **Given** a validated project with nothing wrong, **when** the export is chosen, **then** it is
  available and writes a file holding no findings — a record that this save was checked and was clean is worth
  keeping, and is the same statement the panel is already making.
- MUST: **Given** the panel is still validating, **when** the export is offered, **then** it is withheld: there
  is no list yet, and a file naming the current project while holding nothing would read as a clean bill of
  health.
- MUST: **Given** the project has been edited past the findings on screen, **when** the export is offered,
  **then** it is withheld — the file would describe a superseded project while naming the current one, and
  would say so nowhere.
- MUST: **Given** findings have been hidden by a tier filter, **when** the list is exported, **then** the
  file holds exactly the findings on screen, **and** it records which tiers were included, so a short file
  cannot be mistaken for a short list of problems. *Fatale fejl* and *Fejl* both being Error findings, recording
  the severities alone cannot tell a fatal-only export from an all-errors one, so the file must say.
- MUST: **Given** every tier filter is switched off, **when** the list is exported, **then** the file holds
  no findings and records that none were included — this file and the clean project's file must not read alike.
- MUST: **Given** a column has been sorted, **when** the list is exported, **then** the file's findings are in
  the order the panel shows them.
- MUST: **Given** the export is chosen, **when** the destination is not given, **then** nothing is written and
  nothing is reported.
- MUST: **Given** a destination that cannot be written, **when** the export is attempted, **then** the failure
  is reported in Danish, naming what failed, and the panel is left as it was.
- SHOULD: **Given** the panel is hidden, **when** the export is wanted, **then** it is reached by showing the
  panel again — the export belongs to the panel, as its filters and sorting already do.

**Readiness:** Ready — every criterion above is observable in `safe_visual_tests`: the four states gate the
command's availability, and the written file's contents are compared against the panel's visible rows. The
save dialog itself is a native window and is exercised through the dialog port rather than driven.

## US-086 — Go from a finding to the control the fix is made in

**As an** IHC installer, **I want** a finding to take me all the way to the field I have to change, **so that**
I stop hunting for it through dialogs I already know I need.

Activating a finding reveals its element, as US-083 says. This story is the rest of that one gesture: the
dialog the value lives in opens and the caret lands in the field itself. Selecting a row is still the quiet
gesture that moves nothing — the two answer different questions, *which finding is this?* and *let me fix it.*

**Acceptance criteria**

- MUST: **Given** a finding, **when** its row is activated by double-click, **then** the route runs; **and given**
  the same row, **when** it is activated by Enter, **then** the identical route runs. A keyboard user reaches the
  fix by the route a mouse user takes.
- MUST: **Given** a row is activated with Enter, **when** the keystroke is handled, **then** it does not also
  press whatever default button the surrounding window has.
- MUST: **Given** a finding about a value on a product, **when** its row is activated, **then** the product's
  dialog opens with that field focused and on screen.
- MUST: **Given** a finding about a value on one of a product's terminals, **when** its row is activated, **then**
  the product's dialog opens with that terminal's row selected, the terminal's own editor opens **on top of** it,
  and the caret lands in the field the finding is about. The parent dialog stays open underneath throughout.
- MUST: **Given** a finding whose attribute the owning dialog does not offer as an editable field, **when** its
  row is activated, **then** the dialog opens and nothing is focused — and the row said "dialog", not "field",
  beforehand.
- MUST: **Given** a finding about a value on one of a product's configurable constants, **when** its row is
  activated, **then** the product's dialog opens with that *Indstillinger* row selected and the constant's
  editor opens on top of it (US-087). A constant has no tree row of its own, so the grid is the only way to it.
- MUST: **Given** a finding that names no FIELD, on an element the tree draws a row for, **when** its row is
  activated, **then** the tree lands on that row and no dialog opens. This is the general rule and not only the
  link family: an empty locality, a block with no program, a variable written but never read are all repaired by
  a gesture on the row, and a modal the installer must dismiss first is a detour, not a shortcut.
- MUST: **Given** a finding about the PROJECT rather than about anything in it, **when** its row is activated,
  **then** the one window that repairs it opens — chosen by the finding's code, since there is no element to
  derive it from — and no tree moves.
- MUST: **Given** a finding that names neither a reachable element nor such a window, **when** its row is
  activated, **then** nothing opens and the panel says so.
- MUST: **Given** any activated route, **when** the installer cancels out of the dialogs it opened, **then** the
  project is unchanged. This feature navigates; it never repairs anything on the installer's behalf.

**The row says which depth it has, before the gesture.** Its tooltip names the destination: the tree, the owning
dialog, or the exact field. The promise and the route are computed once, from one resolver, so a row cannot
offer a field and then open a dialog with nothing focused.

**A deliberate asymmetry with the tree, stated so it is not filed as a bug.** Double-clicking a terminal *in the
tree* opens the product's dialog and stops there — the installer chose that terminal and can see the grid.
Activating a *finding* about the same terminal opens the product dialog, the terminal's editor and the field,
because the finding names the value, and stopping at the dialog would leave the installer to find it again. The
two gestures start in different places and are allowed to end in different places.

**One visit, one undo entry.** Everything an activated route opens is a single act: values entered in a stacked
editor join the visit, nothing reaches the project until the outermost dialog is accepted, and *Fortryd*
afterwards takes back the whole visit rather than half of it. The same editor opened directly from the tree has
no visit to belong to and commits on its own, which is the one place the two routes differ.

**Readiness:** Ready — the route classes and their degradations are covered in `safe_visual_tests`, and the two
deepest are additionally driven end to end through UI Automation on Windows: a terminal's cable-colour finding
activated by both gestures, and a product field finding. What no automated test covers is whether the caret is
where the installer was *looking* — a judgement about attention rather than about behaviour.
