# ADR-001: UI-thread-affine threading model for Avalonia GUI applications

## Status

Decided — 2026-07-19.

Amended — 2026-08-02. The document-session bullet of the Decision is superseded: `ProjectDocumentSession` — now the
`IProjectDocument` port behind `ProjectAppService.OpenDocument` (crudarch redesign, decision D04) — is
**lock-serialized instead of thread-affine**. A private monitor serializes its member bodies (any thread may read
while one mutates) and `Changed`/`StateChanged` are raised outside the lock, synchronously on the thread that
performed the state change. The affinity guard blocked the port's sanctioned uses (headless test drivers and the
auto-backup timer's worker-thread snapshot read), so the safety condition became an explicit contract instead of a
fail-fast check: a GUI issues **all document mutations from the UI thread** (worker threads only read), and
`ConfigureAwait(false)` stays confined to paths that never mutate the document or handle its events. Accepted
trade-off: the fail-fast guard against off-UI-thread mutations is gone and Avalonia's downstream failure there is
partly silent, so the contract is pinned by dedicated cross-thread tests (`SessionThreadingContractTests`) — a
sanctioned exception to the no-multithreaded-tests policy. Everything else in this ADR — single-UI-thread ownership
of UI-bound state, explicit `Dispatcher.UIThread` marshalling, no ambient-context reliance, no locks in GUI code —
stands.

Revisit triggers: (a) upgrade to a new Avalonia major version — re-verify dispatcher and ambient-context defaults;
(b) any feature requiring concurrent mutation of document or UI-bound state (multi-document, collaborative editing,
background recomputation); (c) responsiveness budgets missed because of CPU-bound work on the UI thread.

## Decision at a glance

All Avalonia GUI applications in this repository use a single-UI-thread ownership model: UI-bound state is owned
and mutated only by the UI thread; background work exchanges immutable data and re-enters exclusively via explicit
`Dispatcher.UIThread` calls (never ambient context); GUI code takes no locks; state-owning components are
thread-affine with fail-fast wrong-thread guards.

## Context

**Current state** (2026-07-19; Avalonia 12.1.0, `net10.0`; apps: `ihc_openvisual`, `ihc_lab`):

- `ihc_openvisual` contains zero `Dispatcher` usage; correctness rests on the unstated invariant that everything
  touching view-models runs on the UI thread. Its only background thread — the auto-backup
  `System.Threading.Timer` (`Services/ProjectSession.cs:41,1669`) — never touches UI-bound state. Nothing guards
  the invariant (no `VerifyAccess` anywhere).
- `ihc_lab` has one background→UI path — streaming resource values — marshalled via `Dispatcher.UIThread.Post`
  (`Windows/MainWindow.axaml.cs:396`); the service layer documents but does not enforce delivery context
  (`ihcclient/src/app/services/labservice.cs:1324`).
- Canonical docs state only an async stance (`ARCHITECTURE.md`: `AsyncContinueOnCapturedContext` default `false`,
  "no analyzer enforces a context-capture policy"); no thread-affinity policy exists. No cross-thread violation
  exists today; nothing prevents one.
- Avalonia 12 behavior (verified against official docs and release notes, 2026-07-19): UI-bound INPC and
  collection updates are UI-thread-only and cross-thread changes can fail silently; v12 introduces multiple
  dispatchers, and `DispatcherTimer`/`AvaloniaSynchronizationContext` now bind to the *current* dispatcher by
  default (previously the UI thread).

**Decision forces**: owner ruling that full multithread support is overkill for a GUI; the Avalonia 12
ambient-default change makes implicit-context reliance fragile; the planned `ProjectDocumentSession`
(`tmp/fablerefac.md`, decision D12) needs the app-wide contract this ADR states; the test policy excludes
multithreaded tests.

