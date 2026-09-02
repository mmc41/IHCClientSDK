# Test Strategy

How this repository's tests are levelled, what each suite is for, and — the question this document exists to
answer — **which suite a new test belongs in**.

These are guidelines, not an inventory. No section lists which tests exist today; where the current layout
disagrees with a rule below, that is recorded once in [Action items](#action-items) rather than described
per file.

- [The safety rule](#the-safety-rule)
- [Levels and suites](#levels-and-suites)
- [The end-to-end bar](#the-end-to-end-bar)
- [Techniques that cross suites](#techniques-that-cross-suites)
- [Running the suites](#running-the-suites)
- [Coverage](#coverage)
- [Analyzer policy for test code](#analyzer-policy-for-test-code)
- [Action items](#action-items)

## The safety rule

Three things are untouchable: a **live controller** (state changes are destructive), the **vendor
application** (Windows-only, closed) and the **vendor catalog**. The suite layout encodes the
answer. Every suite is named `safe_*` to signal it, only `safe_integration_tests` may reach a controller and
only through state-safe operations, and everything else runs on fakes, files or headless UI.

The seam is chosen deliberately: fakes go on the low-level `IIHCApiService` implementations and, where a
test's scope does not need the embedded catalog, on `ICatalog`, while **application services are always
real**, so business logic is exercised rather than mocked. This is
convention plus fixture design — nothing mechanically prevents an unsafe test from being added, which is why
it is stated here.

Vendor behaviour is pinned by **oracles** rather than by reasoning: committed `.vis`/`.def`/`.ifb` files under
`tests/testdata/`, provenance documented per file in [testdataoverview.md](tests/testdata/testdataoverview.md).
Only part of that corpus is vendor ground truth — the `.vis` projects the vendor application saved and the two
`.ifb` exports it produced; every `.def` oracle and the rest of the `.ifb` and `.vis` files are synthetic —
and the authentic files are kept physically separate from the synthetic ones.

## Levels and suites

One suite per level per subject. A test belongs at the **cheapest level that can hold it** — a level exists
for what the level below genuinely cannot reach, not as a place to repeat it. To place a test, read the rows
in order and stop at the first whose subject claims it; the last row is the general home.

| Suite | Level · cost | Subject — what it is for | Not here |
| ------- | -------------- | -------------------------- | ---------- |
| `safe_architecture_tests` | Architecture · s | Compliance with the design: layering, dependency direction, banned shapes, the invariants in [ARCHITECTURE.md](ARCHITECTURE.md). ArchUnitNET over one IL model per assembly | Behaviour of any kind |
| `safe_integration_tests` | Controller · s, never in CI | General `ihcclient` behaviour against a **real controller**, through state-safe operations only. Needs `ihcsettings.json`; the configured test resources' outputs may change when it runs | Anything that can be verified controller-free |
| `safe_lab_tests` | Headless UI + unit · s | The `utilities/ihc_lab` application and only that — its GUI, parameter-control strategies, operation filtering, fakes. `[NonParallelizable]`, `mock://` services | Anything not `ihc_lab` |
| `safe_visual_tests` | Headless UI · s | `applications/ihc_openvisual`'s **GUI** — whatever needs an Avalonia application, window, control, style or automation peer — but in a headless execution environment. Real `MainWindow` on the headless backend. Also the Avalonia-dependent `shared/` bootstrap code both apps host (`shared/ihc_appbootstrap`), which has no toolkit-free level to sit at. Run on **Linux** as well as Windows: that leg is the only one that catches a portability defect | Anything that compiles without an Avalonia type: view-model logic, stores, presentation mapping, host catalogs. Those are OpenVisual's non-GUI code and belong in `safe_project_tests`. Anything about `ihc_lab`, which belongs in `safe_lab_tests` |
| `safe_visual_e2e_tests` | End-to-end GUI · **minutes** | The confidence **no other suite can give**: that the shipped app is driveable. A thin, deliberately small set of representative scenarios — see [the bar](#the-end-to-end-bar) | Combinations, route matrices, business logic, anything still testable without real GUI |
| `safe_project_tests` | Engine · ms–s | Not general to the SDK but about the project domain: the `Ihc.Vis` engine, `ProjectAppService`, sessions and commands, validation rules, the problem catalogue, reporting — **and OpenVisual's non-GUI code** (view-models, services, stores, route planners, presentation mapping) — for OpenVisual code the rule, not yet the layout, until [A2](#a2--openvisual-non-gui-tests-sit-in-safe_visual_tests) lands. Real application services, oracle files, and the catalog the test's scope calls for — `BuiltInCatalog` or a fake `ICatalog` | Anything needing execution in Avalonia |
| `safe_unit_tests` | Unit · ms | The `ihcclient` SDK in general — transport, models, serialization, security, settings, telemetry primitives. Also **any utility or `shared/` project with no suite of its own**. Faked at the `IIHCApiService` seam | Anything Avalonia-shaped; anything app-specific |

Two rules for the architecture suite, because both are easy to get wrong: a clean subject with an empty
exemption roster is indistinguishable from a **broken detector**, so every scan carries a seeded violator
proving it can fail — the seed families several detectors share live in `ArchitectureDetectorSeeds.cs`, a seed
only one detector needs sits beside that detector; and a **ban file beats a fluent rule** where it can
express the same thing ([ADR-004](docs/adr/ADR-004-compile-time-bans-over-architecture-tests.md)) — fluent
bans go vacuous for types an assembly never references.

## The end-to-end bar

`safe_visual_e2e_tests` is not part of ordinary verification, and it has an explicit admission test:

> A scenario belongs here only if it fails for a reason that exists **solely** in the real desktop: the
> Avalonia-to-Windows-UIA bridge, real keyboard focus, the desktop modal stack, process startup and document
> binding, or the `aui` driver itself.

Everything else — combinations, route matrices, sorting, filtering, counts, wording, undo — is business logic,
and business logic is cheaper one level down.

The suite has two modes. The **default** launches the real `ihc_openvisual.exe` and drives it over Windows UI
Automation, one `pwsh` process per verb; it holds the screen for minutes and force-kills any running
OpenVisual, so run it only when asked and say so first. The **headless** mode, which CI gates, hosts the same
`MainWindow` in-process and is a **second implementation** of the verb vocabulary: it exercises neither
`aui.ps1` nor the UIA bridge, and it refuses the verbs behind `[Category(E2E.DesktopOnly)]` rather than
approximating them. Read a headless pass as *"the scenario paths still work"*, never as *"the application is
driveable"*.

## Techniques that cross suites

| Technique | What it is for | Where it lives |
| ----------- | ---------------- | ---------------- |
| **Oracles** | Vendor behaviour is *measured*, not reasoned about — committed `.vis`/`.def`/`.ifb` files, report and findings exports, catalog digests | `tests/testdata/`, harnesses in `tests/shared/`; the catalog digests are recorded inside `BuiltInCatalogDigestTests` itself |
| **Property-based tests** (CsCheck) | Laws over randomized input, where the only available model would be a reimplementation of the thing under test | mostly `safe_project_tests`, `safe_unit_tests` |
| **Metamorphic laws** | Compares two genuinely different **routes** to the same destination — a bundled command against the parts applied singly, a dialog submit against one field at a time. Needs no oracle at all. Two structural traps: the compared carrier must be **mutable** (an immutable one records nothing and the property passes vacuously), and `equal:` must be supplied explicitly (the default degrades to reference equality for a class carrier) | `safe_project_tests`, `safe_unit_tests` |
| **Time as a seam** | `FakeTimeProvider` injected into `ProjectAppService`, so clock-dependent output such as report timestamps is deterministic. The `safe_visual_tests` shell harness defaults to a fake clock; `safe_lab_tests` has no clock seam, and the headless E2E driver deliberately runs on the real clock because the panel debounce is part of what a scenario waits on | `safe_project_tests`, `safe_unit_tests`, `safe_visual_tests` |
| **Survivable byte comparison** | `TestData` reports length, byte offset, line, column and a hex+ASCII window on mismatch; catalog files compare under a documented fidelity relation (`CatalogTextCompare`) with `CatalogWellFormedness` as the backstop for that relation's known blind spot | `safe_project_tests` |
| **Telemetry capture** | Asserting on emitted spans and instruments instead of on log text | `tests/shared/TelemetryCapture.cs` |
| **Shared helpers** | A helper a **second** suite needs moves to `tests/shared/` and is linked in with `<Compile Include>`, not referenced. A copy is how the two drift | `tests/shared/` |

**Oracle discipline.** Never re-save an authentic `.vis`/`.def`/`.ifb` oracle to make a test pass — byte-fidelity
tests and `.gitattributes` pin them; diagnose the product code instead. A changed validation rule moves two
committed oracle sets (`tests/testdata/validation/` and, for a DOCUMENTATION-category rule, the `full-*`
report oracles), and **both are regenerated by their `[Explicit]` test and then diffed** — never hand-edited.
Adopting a diff means explaining every changed line by a rule that changed in the same edit. Ask before
changing any committed oracle bytes.

> `[Explicit]` tests **do run** under a `--filter`. A broad fixture filter can silently invoke an oracle
> regenerator; verify with `git diff --numstat`, not by looking at file timestamps.

## Running the suites

```bash
# Controller-free suites — these are what a change is verified against
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj
dotnet test tests/safe_project_tests/safe_project_tests.csproj
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj

# Controller-backed; may toggle only the configured test resources
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# Desktop-bound. Seizes the screen for minutes and force-kills a running OpenVisual. Ask first.
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj

# The same scenarios headless, which is what CI runs
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj \
  --filter "TestCategory!=DesktopOnly" \
  -- TestRunParameters.Parameter\(name=\"headless\",value=\"true\"\)

# A single test
dotnet test <test-project.csproj> --filter "FullyQualifiedName~TestName"
```

**Prefer the per-project commands.** A bare `dotnet test` at the repository root runs every project in the
solution — the desktop-bound suite and the controller-backed one included.

**What to run after a change** is in [CLAUDE.md](CLAUDE.md) under *Verification*; it maps a changed layer to
the suites that cover it. In short: build the affected project and run the suite mapped to that layer; add
`safe_architecture_tests` for anything crossing the SDK/GUI boundary; add the matching headless UI suite for
a UI change — `safe_visual_tests`, never `safe_visual_e2e_tests`.

**CI** ([.github/workflows/build-validation.yml](.github/workflows/build-validation.yml)) builds on all three
desktop platforms and runs every suite except `safe_integration_tests`, which is compiled but never executed
against a controller. `safe_visual_e2e_tests` runs only headless and only with `TestCategory!=DesktopOnly`,
so **no CI leg needs a screen**. The per-suite runner matrix lives in that workflow rather than here, because
it changes with the runners rather than with the strategy.

## Coverage

Coverage is collected on every `dotnet test` and **reports rather than gates**: no percentage can fail a
build, so this document, not a number, decides what is worth testing. Keep it on observable product
behaviour; add null-guard, expected-exception or multithreading tests only when asked.

Each suite refreshes its own slice under `artifacts/coverage/raw/<suite>/` and every run re-merges what is
present, so a repo-wide number is current only after every controller-free suite has run. `Summary.txt` names
any stale slice and is the figure to quote. `safe_visual_e2e_tests` opts out entirely, because CI runs only
a filtered subset of it. Opt a run out with `-p:CollectCoverage=false`; an empty `--settings` fails before it
is read. Scope is declared once, in [.runsettings](.runsettings).

## Analyzer policy for test code

The suites run at the same `AnalysisMode` as the rest of the repository, and `TreatWarningsAsErrors` applies
to them too. Rules that do not fit test code are turned off **one at a time, each with its reason**, in
[tests/.editorconfig](tests/.editorconfig) — never as a blanket exemption. `tests/Directory.Build.props` is a
**layer**, not a replacement: its explicit `Import` is the only thing keeping the root build policy applying
under `tests/`, and deleting the file silently drops it.

## Action items

Known gaps between these guidelines and the current layout. None of these are done.

### A1 — `safe_visual_e2e_tests` is too slow, and most of it is at the wrong level

**Problem.** The suite tests many combinations where it should test a few representative cases. Its cost is
structural: the default mode launches the real executable per fixture and spawns **one `pwsh` process per
driver verb**, so a scenario's runtime is dominated by process startup, and a route matrix multiplies that
directly. Several scenarios there also assert facts the headless suites already own — panel ordering, tier
filtering, sort order, which findings a fixture produces, undo semantics — and at least one scenario loops
over an input combination (`byKeyboard` true/false) *inside* an end-to-end test.

**Action.** Reduce the suite to a small set of representative scenarios that clear
[the bar](#the-end-to-end-bar): a scenario stays only if it can fail for a reason that exists solely in the
real desktop. Push the rest down:

- combinations and route matrices → `safe_visual_tests` (if a control is needed) or `safe_project_tests`
- business logic — ordering, filtering, counts, refusal wording, validation outcomes → `safe_project_tests`
- anything asserting a view-model's behaviour → `safe_project_tests`

The target is the coverage this suite *uniquely* provides, at a runtime short enough that running it is a
normal decision rather than an event.

### A2 — OpenVisual non-GUI tests sit in `safe_visual_tests`

Roughly half of `safe_visual_tests` compiles without any Avalonia type — view-models, stores, route planners,
presentation mapping, host catalogs, telemetry registries. By the routing rules those belong in
`safe_project_tests`, which will need a `ProjectReference` to `ihc_openvisual` — and, for any migrated test
that touches an internal, an `InternalsVisibleTo` entry in `ihc_openvisual.csproj`, which today names only
`safe_visual_tests` and `safe_visual_e2e_tests`.

The candidate set is re-derivable rather than listed here:

```bash
grep -L "AvaloniaTest\|Avalonia\." tests/safe_visual_tests/*.cs
```

### A3 — `safe_unit_tests` holds suite-specific tests

The misplaced tests fall into three groups, and the set is re-derivable from which project declares the
subject type. A `using` naming `IhcLab`, `ihc_openvisual` or `Ihc.Vis` is the usual tell but not a complete
one: `ihc_lab` declares its operation-filter configuration in the `Ihc.App` namespace.

- `IhcLab.*` subjects — the parameter-control strategy tests, the operation-filter configuration test and the
  Lab backend smoke test → `safe_lab_tests`
- `ihc_openvisual` subjects with no Avalonia dependency — projector, menu forest, command registry, context
  gate, automation ids, variable palette and value format, report self-containment, design-time view-models →
  `safe_project_tests`
- `Ihc.Vis` subjects — the report oracle and report-icon tests → `safe_project_tests`

What stays is what the suite is for: general SDK behaviour, plus the utilities and `shared/` projects that
have no suite of their own. The `LabAppService` tests stay too: that service is SDK code in `Ihc.App`, not
part of the Lab utility.

### A4 — the safety rule is convention, not a mechanism

Nothing prevents a future test from reaching a controller from a suite that may not. If this ever bites, the
fix is a fixture-level guard, not more prose.

### A5 — no derivation gate on the generated SOAP layer

"Regenerate, never hand-edit" rests on prose in `ihcclient/README.md`, `ARCHITECTURE.md` and `CLAUDE.md`
alone. The embedded
catalog has two gates for the equivalent property (`VerbatimFreeGateTests` for what the code *contains*,
`BuiltInCatalogDigestTests` for what it *evaluates to*); the SOAP side has neither.

## See also

- [ARCHITECTURE.md](ARCHITECTURE.md) — layers, invariants, and the design challenges the tests defend
- [CLAUDE.md](CLAUDE.md) — *Verification*: which suites to run after which change
- [tests/testdata/testdataoverview.md](tests/testdata/testdataoverview.md) — the oracle corpus and its provenance
- [tests/safe_lab_tests/README.md](tests/safe_lab_tests/README.md) — Lab fixtures, fakes and screenshots
- [docs/adr/](docs/adr/) — ADR-001 (threading), ADR-002 (service tiers / thin apps), ADR-004 (bans over architecture tests)
