# IHCClientSDK Agent Instructions

Unofficial cross-platform .NET 10 SDK and applications for LK/Schneider Electric IHC controllers and `.vis` project editing.

## Build and Run

Run commands from the repository root.

```bash
# Once per clone: restores dotnet-coverage, which merges the coverage the test suites collect
dotnet tool restore

# Build
dotnet build IHCClientSDK.sln
dotnet build ihcclient/ihcclient.csproj

# Run the static checks a build runs, without building
bash scripts/static_check.sh                       # pwsh -NoProfile -File scripts/static_check.ps1 on Windows

# Skip the static checks a build otherwise runs
dotnet build IHCClientSDK.sln -p:RunStaticChecks=false

# Applications and GUI E2E entry points
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
dotnet run --project utilities/ihc_httpproxyrecorder/ihc_httpproxyrecorder.csproj
dotnet run --project utilities/ihc_project_download_upload/ihc_ProjectDownloadUpload.csproj
dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json
```

For OpenVisual E2E, launch it with a fixture and drive it through `aui-openvisual`. After any application, example, or utility run, use `openobserve` to inspect telemetry. Before running a controller-backed command, follow the controller boundary and configuration guidance.

Every build runs the repository's static checks once, whatever mix of projects is built. `scripts/static_check.sh` and `scripts/static_check.ps1` ARE the list -- the build files name no check, so extend a check by editing both peers and nothing else. A check whose tool is missing, or that fails to run, warns and leaves the verdict to the compiler, so never read a green build as evidence the checks ran: read the warning, or the report's timestamp. A peer exits non-zero only when a check could not RUN, never because a check found something. Opt a run out with `-p:RunStaticChecks=false`.

Today that means jscpd copy/paste detection, writing `artifacts/jscpd/jscpd-ai.txt` -- one line per clone pair, then a summary. It is `.txt` because the `ai` reporter emits plain text, not markdown, whatever its name suggests. The file is written only when the scan succeeds, so a report on disk is always a report some run produced. The whole scan is declared in `.jscpd.json`: authored C# only, with the generated SOAP layer, `*.g.cs`/`*.Designer.cs` and the `tests/testdata/` oracle corpus excluded as unauthored or byte-pinned, and the reporter and output directory alongside them. `ProblemCatalogEntries.*.cs` is excluded on a third ground: those files are pure declaration tables, where a "clone" is only ever two catalogue entries passing the same constructor arguments positionally, which is the shape the data has and not something to refactor. What keeps that exclusion honest is that the files carry no control flow at all -- no `if`, `for`, `switch`, `return` or local; the day logic lands in one, it stops being a declaration table and the exclusion stops being safe. `ihcclient/src/vis/catalog/definitions/**` is excluded on a near-identical ground and needs its own entry because those files carry no `.g.cs` suffix any more: a clone there is two products or two grammar declarations passing the same builder-call chain, which is the transcribed catalogue data, and what each file evaluates to is pinned by `BuiltInCatalogDigestTests` rather than kept honest by refactoring. Naming the folder also removes a half-truth the file-size limit would otherwise leave in the report: jscpd skips a file over 1 MB silently, and one of the three is well past that, so a scanned folder would report on two of them while reading as all three. Do not add `"gitignore": true`: jscpd already respects `.gitignore` by default, so it reads as scope the config does not actually control. That file is jscpd's own config format, so `jscpd .` by hand from the repository root is the same scan; its `output` key is why a hand-run with a file reporter lands under `artifacts/jscpd/` instead of creating `report/` in the repository root.

`ignorePattern` says which text is not evidence of copy/paste -- `using` directives, and the fixture plumbing every headless UI test opens with. jscpd blanks each match before matching but keeps the block's line numbers, and the surviving tokens still have to reach the threshold alone, so the effect is "fifty tokens of something other than boilerplate", not a lower bar. The test a pattern has to pass: the text it matches must be the SAME text everywhere it matches, so blanking it erases no distinction between two blocks. That is why each entry spells out a whole statement and pins it between `^[ \t]*` and `\r?$` rather than wrapping a method name in `^.*` and `.*$` -- the wrapping form also swallows whatever else shares the line, and a line is shared by the receiver and the variable the call is assigned to, by a comment that merely mentions the method, and by an `Assert.That(...)` whose subject the call happens to be. Blanked that way, `CurrentTestWindow = null;` and `CurrentTestWindow = window;` become the same line, which is the invented match. Anchor `using` at column zero, because the indented `using var x = ...;` statement is real code; use `[ \t]*` and not `\s*` for indentation, because under `(?m)` a `\s*` also eats newlines and runs the match back through the preceding blank lines; and write a line end `\r?$`, because the working tree is CRLF on Windows and LF elsewhere. Naming the variable in the pattern (`harness`, `vm`, `window`) is deliberate: it is what makes the match a fixed string instead of a hole a real name could fall into.

