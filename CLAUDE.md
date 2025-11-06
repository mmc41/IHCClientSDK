# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building
```bash
# Build entire solution (run from repository root)
dotnet build IHCClientSDK.sln

# Build specific project
dotnet build ihcclient/ihcclient.csproj
dotnet build tests/safe_integration_tests/safe_integration_tests.csproj
```

### Testing
```bash
# Run SDK integration tests (safe to run against active controllers)
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# Run IHC Lab GUI tests (headless Avalonia UI tests)
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj

# Run tests with detailed output
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj --verbosity detailed

# Run specific test by name filter
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj --filter "FullyQualifiedName~TestName"
```

### Running Examples
```bash
# Run example programs (requires ihcsettings.json configuration at repo root)
dotnet run --project examples/ihcclient_example1/example1.csproj
dotnet run --project examples/ihcclient_example2/example2.csproj
```

### Running Utilities
```bash
# Run IHC project IO extractor utility
dotnet run --project utilities/ihc_project_io_extractor/ihc_projectextractor.csproj

# Run HTTP proxy recorder for debugging API calls
dotnet run --project utilities/ihc_httpproxyrecorder/ihc_httpproxyrecorder.csproj

# Run project download/upload tool
dotnet run --project utilities/ihc_project_download_upload/ihc_ProjectDownloadUpload.csproj

# Run IHC Lab GUI utility (Avalonia-based desktop application)
dotnet run --project utilities/ihc_lab/ihc_lab.csproj
```

## Project Architecture

### Core Structure
This is a .NET 9.0 mono-repository containing an unofficial SDK for IHC (Intelligent House Concept) controllers from LK/Schneider Electric.

**Main Projects:**
- `ihcclient/` - Core SDK library with high-level API wrapper around SOAP services
- `tests/safe_integration_tests/` - NUnit test suite for SDK integration tests (safe to run against active controllers)
- `tests/safe_lab_tests/` - NUnit test suite for IHC Lab GUI tests (headless Avalonia UI tests with diagnostic features)
- `examples/ihcclient_example1/` & `examples/ihcclient_example2/` - Console application examples
- `utilities/ihc_project_io_extractor/` - Utility to generate C# constants from IHC project files
- `utilities/ihc_httpproxyrecorder/` - HTTP proxy for debugging/investigating IHC API calls
- `utilities/ihc_project_download_upload/` - Tool for downloading/uploading IHC project files
- `utilities/ihc_lab/` - Avalonia-based GUI desktop application for IHC controller interaction and testing

### SDK Architecture
The `ihcclient` project follows a layered architecture:

**High-Level Services** (`ihcclient/src/api/services/`):
- Service classes: `AuthenticationService`, `ResourceInteractionService`, `ConfigurationService`, `ControllerService`, `MessagecontrollogService`, `ModuleService`, `NotificationManagerService`, `TimeManagerService`, `UserManagerService`, `OpenAPIService`, `AirlinkManagementService`, `SmsModemService`, `InternalTestService`
- Each service wraps a corresponding SOAP implementation (SoapImpl classes)
- Uses custom data models in `src/models/` instead of exposing SOAP artifacts
- Fully async API design with no SOAP inheritance
- Services require logger and endpoint in constructor; most require authentication first (except OpenAPIService)
- `SmsModemService` - SMS modem control including settings, status, hardware/firmware info, and reset operations
- `InternalTestService` - LK/Schneider internal testing operations for hardware diagnostics, LED control, board version queries, time/date management, and RS485 communication. Some potentially dangerous operations (BurnIO, TestSdCard, TestIOBoard, RS485 operations, ProductionTestPassed) require `allowDangerousInternTestCalls` setting enabled in IhcSettings. Intended for manufacturing/testing scenarios.

**Application Services** (`ihcclient/src/app/services/`, namespace: `Ihc.App`):
- Higher-level, tech-agnostic backend services intended for GUI or console applications
- Build on top of SDK services to provide specialized functionality for specific application use cases
- All application services inherit from `AppServiceBase` and support auto-authentication
- Service classes:
  - `AdminAppService` - Manages administrator-related data (users, email, SMTP, DNS, network, web access, WLAN settings). Features change tracking that detects and applies only modified settings to minimize API calls. Supports JSON serialization with optional encryption of sensitive data (marked with `[SensitiveData]` attribute). Provides `GetModel()` to retrieve settings, `Store()` to apply changes, and `SaveAsJson()`/`LoadFromJson()` for file operations.
  - `InformationAppService` - Retrieves read-only controller information (system status, versions, uptime, time settings, SD card info, SMS modem info). Provides `GetInformationModel()` for comprehensive system information retrieval.
  - `LabAppService` - Laboratory/testing backend where users can dynamically select and execute individual IHC service operations. Supports runtime service and operation selection for experimentation and testing scenarios.
