# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building
```bash
# Build entire solution
dotnet build main.sln

# Build specific project
dotnet build ihcclient/ihcclient.csproj
dotnet build ihcclient_tests/ihcclient_tests.csproj
```

### Testing
```bash
# Run all tests
dotnet test ihcclient_tests/ihcclient_tests.csproj

# Run tests with detailed output
dotnet test ihcclient_tests/ihcclient_tests.csproj --verbosity detailed
```

### Running Examples
```bash
# Run example programs (requires ihcsettings.json configuration)
dotnet run --project ihcclient_example1/example.csproj
dotnet run --project ihcclient_example2/example.csproj

# Run IHC project IO extractor utility
dotnet run --project ihcproject_io_extractor/ihcprojectextractor.csproj
```

## Project Architecture

### Core Structure
This is a .NET 9.0 mono-repository containing an unofficial SDK for IHC (Intelligent House Concept) controllers from LK/Schneider Electric.

**Main Projects:**
- `ihcclient/` - Core SDK library with high-level API wrapper around SOAP services
- `ihcclient_tests/` - NUnit test suite for system and unit tests
- `ihcclient_example1/` & `ihcclient_example2/` - Console application examples
- `ihcproject_io_extractor/` - Utility to generate constants from IHC project files

### SDK Architecture
The `ihcclient` project follows a layered architecture:

**High-Level Services** (`src/services/`):
- Service classes like `AuthenticationService`, `ResourceInteractionService`, `ConfigurationService` etc.
- Each service wraps a corresponding SOAP implementation
- Uses custom data models in `src/models/` instead of exposing SOAP artifacts
- Fully async API design with no SOAP inheritance

**Generated SOAP Layer** (`generatedsrc/`):
- Auto-generated from WSDL files using dotnet-svcutil
- Low-level SOAP implementations in `Ihc.Soap.*.*` namespaces
- Should not be used directly - access through high-level services

**Supporting Infrastructure** (`src/util/`):
- HTTP client utilities, serialization helpers, date extensions
- Cookie handling for maintaining IHC controller sessions

### Key Design Patterns
- All service classes require logger and endpoint in constructor
- Authentication required before using most services (except OpenAPIService)
- Async/await throughout with async enumerables for long polling operations
- Custom serialization layer to handle IHC-specific data formats

## Configuration Requirements

### ihcsettings.json
All tests, examples, and utilities require an `ihcsettings.json` file (not tracked in git). Use `ihcsettings_template.json` as reference.

Required for development:
- IHC controller endpoint and credentials
- Test resource IDs for boolean inputs/outputs
- Logging configuration

### IHC Controller Setup
Before running any code that connects to an IHC controller:
1. Enable network access in IHC administrator interface
2. Enable "thirdparty access" 
3. Use appropriate application name: "treeview", "openapi", or "administrator"

## Important Notes

- Target framework: .NET 9.0
- Test framework: NUnit 3.x
- The project wraps SOAP web services since .NET Core doesn't natively support SOAP
- `AuthenticationService` and `ResourceInteractionService` are feature-complete
- Other services are partially implemented but can be extended via the underlying SoapImpl classes
- Version 3.0+ controllers have additional OpenAPIService (quality uncertain)
- WSDL regeneration requires macOS with wget and dotnet-svcutil tools