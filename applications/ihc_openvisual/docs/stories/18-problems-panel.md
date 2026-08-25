---
version: 0.1.0
last-updated: 2026-08-25
status: draft
---

# E18 — Problems panel

> **Scope:** In scope. The application already knows everything wrong with a project — the validation engine
> reports every finding with its severity, its category and the element it is about — and shows none of it. An
> installer therefore learns about a fault when a save or a transfer refuses, at the moment it is most expensive
> to learn. This epic gives the findings a permanent home: a panel that keeps itself up to date as the project is
> edited, and that navigates to the element a finding is about in one click.

**Goal:** Let an IHC installer see, at any moment, what is wrong with the open project and go straight to it, so
that faults are found while the project is being built rather than when it is being handed over.

**Scope:** a bottom panel listing the current findings with severity, code, message, element and category;
continuous background revalidation as the project changes; per-severity filtering with live counts; per-column
sorting; one-click navigation to the offending element; saving the panel's own list as a file; and withholding
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

- MUST: **Given** the three severity filters, **when** the panel opens, **then** all three are on, and each
  shows how many findings of its severity the project has.
- MUST: **Given** a severity filter, **when** it is switched off, **then** that severity's rows are hidden while
  its count is unchanged — hiding findings never suggests they were fixed.
- MUST: **Given** any column, **when** its header is chosen, **then** the list sorts by that column; **and when**
  it is chosen again, **then** the direction reverses, with the current direction shown on that header alone.
- MUST: **Given** Danish element names, **when** they are sorted, **then** Æ, Ø and Å order after Z.
- SHOULD: **Given** no sort has been chosen, **when** the list is shown, **then** the worst findings are first
  and findings of equal severity keep the order the project reads in.

## US-083 — Go from a finding to the element it is about

**As an** IHC installer, **I want** one click on a finding to take me to the element, **so that** I can fix it
instead of hunting for it.

**Acceptance criteria**

- MUST: **Given** a finding about an element, **when** its row is clicked, **then** that element is selected in
  the tree pane that owns it, with its ancestors opened so it is actually on screen.
- MUST: **Given** a finding about something inside a function block's program, **when** its row is clicked,
  **then** the application switches into programming mode on that block first.
- MUST: **Given** a finding about a locality or product while a block's program is open, **when** its row is
  clicked, **then** the application leaves programming mode first.
- MUST: **Given** a finding that leads nowhere, **when** its row is clicked, **then** nothing moves, and the row
  said so before the click.

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
- MUST: **Given** findings have been hidden by a severity filter, **when** the list is exported, **then** the
  file holds exactly the findings on screen, **and** it records which severities were included, so a short file
  cannot be mistaken for a short list of problems.
- MUST: **Given** every severity filter is switched off, **when** the list is exported, **then** the file holds
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
