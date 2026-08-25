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
| **T1** | `UserManagerService.GetUsers` applies its redaction conditional in the **opposite direction** from its own comment | Defect | Verify | [§T1](#t1--usermanagerservicegetusers-redaction-is-inverted) |
| **T2** | Configuration services attach **raw** WLAN/SMTP/email-control models to activity tags; their `ToString()` reveals secrets | Defect | Todo | [§T2](#t2--raw-secret-bearing-models-on-activity-tags) |
| **G2a** | Numeric `Minimum`/`Maximum` **are** enforced on write-back now — but nothing tests it, for any family | Task | Todo | [§G2a](#g2a--numeric-range-is-enforced-but-untested) |
| **G2c** | A **non-numeric** value in a bounded numeric field still commits — the surviving half of the old G2a | Decision | **Needs decision** | [§G2c](#g2c--a-non-numeric-value-in-a-bounded-field) |
| **G1** | The vendor app can edit a `.vis` while OpenVisual has it open; the save silently overwrites it | Defect | Todo | [§G1](#g1--external-modification-of-the-open-file) |
| **A1** | Catalog import blocks the UI thread for ~2.4 s with no cancel; `LoadPersisted` blocks startup the same way | Defect | Todo | [§A1](#a1--catalog-import-blocks-the-ui-thread) |
| **A1b** | No HTTP timeout on the controller path — a caller blocks until `HttpClient`'s 100 s default | Defect | Todo | [§A1b](#a1b--http-timeout-on-the-controller-path) |
| **A2** | No behavioural test for the controller half — no wire fixtures, and `safe_integration_tests` never runs | Task | Todo | [§A2](#a2--soap-fixture-corpus--replay-harness) |
| **B1** | Tracing but no metrics — needs a `Meter` **and** an export pipeline that does not exist | Task | Todo | [§B1](#b1--metrics-instrument-and-export-pipeline) |
| **B4** | Refusals carry a bare Danish string while validation findings carry a `RuleId` | Task | Todo | [§B4](#b4--refusal-codes) |
| **C1** | The Linux CI legs cannot be run locally, and their native-dep list exists only in the workflow YAML | Task | Todo | [§C1](#c1--local-linux-ci-legs-on-a-windows-workstation) |
| **G2b** | Live per-field dialog feedback — the GUI's `IsSatisfied` still ignores the bounds the SDK now enforces | Task | Todo | [§G2b](#g2b--live-per-field-dialog-feedback) |
| **T3** | HTTPS certificate identity is not authenticated (`DangerousAcceptAnyServerCertificateValidator`) | Decision | **Needs decision** | [§T3](#t3--https-certificate-trust-boundary) |
| **D1** | Rule on the two US-068 residuals (log-mark scope; stop-point / jump-to leaf routes) | Decision | **Blocked** — needs T018's Discoveries entry | [§D1](#d1--us-068-residuals) |
| **O1** | PG-5 enum-editing oracle session — capture the value-id reallocation rule | Oracle | Todo | [§O1](#o1--pg-5-enum-editing-oracle-session) |
| **P1** | `PerfBaselineBenchmark` is `[Explicit]` — the five perf budgets are measurable but never gated | Idea | Todo | [§P1](#p1--perf-benchmark-is-never-run) |
| **V3** | The Fuld report opens its **own** validation run under a weaker profile than the facade's | Defect | Todo | [§V3](#v3--the-report-opens-its-own-validation-run) |
| **V6** | The `edit.*` family — the largest — sits **outside** the reflective refusal drift gate | Defect | Todo | [§V6](#v6--the-edit-family-is-outside-the-drift-gate) |
| **V9** | `ProblemConsoleFormat` drops **every finding** of a validation failure | Defect | Todo | [§V9](#v9--the-utility-loses-a-validation-failures-findings) |
| **V8** | Two shell sites render `Problem.Message` raw, bypassing `ProblemPresenter` | Defect | Todo | [§V8](#v8--two-shell-sites-bypass-the-presenter) |
| **V1** | ~190 catalogue entries hand-spell the same positional constructor; the host already has the fix | Task | Todo | [§V1](#v1--catalogue-entry-factories) |
| **V2** | `Target` is `default` on ~140 of 141 finding rows, so the second engine face is **vacuous** | Task | Todo | [§V2](#v2--the-entry-target-is-undeclared) |
| **V5** | The first-holder-wins duplicate scan is written out **eight times** | Task | Todo | [§V5](#v5--the-duplicate-scan-written-eight-times) |
| **V4** | Work repeated **per rule** inside one run — the analyses stop short of three shared facts | Task | Todo | [§V4](#v4--facts-recomputed-per-rule-within-one-run) |
| **V7** | The session refusal channel carries `(code, string)`; the shell re-assembles a `Problem` | Task | Todo | [§V7](#v7--the-session-refusal-channel-drops-its-arguments) |
| **V13** | The executor invents severity/category for a thrown rule; catalogue invariants never run at build | Task | Todo | [§V13](#v13--a-thrown-rules-classification-is-invented) |
| **V10** | Rule-test scaffolding: four helpers pasted into 14–19 files each | Task | Todo | [§V10](#v10--rule-test-scaffolding) |
| **V11** | Four `severity-*.svg` assets ship in the binary and are referenced nowhere | Decision | **Needs decision** | [§V11](#v11--four-unreferenced-severity-icons) |
| **V12** | The report appendix's order is a hard-coded per-code rank list read across a layer | Idea | Todo | [§V12](#v12--appendix-order-as-a-rank-list) |

> Rows **V1–V13** came from a four-angle cleanup review of the rule-engine and problem-catalogue work
> (`6bedab9..HEAD`) on **2026-08-24**. That pass **already landed** the mechanical wins — 63 duplicated
> rule helpers folded into `RuleAuthoring`, two whole-document walks removed, dead `FindingCollector`
> deleted, and eight further reuse/efficiency fixes — with all 4 010 controller-free tests green and the
> characterization oracle unmoved. What is listed here is the **residue**: items that change behaviour,
> move an oracle, touch public API, or are an owner's call, and so were deliberately not taken
> unilaterally. Each row states which of those it is.

**Withdrawn after review:** a proposed `IProjectDocument : IDisposable` item and a nullable-enable item —
see [Standing constraints](#standing-constraints--do-not-reopen-without-new-evidence). Coverage
gating / mutation testing remains unpromoted.

---

## Details

### T1 · `UserManagerService.GetUsers` redaction is inverted

- [x] **Fixed 2026-08-22.** The branches were swapped, so the `retv` span tag carried cleartext
      passwords exactly when `LogSensitiveData` was **false** — the default. The tag is now built
      through `IhcUser.ToString(bool)`, which restores the direction and also stops an exporter
      falling back to the parameterless `ToString()` the model itself documents as unsafe.
      Reproduced first in `tests/safe_unit_tests/UserManagerServiceTelemetryTests.cs` (both
      directions, plus a guard that the *returned* user keeps its password); the fixture reaches the
      service through a new internal test-seam constructor mirroring `ControllerService`’s.

### T2 · Raw secret-bearing models on activity tags

Configuration services attach raw WLAN/SMTP/email-control models to activity tags, and those models'
parameterless `ToString()` reveals secrets.

- [ ] Decide the fix shape: redacting `ToString()` overrides · an `[SensitiveData]`-aware scrubber in
      `ActivityExtensions` · or tag-site redaction.
- [ ] Implement it.

`ARCHITECTURE.md` states the standing property (redaction is call-site, not global, so trace data must
be treated as sensitive); these are the specific defects behind that warning.

### G2a · Numeric range is enforced but untested

> **Re-measured 2026-08-24. The correctness defect is FIXED; only its test coverage is outstanding.**
> This row's central evidence — *"`ProductDialogCommands.cs` contains no reference to `Minimum` or
> `Maximum`"* — is no longer true, and the fix predates this re-measurement (the comment above the call
> reads *"the bounds … finally read"*). The row is retitled and re-kinded accordingly; the one clause of
> its old net-effect sentence that DOES survive was split out as [§G2c](#g2c--a-non-numeric-value-in-a-bounded-field)
> rather than dropped.

**What now holds.** The write-back enforces the descriptor's bounds on the same path that already refuses
unoffered and read-only fields, so the guarantee holds for **every** caller and not just the GUI:

- `ProductDialogCommands.cs:115` — `if (OutsideBounds(field, edit.Value) is { } outside) return
  EditVerdict.Refuse(EditRefusalCodes.FieldOutOfRange, outside);`
- `OutsideBounds` (`:150`) reads `field.Minimum`/`field.Maximum`; `Sentence` (`:171`) builds the Danish
  refusal naming the field and its bounds (*"Feltet 'X' skal være mellem 0 og 9999."*).
- The refusal-versus-failure question is settled the way this row recommended: **refusal**.
  `edit.field-out-of-range` carries `CatalogDisposition.Refusal`
  (`ProblemCatalogEntries.EditRefusals.cs:589-592`).

**What is outstanding — and it is the whole of this row now.** `grep -rln "FieldOutOfRange" tests` returns
**nothing**. No test, in any suite, asserts this refusal for any family. The behaviour is real and
ungated, so a regression would ship silently. (`CatalogCompletenessTests` proves the code *has a
catalogue entry*; it does not prove the code is ever raised.)

- [ ] Cover the refusal per family in `safe_project_tests`, not for one product: a field that declares
      bounds, a value below the minimum, a value above the maximum, and the two boundary values
      accepted. Assert the CODE (`EditRefusalCodes.FieldOutOfRange`), not the sentence alone.
- [ ] Assert the sentence separately against the field's declared bounds, so the three `Sentence`
      shapes (min+max, min only, max only) are each exercised rather than only the common one.
- ~~Reproduce with a test first~~ — moot: there is no longer a defect to reproduce. It collapses into
  the coverage item above.
- ~~Enforce the bounds in the SDK~~ — **done**, see above.
- ~~Decide refusal versus failure~~ — **done**: refusal.

### G2c · A non-numeric value in a bounded field

**Split out of G2a on 2026-08-24**, because it is the one clause of that row's net-effect sentence
(*"a **non-numeric** or out-of-range value can be committed"*) that survived the fix. Out-of-range is now
refused; non-numeric is not.

`OutsideBounds` returns `null` for a blank **or unparseable** value, deliberately and with the reasoning
written at the call site (`ProductDialogCommands.cs:110-114`):

> *A BLANK value is not out of range: a numeric field sitting at its declared default presents blank, and
> committing blank writes the default back. Neither is an unparseable one — this condition answers "is
> this number outside its bounds", and nothing else.*

The blank half is clearly right. The unparseable half is a **decision, not an oversight** — but it means
`abc` submitted into a bounded numeric field is written through unless that field also carries a
`DialogValueRule`, and the descriptor's bounds are the only thing that marks the field as numeric at all.

- [ ] **Decide where "this field holds a number" is enforced.** Three shapes, and they are not equivalent:
      the write-back refuses an unparseable value in a bounded field · the composer gives every bounded
      field a numeric `DialogValueRule`, so the existing rule check catches it · or it stays the caller's
      problem and this row is closed as won't-do.
- [ ] Whichever is chosen, it needs its own code and Danish sentence — *"is not a number"* is a different
      refusal from *"is outside its bounds"*, and reusing `edit.field-out-of-range` would anchor a
      sentence to an entry that does not govern it.
- [ ] Check the vendor's behaviour before ruling: parity is the governing default, and this was not
      measured. IHC Visual's numeric fields may simply refuse the keystroke, in which case the state is
      unreachable through the dialog and only reachable by import or hand-edit — which would make it a
      whole-project finding rather than a commit refusal.

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

### A1 · Catalog import blocks the UI thread

> **Re-evaluated 2026-08-24.** The original A1 proposed one `CancellationToken` sweep across the API tier,
> the application tier and `ProjectAppService`. Three things have changed since it was written, and
> together they invert it. Its checkboxes were also marked `[x ]` while **none** of the work was done —
> `Client.Post` still calls `SendAsync(request)` with no token, no `HttpClient.Timeout` is set anywhere on
> the SOAP path, and the three retry TODOs are still at `controllerService.cs:800,818,836`. The boxes are
> gone rather than reset: most of that list is now foreclosed, and the rest is [§A1b](#a1b--http-timeout-on-the-controller-path).
>
> 1. **ADR-001 already ruled on the SDK half and rejected it** (broadened 2026-08-24). It weighed
>    *"the facade grows `…Async(snapshot, CancellationToken)` doors"* against host-side offload and chose
>    the host, on three counts: a library wrapping synchronous CPU work in `Task.Run` spends the caller's
>    thread-pool thread unasked; every existing `Task`-returning facade door is I/O-shaped, so `Async`
>    would mean two different things on one type across a shipped public-API baseline; and the token would
>    be a public promise the engine cannot keep mid-run. Its standing rule: **never publish a
>    `CancellationToken` on a door that ignores it.** See [Standing constraints](#standing-constraints--do-not-reopen-without-new-evidence).
> 2. **Validation does not need cancelling — measured, not assumed.** `PerfBaselineBenchmark` over the
>    largest authentic project (`project3-KompleksWired`, 11 localities / **1337 elements**):
>    `ValidateCategorized` is **12.6 ms median, 13.4 ms p95** in a DEBUG build; the whole 17-document
>    characterization corpus is 28 ms. And it cannot grow without bound — a `.vis` targets a physical
>    controller, so `ControllerCapabilityLimits` caps a project at 2000 resources / 8 input + 16 output
>    modules / 128 addresses per direction / 64 wireless devices. ADR-001 makes finer cancellation
>    *"an increment to buy when measurement justifies it"*; measurement does not. Nothing under
>    `applications/` calls `Validate` or `ValidateCategorized` today either, so there is also no caller to
>    cancel.
> 3. **The genuinely long-running path is catalog import, which A1 never mentioned** — and it is host-side,
>    in OpenVisual, which is the low-risk half. That is the item below.

`CatalogImportWorkflow.ImportFolderAsync` is declared `async Task` but **contains no `await` inside its
loop**: it walks `Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)` and per file runs
`service.ImportCatalogFile(file)` (read + XML parse + catalog append) and, when persisting, a `File.Copy`.
The only awaits are the error dialogs. So the whole run occupies the UI thread — and OpenVisual has **no
`Task.Run` anywhere**, so ADR-001's five-step host contract is currently written but unimplemented.

**Measured against a real vendor install** (`C:\Program Files (x86)\LK IHC Control`): **480** `.def`/`.ifb`
files, **2385 ms parse-only**, DEBUG. Directory enumeration is 7 ms of that — the cost is the per-file
parse. `persist: true` adds 480 `File.Copy` calls on top. Unlike everything else in the engine this is
bounded by the folder the installer picks, not by controller capacity, so there is no ceiling argument to
make here.

`LoadPersisted()` is the worse half of the same defect: it is called **synchronously from the
`ProjectWorkflow` constructor** (`ProjectWorkflow.cs:71`), so a large persisted catalog delays startup with
no window drawn yet — and unlike the folder import it deliberately does **not** stop at the first bad file
(it logs and continues), so it always pays the full walk.

- [ ] Reproduce first: a `safe_visual_tests` case over a fixture folder asserting the import is
      cancellable and that the UI thread is not the one doing the parsing.
- [ ] Offload the loop under ADR-001's host contract — `await Task.Run(… , ct)`, bind on the UI thread,
      honour the token at both boundaries. **The token is checked between files, in the host's own loop**,
      which is exactly ADR-001's coarse-cancellation model and needs **no SDK change**: `ImportCatalogFile`
      stays synchronous and tokenless.
   - Thread-safety is already settled — `CompositeCatalog` documents itself *"Deferred + concurrent-read
     safe"*: `Import` appends under a `lock (gate)` and invalidates the snapshot, reads take a lock-free
     `Volatile.Read`. ⚠️ But note this is a **mutation**, not one of ADR-001's compute-doors-over-immutable-
     snapshots, so steps 1 and 3 (snapshot + version capture, latest-wins discard) do not apply; steps 2,
     4 and 5 do. Say so where the code lands, or the next reader will assume the contract was misapplied.
- [ ] Add a progress + cancel affordance. `IDialogService` has **no** progress or busy dialog today, so
      this is a new seam rather than a new call. ADR-001 already fixes the mechanism: `IProgress<T>`,
      which keeps view-models free of Avalonia types.
- [ ] Decide what cancelling a half-finished import means. `CatalogImportOutcome` already distinguishes
      `Stopped` from `Completed` precisely so a partial run is not reported as a whole one (UX review
      CORE-03) — a cancelled run is a third outcome and should not be folded into `Stopped`, which means
      "hit an unreadable file".
- [ ] Startup: make `LoadPersisted()` not block the constructor. This one is **not** cancellable by the
      installer — there is no UI yet — so it wants deferral, not a cancel button.

### A1b · HTTP timeout on the controller path

The one piece of the original A1 that survives intact, and the only genuinely bad failure mode in it:
**no `HttpClient.Timeout` is set anywhere on the SOAP path** (the sole `.Timeout` in the SDK is an
unrelated 5 s telemetry probe in `config/Telemetry.cs:281`), and `Client.Post` calls `SendAsync(request)`
with no token. A caller therefore blocks until `HttpClient`'s 100 s default with no way to abort.

**Deliberately narrowed.** The original item bundled this with adding `CancellationToken` to 429 public
`Task`-returning members under `src/api/services` (plus `ProjectAppService`'s 11). That sweep is now
**not recommended**: it is binary-breaking on every interface it touches, it is the highest-risk surface in
the repo, and — per ADR-001 — a token that only reaches the transport is a public promise most of those
doors would not keep. The timeout can be had without any of it.

- [ ] Bound the request in `Client.Post` with a per-request `CancellationTokenSource(timeout)` rather than
      setting `HttpClient.Timeout`. ⚠️ The client is a process-wide singleton built by the first caller
      (`src/api/util/httpclient.cs`), so a settings-derived `HttpClient.Timeout` is **first-caller-wins**
      across every service in the process; a per-request linked token has the same effect with no
      singleton coupling and touches no public signature.
- [ ] Source the value from `IhcSettings` and bound the **response-body read** as well as the request — a
      bound that stops at the request leaves the read unbounded, which is where a slow controller actually
      hangs.
- [ ] ⛔ **Do NOT replace the `CancellationToken.None` at `src/api/util/services.cs:86`.** It is
      deliberate: that call sits in a `finally` block (*"no cancellation here to avoid masking
      exceptions"*) immediately before `disableSubscription(resourceIds)`. Flowing an already-cancelled
      caller token there would skip the cleanup and leak the subscription on the controller. If cleanup
      needs bounding, give it its **own** short independent token — never the caller's.
- [ ] Delete or rewrite the three `// TODO: Retry x times.` comments in `controllerService.cs:800,818,836`
      — they advertise work that will not happen (see the retry ruling in Standing constraints).
- [ ] Update `ARCHITECTURE.md` → Cross-cutting → Async with the timeout policy, and state that
      cancellation of SDK compute is ADR-001's host-side concern rather than a facade parameter.

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

### C1 · Local Linux CI legs on a Windows workstation

**Why.** A Linux-only rendering defect shipped undetected and was caught only by CI (2026-08-17 — see
the `Svg.Controls.Skia.Avalonia` floor pinned in `Directory.Packages.props`). Verifying a Linux fix
today means push-and-wait. The recipe below was proven end to end during that session, so this item is
formalizing something that works, not discovering it.

**Proven recipe** (ad-hoc, uncommitted): `mcr.microsoft.com/dotnet/sdk:10.0-noble` — noble = Ubuntu
24.04 = `ubuntu-latest` — plus `libfontconfig1 libice6 libsm6`, repo bind-mounted at `/src`. Runs all
four Linux CI suites green from clean.

**Scope it honestly: "run the Linux legs", not "develop here".** In-container: the four controller-free
suites CI already runs on Linux. Not possible: the `aui-openvisual` skill (Windows UI Automation), live
OpenVisual / `ihc_lab` runs, `screen-recorder`, `safe_lab_tests`, and the nine `IhcVisualInstallDir`-gated
reference-catalog tests (the vendor tool is Windows-only). A container presenting itself as *the* dev
environment while unable to do half the work is a trap — name it for its job.

- [ ] Add `.devcontainer/Dockerfile` as the **single source** of the native-dep list. It currently
      exists only as a `run:` step in `.github/workflows/build-validation.yml`, so what "Linux CI" means
      can drift with nothing local pinning it.
- [ ] Add `.dockerignore`: `tmp/` (800 MB), `bin/`, `obj/`, `.git/`, `ihcsettings.json`.
- [ ] Add `scripts/test-linux.ps1` + `.sh`, matching the existing paired-script convention
      (`check-no-raw-tags.*`). No VS Code dependency.
- [ ] **Decide — bin/obj sharing.** A bind mount puts the Linux and Windows builds on the same
      `obj/project.assets.json` and `bin/`: rebuild thrash plus the stale-build trap below. Options: an
      OS-scoped artifacts path in `Directory.Build.props` · a named volume · copy-in. Solve this or the
      tool actively misleads.
- [ ] **Decide — `ihcsettings.json`.** Exclude it (matches CI exactly; recommended) or pass
      `IHC_ENCRYPT_PASSPHRASE` through for controller-touching runs.
- [ ] **Decide — should CI build the same image** rather than `apt-get`-ing inline? That is what makes
      local and CI unable to diverge; it costs some CI time.
- [ ] `devcontainer.json` only if "Reopen in Container" is actually used — trivial once the Dockerfile
      exists, and pointless otherwise.

**Gotchas already paid for — do not rediscover:**

- **Stale incremental builds.** Copying files in from Windows preserves the source mtime, so a file
  older than the container's build output makes MSBuild skip the rebuild. This produced a false
  *failure*, and would as readily produce a false *pass*. Clean `bin`/`obj` before any run whose
  result you intend to trust.
- **`ihcsettings.json` is gitignored, so CI has no such file** (`.gitignore:20`). Copy it in and its
  encrypted fields demand `IHC_ENCRYPT_PASSPHRASE` → 1324 failures in 230 ms in `safe_project_tests`,
  entirely an artefact of the sandbox.
- **Git Bash mangles `-v` paths**: `-v "$(pwd -W)/x:/src"` rewrites `/src` to `C:/Program Files/Git/src`.
  Drive docker from PowerShell, or set `MSYS_NO_PATHCONV=1`.
- WSL was tried first and abandoned: `sudo` needs a password there, so an unattended `apt-get` hangs.
- `.gitattributes` forces CRLF for `.vis`/`.def`/`.ifb` on every OS, so the byte oracles survive a Linux
  checkout or a volume clone — no special handling needed.

**Watch the skip count, not just the pass count.** `safe_project_tests` reports green with 9 skipped on
Linux; those are the `IhcVisualInstallDir`-gated tests, and a `CatalogDiscovery` path-separator defect
lived in exactly that gap until 2026-08-17. Skips gated on a *machine* rather than a *platform* are
where platform bugs hide.

### G2b · Live per-field dialog feedback

**UNBLOCKED 2026-08-24** — G2a's SDK half has landed, so there is now a bound worth surfacing. Re-measured
at the same time: item (a) below is unchanged and is the whole of the gap.

Correcting the original framing: the GUI does **not** discard the descriptor's rules. `ProductDialogViewModel`
already exposes `Rule`, `IsSatisfied` and `RefusalSentence`, so rule feedback partly exists. What is
missing is (a) `Minimum`/`Maximum` participating in `IsSatisfied` and (b) a standard error-presentation
channel.

**The measured gap.** `IsSatisfied` is still `Rule?.IsSatisfiedBy(Value) ?? true`
(`ProductDialogViewModel.cs:81`) and `TryCommit` gates on it (`:416`), so the GUI's own pre-check ignores
the bounds the SDK now enforces. The user-visible consequence is no longer "an out-of-range value
commits" — it is that the refusal arrives from the COMMIT instead of inline while typing, which is
exactly what this row is for.

- [ ] Include the bounds in the field's satisfied/unsatisfied state. Consult the SDK rather than
      re-deriving: the descriptor already carries `Minimum`/`Maximum`, and the comparison the write-back
      makes is the one to mirror — a second definition of "in range" in the GUI is the defect G2a just
      closed, reintroduced one layer up.
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

### V3 · The report opens its own validation run

`FullModeShapes.cs:105` calls `ProjectRules.Validator.Validate(project, ValidationProfile.Categorized)`
directly from `Ihc.Vis.Reporting` — a **second pipeline**, against the commitment `ProjectRules` states in
its own doc comment (*"every lifecycle gate … reads the findings of a single run of these rules, never a
second pipeline with its own rule set"*).

It is not merely a duplicate run, it is a **weaker** one. `ProjectAppService` runs
`ValidationProfile.Categorized with { Library = library.Value }` (now `CategorizedProfile`); the report
runs the bare `Categorized`. Every `RequiresLibrary` row — today `logic-block-locked-content`, a
`ValidationCategory.Logic` row — is therefore **skipped in the Fuld appendix** even when the report is
generated through the service that holds the catalog. The appendix and the verification dialog can
disagree about the same project, and each new declared context (D27) re-opens the divergence.

There is a cost on top of the correctness gap: all three builders add the appendix
(`InstallationReportBuilder.cs:140`, `FunctionsReportBuilder.cs:57`, `FunctionBlockReportBuilder.cs:97`),
so exporting the three Fuld reports validates the same **immutable** project three times end to end.

The same shape appears one layer down: `ProjectVerification.Structural`/`Categorized`
(`ProjectVerification.cs:30,35`) remain shipped context-less statics that skip every context-declaring
row, which is why `ProjectAppService` had to grow private `StructuralProfile`/`CategorizedProfile`
properties beside them. Three call paths, three library answers.

- [ ] Thread the facade's findings (or its profile) into `GenerateReport` so the appendix renders the run
      that already happened, rather than opening one. Memoising per `Project` instance is a legitimate
      cheaper variant — `Project` is immutable, so the result cannot go stale.
- [ ] Decide the fate of the two context-less statics: make the context part of profile construction
      (a `ValidationProfile.For(catalog)`-style factory), or delete them so a caller must state what it
      can supply.
- [ ] Reproduce first: a project with a locked library block, validated through the facade and rendered
      to a Fuld report, must report the row in **both**.

⚠️ Changing what the appendix contains **moves the `full-*` report oracles**. Treat the oracle move as
part of the fix, not as a surprise — and follow the regeneration procedure rather than hand-editing.

### V6 · The `edit.*` family is outside the drift gate

Four refusal families expose `RefusalIdentity` (template + declared `{slot}`s + binding) and are swept
**reflectively** by `RefusalLabelDriftTests.Identities()` (`:31-34`), which walks every registry type in
the assembly. That gate's own doc records why the load family was converted: *"its registry exposed bare
codes … it now exposes whole identities like its three siblings, so the gate is universal."*

It is not universal. `EditRefusalCodes.cs` contains **no `RefusalIdentity` at all** — it exposes ~40 bare
`ProblemCode`s, and `EditRefusalProblems` builds their Danish with C# interpolation. The reflective sweep
cannot see any of it, and this is the **largest** family.

The substitute is `RefusalLanguageTests.ACatalogueTemplateSaysWhatItsRefusalSiteSays` (`:528`), a
hand-maintained sample of about a quarter of the family — and several of its sentences are **retyped as
string literals in the test** (`(EditRefusalCodes.TerminalMissing, "Klemmen findes ikke længere.")`),
which makes the test a *third* copy of the words rather than a comparison of the two that exist. Roughly
thirty Danish sentences are duplicated between the catalogue and the session with nothing comparing them,
and the sampling list has to be remembered on every new `edit.*` code.

- [ ] Convert the edit family to `RefusalIdentity` with slot templates, like its four siblings. The
      reflective gate then covers it with **no new test**, and the hand-maintained sample can be deleted
      rather than extended.
- [ ] While converting, fold in the second spelling: `EditRefusalProblems` binds some sentences by
      interpolation (`:88, 92, 99, 103, 116`) and others through `ProblemTemplate.Bind` (`:123-129`), so
      one family substitutes values two ways. `ProblemTemplate` lives in `Ihc.Vis.Problems`, which the
      session layer already depends on — the layering rule is untouched by this.

*Not* in scope: the deliberate duplication of a Danish sentence beside its refusing site. That is the
documented layering rule (`Session`/`Io` must not depend on `Validation`); this row is about the sentences
the gate cannot **check**, not about the copies themselves.

### V9 · The utility loses a validation failure's findings

`ProblemConsoleFormat.Describe(Exception)` (`:71`) matches only
`IProblemCarrier { Problems: { } chain }`. But `ProjectValidationException` deliberately publishes
`IProblemCarrier.Problems => null` (`ProjectValidationException.cs:55`) and exposes its content as
`IProblemCarrier.Aggregate` instead (`:58`) — the two members cannot share a name.

So the one exception shape that carries a *list of findings* falls through to `error.Message`, and the
`ihc_project_download_upload` utility prints a bare sentence for a failed validation while every finding
behind it is discarded. This is precisely the defect `RaisedProblemDisplay` exists to prevent on the GUI
side, reproduced here because the shape decision was **copied rather than shared**.

- [ ] Reproduce with a test: a project failing validation, rendered through `ProblemConsoleFormat`, must
      list its findings.
- [ ] Handle the aggregate case. Better than adding a second branch: lift the shape traversal
      (chain → cause; aggregate → head + items; carrier → which) onto the problem layer as a small
      visitor, leaving each medium only its **decoration** (`[code]` brackets, argument tail). The
      catalogue doc's argument that a console is a different *medium* justifies different decoration — it
      does not justify a second copy of the composition rules.

### V8 · Two shell sites bypass the presenter

`MainWindowViewModel.UserFacingRefusal` (`:560-568`) goes out of its way to route a status-bar refusal
through `ProblemPresenter`, with the reason written at the site: *"so a refusal shown in the status bar
carries the same bracketed identity it carries in a dialog (R18)."*

`ProgramAuthoringCoordinator.cs:436` and `:450` do not — they pass `blank.Message` / `refusal.Message`
straight to `setStatus`. These are the only two sites in the shell that skip the presenter, so the same
refusal is identified on one surface and anonymous on another.

- [ ] Give `setStatus` a `Problem` rather than a `string`, so the rendering happens once inside the shell
      and a coordinator cannot format its own. That fixes the class, not the two instances.

⚠️ This **changes user-visible text** (the bracketed identity appears where it did not). Check
`RefusalIdentitySurfacesTests` and the message-site register before assuming it is free.

### V1 · Catalogue entry factories

`ProblemCatalogEntries.*` declares ~192 entries as hand-spelled positional constructors. All 49
refusal/outcome entries repeat the identical five arguments (`ProblemCatalogSection.OperationOutcomes`,
`null`, `CatalogDisposition.Refusal`, `RuleKind.*`, `RuleFaces.None`, `default`,
`FindingShape.OneFinding`); the 141 finding entries repeat `ProblemCatalogSection.ProjectFindings` 141
times, `RuleKind.UserContentRule` 118 times, `RuleFaces.WholeProject` 122 times and
`FindingShape.OnePerOccurrence` 91 times. That is roughly **900–1000 lines** of boilerplate, and because
the repeated arguments are four *distinct* enums, a copy that transposes two of them still compiles.

The fix already exists in this codebase and was written for exactly this reason — in the **host**:
`HostProblemCatalog.Outcome(code, template, diagnostic, params slots)`
(`applications/ihc_openvisual/Services/HostProblemCatalog.cs:237`), whose doc says *"fifteen literal
repetitions of the same five arguments would only be fifteen chances to get one wrong."* Its fifteen
entries are 3–5 lines each. The SDK needs the same two or three shape factories.

- [ ] Add `Finding(...)`, `EditRefusal(...)` and `Outcome(...)` factories to `ProblemCatalogEntries`,
      mirroring the host's, with optional `target:`/`faces:` named arguments for the rows that differ.
- [ ] Convert the three partials. Do it **mechanically and in one commit per partial**, and require the
      catalogue index (`ihcclient/docs/problem-catalogue.md`, generated from the declarations and compared
      by a test) plus `rule-characterization.txt` to be **byte-unchanged**. Any movement means the
      conversion changed a declaration, which is the whole risk.

Deferred from the 2026-08-24 pass purely on **size** — it is the largest single duplication left, and it
is mechanical, not subtle.

### V2 · The entry `Target` is undeclared

`ProblemCatalogEntry.Target` is the declaration that says *what a row is about*. Exactly two entries in
the SDK declare a real one; every other row passes `default`, i.e. whole-project — including rows that are
demonstrably about one `(tag, attribute)` pair.

The consequence is that the fact lives somewhere else instead. `DocumentationRules` keeps it in a private
`ImmutableDictionary<string, string>` keyed by the literal code string
(`["doc-cabletype"] = "cabletype"`), and `NamingRules` / `DocumentationCompletenessRules` keep their own
`private const` copies of `documentation_tag`, `cablenumber`, `power_group`.

So `RuleSet.ForTarget`, `FieldMetadataFace.DescribeField`/`ConstraintsOn` and the `UnknownTarget`
registration guard are **vacuous for every shipped row but one**. The second engine face cannot answer for
any field a dialog actually edits, and the registration check passes by construction. Any future
"which rules govern this field" question — a tooltip, a coverage test, a field-level filter — has nothing
to read.

- [ ] Declare `Target` on the rows that have one, starting with the eight `doc-*` rows whose attribute is
      already written down in `ProductAttributes`.
- [ ] Have the rules read `entry.Target.Attribute` and delete the parallel maps and consts.
- [ ] Re-point `FieldMetadataFaceTests` at a real shipped row rather than a synthetic one, so the face is
      gated on shipped data.

Declaring a target is **independent of the body kind** — ARCHITECTURE's exemption covers migrating bodies
to `Constrain` (which moves oracles), not declaring the target on a traversal row, which moves nothing.
Expect `rule-characterization.txt` to be unchanged; if it moves, something else changed with it.

### V5 · The duplicate scan written eight times

`NamingRules.cs:144` and `:175`, `EnumDefinitionRules.cs:80` and `:115`, `FunctionBlockShapeRules.cs:127`,
`ProgramShapeRules.cs:149`, `ModuleAddressRules.cs:156`, `DeviceAddressRules.cs:141` are eight bodies of
one shape: an ordinal `Dictionary<TKey, ProjectElement> seen`, a skip for a blank key,
`TryGetValue` → `ReportGroup(current, [first], …)` else store. They differ only in scope, key selector and
arguments — and they already disagree in small ways.

`IdAnalysis` is a ninth implementation, and its doc comment argues that stating *"first holder wins, in
document order"* **once** is load-bearing — *"a rule re-deriving it would be a second answer to which
element is the duplicate."* The eight rule-local copies undo that for every key that is not an id.

- [ ] Add one authoring form — `RuleBuilder.DuplicatesOf(scope, keyOf, argsOf)`, or a
      `ReportFirstWinsDuplicates` helper beside `RuleAuthoring`. Each of the eight call sites collapses to
      about three lines and the blank-key rule is stated once.

This is the case `RuleBuilder` exists for, and none of the eight uses it. Behaviour-preserving:
`rule-characterization.txt` must not move.

### V4 · Facts recomputed per rule within one run

The `IProjectAnalyses` contract is *"the analyses one run computes AT MOST ONCE, which any rule may
read"*, and the 2026-08-24 pass finished its element-walk half. Three shared facts are still missing, so
the work is done **per rule** instead:

- `ModuleAddressRules.Modules()` (`:195`) scans every element and parses every data-line address to group
  terminals into modules. `ModulePartial` and `ModuleMixedLocality` each call it — the whole grouping,
  address parsing included, twice per run.
- `ProgramDataflowRules.Collect(...)` builds two reference-keyed `HashSet` maps of triggers and writes;
  `SelfTrigger` and `ContendingWriters` each call it — twice per run, identically.
- `ProgramDataflowRules.Written(...)` (`:353`) scans **all** usages once **per variable**, driven by
  `FlagNeverCleared` and `CounterNeverReset`; `TimerNeverStarted` (`:139`) does the same shape inline.
  On U usages and V variables that is 3 × O(V·U) — on the order of a million reference comparisons on a
  large project. `TriggerAncestors` (`:238`) is likewise recomputed from scratch for every writing program
  of every contended variable.

- [ ] Decide whether these become members on `IProjectAnalyses` / `IProgramUsageAnalysis` — the sanctioned
      home for a shared fact — accepting that both are **public API** and the addition moves
      `PublicAPI.Unshipped.txt`.
- [ ] If so: a `WritesTo(variable)` lookup built in the walk that already produces the usages collapses
      all three dataflow items at once.

Deferred from the 2026-08-24 pass because it is a **public-interface** change, and because
`IProjectAnalyses`'s own doc says an analysis arrives *with the rules that need it* — adding three is a
design call, not a cleanup. Note the measured context before pricing it: `ValidateCategorized` is ~12.6 ms
on the largest authentic project, so this is about **allocation and clarity**, not a user-visible stall.

### V7 · The session refusal channel drops its arguments

`EditVerdict.Refuse(code, reason)` (`SessionCoreTypes.cs:45`, and the same shape on `EditOutcome` and
`PreviewOutcome`) carries a code and a loose string, while every other refusal channel in the SDK carries
a whole `Problem`. `EditRefusalProblems` already exposes typed `Problem` factories, but the session's
shared guards reach for the sibling `*Refusal(string)` helpers instead.

The shell then **re-assembles** the value at the presentation site:
`new Problem(outcome.Code, outcome.Reason ?? "", EquatableArray<ProblemArgument>.Empty)`
(`MainWindowViewModel.cs:566`). The declared arguments are dropped for the whole session refusal channel,
so a host cannot group or re-render by argument, and a presentation path is assembling a value the
producer already had. Every new consumer of a refusal repeats the reconstruction.

- [ ] Add `EditVerdict.Refuse(Problem)` (and the `EditOutcome`/`PreviewOutcome` equivalents), keeping the
      string overload only for the sanctioned host-without-a-family case.
- [ ] Delete the reconstruction in `MainWindowViewModel`.

Pairs naturally with [§V6](#v6--the-edit-family-is-outside-the-drift-gate) — converting the edit family to
`RefusalIdentity` is what gives these sites a `Problem` to pass.

### V13 · A thrown rule's classification is invented

`WholeProjectValidator.cs:155-156` turns a rule that throws into `ValidationSeverity.Error` +
`entry.Category ?? ValidationCategory.FileIntegrity`, hard-coded — while `internal.unexpected` is declared
`Refusal` with no category. The two facts the catalogue exists to own are decided **in the executor** for
this one case. The same `?? FileIntegrity` sits on the normal path (`:175`), where it would silently
mislabel any content entry that forgot a category.

`CatalogInvariants.CategoryMisplaced` would catch that — but `ProblemCatalog.From` never runs it. The
catalogue is validated only by a test, whereas `RuleSet.Create` validates at composition. The two halves of
one gate are enforced at different times.

- [ ] Declare an entry for *"a rule threw"* carrying its own category and Error disposition, read through
      `SeverityFor` like every other row.
- [ ] Run `CatalogInvariants.Check` at catalogue construction, so the entry set is gated where the rule set
      already is.

Small related residuals in the same executor, worth folding in rather than their own rows:
`Build` binds the **English diagnostic** eagerly for every finding although it is documented as never shown
to a user; and `SortKey` runs a `string.Join` per emission for a tiebreak consulted only after scan
position, code and locator have all tied.

### V10 · Rule-test scaffolding

The new rule-test files under `tests/safe_project_tests/problems/` each paste the same four helpers:
`Validate(project)` and `Count(project, ruleId)` in **14** files, `Token(tag, counter)` in **19**, and
`Authentic(file)` in **14** — roughly 150 lines of identical scaffolding. The locality builder
(`Tree.Node("groups", Token("groups", 0x20), …)`) is pasted about a dozen times with the *same* magic
counters.

Each `Validate` also constructs a fresh `ProjectAppService`, so a 30-case file builds 30 services and 30
catalogs. A change to how these suites reach the engine is 14 edits with nothing failing if one is missed.

- [ ] Put `Validate`, `Count`, `Token`, `Authentic` and a `Locality(...)` builder in one
      `RuleTestHarness` beside `tests/safe_project_tests/helpers/Tree.cs` — where `Tree.WithRoot` already
      lives, added in this same branch for the same reason and stopping one step short. Share one
      `ProjectAppService`.
- [ ] Leave the per-file fixture builders (`Dimmer`, `Modem`, `Block`) alone — those are the test content,
      not scaffolding.

Also: `HostProblemCatalogTests.RepositoryRoot()` (`:406`) is a line-for-line copy of
`TestRepository.RequireRoot()`, and `TheCatchAllSentenceIsWrittenOnceInTheApplication` (`:200`) walks the
live checkout with a hand-rolled `bin`/`obj` filter — while `safe_visual_tests.csproj:52-56` copies the GUI
sources to `appsrc/` *specifically* so no test walks the checkout, which is how
`MessageSiteRegisterTests.cs:252` does it.

### V11 · Four unreferenced severity icons

`applications/ihc_openvisual/Assets/severity-{error,fatal,info,warning}.svg` were added in this branch.
They match the house style exactly (24×24 viewBox, `fill="none" stroke="currentColor" stroke-width="2"`,
`aria-hidden`, named ids) and are packaged as Avalonia resources — but **no `.cs`, `.axaml` or doc
references them**, and neither `docs/icons_design.md` nor `docs/icon_codes.md` was updated.

They ship in the binary as dead weight, and when a findings pane eventually needs them a second set is as
likely to be authored as these are to be found. They look **staged for planned work** rather than
abandoned, which is why this is a decision and not a deletion.

- [ ] Owner call: **wire them up** — register as constants on `NodeIcons.cs:14` (the existing `/Assets/*.svg`
      registry) with a `Severity(ValidationSeverity)` selector mirroring `ControllerConnection(bool)`, and
      add the rows to `docs/icon_codes.md` — or **delete them** until the pane that needs them exists.

### V12 · Appendix order as a rank list

`DocumentationRules.cs:46-59` declares two literal `ProblemCode[]` arrays fixing the documentation
appendix's print order, and `FullModeShapes.cs:93-100` ranks by `IndexOf`, sorting anything unlisted to
`int.MaxValue`. The DOCUMENTATION category is wider than those eight codes — every `name-*` row is in it —
so the mechanism is explicitly "special cases plus a fallback".

Two costs: adding or reclassifying a DOCUMENTATION row silently lands it at the end of the appendix and
moves a byte-pinned oracle, with nothing at the rule site saying so; and a rule module now exports a
**rendering** fact for one consumer.

- [ ] Consider a declared rank (or an explicit "unranked") on the catalogue entry, so a new row states its
      appendix position in the same declaration that states its category — the pattern the entry already
      uses for every other cross-cutting fact.

Kept as an `Idea`: the present arrangement is *documented* as a report-parity fact declared where the
checks are, and `AppendixUnrankedOrderTests` already pins the fallback. Promote it only if a third
consumer appears or a reclassification actually surprises someone.

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
  supported.** Timeouts are *not* covered by this and stay in scope
  ([§A1b](#a1b--http-timeout-on-the-controller-path)).
- **No `CancellationToken` sweep across the SDK surface** (ruled by ADR-001, broadened 2026-08-24). An SDK
  compute door is synchronous and thread-agnostic; the **host** offloads it under ADR-001's five-step
  contract and honours the token there. The alternative — growing `…Async(snapshot, CancellationToken)`
  doors on the facade — was weighed and rejected: it spends the caller's thread-pool thread unasked, makes
  `Async` mean two different things on one type across a shipped public-API baseline, and publishes a
  promise the engine cannot keep mid-run. The standing rule is **never publish a `CancellationToken` on a
  door that ignores it.** Cancellation is therefore *coarse* — honoured between runs, not mid-run — and
  finer cancellation inside the validation executor is an increment to buy **when measurement justifies
  it**, cheapest first (a check between rules in the executor's own loop, one file; only then a token
  threaded into the two dozen `*Rules.cs`). Measurement as of 2026-08-24 does **not** justify it:
  `ValidateCategorized` is 12.6 ms on the largest authentic project, and `ControllerCapabilityLimits` caps
  how large a project can get. Re-open on a measured figure, not on a consistency argument. Transport
  timeouts are *not* covered by this and stay in scope ([§A1b](#a1b--http-timeout-on-the-controller-path)).
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
- **The generated SOAP layer stays public** (ruled 2026-08-17;
  `docs/adr/ADR-003-generated-soap-layer-stays-public.md`). `dotnet-svcutil --internal` was tried end to
  end, not reasoned about: it builds warning-free and then fails at runtime. `XmlSerializer` binds only
  public types, and the SDK hands it the generated message wrappers on every call — so even
  `RequestEnvelope<T>` cannot construct a serializer. Friend assemblies do not lift that check, which
  precedes serializer codegen. The `WS*` data types would stay public regardless, for the same reason.
  The boundary therefore stays in signatures and dependency direction: the architecture tests, plus a
  repo-wide compile-time ban on `N:Ihc.Soap` (`BannedSymbols.txt`, applied by `Directory.Build.targets`).
  A project that genuinely needs the layer opts out with `<UsesGeneratedSoapLayer>true</UsesGeneratedSoapLayer>`
  in its own file — there are three, and that property is how you find them. The layer is deliberately
  **not** recorded in `PublicAPI.*.txt`: the baseline states what the SDK promises, and this is not it.
  **The premise expires if the wire path stops using `XmlSerializer`** — or before `ihcclient` is
  published as a NuGet package, which would turn a source-level detail into a distributed contract.
- **The declarative constraint vocabulary is deliberately ahead of its callers** (2026-08-24). A cleanup
  review flagged `ConstraintSequence` as an abstraction used once — `RuleBuilder.Constrain` has a single
  SDK caller (`addr-modem-phonenumber-malformed`) and the sequence overload has none — and proposed
  collapsing `RuleDefinition.Constraints` to a bare `IValueConstraint?`. **Do not.** The type documents
  itself as *"authored and reserved"*, and `RuleBuilder`'s doc records the flip conditions for the larger
  question it belongs to (adopt a validation library if the catalogue grows a large population of
  per-element value predicates known at COMPILE time, or if rules must become asynchronous). Collapsing
  the shape now would have to be undone by the second constraint on one code. Note the *related* item that
  IS live: the rows that could be declarative are not, because their entries declare no target — that is
  [§V2](#v2--the-entry-target-is-undeclared), and it is about the declaration, not the vocabulary.
- **A refusing site repeats its Danish sentence beside its code, on purpose** (restated 2026-08-24). Three
  independent reviewers flagged this as duplication in one pass, so it is recorded here rather than
  re-argued each time. `Ihc.Vis.Session` and `Ihc.Vis.Io` must not depend on `Ihc.Vis.Validation`
  (enforced by `tests/safe_architecture_tests/ValidationLayerArchitectureTests.cs`), so a site below the
  engine cannot read the catalogue and carries its own copy, kept equal by a drift test. What is **not**
  settled is whether that drift test actually covers the copy — for the largest family it does not, which
  is [§V6](#v6--the-edit-family-is-outside-the-drift-gate).
- **The GUI's `ConfigureAwait` and `Process.Start` bans stay architecture tests** (ruled 2026-08-17;
  `docs/adr/ADR-004-compile-time-bans-over-architecture-tests.md`). Moving them to banned-symbol entries
  was proposed and rejected on that ADR's own test: neither is a complete ban. `ConfigureAwait` is declared
  on `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` and the awaitable extensions, so an entry list would
  need one line per declaring type and would not cover a new awaitable at all — where the IL scan bans the
  member by NAME and covers every declaring type, present and future. `Process.Start`'s overloads are
  enumerable but equally a fixed list. Both are also documented as admitting no exemption, and a
  banned-symbol entry can be waived at the call site with a suppression comment. ⛔ **Never apply either
  ban to `ihcclient`** — the SDK uses `ConfigureAwait` deliberately and pervasively (239 occurrences
  across 22 files). Six other GUI prohibitions did migrate; see the ADR for which and why.