- Application services can create their own SDK service instances or accept existing instances via constructor injection
- Designed to be framework-agnostic, suitable for WPF, Avalonia, console apps, or web backends

**Generated SOAP Layer** (`ihcclient/generatedsrc/`):
- Auto-generated from WSDL files using dotnet-svcutil (authentication.cs, configuration.cs, controller.cs, resourceinteraction.cs, openapi.cs, airlinkmanagment.cs, etc.)
- Low-level SOAP implementations in `Ihc.Soap.*.*` namespaces
- Should not be used directly - access through high-level services
- Regeneration requires macOS with `download_wsdl.sh` and `generate.sh` scripts located in `ihcclient/`

**Supporting Infrastructure** (`ihcclient/src/util/`):
- HTTP client utilities, serialization helpers, date extensions
- Cookie handling for maintaining IHC controller sessions

**Extensions** (`ihcclient/src/extensions/`):
- Extension methods for various types

### Key Design Patterns
- All service classes require logger and endpoint in constructor
- Authentication required before using most services (except OpenAPIService)
- Async/await throughout with async enumerables for long polling operations (see `ResourceInteractionService.GetResourceValueChanges`)
- Custom serialization layer to handle IHC-specific data formats
- Cookie-based session management for maintaining controller connections
- Each service uses internal SoapImpl wrapper around generated SOAP code
- Prioritise the following patterns in specified order of priority: D.R.Y, KISS, YAGNI, Single return statements, SOLID.
- Use nameof() instead of hardcoded parameter names

## Configuration Requirements

### ihcsettings.json
All tests, examples, and utilities require an `ihcsettings.json` file in the repository root (not tracked in git). Use `ihcsettings_template.json` or `ihcsettings_example.json` as reference.

Required for development:
- IHC controller endpoint and credentials
- Test resource IDs for boolean inputs/outputs
- Logging configuration
- Security settings (see below)

Note: The SDK library itself does NOT require this file - only the test/example/utility projects need it for configuration.

**SDK API Usage:**
When creating service instances programmatically, pass `logSensitiveData` parameter:

```csharp
// Secure default - credentials not logged
var authService = new AuthenticationService(logger, endpoint);

// Debug mode - credentials visible in logs (USE WITH CAUTION)
var authService = new AuthenticationService(logger, endpoint, logSensitiveData: true);
```

This parameter is available on:
- `AuthenticationService` constructor
- `OpenAPIService` constructor

### IHC Controller Setup
Before running any code that connects to an IHC controller:
1. Enable network access in IHC administrator interface
2. Enable "thirdparty access" 

## Important Notes

- Target framework: .NET 9.0 (version 0.5.0-beta4)
- Test framework: NUnit 3.x
- The project wraps SOAP web services since .NET Core doesn't natively support SOAP
- `AuthenticationService` and `ResourceInteractionService` are feature-complete
- Other services are partially implemented but can be extended via the underlying SoapImpl classes
- Version 3.0+ controllers have additional OpenAPIService (not recommended for use - quality uncertain)
- WSDL regeneration requires macOS with wget and dotnet-svcutil tools
- This is an **unofficial** SDK not affiliated with or supported by LK/Schneider Electric
- Project treats warnings as errors (TreatWarningsAsErrors=true)
- Recommended usage: Use Microsoft.Extensions.Hosting (Generic Host or ASP.NET Core) for proper lifecycle management and orderly shutdown
- When refactoring, do not add simple methods that does nothing but calling another method in another class

## Test Infrastructure

### Test Suites
- **safe_integration_tests** - SDK integration tests (safe to run against active controllers)
- **safe_lab_tests** - Headless Avalonia UI tests for IHC Lab application with advanced diagnostic capabilities (using fake sevices instead of active controller)

### safe_lab_tests Diagnostic Features

The `safe_lab_tests` project includes comprehensive diagnostic capabilities to help troubleshoot test failures:

#### Trace Logging
All tests output detailed trace-level logs visible in test results:
- **Application logs**: MainWindow, ViewModel, and app service operations (Trace level)
- **Avalonia UI logs**: Framework internal logs (Warning level by default)

Log levels configured in `tests/safe_lab_tests/Setup.cs`:
```csharp
// Application code logs (line 196)
builder.SetMinimumLevel(LogLevel.Trace);  // Change to Information or Warning to reduce verbosity

// Avalonia framework logs (line 44)
.LogToSink(loggerFactory, LogEventLevel.Warning);  // Controls UI framework log verbosity
```