Raising `minTokens`/`minLines` instead does not work here and has been measured -- the boilerplate and the smallest real findings share one size band, so a threshold that removes much of the first removes most of the second; `mode` is no lever either, the default `mild` already reports the fewest. `baseline` and `failOnNewClones` are valid keys, but never set `failOnNewClones`: exiting non-zero on a finding inverts the exit-code contract above, and the report gets deleted rather than written.

## Testing

```bash
# Controller-free suites
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj
dotnet test tests/safe_project_tests/safe_project_tests.csproj
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj

# Controller-backed suite; may toggle only the configured test resources
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# Desktop-bound suite; DO NOT run it by default -- see the bullets below
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj
# ...and the same scenarios headless, which is what CI runs
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj --filter "TestCategory!=DesktopOnly" -- TestRunParameters.Parameter(name="headless",value="true")

# Single test
dotnet test <test-project.csproj> --filter "FullyQualifiedName~TestName"
```

- NUnit is the test framework. Controller-free suites need neither a live controller nor controller credentials.
- `safe_visual_e2e_tests` is DESKTOP-BOUND and is not part of any default verification. It launches the real `ihc_openvisual.exe`, drives it through Windows UI Automation, holds the foreground for minutes, and force-kills any OpenVisual already running -- including one a person is using. Run it only when asked to, and say first that it will take over the screen. It skips itself off Windows; on Windows with no session it fails, deliberately, because a suite that ignored its way past a broken application would be worse than no suite.
- `safe_visual_e2e_tests` runs in two modes. The DEFAULT drives the real application and is the desktop-bound one described above. `headless=true` swaps in an in-process driver over the same window, which is what CI gates -- but it is a second implementation of the verb vocabulary, so it exercises neither `aui.ps1` nor the Avalonia-to-UIA bridge. Read a headless pass as "the scenario paths still work", never as "the application is driveable". Scenarios only the real desktop can run carry `[Category(E2E.DesktopOnly)]` and are excluded by filter; the headless driver refuses those verbs rather than approximating them.
- It is a project of its own so that the DESKTOP mode is never reached by accident: every suite is run by project path, and Verification names this one nowhere. CI does name it, but only for the headless leg and only with `TestCategory!=DesktopOnly`, so nothing on a push takes a screen. What reaches the desktop mode is an explicit run of the command above, or a bare `dotnet test` at the repository root, which runs every project in the solution -- the controller-backed suite included. Prefer the per-project commands above.
- Run `safe_unit_tests` for SDK and view-model logic, `safe_project_tests` for `.vis` engine/session behavior, and the matching Avalonia suite for Lab or OpenVisual UI construction and interaction.
- Only mock low-level `IIHCApiService` controller services. Exercise application-service business logic through real `IIHCAppService` instances; read the Safe Lab test documentation before changing its fakes.
- Keep default coverage focused on observable product behavior. Add null-guard, expected-exception, or multithreading tests only when the user requests that risk area.
- Code coverage is measured on every `dotnet test` with no extra flag, and it reports rather than gates -- no percentage can fail a build, so the bullet above still decides what is worth testing. Each suite refreshes only its own slice under `artifacts/coverage/raw/<suite>/`; every run re-merges whatever slices are present into `artifacts/coverage/report/` and prints one line. A repo-wide number is therefore only current once every controller-free suite listed above has run, and `Summary.txt` names any slice older than the build it was merged with. `safe_visual_e2e_tests` contributes nothing and is not missing from the number: it opts out of collection entirely, because the only leg CI runs there is filtered, so its slice would describe a subset while reading as a statement about the whole suite.
- Opt a run out with `-p:CollectCoverage=false`. Passing an empty `--settings` does not work -- the `dotnet test` CLI fails with "The path is empty" before the settings are read.
- `artifacts/coverage/report/html/` holds a browsable one-page version of the same report. Add `-p:CoverageHtmlDetail=true` for the per-file drill-down that shows which lines are uncovered; it is opt-in because it writes hundreds of files. Quote `Summary.txt` for a number -- the HTML headline is computed by a different tool and differs from it slightly.
- A new or changed validation rule moves two committed oracles, and BOTH are regenerated by their `[Explicit]` test and then diffed -- never hand-edited: `tests/testdata/validation/` (one XML file per corpus case, holding every finding that case produces in production order) and, for a DOCUMENTATION-category rule, the `full-*` report oracles under `tests/testdata/reports/`, because the Fuld report renders that category as its appendix. Adopting a diff means explaining every changed line by a rule that changed in the same edit.