**Reversibility**: one-way door — adopting is cheap, but reversal grows costly as session APIs, tests and
view-models accrete affinity assumptions.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| In-memory edits stay within responsiveness budgets on the UI thread | technical | medium | proposed budget (commit < 50 ms), measurement pending | budget misses attributable to UI-thread compute |
| Avalonia keeps the single-UI-thread binding model | environmental | high | Avalonia 12 docs: "multiple UI threads are currently still unsupported" | Avalonia major-release notes |
| Apps remain single-document, single-window editors | business | medium | `product.md` scope | concurrent-editing feature request |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| UI-bound state may only be mutated on the UI thread | technical | given (Avalonia) |
| `ihcclient` must not reference Avalonia | technical | chosen — standing repo invariant |
| No multithreaded tests | organizational | chosen — repo test policy |

## Evaluation Criteria

Priority order (highest first) — the order resolves the built-in conflict between correctness/failure-visibility
(which want guards and ceremony) and simplicity (which wants none):

1. **Correctness under Avalonia 12 threading rules** — no cross-thread UI mutation; robust against the v12
   ambient-context change.
2. **Simplicity & maintainability** — cognitive load of the rules in everyday GUI code.
3. **Testability** — verifiable in headless/unit suites without multithreaded tests.
4. **Failure visibility** — mistakes surface loudly at the point of error, not as silent UI corruption.
5. **Layer independence** — SDK stays Avalonia-free; the model must also serve console hosts.

## Options

### 1. UI-thread-affine ownership, explicit marshalling at the edges (chosen)

The UI thread owns all UI-bound state; background work is IO/compute over immutable data; the only re-entry is an
explicit `Dispatcher.UIThread.Post`/`InvokeAsync`; state owners fail fast on wrong-thread access; no locks in GUI
code.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Correctness (Avalonia 12) | 5/5 | Explicit `Dispatcher.UIThread` targeting is immune to the v12 current-dispatcher default; matches the documented model |
| Simplicity | 4/5 | One rule set, but ceremony at every background→UI boundary and guard code in state owners |
| Testability | 5/5 | Everything single-threaded; timers via injected `TimeProvider` + fake; guards assertable in plain unit tests |
| Failure visibility | 4/5 | Guarded owners throw on wrong-thread use; plain view-models still depend on Avalonia's own checks |
| Layer independence | 5/5 | Affinity guard is a framework-free thread-id compare; SDK unaffected |
| | **Total: 23/25** | **Trade-offs**: boundary ceremony everywhere; CPU-heavy features need deliberate background+snapshot restructuring |

### 2. Free-threaded shared state under synchronization

View-models/documents become thread-safe via locks or synchronized/reactive collection libraries; background
threads mutate state directly.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Correctness (Avalonia 12) | 3/5 | Achievable, but bound collections must still change on the UI thread (Avalonia has no `EnableCollectionSynchronization`), and lock + `Dispatcher.Invoke` is a classic deadlock pair |
| Simplicity | 1/5 | Lock ordering, reentrancy and event-under-lock discipline spread across all GUI code |
| Testability | 2/5 | Meaningful verification needs the concurrency tests the repo's policy excludes |
| Failure visibility | 2/5 | Races and deadlocks are intermittent, not fail-fast |
| Layer independence | 3/5 | Locks are framework-free, but UI-bound collections force dispatcher coupling inside view-models anyway |
| | **Total: 11/25** | **Trade-offs**: real parallelism for state mutation; highest complexity and defect surface |

### 3. Ambient-context reliance (status quo, unguarded)

Keep zero explicit dispatching: rely on `SynchronizationContext` capture to keep continuations on the UI thread;
discipline by convention.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Correctness (Avalonia 12) | 3/5 | Works today (inventory found zero violations), but one background caller corrupts silently, and v12 binds ambient context/timers to the current dispatcher — a live trap off-thread |
| Simplicity | 5/5 | Zero ceremony — nothing to write, nothing to review |
| Testability | 4/5 | Single-threaded, but the invariant is unstated, so nothing can assert it |
| Failure visibility | 1/5 | Avalonia documents silent drops for cross-thread collection changes |
| Layer independence | 4/5 | Nothing explicit anywhere, but state owners stay unguarded for non-GUI hosts too |
| | **Total: 17/25** | **Trade-offs**: lowest friction now; highest latent-defect risk as the apps grow |

