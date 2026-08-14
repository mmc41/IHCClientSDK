# TODO — backlog

One row per open item. **Detail lives in the sections below the table**, deeper rationale in the linked
`tmp/` analyses. Standing constraints are at the bottom — they are rules, not work, and have no status.

**Status vocabulary:** `Todo` accepted, not started · `Needs decision` blocked on an owner ruling ·
`Blocked` waiting on evidence or another piece of work · `Idea` not committed · `Verify` believed done,
needs confirming.

**Kinds:** `Defect` something is wrong · `Task` accepted work · `Decision` owner call ·
`Oracle` vendor-measurement session · `Idea` unconvinced.

## Backlog

Ordered by priority within each group.

| ID | Item | Kind | Status | Detail |
|----|------|------|--------|--------|
| **T1** | `UserManagerService.GetUsers` applies its redaction conditional in the **opposite direction** from its own comment | Defect | Todo | [§T1](#t1--usermanagerservicegetusers-redaction-is-inverted) |
| **T2** | Configuration services attach **raw** WLAN/SMTP/email-control models to activity tags; their `ToString()` reveals secrets | Defect | Todo | [§T2](#t2--raw-secret-bearing-models-on-activity-tags) |
| **G1** | The vendor app can edit a `.vis` while OpenVisual has it open; the save silently overwrites it | Defect | Todo | [§G1](#g1--external-modification-of-the-open-file) |
| **A1** | No `CancellationToken` on the async surface and no HTTP timeout — a wedged controller hangs every caller | Defect | Todo | [§A1](#a1--cancellation-and-timeouts) |
| **A2** | The controller half of the SDK has no test that runs anywhere — no recorded-wire fixtures, `safe_integration_tests` never executed | Task | Todo | [§A2](#a2--soap-fixture-corpus--replay-harness) |
| **B1** | Tracing but no metrics — add a `Meter` beside the existing `ActivitySource` | Task | Todo | [§B1](#b1--metrics-beside-the-activitysource) |
| **B2** | The library's public surface is not pinned; Invariant 9's SOAP-leak gap is neither fixed nor decided | Task | Todo | [§B2](#b2--pin-the-public-api-surface) |
| **B4** | Refusals carry a bare Danish string while validation findings carry a `RuleId` | Task | Todo | [§B4](#b4--refusal-codes) |
| **B3** | `IProjectDocument` has `Close()` but is not `IDisposable` | Task | Todo | [§B3](#b3--iprojectdocument--idisposable) |
| **G2** | Product-dialog fields validate only on OK, though the descriptor already computes each field's rule and range | Task | Todo | [§G2](#g2--live-product-dialog-field-validation) |
| **T3** | HTTPS certificate identity is not authenticated (`DangerousAcceptAnyServerCertificateValidator`) | Decision | **Needs decision** | [§T3](#t3--https-certificate-trust-boundary) |
| **D1** | Rule on the two US-068 residuals (log-mark scope; stop-point / jump-to leaf routes) | Decision | **Blocked** — needs T018's Discoveries entry | [§D1](#d1--us-068-residuals) |
| **O1** | PG-5 enum-editing oracle session — capture the value-id reallocation rule | Oracle | Todo | [§O1](#o1--pg-5-enum-editing-oracle-session) |
| **R1** | Model-driven report rendering (option B: generic shape document + GUI shape interpreter) | Idea | **Verify** — appears superseded | [§R1](#r1--model-driven-report-rendering) |

**Not in this table:** three further architecture gaps (nullable-in-SDK, benchmarks, coverage/mutation
testing) stay in [`tmp/agaps.md`](tmp/agaps.md) unpromoted. Two items were **ruled out** on 2026-08-14
and should not be re-raised without new evidence — see
[Standing constraints](#standing-constraints--do-not-reopen-without-new-evidence).

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

### A1 · Cancellation and timeouts

428 public `Task`-returning member declarations under `ihcclient/src/api/services` and **zero** accept a
`CancellationToken`; `ProjectAppService` is 11/0. The token exists only on the two
`GetResourceValueChanges` streaming methods. `PostAsync` is called without one and no `HttpClient.Timeout`
is set, so the 100 s default is the only bound and nothing can abort an in-flight call.

- [ ] Add `CancellationToken cancellationToken = default` as the trailing parameter across the API tier,
      the application tier, and `ProjectAppService`'s file/controller operations. Source-compatible.
      **One sweep, not opportunistically** — a half-threaded surface looks like cancellation works.
- [ ] Thread it to the wire (`PostAsync(url, content, cancellationToken)`), and replace the explicit
      `CancellationToken.None` in the polling delay (`src/api/util/services.cs:86`) with the flowed token.
- [ ] Set an explicit `HttpClient.Timeout` from `IhcSettings`.
- [ ] Delete or rewrite the three `// TODO: Retry x times.` comments in `controllerService.cs:800,818,836`
      — they advertise work that will not happen (see the retry ruling in Standing constraints).
- [ ] Update `ARCHITECTURE.md` → Cross-cutting → Async with the cancellation policy.

Evidence and rationale: [`tmp/agaps.md`](tmp/agaps.md) F1.

### A2 · SOAP fixture corpus + replay harness

`tests/testdata/` holds `.vis`/`.def`/`.ifb`/report oracles and no wire fixtures. `safe_integration_tests`
is compiled but never executed. `utilities/ihc_httpproxyrecorder` produces exactly the recordings that
would fix this and **nothing consumes its output** — the fixture-production half was built, the
consumption half was not.

- [ ] Record one request/response envelope pair per operation for a representative subset.
- [ ] **Scrub before committing** — recordings carry credentials, cookies, IPs, serial numbers and a real
      installation's layout. Prerequisite, not a follow-up.
- [ ] Commit under `tests/testdata/soap/` with the same per-file provenance discipline
      `testdataoverview.md` already applies.
- [ ] Add a replay `DelegatingHandler` and run the existing `safe_integration_tests` assertions against
      it in a new controller-free suite — a CI gate that touches no controller and keeps Invariant 5.
- [ ] Handle vendor-response drift the way the `.vis` oracles do: re-record deliberately, never edit.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F9.

### B1 · Metrics beside the `ActivitySource`

`Meter` ships in the same `System.Diagnostics.DiagnosticSource` assembly the SDK already uses for
tracing, so Invariant 7's "no logging dependency" argument does not forbid it — metrics cost **zero**
new package references.

- [ ] Add one `Meter` in `src/config/Telemetry.cs`. Suggested starting instruments:
      `ihc.controller.operation.duration` (histogram; service, operation, outcome) ·
      `ihc.controller.operation.count` (counter; same tags) ·
      `ihc.vis.document.command.duration` (histogram; command, `Ok`/`Refused`/`Failed`) ·
      `ihc.vis.project.load.duration` (histogram; size bucket).
- [ ] Amend Invariant 7 to say what it means: *no logging dependency*; tracing **and metrics** via the
      in-box diagnostics APIs.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F3.

### B2 · Pin the public API surface

Every other architectural rule in this repo is mechanically enforced (ArchUnitNET with positive
controls, generation fingerprints, the verbatim-free gate). The library's own contract is the one left
to prose — and Invariant 9 already admits the leak: generated SOAP types are `public` but must not be
consumed, so vendor WSDL churn would ship as this SDK's breaking changes.

- [ ] Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to `ihcclient`; check in `PublicAPI.Shipped.txt`
      (bootstraps via the analyzer's own code fix). Every surface change then shows up as a diff line.
- [ ] Add `Microsoft.CodeAnalysis.BannedApiAnalyzers` with `BannedSymbols.txt` seeded from bans already
      enforced by IL scan (`ConfigureAwait`, `Process.Start`) — compile-time beats test-time; keep the
      arch tests as the backstop.
- [ ] **Close Invariant 9's gap or record the decision:** either emit the generated SOAP layer as
      internal (the `InternalsVisibleTo` friend list already covers the suites that need it), or accept
      it deliberately as an ADR. It is currently neither.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F4.

### B4 · Refusal codes

`EditRefusedException(string message)` carries a bare Danish sentence; validation findings carry
`(Severity, RuleId, Category, Locator, Message)`. So half of "errors as data" is structured and half is
not: refusals can only be asserted by substring, cannot be re-worded or translated by any frontend, and
cannot be aggregated by cause.

- [ ] Add a stable `RefusalCode` (or reuse the `RuleId` vocabulary) to `EditRefusedException` and the
      `Refused` outcome, keeping the composed Danish sentence as the default message.
- [ ] Move refusal tests onto the code rather than the text.

This **preserves** the Danish-refusal decision rather than reopening it — it makes Danish the default
instead of the only possibility. Evidence: [`tmp/agaps.md`](tmp/agaps.md) F13.

### B3 · `IProjectDocument : IDisposable`

The one-document-per-file rule is important enough to be arch-enforced (*"a second document over one
file splits the undo history and silently loses edits"*), but an exception between `OpenDocument` and
`Close` still leaks the document, and the failure surfaces later at the next open.

- [ ] Make `IProjectDocument : IDisposable`, `Dispose()` delegating to an idempotent `Close()`; keep
      `Close()` as the intent-revealing name. `IAsyncDisposable` only if closing ever needs to await.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F6.

### G1 · External modification of the open file

**Accepted 2026-08-14: save-time identity check.** Saving is correctly atomic (temp + `File.Replace`),
but nothing records the loaded file's identity, so: installer has the project open in OpenVisual →
changes it in IHC Visual (or a backup restores it) → saves in OpenVisual → the external change is
overwritten with no error at any layer. In a project whose whole premise is co-existing with the vendor
tool over the same files, this is the write half of that contract.

- [ ] Capture the source file's identity at load — a content hash preferred over
      `(length, LastWriteTimeUtc)`, which is affordable at these file sizes and immune to
      timestamp-granularity and clock oddities.
- [ ] Re-check it immediately before the atomic replace, inside the same save path.
- [ ] On mismatch, **refuse** the save through the existing Danish refusal channel, offering
      overwrite / save-as / reload. Reuse `EditRefusedException`-style semantics rather than inventing a
      new failure kind (and give it a code once B4 lands).
- [ ] **Never take an exclusive OS lock on the `.vis`** — that would break the vendor tool, which is the
      opposite of the goal.
- [ ] Reproduce first with a test: load, mutate the file underneath, save, assert refusal.

**Scope boundary (decided):** detection is at save time only. A `FileSystemWatcher` for live
notification was considered and **not** taken — it would see the app's own temp+rename writes and needs
debounce plus self-write suppression, which is more to get wrong than the check is worth. Revisit only
if save-time detection proves too late in practice.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F8. Independent of the ruled-out autosave item.

### G2 · Live product-dialog field validation

**Accepted 2026-08-14.** `ProductDialogComposer` already resolves every field's validation rule and
numeric range into the descriptor, and the GUI currently discards both — rules are enforced only on
write-back, so invalid input is accepted into the control and rejected after OK. This adds the display
channel for data that already exists; it does **not** move any rule into the GUI.

- [ ] Have the dialog field view-model implement `INotifyDataErrorInfo`, evaluating the descriptor's
      existing rule/range on change.
- [ ] Keep the write-back refusal as the authority — the SDK-owns-legality boundary is unchanged, and
      the two must not be allowed to disagree.
- [ ] Re-run `AutomationCoverageTests`: validation adornments can add nodes to the peer tree the audit
      walks, and the descriptor-driven dialog is audited populated, once per family.
- [ ] Cover it in `safe_visual_tests` per family, not just for one.

**Known tension, accepted:** vendor parity is this repo's governing default and it was not measured here
— IHC Visual may validate only on OK. The ruling treats live validation as an additive affordance that
changes no write semantics, so parity is not considered binding. If a later vendor session shows a
behavioural difference that matters, that is new evidence.

Evidence: [`tmp/agaps.md`](tmp/agaps.md) F12.

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

### R1 · Model-driven report rendering

Original question (explore, do not implement): *can report generation in OpenVisual be made model-driven
using reflection on the data models supplied by the ihcclient API, possibly extended with attribute
metadata, so report content is not hardcoded in OpenVisual but derived?* Explored 2026-07-21 —
[`tmp/metadrivenreport-ana.md`](tmp/metadrivenreport-ana.md).

The idea was option B: the combined report model (backlog T020) emits a generic shape document
(Table/KeyValue/Outline sections with US-071 option tags) and the GUI becomes a small shape interpreter;
reflection/attributes stay SDK-internal if used at all. To be decided as an amendment to T020 **before**
the reporting phases (Phase 4+) of `tmp/programming-reporting-backlog.md` start — not retrofitted onto
the then-current three report models.

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
  supported.** Timeouts are *not* covered by this and stay in scope (A1). Evidence:
  [`tmp/agaps.md`](tmp/agaps.md) F2.
- **No autosave, command journal, or crash recovery** (ruled 2026-08-14). In-memory-only undo history is
  accepted for an incubating app; commands are deliberately not serializable, so there is no macro or
  scripting surface either. `ARCHITECTURE.md` was corrected to stop claiming one. Does **not** cover G1,
  which is a different defect. Evidence: [`tmp/agaps.md`](tmp/agaps.md) F7.
