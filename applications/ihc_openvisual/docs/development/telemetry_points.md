# Central telemetry points — where enrichment belongs

Developer reference (HOW) identifying the **chokepoints, anchors and common flowpoints** in IHC OpenVisual and
the `ihcclient` code beneath it where OpenTelemetry enrichment can be added *once* and pay off across many
operations — so that bugs and performance problems become visible in the trace rather than only in a bug report.

The behavioural spec (WHAT) is in [`../stories/`](../stories/) and [`../product.md`](../product.md); the layering
this analysis must not violate is in [`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) and
[ADR-001](../../../../docs/adr/ADR-001-threading-and-concurrency-model.md) /
[ADR-002](../../../../docs/adr/ADR-002-thick-sdk-services-and-thin-apps.md).

Symbols are named rather than line-numbered, because line numbers drift and member names do not.

This is an **analysis and proposal**. Nothing in it has been implemented; §7 is the sequencing if it is adopted.

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

Around it, `Program.cs` wires the startup connectivity probe (`Ihc.TelemetrySelfCheck.ProbeAndReportAsync`) and
four exception layers (AppDomain, Dispatcher, UnobservedTask, and the X11/GLib logger in `CreateX11Options`),
with `Main`'s own `catch` as the fifth.

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
| `ihc.edit.status` | string | edit span + metric | `EditStatus` — closed (4) |
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
| `ihc.project.load.duration` | Histogram | s | `ihc.project.source`, `error.type` | exponential | sources × normalized error types (§5.4) |
| `ihc.project.save.duration` | Histogram | s | `ihc.project.source`, `error.type` | exponential | same, small |
| `ihc.edit.apply` | Counter | `{edit}` | `ihc.edit.command`, `ihc.edit.status` | — | commands × 4 |
| `ihc.edit.apply.duration` | Histogram | s | `ihc.edit.status` | exponential | 4 |
| `ihc.validation.run.duration` | Histogram | s | `ihc.validation.outcome` | exponential | 4 |
| `ihc.command.invocation` | Counter | `{invocation}` | `ihc.command.id` | — | registered rows. **No error dimension** — the registry cannot observe a failure (§2 (5)) |
| `ihc.ui.tree_update.duration` | Histogram | s | `ihc.tree.update` | exponential | 2 |
| `ihc.ui.context_rebuild.duration` | Histogram | s | — | exponential | 1 |
| `ihc.problem.raised` | Counter | `{problem}` | `ihc.problem.code`, `ihc.problem.family` | — | catalogue size |
| `ihc.edit.analysis.miss` | Counter | `{analysis}` | — | — | 1 |

The **Boundaries** column exists so that adding a histogram to this registry forces the decision rather than
inheriting the default that §2 Tier 0 shows to be wrong here. Every entry currently reads `exponential` for one
reason — no operation in this table has a measured range or a declared threshold yet. The column earns its keep
the first time one of them does and the row diverges.

**Series bound counts attribute combinations only.** A histogram's stored series is that number multiplied by
its bucket count — 160 for the exponential default `max_size`, against 15 for the default explicit set. So
`ihc.project.load.duration`, the widest row here, is the one to re-check if metric volume ever becomes a
concern; the counters are unaffected.

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
