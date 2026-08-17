# PROPOSALS — backlog

One row per open item. **Detail lives in the sections below the table.** Standing constraints are at the
bottom — they are rules, not work, and have no status.

**Status vocabulary:** `Todo` accepted, not started · `Needs decision` blocked on an owner ruling ·
`Blocked` waiting on evidence or another piece of work · `Idea` not committed · `Verify` believed done,
needs confirming.

**Kinds:** `Defect` something is wrong · `Task` accepted work · `Decision` owner call ·
`Oracle` vendor-measurement session · `Idea` unconvinced.

> Rows A1–A2, B1–B4 and G1–G2 came from an architecture gap analysis on 2026-08-14 and were **revised
> after external review** on the same day; the review corrected several of them and killed one outright.
> Each section below is self-contained — the analysis it came from lives in `tmp/agaps.md`, which is
> **untracked** (`.gitignore:11`) and therefore absent from a clean clone. Do not rely on those links.

## Backlog

Ordered by priority within each group.

| ID | Item | Kind | Status | Detail |
| ---- | ------ | ------ | -------- | -------- |
| **T1** | `UserManagerService.GetUsers` applies its redaction conditional in the **opposite direction** from its own comment | Defect | Todo | [§T1](#t1--usermanagerservicegetusers-redaction-is-inverted) |
| **T2** | Configuration services attach **raw** WLAN/SMTP/email-control models to activity tags; their `ToString()` reveals secrets | Defect | Todo | [§T2](#t2--raw-secret-bearing-models-on-activity-tags) |
| **G2a** | Product-dialog numeric `Minimum`/`Maximum` are enforced **nowhere** — an out-of-range value commits | Defect | Todo | [§G2a](#g2a--numeric-range-is-not-enforced-on-dialog-write-back) |
| **G1** | The vendor app can edit a `.vis` while OpenVisual has it open; the save silently overwrites it | Defect | Todo | [§G1](#g1--external-modification-of-the-open-file) |
| **A1** | No `CancellationToken` on the async surface and no HTTP timeout — a caller blocks until the 100 s default | Defect | Todo | [§A1](#a1--cancellation-and-timeouts) |
| **A2** | No behavioural test for the controller half — no wire fixtures, and `safe_integration_tests` never runs | Task | Todo | [§A2](#a2--soap-fixture-corpus--replay-harness) |
| **B2a** | Decide the generated SOAP layer's visibility — Invariant 9's admitted `public` leak | Decision | **Needs decision** | [§B2a](#b2a--generated-soap-visibility) |
| **B2b** | The SDK's public surface is not pinned (depends on B2a) | Task | **Blocked** — needs B2a | [§B2b](#b2b--sdk-public-api-baseline) |
| **B1** | Tracing but no metrics — needs a `Meter` **and** an export pipeline that does not exist | Task | Todo | [§B1](#b1--metrics-instrument-and-export-pipeline) |
| **B4** | Refusals carry a bare Danish string while validation findings carry a `RuleId` | Task | Todo | [§B4](#b4--refusal-codes) |
| **B2c** | GUI banned-API rules are test-time only; could be compile-time (scoped to `ihc_openvisual`) | Task | Todo | [§B2c](#b2c--gui-banned-apis-at-compile-time) |
| **G2b** | Live per-field dialog feedback (depends on G2a) | Task | **Blocked** — needs G2a | [§G2b](#g2b--live-per-field-dialog-feedback) |
| **T3** | HTTPS certificate identity is not authenticated (`DangerousAcceptAnyServerCertificateValidator`) | Decision | **Needs decision** | [§T3](#t3--https-certificate-trust-boundary) |
| **D1** | Rule on the two US-068 residuals (log-mark scope; stop-point / jump-to leaf routes) | Decision | **Blocked** — needs T018's Discoveries entry | [§D1](#d1--us-068-residuals) |
| **O1** | PG-5 enum-editing oracle session — capture the value-id reallocation rule | Oracle | Todo | [§O1](#o1--pg-5-enum-editing-oracle-session) |
| **P1** | `PerfBaselineBenchmark` is `[Explicit]` — the five perf budgets are measurable but never gated | Idea | Todo | [§P1](#p1--perf-benchmark-is-never-run) |
| **R1** | Model-driven report rendering (option B: generic shape document + GUI shape interpreter) | Idea | **Verify** — appears superseded | [§R1](#r1--model-driven-report-rendering) |

**Withdrawn after review:** a proposed `IProjectDocument : IDisposable` item and a nullable-enable item —
see [Standing constraints](#standing-constraints--do-not-reopen-without-new-evidence). Coverage
gating / mutation testing remains unpromoted.

---

## Details

### T1 · `UserManagerService.GetUsers` redaction is inverted

- [ ] Verify against `IhcSettings.LogSensitiveData` and fix. Reproduce with a test first, per the
      repo's bug workflow.

### T2 · Raw secret-bearing models on activity tags

Configuration services attach raw WLAN/SMTP/email-control models to activity tags, and those models'
parameterless `ToString()` reveals secrets.

- [ ] Decide the fix shape: redacting `ToString()` overrides · an `[SensitiveData]`-aware scrubber in
      `ActivityExtensions` · or tag-site redaction.
- [ ] Implement it.

`ARCHITECTURE.md` states the standing property (redaction is call-site, not global, so trace data must
be treated as sensitive); these are the specific defects behind that warning.

### G2a · Numeric range is not enforced on dialog write-back

**Correctness defect.** `ProductDialogDescriptor` carries `Minimum`/`Maximum` (`ProductDialogDescriptor.cs:76-77`,
*"the numeric lower/upper bound DERIVED from the target element"*), and **nothing enforces them**:

- `ProductDialogCommands.cs` contains **no reference to `Minimum` or `Maximum`** — the write-back
  re-composes the dialog to check that a field is offered and writable, not that its value is in range.
- The GUI's `ProductDialogViewModel` exposes `Minimum`/`Maximum` (`:44,:46`) but its `IsSatisfied`
  (`:81`) consults **only** `Rule` — `Rule?.IsSatisfiedBy(Value) ?? true` — so the bounds are surfaced
  and never evaluated.

Net effect: a non-numeric or out-of-range value can be committed through the dialog.

- [ ] Reproduce with a test first: submit an out-of-range value for a field that declares bounds, assert
      it currently commits.
- [ ] Enforce the bounds in the SDK, on the same write-back path that already refuses unoffered and
      read-only fields — so the guarantee holds for every caller, not just the GUI.
- [ ] Decide whether an out-of-range value is a **refusal** (installer-actionable, Danish sentence) or a
      **failure**; refusal is the better fit and matches the existing unoffered/read-only behaviour.
- [ ] Cover per family in `safe_project_tests`, not for one product.

### G1 · External modification of the open file

**Accepted 2026-08-14: save-time identity check.** Saving is correctly atomic (temp + `File.Replace`),
but nothing records the loaded file's identity, so: installer has the project open in OpenVisual →
changes it in IHC Visual (or a backup restores it) → saves in OpenVisual → the external change is
overwritten with no error at any layer. In a project whose whole premise is co-existing with the vendor
tool over the same files, this is the write half of that contract.

**Design decisions this needs before coding** (raised in review):

- [ ] **Decide who owns the fingerprint.** `IProjectDocument` has no path; `ProjectWorkflow` owns
      `FilePath`. The fingerprint belongs with whoever owns the path, which points at `ProjectWorkflow`
      — but then the check must still run inside the save path.
- [ ] **Do not reuse `EditRefusedException`** — it means "the command session refused an edit". A save
      conflict is a different category and needs its own result type or refusal kind.
- [ ] Specify the full lifecycle, not just the happy path: baseline **updated after** save and save-as ·
      target **deleted** or replaced by a different file · explicit **reload** and **force-overwrite**
      operations · a **new document** that has no baseline yet.

**Implementation:**

- [ ] Capture a content hash at load (preferred over `length + LastWriteTimeUtc`: affordable at these
      file sizes and immune to timestamp granularity and clock oddities).
- [ ] Re-check immediately before the atomic replace and refuse on mismatch, offering
      overwrite / save-as / reload.
- [ ] **Never take an exclusive OS lock on the `.vis`** — that would break the vendor tool, which is the
      opposite of the goal.
- [ ] Reproduce first with a test: load, mutate the file underneath, save, assert refusal.

**Accepted limitations.** Detection is at save time only — a `FileSystemWatcher` was considered and
declined (it would see the app's own temp+rename writes and needs debounce plus self-write suppression).
And the check is inherently **best-effort**: there is a race between hashing and `File.Replace` that no
amount of care closes without a lock, which is ruled out. This narrows the window; it does not close it.

### A1 · Cancellation and timeouts

428 public `Task`-returning member declarations under `ihcclient/src/api/services` and **zero** accept a
`CancellationToken`; `ProjectAppService` is 11/0. The token exists only on the two
`GetResourceValueChanges` streaming methods. `PostAsync` is called without one and no `HttpClient.Timeout`
is set, so a caller blocks until `HttpClient`'s 100 s default and cannot abort in the meantime.

- [ ] Add `CancellationToken cancellationToken = default` as the trailing parameter across the API tier,
      the application tier, and `ProjectAppService`'s file/controller operations. **One sweep, not
      opportunistically** — a half-threaded surface looks like cancellation works.
      ⚠️ Source-compatible for *call sites* only. Adding an optional parameter to an **interface** is a
      binary-breaking change and breaks every existing implementer, including any outside this repo.
      Decide explicitly whether that is acceptable before starting.
- [ ] Thread it to the wire (`PostAsync(url, content, cancellationToken)`) **and to the response-body
      read** in `src/api/services/serviceBase.cs` — a token that stops at the request leaves the read
      unbounded, which is where a slow controller actually hangs.
- [ ] ⛔ **Do NOT replace the `CancellationToken.None` at `src/api/util/services.cs:86`.** It is
      deliberate: that call sits in a `finally` block (*"no cancellation here to avoid masking
      exceptions"*) immediately before `disableSubscription(resourceIds)`. Flowing an already-cancelled
      caller token there would skip the cleanup and leak the subscription on the controller. If cleanup
      needs bounding, give it its **own** short independent token — never the caller's.
- [ ] Set an explicit `HttpClient.Timeout` from `IhcSettings`. ⚠️ The client is a process-wide singleton
      built by the first caller (`src/api/util/httpclient.cs`), so a settings-derived timeout is
      **first-caller-wins** across every service in the process. Either accept that, move the bound to a
      per-request linked token, or change the client's lifetime — decide before implementing.
- [ ] Delete or rewrite the three `// TODO: Retry x times.` comments in `controllerService.cs:800,818,836`
      — they advertise work that will not happen (see the retry ruling in Standing constraints).
- [ ] Update `ARCHITECTURE.md` → Cross-cutting → Async with the cancellation policy.

### A2 · SOAP fixture corpus + replay harness

`tests/testdata/` holds `.vis`/`.def`/`.ifb`/report oracles and no wire fixtures, and
`safe_integration_tests` is compiled but never executed. **Precise claim:** the controller half has
serialization property tests and mocked-service tests, but **no behavioural test against realistic wire
data** — so an envelope, cookie-session or adapter regression is unguarded.

**Prerequisites the proposal previously assumed and does not have** (raised in review):

- [ ] **An injectable transport seam.** `GetOrCreateHttpClient` is `static private` and process-wide
      (`src/api/util/httpclient.cs`) — there is currently no way to substitute a handler for a test. This
      is the gating item; without it the rest cannot be built.
- [ ] **A cassette schema.** `utilities/ihc_httpproxyrecorder` writes an append-only timestamped log, not
      canonical replay fixtures. Define the on-disk format, request-matching key, and how ordered
      request/response pairs are addressed.
- [ ] **Cookie/auth sequencing.** Sessions are cookie-based; replay must reproduce login → cookie →
      subsequent calls deterministically, or every fixture becomes order-dependent.
- [ ] **Deterministic redaction.** Recordings carry credentials, cookies, IPs, serial numbers and a real
      installation's layout. Redaction must be scripted and repeatable, not a manual pass — a
      hand-scrubbed corpus cannot be re-recorded safely later.
- [ ] **Test isolation.** The process-wide client and cookie handler are shared state; two fixtures in
      one run must not see each other's session.

**Then:**

- [ ] Record, redact, and commit under `tests/testdata/soap/` with the per-file provenance discipline
      `testdataoverview.md` already applies.
- [ ] Run the existing `safe_integration_tests` assertions against the replay seam in a controller-free
      suite — a CI gate that touches no controller and keeps Invariant 5.
- [ ] Handle vendor-response drift the way the `.vis` oracles do: re-record deliberately, never edit.

### B2a · Generated SOAP visibility

**Decide first — B2b depends on it.** `ARCHITECTURE.md` Invariant 9 admits the gap: generated SOAP types
and proxy classes are technically `public` but consumers must not use them, so vendor WSDL churn would
ship as this SDK's breaking changes.

- [ ] Decide: emit the generated layer as **internal** (`generate.sh` can; the `InternalsVisibleTo`
      friend list already covers the suites that need it) · or accept the public surface deliberately and
      record it as an ADR. It is currently neither.

### B2b · SDK public-API baseline

**Blocked on B2a** — pinning a surface you are about to change wastes the baseline.

- [ ] Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to `ihcclient` and check in **both**
      `PublicAPI.Shipped.txt` *and* `PublicAPI.Unshipped.txt`. The documented workflow needs the pair:
      new API lands in *Unshipped* (via the analyzer's code fix) and is promoted to *Shipped* at release.
      A single checked-in Shipped file is not the baseline.
- [ ] Note the interaction with the project's `<Nullable>disable</Nullable>` default: the analyzer
      records nullability in the API files, so files that already opt in per-`#nullable enable` and files
      that do not will produce different entries. Settle the ordering against the nullability position in
      Standing constraints before generating the baseline.

### B1 · Metrics instrument and export pipeline

`Meter` ships in the same `System.Diagnostics.DiagnosticSource` assembly the SDK already uses for
tracing, so Invariant 7's "no logging dependency" argument does not forbid it — the **instrument** costs
zero new package references. **But instrumenting is not exporting** (raised in review):
`shared/ihc_appbootstrap/AppTelemetryBootstrap.cs` builds only `Sdk.CreateTracerProviderBuilder()` and
the telemetry configuration has no metrics section, so a `Meter` added today emits to nobody.

- [ ] SDK: add one `Meter` in `src/config/Telemetry.cs`. Suggested starting instruments:
      `ihc.controller.operation.duration` (histogram; service, operation, outcome) ·
      `ihc.controller.operation.count` (counter; same tags) ·
      `ihc.vis.document.command.duration` (histogram; command, `Ok`/`Refused`/`Failed`) ·
      `ihc.vis.project.load.duration` (histogram; size bucket).
- [ ] Host: add a `MeterProvider` to `AppTelemetryBootstrap` with `AddMeter(...)`, an OTLP metrics
      exporter, its own configuration section, and the same keep-alive/dispose-to-flush lifetime handling
      the `TracerProvider` already has.
- [ ] Verify end-to-end that a metric reaches OpenObserve — the `openobserve` skill can confirm; without
      this step the work is unfalsifiable.
- [ ] Amend Invariant 7 to say what it means: *no logging dependency*; tracing **and metrics** via the
      in-box diagnostics APIs.

### B4 · Refusal codes

`EditRefusedException(string message)` carries a bare Danish sentence; validation findings carry
`(Severity, RuleId, Category, Locator, Message)`. So half of "errors as data" is structured and half is
not: refusals can only be asserted by substring, cannot be re-worded or translated by any frontend, and
cannot be aggregated by cause.

- [ ] Cover **every** refusal origin, not just the exception: `EditVerdict` and the pre-edit guards in
      `SessionCoreTypes.cs`, preview outcomes, and directly-returned refusal results. A code on the
      exception alone leaves most refusals unclassified.
- [ ] Carry **structured arguments** alongside the code, not just the code. Many refusal sentences are
      composed from element names and ids; a code without its arguments cannot be re-rendered in another
      language, only classified.
- [ ] **Add** code assertions to `tests/safe_project_tests/RefusalLanguageTests.cs` — do **not** replace
      the existing exact-Danish assertions. That file is the contract for the wording and must keep
      pinning it.

This **preserves** the Danish-refusal decision rather than reopening it — it makes Danish the default
instead of the only possibility.

### B2c · GUI banned APIs at compile time

`ConfigureAwait` and `Process.Start` are banned in `ihc_openvisual` and enforced today by IL scan in
`safe_architecture_tests`. Compile-time enforcement would fail faster and closer to the edit.

- [ ] Add `Microsoft.CodeAnalysis.BannedApiAnalyzers` **to `applications/ihc_openvisual` only**, with a
      GUI-scoped `BannedSymbols.txt`. Keep the arch tests as the backstop.
- [ ] ⛔ **Never apply these bans to `ihcclient`.** The SDK uses `ConfigureAwait` deliberately and
      pervasively — 239 occurrences across 22 files, mostly
      `ConfigureAwait(settings.AsyncContinueOnCapturedContext)`. Banning it there would be wrong, not
      merely noisy.

### G2b · Live per-field dialog feedback

**Blocked on G2a** — there is no point surfacing a bound the write-back does not enforce.

Correcting the original framing: the GUI does **not** discard the descriptor's rules. `ProductDialogViewModel`
already exposes `Rule`, `IsSatisfied` and `RefusalSentence`, so rule feedback partly exists. What is
missing is (a) `Minimum`/`Maximum` participating in `IsSatisfied` (G2a's SDK fix should drive this) and
(b) a standard error-presentation channel.

- [ ] Once G2a lands, include the bounds in the field's satisfied/unsatisfied state.
- [ ] Consider `INotifyDataErrorInfo` on the field view-model so the error presents through the standard
      channel rather than an ad-hoc binding. Optional — the existing `IsSatisfied`/`RefusalSentence` pair
      may be sufficient.
- [ ] Re-run `AutomationCoverageTests`: validation adornments can add nodes to the peer tree the audit
      walks, and the descriptor-driven dialog is audited populated, once per family.
- [ ] Cover it in `safe_visual_tests` per family, not just for one.

**Known tension, accepted:** vendor parity is this repo's governing default and IHC Visual's per-field
behaviour was **not** measured. The ruling treats live feedback as an additive affordance that changes
no write semantics, so parity is not considered binding on it. G2a is unaffected by this — enforcing a
declared bound is correctness, not an affordance.

### T3 · HTTPS certificate trust boundary

The client plumbing accepts every HTTPS server certificate through
`DangerousAcceptAnyServerCertificateValidator`, so certificate identity is not authenticated.
Documented as the controller trust boundary in `ARCHITECTURE.md`.

- [ ] Decide whether it stays deliberate (self-signed controller certs) or becomes opt-in.

### D1 · US-068 residuals

T018 records the *current* log-mark / stop-point / jump-to behaviour in the backlog's Discoveries before
setting US-068 to blocked. Both decisions were deliberately deferred (ruling 2026-07-21) until that
evidence exists — **do not decide earlier.**

- [ ] Read T018's Discoveries entry.
- [ ] Decide the **log-mark scope**: offered on every product pin, or only on `Logning`-bearing rows.
- [ ] Decide the **stop-point / jump-to leaf routes**: build · drop (won't-do) · re-spec. (If they turn
      out simulation-adjacent, note that E8/simulation is out of scope.)
- [ ] Promote a small follow-up task implementing the rulings; refresh US-068's `Implementation status:`
      line from **blocked** to its real state
      (`applications/ihc_openvisual/docs/stories/11-interaction-model.md`).

### O1 · PG-5 enum-editing oracle session

Route approved 2026-07-21; plan not yet authored. Goal: let US-030 gain enum state **reorder**, state
**remove**, and type **rename** — currently out of scope (D05) because the value-id reallocation
semantics are unknown.

- [ ] Author the capture plan (separate elevated ihcvisual-MCP session, config-mode): vendor enum dialog
      before/after `.vis` byte pairs for each of the three operations. The Win32 recipe for dialog
      `24588` exists from the enumvalues Gap3 session; oracle naming/registration follows the
      `project4-PrgTokens*` pattern.
- [ ] Run it; establish the value-id reallocation rule and what happens to referencing program rows /
      case branches / inline enum constants.
- [ ] Promote engine + UI tasks on that evidence (D05 lifts only then); update US-030.

### P1 · Perf benchmark is never run

`tests/safe_project_tests/benchmarks/PerfBaselineBenchmark.cs` already measures the five budgeted hot
paths (drag-over probe < 5 ms, save < 1 s, open < 2 s, commit < 50 ms, undo/redo < 50 ms) with warm-up +
samples → median/p95. It is `[Explicit]` and `[Category("Benchmark")]`, so it never runs in the gate, and
its numbers are machine-specific with no recorded baseline to compare against.

- [ ] Decide whether that is intentional (a manual tool) or whether it should become a nightly leg with a
      committed per-machine baseline and a non-regression check. Low priority — the measurement exists,
      only the gate is missing.

### R1 · Model-driven report rendering

Original question (explore, do not implement): *can report generation in OpenVisual be made model-driven
using reflection on the data models supplied by the ihcclient API, possibly extended with attribute
metadata, so report content is not hardcoded in OpenVisual but derived?* Explored 2026-07-21 —
analysis at `tmp/metadrivenreport-ana.md` (untracked).

The idea was option B: the combined report model (backlog T020) emits a generic shape document
(Table/KeyValue/Outline sections with US-071 option tags) and the GUI becomes a small shape interpreter;
reflection/attributes stay SDK-internal if used at all. To be decided as an amendment to T020 **before**
the reporting phases (Phase 4+) of the reporting backlog start — not retrofitted onto the then-current
three report models.

- [ ] **Verify and then close.** `ARCHITECTURE.md` now describes the shipped reporting pipeline as
      *"per-kind builders project the tree into a mode-tagged shape document (a closed layout
      vocabulary) … and one generic writer per format"*, with reports generated SDK-side and the GUI
      composing no report markup at all. That reads as option B having landed, in which case this item
      is superseded and should be deleted rather than left open. Confirm before deleting.

---

## Standing constraints — do not reopen without new evidence

- **Float-target ÷ is unauthorable** (F-107; the P7 manual rung was waived 2026-07-21). US-032 is final:
  division targets integers only. Reopen only if a new token source appears.
- **Dead popup entries are never offered** (F-106/F-109): float+float `+` · int−int and int←float `−` ·
  counter two-operand `−` · int×int `×` · the 2-operand `Timer ->` event · the `Timer <` condition
  (authors express "less than" by swapping the operands of `>`).
- **Never invent method tokens** (D09). The token oracle is `tmp/prgmode/out/method-map.md` (e2 +
  progmode3 rows) attested by `tests/testdata/projects/project4-PrgTokens.vis` and `…-round2.vis`.
- The F-096 vendor quirk (`= Timer +` greyed until a Timertid pin exists) must **not** be copied.
- **No transport resilience policy** (ruled 2026-08-14). No retry, backoff, or circuit breaker on the
  controller path: the transport addresses a controller on the same LAN, not a WAN dependency, so the
  failure profile that motivates the pattern largely does not arise, and a breaker in front of one fixed
  endpoint adds state without adding availability. **The premise expires if remote access is ever
  supported.** Timeouts are *not* covered by this and stay in scope (A1).
- **No autosave, command journal, or crash recovery** (ruled 2026-08-14). In-memory-only undo history is
  accepted for an incubating app; commands are deliberately not serializable, so there is no macro or
  scripting surface either. `ARCHITECTURE.md` was corrected to stop claiming one. Does **not** cover G1,
  which is a different defect.
- **`IProjectDocument` will not implement `IDisposable`** (withdrawn after review, 2026-08-14). It was
  proposed on the assumption that `Close()` releases a resource. It does not: `Close()` only clears
  managed session state (`_current`/`_savePoint`/`_index` to null, history cleared, version bumped) and
  the same session is deliberately **reopened** afterwards by `ProjectWorkflow`. There is no lock,
  handle or registration to leak. `IDisposable` would impose terminal semantics on a reusable state
  transition and is a breaking interface change — the dispose pattern is for deterministic resource
  cleanup, not for enforcing an ordinary state transition.
- **Nullable is already largely enabled in the SDK** (withdrawn after review, 2026-08-14). A proposed
  "enable nullable file by file" item was based on `ihcclient.csproj` setting
  `<Nullable>disable</Nullable>`. That is only the project default: **151 files** under `ihcclient/src`
  already carry `#nullable enable`, including essentially all of `src/vis`. The residual is the older
  API/app-tier files and the project-level default (tracked separately as designfix P3/C4) — not a
  greenfield migration. Do not re-raise it as one.
