using System.Linq;
using ihc_openvisual.Configuration;
using Ihc.Tests;
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

    /// <summary>
    /// An owner built through the registry's factory reports a fault in the RECORDING, and reports it where
    /// every other host fault goes.
    /// </summary>
    /// <remarks>
    /// The instrumentation core has had a fault port for a while, and the SDK's application services bind one;
    /// the host's owners were all constructed straight from the surface, so the port was null for every span
    /// this application opens. That is the shape a wiring gap takes here — nothing fails, nothing is logged, and
    /// a broken recording stays silent — so what is asserted is that the port is BOUND, on an owner obtained the
    /// way the product obtains them.
    /// </remarks>
    [Test]
    public void AnOwnerFromTheFactory_ReportsAFaultInItsOwnRecording()
    {
        using CapturedFaults faults = new();
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "FactoryProbe.");
        Ihc.OperationTelemetry telemetry = AppTelemetryRegistry.For("FactoryProbe");

        // The span's status description is read from the exception, so an exception that cannot describe itself
        // breaks the recording — which is the fault this port exists to surface.
        using (Ihc.OperationScope scope = telemetry.Start("Work"))
        {
            scope.SetOutcome(Ihc.OperationOutcome.Failed(new UnreadableException()));
        }

        Assert.That(faults.Rows, Is.Not.Empty,
            "a host span whose recording broke has to leave a record; before the port was bound it left none");
    }

    /// <summary>
    /// Every host owner is built THROUGH that factory, because a port bound on a factory nobody calls is no port.
    /// </summary>
    /// <remarks>
    /// Read off the IL rather than trusted, for the reason the gap itself gives: the thirteen owners were built
    /// straight from the surface for as long as the port existed, and nothing anywhere said so — an unbound port
    /// is the ABSENCE of reports, which no assertion about behaviour can catch from outside. The factory is the
    /// only member allowed to reach the constructor, so the population of violators is exactly "everyone else".
    /// </remarks>
    [Test]
    public void EveryHostOwnerIsBuiltThroughTheFactory()
    {
        System.Collections.Generic.List<string> builders = AuthoredMembers
            .Of(typeof(ihc_openvisual.App).Assembly)
            .Where(member => IlBody.CalledMethods(member).Any(
                called => called.IsConstructor && called.DeclaringType == typeof(Ihc.OperationTelemetry)))
            .Select(member => $"{member.DeclaringType?.FullName}.{member.Name}")
            .Distinct()
            .ToList();

        Assert.Multiple(() =>
        {
            // ARMED: the factory is one call site the scan must see, so an empty result below is a verdict
            // rather than a scan that resolved no tokens.
            Assert.That(builders, Does.Contain($"{typeof(AppTelemetryRegistry).FullName}.{nameof(AppTelemetryRegistry.For)}"),
                "non-vacuity: the scan has to find the one site that is allowed to construct one");
            Assert.That(
                builders.Where(site => !site.StartsWith(typeof(AppTelemetryRegistry).FullName!, System.StringComparison.Ordinal)),
                Is.Empty,
                "constructing OperationTelemetry directly skips the fault port; use AppTelemetryRegistry.For");
        });
    }
}
