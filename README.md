# IHCClientSDK

**Unofficial .NET SDK for IHC home-automation controllers — control a live controller and edit its project files from C# on Windows, Mac and Linux.**

[![build](https://github.com/mmc41/IHCClientSDK/actions/workflows/build-validation.yml/badge.svg)](https://github.com/mmc41/IHCClientSDK/actions/workflows/build-validation.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE.md)

This project is an **unofficial**, community-provided software development kit for [IHC (Intelligent House Concept)](https://www.lk.dk/professionel/produktoversigt/intelligente-systemer/ihc/) controllers.
This project is **not** affiliated with or endorsed by Schneider Electric or Lauritz Knudsen (LK). The project supply API and clients running Microsoft .NET on Windows, Mac and Linux variants (incl. Raspberry Pi). The project is released as open source. Please supply pull requests with tested changes. New contributors are welcome!

## Why this SDK?

Schneider Electric has not released a public SDK for IHC, and the controllers speak SOAP — a protocol modern .NET no longer supports out of the box. Also Schneider Electric has discontinuted IHC
and official software updates are unlikely. Hence without new alternatives, IHC owners will soon face inoperatable software as well that may break in future Windows versions. This SDK provide
open source alternatives to applications and fills that gap for experienced .NET developers who want to integrate with their own IHC installation.

* **Control a live controller** through a fully async, high-level C# API (authentication, reading/writing IO resources, streaming value changes, users, time, notifications and more) that hides all SOAP details.
* **Read, edit and create IHC Visual project files (`.vis`) offline** — load, validate, modify and save projects with byte-identical round-trips, without an IHC Visual installation.
* **Run anywhere .NET runs** — Windows, Mac, Linux; (USB connection requires ethernet over USB driver only available on Windows)

## Features

* Async high-level services wrapping all IHC controller SOAP APIs: authentication, resource interaction (incl. change streaming via `IAsyncEnumerable`), configuration, controller, users, time, notifications, message logs, modules, SMS modem and more.
* Higher-level, tech-agnostic application services (`Ihc.App`) for administrator settings, controller information, lab experimentation and project editing — ready to sit behind a GUI or console frontend.
* `Ihc.Vis` project-file engine: byte-identical `.vis` round-trips, new-project creation, editing sessions, validation and a built-in component catalog.
* Project download/upload bridge between `.vis` files and a live controller.
* AES-256-GCM encryption of passwords in settings files.
* OpenTelemetry tracing built in (the SDK itself has no logging dependency).
* Cross-platform Avalonia lab GUI, console utilities and runnable examples.

## Important disclaimers

* Please notice that this project is **not** in any way affiliated with- or supported by LK / Schneider Electric. It exists only because Schneider Electric has not yet released a public SDK themselves.
* The project is unofficial, unfinished and may contain serious bugs. Use at your own risk!
* The project is only partially supported. You are welcome to report bugs and feature requests but don't expect quick solutions unless you supply tested pull-requests as well (or offer to pay the author(s) for support).
* The project is intended for experienced .NET developers using C# and/or F#.
* The SDK has been tested against v3.0 controllers using both Mac and Windows (but Linux ought to work too). More testing from users is needed. Feedback is welcome.

## Status

The project is an early preview/beta. Current version: 0.8.1.

NB: Openvisual is alpha-quality - not yet fully tested.

The SDK currently supports v3.0 IHC controllers only. Support for pre-3.0 controllers is possible but requires contributors interested in this. See [this issue](https://github.com/mmc41/IHCClientSDK/issues/1) to discuss this subject and to keep track of future support.

Definitely missing is a ready-to-consume NuGet package for the client. I expect to publish a package if there is interest. For now you will have to build the client yourself.

A cross-platform replacement for the vendor's IHC Visual designer application is incubating in [applications/ihc_openvisual](applications/ihc_openvisual/), built on the SDK's project-edit engine. It is in early development, but part of the solution and its headless smoke tests run in CI (Windows).

See [ihcclient](ihcclient/README.md#status) for more details on IHC API implementation status.

## Getting started

### Prerequisites

* [.NET SDK](https://dotnet.microsoft.com/download) 9.0 or later.
* [Opengrep](https://github.com/opengrep/opengrep) for static security analysis (optional)
* [jscpd](https://github.com/kucherenko/jscpd) for code duplication analysis (optional)
* An LK IHC v3.0 controller (only needed for controller access — the `.vis` project-file engine works entirely offline).
* Network and third-party access enabled on the controller: open the standard **IHC administrator** app from LK, log in, click access control and enable localnet or internet access plus "Open for thirdparty products". Only enable internet access if you have a firewall and know how to use it securely.

### Build and run

There is no NuGet package yet, so clone the repository and reference the `ihcclient` project from your own solution:

```bash
git clone https://github.com/mmc41/IHCClientSDK.git
cd IHCClientSDK
dotnet tool restore
dotnet build IHCClientSDK.sln
```

`dotnet tool restore` installs the local tool that merges the code coverage the test suites collect. Skipping it leaves the tests themselves working — only the merged coverage report is missing.

Before running any tests, tools or examples in this repo, create a private `ihcsettings.json` file in the repository root with information on your IHC installation (see [Configuration](#configuration) below). The file is needed by the tests/tools/examples located here, but NOT in your own projects if you only consume this SDK.

Then try the examples and the controller-free unit tests:

```bash
dotnet run --project examples/ihcclient_example1/example1.csproj
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj
```

#### Running the tests

The controller-free suites need neither a controller nor credentials, and are what a change is verified
against:

```bash
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj            # general SDK + utilities
dotnet test tests/safe_project_tests/safe_project_tests.csproj      # the .vis engine and ProjectAppService
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj              # headless Ihc Lab UI
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj        # headless OpenVisual UI
```

Two suites are deliberately left out of that list. `safe_integration_tests` is the only one allowed to talk to
a real controller and needs `ihcsettings.json`; `safe_visual_e2e_tests` drives the OpenVisual desktop
application, takes over the foreground for minutes and force-kills any OpenVisual already running — CI runs
only its headless mode.

```bash
# Against a real controller. Changes the configured test resources' outputs.
dotnet test tests/safe_integration_tests/safe_integration_tests.csproj

# End-to-end, headless and in-process — what CI runs. Drop the arguments for the real desktop app.
dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj \
  --filter "TestCategory!=DesktopOnly" \
  -- 'TestRunParameters.Parameter(name="headless",value="true")'
```

Avoid a bare `dotnet test` at the repository root: it runs every project in the solution, including both of
those. What each suite is for, which one a new test belongs in, and how far to trust a headless end-to-end
pass are in [TESTSTRATEGY.md](TESTSTRATEGY.md) — along with what is at stake per subject, the risk tiers that
decide how much testing each part of the system earns.

Static checks. The script pins the ruleset to the same commit CI uses, so a local run and the
CI run are the same scan; a bare `opengrep scan` is not, because it falls back to a mutable
ruleset fetched from semgrep.dev. Findings are reported, not fatal — add `--error` to gate.

```bash
bash scripts/opengrep-scan.sh
```

[CodeQL](https://codeql.github.com/) runs the second half of the analysis, in CI only: it builds the
solution and asks dataflow questions of the result, which the pattern matching above cannot answer.
It needs no local tool and nothing to run here — see the Security tab for its findings, and
[.github/workflows/codeql.yml](.github/workflows/codeql.yml) for what it covers.

## Usage

Reading and writing controller IO resources (condensed from [example1](examples/ihcclient_example1/README.md)):

```csharp
using Ihc;
using Microsoft.Extensions.Configuration;

// Reading settings this way decrypts sensitive data if encryption is enabled.
IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("ihcsettings.json").Build();
IhcSettings settings = IhcSettings.GetFromConfiguration(config);

var authService = new AuthenticationService(settings);
var resourceService = new ResourceInteractionService(authService);
try
{
    await authService.Authenticate();

    int inputId = 1234567; // Find your IDs with CTRL+hover in IHC Visual or the IO extractor utility.
    var value = await resourceService.GetRuntimeValue(inputId);
    Console.WriteLine($"Resource {inputId} is {(value.Value.BoolValue.Value ? "ON" : "OFF")}");
}
finally
{
    await authService.Disconnect();
}
```

Editing an IHC Visual project file — no controller connection or IHC Visual installation required:

```csharp
var projectService = new ProjectAppService(settings);        // Ihc.App namespace
var project = await projectService.Load("MyHouse.vis");     // or CreateNew(...) / DownloadFrom()
// Browse the immutable project model, or modify it through a project.Edit() session...
var validation = projectService.Validate(project);
await projectService.Save(project, "MyHouse-updated.vis");  // unchanged content saves byte-identically
```

See the [ihcclient README](ihcclient/README.md) for API details and [ARCHITECTURE.md](ARCHITECTURE.md) for how the pieces fit together.

## Running from the command line

All applications, utilities and examples are launched with `dotnet run --project <csproj>` from the repository root. Each needs an `ihcsettings.json` in the repo root (see [Configuration](#configuration)); the offline `.vis` and settings tools do not need a controller.

| Project | Kind | Command |
| --------- | ------ | --------- |
| IHC OpenVisual (`.vis` editor GUI) | Application | `dotnet run --project applications/ihc_openvisual/ihc_openvisual.csproj` |
| IHC Lab (API explorer GUI) | Utility | `dotnet run --project utilities/ihc_lab/ihc_lab.csproj` |
| IHC admin (settings download/upload) | Utility | `dotnet run --project utilities/ihc_admin/ihc_admin.csproj` |
| IHC info (system information) | Utility | `dotnet run --project utilities/ihc_info/ihc_info.csproj` |
| Program code extractor (IO constants) | Utility | `dotnet run --project utilities/ihc_project_io_extractor/ihc_projectextractor.csproj` |
| HTTP proxy recorder | Utility | `dotnet run --project utilities/ihc_httpproxyrecorder/ihc_httpproxyrecorder.csproj` |
| Project download/upload | Utility | `dotnet run --project utilities/ihc_project_download_upload/ihc_ProjectDownloadUpload.csproj` |
| Settings encrypt/decrypt | Utility | `dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json` |
| Example 1 (read/write IO) | Example | `dotnet run --project examples/ihcclient_example1/example1.csproj` |
| Example 2 | Example | `dotnet run --project examples/ihcclient_example2/example2.csproj` |

## Content

This project is hosted in a mono-repo containing the following sub-projects:

* SDK:
  * [ihcclient](ihcclient/README.md) This is the main project that contains the code for the IHC client API. This is the project you will need to reference in your own solutions.
* Applications:
  * [ihc_openvisual](applications/ihc_openvisual/) contains an incubating cross-platform GUI intended to replicate the vendor's IHC Visual designer on top of the SDK's project-edit engine (early development; part of the solution, with headless smoke tests in CI).
* SDK usage examples:
  * [ihcclient_example1](examples/ihcclient_example1/README.md) contains code for a simple command line client console program in C#. Use this for inspiration on how to get started.
  * [ihcclient_example2](examples/ihcclient_example2/README.md) contains code for a simple command line client console program in C#. Use this for inspiration on how to get started.
* SDK utilities:
  * [Ihc Lab](utilities/ihc_lab/README.md) contains an experimental cross-platform GUI for calling individual API's.
  * [IHC admin](utilities/ihc_admin/) contains a command line utility that downloads/uploads controller administrator settings as a JSON file.
  * [IHC info](utilities/ihc_info/) contains a command line utility that prints IHC system information such as system version, license info, number of users, modules and resources.
  * [Program code extractor](utilities/ihc_project_io_extractor/README.md) contains an optional command line utility for software developers that can generate constant definitions of IO addresses in a concrete IHC installation. Use this approach in your projects if you don't want to lookup and hardcode IO addresses yourself.
  * [IHC Http Proxy recorder](utilities/ihc_httpproxyrecorder/README.md) contains a simple http proxy useful for software (sdk) developers to investigate undocumented IHC controller API's.
  * [IHC Project download/upload](utilities/ihc_project_download_upload/README.md) contains a tool to download/upload project files.
  * [IHC Settings encrypt](utilities/ihc_settings_encrypt/README.md) contains a tool to encrypt/decrypt passwords in ihcsettings.json.
* Tests — every suite is named `safe_*`, and only [safe integration tests](tests/safe_integration_tests/README.md) may reach a controller:
  * [Safe unit tests](tests/safe_unit_tests/README.md) — general SDK unit tests, plus the utilities without a suite of their own.
  * [Safe project tests](tests/safe_project_tests/) — the `.vis` project-file engine and `ProjectAppService`, driven by committed oracle files.
  * [Safe architecture tests](tests/safe_architecture_tests/) — ArchUnitNET rules enforcing the SDK's directional layering and the OpenVisual GUI's thin-shell boundary.
  * [Safe Lab tests](tests/safe_lab_tests/README.md) — headless GUI tests for the Ihc Lab utility.
  * [Safe visual tests](tests/safe_visual_tests/) — headless GUI tests for the ihc_openvisual application.
  * [Safe visual E2E tests](tests/safe_visual_e2e_tests/) — whole-scenario end-to-end tests against the real OpenVisual desktop app, or the same scenarios through an in-process headless driver; CI gates only the headless mode.
  * [Safe integration tests](tests/safe_integration_tests/README.md) — system tests that can be safely run against a controller in use.

For a whole-repo overview of layers, invariants and boundaries, see [ARCHITECTURE.md](ARCHITECTURE.md); for
what each test suite is for, which one a new test belongs in, and how much testing a given subject earns,
[TESTSTRATEGY.md](TESTSTRATEGY.md).

## Configuration

The SDK uses an `ihcsettings.json` file to configure the IHC controller connection, logging/telemetry, application setup and tests. Before using the SDK or any utilities/tests/examples,
take a copy of [ihcsettings_template.json](ihcsettings_template.json) into `ihcsettings.json` in the same directory and fill-in the details of your installation such as endpoint, username, password etc. See also [ihcsettings_example.json](ihcsettings_example.json).

```json
"ihcclient": {
        "endpoint" : "http://192.100.1.10",
        "userName" : "johndoe",
        "password" : "mypassword",
        "application" : "administrator",
        "logSensitiveData": false,
        "asyncContinueOnCapturedContext": false
},
```

Note:

* Endpoint should be the http/https baseurl for the controller. If connecting to controller over usb, use endpoint set to "<http://usb>".
* Username and password should match user setup by controller. Ignored by controller if logging in over usb.
* Application name can be set to 'treeview', 'openapi', 'administrator'.
* Keep logSensitiveData and asyncContinueOnCapturedContext set to false unless you know what you are doing.
* The template contains additional optional settings (telemetry endpoints, test resource IDs, `allowDangerousInternTestCalls`, `ihcVisualInstallDir`) — see the comments inside the template file.

## Secure password in configuration

For better security, you can encrypt the password stored in `ihcsettings.json` instead of keeping it in plaintext. The SDK provides an encryption utility and automatically decrypts passwords at runtime when configured correctly.

### Using the IHC Settings Encrypt Utility

The [IHC Settings Encrypt utility](utilities/ihc_settings_encrypt/README.md) allows you to encrypt and decrypt passwords in your configuration file using AES-256-GCM encryption.

**Quick Start:**

1. **Set your encryption passphrase** (12+ characters, keep it secure) in a system environment variable:

   ```bash
   export IHC_ENCRYPT_PASSPHRASE="your-secure-passphrase-here"
   ```

2. **Encrypt your password**:

   ```bash
   dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json
   ```

3. **Your `ihcsettings.json` will be updated** with the encrypted password and `encryption.isEncrypted` set to `true`:

   ```json
   {
     "encryption": {
       "isEncrypted": true
     },
     "ihcclient": {
       "endpoint": "http://192.168.1.100",
       "userName": "johndoe",
       "password": "AUk7A8St5R3czttCtvdE2un-WCO0g49...",
       "application": "administrator"
     }
   }
   ```

4. **The SDK will automatically decrypt** the password when isEncrypted is set to true when using
   the built-in methods ```IhcSettings.GetFromConfiguration()``` or ```IhcSettings.GetFromFile()``` to read the configuration (all utilities/examples/tests do this).

**Important Notes:**

* Store your `IHC_ENCRYPT_PASSPHRASE` securely in the system environment.
* Never commit the passphrase to version control
* Use different passphrases for different environments (dev/test/production)
* To decrypt back to plaintext: `dotnet run ... -- decrypt ihcsettings.json`

For complete documentation, see the [IHC Settings Encrypt README](utilities/ihc_settings_encrypt/README.md).

## OpenTelemetry as a logging replacement

As a more powerful alternative to log files, the SDK (optionally) supports [OpenTelemetry](https://opentelemetry.io/) to view traces. To enable this change ```telemetry``` settings in the config file. The SDK should work with any OpenTel solutions. Below is listed one example.

Note: while the SDK uses OpenTelemetry instead of logging, some utilities/applications using the SDK may still use logging. Therefore the example/template specification for ```ihcsettings.json``` files retains a logging configuration.

### OpenTelemetry using OpenObserve details

[OpenObserve.ai](https://openobserve.ai) provides a free, self-hosted OpenTelemetry solution that installs as a single executable, downloadable from [OpenObserve.ai](https://openobserve.ai/downloads/). Select "Open source" version, your OS and download. Once installed and run you should be able to access OpenObserve from [http://localhost:5080](http://localhost:5080). From the menu select Datasource, select Traces and note the Authorization key. Then update ```ihcsettings.json``` with the following information but with the placeholder ```<Authorization Key here>``` replaced with your key.

OpenObserve setup:

```json
 "telemetry": {
      "Host": "http://localhost:5080",
      "Traces": "http://localhost:5080/api/default/v1/traces",
      "Logs": "http://localhost:5080/api/default/v1/logs",
      "Headers": "Authorization=Basic <Authorization Key here>, stream-name=Ihc, organization=default"
    },
```

## Developer skills (Claude Code)

The `.claude/skills/` folder contains two [Claude Code](https://claude.ai/code) skills that assist with **development and testing only**. They are not part of the shipped SDK, are not required to build or use the library, and never run in production. When working in this repo with Claude Code they are consulted automatically (or you can invoke them by name); the underlying scripts can also be run by hand as described below.

### `openobserve` — runtime error lookup & diagnosis

Queries this repo's OpenObserve logs and traces to tell you whether a run actually failed and why — after running any app/example/utility, or when investigating a reported bug, exception, silent failure, timeout or slow span. Much of what goes wrong at runtime (controller/SOAP failures, dropped telemetry, unhandled exceptions) is only visible in telemetry, not in the console.

* Cross-platform, Python-standard-library only (no extra packages).
* Requires OpenObserve running and the `telemetry` section configured in `ihcsettings.json` (see [OpenTelemetry using OpenObserve details](#opentelemetry-using-openobserve-details) above).
* In Claude Code, ask it to "check OpenObserve for errors" after a run; it reads the settings and queries the collector, accounting for the short indexing delay.

### `aui-openvisual` — UI automation for the IHC OpenVisual app (Windows only)

Drives the **IHC OpenVisual** desktop app (`applications/ihc_openvisual`) through Windows UI Automation for scripted GUI testing: launching it, navigating the locality/function trees, invoking toolbar/menu/context commands, expanding/collapsing and clicking nodes, reading tooltips, capturing the window, and checking a uniform JSON result. Useful for verifying a GUI change end-to-end in the real app rather than only in the headless test suites.

* **Windows only** — it uses the Windows UI Automation API and errors with `Code=PlatformUnsupported` on macOS/Linux.
* No install required: it uses the built-in `System.Windows.Automation` client via PowerShell (Windows PowerShell 5.1 or PowerShell 7).
* Exposes a stable `domain.verb` command vocabulary with label-path node addressing; every command prints one JSON result and sets an exit code, so multi-step runs are scriptable and diffable.

Run the driver directly (build the app first with `dotnet build applications/ihc_openvisual/ihc_openvisual.csproj`):

```bash
# from the repository root
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 catalog commands        # list the command vocabulary
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 doctor --launch          # launch the app, then readiness check
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 tree select "Localities/Kitchen"
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 node expand "Localities"
pwsh .claude/skills/aui-openvisual/scripts/aui.ps1 capture window
```

`doctor --launch` starts the app and waits for its window to become usable. See `.claude/skills/aui-openvisual/SKILL.md` and its `references/` for the full command list, node addressing, result/exit-code contract, and how to extend the vocabulary.

## FAQ

**Q**: Do I need to configure my IHC before running the examples or using the API from my own code?
**A**: Open the standard IHC administrator app from LK, login, click access control and enable localnet or internet access + "Open for thirdparty products". Only enable internet access if you have a firewall and know how to use it securely.

**Q**: Why do I get an error message 'The configuration file 'ihcsettings.json' was not found and is not optional' when running examples/tests/applications?
**A**: You must create a "ihcsettings.json" file in the root folder. Copy the 'ihcsettings_example.json' file to 'ihcsettings.json' and fill out your information.

**Q**: Is there a NuGet package available?
**A**: Not yet but might happen later if there is demand. For now, clone the repo and reference the `ihcclient` project directly.

**Q**: Can I edit IHC project files without an IHC Visual installation?
**A**: Yes. The SDK's `ProjectAppService` loads, edits, validates and saves `.vis` files using a component catalog built into the SDK, and can also download/upload projects from/to a controller.

**Q**: Can I contribute to this project?
**A**: Yes please. See <https://docs.github.com/en/get-started/quickstart/contributing-to-projects> and <https://github.com/mmc41/IHCClientSDK>

**Q**: How do I get support?
**A**: There is no official support for this SDK but you can post issues on the github site. <https://github.com/mmc41/IHCClientSDK>.

## Contributing

Contributions are welcome! Please supply pull requests with tested changes. If you are new to the fork/PR workflow, see [GitHub's guide to contributing to projects](https://docs.github.com/en/get-started/quickstart/contributing-to-projects). Bug reports and feature requests can be filed as [issues](https://github.com/mmc41/IHCClientSDK/issues).

## Support

There is no official support for this SDK. Questions, bug reports and feature requests are handled on a best-effort basis via [GitHub issues](https://github.com/mmc41/IHCClientSDK/issues) — see the disclaimers above regarding expectations.

## License

[Apache License 2.0](LICENSE.md). This project is not affiliated with, or endorsed by, LK / Schneider Electric.
