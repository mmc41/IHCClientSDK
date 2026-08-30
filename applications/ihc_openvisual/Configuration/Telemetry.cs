using System.Diagnostics;
using Ihc.Bootstrap;

namespace ihc_openvisual.Configuration;

/// <summary>
/// The application's own <see cref="ActivitySource"/> and OpenTelemetry service identity. The SDK has a
/// separate source (<see cref="Ihc.Telemetry"/>, name <c>"ihcclient"</c>); both are registered on the
/// TracerProvider so app and SDK spans share one trace.
/// </summary>
public static class Telemetry
{
    public const string AppServiceName = "IhcOpenVisual";
    public const string AppServiceNamespace = "Ihc";
    public const string ActivitySourceName = "IhcOpenVisual";

    public static ActivitySource ActivitySource { get; } =
        new ActivitySource(name: ActivitySourceName, version: TelemetryBootstrap.GetAppVersionStr());
}
