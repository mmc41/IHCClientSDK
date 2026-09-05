# Central telemetry points — where enrichment belongs

Developer reference (HOW) identifying the **chokepoints, anchors and common flowpoints** in IHC OpenVisual and
the `ihcclient` code beneath it where OpenTelemetry enrichment can be added *once* and pay off across many
operations — so that bugs and performance problems become visible in the trace rather than only in a bug report.

The behavioural spec (WHAT) is in [`../stories/`](../stories/) and [`../product.md`](../product.md); the layering
this analysis must not violate is in [`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) and
[ADR-001](../../../../docs/adr/ADR-001-threading-and-concurrency-model.md) /
[ADR-002](../../../../docs/adr/ADR-002-thick-sdk-services-and-thin-apps.md).

Symbols are named rather than line-numbered, because line numbers drift and member names do not.

This was an **analysis and proposal**, and §1–§9 are kept as the record of what the analysis found. It has
since been **implemented**; §10 records the outcome, including the two places where what was built differs
from what §2 proposed. Read §10 before treating a symbol in §1 or §2 as current — several moved.

§11 is a later audit of a different question: not where enrichment should go, but whether what was built
actually nests — whether a workflow reads as ONE tree whose root times it. It measures the running
application rather than the source, and it supersedes both earlier sections where they disagree.

§12 reviews §11 against the OpenTelemetry research corpus and re-measures it: an attribute name that collided,
two tests that could not fail, a restore that was never needed, and the `bool` return type that was the real
reason a handled failure never reached the gesture that caused it. It is current over all of the above.

---

## How to review this document

### Method

Every "what exists today" claim was derived by reading the named symbol or by running the shell command printed
beside the claim. A reviewer can re-derive the whole baseline from §1 without reading this document's prose.

The scope walked was: `shared/ihc_appbootstrap/`, `applications/ihc_openvisual/` (all of `Services/`,
`ViewModels/`, `Views/MainWindow.axaml`, `Program.cs`, `App.axaml.cs`), and in `ihcclient/src/`:
`config/Telemetry.cs`, `api/services/`, `api/util/`, `app/services/`, `vis/session/`, `vis/io/`,
`vis/validation/`, `vis/reporting/`, `vis/catalog/`.

**Revision note.** A second review challenged nine claims in the first draft; all nine were re-checked against
the code and all nine held, so this revision narrows or corrects each of them in place. The corrections that
changed a *conclusion* rather than a wording are flagged where they appear: §2 (5) (the command funnel covers
registered rows only, and can supply neither surface nor failure), §2 (7) (interactive lifecycle operations are
already gesture-anchored), §2 (10) (`RaisedProblemDisplay` is not the display chokepoint), §5.4 (the catalogue
bounds `ihc.problem.code`, not `error.type`), and D2 (payload export is a data-exposure problem, not a size
problem). Tier 0 gained the configuration surface the metrics signal needs and did not have.

A later pass added the one thing the first two drafts both missed: **bucket boundaries**. Every histogram in
§5.3 is declared in seconds, and a histogram registered without a `View` inherits a default boundary set whose
first bucket is 5 s — so as originally written this registry would have exported six histograms that resolve
nothing. Tier 0 is a five-part change as a result, and §5.3 now records a boundary decision per instrument.

### Evidence rules used

| Claim type | How it is backed |
|---|---|
| "X exists / does not exist today" | A named symbol, or a reproduction command whose output is stated. |
| "X is a chokepoint" | The set of callers that must pass through it, named. |
| "X would answer question Q" | Q is written out; if the attribute cannot answer it, the gap is stated. |
| "X is a defect" | The rule it breaks is named (OpenTelemetry specification, or a repo invariant), plus the consequence. |
| A prediction not yet measured | Marked **[unverified]** with the experiment that would falsify it. |

### Confidence markers

- **[measured]** — verified by reading the code in this repository at the stated symbol.
- **[unverified]** — a mechanism-based prediction; the falsifying experiment is given.
- **[judgement]** — a design opinion, argued but not derivable from the code.

### Scope exclusions

Not examined: `utilities/ihc_lab` beyond its `Configuration/Telemetry.cs`, the other utilities, the generated
SOAP layer under `ihcclient/generatedsrc/`, collector/backend deployment topology, sampling economics, and the
individual `.vis` validation rule bodies. Cost figures for the proposed instrumentation are **not** given: no
overhead measurement was performed. OpenTelemetry publishes a measurement *procedure* — warm up JIT runtimes,
run at least 15 s per iteration and at least ten iterations, and report average and peak CPU and heap at a
stated throughput — but it deliberately publishes no thresholds, and the one peer-reviewed cross-agent
comparison available is JVM-only and does not port to .NET. So the procedure is borrowable here; the numbers
are not.

---

## 1. The baseline — what exists today

### 1.1 The pipeline

`Ihc.Bootstrap.AppTelemetryBootstrap.SetupTelemetryAndLogging` (in `shared/ihc_appbootstrap/`) is the **single**
telemetry composition root for both Avalonia apps. Called from `ihc_openvisual/Program.cs::Main` and from
`ihc_lab/App/Program.cs`. It builds:

- an `ILoggerFactory` — always Console + Debug, plus an OTLP log exporter when `telemetry.Logs` is configured;
- a `TracerProvider` — only when `telemetry.Traces` is configured — with `SetErrorStatusOnException(true)`,
  a `Resource` carrying `service.name` / `service.namespace` / `service.version`, and
  `AddSource("ihcclient", "IhcOpenVisual")` so SDK and app spans share one trace;
- OTLP over `HttpProtobuf`.

Around it, `Program.cs` wires the startup connectivity probe and the exception layers. The probe goes through
`Ihc.TelemetrySelfCheck.ProbeAsync` — the structured door — because the SDK's report-for-me sibling writes to
`Trace` and `Console.Error`, neither of which a `WinExe` has a reader for; `Program.ReportSelfCheckAsync`
routes the outcome to the log and, when it is a problem, to the fault sink. The exception layers are AppDomain,
Dispatcher, UnobservedTask and the X11/GLib logger in `CreateX11Options`, with `Main`'s own `catch` as the last.

### 1.2 The two ActivitySources

| Source name | Declared in | Version |
|---|---|---|
| `ihcclient` | `Ihc.Telemetry` (`ihcclient/src/config/Telemetry.cs`) | SDK version |
| `IhcOpenVisual` | `ihc_openvisual.Configuration.Telemetry` | entry-assembly file version |

Both are registered on the TracerProvider, so a user gesture and the SDK work it causes land in one trace —
provided the app actually opens a span for the gesture. §2 shows that it frequently does not.

### 1.3 Signals present

| Signal | State | Evidence |
|---|---|---|
| Traces | Present, dense in the SDK, thin in the app | §1.4 |
| Logs | Present, OTLP-exported, Avalonia's own logs bridged in via `ChainedILoggerSink` | `AppTelemetryBootstrap` |
| **Metrics** | **Absent entirely** | `grep -rn "new Meter(\|AddMeter\|MeterProvider" --include="*.cs"` returns no production hit |

There is no `MeterProvider` in the bootstrap and no `Meter` anywhere in the repository. Consequence beyond the
missing data: a `Meter` added today would be **silently unregistered** and collect nothing, because
`AddMeter` is what connects a meter to a provider.

The `OpenTelemetry` package — which contains the metrics SDK — is *already referenced* by
`ihc_openvisual.csproj` and `ihc_appbootstrap.csproj`, and `System.Diagnostics.Metrics.Meter` ships in the .NET
shared framework, so **no new package reference is needed** in either the app or the SDK (`ihcclient.csproj`
references no OpenTelemetry package and does not need one — `ActivitySource` and `Meter` come from the same
shared-framework assembly). **[measured]**

### 1.4 Where spans are minted today

```
grep -rn "StartActivity" --include="*.cs" --exclude-dir=obj --exclude-dir=bin ihcclient/src | wc -l   # 188
grep -rn "StartActivity" --include="*.cs" --exclude-dir=obj --exclude-dir=bin applications/ihc_openvisual | wc -l   # 11
grep -rn "RunTraced"     --include="*.cs" --exclude-dir=obj --exclude-dir=bin ihcclient/src | wc -l   #  29
```

These are **raw grep hits, not operation counts**. The SDK figure includes the two `StartActivity` helper
*definitions* and helper call sites as well as per-operation calls, and the `RunTraced` figure includes its four
definitions in `AppServiceBase`. Read them as an order-of-magnitude comparison, not as "188 instrumented
operations".

**With that caveat, the asymmetry is the headline finding.** The SDK is instrumented almost exhaustively; the
application — the only layer that knows what the user was *trying to do* — has eleven span sites, and the table
below shows they are concentrated in a handful of workflows.

| Mint site | Source | Span name | Kind | What it tags today |
|---|---|---|---|---|
| `Client.LoggingHandler.SendAsync` | ihcclient | `SendAsync` | Internal | method, url, every request/response header, status |
| `ServiceBaseImpl.soapPost` | ihcclient | `soapPost.<action>` | Internal | full request XML, full response XML |
| `CookieHandler.GetCookie` / `SetCookie` | ihcclient | `GetCookie` / `SetCookie` | Internal | — |
| `ServiceBase.StartActivity` (api tier) | ihcclient | `<Service>.<op>` | Internal | `service.name`, `service.operation` |
| `AppServiceBase.StartActivity` + `RunTraced*` | ihcclient | `<Service>.<op>` | Internal | same, plus per-call `retv` |
| `ProjectDocumentSession.ApplyInternal` | ihcclient | `ProjectDocumentSession.Apply` | Internal | `command` (the class name) |
| `CopyUtil.DeepCopyAndApply`, `metadata.GetOperations` | ihcclient | member name | Internal | — |
| `MainWindowViewModel.RunAsync` | app | `MainWindowViewModel.<op>` | Internal | — (error only) |
| `ProjectWorkflow.{Undo,Redo,Rollback,SaveFunctionBlock}Async` | app | `ProjectWorkflow.<op>` | default | — |
| `CatalogImportWorkflow.Import{File,Folder}Async` | app | `<Type>.<op>` | default | — |
| `ProjectReportWorkflow.{ViewInBrowser,SaveAs}Async` | app | `<Type>.<op>` | default | — |
| `ProjectFindingsWorkflow.ExportAsync` | app | `<Type>.<op>` | default | — |
| `AboutWindow.OnRepoLinkClick` | app | `<Type>.<op>` | Internal | — |

### 1.5 Questions the baseline cannot answer

These are the concrete questions that motivate everything in §2. None of them is answerable today.

1. How long does opening *this* project take, and which phase dominates — file read, XML parse, catalog-enum
   normalization, index build, tree build, or the first validation pass?
2. How often does an edit get **refused**, by which rule, and against which command? (A refusal is the product's
   central user-visible event and leaves no trace record at all — §6 D4.)
3. Is the Problemer panel slow because validation is slow, or because projecting findings into rows is slow?
4. Did the tree update *reconcile in place* or silently fall back to a **full rebuild**? The fallback is a
   pure performance regression and is invisible.
5. How many validation runs are started and then thrown away (superseded or abandoned)? Is the 300 ms debounce
   the right number for a real project?
6. Which menu/toolbar commands do installers actually invoke? (Note §2 (5): only part of this is reachable — the
   registry does not own the catalog, authoring or panel commands.)
7. Which project failed to open, and why? A gesture span exists for an interactive Open, but it carries no path,
   and a failure is swallowed inside `ProjectWorkflow.OpenAsync`, so the span reports success.

---

## 2. The chokepoints, ranked

Ranked by **coverage per edit**: how many distinct operations one change instruments. Tier 0 must exist before
any of the metric proposals can collect at all.

| # | Chokepoint | Symbol | Tier | Covers | Instrumented today |
|---|---|---|---|---|---|
| 0 | Telemetry composition root | `AppTelemetryBootstrap.SetupTelemetryAndLogging` | 0 | both apps, all signals | traces + logs only |
| 1 | SDK app-service scaffold | `AppServiceBase.RunTraced` / `RunTracedAsync` | 1 | the whole `ProjectAppService` public surface | span + error only |
| 2 | SDK api-service scaffold | `ServiceBase.StartActivity` | 1 | every controller service operation | span + 2 tags |
| 3 | The one edit pipeline | `ProjectDocumentSession.ApplyInternal` | 1 | **every** edit from every frontend | span + `command` |
| 4 | App error boundary | `MainWindowViewModel.RunAsync` | 1 | commands that opt in | span + error |
| 5 | **The registered-row command funnel** | `CommandRegistry.Register` → local `Execute` | 2 | every route to a *registered shell row* — bar, toolbar, flyout, key binding (**not** the data-driven commands; see below) | **none** |
| 6 | The availability sweep | `CommandRegistry.OnContextChanged` | 2 | every gate of every row, per context change | **none** |
| 7 | Project lifecycle | `ProjectWorkflow.{Start,Open,New,Save,SaveAs,Close}Async`, `SaveToAsync` | 2 | the whole document lifecycle | **none** |
| 8 | Background validation loop | `ValidationWorker.RunAsync` / `Notify` / `AbandonGeneration` | 2 | the app's only off-UI-thread compute | **none** |
| 9 | Validation generation logic | `ValidationMonitor.OnDocumentChanged` | 2 | the edit/save/replacement derivation | **none** |
| 10 | The problem-display surface | `AvaloniaDialogService.ShowProblemAsync` (3 overloads) | 2 | every coded problem shown to the user — exception-borne or not | **none** |
| 11 | UI refresh | `MainWindowViewModel.Refresh` → `TreePaneCoordinator` | 2 | every document transition's UI cost | **none** |
| 12 | Findings → rows | `ProblemsPanelViewModel.Bind` | 2 | every panel refresh, on the UI thread | **none** |
| 13 | Deep engines | `ProjectReader.Read`, `ProjectSerializer.Serialize`, `ProjectIndex.Build`, `ProjectChangeSet.Diff`, `WholeProjectValidator.Validate`, `ReportGenerator.Generate`, `BuiltInCatalog` lazy | 3 | the phases inside the above | **none** |

### Tier 0 — the process anchor

**`AppTelemetryBootstrap.SetupTelemetryAndLogging`.** Everything below is downstream of this one method. It is
shared by `ihc_openvisual` and `ihc_lab`, which is exactly why it is the right place: an anchor that only one
app has is not an anchor.

What belongs here and nowhere else:

- A **`MeterProvider`**, anchored in a static the way `TracerProvider` already is (the existing comment on that
  field — "must be kept alive … otherwise it can be GC'd (silently stopping export)" — applies verbatim to a
  MeterProvider, and `Program.cs`'s `finally` already disposes the tracer, so the meter disposal has an obvious
  home). Registered with `AddMeter` for both meter names, or metrics collect nothing.
- **Attribute limits.** No `OTEL_*` variable is configured anywhere in the repository
  (`grep -rn "OTEL_" --include="*.cs" --include="*.json"` → nothing). The specification default for
  attribute *count* is 128 per record; the default for attribute *value length* is **no limit**. §6 D2 shows
  the concrete consequence. A value-length limit set here fixes it for every span in both apps at once.
- **Resource identity** the queries will want: a deployment/environment marker and a `service.instance.id`, so
  two machines' traces are separable.
- **Exemplar filter** (default `trace_based`), so a latency spike on a histogram leads to a concrete trace.

#### Tier 0 is not one edit — the metrics signal needs a configuration surface it does not have

A `MeterProvider` with no exporter collects in-process and exports nothing. `TelemetryConfiguration`
(`ihcclient/src/config/Telemetry.cs`) declares only `Host`, `Traces`, `Logs`, `Headers` and the two self-check
fields, and `AppTelemetryBootstrap` wires OTLP exporters for exactly the two signals that have endpoints. So
step 1 is a **five-part** change. Omitting any of (a)–(d) produces a pipeline that looks wired and exports
nothing; omitting (e) produces one that exports diligently and cannot resolve anything:

| Part | What | Where |
|---|---|---|
| a | A `Metrics` endpoint property (empty ⇒ metrics disabled, mirroring how `Traces`/`Logs` already gate their exporters) | `TelemetryConfiguration` |
| b | `AddOtlpExporter` for metrics through the existing `ConfigureOtlp` helper, plus `AddMeter` for **both** meter names | `AppTelemetryBootstrap.SetupTelemetryAndLogging` |
| c | The new key documented in the shipped templates | `ihcsettings_template.json`, `ihcsettings_example.json` |
| d | A `Metrikker:` line beside the existing `Log:` / `Spor:` / `Selvtjek:` rows, so an installer can see whether metrics are configured | `MainWindowViewModel.BuildSettingsText` |
| e | An `AddView` per duration histogram, declaring its bucket boundaries — see below | `AppTelemetryBootstrap.SetupTelemetryAndLogging` |

**Histogram bucket boundaries — the part most easily skipped, and the one that fails quietly.** A `Histogram`
registered with no `View` inherits the specification's default explicit boundaries: `0, 5, 10, 25, 50, 75, 100,
250, 500, 750, 1000, 2500, 5000, 7500, 10000`. Those numbers are **unitless** — the instrument's declared unit
is not consulted. §5.3 declares its durations in **seconds**, so the first boundary is 5 s, and every operation
this document proposes to time lands in the first bucket; the interactive ones (`ihc.edit.apply.duration`,
`ihc.ui.tree_update.duration`, `ihc.ui.context_rebuild.duration`) land there by orders of magnitude. A p95 read
off that histogram is not merely imprecise, it is unrecoverable: the raw values are gone at export time, so no
query can repair it afterwards. The failure is silent — the export succeeds and the graph is flat.
**[measured / unverified]** — the boundary set is the specification's stated default, but that these operations
run far below 5 s is a prediction, falsified by any recorded run in which an edit apply or a tree update
exceeds five seconds.

Nor is the obvious alternative right: the HTTP semantic convention's recommended seconds boundaries bottom out
at 5 ms, which is still above a plausible edit apply.

The rule this document adopts: **every histogram in §5.3 declares its boundaries, and the default is
exponential** — `Base2ExponentialBucketHistogramConfiguration` (present in the pinned OpenTelemetry 1.16.0, per
`Directory.Packages.props` **[measured]**), which auto-rescales and so needs no range known in advance. That is
the right default precisely because these operations span at least four orders of magnitude and their real
ranges have never been measured. A row moves to `ExplicitBucketHistogramConfiguration` only once a *declared*
threshold exists to align to — not before, because inventing boundaries is inventing an SLO.

> **DEVIATION, on the acceptance spike's evidence: the implementation uses EXPLICIT boundaries, not
> exponential.** The rule above is sound about the instrument and wrong about this backend. Measured against
> the configured OpenObserve: an exponential histogram is *ingested* and its `_sum` and `_count` are exact,
> but its `_bucket` rows carry a bucket **index** in the boundary column — observed range −47…63 for values
> spanning 0.004–44 s — and **no scale or base is exported anywhere on the row**, so the indices cannot be
> converted back to seconds by any query. The distribution is therefore unreconstructable, and percentiles
> and heatmaps are underivable. It also amplified rows about tenfold: 8 measurements produced 108 bucket rows
> against 11 for the explicit form.
>
> The boundaries in force are the fallback S4.5 pre-declared, in seconds:
> `0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30`. They are applied at the composition root by one view
> matching `*.duration` across both meters, so a histogram added later inherits them rather than silently
> inheriting the unitless default this section warns about. The naming convention is what makes that
> wildcard exact.
>
> This does not retract the reasoning above — inventing boundaries *is* inventing an SLO, and these are
> invented. It records that the alternative is unqueryable here, which is a worse failure than an imprecise
> bucket edge: an imprecise boundary gives a wrong number, an unreadable one gives none at all. Revisit if
> the collector ever exports the scale alongside the index.

Exponential also protects comparisons. Explicit-bucket histograms merge only across **identical** boundary
sets, so re-tuning boundaries between a baseline and a candidate run invalidates the comparison without
reporting an error; exponential histograms downscale to merge and cannot fail that way.

**Meter names and versions.** Mirror the two `ActivitySource`s exactly rather than inventing a parallel
vocabulary: `ihcclient` (SDK version) and `IhcOpenVisual` (entry-assembly file version). One name per emitting
layer, matching what the TracerProvider already registers, so `AddSource` and `AddMeter` never drift apart.

**Export interval.** The metrics export interval (default 60 s) sets the floor on how stale a metric can be. For
a desktop session that is a freshness choice worth making explicitly, not inheriting.

**Self-check.** `TelemetrySelfCheck` probes one endpoint. If metrics get their own endpoint, the existing
`SelfCheckEndpoint` no longer validates the whole pipeline — either point it at a URL common to all three
signals, or accept that a metrics endpoint typo fails silently the way the doc-comment on that field says a
wrong traces endpoint used to.

Cost of getting this wrong: zero metrics if (a)–(d) are incomplete, metrics with no resolution if (e) is —
silently, in both cases. Verification: both UI suites plus `safe_architecture_tests`, because this assembly is
shared.

### Tier 1 — the four scaffolds that already exist

These need **no new spans** — only enrichment inside a method that already runs for every operation. Highest
value per line changed.

**(1) `AppServiceBase.RunTraced` / `RunTracedAsync`.** The SDK's "StartActivity + try/catch + SetError scaffold,
once", and `ProjectAppService` routes its whole public surface through it. One body change enriches load, save,
create, validate, report, export, download and upload simultaneously.

What to add centrally:
- a duration **histogram** keyed by operation name (bounded: the method names of one class);
- `error.type` derived from the caught exception — and crucially, when the exception implements
  `Ihc.Vis.Problems.IProblemCarrier`, from its **`ProblemCode`** rather than its CLR type name. That is the
  repository's own bounded, *declared* error vocabulary; see §5.4 for why this matters.

What must **not** move here: per-operation facts like element counts. `RunTraced` hands the body its `Activity`
precisely so the body can tag what only it knows; that seam already exists and works.

**(2) `ServiceBase.StartActivity` (api tier).** Same shape, for every controller-service operation across the
SOAP-backed services. It also carries defect D1 (§6). Because controller work is out-of-process and slow, this
is where a duration histogram keyed by `(service, operation)` earns its place.

**(3) `ProjectDocumentSession.ApplyInternal`.** *The* SDK chokepoint for editing. Every command from every
frontend — interactive (`OpenDocument` → `IProjectDocument`) and one-shot (`ProjectAppService.Apply` /
`CanApply` / `Preview`, which run on a throwaway session) — passes through this one method. It already opens a
span and tags `command`.

What is missing is the **outcome**, and its absence is the largest single blind spot in the product (§6 D4).
Add, on the existing span:
- `ihc.edit.status` — the `EditStatus` (Committed / NoChange / Refused / Failed): four values, safe as a metric
  dimension;
- `ihc.problem.code` on a refusal — the `EditOutcome.Code`, which is exactly the identity the architecture
  invariant already requires the refusal to carry ("what the gate refuses, the door refuses" is checkable *by
  identity*);
- change-set magnitude on a commit (`Added`/`Removed`/`Changed` counts from the `ProjectChangeSet`) — the
  cheapest available proxy for "was this edit large?";
- an apply-duration histogram keyed by `ihc.edit.status`.

**(4) `MainWindowViewModel.RunAsync`.** The app's one error boundary, and already a span. Its limitation is that
it is **opt-in per row**, which is why chokepoint 5 exists.

### Tier 2 — the funnels with no instrumentation at all

**(5) `CommandRegistry.Register` — the local `Execute` function.** For a **registered row**, this is the funnel:
`Register` materializes both the ordinary `IAsyncRelayCommand` and the keyboard-gesture command from the *same*
local `Execute`, so every route to that row — menu bar, toolbar, context flyout, key binding, and the test call
sites — runs it.

`RunAsync` does not cover those rows uniformly. Eleven registered rows are wrapped in the `Sync(...)` adapter and
never reach `RunAsync` at all: `edit.cut`, `edit.copy`, `view.showProgram`, `node.useInProgram`,
`link.startFromHere`, `link.jumpOpposite`, `program.leaveMode`, `app.exit`, `view.toggleToolbar`,
`view.toggleStatusBar`, `view.toggleProblems`. Reproduce with
`grep -n "Sync(" applications/ihc_openvisual/ViewModels/MainWindowViewModel.cs` (eleven call sites plus the
adapter's own definition); then read e.g. `Cut` and `Copy`, which contain no telemetry of their own. **[measured]**

Instrumenting `Execute` gives, in one place:
- a span per registered command named by `CommandSpec.Id` — a **declared, closed** vocabulary (`file.open`,
  `edit.delete`, `controller.send`, …), which makes it a legitimate metric dimension;
- an invocation counter and duration histogram over that vocabulary;
- one entry point that no *row* can forget to opt into.

##### What it does NOT cover — and why the claim must stay narrow

The registry is not the app's only command factory. Thirteen commands are constructed directly, outside it
(`grep -rn "new AsyncRelayCommand\|new RelayCommand" applications/ihc_openvisual --include=*.cs | grep -v CommandRegistry.cs`),
plus thirteen source-generated `[RelayCommand]` members. The families that would be missed:

| Family | Built at | Why it is not a row |
|---|---|---|
| Catalog insert leaves (every product and function block) | `MainWindowViewModel.BuildProductMenu` | data-driven from the catalog, one command per item |
| Variable / enum insert items | `MainWindowViewModel.CreateVariableMenuItem` | data-driven from the project's enum types |
| Program-authoring menu items (case, arithmetic, actions) | `ProgramAuthoringCoordinator` | built per armed variable |
| Problemer export, column sort, tier toggles | `ProblemsPanelViewModel`, `ProblemsColumnViewModel` | panel-local |
| `OpenRecent`, `SetTheme` | `MainWindowViewModel` | explicitly ruled non-rows ("parameterized ITEM commands (data-driven lists — the established non-row ruling)") |

Those are *not* an oversight to correct — the non-row status of the data-driven lists is a recorded ruling. The
consequence for telemetry is what matters: **an `ihc.command.invocation` counter sourced only from the registry
counts registered shell commands, not user gestures**, and must be named and documented as such or it will be
read as a usage figure it cannot support. Inserting a product — one of the most common gestures in the app —
would never appear in it.

If a genuine "every gesture" figure is wanted later, the shared abstraction does not exist yet and would have to
be introduced (a wrapper factory the catalog/authoring/panel builders also route through). That is a larger
change than this document proposes. **[judgement]**

This remains the single highest-value *application* edit here, with the narrowed claim. **[judgement]**

##### Two dimensions the registry cannot supply as-is

- **`ihc.command.surface` is not available.** The local `Execute` receives only the arbitrary ICommand
  `parameter`; the same `IAsyncRelayCommand` instance is bound from the menu bar, the context flyout and the
  toolbar (`Views/MainWindow.axaml` binds `Registry.Bar[edit.cut]`, `Registry.ContextMenu[edit.cut]` and the
  toolbar row to one command), and the keyboard route calls the same function through `GestureCommands`. The
  `Surface` enum has three members and **keyboard is not one of them**. So the attribute is droppable, or it
  needs an execution-context parameter that does not exist today — a real design change, not a tagging change.
- **Command failure is not observable at the registry.** Rows that route through `RunAsync` have their exception
  caught, logged and turned into a dialog there; nothing propagates back out of `Execute`. An outer registry
  span therefore sees success on every failed command. Either the row reports its outcome explicitly (an
  outcome the registry can read), or the command span must carry **no** `error.type` and the failure stays where
  it already is — on `MainWindowViewModel.RunAsync`'s span.

Both are reflected in §5: `ihc.command.surface` is marked as blocked, and the invocation counter carries no
error dimension.

**(6) `CommandRegistry.OnContextChanged`.** The one invalidation signal. Each sweep re-evaluates every row's
gate against the current `ShellContext` and raises `CanExecuteChanged` on every command — and gates reach into
the SDK (`document.CanApply`). The code already carries a memo (`_verdicts`) added because the sweep was
running gates up to three times per context change plus once per bound control.

A duration histogram here answers "is the menu-gate sweep the reason selection feels sticky in a large project?"
— a question the existing memo was added on suspicion of, without a measurement to confirm or refute it.

**(7) `ProjectWorkflow` lifecycle.** The concern the request named first. In `ProjectWorkflow` itself,
`StartAsync`, `OpenAsync`, `NewAsync`, `SaveAsync`, `SaveAsAsync`, `CloseAsync`, `SaveToAsync` and
`ConfirmSaveIfDirtyAsync` carry **no spans**; only `UndoAsync` / `RedoAsync` / `RollbackAsync` /
`SaveFunctionBlockAsync` do. `OpenAsync` and `SaveToAsync` log an error and show a dialog on failure but never
touch the `Activity`.

**What already exists, and must not be overstated.** The *interactive* routes to those operations are wrapped one
level up: `MainWindowViewModel.NewAsync`, `OpenAsync`, `OpenRecentAsync`, `SaveAsync`, `SaveAsAsync`,
`CloseAsync`, `Undo` and `Redo` all call `RunAsync`, so a span named `MainWindowViewModel.<op>` does exist for a
menu-driven Open or Save, and an exception escaping it does set the span to Error. So it is **wrong** to say
nothing anchors the gesture. **[measured]**

What is genuinely missing is narrower and still worth having:

- Those spans are **anonymous**. They carry no path, no source, no file size, no outcome — `RunAsync` tags
  nothing but the operation name. "Open failed" and "open of a 40 MB file succeeded slowly" are the same span.
- **A returned `false` is not a failure the span can see.** `ProjectWorkflow.OpenAsync` catches, shows the
  dialog, and returns `false`; nothing propagates, so the `RunAsync` span stays Unset. A failed open therefore
  reads as a successful command in the trace, and only the SDK's `ProjectAppService.Load` span (Error, via
  `RunTracedAsync`) records that anything went wrong — with no path on it either.
- **The startup path is genuinely unanchored.** `MainWindowViewModel.InitializeAsync` is a bare
  `=> _session.StartAsync(startupProjectPath)` with no `RunAsync` wrapper, so opening a double-clicked `.vis`
  produces SDK spans under no gesture span at all. **[measured]**

So the proposal is a *semantic lifecycle span* on `ProjectWorkflow` carrying path, source and outcome — not the
first gesture anchor. §3 develops it into the full load trace.

**(8) `ValidationWorker`.** The only background compute in the application, and completely uninstrumented. It
implements ADR-001's five-step host contract plus single-flight coalescing, and every state it can reach is a
question someone will eventually ask:

| Worker state | The question it answers | Proposed record |
|---|---|---|
| Run completed and bound | how long does validation take on a real project? | `ihc.validation.run.duration`, `ihc.validation.outcome=bound` |
| Run completed but superseded (`IsStillCurrent` false) | how much work is wasted because the debounce is too short? | outcome `superseded` + counter |
| Generation abandoned (`AbandonGeneration`) | did closing/reopening leave work in flight? | outcome `abandoned` + counter |
| Faulted (`_onFaulted`) | a validation rule threw | outcome `faulted` + `error.type` |
| Follow-up started immediately (`_duringRun`) | is the loop permanently catching up? | counter |

**Trace-context hazard — [unverified].** The worker's timer is created **in the constructor**
(`time.CreateTimer(_ => OnQuietPeriodElapsed(), …)`). `System.Threading.Timer` captures the ambient
`ExecutionContext` at *construction*, and `Activity.Current` flows on `ExecutionContext`. So a span started
inside `OnQuietPeriodElapsed` → `RunAsync` → `Task.Run` would take its parent from whatever was current when
`ValidationMonitor` built the worker — i.e. `ProjectWorkflow` construction — **not** from the edit that
triggered the run. The prediction is that validation spans appear as roots (or under a startup span), never
under the edit that caused them.

Falsify it by adding a span in `RunAsync` and reading `Activity.Current?.Parent` in the debugger, or by
querying the backend for the parent span id of a validation span after a single edit.

If confirmed, the correct fix is not to force a parent — the run genuinely is deferred and coalesced, and may
serve several edits — but to capture `Activity.Current.Context` in `Notify` (on the owning thread, where the
snapshot and version are already captured atomically) and attach it as an **`ActivityLink`** on the run's span.
That is precisely the pattern OpenTelemetry defines for a batched/deferred consumer whose work does not have one
causal parent. **[judgement]**

**(9) `ValidationMonitor.OnDocumentChanged`.** Three branches derived from facts the workflow publishes: an
edit, a save (run nothing), or a *replacement* (new generation). Mis-deriving the replacement branch is the
exact bug the generation counter exists to prevent — the previous file's findings bound into the new project.
Recording which branch ran, plus the generation, turns "the panel showed rows from the old file" from an
irreproducible report into a query.

**(10) `AvaloniaDialogService.ShowProblemAsync` — the three overloads.** The question "which coded problems do
real installers actually hit?" needs the place every problem is *shown*, and that is the dialog service, not
`RaisedProblemDisplay`.

`RaisedProblemDisplay.ShowAsync` is the one place that decides which **shape** a raised exception is rendered as
(chain vs aggregate) — that is what its own doc comment claims, and it is true. It is **not** the one place a
problem reaches the user. Eight sites call it; eleven others call `IDialogService.ShowProblemAsync` directly,
including two that matter most:

- `MainWindowViewModel.RunAsync`'s catch — the app's general command exception boundary — calls
  `_dialogs.ShowProblemAsync(UnexpectedErrorTitle, HostProblems.Unexpected(ex))` directly;
- `ReportOutcomeAsync`'s `EditStatus.Failed` arm calls it directly too.

Reproduce with
`grep -rn "ShowProblemAsync(" applications/ihc_openvisual --include=*.cs | grep -v IDialogService.cs | grep -v AvaloniaDialogService.cs | grep -v NullServices.cs`.
**[measured]**

Instrumenting `RaisedProblemDisplay` would therefore miss the unexpected-exception path entirely — the single
most important one — plus every *non-exception* coded problem (a refusal shown as a dialog with no exception
behind it, e.g. the controller-required and delete-refusal paths).

The three `AvaloniaDialogService.ShowProblemAsync` overloads (`Problem`, `ProblemChain`, `ProblemAggregate`) sit
below all of them and are the production chokepoint. A counter keyed by `ihc.problem.code` there answers the
question; the `Problem.Code` it holds is a declared value, not free-form text. The cost is that it is an
Avalonia-layer type, so a headless test asserting on it drives `AvaloniaDialogService` rather than a view-model
— which the visual suite already does.

**(11) `MainWindowViewModel.Refresh` → `TreePaneCoordinator`.** Every document transition — every edit, undo,
redo, load, save — funnels through `Refresh`, which is wrapped in `AsOneContextRebuild` so the whole transition
is one context sweep. Inside it, the crucial branch is:

```
if (!(sameView && _treePanes.TryReconcileConfig()))
    RebuildPreservingSelection(() => _treePanes.RebuildConfig(preserve: sameView));
```

`TryReconcileConfig()` returning `false` silently downgrades an in-place reconcile to a **full tree rebuild**
with fresh node instances plus a selection save/restore — on the UI thread, on every edit. That is a pure
performance cliff with no user-visible symptom other than "it got slower", and no record of it exists.

One attribute — `ihc.tree.update` = `reconcile` | `rebuild` — plus a duration histogram makes the cliff
detectable the day it appears. This is the cheapest high-value addition in the whole document. **[judgement]**

**(12) `ProblemsPanelViewModel.Bind`.** Runs on the UI thread, and its own documentation states the cost model:
`IndexById` walks the entire tree once, and "150 rows over a 2 000-element project is the normal case here, not
the edge". Whether the Problemer panel's latency is validation or projection is unanswerable today because only
one half could ever be timed. A span around `Bind` with `ihc.validation.finding_count` splits it.

### Tier 3 — the deep single-pass engines

Child spans here are what turn a slow load into a *diagnosed* slow load. They are the phases §3 needs.

| Engine | Symbol | Runs | Worth recording |
|---|---|---|---|
| Parse | `ProjectReader.Read` | per load | bytes in, elements out, DTD capture |
| Serialize | `ProjectSerializer.Serialize` | per save/upload | bytes out |
| Index | `ProjectIndex.Build` | per **commit** and per session open | element count |
| Diff | `ProjectChangeSet.Diff` | per commit | added/removed/changed counts |
| Validate | `WholeProjectValidator.Validate` | per validation run | rules run, findings emitted; per-rule timing **behind a switch** |
| Report | `ReportGenerator.Generate` | per report | kind, mode, mime type, bytes |
| Catalog | `BuiltInCatalog`'s `Lazy<MaterializedCatalog>` | **once**, on first catalog use | first-use latency — deliberately deferred, so its cost lands inside whichever user operation touches the catalog first |
| Edit analysis | `EditAnalysisCache` | per edit | already counts misses in `FullAnalysisCount` for a test — promote to a real Counter rather than adding a second mechanism |

**Layering check.** Adding spans in `Ihc.Vis.Io` and `Ihc.Vis.Validation` does not breach an invariant:
`ValidationLayerArchitectureTests` forbids `Ihc.Vis.Session` and `Ihc.Vis.Io` from depending on
`Ihc.Vis.Validation`; `Ihc.Telemetry` lives in `Ihc` (`src/config/`) and is not in that forbidden set —
`ProjectDocumentSession` (in `Ihc.Vis.Session`) already uses it. **[measured]**

**Per-rule timing must be opt-in.** `WholeProjectValidator.Validate` runs the whole rule set in one loop; a span
per rule per run would multiply span volume by the rule count on every keystroke-debounced validation. A
profile-style switch — spans off by default, on when diagnosing — keeps it affordable. **[judgement]**

---

## 3. Worked example: the project-load lifecycle

The lifecycle the request named. Below is the actual call chain, with today's spans marked `[span]` and the
proposal marked `[+]`.

**Part 1 — the synchronous open, all on the UI thread.**

```
Window.Opened
└─ MainWindowViewModel.InitializeAsync(startupPath)          [+] the startup route has NO RunAsync wrapper today
   └─ ProjectWorkflow.StartAsync(path)
      └─ ProjectWorkflow.OpenAsync(path)                     [+] ihc.project.open — the semantic lifecycle span
         │                                                       attrs: ihc.project.source = file,
         │                                                              ihc.project.file_size, outcome
         ├─ ConfirmSaveIfDirtyAsync                          [+] own child — may BLOCK on the user
         ├─ ProjectAppService.Load(path)                  [span]
         │  ├─ File.ReadAllBytesAsync
         │  └─ ProjectReader.Read(bytes)                     [+] child: parse + InlineDtd.Capture + SchemaView
         ├─ ProjectAppService.NormalizeOnOpen(project)    [span]
         │  └─ Edit().NormalizeCatalogEnums().ToProject()
         ├─ ProjectWorkflow.SetProject
         │  ├─ ProjectAppService.OpenDocument            [span]
         │  │  └─ ProjectDocumentSession.Open
         │  │     └─ ProjectIndex.Build                      [+] child: full pre-order walk
         │  └─ RaiseChanged → StateChanged                        ─────► fans out; see Part 2
         └─ RecentProjectsStore.Add(path)
```

Drawn for the **startup** route, which is the one with no gesture span at all. The *interactive* routes reach
the same `ProjectWorkflow.OpenAsync` from `MainWindowViewModel.OpenAsync` / `OpenRecentAsync`, which do wrap
`RunAsync` — so there the proposed `ihc.project.open` span becomes a child of an existing (but attribute-less)
`MainWindowViewModel.<op>` span rather than a new root. Its value is the same either way: the attributes and the
outcome, which no span carries today.

**Part 2 — what `StateChanged` fans out to.** Two independent subscribers, one of which leaves the UI thread.

```
StateChanged
├─ MainWindowViewModel.Refresh                               [+] ihc.ui.refresh
│  └─ AsOneContextRebuild(...)                                   (one transition = one sweep)
│     ├─ TreePaneCoordinator.TryReconcileConfig / RebuildConfig
│     │     [+] attr ihc.tree.update = reconcile | rebuild        ← the silent performance cliff
│     └─ RebuildContext → CommandRegistry.OnContextChanged
│           [+] ihc.ui.context_rebuild                            (every row's gate; some reach the SDK)
│
└─ ValidationMonitor.OnDocumentChanged                       [+] attr: branch = first | edit | save | replacement
   └─ ValidationWorker.Notify   ── arms the 300 ms debounce ──┐
                                                              │  timer thread
      ValidationWorker.RunAsync                          ◄────┘
         [+] ihc.validation.run  + ActivityLink to the captured edit context   ← NOT a child (see below)
         └─ Task.Run  ── thread pool ──►
               ProjectAppService.ValidateStructured      [span]
               └─ WholeProjectValidator.Validate              [+] child
         ── post back to the UI thread ──►
               ValidationMonitor.Bind
               └─ ProblemsPanelViewModel.Bind                 [+]  (IndexById = full tree walk, UI thread)
```

### What the enriched trace would answer

| Question | Answered by |
|---|---|
| Was the load slow because the file is big, or because the project is complex? | `ihc.project.file_size` vs `ihc.project.element_count` on the open span |
| Which phase dominated? | the child spans: parse / normalize / index / tree / first validation |
| Was the wait *my* code or the user staring at a save prompt? | `ConfirmSaveIfDirtyAsync` as its own child span |
| Did the open fail, and on which file? | outcome on `ihc.project.open` + `error.type`. Note this must be set from the **caught** exception inside `ProjectWorkflow.OpenAsync`, not inferred from a propagating throw: that method swallows the failure and returns `false`, so nothing above it can see one |
| Was the first paint delayed by validation? | validation span is **linked**, not nested — so the open span's duration stays honest |
| Did the catalog materialize during this open? | the `BuiltInCatalog` lazy child span appears only on the first catalog-touching operation |

### The three separations that make the numbers honest

These are the design points a reviewer should push on, because getting them wrong produces confidently wrong
latency data. **[judgement]**

1. **Modal waits are not latency.** `ConfirmSaveIfDirtyAsync` and every file-picker dialog block on a human.
   They must be their own child spans, and the `ihc.project.load.duration` histogram must be recorded around
   the work, not around the gesture — otherwise the metric measures how fast the installer reads.
2. **Validation is linked, not nested.** It is debounced, coalesced, and may be abandoned. Nesting it under the
   open span would either inflate the open's duration or (given the timer's `ExecutionContext` capture, §2 (8))
   silently orphan it. A link records the causal relationship without lying about the duration.
3. **`Refresh` is one transition, not three.** `AsOneContextRebuild` already collapses the inner triggers into a
   single sweep; the span must be opened around the same scope, or the counts will disagree with the code.

---

## 4. Worked example: the edit → validate → panel loop

The second lifecycle worth naming, because it runs on **every keystroke-scale interaction** and is where a
performance regression would first be felt.

```
menu / toolbar / flyout / key binding  — for a REGISTERED ROW only
                                         (catalog inserts, program authoring and the
                                          Problemer panel build their own commands — §2 (5))
└─ CommandRegistry  Execute                       [+] ihc.command {ihc.command.id}
   │                                                  no surface: one command instance serves all three
   └─ MainWindowViewModel.<Cmd>                [span] only when the row opts into RunAsync (11 rows do not)
      ├─ ProjectWorkflow.ApplyAsync
      │  └─ ProjectDocumentSession.Apply       [span] + [+] ihc.edit.status / ihc.problem.code / changed_count
      │     ├─ command.Evaluate                       the gate — the same rule the menu already asked
      │     ├─ TryProduceUpdated                      the edit itself
      │     ├─ ProjectIndex.Build                 [+] per commit
      │     └─ ProjectChangeSet.Diff              [+] per commit
      └─ ReportOutcomeAsync                            Committed → status text
                                                       Refused   → the SDK's Danish sentence
                                                       Failed    → log + dialog
   └─ (the commit raises StateChanged) ─────────► Part 2 of §3: Refresh [+] and ValidationMonitor [+]
```

The loop's health is expressible as four numbers, none of which exists today:

- `ihc.edit.apply.duration` split by `ihc.edit.status` — a Refused edit should be *fast*; a slow refusal means a
  gate is doing real work on the interactive path;
- `ihc.ui.tree_update.duration` split by `ihc.tree.update` — reconcile vs rebuild;
- `ihc.ui.context_rebuild.duration` — the gate sweep;
- `ihc.validation.run.duration` split by outcome — with the `superseded` share telling you whether the 300 ms
  debounce is tuned for real projects.

Together those four say whether an edit feels slow because of the *edit*, the *tree*, the *menus*, or the
*validation* — which is precisely the discrimination that is impossible today.

---

## 5. The proposed name registry

### 5.1 Prefix decision

Custom names use the **`ihc.`** prefix. The OpenTelemetry sources genuinely conflict here — the specification
recommends a company/application prefix for custom attributes, while the project's own naming blog argues
against prefixes for domain-*generic* concepts because they pollute the global namespace and are not reusable.

The resolution adopted: these attributes describe **`.vis` project editing** — a domain nobody else models — so
they are domain-specific, not domain-generic, and a short owned prefix is the correct side of that argument. It
also matches the repository's own root namespace and its existing `app.*` problem-code family convention.
**[judgement — the strongest counter-argument is that a reviewer preferring the blog's position would drop the
prefix on the few generic ones, e.g. `error.type`, which this registry already leaves unprefixed because it is
a semantic convention.]**

### 5.2 Attribute registry

| Attribute | Type | Placed on | Value space |
|---|---|---|---|
| `error.type` | string | any errored span + its duration metric | **semantic convention.** Bounded only by the normalization policy in §5.4 — *not* by the catalogue |
| `ihc.problem.code` | string | refusal/finding spans, `ihc.problem.raised` | the problem catalogue's declared codes — closed |
| `ihc.problem.family` | string | same | `ProblemFamily` enum — closed |
| `ihc.command.id` | string | command span + metric | the `CommandSpec.Id` set — closed. Registered rows only (§2 (5)) |
| ~~`ihc.command.surface`~~ | — | **blocked** | the registry's `Execute` cannot see the surface, and `Surface` has no keyboard member. Needs an execution-context change first — see §2 (5) |
| `ihc.edit.command` | string | edit span + metric | command class names — closed |
| `ihc.operation.status` | string | **every** operation's span + metric | `ok` / `refused` / `cancelled` / `failed` — closed. Spelled `ihc.edit.status` before §11.3's rename, and three-valued before §11.7 added `cancelled` |
| `ihc.edit.added_count` | int | edit span only | unbounded |
| `ihc.edit.removed_count` | int | edit span only | unbounded |
| `ihc.edit.changed_count` | int | edit span only | unbounded |
| `ihc.document.version` | int | edit / validation spans only | unbounded |
| `ihc.document.generation` | int | validation spans only | unbounded |
| `ihc.project.source` | string | load/save spans + metric | `file` \| `controller` \| `new` — closed. A startup-argument open is still `file`; how the app was *launched* is a different fact and does not belong in this slot |
| `ihc.project.file_size` | int (bytes) | load/save spans only | unbounded |
| `ihc.project.element_count` | int | load/validate spans only | unbounded |
| `ihc.validation.finding_count` | int | validation span only | unbounded |
| `ihc.validation.outcome` | string | validation span + metric | `bound` \| `superseded` \| `abandoned` \| `faulted` — closed |
| `ihc.tree.update` | string | refresh span + metric | `reconcile` \| `rebuild` — closed |
| `ihc.tree.node_count` | int | refresh span only | unbounded |

The **"span only"** marking is load-bearing, not decorative: those values are unbounded and must never become
metric dimensions.

The three `ihc.edit.*_count` attributes are separate on purpose: `ProjectChangeSet` carries `Added`, `Removed`
and `Changed` as distinct sets, and collapsing them into one number would make a cascading delete
indistinguishable from a bulk insert of the same size.

### 5.3 Metric registry

| Instrument | Kind | Unit | Dimensions | Boundaries | Series bound |
|---|---|---|---|---|---|
| `ihc.project.load.duration` | Histogram | s | `ihc.project.source`, `error.type` | explicit (s) | sources × normalized error types (§5.4) |
| `ihc.project.save.duration` | Histogram | s | `ihc.project.source`, `error.type` | explicit (s) | same, small |
| `ihc.edit.apply` | Counter | `{edit}` | `ihc.edit.command`, `ihc.operation.status` | — | commands × 4 |
| `ihc.edit.apply.duration` | Histogram | s | `ihc.operation.status` | explicit (s) | 4 |
| `ihc.validation.run.duration` | Histogram | s | `ihc.validation.outcome` | explicit (s) | 4 |
| `ihc.command.invocation` | Counter | `{invocation}` | `ihc.command.id`, `ihc.operation.status`, `error.type` | — | registered rows × statuses. The error dimension arrived with §11.7: the funnel awaits the row AND reads its answer, so a handled failure reaches the count. Still **no surface dimension** — that one the registry structurally cannot see (§2 (5)) |
| `ihc.ui.tree_update.duration` | Histogram | s | `ihc.tree.update` | explicit (s) | 2 |
| `ihc.ui.context_rebuild.duration` | Histogram | s | — | explicit (s) | 1 |
| `ihc.problem.raised` | Counter | `{problem}` | `ihc.problem.code`, `ihc.problem.family` | — | catalogue size |
| `ihc.edit.analysis.miss` | Counter | `{analysis}` | — | — | 1 |

The **Boundaries** column exists so that adding a histogram to this registry forces the decision rather than
inheriting the default that §2 Tier 0 shows to be wrong here. Every entry reads `explicit (s)` for one reason,
and it is not that any of these operations has a measured range or a declared threshold — none has. It is that
the exponential alternative proved unqueryable against this backend; see the deviation note in §2. The column
earns its keep the first time an operation *does* acquire a threshold and its row diverges from the shared set.

**Series bound counts attribute combinations only.** A histogram's stored series is that number multiplied by
its bucket count — 11 for the shared explicit set (ten boundaries plus the overflow bucket), against 160 for
the exponential default `max_size` that was rejected. So the explicit choice is roughly a fifteenfold
reduction in stored series as well as the only queryable one. `ihc.project.load.duration`, the widest row
here, is still the one to re-check if metric volume ever becomes a concern; the counters are unaffected.

`ihc.edit.analysis.miss` is named for what it measures: `EditAnalysisCache` (`ihcclient/src/vis/editing/`), the
per-edit open-analysis cache whose `FullAnalysisCount` already counts misses for a test. It is unrelated to the
component catalog, and an earlier `ihc.catalog.*` name for it was simply wrong.

`ihc.command.invocation` counts **registered shell rows**, not user gestures — see §2 (5) for what it structurally
cannot see. Name and document it so nobody reads it as feature-usage data.

### 5.4 Cardinality: what the catalogue does and does not bound

The usual objection to `error.type` as a metric dimension is unbounded cardinality — every new exception type, or
worse every message, becomes a new series, which is why the specification provides an `_OTHER` fallback.

**What the catalogue genuinely bounds: `ihc.problem.code`.** Every *coded* refusal and finding carries a
`ProblemCode` drawn from a compiled catalogue with a completeness gate ("a code with nothing behind it fails the
completeness gate"), across closed families (`edit.*`, `io.*`, `import.*`, `bridge.*`, `internal.*`, the bare
validation ids, and the host's `app.*`). So `ihc.problem.raised`'s series count is a function of how many codes
are *declared* — it changes when someone edits a catalogue entry, not when traffic changes. That is a
deployment-topology bound, which is exactly what the metrics guidance asks for.

**What it does not bound: `error.type`.** Not every failure is coded, and the code proves it:

- `ProjectDocumentSession.ApplyInternal`'s final catch returns `new EditOutcome(EditStatus.Failed, label,
  ex.Message, null)` — **no code**. Its own comment says so: "Anything else is broken rather than refused (incl.
  an engine `InvalidOperationException` on a malformed doc)".
- `AppServiceBase.RunTraced*` catches `Exception`, unrestricted.
- `MainWindowViewModel.RunAsync` likewise, and hands the result to `HostProblems.Unexpected` — one code
  (`app.openvisual.unexpected`) standing in for every possible CLR exception.

**[measured]** So an uncoded failure falls back to a CLR type name, and the reachable set of those is
open-ended — a new `IOException` subtype, a serialization fault, an Avalonia type from a dispatcher fault.

**The policy this registry therefore adopts**, so `error.type` stays predictable and low-cardinality:

1. If the failure carries a `ProblemCode`, `error.type` is that code. Bounded by the catalogue.
2. Otherwise, `error.type` is the CLR type name **only if it is on an explicit allowlist** kept beside the
   instrumentation — the handful actually expected on that path (`IOException`,
   `UnauthorizedAccessException`, `ProjectFormatException`, `InvalidOperationException`, …).
3. Anything else is `_OTHER`, the specification's own escape hatch. The full type name and message still go on
   the *span*, where cardinality is not a cost, so nothing is lost diagnostically.
4. `error.type` is **never** populated from an exception *message*, on any path.

Without rule 3 the histograms in §5.3 have no series bound at all, and the "deployment-topology, not traffic"
property claimed above holds only for `ihc.problem.raised`.

### 5.5 Rules the registry obeys

- lowercase, dot-namespaced, underscores only inside a multi-word component;
- no metric namespace or name pluralised; no `_total` suffix; units declared on the instrument, not in the name;
- no trace or span id ever used as a metric dimension — correlation goes through exemplars;
- nothing written under `otel.*`, and no OpenTelemetry convention namespace (`http.*`, `db.*`, `service.*`, …)
  reused as a prefix for a custom name;
- process-invariant facts (service, version, environment, instance) live on the `Resource`, not on records.

---

## 6. Defects found in the existing instrumentation

Listed because adding signal on top of these makes them harder, not easier, to fix later. Each states the rule
broken and the blast radius of fixing it.

### D1 — `service.name` is duplicated at span level with a conflicting value — **[measured]**

`ServiceBase.StartActivity` (api tier) and `AppServiceBase.StartActivity` (app tier) both do:

```csharp
activity?.SetTag("service.name", this.GetType().Name);
activity?.SetTag("service.operation", operationName);
```

`service.name` is a **Resource** attribute in the OpenTelemetry semantic conventions — it identifies the process
emitting the telemetry, and `AppTelemetryBootstrap.BuildResource` correctly sets it to `IhcOpenVisual`.

To be precise about the mechanism: OTLP carries resource attributes and span attributes in separate structures,
so the span-level tag does **not** literally overwrite the resource value — nothing is lost on the wire. The
problem is downstream: the same key now holds two different meanings in one trace (`IhcOpenVisual` at resource
level, `ProjectAppService` / `ControllerService` at span level), so a query or a UI that flattens the two — and
many do — reports the service inconsistently depending on which layer it read. `service.operation` squats the
same reserved namespace with no convention behind it at all.

Both tags are also redundant: the span is already named `<Service>.<operation>`.

Fix: drop them, or rename to `ihc.service.name` / `ihc.service.operation`.
Blast radius: any saved backend query filtering on `service.name` at span level. Two methods, reaching every
`StartActivity`/`RunTraced` call in the SDK.

### D2 — Whole SOAP payloads are exported as span attributes — a data-exposure problem, not just a size one — **[measured]**

`ServiceBaseImpl.soapPost` tags the **entire serialized SOAP request** as `input.request` and the **entire
response body** as `retv`. `Client.LoggingHandler` deliberately avoids re-reading the response and its comment
states why: "getProject responses are megabytes of base64". `soapPost` tags exactly that body anyway.

**The size half.** The default attribute value-length limit is *no limit*, and no `OTEL_*` variable is set
anywhere in the repository. So every controller project download pushes a multi-megabyte string through the OTLP
exporter.

**The exposure half, which matters more.** The only redaction on that path is `SecurityHelper.RedactPassword`,
and it is a regex over `<…:password>` elements — nothing else. `LogSensitiveData` gates the *request* only
(`settings.LogSensitiveData ? req : RedactPassword(req)`); the *response* is always emitted with the same
password-only redaction, whatever that setting says. Everything else in a SOAP body therefore leaves the process
verbatim: whole project XML, customer and installer information, user lists and group membership, network and
SMTP settings, log contents.

This matters because `ARCHITECTURE.md` already records the design rule — "**Redaction is call-site, not
global**… Trace data must therefore be treated as sensitive" — and this call site is the one carrying the
largest and least selective payloads in the SDK.

**Truncation is not a privacy control.** A 4 KB cap on a project download still exports the first 4 KB of a
customer's installation, and a cap tuned to keep the useful part of a small response keeps the whole of a
sensitive small one. So the fix is not primarily a length limit:

1. **Omit bodies by default.** Record bounded metadata instead: the SOAP action (already in the span name),
   request and response **byte counts**, the HTTP status, and the outcome. That answers every operational
   question — which call, how big, how long, did it fail — without the payload.
2. **Payload capture behind explicit authorization**, i.e. gated on `LogSensitiveData` for *both* directions,
   consistently, and documented in the same place the settings template warns about that flag today.
3. **Then** the Tier 0 value-length limit, as the backstop for whatever still gets tagged anywhere.

This is a behaviour change to existing diagnostics — a developer relying on full-payload traces loses them
unless they set the flag — which is exactly the trade the flag exists to express.

### D3 — No metrics signal, and no configuration surface for one — **[measured]**

Covered in §1.3. Two distinct gaps, and the second is the one easily missed: there is no `MeterProvider`
(so an added `Meter` collects nothing), **and** `TelemetryConfiguration` has no metrics endpoint, so even a
correctly registered provider would have nothing to export to. §2's Tier 0 lists the five parts a working
metrics pipeline needs. **Every metric in §5.3 is blocked on all five** — the fifth, per-instrument bucket
boundaries, being the one that blocks the numbers from meaning anything rather than blocking export.

### D4 — Refusals are invisible, and the span status is inconsistent — **[measured]**

`ProjectDocumentSession.ApplyInternal` returns an `EditOutcome` on every non-exceptional path. A `Refused`
outcome — a user blocked by a rule, the single most product-meaningful event the app has — returns normally, so
the span ends with `ActivityStatusCode.Unset` and carries only `command`. The `ProblemCode` that the whole
coded-refusal architecture exists to make checkable never reaches telemetry.

Worse, the same user-visible outcome is recorded two different ways. A refusal raised as an
`EditRefusedException` from inside `Execute` hits `ActivityExtensions.SetError(activity, ex)` → status
**Error** — and is then converted to `EditStatus.Refused`, i.e. *not* a failure. A refusal produced by the
gate's verdict is status **Unset**. One outcome, two statuses, depending only on which code path noticed.

Fix: record `ihc.edit.status` on every path; reserve `Error` for `EditStatus.Failed`; carry the `ProblemCode` as
`ihc.problem.code` in both cases.

### D5 — HTTP semantic-convention drift — **[measured]**

In `Client.LoggingHandler.SendAsync`:

| Present | Convention |
|---|---|
| span name `SendAsync` | the HTTP method |
| `ActivityKind.Internal` | `Client` for an outbound call |
| `http.url` | `url.full` (`http.url` is deprecated) |
| `http.response.reason` | no such convention |
| `http.request.method` set to an `HttpMethod` object | a string |
| `http.response.status_code` set to a `HttpStatusCode` **enum** | an **integer**. Exported via `ToString()` it becomes `"NotFound"`, so a query for `404` — or any numeric comparison, including `>= 500` — silently matches nothing |
| header names taken verbatim from the message (`http.request.header.Content-Type`) | lowercased, with the header name as the final component (`http.request.header.content-type`) |

Also: every request and response header becomes its own span attribute, so the default 128-attribute ceiling is
reachable on a header-heavy exchange, at which point *later* attributes — including the ones this proposal would
add — are dropped silently. Capturing an explicit small allowlist of headers rather than all of them fixes both
that and the naming, in the same edit.

Fix is contained to one class. Blast radius: queries filtering `http.url` or matching status codes as text.

### D6 — `RunAsync` is opt-in, so eleven registered rows have no span and no error boundary — **[measured]**

Enumerated in §2 (5). This is what makes chokepoint 5 preferable to "add more `RunAsync` calls": a funnel each
new row must remember to enter is a funnel that will eventually be missed again.

It is a defect *within* the registered rows, and fixing it does not reach the commands built outside the
registry — which is the separate limit §2 (5) documents. `RunAsync` also *consumes* the exception it catches, so
even for the rows that do use it, an outer span cannot observe the failure; only `RunAsync`'s own span records
it.

### D7 — The background validation loop is untraced and probably trace-orphaned — **[measured / unverified]**

Untraced: measured (§2 (8)). Orphaned: unverified, with the falsifying experiment stated there.

### D8 — Tag-name conventions in `Ihc.Telemetry` — **[measured]**

`argsTagPrefix = "input."` and `returnValueTag = "retv"` are unnamespaced and, in the second case, a cryptic
abbreviation. `input.*` also risks colliding with any future convention in that namespace.

This is the *lowest* priority item here despite being the most visible, because renaming it breaks every saved
query in the backend and gains nothing diagnostic. Recommendation: treat it as a deliberate, announced rename
(or leave it) — never a drive-by change bundled with a functional edit. **[judgement]**

### Observations that are *not* defects

- `AppTelemetryBootstrap.LogUnhandledException` disposes the whole `Activity` chain on the terminal path. A
  second `Stop()` on an already-stopped Activity is a no-op, so this is benign; it is called out only because it
  looks alarming next to the proposal to open more spans.
- The absence of a configured sampler (default: parent-based, always on) is correct for a single-user desktop
  application and should stay a conscious choice rather than drift into one.
- **A good practice worth protecting.** `Activity.SetTag` holds the object, and an exporter renders a
  non-primitive through `ToString()` — a fact `UserManagerServiceTelemetryTests` documents explicitly. The
  `retv` tag is therefore only safe because `Project.ToString()` and `ProjectValidationResult.ToString()` are
  overridden to bounded one-line summaries (`Project(Version=…, Children=ProjectElement[n])`) rather than the
  compiler-generated record `ToString`, which would print the whole tree. Removing either override would turn
  every `ProjectAppService.Load` span into a full serialization of the project. D2 is the same hazard where no
  such override exists.

---

## 7. Sequencing

Each step is independently shippable and independently verifiable. Later steps depend on earlier ones only where
stated.

| Step | Change | Why here | Verify with |
|---|---|---|---|
| 1 | Tier 0, **all five parts**: `Metrics` config key + templates + settings readout, then `MeterProvider`/`AddMeter`, histogram boundary Views, attribute limits, resource identity | unblocks every metric; skipping any of (a)–(d) yields a pipeline that exports nothing, and skipping (e) one whose percentiles are flat | both UI suites + `safe_architecture_tests` (shared assembly) |
| 2 | D2 first (payload omission — a live data-exposure issue), then D1 and D5 | remove wrong, oversized and over-sharing signal before adding more | `safe_unit_tests` (existing telemetry fixtures, incl. the redaction ones) |
| 3 | Enrich `AppServiceBase.RunTraced*` and `ProjectDocumentSession.ApplyInternal` (D4); adopt the §5.4 `error.type` policy | highest coverage per line; SDK-only | `safe_project_tests` (`RunTracedTests`), `safe_unit_tests` |
| 4 | `ProjectWorkflow` lifecycle spans with path/source/outcome, plus `RunAsync` on `InitializeAsync` | the §3 picture; the largest single gap for the named concern | `safe_visual_tests` |
| 5 | `CommandRegistry` Execute span, scoped and named as *registered rows* (D6) | narrower value than step 4 once §2 (5)'s limits are accounted for | `safe_visual_tests` |
| 6 | `ValidationWorker` spans/metrics + the `ActivityLink` fix (D7) | needs step 1 for the metrics; resolve the orphan question first | `safe_visual_tests` (the worker is drivable on a fake clock) |
| 7 | UI histograms: `Refresh` / `ihc.tree.update`, `OnContextChanged`, `ProblemsPanel.Bind` | the interactive-latency picture; needs step 1 | `safe_visual_tests` |
| 8 | `AvaloniaDialogService.ShowProblemAsync` → `ihc.problem.raised` | needs step 1; the only bounded-cardinality counter in the set | `safe_visual_tests` |
| 9 | Tier 3 child spans, per-rule validation timing behind a switch | the deepest and the most expensive; last | `safe_project_tests` |

**Step 1 ordering note.** Parts (a) and (c) — the config key and the templates — are the pieces most easily
forgotten, and each on its own makes the rest inert. Treat "metrics arrive in the backend from a real run,
confirmed with the `openobserve` skill" as step 1's acceptance criterion, not "it builds".

**Step 3 caveat.** `Ihc.Telemetry` is public SDK surface and the project runs `Microsoft.CodeAnalysis.PublicApiAnalyzers`
with committed baselines (`ihcclient/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`). Exposing a public
`Meter` or new public helper moves that baseline, which is a reviewed change — an internal meter avoids it.
Note also that an *incremental* build skips analyzers, so the baseline gate must be confirmed on a clean build.

---

## 8. How each addition is gated

The repository already has the harnesses; none of this needs a new mechanism.

| Kind of assertion | Existing pattern to extend |
|---|---|
| "this operation emits a span named X, and Error on throw" | `tests/safe_project_tests/RunTracedTests.cs` |
| "the span stays live for as long as the work it describes" | `tests/safe_unit_tests/StreamingTelemetryTests.cs` — its `ExportedSpans` listener deliberately snapshots status **at stop time**, because an `Activity` stays mutable after `Stop()` and asserting on the live object hides exactly the defect under test |
| "this attribute carries what it should — and not a secret" | `tests/safe_unit_tests/UserManagerServiceTelemetryTests.cs` — pins that a span's return-value tag only carries cleartext passwords when `IhcSettings.LogSensitiveData` allows it, and renders the captured tag through `ToString()` because that is what an exporter does |
| "metrics are recorded" | `MeterListener` in-process — the exact mirror of the `ActivityListener` pattern above, and BCL-only so it needs no new test package (`MetricCollector<T>` is friendlier but would add `Microsoft.Extensions.Diagnostics.Testing`) |
| "no row can bypass the command funnel" | `tests/safe_architecture_tests/` — the same shape as the existing SDK-has-no-ILogger rule |
| "it actually works end to end" | the `openobserve` skill against a real run — code that compiles and tests that pass do not prove an exporter reached a backend |

Two repository rules constrain what the tests may assert: `ILogger`/`ILoggerFactory` are **never mocked** (real
instances only), and the SDK must remain free of any logging dependency — so SDK-side telemetry assertions use
`ActivityListener`/`MeterListener`, never a log sink.

---

## 9. Summary — the five changes that matter most

If only five things are done, these five, in this order:

1. **D2** — stop exporting whole SOAP bodies. It is the only item here that is a *live* problem rather than a
   missing capability: complete project, customer, installer, user and network data leaves the process today
   with password-only redaction, and the response path ignores `LogSensitiveData` entirely.
2. **Tier 0, all five parts** — the `Metrics` config key and templates, then `MeterProvider`/`AddMeter`,
   per-histogram boundary Views, attribute limits and resource identity in `AppTelemetryBootstrap`. Every metric
   below is blocked on it, and a partial version either exports nothing while looking wired, or — if the
   boundaries are the part left out — exports flat percentiles that cannot be repaired at query time.
3. **D4** — record the `EditStatus` and `ProblemCode` on `ProjectDocumentSession.ApplyInternal`. One method;
   makes every refusal in the product queryable for the first time, and resolves the Error/Unset inconsistency
   between the two refusal paths.
4. **Chokepoint 7 + §3** — a semantic lifecycle span on `ProjectWorkflow` carrying path, source and outcome,
   with the modal-wait separation, plus a wrapper on the unanchored startup path. This is the project-load
   picture the request asked for. Note the correction in §2 (7): interactive Open/Save/New/Close already have a
   gesture span — what they lack is any attribute on it, and any record of a swallowed failure.
5. **Chokepoint 11** — the `ihc.tree.update` = `reconcile` | `rebuild` attribute. One attribute, one enum, and
   it turns the app's most likely silent performance cliff into a graph.

`CommandRegistry` (chokepoint 5) drops out of this list deliberately. It remains worth doing, but §2 (5)
establishes that it covers registered shell rows only — not catalog inserts, program authoring or the Problemer
panel — and that it can supply neither a surface nor a failure. It is a good span; it is not "every user
gesture", and the counter must be named so nobody reads it that way.

---

## 10. Outcome — what was built, and where it differs from §2

§1–§9 are the analysis as it stood before implementation and are left unchanged. This section is the record of
what shipped. **Where a symbol here disagrees with one above, this section is the current one.**

### 10.1 The composition root moved

`SetupTelemetryAndLogging` is no longer on `AppTelemetryBootstrap`. It is
`Ihc.Bootstrap.TelemetryBootstrap.SetupTelemetryAndLogging` in
[`shared/ihc_telemetrybootstrap/`](../../../../shared/ihc_telemetrybootstrap/TelemetryBootstrap.cs), a
toolkit-neutral project. `AppTelemetryBootstrap` still exists in
[`shared/ihc_appbootstrap/`](../../../../shared/ihc_appbootstrap/AppTelemetryBootstrap.cs) and keeps only what
needs Avalonia: `ChainedILoggerSink`, `LogToSink`, the dispatcher exception handler and the two level maps.

The split was made because the three CLI utilities each carried their own `TracerProvider` rather than reuse a
bootstrap they could not reference without dragging in a UI toolkit. They now reference the neutral half. So
every mention of `AppTelemetryBootstrap` in §1, §2 and §6 should be read as `TelemetryBootstrap` except for the
four Avalonia members named above.

### 10.2 Tier 0 — all five parts, and the acceptance outcome behind them

| Part | Built as | Where |
|---|---|---|
| a | `TelemetryConfiguration.Metrics` (empty ⇒ metrics disabled, mirroring `Traces`/`Logs`) | [`ihcclient/src/config/Telemetry.cs`](../../../../ihcclient/src/config/Telemetry.cs) |
| b | `AddMeter` for both scopes + OTLP through `ConfigureOtlp` | `TelemetryBootstrap.SetupTelemetryAndLogging` |
| c | The key documented in both shipped templates | [`ihcsettings_template.json`](../../../../ihcsettings_template.json), [`ihcsettings_example.json`](../../../../ihcsettings_example.json) |
| d | `Metrikker:` beside `Log:` / `Spor:` / `Selvtjek:` | `MainWindowViewModel.BuildSettingsText` |
| e | Bucket boundaries — see 10.3, which is where the deviation is | `TelemetryBootstrap.ConfigureDurationHistogramViews` |

**Temporality is Delta, set explicitly.** The OTLP exporter defaults to Cumulative, under which every export
interval re-states the running total as a new row, so any `SUM(value)` over a window double-counts. This was
measured against the collector rather than assumed, and `MetricReaderTemporalityPreference.Delta` is set on the
reader options.

### 10.3 The bucket-boundary deviation — a wildcard view, and explicit rather than exponential

§2 proposed **an `AddView` per duration histogram**. What shipped is **one view matching `*.duration`**. Two
reasons, and both are constraints rather than preferences:

- The bootstrap **cannot see either registry**. `SdkTelemetryRegistry` and `AppTelemetryRegistry` are `internal`
  to their own assemblies, and one of those assemblies references the bootstrap — so naming instruments
  individually here is not possible without inverting a dependency.
- A per-instrument list is a list someone must remember to extend. The convention that a duration histogram's
  name ends in `.duration` makes the wildcard exact, and a histogram added later inherits the boundaries
  instead of silently inheriting the wrong default — which is the failure mode §2 called "the part most easily
  skipped, and the one that fails quietly".

The boundaries are `0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30`, in **seconds**.

**Base2 exponential buckets were measured and rejected.** The acceptance spike exported the same measurements
as both an exponential and an explicit histogram. The collector ingests the exponential form, but stores a
bucket **index** in its boundary column with no scale exported anywhere, so the distribution cannot be
reconstructed by any query — and it produced roughly ten times the rows for the same measurements. Explicit
boundaries are therefore not a fallback here; they are the only queryable option against this backend.

### 10.4 The instrumentation core — the one seam that mints spans and instruments

Not in §2 at all, because §2 was about *where* to enrich rather than *how*. The concerns every instrumented
operation shares — span naming, duration, outcome on both signals, a normalized `error.type`, and the guarantee
that a fault in the instrumentation never fails the operation — live once, in
[`ihcclient/src/config/OperationTelemetry.cs`](../../../../ihcclient/src/config/OperationTelemetry.cs).
Instruments are declared by exactly one registry per layer: `SdkTelemetryRegistry` for the SDK and
`ihc_openvisual.Configuration.AppTelemetryRegistry` for the host.

This is enforced rather than documented: `TelemetryCoreArchitectureTests` fails the build if any SDK or GUI type
starts a span from a raw `ActivitySource`, constructs a second `Meter`, or declares an instrument outside a
registry. See [`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) § *Observability without imposing a stack*.

### 10.5 The metric-to-trace join — asked for explicitly, because the SDK's default is not the spec's

The Tier-0 list in §2 says "exemplar filter (default `trace_based`)". That parenthetical is wrong, and it is
the kind of wrong that ships silently: `trace_based` is the **specification's** default, not the .NET SDK's.
`MeterProviderBuilderSdk.ExemplarFilter` is a nullable that stays unset until something sets it, and an unset
filter attaches no exemplars at all. Measured twice — the built provider's field read back `null`, and metric
rows exported by the running app carried no `exemplars` field.

`TelemetryBootstrap.SetupTelemetryAndLogging` therefore calls
`SetExemplarFilter(ExemplarFilterType.TraceBased)` explicitly. This is what makes the core's
record-before-dispose ordering pay: instruments are recorded while the activity is still live precisely so each
point is exemplar-eligible, and without the filter that cost is paid at every measurement for nothing.

Verified end to end: a point on `ihc.edit.apply` carries
`{trace_id, span_id, value, _timestamp}`, and following that pair reaches exactly the
`ProjectDocumentSession.Apply` span that produced it. The duration histogram carries them too.

A regression here is silent — no test fails, no error is logged, the metric simply loses its link to the
trace — so `TelemetryCompositionTests.TheMeterProvider_AttachesExemplarsSoAMetricPointCanBeTracedBack` reads
the filter back off the built provider and fails if the call is removed.

---

## 11. The nesting audit — is a workflow one tree, and does that tree time it?

§10 records what was BUILT. This section records an audit of what the built instrumentation actually
produces at run time, the gaps it found, and what closed them. The question it answers is narrower than
§1–§9's "where should enrichment go": **can a major workflow — load, save, edit, insert, validate — be read
in OpenObserve as ONE tree whose root times the whole operation and whose children explain it?**

Read §10 first where a symbol disagrees with §1–§9. Where this section disagrees with §10, this one is
current.

### 11.1 Method, so the audit can be re-run rather than believed

Every claim below is either a symbol in this repository or a measurement taken from the live OpenObserve
backend named in `ihcsettings.json`. Nothing is inferred from the source alone: the whole point of the audit
is that a span's PARENT is decided at run time by an ambient `Activity`, which no amount of reading a call
site can settle.

The two queries the audit is built on, both against the traces stream (`ihc`):

```sql
-- (A) Which operations start a trace of their own, and how often. A root that is not a named
--     operation — a gesture, a launch, a deliberately linked background run — is a fragment.
SELECT operation_name, count(*) AS n FROM "ihc"
WHERE reference_parent_span_id IS NULL AND service_name = 'IhcOpenVisual'
GROUP BY operation_name ORDER BY n DESC;

-- (B) One launch, whole. service_service_instance_id is the traces spelling of the run id
--     (logs and metrics spell it service_instance_id); one value = one launch of the app.
SELECT operation_name, span_id, reference_parent_span_id, trace_id, duration, start_time, links
FROM "ihc" WHERE service_service_instance_id = '<RUN>' ORDER BY start_time;
```

Query (A) over the whole history mixes builds, and an operation that is a root in an old build only because
the span above it did not exist yet is not evidence about today. **Scope to one run** — pick the newest
`service_service_instance_id` — before drawing any conclusion. The audit's first pass did not, and read four
historical roots as live defects; they were older builds.

Both queries are wrapped by the skill now, and the audit is why: `oo_query.py --run` runs (B) and draws the
result as trees, `--trace <id>` does one trace, and `:run` inside a `--sql` string expands to the newest
launch's instance predicate — `--latest-run` alone does NOT reach `--sql`, which is what made the run id
have to be spelled out by hand here. The raw SQL is kept above because it is what the flags do, and because
a question the flags do not answer starts from it. The skill's reference
(`references/openobserve-api.md`) carries the field schemas.

### 11.2 What the audit found — measured, before any change

Baseline: the four newest launches at the time of the audit, and one live save and one live edit pulled from
the same backend.

| # | Finding | The measurement |
|---|---|---|
| 1 | **The gesture root timed nothing.** `CommandRegistry.Invoke` disposed its scope on RETURNING the row's task, so it closed at the row's first await. | Root `CommandRegistry.Invoke` **10.1 ms** over a child `MainWindowViewModel.SaveAsAsync` of **20 226 ms**. |
| 2 | **Composition-time work was orphaned.** The shell's constructor builds the catalog menus, both trees and the gate sweep outside any span. | Per launch: `GetAvailableFunctionBlocks` a root **4 times of 4**, `CatalogImportWorkflow.LoadPersisted` **4 of 4**, `TreeUpdate` **4 of 8**, `OnContextChanged` **6 of 12**. A launch left about four single-span traces. |
| 3 | **Modal think-time was billed to the operation.** | `ProjectWorkflow.SaveToAsync` **13 657 ms** over a `ProjectAppService.Save` of **24 ms**; the difference was an undismissed failure dialog, and `ihc.project.save.duration` recorded it. A picker turned a save-as into **20 s**. |
| 4 | **Phases missing inside the top-tier I/O.** | Under a 24 ms `Save`, the two child spans accounted for **11 ms**; the atomic write was unnamed. Under a 9.6 ms `Apply`, index + diff accounted for **4 ms**; the mutation was unnamed. `BuiltInCatalog` materialization was a metric with no span. |
| 5 | **`ihc.edit.status` was the status attribute of EVERY span**, not only edits. | The attribute appears on `ValidationWorker.Run`, `Load`, `Save`, `GenerateReport` — a reader filtering failed loads had to ask an attribute named for editing. |

Two things the audit examined and did NOT change:

- **Errors do not roll up.** A handled failure marks its own span and leaves its ancestors `UNSET` — measured
  on the save above, where only `SaveToAsync` was `ERROR`. This is kept: a lifecycle method answers `false`
  both when it failed and when the installer cancelled a picker, so marking the ancestor Error would report
  cancelling as breakage. Finding the gesture behind a red span is a QUERY, and finding 1's fix is what makes
  it work — the trace root now spans the whole gesture and carries `ihc.command.id`.
- **The debounced validation run links rather than parents.** One run serves every edit that coalesced into
  it. Confirmed still true, and confirmed to survive ingestion: the run's `links` column holds the triggering
  trace id, so the join is available in SQL even though the tree does not show it.

### 11.3 What changed

| Gap | Change | Where |
|---|---|---|
| 1 | The invocation funnel AWAITS the row's task, so the span covers the gesture and observes its faults. The count now records on completion rather than on invocation — stated at the site, because a gesture whose process dies mid-modal is no longer counted. | `CommandRegistry.Register` |
| 2 | A `Compose` span over the constructor's work; an `App.Startup` span over composition AND the start-up load. | `MainWindowViewModel` ctor, `App.OnFrameworkInitializationCompleted` |
| 3 | A span per modal, named by `[CallerMemberName]` on the dialog service's funnels. | `AvaloniaDialogService` |
| 4 | `WriteAtomically`, `Produce` (the edit kernel) and `BuiltInCatalog.Materialize` each report a span; the catalog's hand-rolled `Stopwatch` is replaced by the core, which records the same histogram. | `ProjectAppService`, `ProjectDocumentSession`, `BuiltInCatalog` |
| 5 | `ihc.edit.status` → **`ihc.operation.status`**. | `SdkTelemetryRegistry.Attributes.OperationStatus` |

Two consequences of gap 5 that the rename itself did not settle, both closed in §11.7: the new name COLLIDED with the
SOAP layer's `ihc.operation`, and `BuiltInCatalog.Materialize`'s histogram silently gained the status dimension when
its hand-rolled `Stopwatch` was replaced by the core. That second one is a series change of the same kind as the
rename, and belongs in the same announcement: `ihc.catalog.materialization.duration` had no dimensions at all before.

Gap 3 is closed for TRACES and deliberately left open for METRICS. The child span makes the modal's time
subtractable in a tree; `ihc.project.save.duration` still records the whole wait, because the alternative is a
suspend/resume pair on `OperationScope` — considered and declined in that type's own remarks, since it would put a
host's presentation concern into the SDK's measurement contract. Read a save percentile knowing that.

**The rename is a break, and D8 said it must be an announced one.** Queries and dashboards written against
`ihc_edit_status` match nothing after the cutover — they do not fall back — and every metric series carrying
the dimension splits there. The old spelling stays readable in rows exported by older builds, which is why
the skill's SQL cookbook notes both. It is a separable edit: one declaration, its uses in the telemetry test
fixtures, and the cookbook.

Two changes were made only because the LIVE run showed them, and neither could have been found by reading
the code or by any headless test:

- **A worker built inside a span inherits it forever.** `ValidationWorker`'s debounce timer is created in its
  constructor, and a timer captures the execution context it is created in. Once composition had a span,
  every validation run for the life of the process became that span's child — three runs, one of them
  minutes after start-up, all parented to a launch span that had closed. The run now clears
  `Activity.Current` before opening its span, which makes the link-not-parent design explicit instead of
  accidental. Pinned by `ValidationWorkerTelemetryTests.ARunStartedWhileAnActivityIsAmbient_…` (§12.2 explains
  why the test is shaped around what is ambient rather than around how the worker was built).
- **Starting a span in the composition root leaves it ambient on the message loop.** `StartActivity` makes
  the new activity current, and `OnFrameworkInitializationCompleted` runs straight off the message loop, so
  what it leaves current is what the loop keeps. The quit prompt arrived as a 3.1 s child of a 1.5 s launch.
  The launch is now restored to the previous ambient value once composition returns, while the scope itself
  stays open until the start-up document has loaded.

The second is the general hazard worth remembering: **an ambient span is not scoped by the method that
started it.** A `using` restores it on the way out; a scope held across an await or across a callback
boundary does not.

### 11.4 The measured result

One launch, driven through `aui-openvisual`: open a project through the file picker, insert a locality, quit
and discard. Before, the same shape of session produced roughly 20 spans across about 8 traces with 6
fragments among the roots. After:

```
55 spans · 7 traces · 7 roots · 0 fragments
roots: App.Startup ×1 · CommandRegistry.Invoke ×2 · ValidationWorker.Run ×3 (linked) · CanCloseAsync ×1
```

```
App.Startup                                    1477.7ms
  CatalogImportWorkflow.LoadPersisted              1.9ms
  MainWindowViewModel.Compose                    599.7ms
    ProjectAppService.GetAvailableProducts       561.0ms
      BuiltInCatalog.Materialize                 551.1ms
    ProjectAppService.GetAvailableFunctionBlocks   0.2ms
    MainWindowViewModel.TreeUpdate                 5.0ms
    CommandRegistry.OnContextChanged               5.1ms
  MainWindowViewModel.InitializeAsync             34.3ms
    ProjectWorkflow.StartAsync                    33.8ms
      ProjectAppService.CreateNew                  6.3ms
      ProjectAppService.OpenDocument               6.1ms
        ProjectIndex.Build                         4.8ms
      ValidationMonitor.OnDocumentChanged          4.3ms
      MainWindowViewModel.TreeUpdate              14.8ms

CommandRegistry.Invoke                          5157.9ms
  MainWindowViewModel.OpenAsync                 5157.4ms
    AvaloniaDialogService.PickOpenProjectAsync  5071.4ms   <- the installer, not the app
    ProjectWorkflow.OpenAsync                     82.5ms
      ProjectAppService.Load                      14.7ms
        ProjectReader.Read                        12.6ms
      ProjectAppService.NormalizeOnOpen           26.9ms
        ProjectChangeSet.Diff                     16.2ms
      MainWindowViewModel.TreeUpdate              30.1ms

CommandRegistry.Invoke                            29.7ms
  MainWindowViewModel.InsertLocality              29.6ms
    ProjectDocumentSession.Apply                   7.8ms
      ProjectDocumentSession.Produce               1.9ms
      ProjectChangeSet.Diff                        4.0ms

ValidationWorker.Run  (a root, linking back)     153.7ms
  ProjectAppService.ValidateStructured           152.4ms
    WholeProjectValidator.Validate                76.6ms
  ProblemsPanelViewModel.Bind                      0.4ms
```

Three readings that were not available before, and are the audit's actual payoff: **551 of a 1478 ms launch
is materializing the component catalog**; **5071 of a 5158 ms open is the file picker**, leaving 83 ms of
work; and an insert's mutation is 1.9 ms of its 7.8 ms apply.

### 11.5 What is still true, and deliberately so

- **`ProblemsPanelViewModel.Bind` can outlast the run that parents it** (12.2 ms under a 4.6 ms run). The
  bind is posted to the UI thread and the run must not wait for it — ADR-001. The parentage is causally true
  and the two durations measure different things. Unlike finding 1, this parent could NOT have awaited.
- **`BuiltInCatalog.Materialize` is not pinned by a test.** The guarding `Lazy` is process-wide, so whether
  the span fires during any given test depends on which test ran first; a conditional assertion pins nothing.
  It is verified live, in 11.4, and `Tier3TelemetryTests` records why the unit test is absent.
- **The controller bridge is unexercised from OpenVisual.** `SendProject`/`RetrieveProject` refuse without a
  controller (E10), so no host trace reaches `DownloadFrom`/`UploadTo`. Those app-service operations and the
  SOAP + HTTP spans beneath them are instrumented and will nest when the transfer is wired; nothing here
  measures that.
- **`App.Startup` is lost if composition throws.** The scope closes in the `Opened` handler, which such a
  launch never reaches. That launch ends in `Main`'s catch and process exit.

### 11.6 Re-running the audit

```bash
# 1. The suites that gate the nesting claims (all controller-free)
dotnet test tests/safe_project_tests/safe_project_tests.csproj      # phases, apply, startup, invocation, worker
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj        # the dialog spans
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj            # the instrumentation core

# 2. A live launch, driven rather than clicked
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 doctor --launch
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 project open --path tests/testdata/projects/Project1-SimpelWired.vis
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 locality insert
#    close the window, then answer the save prompt with "Gem ikke" — the fixture is a byte-pinned oracle

# 3. Read the trees back. --run draws every trace of the newest launch, reports
#    "N spans, N traces, N roots", and names any child that outlasts its parent.
python .claude/skills/openobserve/scripts/oo_query.py --run --since 1h --size 500

#    Then check three properties it does NOT decide for you: every root is a named
#    operation; the only child outlasting its parent is the posted panel bind; every
#    AvaloniaDialogService span sits under the operation that raised it.
#    --trace <id> drills into one; query (B) of 11.1 is the same rows unassembled.
```

The tests pin the shape; only the live run pins the PARENTAGE, because parentage is decided by an ambient
`Activity` that a headless harness does not have. Both of the run-time defects in 11.3 passed every suite.

---

## 12. The review pass — what §11 got wrong, and the outcome roll-up

§11 was audited against the OpenTelemetry research corpus and re-measured. Six things it left behind are recorded
here; where this section disagrees with §11, this one is current.

### 12.1 A name cannot be a value and a namespace at once

Renaming `ihc.edit.status` to `ihc.operation.status` put it directly on top of `ihc.operation`, the SOAP layer's
action name — and `SoapPostTelemetryTests` pins BOTH on one metric point. A backend that expands dotted keys into
nested structures then has to store one key as a string and as an object; OpenObserve hides it only because it
flattens the dots to underscores, which is exactly why this had to become a build-time rule rather than something a
query eventually reveals. The conventions solve it by moving the leaf down — `db.operation` became
`db.operation.name` — and so does this: **`ihc.operation` → `ihc.operation.name`**, a second announced break
deliberately spent in the same cutover as the first.

`TelemetryRegistryContract.AssertNameIsNotAlsoANamespace` now fails any registry that reintroduces the shape. Its
existing checks — lowercase, `ihc.` prefix, singular segments, uniqueness — could not: `Is.Unique` sees only exact
duplicates.

### 12.2 Two tests that could not fail

- **`ValidationWorker`'s trace-root test.** It passed with the fix removed — verified by removing it. Two reasons,
  and each alone was enough: `FakeTimeProvider`'s timer references `ExecutionContext` NOWHERE, so it reproduces
  none of the "a timer captures its creation context" mechanism the docstring described; and by the time the test
  advanced the clock, both of its activity scopes had closed, so `Activity.Current` was already null. The property
  the fix actually implements — a run does not adopt whatever is ambient when it starts — is now supplied where the
  fake puts it, at the `Advance`, and the test fails without the fix.
- **`StartupTraceTelemetryTests`' gate-sweep assertion.** `Is.Not.Null` on a parent, under a `TraceProbe` that
  makes every owned span have one. Its own sibling test's docstring argues against exactly this. Now `Is.SameAs`.

### 12.3 One of the two ambient restores was dead code

Measured with a probe over the three shapes: an ExecutionContext change made before the first `await` escapes to
the caller from a PLAIN method, and does not from an `async void` lambda or an `async Task` method — the async
kickoff restores it. So `OnFrameworkInitializationCompleted`'s restore is load-bearing and the `Opened` handler's
try/finally was not, though its comment claimed the opposite and cited a measurement belonging to the other site.
The handler now assigns and says why nothing has to undo it. `ValidationWorker.RunAsync`'s "the timer callback's
context is discarded" note is likewise weaker than the truth: the guarantee is the method's own `async` shape.

### 12.4 Smaller things

`CommandRegistry`'s pre-execute hook ran outside the try, so a throw from it recorded the gesture as a success.
`ShowProblemAsync` routed through the message door and produced a span named `ShowMessageAsync` — indistinguishable
from an informational box, for precisely the case §11.2's finding 3 measured; it now opens its own span, named for
the coded door and carrying `ihc.problem.code`. And the new worker test copied thirteen lines of its neighbour,
which is now one `DriveOneRunAsync`.

### 12.5 The roll-up: `bool` was the reason failures stopped at the layer that handled them

§11.2 kept "errors do not roll up", reasoning that a lifecycle door answers `false` both for a failure and for a
cancelled picker, so marking the ancestor Error would report cancelling as breakage. That reasoning is correct
GIVEN a bool — and the bool was the thing to change.

Four meanings were riding on one `false`, and the sharpest case was not a picker at all:
`ConfirmSaveIfDirtyAsync` answered `false` when the installer pressed *Fortryd* **and** when the save they asked
for broke. A quit stopped by a failed save and a quit the installer abandoned were one event.

Why a trace query does not repair this: `error.type` belongs on the operation's duration metric as well as its
span, and a metric point can be joined to its own span and to nothing beneath it. A failure that stops at a child
span is invisible to every rate built on `ihc.command.invocation`, `ihc.project.load.duration` and
`ihc.project.save.duration` — no query fixes that, only the roll-up.

What changed:

| Layer | Before | After |
|---|---|---|
| `OperationStatus` | `Ok`/`Refused`/`Failed` | plus **`Cancelled`** — a person changing their mind is neither a rule declining nor a break. Span status stays Unset, like a refusal |
| `ProjectWorkflow` lifecycle doors | `Task<bool>` | `Task<OperationOutcome>` — the vocabulary the core already speaks, rather than a second one to keep in step |
| The three history doors | `Task<bool>` | `Task<EditOutcome?>` — `OperationOutcome` cannot separate "committed" from "nothing to undo", because both are the operation working, and both callers need that difference |
| `MainWindowViewModel.RunAsync` | `Task` | `Task<OperationOutcome>`, read off the scope. Every body's shape is unchanged; a body with nothing to declare still says nothing |
| `CommandSpec.Execute` | `Func<ShellContext, Task>` | `Func<ShellContext, Task<OperationOutcome>>`, so the funnel can put the answer on the gesture's ROOT — the span every query starts from |

`OperationScope.Outcome` is readable so a boundary hands back what it recorded rather than deriving it twice; the
`Announce` helper puts a door's answer on the span and the status line together, so no site can do half of it.

### 12.6 What this section did NOT change

- **A refusal is still not an error, and neither is a cancellation.** Only `Failed` sets a span Error.
- **`ihc.command.surface` is still blocked** — the funnel structurally cannot see which surface invoked it.
- **The modal think-time still lands in the duration histograms** (§11.3's note on gap 3).
- **`ImportCatalogFileAsync` still answers a bool.** It delegates to a collaborator with no scope of its own, so
  there is no outcome to forward; converting it would have invented one.

### 12.7 What stayed uncovered, and why

Read over the changed files only, per the coverage rule in `CLAUDE.md`. Every new branch that is observable
product behaviour is covered — the four outcome values on a gesture's root and its count, the quit gate's two
"no"s, a dismissed picker and a stopped prompt, the pre-execute hook's throw, the coded dialog's span. What is
left, and the reason each is left:

| Uncovered | Why |
|---|---|
| `ProjectWorkflow`'s `NothingOpen` guards (`SaveAsync`, `SaveAsAsync`, `SaveFunctionBlockAsync`) | Precondition guards the command gates make unreachable, and uncovered before this change too — they only changed what they RETURN. The `Testing` rule declines null-guard tests unless the risk area is asked for. |
| `AvaloniaDialogService.ConfirmSaveChangesAsync`'s body | Needs an owner window and a dismissal; the headless suite covers the same funnel through `ConfirmAsync`, and the workflow suite drives the fake dialog rather than this one. |
| `OperationOutcome`'s `Equals`/`GetHashCode`/operators | Structural equality nobody calls; unchanged by this pass. |
| `App.axaml.cs`'s launch span | The composition root, which no controller-free suite can reach — §11.5 already says so, and the ExecutionContext behaviour it turns on is pinned by the probe in §12.3 rather than by a test. |
