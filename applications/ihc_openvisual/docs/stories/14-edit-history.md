---
version: 0.4.0
last-updated: 2026-08-02
status: draft
---

# E14 — Edit history (undo / redo)

> **Scope:** In scope (foundational, cross-cutting) — undo/redo is a general capability every
> project-mutating operation must honour, not a feature of any one command. It underlies every editing
> epic (E2–E9).

**Goal:** Let an IHC installer reverse any change made to the project — and re-apply a reversed change —
so that a mistake in any editing area (localities, products, function blocks, links, programming,
project metadata) can be recovered without rebuilding the work by hand.

**Scope:** the *Rediger* menu's *Undo* and *Redo* actions and their `Ctrl+Z` /
`Ctrl+Y` shortcuts; the requirement that every mutating operation across E2–E9 enters the history; the
status-bar confirmation of what was undone/redone; redo invalidation on a new edit; and the
empty-history behaviour (greyed menu items, inert shortcuts). **Scope excludes:** the
per-command semantics of the edits themselves (their own epics); and non-mutating actions (view/mode
switches, chrome toggles, simulation) which do not enter the history.

**Acceptance criteria (epic level):**
- MUST: Every operation that changes the project is undoable with *Undo* (`Ctrl+Z`) and, once undone,
  re-applicable with *Redo* (`Ctrl+Y`), across all editing epics (E2–E9).
- MUST: Undo/redo restores the exact prior/post state, reflected identically in both panes, and the
  status bar names the action reversed or re-applied.
- MUST: One user action is one undo step, and undoing an action that cannot be reversed degrades
  gracefully rather than crashing.
- SHOULD: A destructive, cascading edit (e.g. deleting a non-empty locality, US-009) is reversed as a
  single step.

**Readiness:** Ready — the undo history is **multi-level with unlimited depth** (no configured step cap,
bounded only by process memory). See US-052.

---

## US-052 — Undo and redo any edit

> **Cross-cutting:** this applies to **every** project-mutating operation across E2–E9 (insert, delete,
> rename, move, configure, link, paste, variable/program authoring). It is a general capability, not
> tied to one command.

**As an** IHC installer, **I want** every change I make to the project to be reversible with undo, and
re-applicable with redo, **so that** I can recover from any mistake without rebuilding work by hand.

**Scope excludes:** non-mutating actions (view/mode switches, toolbar/status-bar toggles, simulation)
which do not enter the edit history.

### Acceptance criteria (Checklist)

- MUST: **Every** operation that changes the project is undoable by *Edit > Undo* (`Ctrl+Z`)
  and, once undone, re-applicable by *Edit > Redo* (`Ctrl+Y`) — this includes, across all editing
  epics: locality add/rename/delete (E2), product insert/configure/address/delete (E3–E4),
  function-block insert/unlock/save (E5), product↔FB and scenario links (E6), variable and
  program-logic authoring (E7), enumerator changes (E7), and project-info/data-table edits (E9).
- MUST: Undo restores the project to its exact prior state, reflected **identically in both panes**
  (as every edit is, per E2), and redo restores the post-edit state.
- MUST: The status bar names the action being reversed or re-applied (e.g. `Undoing insertion of
  <product>`).
- MUST: Making a **new** edit after an undo clears the redo history — the undone change can no
  longer be redone.
- MUST: With nothing to undo (a freshly opened/saved project with no edits since), *Edit > Undo* is
  **greyed**, and `Ctrl+Z` changes nothing — the status bar explains why (`Nothing to undo.`, per
  US-044). *Redo* behaves the same when there is nothing to redo.
- MUST: **One user action is one undo step.** A single action taken in the UI is reversed by a single
  *Undo* and re-applied by a single *Redo* — the history's granularity is the user's action, not the
  internal edits it performs.
- MUST: Undoing an action the application **cannot reverse** **degrades gracefully**: the application
  says the action cannot be undone and stays running with the project intact. It never crashes,
  and never leaves the project half-reverted.
- SHOULD: A destructive edit that required confirmation or cascaded (e.g. deleting a non-empty
  locality, US-009) is reversed **as one step**, restoring the locality, its products, and the
  dependent commands/conditions together.
- SHOULD: An action that cannot be reversed is either **made undoable** or **guarded before it happens**.
  **Prefer making it undoable** — a guard interrupts everyone to protect the few, an undo protects everyone
  and interrupts nobody. **No project-mutating action in IHC OpenVisual currently needs the guard branch**
  (unlocking a library block, once thought irreversible, is an ordinary undoable edit — US-020).
- SHOULD: Non-mutating actions (entering/leaving programming mode, US-026; toolbar/status-bar
  toggles, US-051) do **not** appear on the undo history.
- MUST: **Id allocation is stable across the history.** Undo restores the id counter along with the
  content (a cancelled or undone insert burns no ids), redo re-creates an element under the **same**
  id it first had, and an element restored by a later undo keeps that id — an insert → undo → redo →
  delete → undo cycle leaves both the surviving element's id and the project's next-id counter exactly
  where a single plain insert would have left them.

### AC illustrations

- Insert a locality → `Ctrl+Z` removes it (status: an "Undoing …" hint, as illustrated in US-001) →
  `Ctrl+Y` reinserts it in the same position, shown in both panes.
- Delete `Living room` while it holds a lamp output linked to a block (US-009) → one `Ctrl+Z` brings back the
  locality, the product, **and** the function-block command that referenced it — the cascade is undone
  as a unit.
- After undoing an insertion, dragging a new link instead of redoing leaves the earlier insertion
  unredoable.
- Unlocking a library block and then pressing `Ctrl+Z` **re-locks the block** and leaves the application
  running — the unlock is an ordinary undoable edit.

### Constraints

- Verification method — **Demonstration** that a representative edit from each editing epic (E2–E9)
  undoes and redoes; that a cascading delete reverses as one step; that redo is invalidated by a
  new edit; and that undoing an unlock degrades gracefully rather than crashing.
- **Undo depth** is **unlimited** — the app is multi-level with no configured step cap, bounded only by
  process memory.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (multi-level, unlimited depth) — granularity is one-action-one-step,
the graceful-degradation rule holds (unlocking a library block and then pressing `Ctrl+Z` runs cleanly,
re-locking the block with the app still running), and *Undo*/*Redo* grey on an empty history with the
status-bar explanation on their shortcuts.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-052 | Undo and redo any edit | Ready | E14 | Must | -- |
