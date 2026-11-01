# ihc_hostbootstrap

The telemetry pipeline every IHC host builds at start-up: one logger factory, one tracer provider and one meter provider, exported over OTLP. It has no UI dependency, so a console utility gets the same pipeline as the desktop applications without referencing Avalonia.

## Purpose

The SDK emits traces and metrics through .NET's own `ActivitySource` and `Meter` and takes no OpenTelemetry or logging dependency, as [design challenge 9](../../ARCHITECTURE.md#9-observability-without-imposing-a-stack) explains. Each host must therefore build the providers that collect and export those signals. This project builds them once, so every host reports with the same resource, scopes and export policy, and a run of any of them reads the same way in the backend.

OpenVisual and Lab use it through [`ihc_hostbootstrap_avalonia`](../ihc_hostbootstrap_avalonia/AvaloniaTelemetryBootstrap.cs), which adds the Avalonia parts. The console utilities `ihc_info`, `ihc_admin` and `ihc_project_download_upload` reference it directly.

## Scope

- **`TelemetryBootstrap.SetupTelemetryAndLogging`** returns the logger factory, with console and debug logging always on. It adds an OTLP exporter for each of logs, traces and metrics whose endpoint is set, and an empty endpoint switches that signal off.
- **One resource and two scopes for every signal.** The resource carries the service name, namespace and version, one `service.instance.id` per process, `deployment.environment.name`, and `ihc.controller.stub` on a run started with the controller-stub switch. The scopes are the SDK's `ihcclient` and the host's own.
- **One export policy**: OTLP over HTTP/protobuf, delta metric temporality, a shortened metric export interval, trace-based exemplars, second-scale buckets for histograms named `*.duration`, and span attribute limits. The values and the reasons for them are in [`TelemetryBootstrap.cs`](TelemetryBootstrap.cs).
- **`TelemetryBootstrap.Shutdown`** flushes and releases both providers.
- **Handlers for `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`** that log through the pipeline.
- **`TelemetryBootstrap.GetAppVersionStr`** gives the host's version from its entry assembly, and **`ControllerStubConsole.AnnounceIfStub`** prints `CONTROLLER STUB in use (no real controller): <endpoint>` to standard error when a console utility runs against the controller stub.

Out of scope:

