# safe_lab_tests - IHC Lab Headless GUI Test Project

This is a headless Avalonia UI test project for the IHC Lab application. Tests use mocked IHC services (FakeItEasy proxies) and do not require a live IHC controller or settings file.

## Test Infrastructure

### Mocked Services Configuration

All IHC API services are mocked using FakeItEasy when the endpoint starts with `SpecialEndpoints.MockedPrefix`. The mock implementations are defined in `utilities/ihc_lab/App/IhcSetup.cs` in the `IhcFakeSetup` class.

**Key Classes:**
- `IhcSetup` - Main setup class that creates either real or mocked services based on endpoint
- `IhcFakeSetup` - Static class containing setup methods for each mocked service

### Configuring Mocked Service Operations

To add or modify operations available in mocked services for testing, update the corresponding `Setup*Service` method in `IhcFakeSetup`:

```csharp
public static IAuthenticationService SetupAuthenticationService(IhcSettings settings)
{
    var service = A.Fake<IAuthenticationService>();

    // Configure mock behavior for operations (note: async methods require Task.FromResult)
    A.CallTo(() => service.Authenticate()).Returns(Task.FromResult(new IhcUser { ... }));
    A.CallTo(() => service.Authenticate(A<string>._, A<string>._, A<Application>._))
        .ReturnsLazily((string username, string password, Application app) =>
            Task.FromResult(new IhcUser { ... }));

    return service;
}
```

**Example: Adding a new operation to AuthenticationService**
```csharp
// For async methods returning Task<T>
A.CallTo(() => service.Logout()).Returns(Task.FromResult(true));

// For async methods returning Task (void)
A.CallTo(() => service.ClearCache()).Returns(Task.CompletedTask);
```

### Currently Mocked Services

**IAuthenticationService** - Fully mocked with user authentication operations
- `Authenticate()` - Returns mock user with credentials from settings
- `Ping()`, `Disconnect()`, `IsAuthenticated()` - Return success values

**IControllerService** - Comprehensively mocked with all 24 operations
- Project operations: `GetProject()`, `StoreProject()`, `GetProjectInfo()`, `IsIHCProjectAvailable()`
- State operations: `GetControllerState()`, `WaitForControllerStateChange()`, `EnterProjectChangeMode()`, `ExitProjectChangeMode()`
- SD card operations: `IsSDCardReady()`, `GetSDCardInfo()`
- Backup operations: `GetBackup()`, `Restore()`
- Segmentation operations: `GetIHCProjectSegment()`, `StoreIHCProjectSegment()`, `GetIHCProjectSegmentationSize()`, `GetIHCProjectNumberOfSegments()`
- S0 energy meter operations: `GetS0MeterValue()`, `ResetS0Values()`, `SetS0Consumption()`, `SetS0FiscalYearStart()`

**Other Services** - Empty fakes (no operations configured)
- `IResourceInteractionService`, `IConfigurationService`, `IOpenAPIService`, `INotificationManagerService`, `IMessageControlLogService`, `IModuleService`, `ITimeManagerService`, `IUserManagerService`, `IAirlinkManagementService`, `ISmsModemService`, `InternalTestService`

### Test Setup Process

1. `Setup.RunBeforeAnyTests()` (TestSetup.cs) - Runs once before all tests:
   - Configures `Program.config` with mocked endpoint (`SpecialEndpoints.MockedPrefix`)
   - Initializes logger factory with `TestContextLoggerProvider` for test output visibility

2. `TestAppBuilder.BuildAvaloniaApp()` (TestSetup.cs) - Creates Avalonia test application:
   - Configures headless Avalonia with Skia renderer (enables screenshot capture)
   - Sets up logging to forward to NUnit TestContext

3. Test Execution:
   - `[AvaloniaTest]` attribute creates headless UI session
   - `[CaptureScreenshotOnFailure]` captures PNG on test failure
   - Tests create `MainWindow` which instantiates `IhcSetup`
   - `IhcSetup` detects mocked endpoint and uses `IhcFakeSetup` to create services

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
- Event subscription for DynField controls

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


