# IHCClientSDK Agent Instructions

Unofficial cross-platform .NET 10 SDK and applications for LK/Schneider Electric IHC controllers and `.vis` project editing.

## Build and Run

Run commands from the repository root.

```bash
# Once per clone: restores dotnet-coverage and reportgenerator, which merge and render the coverage the test suites collect
dotnet tool restore

# Build
dotnet build IHCClientSDK.sln
dotnet build ihcclient/ihcclient.csproj

# Run the static checks a build runs, without building
bash scripts/static_check.sh                       # pwsh -NoProfile -File scripts/static_check.ps1 on Windows

# Skip the static checks a build otherwise runs
dotnet build IHCClientSDK.sln -p:RunStaticChecks=false

# Applications; the second line opens a fixture for hand-driven GUI checks
dotnet run --project applications/ihc_openvisual/ihc_openvisual.csproj
dotnet run --project applications/ihc_openvisual/ihc_openvisual.csproj -- tests/testdata/projects/Project1-SimpelWired.vis
dotnet run --project utilities/ihc_lab/ihc_lab.csproj

# SDK examples
dotnet run --project examples/ihcclient_example1/example1.csproj
dotnet run --project examples/ihcclient_example2/example2.csproj

# Utilities
dotnet run --project utilities/ihc_admin/ihc_admin.csproj
dotnet run --project utilities/ihc_info/ihc_info.csproj
dotnet run --project utilities/ihc_project_io_extractor/ihc_projectextractor.csproj
dotnet run --project utilities/ihc_project_download_upload/ihc_ProjectDownloadUpload.csproj
dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json
# A controller stub; start an app or utility with the --controller-stub=... line it prints first
dotnet run --project ihcclient_stub/ihcclient_stub.csproj
```