- **Anything that needs Avalonia.** The log sink, the `AppBuilder` hook, the dispatcher exception handler and the log-level maps live in `ihc_hostbootstrap_avalonia`.
- **What is measured.** The SDK and each host declare their own spans and instruments, under [invariant 11](../../ARCHITECTURE.md#architectural-invariants).
- **The self-check and the stub switch.** Each host runs `TelemetrySelfCheck` and `ControllerStubs.ApplySwitch` itself. This project only marks a stub run and prints its line.

## Limitations

- **One pipeline per process.** The providers live in static properties. Calling `SetupTelemetryAndLogging` again replaces them, but the logger factory each call returns is the caller's to dispose.
- **Only the two scopes are collected.** Spans and metrics from any other source, such as `HttpClient` or the .NET runtime, are not.
- **The export policy is fixed in code.** The settings file cannot change the protocol, the temporality, the metric interval, the exemplars or the buckets. Standard `OTEL_*` environment variables still apply where the code sets nothing, such as the batch delays and the export timeout.
- **A bad endpoint fails quietly.** A malformed one falls back to the exporter's default and an unreachable one loses what is sent to it, and neither stops start-up. To have it reported, the host must run the SDK's `TelemetrySelfCheck` against a configured `SelfCheckEndpoint`.
- **Only an orderly exit exports everything.** A killed process loses what is still buffered. The unhandled-exception handler logs without flushing, so the record of a crash can be lost with the process.
- **It writes process environment variables.** The span attribute limits are published as `OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT` and `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT` only when unset, so a value an operator already set wins.

## Usage

Reference the project. It brings the SDK with it.

```xml
<ProjectReference Include="..\..\shared\ihc_hostbootstrap\ihc_hostbootstrap.csproj" />
```

The endpoints, headers and environment label come from the `telemetry` section of `ihcsettings.json`. [`ihcsettings_template.json`](../../ihcsettings_template.json) explains each key, and the root README has an [OpenObserve example](../../README.md#opentelemetry-using-openobserve-details). Log levels come from the `Logging` section.

A console host:

```csharp
using Ihc;
using Ihc.Bootstrap;
using Ihc.Stubbing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal static class Program
{
    // The service name, and the name of the host's own ActivitySource and Meter: one name, as in every host.
    private const string Name = "MyUtility";

    private static int Main(string[] args)
    {
        IConfigurationRoot config = IhcConfiguration.FromAppDirectory();
        IhcSettings settings = IhcSettings.GetFromConfiguration(config);
        TelemetryConfiguration telemetry = TelemetryConfiguration.GetFromConfiguration(config);

        ILoggerFactory loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
            Name, "Ihc", Name, telemetry, config.GetSection("Logging"), ControllerStubs.SwitchRequested(args));
        ILogger logger = loggerFactory.CreateLogger(Name);
        AppDomain.CurrentDomain.UnhandledException += TelemetryBootstrap.UnhandledExceptionHandler(logger);
        TaskScheduler.UnobservedTaskException += TelemetryBootstrap.UnobservedTaskExceptionHandler(logger);
        try
        {
            // After the pipeline, so the switch is traced, and before any service is built from the settings.
            (settings, _) = ControllerStubs.ApplySwitch(args, settings);
            ControllerStubConsole.AnnounceIfStub(settings);
            // ... build services from settings and do the work ...
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed: {Message}", ex.Message);
            return 1;
        }
        finally
        {
            TelemetryBootstrap.Shutdown();   // before the logger factory, so a problem in the flush can still be logged
            loggerFactory.Dispose();
        }
    }
}
```

The desktop applications follow the same order in `Main` and add the Avalonia parts from `ihc_hostbootstrap_avalonia`. OpenVisual's [`Program.cs`](../../applications/ihc_openvisual/Program.cs) is the fullest example.

## Maintenance

- **Keep it free of UI and Windows dependencies.** `BootstrapSplitArchitectureTests` fails on any Avalonia dependency and pins which members sit on which side of the split, and `WindowsQuarantineArchitectureTests` fails on a Windows-only API. Anything that needs Avalonia belongs in `ihc_hostbootstrap_avalonia`.
- **State a pipeline rule here, once.** The shutdown order, the resource and the export policy live here rather than in each host, so the hosts cannot drift apart.
- **Name a new duration histogram `*.duration` and record seconds**, so it gets the bucket view instead of OpenTelemetry's unitless default.
- **Package versions are pinned** in [`Directory.Packages.props`](../../Directory.Packages.props). The composition and bucket tests are the ones that catch an OpenTelemetry upgrade changing what the providers build.
- **A rename reaches beyond the project references.** [`.runsettings`](../../.runsettings) measures the assembly by name and leaves one it does not list unmeasured without failing, and `StubSurfaceArchitectureTests` lists it by name in its roster of executables.

This is shared bootstrap code, so verify a change by building `IHCClientSDK.sln` and running every controller-free suite, as [CLAUDE.md](../../CLAUDE.md#verification) requires. These tests pin the project:

| Suite | Tests | Pin |
| --- | --- | --- |
| `safe_unit_tests` | `TelemetryCompositionTests`, `HistogramBucketViewTests` | What the providers are built with |
| `safe_unit_tests` | The `*ProcessTests` fixtures | The controller-stub line |
| `safe_project_tests` | `UnhandledExceptionTests` | The unhandled-exception handler |
| `safe_visual_tests` | `GlobalExceptionHandlerTests` | The unobserved-task handler |
| `safe_architecture_tests` | `BootstrapSplitArchitectureTests`, `WindowsQuarantineArchitectureTests`, `StubSurfaceArchitectureTests` | No Avalonia or Windows-only dependency, and only `AnnounceIfStub` calls the stub surface |

When the scope changes, update the project's entries in [ARCHITECTURE.md](../../ARCHITECTURE.md#codemap) and the header comment in [`ihc_hostbootstrap.csproj`](ihc_hostbootstrap.csproj) in the same change.