## Verification

- After code changes, build the affected project and run the suite mapped to that layer in Testing.
- After changes that cross SDK/GUI boundaries, also run `safe_architecture_tests`.
- After OpenVisual or Lab UI changes, also run the corresponding headless UI suite. That is `safe_visual_tests`, never `safe_visual_e2e_tests` -- the headless suite is the one that verifies a change; the desktop-bound one is run deliberately, on request.
- After changing shared build/package configuration, public SDK contracts, or shared bootstrap code, run `dotnet build IHCClientSDK.sln` and every controller-free suite listed above.
- For documentation-only changes, verify links, paths, commands, and terminology against the repository; a .NET build is unnecessary.
- Do not run `safe_integration_tests` as a substitute for controller-free verification.

## Project Structure

- `ihcclient/src/vis/` is the controller-free `.vis` engine behind `ProjectAppService`.
- `tests/shared/` holds the test helpers more than one suite compiles, linked in by `<Compile Include>` rather than referenced -- the oracle harnesses, the telemetry capture, the screenshot machinery. Put a helper here when a second suite needs it; a copy is how the two drift.
- `shared/ihc_appbootstrap/` contains application bootstrap infrastructure shared by the Avalonia apps.
- `tests/testdata/` contains byte-exact vendor and generated oracles consumed through `tests/TestData.props`.

## Architecture Invariants

- Read `ARCHITECTURE.md` before changing service layers, `.vis` editing, OpenVisual, or dependency boundaries.
- Keep controller SOAP artifacts behind the high-level API. Application business logic belongs in `Ihc.App`, not in the SOAP layer or a frontend.
- Construct `AuthenticationService` from `IhcSettings`; construct authenticated services from `IAuthenticationService` so they share its settings and cookie session. `OpenAPIService` supports either form.
- The SDK has no logging dependency. Emit SDK observability through its `ActivitySource`; host applications choose logging and exporters.
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

- Every project is nullable-enabled; RS0041 guards the SDK's public surface, with the generated SOAP layer scoped out from the ROOT `.editorconfig` — a rule written inside it would be lost on regeneration.
- Prefer an attribute over `!`: `[NotNull]`, `[return: NotNullIfNotNull(...)]` and `[AllowNull]` bind a contract at every call site, where `!` silences one. Keep `!` for a real but inexpressible invariant, and name it in a comment.
- A property fed from the generated `Ihc.Soap.*` layer takes `?`, never `required`: that layer is oblivious, so a null wire value assigned to a non-nullable property compiles SILENTLY. `required` is for what the SDK guarantees, and never for a reflectively built type.
- A `[Required]` DataAnnotation states the OUTBOUND contract, not the inbound mapping truth — `NetworkSettings.IpAddress` carries `[Required]` and is `string?`.

## Dealing with duplicated code

The dotnet build automatically produce list of duplicated code in artifacts\jscpd\jscpd-ai.txt. New changes that introduce duplicated code should refactor duplications (do not touch existing duplicated code unrelated to your change unless explicitly instructed by user)

Guidence for refactoring duplicated code:

- Extract function — when the duplicate is a block of logic:
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

