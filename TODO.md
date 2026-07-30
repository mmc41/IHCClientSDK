explore feasability but do not implement follow: can report generation
  in openvisual be made modeldriven using reflection on the supplied data  
  models by the ihcclient API (possibly extended with attribute metadata),
  so report content is not hardcoded in openvisual but derived ?

  → explored 2026-07-21, analysis: [tmp/metadrivenreport-ana.md](tmp/metadrivenreport-ana.md)

- [ ] **Consider (idea):** adopt model-driven report rendering per the analysis's option B — the
      combined report model (backlog T020) emits a generic shape document
      (Table/KeyValue/Outline sections with US-071 option tags) and the GUI becomes a small
      shape interpreter; reflection/attributes stay SDK-internal if used at all. Decide as an
      amendment to T020 **before** the reporting phases (Phase 4+) of
      `tmp/programming-reporting-backlog.md` start — do not retrofit the current three report
      models. Full feasibility analysis: [tmp/metadrivenreport-ana.md](tmp/metadrivenreport-ana.md).

----

# TODO — after the programming/reporting backlog loop completes

## 1. Rule on the two US-068 residuals (owner decision — input comes from T018)

T018 records the *current* log-mark / stop-point / jump-to behaviour in the backlog's Discoveries
before setting US-068 to blocked. When the loop is done:

- [ ] Read T018's Discoveries entry.
- [ ] Decide the **log-mark scope**: offered on every product pin, or only on `Logning`-bearing rows.
- [ ] Decide the **stop-point / jump-to leaf routes**: build, drop (won't-do), or re-spec.
      (If they turn out simulation-adjacent, note that E8/simulation is out of scope.)
- [ ] Promote a small follow-up task implementing the rulings; refresh US-068's
      `Implementation status:` line from **blocked** to its real state
      (`applications/ihc_openvisual/docs/stories/11-interaction-model.md`).

Both decisions were deliberately deferred (ruling 2026-07-21) until T018's evidence exists — do not
decide earlier.

## 2. PG-5 enum-editing oracle session (route approved 2026-07-21; plan not yet authored)

Goal: let US-030 gain enum state **reorder**, state **remove**, and type **rename** — currently out of
scope (D05) because the value-id reallocation semantics are unknown.

- [ ] Author the capture plan (separate elevated ihcvisual-MCP session, config-mode): vendor enum
      dialog before/after `.vis` byte pairs for each of the three operations (the Win32 recipe for
      dialog `24588` exists from the enumvalues Gap3 session; oracle naming/registration follows the
      `project4-PrgTokens*` pattern).
- [ ] Run it; establish the value-id reallocation rule and what happens to referencing program rows /
      case branches / inline enum constants.
- [ ] Promote engine + UI tasks on that evidence (D05 lifts only then); update US-030.

## 3. Standing constraints — do not reopen without new evidence

- **Float-target ÷ is unauthorable** (F-107; the P7 manual rung was waived 2026-07-21). US-032 is
  final: division targets integers only. Reopen only if a new token source appears.
- **Dead popup entries are never offered** (F-106/F-109): float+float `+` · int−int and int←float `−` ·
  counter two-operand `−` · int×int `×` · the 2-operand `Timer ->` event · the `Timer <` condition
  (authors express "less than" by swapping the operands of `>`).
- **Never invent method tokens** (D09). The token oracle is `tmp/prgmode/out/method-map.md` (e2 +
  progmode3 rows) attested by `tests/testdata/projects/project4-PrgTokens.vis` and
  `…-round2.vis`.
- The F-096 vendor quirk (`= Timer +` greyed until a Timertid pin exists) must **not** be copied.
