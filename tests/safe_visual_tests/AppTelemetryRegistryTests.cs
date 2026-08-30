using ihc_openvisual.Configuration;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The host half of the registry drift check. The SDK half lives in <c>safe_unit_tests</c>: this suite is
/// the only one <c>ihc_openvisual</c> grants internals access to, so the check is split across the two
/// suites that can actually see their own registry rather than centralised where neither can.
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
            Assert.That(AppTelemetryRegistry.Surface.ActivitySource, Is.SameAs(Telemetry.ActivitySource));
            Assert.That(AppTelemetryRegistry.Surface.Meter.Name, Is.EqualTo(Telemetry.ActivitySourceName));
        });
    }
}