To check OpenVisual by hand, launch it with a fixture and drive it through the `aui-openvisual` skill, which is independent of `safe_visual_e2e_tests`. Before running a controller-backed command, follow the controller boundary and configuration guidance. To run an application or utility without a controller, start it under the controller-stub switch as [Running against a controller stub](README.md#running-against-a-controller-stub) describes; the SDK examples ignore that switch.

Every build runs the repository's static checks once, whatever mix of projects is built. `scripts/static_check.sh` and `scripts/static_check.ps1` ARE the list -- the build files name no check, so extend a check by editing both peers and nothing else. Today that means jscpd copy/paste detection ([Dealing with duplicated code](#dealing-with-duplicated-code)). A check whose tool is missing, or that fails to run, only warns, so never read a green build as evidence the checks ran: read the warning, or the report's timestamp. Opt a run out with `-p:RunStaticChecks=false`. The exit-code contract, the scan's scope and the rules an `ignorePattern` entry must pass are in [docs/static-analysis.md](docs/static-analysis.md).

## Testing

```bash
# Controller-free suites
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj
dotnet test tests/safe_stub_tests/safe_stub_tests.csproj
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj
dotnet test tests/safe_project_tests/safe_project_tests.csproj
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj
dotnet test tests/safe_visual_stubbed_tests/safe_visual_stubbed_tests.csproj

# Controller-backed suite; may toggle only the configured test resources
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# Desktop-bound suite; DO NOT run it by default -- see Ask First
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj
# ...and the same scenarios headless, which is what CI runs
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj --filter "TestCategory!=DesktopOnly" -- 'TestRunParameters.Parameter(name="headless",value="true")'

# Single test
dotnet test <test-project.csproj> --filter "FullyQualifiedName~TestName"
```

- NUnit is the test framework. Controller-free suites need neither a live controller nor controller credentials.
- `safe_visual_e2e_tests` is DESKTOP-BOUND and in no default verification. Run it, or a bare `dotnet test` at the repository root, only after asking (Ask First), and say first that it will take over the screen. Prefer the per-project commands above.
- Its `headless=true` mode is what CI gates, and it is a second implementation of the verb vocabulary: read a headless pass as "the scenario paths still work", never as "the application is driveable". The two modes, the `DesktopOnly` category, how the suite behaves off a desktop and why it is a project of its own are in [TESTSTRATEGY.md](TESTSTRATEGY.md#the-end-to-end-bar).
- Run `safe_unit_tests` for SDK logic, including SDK behaviour that merely runs over a stub and the console utilities started under the controller-stub switch; `safe_stub_tests` for the controller stub itself and the SDK's stub seam; `safe_project_tests` for `.vis` engine/session behavior AND for OpenVisual's non-GUI code (view-models, services, stores, presentation mapping); `safe_visual_stubbed_tests` for OpenVisual composed over a controller stub, its composition and its stub indicator; and the matching Avalonia suite for Lab or OpenVisual UI construction and interaction.
- Replace the controller only below the application services: fake a low-level `IIHCApiService` controller service, or run the real SDK over the controller stub ([below](#using-the-controller-stub-in-tests)). Exercise application-service business logic through real `IIHCAppService` instances; read the Safe Lab test documentation before changing Lab's controller stub or its remaining fakes.
- Keep default coverage focused on observable product behavior. Add null-guard, expected-exception, or multithreading tests only when the user requests that risk area.
- Coverage is collected on every `dotnet test` and reports rather than gates, so the bullet above still decides what is worth testing. How the per-suite slices merge, why a repo-wide number is rarely current and which suites do not collect is in [TESTSTRATEGY.md](TESTSTRATEGY.md#coverage).
- Coverage flags: `-p:CollectCoverage=false` opts a run out (an empty `--settings` does not work -- the CLI fails before reading it); `-p:RunCoverageMerge=false` skips only the merge -- pass it on every suite of a sequence but the last, never on a single-suite run; `-p:CoverageHtmlDetail=true` adds the per-file drill-down of uncovered lines. Quote `Summary.txt` in `artifacts/coverage/report/` for a number, never the HTML headline.
- A new or changed validation rule moves two committed oracles, and BOTH are regenerated by their `[Explicit]` test and then diffed -- never hand-edited: `tests/testdata/validation/` and, for a DOCUMENTATION-category rule, the `full-*` report oracles under `tests/testdata/reports/`. Adopting a diff means explaining every changed line by a rule that changed in the same edit; TESTSTRATEGY's *Oracle discipline* has the procedure.
- `[Explicit]` tests DO run when a `--filter` selects them, and some rewrite tracked files in place -- the problem-catalogue regenerators rewrite `ihcclient/docs/problem-catalogue.md`. After a broad filtered run, check `git diff --numstat`.

### Using the controller stub in tests

`ihcclient_stub` answers the SOAP calls the SDK sends, so a test runs the whole SDK -- wrappers, serializer, envelope guards, cookie session and telemetry -- with only the controller replaced. It plugs into the SDK's `Ihc.Stubbing` seam (`ihcclient/src/stubbing/`). [Its README](ihcclient_stub/README.md) holds the API with a worked example and the rules below in full, [the design page](ihcclient_stub/docs/stubdesign.md) shows which suite reaches the stub by which path and defines the safeguard tags (`L1`-`L12`, `O1`-`O8`), and [ADR-005](docs/adr/ADR-005-controller-stub-seam.md) records the decisions.

Correct use:

- The test owns the registration `ControllerStubs.Register(stub)` returns, and disposes it AFTER the services built on its `Endpoint`. Let `Register` generate that endpoint: a `mock://` key is single-use per process, so only a fixture that holds its stub for the whole run, like Lab's, names one. In `safe_stub_tests`, `StubbedSdk.Start()` builds every service on one stub, and disposing it ends the session, releases the registration and checks the stub.
- Arrange state, failures and malformed answers in the test that needs them -- through the stub's scenario properties, `FailOn` and `OverrideRaw` -- never by changing the default controller every test shares. `FailOn` and `Journal.Calls` take the SDK's interface and method; `OverrideRaw` takes the wire names a journal entry carries (`SMSModemService`, `getIHCProject`). All three refuse a name no exchange could match, and the refusal names the one meant.
- Call `stub.VerifyNoFaults()` at teardown. An unmodelled operation or a stub defect reaches the code under test only as an HTTP 500 or `CONTROLLER_STUB_FAULTED`, which that code may swallow, so the stub's own record is where it shows (L8).
- Talk to the stub through its public surface, never the generated `Ihc.Soap` types, which `safe_stub_tests` and `safe_unit_tests` can compile against; only `safe_stub_tests` sees the stub's internals.
- A launched executable reaches the stub through a `ControllerStubLoopbackHost` the test starts and the switch it passes, so the stub and its journal stay in the test. Start a console utility through `tests/shared/UtilityProcessRunner.cs` and OpenVisual only through `E2E.Launch`: both run a clean copy of the build output whose settings name a `.invalid` endpoint, so an executable that ignored the switch fails instead of reaching a controller (O8), and `LaunchGateTests` fails any second launch site (O6).
- Assert what the test arranged, or the SDK's contract -- never whatever the default controller happens to answer. Fidelity is best effort by decision: a pass against the stub never shows that an operation works on a controller, and a shape default, the answer an operation without a typed one gets, shows only that the response maps. When a test needs a real value, add a typed answer as [the README's Maintenance section](ihcclient_stub/README.md#maintenance) describes; when a controller disproves an answer, fix it and every test that relied on it in the same change. Where a shape is captured the stub follows it: a segmented download is slices of one gzip stream, so a test joins them before decoding, and `SegmentationSize` counts gzip bytes; every empty list goes out as one nil `arrayItem`, integer lists excepted.

Safe use -- a stub-backed test must never reach a controller. The guardrails are independent layers, so never weaken one to get a test green:

- Every test project except `safe_integration_tests` and `safe_architecture_tests` links `tests/shared/ControllerReachGuard.cs`, and every one of those except `safe_stub_tests` also links `ControllerStubLatchFixture.cs` with `ControllerStubLatchSupport.cs`, which hold a fail-every-call registration for the whole run so the latch refuses every real endpoint (L11); `tests/safe_visual_tests/safe_visual_tests.csproj` shows the full set of links. `TestSafetyLinkPinArchitectureTests` fails a project that lacks the guard or the fixture, or links one that declares a namespace, because NUnit applies a setup fixture to the whole assembly only from the global namespace. A new latched suite proves its latch from its OWN namespace, with a probe handed to `ControllerLatchProbe.RequireRefused`.
- Every site that builds real SDK services from settings is admitted in `ControllerReachGuard.Admitted` with the reason that is true of it: a stub endpoint (`StubEndpointReason`), a latch probe on a `.invalid` host (`LatchProbeReason`), or a service on which nothing is called. An unadmitted site stops the suite's run before any test executes, a filtered run included (L9). The key is `Namespace.Type.Member`, and `EveryAdmittedSiteInThisSuiteStillBuildsOne` judges an entry by its type name alone, so an entry whose type another guarded suite also declares fails in that suite: keep an admitted site in a type no other guarded suite declares, and after an admission run every suite that links the guard.
- `safe_stub_tests` is the one guarded suite without the run-wide latch, because its seam tests must see the latch both released and held. Never stage `ihcsettings.json` into it (`CopyIhcSettingsToOutput`), give any non-stub endpoint there a host under `.invalid`, and never run the public `ControllerStubs.ApplySwitch` success path there: it holds its registration until the process exits and would latch every later test. Test the switch through the internal `ControllerStubSwitch.ApplySwitchCore`, which returns the registration to dispose. `safe_visual_stubbed_tests` fails its run when an `ihcsettings.json` sits beside it (L5).
- A test builds no network transport of its own -- no bare `HttpClient`, socket handler, generated WCF client, socket or web client or `WebRequest`, nor a subclass of one; the reach guard stops the run on one (L9). A wire-level test that needs a canned HTTP answer uses `tests/shared/StubTransport.cs`, an explicit transport that `Client.Post` takes before the latch.
- A real endpoint in a test is a host under the reserved `.invalid` domain (`http://latch-probe.invalid`, `http://no-stub-switch.invalid`): never `.local`, which mDNS answers on a LAN, and never a real address.
- The journal (`stub.Journal`) is not a guardrail: it records only the calls that reached the stub, so it cannot see one sent to a controller. Assert interactions on it, never safety, and add no unsafe-call check over it (ADR-005, Safeguards).
- Only the executables that apply the switch obey it: OpenVisual, Lab, `ihc_admin`, `ihc_info` and `ihc_project_download_upload`. The SDK examples ignore `--controller-stub` and run against the controller their settings file names, so running one stays a controller-connected command under Ask First.
- A run under the switch marks its telemetry `ihc.controller.stub=true`; filter on it in `openobserve`, so a stub run's figures and errors are never read as a controller's.

## Verification

- After code changes, build the affected project and run the suite mapped to that layer in Testing.
- HOW MUCH verification a change earns scales with what is at stake in the subject it touches, and the tiers are in [TESTSTRATEGY.md](TESTSTRATEGY.md). A change to `.vis` I/O or to controller I/O sits at the top tier and earns more than one independent kind of check; a passing suite alone is not the bar there.
- After changes that cross SDK/GUI boundaries, also run `safe_architecture_tests`.
- After OpenVisual or Lab UI changes, also run the corresponding headless UI suite. That is `safe_visual_tests`, never `safe_visual_e2e_tests` -- the headless suite is the one that verifies a change; the desktop-bound one is run deliberately, on request. A change to OpenVisual's start-up composition, its title or its handling of the controller-stub switch also runs `safe_visual_stubbed_tests`.
- After changing the controller stub (`ihcclient_stub/`) or the SDK's stub seam (`ihcclient/src/stubbing/`, the routing in `Client.Post`), run `safe_stub_tests` and `safe_architecture_tests`, plus the suite that uses the changed path. A change to the stub's contract updates ADR-005, the design page, the stub README and *Using the controller stub in tests* above in the same change.
- After changing shared build/package configuration, public SDK contracts, or shared bootstrap code, run `dotnet build IHCClientSDK.sln` and every controller-free suite listed above.
- For documentation-only changes, verify links, paths, commands, and terminology against the repository; a .NET build is unnecessary.
- Do not run `safe_integration_tests` as a substitute for controller-free verification.
- After running an application, example or utility, check its telemetry for unexpected errors with the `openobserve` skill. If the server is not reachable, say so and carry on: telemetry status is then unknown, not clean.

### Coverage as a completeness check

- Coverage judges whether a NEW feature's tests are adequate, and only over the files that change touched: once the mapped suite passes, read THOSE files' uncovered lines -- `-p:CoverageHtmlDetail=true` writes the per-file drill-down that shows them.
- The repo-wide percentage and every untouched module are noise for that judgement -- the number moves with which suites happened to run, so a rise or drop there is not evidence about this change.
- An uncovered new line is a QUESTION, not a defect: cover it when it is observable product behavior, and leave it when covering it would only add the null-guard or expected-exception tests Testing declines.
- A feature whose main path or new branches are untested is not complete; when reporting one done, say which of its lines stayed uncovered and why.

## Project Structure

- `ihcclient/src/vis/` is the controller-free `.vis` engine behind `ProjectAppService`.
- `ihcclient/src/stubbing/` is the SDK's controller-stub seam (`Ihc.Stubbing`) and the only stub code that ships; `ihcclient_stub/` is the stub itself, which only test suites reference and a person runs by hand ([Using the controller stub in tests](#using-the-controller-stub-in-tests)).
- `tests/shared/` holds the test helpers more than one suite compiles, linked in by `<Compile Include>` rather than referenced -- the oracle harnesses, the telemetry capture, the screenshot machinery, the controller-safety fixtures. Put a helper here when a second suite needs it; a copy is how the two drift. `utilities/shared/` is the same arrangement for source more than one utility compiles, such as the GET/STORE command line `ihc_admin` and `ihc_project_download_upload` share.
- `shared/ihc_hostbootstrap/` is the start-up wiring every host shares -- the telemetry pipeline and the process-wide exception handlers -- with no UI dependency, so console utilities reference it directly; `shared/ihc_hostbootstrap_avalonia/` adds the Avalonia parts on top of it for the two desktop apps. Each half's README ([neutral](shared/ihc_hostbootstrap/README.md), [Avalonia](shared/ihc_hostbootstrap_avalonia/README.md)) states its scope.
- `shared/ihc_uiautomation_windows/` is the app-agnostic Windows UI Automation toolkit the E2E desktop driver is built on; [its README](shared/ihc_uiautomation_windows/README.md) lists what it holds.
- `tests/testdata/` contains byte-exact vendor and generated oracles consumed through `tests/TestData.props`.

## Architecture Invariants

- Read `ARCHITECTURE.md` before changing service layers, `.vis` editing, OpenVisual, or dependency boundaries.
- Keep controller SOAP artifacts behind the high-level API. Application business logic belongs in the application-service tier -- the `Ihc.App` services and `ProjectAppService`, with `.vis` domain rules in the engine behind it -- not in the SOAP layer or a frontend.
- Construct `AuthenticationService` from `IhcSettings`; construct authenticated services from `IAuthenticationService` so they share its settings and cookie session. `OpenAPIService` supports either form.
- The SDK has no logging or OpenTelemetry dependency. A span comes only from `OperationTelemetry` and an instrument only from a registry (`SdkTelemetryRegistry`, the host's `AppTelemetryRegistry`) -- never a direct `StartActivity`, `Meter` or instrument (`TelemetryCoreArchitectureTests`); host applications choose logging and exporters.
- No stub ships. No project outside `tests/` may reference `ihcclient_stub`, a test project or the FakeItEasy package (`StubReferenceArchitectureTests`), and an executable touches the stub seam only at the sites `StubSurfaceArchitectureTests` lists: its `Main` asking for and applying the switch, and the composition roots and titles that read `IsStubConfigured()` (O7).
- Every controller call leaves through `Client.Post`, where stub routing and the latch sit, so a second door would bypass both. Only the SDK's named transport sites create or send on a network client (`SdkTransportArchitectureTests`); no executable or shared bootstrap opens a connection of its own (`ExecutableOutboundArchitectureTests`), and neither does the stub (`StubOutboundArchitectureTests`).
- A testability input may change behaviour only by redirecting controller traffic to a stub, which is what `--controller-stub` does; `--test` stays observation-only, and neither may relax validation, authentication or persistence against a real controller ([ADR-005](docs/adr/ADR-005-controller-stub-seam.md)).
- Windows is reached only from a project whose name contains `windows` (today `shared/ihc_uiautomation_windows`). No other project may reference a Windows package or call a Windows-only API -- a platform guard around the call changes nothing -- and gets Windows behaviour by referencing such a project. `WindowsQuarantineArchitectureTests` enforces it over every project file in the solution and over the neutral assemblies the architecture suite loads.
- Keep OpenVisual a thin MVVM shell over `ProjectAppService`: obtain commands from `ProjectAppService.Commands`, execute interactive edits through `IProjectDocument`, and retain project elements by `ElementId` because immutable edits replace object instances.
- Keep view-models free of Avalonia types. Views must not drive `ProjectAppService`, `ProjectWorkflow`, or `IProjectDocument` directly.
- For new or moved OpenVisual code, follow the C# 14/.NET 10 idioms documented in `ARCHITECTURE.md`; do not churn existing syntax solely to modernize it.
- A `.vis` problem is a value, never a string. `Problem` carries a `ProblemCode`, the Danish sentence already bound, the declared arguments and an English diagnostic; `ProblemChain` composes an operation with its cause and `ProblemAggregate` a head with its items. Render a message WHOLE -- a presentation path never re-derives or re-words user-facing text.
- Every code has a catalogue entry, and the entry is the truth: `ProblemCatalog` holds each code's category, severity, Danish template, declared argument slots, thresholds and evidence as compiled declarations. Adding a code means adding an entry; a code with nothing behind it fails the completeness gate.
- Code families are dotted, except the validation rows: `.vis` finding ids stay bare kebab-case (`name-empty`), and everything else takes a prefix -- `edit.*`, `io.*`, `import.*`, `bridge.*`, `internal.*` for the SDK, and `app.*` reserved for a host (OpenVisual mints `app.openvisual.*`). A code whose first dotted segment is `app` is host-owned; every other code is the SDK's.
- Validation has two ENGINE faces over ONE rule set -- the whole-project run (`ProjectAppService.Validate`/`ValidateCategorized`) and the field-metadata read a dialog binds to (`ProjectAppService.DescribeField`) -- plus the SESSION command face a menu gate queries (`IProjectDocument.CanApply`), which is a command precondition rather than a registered rule. An entry declares which engine faces it answers to, and both engine faces honour that declaration. Author a rule once, through `RuleBuilder`, and register it in `ProjectRules`; no rule walks the document twice, and shared facts come from the per-run analyses (`Ids`, `Topology`, `Usage`).
- A rule below the engine may not read the catalogue. `Ihc.Vis.Session` and `Ihc.Vis.Io` must never depend on `Ihc.Vis.Validation`, so a refusing site carries its own Danish sentence beside its code -- and a drift test keeps that copy equal to the catalogue's template. The layer rules are enforced in `tests/safe_architecture_tests/ValidationLayerArchitectureTests.cs`.
- Suppression is foreclosed: an id is a filtering and grouping key, never a way to silence a finding. Do not add a rule-level disable or a per-element accepted-store.

## Code Style

- Write code, documentation, comments, diagnostics, and internal-tool text in English. Write end-user application UI and refusal text in Danish.
- Use `nameof()` instead of hard-coded parameter names so refactors preserve diagnostic parameter names.
- Keep operations at their existing abstraction boundary; a pass-through method that only calls another class adds no abstraction.

Source example: `utilities/ihc_project_io_extractor/IhcProjectLoader.cs`.

```csharp
default: throw new ArgumentOutOfRangeException(nameof(ioType), ioType, "Unknown iotype");
```

### Nullability

- Every project is nullable-enabled; RS0041 guards the SDK's public surface, with the generated SOAP layer scoped out from the ROOT `.editorconfig`, which keeps the rule beside the other repo-wide severities rather than inside a folder that is off limits to hand edits.
- Prefer an attribute over `!`: `[NotNull]`, `[return: NotNullIfNotNull(...)]` and `[AllowNull]` bind a contract at every call site, where `!` silences one. Keep `!` for a real but inexpressible invariant, and name it in a comment.
- CA1062 is on for every project except the test suites, so an externally visible member that dereferences a parameter needs `ArgumentNullException.ThrowIfNull` as its first statement. An expression-bodied member has nowhere to put one and must be widened to a block body; where a base initializer runs first, guard inside the helper it calls, which the rule follows. Why it is on there and off in `tests/` is in `.globalconfig` and `tests/.editorconfig`.
- A property fed from the generated `Ihc.Soap.*` layer takes `?`, never `required`: that layer is oblivious, so a null wire value assigned to a non-nullable property compiles SILENTLY. `required` is for what the SDK guarantees, and never for a reflectively built type.
- A `[Required]` DataAnnotation states the OUTBOUND contract, not the inbound mapping truth — `NetworkSettings.IpAddress` carries `[Required]` and is `string?`.

## Dealing with duplicated code

Every build writes the duplicated-code report to `artifacts/jscpd/jscpd-ai.txt` -- one line per clone pair, then a summary. New changes that introduce duplicated code should refactor the duplication (do not touch existing duplicated code unrelated to your change unless explicitly instructed by user).

Guidance for refactoring duplicated code:

- Extract function — when the duplicate is a block of logic.
- Extract module/utility — when the duplicate spans multiple files in different domains
- Extract constant or config — when the duplicate is repeated data or configuration.
- Template/base class — when the duplicate is structural (e.g., repeated class shape).
- Clones across unrelated modules may signal a missing shared utility
- A clone between test files may indicate a missing test helper
- Start with clones that have the highest line count — they have the most impact

When refactoring always ensure:

- All call sites are updated, not just the two reported by jscpd
- Tests still pass after refactoring
- The extracted abstraction has a clear, descriptive name
  
## Development Tools

- Use the `openobserve` skill for the after-run check in Verification, and when diagnosing a reported exception, silent failure, or slowness.
- Use the `aui-openvisual` skill whenever driving, functionally testing, or visually inspecting the live OpenVisual GUI on Windows.

## Boundaries

### Always Do

- Follow the read trigger in Detail References before modifying any listed area.
- Preserve byte fidelity and line endings for test oracles.
- When the user challenges a technical assessment, restate the evidence before changing it; explicit user decisions still take precedence.

### Ask First

- Before any controller-connected command, confirm the intended endpoint and resource scope without exposing credentials. Integration tests may change configured test outputs but must not change controller configuration or unrelated resources.
- Require explicit authorization before enabling `AllowDangerousInternTestCalls` or invoking manufacturing/internal-test operations.
- Require explicit authorization before enabling `LogSensitiveData`; credentials and other sensitive values can then appear in traces.
- Ask before changing any committed oracle bytes; use the documented capture or regeneration procedure rather than editing by hand.
- Ask before running `safe_visual_e2e_tests`, and before a bare `dotnet test` at the repository root, which runs it. It seizes the desktop for minutes and force-kills any running OpenVisual, including one a person is using; the same root command also reaches the controller-backed suite.

### Never Do

- Git is controlled by the user. Keep Git read-only: do not stage, commit, push, stash, switch branches, or otherwise change Git state; ask before any such operation.
- Treat `ihcclient/generatedsrc/` as generated output. Use the shell scripts on macOS/Linux (`wget`) or the PowerShell ports on Windows; generation requires `dotnet-svcutil`.
- Never re-save authentic `.vis`, `.def`, or `.ifb` oracles to make a test pass. Byte-fidelity tests and `.gitattributes` pin them; diagnose the product code instead.
- Never commit credentials or expose `ihcsettings.json` contents. It is ignored by `.gitignore`; use the tracked templates when documentation needs an example.
- Never link or refer from repo files to files outside the repo or to gitignored folders (for example tmp files). Links between gitignored files are the exception.
- Never add comments that refer to plans, have only temporary value or add nothing to reading the source. Comments should generally be about WHY, not HOW or WHEN things are done.

## When Asking Questions

- Ask one question at a time; wait for the reply before the next.
- For a simple clarification, ask a concise question.
- For a subjective tradeoff, architectural choice, or design decision, include:
  - **Purpose** -- why the decision matters.
  - **Options** -- two or three concrete choices with benefits and drawbacks.
  - **Recommendation** -- the preferred option and its reasoning.
- Search the code, configuration, and relevant documentation first. Ask only when the answer is external, subjective, or materially changes scope.

## Detail References

Markdown links are not auto-loaded. Explicitly open the relevant target before working in that area.

- [README.md](README.md) -- read before environment setup, controller configuration, running an executable against the controller stub, or executable-specific prerequisites.
- [TESTSTRATEGY.md](TESTSTRATEGY.md) -- read before ADDING, MOVING or deleting a test: it defines the test levels, what each suite is for, and the routing rules that decide which suite a test belongs in. Read it also before judging a change adequately tested, because it names what is AT STAKE per subject: the risk tiers that decide how much testing a subject earns, with `.vis` I/O and controller I/O in BOTH directions at the top. It is also the full account of coverage collection, the end-to-end suite's two modes and the oracle discipline that Testing above summarizes.
- [ARCHITECTURE.md](ARCHITECTURE.md) -- read before architectural changes; it defines layers, invariants, and enforced boundaries. Challenge 5 is also the record for the validation engine: its faces, its rule bodies, and the dependency direction among the problem, catalogue, session, IO and definition layers that the architecture tests enforce.
- [ihcclient/README.md](ihcclient/README.md) -- read when changing public SDK usage, service coverage, or generated SOAP integration.
- [Architecture decisions](docs/adr/) -- read the relevant ADR before changing UI-thread behavior, moving work off the UI thread, service layering, where validation or other business logic sits relative to a frontend, generated SOAP visibility, compile-time enforcement, or the controller-stub seam and the testability switches. ADR-001 covers threading and concurrency: single-UI-thread ownership, and the contract for background work (the SDK stays synchronous and thread-agnostic; the host offloads). ADR-002 covers both halves of the SDK/app seam: the service tiers and the thin applications above them, validation placement included. ADR-005 covers the controller-stub seam: what ships where, routing and the latch in `Client.Post`, the `--controller-stub` switch and the safeguard layers.
- [Controller stub](ihcclient_stub/README.md) and its [design page](ihcclient_stub/docs/stubdesign.md) -- read before writing a stub-backed test, adding a typed answer, or changing the stub, its loopback host or the SDK's stub seam. The README carries the test rules, the risks and the maintenance steps; the design page shows which suite uses which path and defines the safeguard tags.
- [OpenVisual product specification](applications/ihc_openvisual/docs/product.md) and [user-story index](applications/ihc_openvisual/docs/stories/INDEX.md) -- read before implementing an OpenVisual feature. These specify WHAT; keep HOW in code/`ARCHITECTURE.md` and WHEN in planning artifacts, except story readiness/status annotations.
- [Icon design](applications/ihc_openvisual/docs/icons_design.md) and [icon mapping](applications/ihc_openvisual/docs/icon_codes.md) -- read before creating or changing OpenVisual icons.
- [Test data overview](tests/testdata/testdataoverview.md) -- read before using, adding, capturing, or regenerating oracle fixtures.
- [Safe Lab test documentation](tests/safe_lab_tests/README.md) -- read before changing Lab UI tests, the run's controller stub or the remaining fakes, screenshots, or test setup.
- [Build validation workflow](.github/workflows/build-validation.yml) -- read before changing CI, analyzers, target frameworks, or the test matrix.
- [Static analysis](docs/static-analysis.md) -- read before changing a static check, `.jscpd.json` or its `ignorePattern`, or before judging what a clean build or scan proves.
- [Coverage scope](.runsettings) -- the single place the measured assemblies and the excluded sources are declared. Read it before adding an assembly to the measured set, and note that an assembly nobody lists is simply unmeasured rather than reported as a failure.
- [Problem catalogue](ihcclient/docs/problem-catalogue.md) -- the SDK's master artifact for every `.vis` finding and every coded refusal: the rows, their categories and severities, the evidence behind each, and the deliberate NON-findings. Read it before adding, changing or reclassifying a code; the rendered index at its end is generated from the declarations and compared by a test, so edit the declarations and regenerate rather than editing the table.
- [Problem catalogue authoring requirements](applications/ihc_openvisual/docs/error_catalog.md) -- read before ADDING an item to either catalogue: the data, formats and wiring a fatal error, error, warning, information item or host operation outcome needs, plus the gates and oracles it moves. It covers the SDK catalogue above and OpenVisual's reserved `app.openvisual.*` family, whose truth is `applications/ihc_openvisual/Services/HostProblemCatalog.cs`. It holds no row inventory -- the declarations do.
