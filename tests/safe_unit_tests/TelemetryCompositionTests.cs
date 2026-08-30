using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using Ihc.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenTelemetry;

namespace Ihc.Tests
{
    /// <summary>
    /// What the one composition root actually builds.
    ///
    /// Nothing asserted on this before: the providers were constructed at start-up and their absence would
    /// have shown up only as telemetry silently not arriving, which is indistinguishable from "the app did
    /// nothing interesting". These tests build the providers and inspect them; they never export, and the
    /// endpoints point at a port nothing listens on.
    ///
    /// Registration is checked through <see cref="Instrument.Enabled"/>, which reports whether any listener
    /// subscribed to the instrument - that is the observable consequence of <c>AddMeter</c>, and unlike
    /// reflection over the provider's internals it is a supported API.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TelemetryCompositionTests
    {
        private const string AppScopeName = "IhcCompositionTestApp";

        // Port 1 refuses instantly, so a flush on dispose cannot stall the suite.
        private static TelemetryConfiguration AllEndpoints() => new()
        {
            Host = "http://localhost:1",
            Logs = "http://localhost:1/v1/logs",
            Traces = "http://localhost:1/v1/traces",
            Metrics = "http://localhost:1/v1/metrics",
        };

        private static ILoggerFactory Setup(TelemetryConfiguration telemetry) =>
            TelemetryBootstrap.SetupTelemetryAndLogging(
                "IhcCompositionTest", "Ihc", AppScopeName, telemetry, new ConfigurationBuilder().Build());

        /// <summary>Leaves the process-wide statics empty, so one test cannot decide another's outcome.</summary>
        [TearDown]
        public void ResetProviders() => Setup(new TelemetryConfiguration()).Dispose();

        [Test]
        public void WithEveryEndpointConfigured_BothProvidersAreBuilt()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            Assert.Multiple(() =>
            {
                Assert.That(TelemetryBootstrap.TracerProvider, Is.Not.Null);
                Assert.That(TelemetryBootstrap.MeterProvider, Is.Not.Null);
                Assert.That(factory, Is.Not.Null);
            });
        }

        [Test]
        public void WithNoEndpointsConfigured_NeitherProviderIsBuilt()
        {
            using ILoggerFactory factory = Setup(new TelemetryConfiguration());

            Assert.Multiple(() =>
            {
                Assert.That(TelemetryBootstrap.TracerProvider, Is.Null,
                    "an unconfigured endpoint must leave the signal off entirely");
                Assert.That(TelemetryBootstrap.MeterProvider, Is.Null);
                Assert.That(factory, Is.Not.Null, "local logging works with no telemetry configured at all");
            });
        }

        /// <summary>Metrics are opt-in on their own key: traces configured alone must not build a MeterProvider.</summary>
        [Test]
        public void WithOnlyTracesConfigured_NoMeterProviderIsBuilt()
        {
            using ILoggerFactory factory = Setup(new TelemetryConfiguration { Traces = "http://localhost:1/v1/traces" });

            Assert.Multiple(() =>
            {
                Assert.That(TelemetryBootstrap.TracerProvider, Is.Not.Null);
                Assert.That(TelemetryBootstrap.MeterProvider, Is.Null);
            });
        }

        [Test]
        public void MeterProvider_SubscribesToBothTheSdkAndTheAppMeter()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            using var sdkMeter = new Meter(Telemetry.MeterName);
            using var appMeter = new Meter(AppScopeName);
            using var unrelatedMeter = new Meter("SomeThirdPartyLibrary");

            Counter<long> sdkCounter = sdkMeter.CreateCounter<long>("ihc.composition.probe");
            Counter<long> appCounter = appMeter.CreateCounter<long>("ihc.composition.probe");
            Counter<long> unrelatedCounter = unrelatedMeter.CreateCounter<long>("ihc.composition.probe");