#### Automatic Screenshot Capture on Failure
Tests automatically capture screenshots when they fail using the `[CaptureScreenshotOnFailure]` attribute:

**Implementation Details:**
- Uses `IWrapSetUpTearDown` NUnit interface to hook into test execution pipeline
- Works alongside `[AvaloniaTest]` attribute (both attributes required on each test method)
- Executes screenshot capture through Avalonia's headless session dispatcher
- Must be applied to **each test method individually** (NUnit framework limitation - cannot be applied at class level)

**Screenshot Location:**
- Saved to: `tests/safe_lab_tests/bin/Debug/{DotNetVersion}/TestFailureScreenshots/`
- Format: `{TestName}_{Timestamp}.png` (e.g., `MyTest_20251106_094227.png`)
- Automatically attached to test results via `TestContext.AddTestAttachment()`
- Requires Skia renderer (already configured in TestAppBuilder)

**Features:**
- Captures exact visual state at failure
- 1024x768 resolution headless rendering
- Timestamped to prevent overwrites
- Only executes on test failure (no overhead for passing tests)
- Fully automatic - no try-finally blocks or manual calls required

### Test Guidelines
- All tests must be safe from potential harmful side effects on IHC controller, including changing state on controller.
- Only safe_integration_tests may use real IHC Api services. All other tests should use mocks of IHC services using FakeItEasy framework.
- When generating tests, only generate valuable tests for functional aspects.
- Prefer blackbox testing over whitebox testing.
- Use best practice test techniques for test cases such as Equivalence Partitioning, Boundary Value Analysis, State Transition Testing.
- Unless specifically instructed otherwise, do not add tests for the following: null checks, expected exceptions, multithreading.

### Configuring Mocked Services for Tests (safe_lab_tests)

The `safe_lab_tests` project uses mocked IHC services configured in `utilities/ihc_lab/App/IhcSetup.cs` via the `IhcFakeSetup` class.

**How Mocking Works:**
- When endpoint starts with `SpecialEndpoints.MockedPrefix`, `IhcSetup` creates mocked services instead of real ones
- Each service has a `Setup*Service` method in `IhcFakeSetup` that configures mock behavior using FakeItEasy
- Tests automatically use mocked services through the normal application flow

**Adding/Modifying Mocked Operations:**

IControllerService and IAuthenticationService has setup mocks for all method. To add more operations that tests can use,
update the corresponding setup method in `IhcFakeSetup`:

```csharp
public static IAuthenticationService SetupAuthenticationService(IhcSettings settings)
{
    var service = A.Fake<IAuthenticationService>();

    // Add mock operation behavior
    A.CallTo(() => service.Login(A<string>._, A<string>._, A<Application>._))
        .ReturnsLazily((string username, string password, Application app) =>
        {
            // Return mock result
            return new IhcUser { Username = username, ... };
        });

    return service;
}
```

**Example: If tests need a specific operation:**
1. Identify which service the operation belongs to (e.g., `IAuthenticationService`)
2. Update `IhcFakeSetup.SetupAuthenticationService()` to configure the operation
3. Use `A.CallTo()` to define the mock behavior
4. Tests will automatically see the operation through `LabAppService`

**IMPORTANT: Mocking Restrictions**

Tests MUST follow these rules when using mocks:

- ✅ **ALLOWED**: Mocking IHC API services (implementing `IIHCApiService` interface)
  - Examples: `IAuthenticationService`, `IControllerService`, `IResourceInteractionService`, etc.
  - These are low-level SDK services that communicate with the IHC controller
  - Use `IhcFakeSetup` methods in `utilities/ihc_lab/App/IhcSetup.cs` to configure mocked API services

- ❌ **FORBIDDEN**: Mocking application services (implementing `IIHCAppService` interface)
  - Examples: `LabAppService`, `AdminAppService`, `InformationAppService`
  - These are high-level business logic services in `ihcclient/src/app/services/`
  - Always use real instances: `new LabAppService(null, null)` instead of `A.Fake<LabAppService>()`
  - Reason: Application services contain business logic that should be tested, not mocked

**Example of correct vs incorrect mocking:**

```csharp
// ✅ CORRECT - Mocking API service
var authService = A.Fake<IAuthenticationService>();
A.CallTo(() => authService.Authenticate()).Returns(Task.FromResult(new IhcUser { ... }));

// ✅ CORRECT - Using real application service
var labAppService = new LabAppService(null, null);

// ❌ INCORRECT - Do not mock application services
var labAppService = A.Fake<LabAppService>(); // WRONG!
```

See `tests/safe_lab_tests/README.md` for detailed test infrastructure documentation.
