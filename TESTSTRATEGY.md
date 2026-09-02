# Test Strategy

How this repository's tests are levelled, what each suite is for, and — the question this document exists to
answer — **which suite a new test belongs in**.

**The scope is automated testing.** Manual testing of the product is not covered here, nor is anything
organizational. One activity crosses that line deliberately — the vendor measurement session in
[when to re-measure](#techniques-that-cross-suites) — because its output is an oracle, and an oracle is
automation input.

These are guidelines, not an inventory. No section lists which tests exist today; where the current layout
disagrees with a rule below, that is recorded once in [Action items](#action-items) rather than described
per file. [What this does not cover](#what-this-does-not-cover) records the deliberate omissions, so that a
gap measured against a general test-strategy standard reads as a decision rather than an oversight.

- [The safety rule](#the-safety-rule)
- [What this does not cover](#what-this-does-not-cover)
- [What is at stake](#what-is-at-stake)
- [Levels and suites](#levels-and-suites)
- [The end-to-end bar](#the-end-to-end-bar)
- [When a test leaves](#when-a-test-leaves)
- [Errors that do not surface](#errors-that-do-not-surface)
- [Designing the cases](#designing-the-cases)
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

Two kinds of test affordance exist here. A **seam** is a substitution point a test configuration reaches: the
`IIHCApiService` and `ICatalog` fakes, `TimeProvider`, `IDialogService`. A **built-in test feature** exists to
support testing or diagnosis from inside the product — `AllowDangerousInternTestCalls` unlocks manufacturing
operations against a live controller, `LogSensitiveData` turns redaction off, and `ihc_lab`'s `mock://`
endpoint swaps its whole service stack. What separates them is not that one ships and the other does not:
plenty of seams ship, and only the debug-gated developer tools are genuinely compiled out. The distinction
that matters is **what a released binary can be talked into doing** — which is why the safety rule covers
both, and why [A4](#a4--the-safety-rule-is-convention-not-a-mechanism) is about both sides of it.

What the seam does not buy is **fidelity**. The fakes are hand-written and nothing pins them to a real
controller: `tests/testdata/` holds no recorded controller traffic, and `safe_integration_tests` is compiled
in CI but never executed against a device. So a fake can be wrong from the day it was written, or model only
the paths some test happened to need, and no gate would say so. Drift is not the worry it would be elsewhere
— the controller is discontinued, so the behaviour behind the fakes is frozen rather than moving — but that
is an argument for recording it *once*, not for never recording it: a corpus captured against a provider that
has stopped changing never goes stale. What the wire↔model mapping does have is checks that share no device:
verbatim controller-shaped envelopes asserted in both directions, round-trip laws over randomized input, and
transport tests that run the real handler chain over a stub socket. What it has never had is **one whole
operation replayed as a real controller actually answered it**.

Vendor behaviour is pinned by **oracles** rather than by reasoning: committed `.vis`/`.def`/`.ifb` files under
`tests/testdata/`, provenance documented per file in [testdataoverview.md](tests/testdata/testdataoverview.md).
Only part of that corpus is vendor ground truth — the `.vis` projects the vendor application saved and the two
`.ifb` exports it produced; every `.def` oracle and the rest of the `.ifb` and `.vis` files are synthetic —
and the authentic files are kept physically separate from the synthetic ones. That separation records
**provenance, not confidence**. The synthetic files are known correct, so an oracle taken from either half
carries the same authority and neither half is a mere change detector. Saying so is what stops a reader
inferring the opposite from the word *synthetic* and setting about repairing a corpus that needs no repair.

## What this does not cover

Deliberate omissions. Each is a decision with a reason, not a gap:

- **Manual testing of the product.** The scope is automation. The one admitted exception is the vendor
  measurement session above, whose output is an oracle.
- **Review as a practice.** Reading is what caught both defects in
  [Errors that do not surface](#errors-that-do-not-surface), and is named there for that reason — but a
  review process, its triggers and its checklists are not automation. The automation-side answer to that
  pair is [A7](#a7--two-surfacing-mechanisms-are-construction-not-detection); until it exists, the hand-off
  to review is across this boundary rather than a hole inside it.
- **Entry and exit criteria.** [CLAUDE.md](CLAUDE.md) under *Verification* already maps a changed layer to
  the suites that cover it. A second, outcome-shaped definition of *verified* would be another place to keep
  in step with the first, and drift between the two would be worse than the absence.
- **Requirement-to-test traceability.** Wiring the
  [user-story index](applications/ihc_openvisual/docs/stories/INDEX.md) to tests would contradict the
  no-inventory rule above, and where traceability is load-bearing it already exists: the problem catalogue's
  completeness gate fails a code with nothing behind it.
- **Test types that need a human to run them**, and everything organizational — who tests, and how
  independent they are.

## What is at stake

[Levels and suites](#levels-and-suites) answers *where* a test goes. This answers *how much*. They are
independent axes, and a subject's tier is what decides whether a given discipline is worth its cost there —
without it, "the cheapest level that can hold it" reads as though every subject deserves the same defence.

| Tier | Worst case when it is wrong | Subjects |
| ------ | ----------------------------- | ---------- |
| **Critical** | Silent, and the damage outlives the session. A `.vis` written wrong corrupts an installation inside a file the customer keeps; a controller read that is wrong misinforms a user, or becomes the basis of a write that misbehaves a real building | `.vis`/`.def`/`.ifb` reading and writing; controller I/O in **both** directions — the mapping between wire and model, not only the operations that change state |
| **High** | A wrong answer the user acts on, but visible and recoverable | The validation engine and problem catalogue; the editing session, its commands and undo/redo; reporting; the SDK's transport, authentication and session handling — the plumbing that carries a value, as against the mapping that decides what it is |
| **Normal** | A visible, fixable annoyance | OpenVisual's GUI shell, `ihc_lab`, SDK models and settings |
| **Low** | Barely noticed | Utilities, examples, design-time and diagnostic code |

**Reads sit in the top tier alongside writes, deliberately.** A wrong read is upstream of a wrong write
wherever the write is derived from what was read, so a read defect does not stay a read defect; and a wrong
value shown to a user needs no write at all to do damage.

What a tier buys is **depth, not existence** — every subject is tested, and the tier decides how many
independent ways. A Critical subject earns more than one kind of check over the same path, because a single
oracle that is quietly wrong is indistinguishable from a subject that is right: byte fidelity *and* a law
that holds over randomized input, or a second opinion that shares no code with the first. High earns an
oracle and the assertions around it. Below that, checking part of the output is worth very nearly as much as
checking all of it, and the effort saved is better spent further up this table than on completeness further
down.

## Levels and suites

One suite per level per subject. A test belongs at the **cheapest level that can hold it** — a level exists
for what the level below genuinely cannot reach, not as a place to repeat it. To place a test, read the rows
in order and stop at the first whose subject claims it; the last row is the general home.

| Suite | Level · cost | Subject — what it is for | Not here |
| ------- | -------------- | -------------------------- | ---------- |
| `safe_architecture_tests` | Architecture · s | Compliance with the design: layering, dependency direction, banned shapes, the invariants in [ARCHITECTURE.md](ARCHITECTURE.md). ArchUnitNET over one IL model per assembly | Behaviour of any kind |
| `safe_integration_tests` | Controller · s, never in CI | General `ihcclient` behaviour against a **real controller**, through state-safe operations only. Needs `ihcsettings.json`; the configured test resources' outputs may change when it runs | Anything that can be verified controller-free |
| `safe_lab_tests` | Headless UI + unit · s | The `utilities/ihc_lab` application and only that — its GUI, parameter-control strategies, operation filtering, fakes. `[NonParallelizable]`, `mock://` services | Anything not `ihc_lab` |
| `safe_visual_tests` | Headless UI · s | `applications/ihc_openvisual`'s **GUI** — whatever needs an Avalonia application, window, control, style or automation peer — but in a headless execution environment. Real `MainWindow` on the headless backend. Also the Avalonia-dependent `shared/` bootstrap code both apps host (`shared/ihc_appbootstrap`), which has no toolkit-free level to sit at. Run on **Linux** as well as Windows: that leg is the only one that catches an *Avalonia* portability defect, the toolkit-free suites running on all three platforms and catching the rest | Anything that compiles without an Avalonia type: view-model logic, stores, presentation mapping, host catalogs. Those are OpenVisual's non-GUI code and belong in `safe_project_tests`. Anything about `ihc_lab`, which belongs in `safe_lab_tests` |
| `safe_visual_e2e_tests` | End-to-end GUI · **minutes** | The confidence **no other suite can give**: that the shipped app is driveable. A thin, deliberately small set of representative scenarios — see [the bar](#the-end-to-end-bar) | Combinations, route matrices, business logic, anything still testable without real GUI |
| `safe_project_tests` | Engine · ms–s | Not general to the SDK but about the project domain: the `Ihc.Vis` engine, `ProjectAppService`, sessions and commands, validation rules, the problem catalogue, reporting — **and OpenVisual's non-GUI code** (view-models, services, stores, route planners, presentation mapping) — for OpenVisual code the rule, not yet the layout, until [A2](#a2--openvisual-non-gui-tests-sit-in-safe_visual_tests) lands. Real application services, oracle files, and the catalog the test's scope calls for — `BuiltInCatalog` or a fake `ICatalog` | Anything needing execution in Avalonia |
| `safe_unit_tests` | Unit · ms | The `ihcclient` SDK in general — transport, models, serialization, security, settings, telemetry primitives. Also **any utility or `shared/` project with no suite of its own**. Faked at the `IIHCApiService` seam | Anything Avalonia-shaped; anything app-specific |

Two rules for the architecture suite, because both are easy to get wrong: a clean subject with an empty
exemption roster is indistinguishable from a **broken detector**, so every scan carries a seeded violator
proving it can fail — the seed families several detectors share live in `ArchitectureDetectorSeeds.cs`, a seed
only one detector needs sits beside that detector; and a **ban file beats a fluent rule** where it can
express the same thing ([ADR-004](docs/adr/ADR-004-compile-time-bans-over-architecture-tests.md)) — fluent
bans go vacuous for types an assembly never references.

`safe_project_tests` is the **subcutaneous** suite, and naming that is what turns
[A1](#a1--safe_visual_e2e_tests-is-too-slow-and-most-of-it-is-at-the-wrong-level) from a wish into a target.
A subcutaneous test enters just beneath the UI — through the same facade the GUI itself calls,
`ProjectAppService`, `IProjectDocument`, the command gateway — runs the application and domain layers for
real, and fakes only the outbound ports. That is the seam [the safety rule](#the-safety-rule) already
describes, arrived at from the other direction. It stays faithful only while the UI above it is thin, and
here that is not an assumption but a rule with a detector behind it: the architecture suite requires the view
layer to drive the model through its view-model, never through `IProjectDocument`, `ProjectWorkflow` or
`ProjectAppService` directly.

Two consequences follow. Because this suite is the primary functional gate, **its runtime and its flake rate
are the numbers that matter about it**, and neither is its coverage. Both have a record rather than a
dashboard: a CI run writes a TRX naming every test and its duration whether the run was green or red, so a
test that varies with no code change between two of them is visible without new infrastructure — as long as
the suite ran at all, which a failure earlier in the leg prevents. Runtime belongs in minutes and flakes at
none; what stands between the suite and the first of those is
[A9](#a9--every-suite-runs-serially-and-nothing-says-whether-that-is-a-choice). And when
`safe_visual_e2e_tests`
repeatedly catches something this suite passed, read it as a design signal rather than a missing scenario:
behaviour has leaked upward into the view, and the fix belongs in the view-model — adding another end-to-end
scenario only pins the leak in place.

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

**When it fails, classify before fixing.** A suite with a process boundary fails for six distinguishable
reasons, and the expensive mistake is to assume the first one.

| Cause | How to tell |
| ------- | ------------- |
| **A product defect** | Reproduce by hand, and check whether the same scenario passes one level down |
| **A missing test hook** | The driver is waiting on a duration because the application publishes no signal to wait on, or reading state it does not expose |
| **A test defect** | Setup, addressing or assertion is wrong; compare against a passing scenario in the same fixture |
| **Infrastructure** | Correlates with runner load, or with an environment difference — OS, locale, screen |
| **A polluter** | Passes alone, fails in the run: another test left state behind |
| **Unclassified** | Time-boxed. After half an hour, record what was ruled out rather than guessing |

## When a test leaves

The routing rules say where a test belongs. On their own they do not stop a suite accreting past its bar —
which is how [A1](#a1--safe_visual_e2e_tests-is-too-slow-and-most-of-it-is-at-the-wrong-level) arose, and why
emptying that suite once would be a cleanup rather than a policy.

A test is removed when it stops earning its level: it asserts what a cheaper suite now asserts, its subject
moved and it stayed, or it pins a behaviour that has since been deliberately dropped. Delete it rather than
weaken it — a test kept alive by loosening its assertion still costs its runtime and no longer defends
anything. Oracles are the exception, and only in procedure: the discipline below governs their removal as
much as their change, so a committed oracle leaves with the same explanation a diff to it would need.

A trigger says a test *may* go, not that nothing goes with it, so removal has a precondition: **name the
suite that now asserts what it asserted**. Removing the last cover for an error path, a recovery path or a
historically defect-prone one is the removal that costs the most and shows the least, and a trigger alone
will not catch it.

**Flakiness is a removal trigger in its own right.** A test that fails and then passes with no code change is
worse than no test, because it teaches everyone to re-run the suite rather than read the failure. The three
usual causes — shared state, real time, ordering — are the three a test can be designed out of: isolation, a
clock seam, an explicit wait. **Classify which one before deciding.** Flakiness is testability debt made
visible, so the classification usually names the fix; and the cause is sometimes a defect in the product, so
deleting the test deletes the evidence. The test goes when the classification says the cost is structural —
never because no cause was looked for. Retrying and quarantining are not alternatives: a suite that is re-run
until it passes has stopped being evidence. This bites hardest in `safe_visual_e2e_tests`, where a process
boundary and real timing make flakiness the dominant failure mode rather than an occasional one.

## Errors that do not surface

An error swallowed somewhere down a nested call — never thrown on, never traced, never shown — is the failure
mode this repository has to work hardest to test, for one reason: **a swallowed error makes a test pass.**
`Assert.DoesNotThrow` is satisfied equally by an operation that worked and by one that failed quietly, so the
usual assertion is blind to precisely the defect that matters. Testing for it means asserting on what the
failure was supposed to *produce* — a span outcome, a log row, a dialog — and never on the absence of an
exception.

The three ways it goes quiet are one per condition a fault has to clear to be caught. Under the **RIPR** model
a test detects a fault only if it *Reaches* the faulty code, *Infects* the state, that state *Propagates* to
an output, and the oracle *Reveals* it by looking at that output. Reach and infect are what a test case buys;
the last two are what a swallowed error destroys. Naming which one a row breaks is what decides the technique
that answers it — and it is why the three are not equally defended.

| How it goes quiet | Condition it breaks | What stands in the way |
| ------------------- | --------------------- | ------------------------ |
| **Never observed at all.** An `async void` handler faults after its first await, or a `Task` is produced and discarded, so the fault is raised on the finalizer thread arbitrarily later — or never | **Propagation.** The carrier is discarded, so the state never reaches an observation boundary at all | Detected **in the GUI assembly**, because a structural failure admits a structural answer: the architecture suite requires every `async void` handler there to reach a containment floor and every discarded task to be handed to `TaskSupervisor`, which observes it at once and reports it with the origin the fault itself cannot carry. Two limits worth knowing — the scans do not reach `ihcclient` or the utilities, and the discard scan stands itself down on a statically instrumented coverage run |
| **Observed and shown, but not traced.** The catch site shows the installer a dialog and forgets the span's outcome, so the operation failed for the user and succeeded in the telemetry | **Revealability.** It did propagate to an output; the oracle read the dialog and not the span. Only asserting the second output closes it | Construction only. `FailureReport` folds the outcome, the log record and the dialog into one call, in that order — the outcome before the dialog, because a dialog awaits a person and a process that dies while the modal is up would record nothing |
| **Traced and shown, but in the wrong shape.** A `ProblemChain` is one failure restated more precisely; a `ProblemAggregate` is N independent failures. Rendering either by the other's rule shows one failure twice, or loses all but one of N findings | **Propagation, lossily.** Information is lost on the way out, which is the canonical masking mechanism. Assert the shape, not the presence | Construction only, and incompletely. `RaisedProblemDisplay` is the intended single decider, and it is the only caller of the aggregate overload — but the shell's widest catch renders an unexpected exception as a bare problem without going through it, so an aggregate escaping that far still loses all but one item |

The two construction-only rows are where the risk actually lives, and the history says so: both defects have
already shipped, and **neither was found by a test — both were found by reading.**
[A7](#a7--two-surfacing-mechanisms-are-construction-not-detection) records that gap.

Where these are tested, they are tested by their artifacts. The span outcome is a
[telemetry capture](#techniques-that-cross-suites) assertion, not a log-text one. The dialog is observable
because `IDialogService` is a port `FakeDialogService` stands in for, so "the installer was told" is a fact a
headless test can assert. `InternalErrorLog` is the collection point for faults in the *tool* as distinct from
findings about the project, and it is readable — a test can assert a fault arrived there rather than vanishing.
An error path on a [Critical](#what-is-at-stake) subject earns an assertion on **every channel that exists on
that path**, because an error that is traced but not shown and an error that is shown but not traced are
different defects with the same symptom: someone, later, cannot tell what happened. That is the table above
restated — the two failures break different conditions, so neither assertion substitutes for the other.

Which channels exist is a property of the path, not a target to hit. A `.vis` I/O failure is an anticipated
one, so `FailureReport` deliberately writes **no** internal-error row for it and the correct assertion there
is that the row is absent — which is what the tests assert. The SDK has no dialog and no internal error log at
all, so a controller path has the span and nothing else. Asserting a channel a path does not have is how a
rule like this turns into ceremony.

## Designing the cases

Routing says where a test goes and the tier says how much a subject earns. Neither says what the cases should
be — usually the author's judgement, but three subjects here have a shape that answers it for them.

- **A validation rule is a decision table.** It fires on a combination of conditions, so the cases are the
  rows of that table rather than a walk through the ones that came to mind. A row nobody wrote down is a rule
  nobody tested.
- **`.vis` loading is an equivalence-partition and boundary problem.** Well-formed, malformed and
  *almost*-valid are three partitions, and the inputs that matter sit on the edges between them. The
  open-world reader makes that boundary a judgement rather than a given: unknown attributes and element types
  are accepted and preserved by design, so "malformed" means what the reader is specified to reject, and a
  case is only a case once its expected outcome is one of those two.
- **A route matrix is a combinatorial problem, and those have a standard answer.** Where a scenario varies
  over independent choices — entered by keyboard or by mouse, reached from one panel or another — covering
  every combination is the expensive way to cover the pairs that actually interact. Pairwise selection cuts
  the count without moving the coverage, and it is the half
  [A1](#a1--safe_visual_e2e_tests-is-too-slow-and-most-of-it-is-at-the-wrong-level) cannot achieve by
  relocating a matrix to a cheaper level.

This section is deliberately short. A technique earns a place here only when the subject's shape makes the
case list a consequence rather than a choice.

## Techniques that cross suites

| Technique | What it is for | Where it lives |
| ----------- | ---------------- | ---------------- |
| **Oracles** | Vendor behaviour is *measured*, not reasoned about — committed `.vis`/`.def`/`.ifb` files, report and findings exports, catalog digests | `tests/testdata/`, harnesses in `tests/shared/`; the catalog digests are recorded inside `BuiltInCatalogDigestTests` itself |
| **Property-based tests** (CsCheck) | Laws over randomized input, where no independent model is available and the only alternative would be a reimplementation making the same assumptions as the original — which is not a second opinion. See **Differential** for when a reimplementation *is* the right answer. Two things a law has to earn: an oracle it does not share with the code under test ([A12](#a12--a-property-test-carrying-one-oracle-that-is-a-copy-of-the-code-it-checks)), and a stated position on seeding — a discovered counterexample is a finding, not flakiness, so what a failure owes the next run is the seed that reproduces it | mostly `safe_project_tests`, `safe_unit_tests` |
| **Metamorphic laws** | Compares two genuinely different **routes** to the same destination — a bundled command against the parts applied singly, a dialog submit against one field at a time. Needs no recorded expected output — but it cannot say the answer both routes reached is the *right* one, only that they agree, and two routes through the same defect agree happily. On a [Critical](#what-is-at-stake) path a law complements an oracle rather than replacing one. Two structural traps: the compared carrier must be **mutable** (an immutable one records nothing and the property passes vacuously), and `equal:` must be supplied explicitly (the default degrades to reference equality for a class carrier) | `safe_project_tests`, `safe_unit_tests` |
| **Differential** | Recomputes a result by a second implementation sharing no code with the first, and requires the two to agree. What makes it a second opinion rather than the same opinion twice is that the two must differ in something that matters: `ChangeSetDifferentialTests` recomputes the session's change set *without* the reference-equality shortcut the real diff depends on, so the two agree only while sharing genuinely implies equality — which is the property actually under test. The vendor application is the other standing second implementation of the `.vis` specification, and comparing against it is a measurement, not a committed file | `safe_project_tests` |
| **Time as a seam** | `FakeTimeProvider` injected into `ProjectAppService`, so clock-dependent output such as report timestamps is deterministic. The `safe_visual_tests` shell harness defaults to a fake clock; `safe_lab_tests` has no clock seam. The E2E drivers run on the real clock, and where a signal exists they wait on it rather than on a duration — the headless driver on the panel's idle signal, the desktop driver on a `--wait` readiness query — with the clock supplying only the timeout. That covers panel readiness; most other desktop verbs still settle by a fixed sleep after the gesture, because the application publishes nothing to wait on. Those are the missing hooks, not the design | `safe_project_tests`, `safe_unit_tests`, `safe_visual_tests` |
| **Survivable byte comparison** | `TestData` reports length, byte offset, line, column and a hex+ASCII window on mismatch; catalog files compare under a documented fidelity relation (`CatalogTextCompare`) with `CatalogWellFormedness` as the backstop for that relation's known blind spot | `safe_project_tests` |
| **Telemetry capture** | Asserting on emitted spans and instruments instead of on log text | `tests/shared/TelemetryCapture.cs` |
| **Screenshots** | Failure diagnostics, not baselines: a failing headless UI test writes a PNG and attaches it to the result, and nothing compares it to anything. Automated visual regression is **deliberately not adopted** — the headless renderer paints only the first window shown, so a second window or content swapped into a shown one captures blank, and a baseline would report that harness limit as a regression forever. Visual change is reviewed by eye on the diff instead | `tests/shared/HeadlessScreenshot.cs`, `tests/shared/ScreenshotCaptureCommand.cs` |
| **Performance budgets** | The hot paths carry a wall-clock budget — the drag-over probe, commit, undo/redo, save, open. They are measured by an `[Explicit]` benchmark run by hand in Release, never in a gate: the figures are machine-specific, so an absolute threshold in CI would be measuring the runner. Enforcing them would mean comparing against a baseline on a pinned machine, which does not exist. Until it does, a budget is a claim someone has to re-check on purpose | `tests/safe_project_tests/benchmarks/` |
| **Shared helpers** | A helper a **second** suite needs moves to `tests/shared/` and is linked in with `<Compile Include>`, not referenced. A copy is how the two drift | `tests/shared/` |

**Oracle discipline.** Never re-save an authentic `.vis`/`.def`/`.ifb` oracle to make a test pass — byte-fidelity
tests and `.gitattributes` pin them; diagnose the product code instead. A changed validation rule moves two
committed oracle sets (`tests/testdata/validation/` and, for a DOCUMENTATION-category rule, the `full-*`
report oracles), and each is regenerated by an `[Explicit]` test and then diffed — never hand-edited. Three
such regenerators exist rather than two, because the report family's HTML half regenerates from a different
suite. None of them writes over the committed file: they emit beside the test binary, so adopting a diff is a
deliberate copy after reading it, and that copy means explaining every changed line by a rule that changed in
the same edit. Ask before changing any committed oracle bytes.

> `[Explicit]` tests **do run** under a `--filter`. A broad fixture filter can silently invoke an oracle
> regenerator; verify with `git diff --numstat`, not by looking at file timestamps.

**When to re-measure.** An oracle records what the vendor did on the day it was captured, and provenance is
not freshness: `testdataoverview.md` says where every file came from, never whether it still answers the
question now being asked of it. The trigger is the kind of claim, not the calendar — when a change turns on
behaviour that was originally *observed* rather than specified, measure the vendor again rather than
reasoning from a frozen file about a case it was never captured to cover. That measurement is exploratory
work: a charter, a session at the vendor driver, and notes. It earns its keep only when what it finds becomes
a test or an oracle in the same edit — a campaign whose findings stay in scratch notes has bought nothing the
next change can use, and the file it did not produce is the one someone will reason from instead.

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
  -- 'TestRunParameters.Parameter(name="headless",value="true")'

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
against a controller. Every suite runs on at least one leg, which is not the same as every leg running every
suite: the UI suites are Windows-only or Windows-and-Linux, so macOS builds the applications and runs the
toolkit-free suites alone. `safe_visual_e2e_tests` runs only headless and only with `TestCategory!=DesktopOnly`,
so **no CI leg needs a screen**. Every suite runs under `--blame-hang` and `--blame-crash` and writes a TRX
whether it passed or failed: a hang or a pool-thread crash is otherwise a job that times out with no test
named. Read those results knowing two things — a failing suite ends the leg, so the suites after it produce
nothing at all, and a hang takes the host down with whatever had not yet run. The per-suite runner matrix
lives in that workflow rather than here, because it changes with the runners rather than with the strategy.

## Coverage

Coverage is collected on every `dotnet test` and **reports rather than gates**: no percentage can fail a
build, so this document, not a number, decides what is worth testing. Keep it on observable product
behaviour; add null-guard, expected-exception or multithreading tests only when asked.

Each suite refreshes its own slice under `artifacts/coverage/raw/<suite>/` and every run re-merges what is
present, so a repo-wide number is current only after every controller-free suite has run. `Summary.txt` names
any stale slice and is the figure to quote. `safe_visual_e2e_tests` opts out entirely, because CI runs only
a filtered subset of it. Opt a run out with `-p:CollectCoverage=false`; an empty `--settings` fails before it
is read. Scope is declared once, in [.runsettings](.runsettings).

Coverage says which lines ran, never whether a test would have noticed them going wrong. The measure for
that is **mutation**: break the product code in small ways and see whether a test fails. It is run by hand,
on a [Critical](#what-is-at-stake) subject, when there is reason to doubt an oracle is load-bearing — not on
a schedule and not as a gate, for the same reason no coverage percentage gates a build. A surviving mutant is
a question rather than a defect, and it has three answers, cheapest first: the **assertion** is too weak and
the test already there would kill the mutant if it looked harder; a **test** is missing, because no input
reaches the mutated behaviour at all; or the behaviour was **never load-bearing**. Reach for the first before
the second — a mutant that a stronger assertion kills needs no new input, and reading it as a missing test
buys a test case to solve an oracle problem.

Mutating the product code is a different practice from mutating the corpus: one asks whether the tests would
notice a broken engine, the other whether the engine survives a broken file. Both are worth having and neither
substitutes for the other.

## Analyzer policy for test code

The suites run at the same `AnalysisMode` as the rest of the repository, and `TreatWarningsAsErrors` applies
to them too. Rules that do not fit test code are turned off **one at a time, each with its reason**, in
[tests/.editorconfig](tests/.editorconfig) — never as a blanket exemption. `tests/Directory.Build.props` is a
**layer**, not a replacement: its explicit `Import` is the only thing keeping the root build policy applying
under `tests/`, and deleting the file silently drops it.

That tier is not only a test-code concession — it is the repository's **static-analysis layer**, and worth
claiming as such. Nullable enabled everywhere, `AnalysisMode=Recommended` and warnings-as-errors are what
stand in for a pass over variable lifecycle and data flow, which is why no suite carries one and why none
needs to.

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

- combinations and route matrices → `safe_visual_tests` (if a control is needed) or `safe_project_tests`, and
  cut by pairwise selection where the choices are independent — see [Designing the cases](#designing-the-cases)
- business logic — ordering, filtering, counts, refusal wording, validation outcomes → `safe_project_tests`
- anything asserting a view-model's behaviour → `safe_project_tests`

**Second action, and the larger lever.** Scenario count sets the bill; the *transport* sets the unit price —
not the driver architecture, which is already what it should be. `IE2EDriver` is one generic
`Run(verb) → envelope` seam with two bindings behind it, so there is no abstraction left to introduce there.
The cost sits inside one binding: `AuiProcessDriver` spawns a `pwsh` process per verb, so a scenario's runtime
is process startup × verb count. That is reducible without touching the seam — hold one driver session open
across a fixture instead of paying for a process per verb.

What is *not* available is folding the two bindings together to make the headless leg evidence about the
desktop leg. `IE2EDriver`'s own remarks record why they answer different questions and are not interchangeable:
the headless driver reads Avalonia's automation peers directly, so it cannot see a defect in the bridge that
projects those peers onto Windows UIA — the class this suite was written for; it never runs `aui.ps1`; and it
reaches several outcomes by assigning view-model state, which the driver manifest classifies as *synthetic* and
therefore evidence of an outcome rather than of a route. Sharing more code between them would narrow neither
gap. That distance is the suite's design, and [the bar](#the-end-to-end-bar) is what keeps it small.

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

**The gating prerequisite is not in that list.** `ShellHarness` and `FakeDialogService` are declared inside
this suite, and most files on both sides of the split use them — so the migration divides its own principal
fixture across two assemblies. They move to `tests/shared/` first, or nothing else moves. A handful of the
files the grep returns are helpers rather than tests and go to `tests/shared/` too, not to
`safe_project_tests`.

**What moves is the composition, not the harness.** The e2e suite paid for these helpers' absence once by
rebuilding the shell by hand in its headless driver, which reads as a third caller waiting for the move — but
it is not one. `ShellHarness` supplies a `FakeDialogService` and a fake clock, and the headless driver exists
to drive real dialogs on a real dispatcher through a window whose lifetime it owns; handing it the harness
whole would fake out precisely what it is there to exercise. So the shared thing is the layer underneath — the
temp workspace, the `ProjectAppService` construction, the recent-projects store — with each suite assembling
the shell it needs on top. Sharing the assembled harness across all three is how it acquires a mode switch per
caller.

### A3 — `safe_unit_tests` holds suite-specific tests

The misplaced tests fall into three groups, and the set is re-derivable from which project declares the
subject type. A `using` naming `IhcLab`, `ihc_openvisual` or `Ihc.Vis` is the usual tell but not a complete
one: `ihc_lab` declares its operation-filter configuration in the `Ihc.App` namespace.

- `IhcLab.*` subjects — the parameter-control strategy tests, the operation-filter configuration test and the
  Lab backend smoke test → `safe_lab_tests`
- `ihc_openvisual` subjects with no Avalonia dependency — projector, menu forest, command registry, context
  gate, automation ids, variable palette and value format, report self-containment, design-time view-models →
  `safe_project_tests`
- the report oracle and report-icon tests → `safe_project_tests`. These read as `Ihc.Vis` subjects and are not:
  both open on `ihc_openvisual.Services`, so they are the group above by another name — and one of them is an
  oracle regenerator, which is a reason to move it rather than a reason to leave it

What stays is what the suite is for: general SDK behaviour, plus the utilities and `shared/` projects that
have no suite of their own. The `LabAppService` tests stay too: that service is SDK code in `Ihc.App`, not
part of the Lab utility.

The move takes the two `ProjectReference`s with it: this suite references `ihc_lab` and `ihc_openvisual` only
to host the tests above, so once those leave, both references are dead weight and should go with them. That is
a routing result and not an architectural one — a *test* project may reference an application, several already
do, and [A2](#a2--openvisual-non-gui-tests-sit-in-safe_visual_tests) proposes adding one on purpose. The
architecture rules constrain what the product assemblies may depend on, not who is allowed to test them.

### A4 — the safety rule is convention, not a mechanism

Nothing prevents a future test from reaching a controller from a suite that may not. If this ever bites, the
fix is a fixture-level guard, not more prose. That is this document's item, and it is the whole of this
document's item.

**A neighbouring hazard that is not ours.** The built-in test features named in
[the safety rule](#the-safety-rule) ship inside the released binary and are each held back by one settings flag
plus prose in [CLAUDE.md](CLAUDE.md) under *Ask First*. An affordance that unlocks destructive controller
operations is the kind that earns a second, independent gate — an authorization a settings file alone cannot
grant. It is recorded here because the safety rule is where the reader meets those features, but it is a
product and security-design decision reached from the product, not from a test, and a test-layer guard cannot
close it. Whoever picks it up should take it to `ARCHITECTURE.md`; nothing in the suite layout will move it.

### A7 — two surfacing mechanisms are construction, not detection

**Problem.** Of the three ways a fault can go quiet ([above](#errors-that-do-not-surface)), only the first is
scanned for. Every `async void` handler must reach a containment floor and every discarded task must be
supervised, and both rules have a detector with a seeded violator behind it. The other two do not.
`FailureReport` folds the span outcome, the log record and the dialog into one call, and
`RaisedProblemDisplay` decides whether a raised exception is shown as a chain or an aggregate — but nothing
detects a **new** catch site that goes through neither. Both defects those two exist to prevent have already
reached shipped code, and both were found by reading rather than by a test — which is the same review that
would have to catch the next one.

**Action.** A scan is expressible in the shape the two containment scans already use, but it has to be scoped
by *member* and not by port. `IDialogService` is the application's entire dialog surface — file pickers, save
confirmations, every property editor, the about box, the settings viewer — so a rule reading "reaches
`IDialogService` without reaching `FailureReport`" indicts the ordinary interactive UI along with the defect,
and buys an exemption roster longer than the population it polices. Worse, a roster that large stops being
evidence: exempting a picker proves nothing about telemetry either way.

Scope it instead to the members that actually present a fault — the `ShowProblemAsync` overloads and
`ShowInternalErrorAsync`. That is a small, stable set whose only legitimate callers are the two helpers
themselves: `FailureReport` reaching the port directly, and `RaisedProblemDisplay` reaching it on
`FailureReport`'s behalf. Every remaining site is the thing being looked for. The rule is then: a site that
shows a problem from inside an instrumented workflow without that workflow's scope being told. It needs a
seeded violator like its neighbours, and the judgement call it turns on is what counts as a workflow type.

One trap for whoever writes it: at least one of these members is not *called* anywhere near where it is used.
It is handed to another view-model as a method group at construction, so a scan matching invocations sees
nothing and the member is invoked later from a component the scan never associated with a workflow. Match the
member reference, not the call. The alternative fix is to make the scan unnecessary — give those members one
entry point by construction, the way the containment floors work, so bypassing it is a compile-time
impossibility rather than a scan result, and the delegate problem disappears with it.

Do not scope it to catch sites. Almost every catch that shows a dialog already goes through the helper; the
one that does not is the shell's general command boundary, which hand-rolls the triplet. The larger population
is not catches at all — a refusal shown as a dialog from a plain conditional branch, where the scope was never
told and ends `ok` because that is its default. `FailureReport.RefusedAsync` exists for exactly that shape and
is barely used. A scan that looks only for `catch` misses the bigger half by construction.

### A9 — every suite runs serially, and nothing says whether that is a choice

**Problem.** NUnit runs tests serially unless a suite opts in, and nothing here opts in — so every
`[assembly: NonParallelizable]` in the repository is presently a **no-op**, documentation rather than
mechanism. That makes the attribute's real value its comment, and most of them earn it: the suites that carry
one name a genuine process-global cause, and `safe_visual_tests` names three while explicitly rejecting the
obvious suspect. Two places do not. `safe_project_tests` — the primary functional gate, and the suite whose
runtime [Levels and suites](#levels-and-suites) calls one of the two numbers that matter — states only that it
matches the other suites, which is not a cause. And several fixture-level attributes state nothing at all,
including two inside an assembly that is already serial.

**Action.** Decide whether serial execution is deliberate or debt, and record which. If deliberate —
determinism bought with wall-clock — say so, and stop treating runtime as a headline number. If debt, then
opting in is the first move and the attributes only start meaning something afterwards: each one becomes a
recorded isolateability problem carrying its cause, the rule says what a fixture may hold statically, and an
attribute comes off as its cause is paid down. Either way, give `safe_project_tests` a real reason or drop
its attribute, and delete the two that sit inside an assembly that already forbids parallelism.

### A10 — the same behaviour is asserted at two levels, and nothing catches it

**Problem.** [Levels and suites](#levels-and-suites) says a level exists for what the level below genuinely
cannot reach, not as a place to repeat it. [A1](#a1--safe_visual_e2e_tests-is-too-slow-and-most-of-it-is-at-the-wrong-level)
treats that at one boundary only, so the same accretion between `safe_project_tests` and `safe_visual_tests`
has read as compliant. It is not. One test exists verbatim in both suites — same name, same body, same
assertion messages — with a neighbouring comment defending the copy on the grounds that the two live in
different assemblies, which is the reasoning the shared-helper rule exists to refuse. Elsewhere a projection
assertion is repeated through the shell, adding a harness prelude that touches nothing it asserts; two files
share a class name across the two suites; and the largest instance is systematic — a long row of dialog
*parity* fixtures, most of them toolkit-free, re-asserting through a dialog what a command test already
asserts on the written element.

**Action.** Decide what a parity test is *for*. It exists to prove the ROUTE — that the dialog's result
reaches the command and the command reaches the document — so it should assert the route and keep **one**
observable effect at the far end as the evidence that the dialog's own value is what arrived.

That last assertion is not the duplicate, and this is the distinction to hold on to while cutting. An engine
test proves the command writes the value it was handed; only the route test proves the value it was handed came
from the *dialog*. Delete what sits beyond it: the second and third assertions re-deriving an outcome the command
test already owns, the harness prelude that touches nothing the test asserts, and the verbatim copies. Do not
narrow the rule to "assert that the command was reached" — a test that observes only the call has to mock the
application service to see it, which trades duplicated coverage for none and is the arrangement
[Levels and suites](#levels-and-suites) exists to avoid. Then move what is toolkit-free with
[A2](#a2--openvisual-non-gui-tests-sit-in-safe_visual_tests) rather than migrating duplication into a new home.

### A11 — helpers are copied instead of moved to `tests/shared/`

**Problem.** The rule is in [Techniques that cross suites](#techniques-that-cross-suites), and its own
example is where it fails: the screenshot mechanism is half shared and half copied, with the capture
attribute and its Avalonia base declared separately in two suites while the machinery they drive is properly
`<Compile Include>`d by both. A CsCheck operation model is declared four times — three of them inside one
suite. And `ShellHarness` is the largest instance, described under A2.

**Action.** Move them, then let the drift they have already accumulated show up in the diff. A copy is worth
looking at before it is deleted: where two copies differ, one of them is telling the truth about a suite that
needed something the other did not.

Not every copy on that list resolves to a move, and the operation models are the case to check rather than
assume. The screenshot mechanism is unambiguous — two suites want identical behaviour and one of them has a
stale version. The CsCheck models are not: they overlap heavily but each carries operations the other's law has
no use for, so a single shared model becomes the union of every law that touches it, and adding an operation
for one property enlarges the state space of the others. Share the *generators* for the values, which are the
tedious part and genuinely common; leave each law its own operation set unless the sets turn out to be equal.
Three declarations inside one suite are still three too many.

### A12 — a property test carrying one oracle that is a copy of the code it checks

**Problem.** [Techniques that cross suites](#techniques-that-cross-suites) warns that a reimplementation
making the same assumptions as the original is not a second opinion. One property test carries such a
reimplementation: its equality helper is line-for-line the private method the subject uses to decide whether a
value changed. On that one question the law cannot fail — if both sides are wrong about what equality means,
they are wrong together, and no seed will separate them.

**The blast radius is one predicate, not the test.** The law's model also fixes how many events fire, in what
order, with which index, and with which old and new value on each, and then checks the final argument vector
against the model — none of which the copied helper decides. An event raised for the wrong index, in the wrong
order, carrying a stale old value, or a vector left inconsistent with the events, all still fail. So this is
not [a test that cannot fail](#when-a-test-leaves) and deleting it would give up real coverage.

**Action.** Replace the helper, keep the law. An independent comparison is what is missing — a differently
derived one, or a metamorphic relation over the same operation. If no independent oracle is worth the effort,
the honest alternative is to narrow the law to what it genuinely establishes and say so in the summary, so the
next reader does not count the change-detection half as verified.

### A15 — controller-free tests sit in the suite CI never runs

**Problem.** `safe_integration_tests` is for what needs a real controller, and its row excludes anything
verifiable controller-free. Two tests there contact no controller — their own summary says constructing the
service is the whole arrange — and they exercise a shared contract helper that the other suites already use.
Parked there, they are the only assertions in the repository that no CI leg ever evaluates.

**Action.** Move them to `safe_unit_tests`, which is where a contract shared by several suites belongs.

## See also

- [ARCHITECTURE.md](ARCHITECTURE.md) — layers, invariants, and the design challenges the tests defend
- [CLAUDE.md](CLAUDE.md) — *Verification*: which suites to run after which change
- [tests/testdata/testdataoverview.md](tests/testdata/testdataoverview.md) — the oracle corpus and its provenance
- [tests/safe_lab_tests/README.md](tests/safe_lab_tests/README.md) — Lab fixtures, fakes and screenshots
- [docs/adr/](docs/adr/) — ADR-001 (threading), ADR-002 (service tiers / thin apps), ADR-004 (bans over architecture tests)
