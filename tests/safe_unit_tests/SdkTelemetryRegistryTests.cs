using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK half of the registry drift check. The app half is <c>AppTelemetryRegistryTests</c> in
    /// <c>safe_project_tests</c>, because <c>ihcclient</c> grants internals access to this suite and
    /// <c>ihc_openvisual</c> grants it to that one - no single suite can reflect both assemblies, so the
    /// check is split rather than skipped.
    /// </summary>
    [TestFixture]
    public class SdkTelemetryRegistryTests
    {
        [Test]
        public void EveryDeclaredNameFollowsTheNamingRules()
        {
            TelemetryRegistryContract.AssertHonoured(typeof(SdkTelemetryRegistry));
        }

        [Test]
        public void EveryDeclaredInstrumentIsLiveOnTheLayersOwnSurface()
        {
            TelemetryRegistryContract.AssertEveryInstrumentIsConstructed(
                typeof(SdkTelemetryRegistry), SdkTelemetryRegistry.Surface.Meter);
        }

        /// <summary>
        /// The surface must ADOPT the shipped <see cref="Telemetry.ActivitySource"/>. A second source under
        /// the same name would still be collected - listeners match by name - so the mistake is invisible in
        /// the backend and only visible here.
        /// </summary>
        [Test]
        public void TheSurfaceAdoptsTheShippedActivitySource()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SdkTelemetryRegistry.Surface.ActivitySource, Is.SameAs(Telemetry.ActivitySource));
                Assert.That(SdkTelemetryRegistry.Surface.Meter.Name, Is.EqualTo(Telemetry.MeterName));
            });
        }
    }
}
