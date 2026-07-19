---
version: 0.3.0
last-updated: 2026-07-17
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
- MUST: One user action is one undo step, and undoing an action that cannot be reversed degrades
  gracefully rather than crashing.
- SHOULD: A destructive, cascading edit (e.g. deleting a non‑empty locality, US-009) is reversed as a
  single step.

**Readiness:** Ready — the undo history **depth** is resolved (fablerefac D1 / W4‑4): IHC OpenVisual's own
retention is **`Unlimited`** (no configured cap, bounded only by process memory); the vendor's retention is a
now‑informational measure, not a blocker. *(This app is **multi‑level**, not "single‑ vs multi‑level".)* See US-052.

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
- [x] MUST: The status bar names the action being reversed or re‑applied (e.g. `Undoing insertion of
  <product>`). *(Met: the reversed/re‑applied action is read from `ProjectWorkflow.LastChange` and surfaced
  to the status bar — fablerefac W3‑6.)*
- [ ] MUST: Making a **new** edit after an undo clears the redo history — the undone change can no
  longer be redone.
- [ ] MUST: Invoking *Undo* with nothing to undo (a freshly opened/saved project with no edits
  since) is a no‑op that changes nothing.
- [ ] MUST: **One user action is one undo step.** A single action taken in the UI is reversed by a single
  *Undo* and re‑applied by a single *Redo* — the history's granularity is the user's action, not the
  internal edits it performs.
- [ ] MUST: Undoing an action the application **cannot reverse** (see below) **degrades gracefully**: the
  application says the action cannot be undone and stays running with the project intact. It never crashes,
  and never leaves the project half‑reverted.
- [ ] SHOULD: A destructive edit that required confirmation or cascaded (e.g. deleting a non‑empty
  locality, US-009) is reversed **as one step**, restoring the locality, its products, and the
  dependent commands/conditions together.
