using ihc_openvisual.Configuration;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The host half of the registry drift check. The SDK half is <c>SdkTelemetryRegistryTests</c> in
/// <c>safe_unit_tests</c>: each half lives with the registry it is about, rather than being centralised
/// where neither assembly can see its own.
/// </summary>
[TestFixture]
public class AppTelemetryRegistryTests
{
    [Test]
    public void EveryDeclaredNameFollowsTheNamingRules()
    {
        TelemetryRegistryContract.AssertHonoured(typeof(AppTelemetryRegistry));
    }

    [Test]
    public void EveryDeclaredInstrumentIsLiveOnTheLayersOwnSurface()
    {
        TelemetryRegistryContract.AssertEveryInstrumentIsConstructed(
            typeof(AppTelemetryRegistry), AppTelemetryRegistry.Surface.Meter);
    }

    /// <summary>
    /// The host's meter must carry the name the composition root registers with <c>AddMeter</c>. A mismatch
    /// builds a provider that collects nothing from this layer, which reads downstream as "the app records
    /// no metrics" rather than as a wiring mistake.
    /// </summary>
    [Test]
    public void TheSurfaceUsesTheScopeNameTheCompositionRootRegisters()
    {
        Assert.Multiple(() =>
        {
            // QUALIFIED. Unqualified, `Telemetry` binds to the SDK's Ihc.Telemetry from this namespace: C#
            // searches enclosing namespaces (Ihc.Vis.Tests -> Ihc.Vis -> Ihc) BEFORE it consults a using, so
            // the app's own type loses to a same-named SDK one and the assertion compares the wrong pair.
            Assert.That(AppTelemetryRegistry.Surface.ActivitySource,
                Is.SameAs(ihc_openvisual.Configuration.Telemetry.ActivitySource));
            Assert.That(AppTelemetryRegistry.Surface.Meter.Name,
                Is.EqualTo(ihc_openvisual.Configuration.Telemetry.ActivitySourceName));
        });
    }
}