## Decision

Adopt option 1 for every Avalonia GUI application in this repository (`ihc_openvisual`, `ihc_lab`, future GUIs):

- The UI thread owns all UI-bound state — view-models, observable collections, document sessions. Mutation happens
  only there. GUI/view-model code takes no locks.
- Background work is IO or computation over immutable inputs and outputs (snapshots in, results out); it never
  touches UI-bound state.
- Re-entry is explicit: background code targets `Dispatcher.UIThread` (`Post`/`InvokeAsync`). Background code never
  relies on ambient `SynchronizationContext` and never creates dispatcher-bound objects (e.g. timers) off the UI
  thread — Avalonia 12 binds those to the current dispatcher.
- GUI/app-layer async keeps default context capture; `ConfigureAwait(false)` is reserved for code that provably
  never touches UI-bound state afterwards (SDK internals; isolated pipelines such as the backup writer).
- State-owning non-UI components (document sessions and peers) are thread-affine with fail-fast wrong-thread
  guards, implemented framework-free so the SDK remains Avalonia-independent; their change events fire
  synchronously on the owning thread, so UI subscribers never marshal. *(Superseded 2026-08-02 — see the
  amendment in Status: document sessions are lock-serialized; events still fire synchronously on the mutating
  thread, and the GUI still mutates only from the UI thread, so UI subscribers still never marshal.)*
- Recurring background triggers use an injected `TimeProvider`; the callback's only act on arrival is the UI-thread
  post.
- Fire-and-forget is limited to self-contained background tasks with internal fault handling (startup telemetry
  probe, backup tick); user-facing flows await their tasks.

Confidence: high — grounded in verified Avalonia 12 documentation, a code inventory showing both apps already fit
the model, and an explicit owner ruling. Top uncertainty: a future CPU-bound feature could strain single-thread
ownership; the designed answer (background compute over an immutable snapshot, posted result) is unproven at scale
in these apps.

## Implications

### Positive

- Data races and lock/dispatcher deadlocks are excluded by construction (long-term, cross-cutting).
- Tests stay single-threaded, consistent with repo test policy; timers become deterministic via fake time.
- Immune to the Avalonia 12 ambient-default change; aligned with official guidance.
- Wrong-thread use of guarded state owners surfaces as an immediate exception instead of silent UI corruption.

### Negative

- CPU-heavy features will freeze the UI unless deliberately restructured as background+snapshot work — recurring
  design friction (short-term, per feature; reversible case by case).
- Explicit marshalling ceremony at every background→UI boundary.
- No analyzer enforces the policy; compliance is review-dependent until a fitness check exists — drift risk
  (cross-cutting).
- Parallel mutation of document/UI state is off the table while this ADR stands (long-term; costly to reverse once
  session APIs and tests assume affinity).

### Neutral

- Both apps already conform (ihc_lab's marshalled stream path; openvisual's walled-off backup timer); this ADR
  requires no immediate code change.
- Existing `ConfigureAwait(false)` uses inside isolated non-UI pipelines remain valid.

## Confirmation

- Code-review checklist: background→UI only via explicit `Dispatcher.UIThread`; no locks in GUI/view-model code; no
  ambient-context reliance or dispatcher-bound object creation off the UI thread.
- Architecture fitness test (ArchUnitNET, already a repo dependency): `ihcclient` must not reference Avalonia.
- Unit tests: wrong-thread access to guarded state owners throws; headless suites exercise the UI-thread pipeline.

## Consultation

Owner (sole maintainer) ruled 2026-07-19 that full multithread support is overkill for the GUI and only basic API
safety is warranted; chose repo-level `docs/adr/`, all-apps scope, and Decided status via in-session Q&A. Avalonia
12 behavior was verified against official documentation the same day. No other stakeholders.
