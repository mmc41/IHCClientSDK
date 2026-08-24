# ADR-001: Threading and concurrency model

## Status

Decided — 2026-07-19. Amended 2026-08-02 (document session lock-serialized, not thread-affine) and 2026-08-24
(wrong-thread guards retired repo-wide; blocking, off-thread `AvaloniaObject` and cancellation rules added; scope
broadened to threading and concurrency generally, file renamed). Superseded text is annotated in place. Neither
amendment changed a rule's meaning for code already written; the only code either one required is the document
session's `object` → `System.Threading.Lock` swap, recorded under Neutral.

Revisit triggers: (a) upgrade to a new Avalonia major version — re-verify dispatcher and ambient-context defaults;
(b) any feature requiring concurrent mutation of document or UI-bound state (multi-document, collaborative editing,
background recomputation); (c) responsiveness budgets missed because of CPU-bound work on the UI thread;
(d) a second in-process frontend needing the same background-recompute loop — the point at which the host contract
below should be extracted into shared code rather than copied.

## Decision at a glance

The repository is **single-threaded by default and concurrent by exception**, in two halves.

*Ownership.* All Avalonia GUI applications use a single-UI-thread ownership model: UI-bound state is owned
and mutated only by the UI thread; background work exchanges immutable data and re-enters exclusively via explicit
`Dispatcher.UIThread` calls (never ambient context); GUI code declares no lock of its own and never blocks on work
scheduled to the UI thread; a state-owning component is confined to the UI thread by contract — pinned by tests, not
by a runtime guard — or lock-serialized where a worker thread must read it, in which case a UI-thread property read
does enter that lock.

*Background work.* SDK compute over an immutable snapshot is synchronous and thread-agnostic — it offloads nothing
on its caller's behalf. A host that must not block captures the snapshot on the UI thread, runs the compute on the
thread pool, and binds the result back after a same-thread `await`. A result whose document version has moved is
discarded, not merged.

## Context

**Current state** (2026-07-19; Avalonia 12.1.0 at the time of the decision, 12.1.1 since, `net10.0`; apps:
`ihc_openvisual`, `ihc_lab`):

- `ihc_openvisual` contains zero `Dispatcher` usage; correctness rests on the unstated invariant that everything
  touching view-models runs on the UI thread. Its only background thread — the auto-backup
  `System.Threading.Timer` (`Services/ProjectSession.cs:41,1669`) — never touches UI-bound state. Nothing guards
  the invariant (no `VerifyAccess` anywhere). *(Both halves have since moved, and neither move affects the
  decision: the auto-backup feature was removed along with that file and its timer, and the app now has two
  `Dispatcher` call sites — the UI-thread exception net in `Program.cs` and one same-thread deferral in
  `Views/MainWindow.axaml.cs`. See Neutral for the 2026-08-24 re-check.)*
