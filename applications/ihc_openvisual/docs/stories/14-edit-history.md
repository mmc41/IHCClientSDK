---
version: 0.1.0
last-updated: 2026-07-12
status: draft
---

# E14 — Edit history (undo / redo)

> **Current scope:** ✅ **In scope (foundational, cross‑cutting)** — undo/redo is a general capability
> every project‑mutating operation must honour, not a feature of any one command. It underlies every
> editing epic (E2–E9) the way E11–E13 underlie the whole UI.

**Goal:** Let an IHC installer reverse any change made to the project — and re‑apply a reversed change —
so that a mistake in any editing area (localities, products, function blocks, links, programming,
project metadata) can be recovered without rebuilding the work by hand.

**Scope:** the *Edit* menu's *Undo* and *Redo* actions and their `Ctrl+Z` /
`Ctrl+Y` shortcuts; the requirement that every mutating operation across E2–E9 enters the history; the
status‑bar confirmation of what was undone/redone; redo invalidation on a new edit; and the
empty‑history no‑op. **Scope excludes:** the automatic crash/power‑loss backup (E1, US-005); the
per‑command semantics of the edits themselves (their own epics); and non‑mutating actions (view/mode
switches, chrome toggles, simulation) which do not enter the history.

**Acceptance criteria (epic level):**
- MUST: Every operation that changes the project is undoable with *Undo* (`Ctrl+Z`) and, once undone,
  re‑applicable with *Redo* (`Ctrl+Y`), across all editing epics (E2–E9).
- MUST: Undo/redo restores the exact prior/post state, reflected identically in both panes, and the
  status bar names the action reversed or re‑applied.
- SHOULD: A destructive, cascading edit (e.g. deleting a non‑empty locality, US-009) is reversed as a
  single step.

**Readiness:** Not Ready — the undo history **depth** (single‑ vs multi‑level) is unresolved; see US-052.

---

## US-052 — Undo and redo any edit

> **Cross‑cutting:** this applies to **every** project‑mutating operation across E2–E9 (insert, delete,
> rename, move, configure, link, paste, variable/program authoring). It is a general capability, not
> tied to one command.

**As an** IHC installer, **I want** every change I make to the project to be reversible with undo, and
re‑applicable with redo, **so that** I can recover from any mistake without rebuilding work by hand.

**Scope excludes:** the automatic crash/power‑loss backup (US-005); non‑mutating actions (view/mode
switches, toolbar/status‑bar toggles, simulation) which do not enter the edit history.

### Acceptance criteria (Checklist)

- [ ] MUST: **Every** operation that changes the project is undoable by *Edit > Undo* (`Ctrl+Z`)
  and, once undone, re‑applicable by *Edit > Redo* (`Ctrl+Y`) — this includes, across all editing
  epics: locality add/rename/delete (E2), product insert/configure/address/delete (E3–E4),
  function‑block insert/unlock/save (E5), product↔FB and scenario links (E6), variable and
  program‑logic authoring (E7), enumerator changes (E7), and project‑info/data‑table edits (E9).
- [ ] MUST: Undo restores the project to its exact prior state, reflected **identically in both panes**
  (as every edit is, per E2), and redo restores the post‑edit state.
- [ ] MUST: The status bar names the action being reversed or re‑applied (e.g. `Undoing insertion of
  <product>`).
- [ ] MUST: Making a **new** edit after an undo clears the redo history — the undone change can no
  longer be redone.
- [ ] MUST: Invoking *Undo* with nothing to undo (a freshly opened/saved project with no edits
  since) is a no‑op that changes nothing.
- [ ] SHOULD: A destructive edit that required confirmation or cascaded (e.g. deleting a non‑empty
  locality, US-009) is reversed **as one step**, restoring the locality, its products, and the
  dependent commands/conditions together.
- [ ] SHOULD: Non‑mutating actions (entering/leaving programming mode, US-026; toolbar/status‑bar
  toggles, US-051) do **not** appear on the undo history.

### AC illustrations

- Insert a locality → `Ctrl+Z` removes it (status: an "Undoing …" hint, as illustrated in US-001) →
  `Ctrl+Y` reinserts it in the same position, shown in both panes.
- Delete `Living room` while it holds a lamp output linked to a block (US-009) → one `Ctrl+Z` brings back the
  locality, the product, **and** the function‑block command that referenced it — the cascade is undone
  as a unit.
- After undoing an insertion, dragging a new link instead of redoing leaves the earlier insertion
  unredoable.

### Constraints

- Verification method — **Demonstration** that a representative edit from each editing epic (E2–E9)
  undoes and redoes; that a cascading delete reverses as one step; and that redo is invalidated by a
  new edit.
- **Open item — undo depth:** whether the history is single‑step or multi‑step, and how many steps it
  retains, is not yet established; confirm during implementation before fixing
  it. Treat as `[TBD]`. (R‑note.)

**Readiness:** Not Ready.
- [R3] Undo/redo **depth** (single‑ vs multi‑level; number of steps retained) is `[TBD]` — confirm
  during implementation.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-052 | Undo and redo any edit | Not Ready | E14 | Must | -- |
