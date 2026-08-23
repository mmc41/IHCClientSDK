# IHCClientSDK Agent Instructions

Unofficial cross-platform .NET 10 SDK and applications for LK/Schneider Electric IHC controllers and `.vis` project editing.

## Build and Run

Run commands from the repository root.

```bash
# Build
dotnet build IHCClientSDK.sln
dotnet build ihcclient/ihcclient.csproj

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

# Single test
dotnet test <test-project.csproj> --filter "FullyQualifiedName~TestName"
```

- NUnit is the test framework. Controller-free suites need neither a live controller nor controller credentials.
- Run `safe_unit_tests` for SDK and view-model logic, `safe_project_tests` for `.vis` engine/session behavior, and the matching Avalonia suite for Lab or OpenVisual UI construction and interaction.
- Only mock low-level `IIHCApiService` controller services. Exercise application-service business logic through real `IIHCAppService` instances; read the Safe Lab test documentation before changing its fakes.
- Keep default coverage focused on observable product behavior. Add null-guard, expected-exception, or multithreading tests only when the user requests that risk area.

## Verification

- After code changes, build the affected project and run the suite mapped to that layer in Testing.
- After changes that cross SDK/GUI boundaries, also run `safe_architecture_tests`.
- After OpenVisual or Lab UI changes, also run the corresponding headless UI suite.
- After changing shared build/package configuration, public SDK contracts, or shared bootstrap code, run `dotnet build IHCClientSDK.sln` and every controller-free suite listed above.
- For documentation-only changes, verify links, paths, commands, and terminology against the repository; a .NET build is unnecessary.
- Do not run `safe_integration_tests` as a substitute for controller-free verification.

## Project Structure

- `ihcclient/src/vis/` is the controller-free `.vis` engine behind `ProjectAppService`.
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

## Code Style

- Write code, documentation, comments, diagnostics, and internal-tool text in English. Write end-user application UI and refusal text in Danish.
- Use `nameof()` instead of hard-coded parameter names so refactors preserve diagnostic parameter names.
- Keep operations at their existing abstraction boundary; a pass-through method that only calls another class adds no abstraction.

Source example: `utilities/ihc_project_io_extractor/IhcProjectLoader.cs`.

```csharp
default: throw new ArgumentOutOfRangeException(nameof(ioType), ioType, "Unknown iotype");
```

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

### Never Do

- Git is controlled by the user. Keep Git read-only: do not stage, commit, push, stash, switch branches, or otherwise change Git state; ask before any such operation.
- Treat `ihcclient/generatedsrc/` as generated output. Use the shell scripts on macOS/Linux (`wget`) or the PowerShell ports on Windows; generation requires `dotnet-svcutil`.
- Never re-save authentic `.vis`, `.def`, or `.ifb` oracles to make a test pass. Byte-fidelity tests and `.gitattributes` pin them; diagnose the product code instead.
- Never commit credentials or expose `ihcsettings.json` contents. It is ignored by `.gitignore`; use the tracked templates when documentation needs an example.

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
- [ARCHITECTURE.md](ARCHITECTURE.md) -- read before architectural changes; it defines layers, invariants, and enforced boundaries.
- [ihcclient/README.md](ihcclient/README.md) -- read when changing public SDK usage, service coverage, or generated SOAP integration.
- [Architecture decisions](docs/adr/) -- read the relevant ADR before changing UI-thread behavior, service layering, generated SOAP visibility, or compile-time enforcement.
- [OpenVisual product specification](applications/ihc_openvisual/docs/product.md) and [user-story index](applications/ihc_openvisual/docs/stories/INDEX.md) -- read before implementing an OpenVisual feature. These specify WHAT; keep HOW in code/`ARCHITECTURE.md` and WHEN in planning artifacts, except story readiness/status annotations.
- [Icon design](applications/ihc_openvisual/docs/icons_design.md) and [icon mapping](applications/ihc_openvisual/docs/icon_codes.md) -- read before creating or changing OpenVisual icons.
- [Test data overview](tests/testdata/testdataoverview.md) -- read before using, adding, capturing, or regenerating oracle fixtures.
- [Safe Lab test documentation](tests/safe_lab_tests/README.md) -- read before changing Lab UI tests, fakes, screenshots, or test setup.
- [Build validation workflow](.github/workflows/build-validation.yml) -- read before changing CI, analyzers, target frameworks, or the test matrix.