- `ihc_lab` has one background→UI path — streaming resource values — marshalled via `Dispatcher.UIThread.Post`
  (`utilities/ihc_lab/Windows/MainWindow.axaml.cs`); the service layer documents but does not enforce delivery
  context (`ihcclient/src/app/services/labservice.cs`, on `IAsyncEnumerable` delivery: "items are delivered on the
  enumerating context; a GUI caller should marshal to the UI thread"). Both are named by file rather than by line,
  which drifts.
- Canonical docs state only an async stance (`ARCHITECTURE.md`: `AsyncContinueOnCapturedContext` default `false`,
  "no analyzer enforces a context-capture policy"); no thread-affinity policy exists. No cross-thread violation
  exists today; nothing prevents one.
- Avalonia 12 behavior (verified against official docs and release notes, 2026-07-19; re-verified 2026-08-24
  against the Avalonia source at tag 12.1.1): UI-bound INPC and collection updates are UI-thread-only, and the two
  fail **differently** — an off-thread `PropertyChanged` for a bound property raises `InvalidOperationException`
  through the binding layer, so it surfaces at the binding rather than at the assignment, while an off-thread
  mutation of a bound collection is silent: items "may be silently dropped or only partially added". Off-thread
  construction of an `AvaloniaObject` is silent too — the object captures `Dispatcher.CurrentDispatcher`, which
  *creates* a dispatcher on a thread that has none, so its own `VerifyAccess()` passes there and the failure
  surfaces only later, when attach reaches `Dispatcher.UIThread.VerifyAccess()`. v12 introduces multiple
  dispatchers, and `DispatcherTimer`/`AvaloniaSynchronizationContext` now bind to the *current* dispatcher by
  default (previously the UI thread).
- *(Added 2026-08-24 with the broadening.)* **The SDK is already thread-agnostic where it computes, and this is
  tested rather than assumed.** `WholeProjectValidator`, its rule set and the problem catalogue are built once and
  hold no per-run state — every run's state lives in locals — so one of each is shared for a process lifetime;
  `EngineConcurrencyTests` runs 16 threads over a single executor and asserts identical ordered results. Every
  `Task`-returning door on `ProjectAppService` is **I/O-shaped, not a CPU offload**: `GenerateReport` runs
  `ReportGenerator.Generate` on the calling thread and awaits only the write. There is no `Task.Run` anywhere under
  `ihcclient/src/`, and no `CancellationToken` in `ihcclient/src/vis/` or on `ProjectAppService`.

**Decision forces**: owner ruling that full multithread support is overkill for a GUI; the Avalonia 12
ambient-default change makes implicit-context reliance fragile; the planned `ProjectDocumentSession`
(working note `fablerefac`, decision D12, since superseded by `crudarch` D04 — neither note is tracked in this
repository, and the rule that survived is restated where it binds, in `ProjectDocumentSession`'s remarks) needs the
app-wide contract this ADR states; the test policy excludes multithreaded tests.

**Reversibility**: one-way door — adopting is cheap, but reversal grows costly as session APIs, tests and
view-models accrete affinity assumptions.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| In-memory edits and whole-project compute stay within responsiveness budgets on the UI thread | technical | medium | proposed budget (commit < 50 ms). `PerfBaselineBenchmark` is the instrument: it measures the commit path, the drag-over probe, per-command `CanApply`, and `ValidateCategorized` over the 17-document characterization corpus, reporting median and p95. By design (va-backlog D22) it **records numbers without asserting them**, so nothing gates a gradual creep — reading it is a human act | a measured median or p95 above the budget for an operation a UI gesture awaits |
| Avalonia keeps the single-UI-thread binding model | environmental | high | Avalonia 12 docs: "multiple UI threads are currently still unsupported" | Avalonia major-release notes |
| Apps remain single-document, single-window editors | business | medium | `product.md` scope | concurrent-editing feature request |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| UI-bound state may only be mutated on the UI thread | technical | given (Avalonia) |
| `ihcclient` must not reference Avalonia | technical | chosen — standing repo invariant |
| No multithreaded tests, save for named exceptions | organizational | chosen — repo test policy. Two exist: `SessionThreadingContractTests` (the document's cross-thread contract) and `EngineConcurrencyTests` (safe concurrent reuse of one executor). Each is the deliverable of a decision, not incidental coverage |

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

These three are the **ownership** decision — who may touch UI-bound state, and how. The separate question of who
offloads long compute is compared in its own table under [Background processing](#background-processing), because it
was decided later, on different grounds, and against a code base that by then already answered part of it.

### 1. UI-thread-affine ownership, explicit marshalling at the edges (chosen)

The UI thread owns all UI-bound state; background work is IO/compute over immutable data; the only re-entry is an
explicit `Dispatcher.UIThread.Post`/`InvokeAsync`; state owners fail fast on wrong-thread access; no locks in GUI
code.

This table is the evaluation **as scored on 2026-07-19**, when the option still included a fail-fast wrong-thread
guard in state owners. That guard was later retired (Status, 2026-08-02 and 2026-08-24), so every mention of one
below is historical. Only Failure visibility changes score as a result; the rows are annotated rather than
rewritten, so the decision record stays readable.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Correctness (Avalonia 12) | 5/5 | Explicit `Dispatcher.UIThread` targeting is immune to the v12 current-dispatcher default; matches the documented model |
| Simplicity | 4/5 | One rule set, but ceremony at every background→UI boundary and guard code in state owners |
| Testability | 5/5 | Everything single-threaded; timers via injected `TimeProvider` + fake; guards assertable in plain unit tests |
| Failure visibility | 4/5 | Guarded owners throw on wrong-thread use; plain view-models still depend on Avalonia's own checks. *(Amended 2026-08-24: no guard exists any more, so this rests on Avalonia's own checks alone, which throw for off-thread INPC but stay silent for bound-collection mutation and `AvaloniaObject` construction. Scored today the row is 3/5, total 22/25, which does not change the ranking.)* |
| Layer independence | 5/5 | Affinity guard is a framework-free thread-id compare; SDK unaffected. *(Amended 2026-08-24: with the guard gone this rests on the SDK simply holding no Avalonia reference, which is arch-enforced — score unaffected.)* |
| | **Total: 23/25** | **Trade-offs**: boundary ceremony everywhere; CPU-heavy features need deliberate background+snapshot restructuring |

### 2. Free-threaded shared state under synchronization

View-models/documents become thread-safe via locks or synchronized/reactive collection libraries; background
threads mutate state directly.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Correctness (Avalonia 12) | 3/5 | Achievable, but bound collections must still change on the UI thread — Avalonia has no confirmed equivalent of WPF's `BindingOperations.EnableCollectionSynchronization`, an absence established from a maintainer answer plus silence in the v12 docs rather than an explicit statement, so treat it as likely-but-unconfirmed — and a worker holding a lock the UI thread needs while calling `Dispatcher.Invoke` is a classic deadlock pair |
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

Adopt option 1 for every Avalonia GUI application in this repository (`ihc_openvisual`, `ihc_lab`, future GUIs).
The bullets below are the ownership rules; [Background processing](#background-processing) then states the SDK's
own threading contract and the model for work that must leave the UI thread.

- The UI thread owns all UI-bound state — view-models, observable collections, document sessions. Mutation happens
  only there. GUI/view-model code declares no lock of its own. It is not lock-free, though: every read of a
  document-session property enters that session's lock, which is why those bodies are bounded as they are — see
  Implications.
- Background work is IO or computation over immutable inputs and outputs (snapshots in, results out); it never
  touches UI-bound state.
- Re-entry is explicit: background code targets `Dispatcher.UIThread` — `Post` for fire-and-forget, `InvokeAsync`
  when the result or the completion matters — and gives deferrable updates an explicit low `DispatcherPriority`
  (`Background`, `ContextIdle`) so they do not compete with input and render. A post from the UI thread back to
  itself is a different move and is permitted: deferring work past a handler that Avalonia has not finished with
  yet is a re-entrancy fix, not a marshal, and it keeps the priority the surrounding interaction runs at rather than
  dropping to a low one. Do not reason about relative priority from the Avalonia docs' priority table, which is
  misordered: the enum is authoritative, and `Default` sits *below*
  `Render`. `Dispatcher.UIThread` is the right target for application code and is not deprecated; reusable control
  code, should any ship inside these assemblies, dispatches through `AvaloniaObject.Dispatcher` instead so it works
  under any dispatcher.
- Background code never relies on ambient `SynchronizationContext`, and never constructs an `AvaloniaObject` off the
  UI thread — control, geometry, `DispatcherTimer`, `AvaloniaSynchronizationContext` alike. Avalonia 12 binds each
  to the constructing thread's dispatcher, creating one where the thread had none, so the mistake compiles, runs and
  passes that object's own access check; it surfaces only at attach, or not at all. Where a dispatcher-bound object
  must be made elsewhere, pass the target `Dispatcher` to its constructor.
- The UI thread never blocks on work scheduled to it: no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on any
  UI-reachable path. Under v12 the consequence depends on the overload — `InvokeAsync(Action).Wait()` pumps the
  dispatcher and completes, trading deadlock for re-entrancy, while `InvokeAsync(Func<Task>).Wait()` and every
  `.Result` still deadlock outright — and the two are indistinguishable at the call site, so the rule is blanket.
- Long background operations take a `CancellationToken` and honour it, so a command the user has abandoned stops
  costing CPU and cannot bind a stale result back into the UI.
- GUI/app-layer async keeps default context capture; `ConfigureAwait(false)` is reserved for code that provably
  never touches UI-bound state afterwards (SDK internals; isolated non-UI pipelines). *(Amended 2026-08-24: the
  auto-backup writer was this bullet's standing example and no longer exists. With it went the scan's only
  allowlist entry, so the GUI ban is now absolute and a new exemption is a decision to take deliberately rather
  than a slot to fill.)*
- State-owning non-UI components (document sessions and peers) are thread-affine with fail-fast wrong-thread
  guards, implemented framework-free so the SDK remains Avalonia-independent; their change events fire
  synchronously on the owning thread, so UI subscribers never marshal. *(Superseded 2026-08-02, widened 2026-08-24.
  No fail-fast guard exists anywhere in the repository. A state owner a worker thread
  must read is lock-serialized (`ProjectDocumentSession`, a private `System.Threading.Lock`); every other one is
  confined to the UI thread by contract, pinned by tests rather than by a runtime check. Events still fire
  synchronously on the mutating thread and the GUI still mutates only from the UI thread, so UI subscribers still
  never marshal. A lock a state owner does take stays short, holds no dispatcher call, and raises no event while
  held.)*
- Recurring background triggers use an injected `TimeProvider`; the callback's only act on arrival is the UI-thread
  post.
- Fire-and-forget is limited to self-contained background tasks with internal fault handling (the startup
  telemetry probe; the auto-backup tick that was this bullet's second example went with its feature); user-facing
  flows await their tasks. The view layer's `async void` event handlers are the same class under another name and
  are contained the same way — see the exception-net paragraph under [Background processing](#background-processing).

Confidence: high — grounded in verified Avalonia 12 documentation, a code inventory showing both apps already fit
the model, and an explicit owner ruling. Top uncertainty: a future CPU-bound feature could strain single-thread
ownership; the designed answer (background compute over an immutable snapshot, posted result) is unproven at scale
in these apps.

### Background processing

*(Added 2026-08-24 with the broadening.)* The rules above say where UI-bound state lives. This says how work too
slow for the UI thread is run. It is general: it binds any long compute over an immutable snapshot, not one
operation.

This opens a door without walking through it. Nothing here schedules any work: FR-8.1 specifies validation *"on
demand and before save/transfer"*, no code under `applications/` calls `Validate` or `ValidateCategorized` today,
and there is no findings pane. A continuously recomputed view is a host schedule over an existing door — permitted
by this section, not decided by it.

**The SDK contract.** An SDK compute door is synchronous and thread-agnostic. Given an immutable snapshot it holds
no per-run state, so any thread may call it and several may call it at once; it never spends a thread-pool thread on
its caller's behalf. This is a contract, not an accident of the current code, and it is pinned by
`EngineConcurrencyTests` rather than assumed. Its limit is equally deliberate: nothing here makes an **edit session**
concurrent. Serializing edits remains the document's job, and a GUI still issues every mutation from the UI thread.

**Who offloads.** The host does — it is the party that knows it has a UI thread to protect. The SDK is host-agnostic
and serves console tools and test suites that want the synchronous call. Three arrangements were considered:

| Arrangement | Verdict |
| --- | --- |
| **Host offloads; the SDK stays synchronous** (chosen) | Changes no code, and the SDK contract it stands on is already tested. Accepted cost: the scheduling policy is not shared code, so a second frontend would copy it — which is why it is written below as a numbered contract instead of left to each caller. |
| The facade grows `…Async(snapshot, CancellationToken)` doors | Rejected on three counts. A library wrapping synchronous CPU work in `Task.Run` spends the caller's thread-pool thread unasked. Every existing `Task`-returning door on the facade is I/O-shaped, so `Async` would come to mean two different things on one type, across a shipped public-API baseline. And the token would be a public promise the engine cannot keep mid-run. It also fails to take the hard part: coalescing, version keying and binding stay with the host either way. |
| A shared latest-wins scheduler component | **Deferred, not rejected** — the right extraction once a second in-process frontend needs the same loop, which is revisit trigger (d). Shipping it for one shell is infrastructure ahead of its consumer. If it is built it belongs in `ihcclient`; `shared/ihc_appbootstrap` references Avalonia by design and is not a framework-free shelf. |

**The host contract.** A background recompute follows five steps. They are numbered because each is a distinct
failure mode, because a second frontend should copy a specification rather than rediscover them, and because a later
extraction into shared code should implement exactly this list.

1. **Capture on the UI thread, once.** Read the snapshot *and* the document version into locals before starting.
   Re-reading `Current` inside the worker can mix document versions: every read is committed and untorn, but two
   reads need not return the same one. That hazard applies to this step's own two reads — `IProjectDocument` exposes
   `Current` and `Version` separately and offers no combined accessor, so the pair is atomic only because the
   capture is on the UI thread and every mutation is issued from it. Off the UI thread the session permits the read
   but not the assumption; capture there and the two values can already disagree.
2. **Run pure compute on the pool** — `await Task.Run(() => …, ct)`. The background path mutates nothing, touches no
   UI-bound state, constructs no `AvaloniaObject`, and returns only immutable data.
3. **Discard a superseded result.** Compare the captured version against the document's current version before
   binding. Latest-wins: a run whose document has moved on is thrown away, never merged into a newer state. This is
   the read-side twin of a check the session already makes on the write side — `Apply(command, baseVersion)`
   refuses with `EditRefusalCodes.StaleBaseVersion` when the document has moved under a pending edit. Same
   staleness question, opposite direction; a host doing both should use the two together rather than invent a third
   scheme.
4. **Bind on the UI thread.** `ConfigureAwait` is banned outright in `ihc_openvisual`
   (`Gui_DoesNotCallConfigureAwait`, which scans that assembly and no other), so there a plain `await` resumes
   correctly; a raw worker thread posts via `Dispatcher.UIThread.Post` at an explicit low `DispatcherPriority`. A
   second frontend copying these steps inherits the rule but not the scan — extend the scan to it, or this step is
   unenforced there.
5. **Honour the token at both boundaries** — pass it to `Task.Run` so an abandoned run never starts, and re-check it
   after the `await` so a completed-but-unwanted result is not bound.

**Cancellation is coarse, and this ADR says so rather than implying otherwise.** A synchronous engine call cannot be
interrupted mid-run, so a token is honoured only *between* runs under any of the three arrangements above.
Latest-wins bounds the waste at one in-flight run. Finer cancellation is an increment to buy when measurement
justifies it, cheapest first: a check between rules inside the executor's existing loop (one file), and only then a
token threaded into the rule bodies (the two dozen `*Rules.cs` files plus the inspection surface). Never publish a
`CancellationToken` on a door that ignores it. Should a rule ever need to *await* rather than merely be cancelled,
that is the recorded validation-library adoption trigger — flip condition (b) on `RuleBuilder`, restated in
`ARCHITECTURE.md` — a different decision, not something to ease into with `Task.Run` wrappers.

**Progress** is reported through `IProgress<T>`, which keeps view-models free of Avalonia types; a `Progress<T>`
constructed on the UI thread invokes its callback there. Avalonia's `^` stream-binding operator is permitted but not
required, with one caveat if used: it surfaces neither faults nor cancellation, so a `^`-bound property needs a
separate error-state property beside it.

**Fire-and-forget background work observes its own exceptions.** The existing limit stands — self-contained tasks
with internal fault handling. A host that adds any also registers the layered nets Avalonia documents:
`Dispatcher.UIThread.UnhandledException` for the UI thread, `TaskScheduler.UnobservedTaskException` (calling
`SetObserved()`) for orphaned pool work, and `AppDomain.CurrentDomain.UnhandledException` for other background
threads. This binds new fire-and-forget code; it requires no change to what exists.

Those three nets do not cover the shape the view layer actually produces most of. An `async void` event handler has
no caller to catch it, and a **window-lifecycle** handler (`Closing`, `Closed`, `Activated`) runs straight off the
window message loop, where neither the dispatcher net nor the `AppDomain` net can see the fault at all — the app
dies with no record. So `async void` handlers are contained at the source rather than by a net: openvisual routes
each through `Views/HandlerGuard.RunAsync`, which logs the fault and *returns* it so a handler with a sensible
reaction (cancel the quit, drop a drag highlight) can take one, and handlers with a view-model in reach use that
view-model's own error boundary instead, which additionally reports to the user. A host writing `async void`
handlers needs the equivalent; the three nets alone are not enough.

#### Worked example

Validation is an illustration of the contract, not a decision about validation — see the scoping note above. A
whole-project run (`ProjectAppService.ValidateCategorized`) has the canonical shape: CPU work over an immutable
`Project` returning an immutable `ProjectValidationResult`.

```csharp
// On the UI thread. Current and Version are two separate locked reads; they agree here only because every
// mutation is issued from this thread. Captured off it, the two can already disagree.
Project snapshot = document.Current!;                                    // 1 — snapshot AND version, once
int version = document.Version;

ProjectValidationResult result = await Task.Run(() => app.ValidateCategorized(snapshot), ct);  // 2

// Resumed on the UI thread: ConfigureAwait is banned in this assembly, so the plain await came back here.
if (ct.IsCancellationRequested) return;                                  // 5 — abandoned: bind nothing
if (version != document.Version) return;                                 // 3 — superseded: discard, never merge
foreach (ProjectValidationFinding f in result.Findings) findings.Add(f); // 4 — bind on the UI thread
```

The two abandon paths are deliberately the same shape. `ct.ThrowIfCancellationRequested()` would read as the more
idiomatic step 5, but it turns "the user moved on" into an `OperationCanceledException` thrown out of a UI handler,
which then needs one of the nets above to swallow it; a plain `return` beside step 3's makes both outcomes a quiet
no-op, which is what they are.

Each step fails differently if dropped, and two of the failures are silent: binding a superseded result corrupts the
display with no exception, and mutating `findings` from a pool thread loses items with no exception either. Only an
off-thread `PropertyChanged` throws.

## Implications

### Positive

- Data races are excluded by confinement and immutability (long-term, cross-cutting).
- Lock/dispatcher deadlocks are excluded by contract plus one short lock rather than purely by construction. The GUI
  takes no locks of its own, but every UI-thread read of a document-session property enters that session's lock, so
  the UI thread can in principle block behind a worker's snapshot read. What keeps the pair safe is that the lock
  bodies are short, make no dispatcher call, and raise no event while held. *(Restated 2026-08-24; the original
  bullet claimed exclusion by construction, which the 2026-08-02 lock-serialization amendment had already
  narrowed.)*
- Tests stay single-threaded, consistent with repo test policy; timers become deterministic via fake time.
- Immune to the Avalonia 12 ambient-default change; aligned with official guidance.
- Off-thread `PropertyChanged` for a bound property surfaces as an `InvalidOperationException` from the binding
  layer rather than as silent corruption. *(Restated 2026-08-24: this is Avalonia's own check. The original bullet
  credited a fail-fast guard of ours, which no longer exists — see the matching Negative for what stays silent.)*
- Background work has one named shape rather than a per-feature invention, and the SDK contract it stands on is
  test-pinned rather than assumed. The SDK keeps one signature per operation, so console tools and the test corpus
  are unaffected by a GUI's decision to offload. *(Added 2026-08-24 with the broadening.)*

### Negative

- CPU-heavy features must be deliberately restructured as background+snapshot work or they freeze the UI — recurring
  design friction (short-term, per feature; reversible case by case). The five-step contract removes the *design*
  cost of that restructuring but not the work itself.
- **The background scheduling policy is written, not shared code**, so a second in-process frontend would copy five
  steps by hand — and two of the steps guard silent failures, which is exactly the duplication class the thick-SDK
  split exists to prevent. This is the accepted cost of not building a component ahead of its second consumer;
  revisit trigger (d) is the flip. *(Added 2026-08-24 with the broadening.)*
- Explicit marshalling ceremony at every background→UI boundary.
- **Two of the three off-thread mistakes are silent and nothing fails fast on them**: mutating a bound collection,
  and constructing an `AvaloniaObject`. Only off-thread INPC throws. Since the retirement of the wrong-thread guard
  (Status, 2026-08-02, widened 2026-08-24) the repository has no runtime net at all for the silent pair, and no
  static one either: the sole IL scan bans `ConfigureAwait`, which is upstream of the mistake rather than the
  mistake itself. What remains is review, plus `SessionThreadingContractTests` for the document contract — which
  is why the code-review checklist below is written out in full.
- No analyzer enforces the policy, and the IL fitness scan standing in for one covers **a single rule**: the
  `ConfigureAwait` ban in `OpenVisualThreadingArchitectureTests.cs` (a partial of `OpenVisualArchitectureTests`,
  added after this ADR), whose only companion is `ConfigureAwaitScan_IsArmed`, its own positive control. Every
  other rule above — no GUI lock, no blocking call, no off-thread `AvaloniaObject`, a token on every long
  operation — is carried by review alone. The analyzer that would cover this ground asks for the opposite — see
  the next bullet.
- **This decision blocks enabling CA2007 anywhere a UI thread is in play, and that is most of the repository.**
  CA2007 ("Consider calling `ConfigureAwait` on the awaited task") demands at every `await` exactly the call this
  ADR forbids in GUI code, so with warnings-as-errors the two are jointly unsatisfiable: no source text passes both
  the analyzer and `Gui_DoesNotCallConfigureAwait`, which is a blanket, exemption-free scan by member name.
  Re-measured 2026-08-24 — occurrences of the `await ` token in `.cs` sources outside `generatedsrc/`, comment
  lines excluded, which is the count this bullet's earlier figures failed to state a method for and which could not
  be reproduced from them. **2,669 `await` sites**, of which **1,631 sit in the four Avalonia assemblies**
  (`ihc_openvisual`, `ihc_lab`, and the headless `safe_visual_tests` / `safe_lab_tests`, which share the same
  dispatcher affinity) and a further 754 in test suites where the rule carries no signal — 2,385 together, or 89%.
  Of the 284 sites left, 260 are in `ihcclient`, which is exactly where CA2007 **is** enabled, in
  `ihcclient/analyser.config`, because a library genuinely must not resume on its caller's context. Enabling it
  repo-wide would therefore bind 24 further sites and collide with 1,631. The same reasoning rules out the category
  mode that would pull CA2007 in — `AnalysisModeReliability=All` — so the repo-wide file opts into rules
  individually. A future proposal to raise either meets this ADR first.
- Parallel mutation of document/UI state is off the table while this ADR stands (long-term; costly to reverse once
  session APIs and tests assume affinity).

### Neutral

- Both apps already conform (ihc_lab's marshalled stream path; openvisual's UI-thread-confined view layer); this
  ADR required no immediate code change. *(Re-checked 2026-08-24 across **both** GUI apps — `applications/ihc_openvisual`
  and `utilities/ihc_lab`, which does not live under `applications/` and which an earlier re-check therefore missed
  — against the rules the amendment added: no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` in either, no
  `DispatcherTimer` or `AvaloniaSynchronizationContext` construction site, and no `Task.Run` background operation
  yet — so the cancellation rule binds the first one written, not existing code. The one code change either
  amendment required is the document session's `object` → `System.Threading.Lock` swap. Openvisual's cited
  background thread, the auto-backup timer, has since been removed with its feature.)*
- Existing `ConfigureAwait(false)` uses inside isolated non-UI pipelines remain valid.

## Confirmation

- Code-review checklist: background→UI only via explicit `Dispatcher.UIThread`, at an explicit low priority for
  deferrable work; no lock declared in GUI/view-model code; no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`
  on a UI-reachable path; no ambient-context reliance; no `AvaloniaObject` — control, geometry or timer — constructed off
  the UI thread; a `CancellationToken` on every long background operation.
- Code-review checklist for a background recompute, one line per step of the host contract: snapshot **and** version
  captured once on the UI thread; nothing UI-bound touched in the worker; the version re-checked before binding; the
  bind on the UI thread; the token passed to `Task.Run` *and* re-checked after the `await`. A missing step 3 or 4 is
  silent at runtime, so it must be caught here.
- Architecture fitness tests: `ihcclient` must not reference Avalonia (ArchUnitNET, already a repo dependency);
  `Gui_DoesNotCallConfigureAwait`, in `OpenVisualThreadingArchitectureTests.cs` (a partial of
  `OpenVisualArchitectureTests`), scans `ihc_openvisual`'s IL for the `ConfigureAwait` ban, with
  `ConfigureAwaitScan_IsArmed` as its positive control. That is the **only** automated threading rule — everything
  else in the checklist above is review-carried, which is why the checklist is written out rather than assumed.
- Tests: `SessionThreadingContractTests` pins the document session's cross-thread contract — the substitute for the
  retired runtime guard, and a sanctioned exception to the no-multithreaded-tests policy; `EngineConcurrencyTests`
  pins the SDK-side contract that makes off-UI-thread compute legal at all, asserting both identical ordered results
  across 16 threads over one executor and the structural absence of mutators; headless suites exercise the UI-thread
  pipeline. A host's accept/reject policy (given a result, a captured version and a current version — bind or
  discard) is a pure function and is tested single-threaded, adding no third exception.
- Measurement, not assertion: `PerfBaselineBenchmark` reports the median and p95 of `ValidateCategorized` and of the
  interactive paths. It gates nothing by design, so a decision to move an operation into the background should cite
  a reading from it rather than an estimate.

## Consultation

Owner (sole maintainer) ruled 2026-07-19 that full multithread support is overkill for the GUI and only basic API
safety is warranted; chose repo-level `docs/adr/`, all-apps scope, and Decided status via in-session Q&A. Avalonia
12 behavior was verified against official documentation the same day. No other stakeholders.

The 2026-08-24 broadening kept that ruling intact — single-threaded by default is unchanged; what was added is the
sanctioned exception and its bounds. The offload-ownership question was put to three independent analyses before it
was settled. Two of the three arrived at the host-offloads answer, and the reasoning that decided it was neither a
majority nor the abstract layering argument but a verified fact about the code: `EngineConcurrencyTests` already
states and pins the SDK-side contract, so the chosen arrangement records an existing guarantee rather than
introducing a policy. The dissenting analysis argued for extracting the scheduler immediately on thick-SDK grounds;
that argument is recorded as revisit trigger (d) and as the second Negative implication rather than dismissed, since
it is right about the risk and wrong only about the timing.
