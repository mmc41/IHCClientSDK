# ihc_hostbootstrap_avalonia

The Avalonia half of the shared host bootstrap. It attaches Avalonia to the telemetry pipeline that [`ihc_hostbootstrap`](../ihc_hostbootstrap/TelemetryBootstrap.cs) builds, so a desktop application's framework logs and UI-thread faults reach the same `ILogger` and OpenTelemetry pipeline as the rest of its telemetry.

## Purpose

The bootstrap is split in two so that a console utility can build the telemetry pipeline without referencing a UI toolkit. `ihc_hostbootstrap` holds everything a headless host can use. This project references it and adds only what needs an Avalonia type. An Avalonia application calls both: the neutral half to build its pipeline, and this one to attach the toolkit to it.

`applications/ihc_openvisual` installs both the log sink and the dispatcher handler. `utilities/ihc_lab` installs only the log sink.

## Scope

Everything is in one static class, `AvaloniaTelemetryBootstrap`, in [`AvaloniaTelemetryBootstrap.cs`](AvaloniaTelemetryBootstrap.cs). Both halves share the namespace `Ihc.Bootstrap`.

| What | Members |
| --- | --- |
| Forward Avalonia's internal logs into `ILogger`, ahead of the sink already installed | `LogToSink`, an `AppBuilder` extension, and `ChainedILoggerSink` |
| Convert between Avalonia's `LogEventLevel` and `LogLevel` | `MapFromIlogToAvaloniaLogLevel`, `MapFromAvaloniaLogToILogLevel` |
| A `Dispatcher.UIThread.UnhandledException` handler: log a UI-thread fault, report it and survive it, until the same fault reaches `BreakerLimit` occurrences | `DispatcherExceptionHandler`, `BreakerLimit`, `FaultIdentity`, `IsCancellation`, `LogDispatcherException` |

Out of scope:

- **Everything a headless host can use.** The logger factory, the tracer and meter providers, the OTLP exporters, `Shutdown`, and the `AppDomain` and `TaskScheduler` exception handlers are in `TelemetryBootstrap`.
- **Application policy.** The level, the logger categories and where a reported fault is recorded belong to the host.

## Limitations

- **It builds no pipeline.** It forwards into an `ILoggerFactory` the host already has, normally the one `TelemetryBootstrap.SetupTelemetryAndLogging` returns.
- **`LogToSink` replaces Avalonia's process-global `Logger.Sink`** and chains the sink it replaced. Call it once per process: a second call adds a second forwarder, and every Avalonia log is then forwarded twice. A test that installs one restores the previous sink afterwards, as `AvaloniaLogLevelTests` does.
- **`LogLevel:Avalonia` does not filter the forwarding sink.** The sink logs under its own category, not an `Avalonia.*` one, so pass the floor as `minLevel`. The default, `Verbose`, sets no floor of its own: everything the `ILogger` allows is forwarded, which at Information includes every layout pass Avalonia reports.
- **The dispatcher handler resumes the UI thread after a fault.** That is a trade, not a safety claim, and [ADR-001](../../docs/adr/ADR-001-threading-and-concurrency-model.md#background-processing) records it. The breaker counts each fault identity, the exception type plus the top stack frame, for as long as the handler lives, and leaves the `BreakerLimit`-th and later occurrences unhandled, so Avalonia escalates them and the application ends. A cancellation is marked handled and is not logged.

## Usage

Reference the project from an Avalonia application:

```xml
<ProjectReference Include="..\..\shared\ihc_hostbootstrap_avalonia\ihc_hostbootstrap_avalonia.csproj" />
```

Build the pipeline in `Main` with `TelemetryBootstrap.SetupTelemetryAndLogging` and keep the `ILoggerFactory` it returns. Then attach Avalonia where the `AppBuilder` is built:

```csharp
using Avalonia;
using Avalonia.Logging;
using Avalonia.Threading;
using Ihc.Bootstrap;
using Microsoft.Extensions.Logging;

public static AppBuilder BuildAvaloniaApp()
{
    // loggerFactory is the field Main assigned from TelemetryBootstrap.SetupTelemetryAndLogging.
    LogEventLevel level = AvaloniaTelemetryBootstrap.MapFromIlogToAvaloniaLogLevel(LogLevel.Warning);
    return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace(level)                // first, so the forwarding sink chains to it
        .LogToSink(loggerFactory, level)  // pass the floor; LogLevel:Avalonia cannot reach this sink
        .AfterSetup(_ =>                  // after setup: reading Dispatcher.UIThread earlier creates it too early
            Dispatcher.UIThread.UnhandledException += AvaloniaTelemetryBootstrap.DispatcherExceptionHandler(
                loggerFactory.CreateLogger("MyApp.DispatcherException"),
                report: fault => { /* a durable, user-visible record; omit it when the host has none */ }));
}
```

`LogLevel.Warning` stands in for the host's configured level; OpenVisual reads it from `LogLevel:Avalonia` in `WithAvaloniaLogging` in [`Program.cs`](../../applications/ihc_openvisual/Program.cs). The process-wide exception handlers, and the rule to call `TelemetryBootstrap.Shutdown` before the logger factory is disposed, belong to the neutral half.

## Maintenance

- **Keep the split.** Code a headless host can use goes in `ihc_hostbootstrap`, and only code that needs an Avalonia type comes here. [`BootstrapSplitArchitectureTests`](../../tests/safe_architecture_tests/BootstrapSplitArchitectureTests.cs) asserts that the neutral half has no Avalonia dependency and pins which side the main members are on.
- **[ADR-001](../../docs/adr/ADR-001-threading-and-concurrency-model.md#background-processing) is where the dispatcher handler's trade is argued.** A change to when it sets `Handled`, to the breaker or to `BreakerLimit` changes that trade, so amend ADR-001 in the same change.
- **Do not dispose the `Activity` chain in the dispatcher handler.** `TelemetryBootstrap.UnhandledExceptionHandler` owns the terminal path, and doing it in both disposes the chain twice.
- **Package versions come from [`Directory.Packages.props`](../../Directory.Packages.props).** A `PackageReference` here names a package, never a version.
- **A rename reaches beyond the project references.** [`.runsettings`](../../.runsettings) measures the assembly by name and leaves one it does not list unmeasured without failing, and `ExecutableOutboundArchitectureTests` loads it by name.

Verify a change with `dotnet build IHCClientSDK.sln` and [every controller-free suite](../../TESTSTRATEGY.md#running-the-suites), as for any change to shared bootstrap code. These suites exercise this project:

| Command | Verifies |
| --- | --- |
| `dotnet test tests/safe_visual_tests/safe_visual_tests.csproj` | The dispatcher handler and its breaker (`GlobalExceptionHandlerTests`), the handler as OpenVisual's net (`ExportCommandContainmentTests`), and the sink's floor (`AvaloniaLogLevelTests`) |
| `dotnet test tests/safe_lab_tests/safe_lab_tests.csproj` | Lab's headless host, which installs the sink, so every Lab test runs through it |
| `dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj` | The split, and the repository-wide rules that load this assembly: the Windows quarantine, no network connection of its own, and no coverage opt-out on a member that catches |

When the scope or the split changes, update this project's entries in [ARCHITECTURE.md](../../ARCHITECTURE.md#codemap) and [TESTSTRATEGY.md](../../TESTSTRATEGY.md#levels-and-suites), and the header comment in [`ihc_hostbootstrap_avalonia.csproj`](ihc_hostbootstrap_avalonia.csproj), in the same change.
