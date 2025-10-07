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
# Run all tests (run from repository root)
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

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
```

## Project Architecture

### Core Structure
This is a .NET 9.0 mono-repository containing an unofficial SDK for IHC (Intelligent House Concept) controllers from LK/Schneider Electric.

**Main Projects:**
- `ihcclient/` - Core SDK library with high-level API wrapper around SOAP services
- `tests/safe_integration_tests/` - NUnit test suite for system and unit tests (safe to run against active controllers)
- `examples/ihcclient_example1/` & `examples/ihcclient_example2/` - Console application examples
- `utilities/ihc_project_io_extractor/` - Utility to generate C# constants from IHC project files
- `utilities/ihc_httpproxyrecorder/` - HTTP proxy for debugging/investigating IHC API calls
- `utilities/ihc_project_download_upload/` - Tool for downloading/uploading IHC project files

### SDK Architecture
The `ihcclient` project follows a layered architecture:

**High-Level Services** (`ihcclient/src/services/`):
- Service classes: `AuthenticationService`, `ResourceInteractionService`, `ConfigurationService`, `ControllerService`, `MessagecontrollogService`, `ModuleService`, `NotificationManagerService`, `TimeManagerService`, `UserManagerService`, `OpenAPIService`, `AirlinkManagementService`
- Each service wraps a corresponding SOAP implementation (SoapImpl classes)
- Uses custom data models in `src/models/` instead of exposing SOAP artifacts
- Fully async API design with no SOAP inheritance
- Services require logger and endpoint in constructor; most require authentication first (except OpenAPIService)

**Generated SOAP Layer** (`ihcclient/generatedsrc/`):
- Auto-generated from WSDL files using dotnet-svcutil (authentication.cs, configuration.cs, controller.cs, resourceinteraction.cs, openapi.cs, airlinkmanagment.cs, etc.)
- Low-level SOAP implementations in `Ihc.Soap.*.*` namespaces
- Should not be used directly - access through high-level services
- Regeneration requires macOS with `download_wsdl.sh` and `generate.sh` scripts

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
3. Use appropriate application name: "treeview", "openapi", or "administrator"

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