            Assert.Multiple(() =>
            {
                Assert.That(sdkCounter.Enabled, Is.True, "the SDK meter must be registered on the provider");
                Assert.That(appCounter.Enabled, Is.True, "the app meter must be registered on the provider");
                // Without this the two assertions above would also pass on a provider that listened to everything.
                Assert.That(unrelatedCounter.Enabled, Is.False,
                    "only the two declared meters are collected, not every meter in the process");
            });
        }

        /// <summary>
        /// OpenTelemetry .NET carries NO attribute limits by default: an unbounded value goes out whole.
        /// The limits live in the OTLP exporter and are read from these variables when a provider is built,
        /// so the composition root sets them. Asserted as configuration because that is where the SDK
        /// applies them - the exporter truncates at serialization time, never on the in-process Activity.
        /// </summary>
        [Test]
        public void AttributeLimits_AreSetExplicitlyRatherThanLeftUnbounded()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            Assert.Multiple(() =>
            {
                Assert.That(System.Environment.GetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT"),
                    Is.EqualTo("4096"));
                Assert.That(System.Environment.GetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT"),
                    Is.EqualTo("128"));
            });
        }

        /// <summary>An operator who set a limit themselves outranks the default; the app must not stamp over it.</summary>
        [Test]
        public void AttributeLimits_DoNotOverrideAValueTheOperatorAlreadySet()
        {
            const string variable = "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT";
            string? original = System.Environment.GetEnvironmentVariable(variable);
            try
            {
                System.Environment.SetEnvironmentVariable(variable, "77");

                using ILoggerFactory factory = Setup(AllEndpoints());

                Assert.That(System.Environment.GetEnvironmentVariable(variable), Is.EqualTo("77"),
                    "a deliberately configured limit must survive the app's default");
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(variable, original);
            }
        }

        private static string? ResourceAttribute(BaseProvider provider, string key)
        {
            foreach (KeyValuePair<string, object> attribute in provider.GetResource().Attributes)
            {
                if (attribute.Key == key)
                {
                    return attribute.Value?.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Both signals must describe the SAME process, or a backend cannot join them. The environment is what
        /// separates a developer's run from a customer site; without it every record from every machine looks alike.
        /// </summary>
        [Test]
        public void Resource_CarriesTheConfiguredDeploymentEnvironment_OnEverySignal()
        {
            TelemetryConfiguration telemetry = AllEndpoints();
            telemetry.Environment = "staging";
            using ILoggerFactory factory = Setup(telemetry);

            Assert.Multiple(() =>
            {
                Assert.That(ResourceAttribute(TelemetryBootstrap.TracerProvider!, "deployment.environment.name"),
                    Is.EqualTo("staging"));
                Assert.That(ResourceAttribute(TelemetryBootstrap.MeterProvider!, "deployment.environment.name"),
                    Is.EqualTo("staging"));
            });
        }

        /// <summary>Unlike the endpoint keys, an empty environment does not mean "off" - it means not yet labelled.</summary>
        [Test]
        public void Resource_WithNoEnvironmentConfigured_FallsBackToDevelopment()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            Assert.That(ResourceAttribute(TelemetryBootstrap.TracerProvider!, "deployment.environment.name"),
                Is.EqualTo("development"));
        }

        /// <summary>
        /// The per-launch id is what every "did MY run do this?" query scopes on. OpenTelemetry generates one
        /// automatically, which is why this reads as a regression pin rather than a feature: it must be the SAME
        /// value on every signal and stable for the process, so a future change to how the resource is built
        /// cannot quietly hand each provider its own id - or drop the field entirely.
        /// </summary>
        [Test]
        public void Resource_ServiceInstanceId_IsOneValueForTheWholeProcess()
        {
            using (ILoggerFactory first = Setup(AllEndpoints()))
            {
                string? tracerId = ResourceAttribute(TelemetryBootstrap.TracerProvider!, "service.instance.id");
                string? meterId = ResourceAttribute(TelemetryBootstrap.MeterProvider!, "service.instance.id");

                Assert.Multiple(() =>
                {
                    Assert.That(tracerId, Is.Not.Null.And.Not.Empty);
                    Assert.That(meterId, Is.EqualTo(tracerId), "traces and metrics must agree on which run they are");
                });

                using ILoggerFactory second = Setup(AllEndpoints());
                Assert.That(ResourceAttribute(TelemetryBootstrap.TracerProvider!, "service.instance.id"),
                    Is.EqualTo(tracerId), "the id identifies the PROCESS, so a second bootstrap must not mint a new one");
            }
        }

        [Test]
        public void WithNoMetricsEndpoint_TheSdkMeterIsNotCollected()
        {
            using ILoggerFactory factory = Setup(new TelemetryConfiguration());

            using var sdkMeter = new Meter(Telemetry.MeterName);
            Counter<long> counter = sdkMeter.CreateCounter<long>("ihc.composition.probe");

            Assert.That(counter.Enabled, Is.False,
                "with metrics unconfigured nothing collects, so recording costs nothing");
        }

        // ---- The console-utility shape ---------------------------------------------------------------------
        // The three CLI utilities call the same builder as the desktop apps, but their configuration differs in
        // one way that matters: they have no logging configuration section at all. These two pin the shape they
        // actually pass, because the hand-rolled provider each utility used to carry got both cases wrong.

        /// <summary>
        /// A utility passes an EMPTY logging configuration and its own scope name, and needs both signals: its
        /// own spans, and - new with the shared builder - its own metrics. Registration of the scope is the part
        /// worth asserting, since a provider that built but did not listen to the utility's source would export
        /// the SDK's spans and silently drop the utility's own.
        /// </summary>
        [Test]
        public void AConsoleShapedConfiguration_BuildsBothProvidersAndListensToTheUtilityScope()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            using var utilitySource = new System.Diagnostics.ActivitySource(AppScopeName);
            using var utilityMeter = new Meter(AppScopeName);
            Counter<long> utilityCounter = utilityMeter.CreateCounter<long>("ihc.composition.probe");

            Assert.Multiple(() =>
            {
                Assert.That(TelemetryBootstrap.TracerProvider, Is.Not.Null);
                Assert.That(TelemetryBootstrap.MeterProvider, Is.Not.Null);
                Assert.That(utilitySource.HasListeners(), Is.True,
                    "the utility's own spans must reach the provider, not just the SDK's");
                Assert.That(utilityCounter.Enabled, Is.True,
                    "metrics are what the shared builder ADDS to a utility that had none");
            });
        }

        /// <summary>
        /// The defect the shared builder removes. Each utility used to call
        /// <c>Sdk.CreateTracerProviderBuilder()</c> unconditionally and test the endpoint INSIDE the exporter
        /// callback - so with no telemetry configured a provider was still built and still exported, to the OTLP
        /// default endpoint rather than to nothing. Gating on the endpoint is the whole difference.
        /// </summary>
        [Test]
        public void AConsoleUtilityWithNoEndpoints_BuildsNoProviderAtAll()
        {
            using ILoggerFactory factory = Setup(new TelemetryConfiguration());

            using var utilitySource = new System.Diagnostics.ActivitySource(AppScopeName);

            Assert.Multiple(() =>
            {
                Assert.That(TelemetryBootstrap.TracerProvider, Is.Null);
                Assert.That(TelemetryBootstrap.MeterProvider, Is.Null);
                Assert.That(utilitySource.HasListeners(), Is.False,
                    "nothing is listening, so an unconfigured utility pays nothing for its instrumentation");
            });
        }

        /// <summary>
        /// The metric-to-trace join. The core already pays for it at every measurement - instruments are
        /// recorded BEFORE the activity is disposed, so each point is exemplar-eligible - but eligibility buys
        /// nothing unless the provider is told to attach exemplars, and the .NET SDK does NOT default to the
        /// specification's <c>trace_based</c>: <c>MeterProviderBuilderSdk.ExemplarFilter</c> is a nullable with
        /// no value until something sets it. Without this the cost is paid on every measurement and the benefit
        /// - following a latency spike to the trace that produced it - is switched off.
        ///
        /// <para>Read by reflection, which this fixture otherwise avoids on principle (see the class summary:
        /// registration is checked through <c>Instrument.Enabled</c> because that is a supported API). The
        /// exception is deliberate and narrow: exemplar attachment has no supported read-back on a built
        /// provider, and its only other observable is an exemplar arriving at an exporter this method does not
        /// let a caller supply. The end-to-end effect is confirmed against the live backend instead; this test
        /// exists so DELETING the call is caught here rather than by noticing months later that no metric row
        /// carries an exemplar.</para>
        /// </summary>
        [Test]
        public void TheMeterProvider_AttachesExemplarsSoAMetricPointCanBeTracedBack()
        {
            using ILoggerFactory factory = Setup(AllEndpoints());

            // The built provider's runtime type IS MeterProviderSdk, so the field is read off it directly.
            object provider = TelemetryBootstrap.MeterProvider!;
            object? filter = provider.GetType()
                .GetField("ExemplarFilter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(provider);

            Assert.That(filter?.ToString(), Is.EqualTo("TraceBased"),
                "an unset filter records no exemplars at all, so every metric point loses its link to the "
                + "trace it came from - the join the core's record-before-dispose ordering exists to enable");
        }
    }
}