- Use the `openobserve` skill after running an app or utility, or when diagnosing a reported exception, silent failure, or slowness. A connection failure means telemetry status is unknown, not that no errors exist.
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
- Ask before running `safe_visual_e2e_tests`, and before a bare `dotnet test` at the repository root, which runs it. It seizes the desktop for minutes and force-kills a running OpenVisual; the same root command also reaches the controller-backed suite.

### Never Do

- Git is controlled by the user. Keep Git read-only: do not stage, commit, push, stash, switch branches, or otherwise change Git state; ask before any such operation.
- Treat `ihcclient/generatedsrc/` as generated output. Use the shell scripts on macOS/Linux (`wget`) or the PowerShell ports on Windows; generation requires `dotnet-svcutil`.
- Never re-save authentic `.vis`, `.def`, or `.ifb` oracles to make a test pass. Byte-fidelity tests and `.gitattributes` pin them; diagnose the product code instead.
- Never commit credentials or expose `ihcsettings.json` contents. It is ignored by `.gitignore`; use the tracked templates when documentation needs an example.
- Never link/refer fro repo files to files outside the repo or to folders that are gitignored (for example tmp files). Exception are links between gitignored files.
- Add comments that refer to plans, have only tempoary value or does not add value compared to reading the source. Comments should generally be about WHY, not HOW or WHEN things are done.

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

- [README.md](README.md) -- read before environment setup, controller configuration, or executable-specific prerequisites.
- [ARCHITECTURE.md](ARCHITECTURE.md) -- read before architectural changes; it defines layers, invariants, and enforced boundaries. Challenge 5 is also the record for the validation engine: its faces, its rule bodies, and the dependency direction among the problem, catalogue, session, IO and definition layers that the architecture tests enforce.
- [ihcclient/README.md](ihcclient/README.md) -- read when changing public SDK usage, service coverage, or generated SOAP integration.
- [Architecture decisions](docs/adr/) -- read the relevant ADR before changing UI-thread behavior, moving work off the UI thread, service layering, where validation or other business logic sits relative to a frontend, generated SOAP visibility, or compile-time enforcement. ADR-001 covers threading and concurrency: single-UI-thread ownership, and the contract for background work (the SDK stays synchronous and thread-agnostic; the host offloads). ADR-002 covers both halves of the SDK/app seam: the service tiers and the thin applications above them, validation placement included.
- [OpenVisual product specification](applications/ihc_openvisual/docs/product.md) and [user-story index](applications/ihc_openvisual/docs/stories/INDEX.md) -- read before implementing an OpenVisual feature. These specify WHAT; keep HOW in code/`ARCHITECTURE.md` and WHEN in planning artifacts, except story readiness/status annotations.
- [Icon design](applications/ihc_openvisual/docs/icons_design.md) and [icon mapping](applications/ihc_openvisual/docs/icon_codes.md) -- read before creating or changing OpenVisual icons.
- [Test data overview](tests/testdata/testdataoverview.md) -- read before using, adding, capturing, or regenerating oracle fixtures.
- [Safe Lab test documentation](tests/safe_lab_tests/README.md) -- read before changing Lab UI tests, fakes, screenshots, or test setup.
- [Build validation workflow](.github/workflows/build-validation.yml) -- read before changing CI, analyzers, target frameworks, or the test matrix.
- [Coverage scope](.runsettings) -- the single place the measured assemblies and the excluded sources are declared. Read it before adding an assembly to the measured set, and note that an assembly nobody lists is simply unmeasured rather than reported as a failure.
- [Problem catalogue](ihcclient/docs/problem-catalogue.md) -- the SDK's master artifact for every `.vis` finding and every coded refusal: the rows, their categories and severities, the evidence behind each, and the deliberate NON-findings. Read it before adding, changing or reclassifying a code; the rendered index at its end is generated from the declarations and compared by a test, so edit the declarations and regenerate rather than editing the table.
- [Problem catalogue authoring requirements](applications/ihc_openvisual/docs/error_catalog.md) -- read before ADDING an item to either catalogue: the data, formats and wiring a fatal error, error, warning, information item or host operation outcome needs, plus the gates and oracles it moves. It covers the SDK catalogue above and OpenVisual's reserved `app.openvisual.*` family, whose truth is `applications/ihc_openvisual/Services/HostProblemCatalog.cs`. It holds no row inventory -- the declarations do.