- [ ] SHOULD: An action that cannot be reversed is either **made undoable** or **guarded before it happens**.
  **Prefer making it undoable** — a guard interrupts everyone to protect the few, an undo protects everyone
  and interrupts nobody. **No project‑mutating action in IHC OpenVisual currently needs the guard branch.**

  > **Corrected 2026‑07‑17 (was: "unlocking a library function block (US-020) is the known instance, and it
  > is guarded by a warning").** The one action this criterion cited as irreversible **is not irreversible
  > here**: `Ctrl+Z` after an unlock fully re‑locks the block and the application keeps running (F‑065). So
  > IHC OpenVisual already took the first branch, and US-020's warning — specced but never built — has been
  > **deleted rather than implemented**. The example is removed rather than replaced because there is no
  > longer a known instance to name; if one appears, this criterion is where it lands. Evidence:
  > `RESULTS.md` **F‑064**, **F‑065**.
- [ ] SHOULD: Non‑mutating actions (entering/leaving programming mode, US-026; toolbar/status‑bar
  toggles, US-051) do **not** appear on the undo history.

> **Added 2026‑07‑16 — granularity confirmed, and one vendor defect explicitly not copied.**
> - **Granularity is measured aligned on both apps** (F‑045): on IHC Visual, inserting a locality
>   (24→25) undid to 24 and redid to 25; on IHC OpenVisual the same one‑action‑one‑step behaviour was
>   effect‑verified for a move. This is a regression baseline.
> - **IHC Visual crashes** when undoing an irreversible action: `edit.undo` after unlocking a function block
>   **closed the application outright**, while a normal insert undo/redo is stable (F‑046). Per the ruling's
>   exception #3 a vendor defect is not authoritative — IHC OpenVisual must **degrade gracefully, not
>   replicate the crash**, which is why the MUST above exists. ⚠ The crash's cause is inferred from the
>   sequence, not isolated by a minimal repro; the *requirement* on IHC OpenVisual stands regardless of
>   what exactly broke in the vendor. Evidence: `RESULTS.md` **F‑045**, **F‑046**.
>
> **Closed 2026‑07‑17 — and the outcome was better than "degrades gracefully".** The unlock‑then‑undo path
> was finally driven on IHC OpenVisual and it does not degrade at all: it **works**. No modal, no warning,
> the block re‑locked, the process alive. So the MUST is satisfied by the strongest possible means — the
> action simply is not irreversible here — and this app **survives the exact sequence that kills the
> vendor**. ⭐ **Worth a standing regression test** (`safe_visual_tests`: unlock → undo → the block is locked
> again and the app lives): it is cheap, and it guards the one sequence known to be fatal in the tool this
> app replaces. Evidence: `RESULTS.md` **F‑065**.

### AC illustrations

- Insert a locality → `Ctrl+Z` removes it (status: an "Undoing …" hint, as illustrated in US-001) →
  `Ctrl+Y` reinserts it in the same position, shown in both panes.
- Delete `Living room` while it holds a lamp output linked to a block (US-009) → one `Ctrl+Z` brings back the
  locality, the product, **and** the function‑block command that referenced it — the cascade is undone
  as a unit.
- After undoing an insertion, dragging a new link instead of redoing leaves the earlier insertion
  unredoable.
- Unlocking a library block and then pressing `Ctrl+Z` **re‑locks the block** and leaves the application
  running — **the same sequence closes IHC Visual outright.**

  > **Corrected 2026‑07‑17 (was: "reports that the unlock cannot be undone and leaves the block unlocked").**
  > The illustration described the vendor's constraint, not this app's behaviour, and it is measurably wrong:
  > IHC OpenVisual's undo reverses the unlock completely. Evidence: `RESULTS.md` **F‑065**.

### Constraints

- Verification method — **Demonstration** that a representative edit from each editing epic (E2–E9)
  undoes and redoes; that a cascading delete reverses as one step; that redo is invalidated by a
  new edit; and that undoing an unlock degrades gracefully rather than crashing.
- **Open item — undo depth:** ⚠ **"single‑ vs multi‑level" is NOT open for IHC OpenVisual** — this app is
  **multi‑level**, already implemented (see *Implementation status*). What remains `[TBD]` (R‑note) is two
  narrower halves: **(a) the VENDOR's depth — a MEASURE:** how many steps does IHC Visual retain, and does a
  second consecutive `Ctrl+Z` undo a second step? **(b) the SELF half:** IHC OpenVisual's own retention cap
  — whether its history is bounded at all, and at what. ⚠ The vendor comparison did **not** close either: it
  measured one‑action‑one‑step **granularity** (F‑045), which is a **different question**, and explicitly
  did **not** stress‑test multi‑level depth or redo‑invalidation. **Do not read F‑045 as resolving the
  depth.** ⚠ **Redo‑invalidation under depth was never stress‑tested on either app.** Owned by **C9** in
  `tmp\compare3.md` §5.

**Readiness:** Ready.
- [x] [R3] Undo/redo **depth** — **CLOSED 2026‑07‑19 (fablerefac D1 / W4‑4, `2785ee0`).** IHC OpenVisual's
  own retention is decided and shipped: an interim `Bounded(1000)` cap during the refactor, lifted to
  **`Unlimited`** once the keyed‑reconciliation history landed — **no configured cap, bounded only by process
  memory** (`HistoryPolicy.Unlimited` is the `ProjectDocumentSession` default). The **self** half is
  therefore resolved. The **vendor‑depth measure** (how many steps IHC Visual retains, and whether a second
  consecutive `Ctrl+Z` undoes a second step; C9, `tmp\compare3.md` §5) is **no longer a blocker** — with IHC
  OpenVisual unbounded it retains at least as many steps as the vendor by construction, so the measure is
  informational only. Granularity (one action = one step) was already measured and closed (F‑045).

**Implementation status:** 🟢 Implemented (multi-level, **unlimited depth**) — undo/redo history has **no
configured cap** (`HistoryPolicy.Unlimited`, fablerefac W4‑4); granularity is measured **aligned** with IHC
Visual (F‑045), and the **graceful‑degradation rule is verified good** (F‑065): the exact sequence that closes
IHC Visual — unlock a library block, then `Ctrl+Z` — runs cleanly here, re‑locking the block with the process
alive and responding. **IHC OpenVisual is strictly better than the vendor on this path**, not merely equal.
The depth `[R3]` is now **closed** (see above), leaving no outstanding blocker.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-052 | Undo and redo any edit | Ready | E14 | Must | -- |
