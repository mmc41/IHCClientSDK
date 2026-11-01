# safe_lab_tests - IHC Lab Headless GUI Test Project

This is a headless Avalonia UI test project for the IHC Lab application. Lab's real IHC services run over an in-process controller stub (`ihcclient_stub`), so the tests need neither a live IHC controller nor a settings file.

## Test Infrastructure

### The controller stub

Lab carries no fake of its own: `IhcSetup` (`utilities/ihc_lab/App/IhcSetup.cs`) builds the real SDK services for every endpoint. A `mock://` endpoint is a controller-stub endpoint, which the SDK sends only to a `ControllerStub` registered for it through `ControllerStubs.Register`, and refuses when none is.

- **The run's stub.** `Setup.RunBeforeAnyTests()` registers one `ControllerStub` under a named `mock://` key for the whole run and points `Program.config` at it, so every window a test opens reaches it. `RunAfterAllTests()` releases it and fails the run if the stub met a call it could not answer.
- **Per-case stubs.** A test that needs state of its own registers a fresh stub without a key, which gives it a generated endpoint, and disposes the registration when it ends. `LabSmokeTests` does this for every case, and `LabSetupStubTests` shows Lab's own setup over two such stubs.
- **Answers.** The stub answers every operation with plausible values. To change what an operation answers in a test, set the value on the stub's scenario before the call, for example `stub.UserManager.Users` or `stub.Time.Now`. The stub's own documentation starts at [its README](../../ihcclient_stub/README.md).

A few tests still fake Lab's collaborators directly with FakeItEasy at the `IIHCApiService` seam, which the suite references itself.

### Test Setup Process

1. `Setup.RunBeforeAnyTests()` (TestSetup.cs) - Runs once before all tests:
   - Registers the run's controller stub and configures `Program.config` with its endpoint
   - Initializes logger factory with `TestContextLoggerProvider` for test output visibility

2. `TestAppBuilder.BuildAvaloniaApp()` (TestSetup.cs) - Creates Avalonia test application:
   - Configures headless Avalonia with Skia renderer (enables screenshot capture)
   - Sets up logging to forward to NUnit TestContext

3. Test Execution:
   - `[AvaloniaTest]` attribute creates headless UI session
   - `[CaptureScreenshotOnFailure]` captures PNG on test failure
   - Tests create `MainWindow` which instantiates `IhcSetup`
   - `IhcSetup` builds the real services on the configured endpoint, which reaches the run's stub

### Available Test Helpers

**Base Class:** `AvaloniaTestBase`
- `SetupMainWindowAsync()` - Creates, initializes, and shows MainWindow
- `CaptureScreenshotOnFailure()` - Called automatically on test failure

**Logging Suppression:** `SuppressLogging`
- Use `using (new SuppressLogging()) { }` to suppress logs in tests that intentionally trigger errors/warnings

## Running Tests

```bash
# Run all lab tests
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj

# Run specific test class
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj --filter "FullyQualifiedName~ModelTests"

# Run with verbose output
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj --verbosity detailed
```

## Test Categories

### ModelTests (23 tests)
- ViewModel property change notifications
- LabAppService event subscription and handling
- Collection synchronization (Services, Operations)
- Circular update prevention mechanism

### MainWindowTests (8 tests)
- Parameter control setup and lifecycle
- Bidirectional synchronization (GUI ↔ LabAppService)
- Value persistence across operation switches
- Event subscription for parameter strategy controls

### TwoWaySyncTests (8 tests)
- Real-time GUI → LabAppService synchronization
- Real-time LabAppService → GUI synchronization
- Complex parameter reconstruction
- Radio button independence for bool parameters
- Circular update prevention

### LaunchTest (6 tests)
- Basic window lifecycle and initialization
- Service and operation ComboBox population
- Run button functionality
