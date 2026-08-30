using System;
using System.Collections.Generic;
using System.Linq;
using ArchUnitNET.Domain;

namespace Ihc.Tests
{
    /// <summary>
    /// The instrumentation core is only a standard if it cannot be bypassed. Every span in the SDK and the GUI
    /// must be started through <c>OperationTelemetry</c>, and every instrument must be declared by the one
    /// registry its layer owns — otherwise a signal exists that no policy applies to: no normalized
    /// <c>error.type</c>, no outcome on both signals, no duration measured the same way, and, for an
    /// instrument, no place a reader can go to learn the instrument exists.
    ///
    /// <para>Three detectors, because a bypass has three shapes: starting a span from a raw
    /// <see cref="System.Diagnostics.ActivitySource"/>, constructing a second
    /// <see cref="System.Diagnostics.Metrics.Meter"/>, and building an instrument off a meter the registry does
    /// not own. Each is scanned over the recorded dependency edges rather than through the fluent API, which
    /// goes vacuous when a forbidden type is (correctly) absent from the subject's own model.</para>
    ///
    /// <para>THE ROSTER IS EMPTY, AND THAT IS THE RESULT RATHER THAN AN OVERSIGHT. The two legacy helpers a
    /// reader would expect to find exempted here, <c>ServiceBase.StartActivity</c> and
    /// <c>AppServiceBase.StartActivity</c>, both delegate to <c>OperationTelemetry.StartSpan</c>: they still
    /// exist, but they are doors INTO the core rather than ways around it, so neither calls
    /// <c>StartActivity</c> and neither needs exempting. The only exemptions are the core itself and the two
    /// registries, which are the declared single home for instrument construction.</para>
    ///
    /// <para>An empty roster over a clean subject is also the exact condition under which a broken detector
    /// looks identical to an enforced one. That is what <see cref="TheDetectorsFlagASeededBypass"/> is for.</para>
    /// </summary>
    public class TelemetryCoreArchitectureTests
    {
        private const string SdkRoot = "Ihc";
        private const string GuiRoot = "ihc_openvisual";
        private const string SeedRoot = "Ihc.Telemetry.Seeded";

        private const string ActivitySourceType = "System.Diagnostics.ActivitySource";
        private const string MeterType = "System.Diagnostics.Metrics.Meter";

        // The core, by full name rather than typeof: OperationTelemetry is public but the rule reads the
        // recorded edges as strings, and mixing the two forms would let one drift from the other.
        private const string CoreTelemetry = "Ihc.OperationTelemetry";
        private const string CoreSurface = "Ihc.TelemetrySurface";
        private const string SdkRegistry = "Ihc.SdkTelemetryRegistry";
        private const string AppRegistry = "ihc_openvisual.Configuration.AppTelemetryRegistry";

        /// <summary>
        /// Instruments are created through the meter's factory methods, not with <c>new</c>, so "constructs an
        /// instrument" is a call scan over this family. Named individually rather than by a <c>Create</c>
        /// prefix, so a future framework method starting with those six letters cannot silently widen the ban.
        /// </summary>
        private static readonly string[] InstrumentFactories =
        [
            "CreateCounter", "CreateUpDownCounter", "CreateHistogram", "CreateGauge",
            "CreateObservableCounter", "CreateObservableUpDownCounter", "CreateObservableGauge",
        ];

        // ---- The production rules --------------------------------------------------------------------------

        [Test]
        public void OnlyTheCoreStartsSpans()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SpanBypasses(ArchitectureModels.Sdk, SdkRoot), Is.Empty,
                    "an SDK span started outside OperationTelemetry carries none of the core's policy: no "
                    + "normalized error.type, no outcome, no duration measured the core's way");
                Assert.That(SpanBypasses(ArchitectureModels.Gui, GuiRoot), Is.Empty,
                    "a GUI span started outside OperationTelemetry carries none of the core's policy");
            });
        }

        [Test]
        public void OnlyTheCoreAndTheRegistriesOwnAMeter()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MeterConstructions(ArchitectureModels.Sdk, SdkRoot), Is.Empty,
                    "a second meter publishes instruments under an identity the host never registered, so they "
                    + "reach no exporter at all");
                Assert.That(MeterConstructions(ArchitectureModels.Gui, GuiRoot), Is.Empty,
                    "a second meter publishes instruments under an identity the host never registered");
            });
        }

        [Test]
        public void OnlyTheRegistriesDeclareInstruments()
        {
            Assert.Multiple(() =>
            {
                Assert.That(InstrumentDeclarations(ArchitectureModels.Sdk, SdkRoot), Is.Empty,
                    "the SDK registry is the single place an instrument's name, unit and description are "
                    + "declared, which is what the naming drift test can hold to a rule");
                Assert.That(InstrumentDeclarations(ArchitectureModels.Gui, GuiRoot), Is.Empty,
                    "the app registry is the single place an instrument is declared");
            });
        }

        /// <summary>
        /// Wanting <see cref="System.Diagnostics.ActivityKind"/>.Client or an
        /// <see cref="System.Diagnostics.ActivityLink"/> is explicitly NOT grounds for an exemption: both are
        /// parameters of the core's own entry points, so a site needing either still goes through it. Asserted
        /// rather than merely written down, because the two are the most plausible reasons someone would reach
        /// for the raw source, and a reason that is only in a comment does not survive being disagreed with.
        /// </summary>
        [Test]
        public void KindAndLinksAreCoreParametersRatherThanReasonsToBypassIt()
        {
            var core = typeof(global::Ihc.OperationTelemetry);
            var parameterTypes = core.GetMethods()
                .Concat<System.Reflection.MethodBase>(core.GetConstructors())
                .SelectMany(m => m.GetParameters())
                .SelectMany(p => ArchRuleHelpers.TypeAndArguments(p.ParameterType))
                .Select(t => t.FullName)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(parameterTypes, Does.Contain(typeof(System.Diagnostics.ActivityKind).FullName),
                    "a site that needs a Client span must be able to ask the core for one");
                Assert.That(parameterTypes, Does.Contain(typeof(System.Diagnostics.ActivityLink).FullName),
                    "a site that needs to link a span must be able to ask the core for that too");
            });
        }

        // ---- Non-vacuity ---------------------------------------------------------------------------------

        /// <summary>
        /// The same three scans, run over the seeded controls in this assembly. Each must flag its violator by
        /// name, and none may flag the negative control that reaches the same outcome through the core.
        /// </summary>
        [Test]
        public void TheDetectorsFlagASeededBypass()
        {
            Architecture seeds = ArchitectureModels.ArchitectureTests.Value;

            var spans = SpanBypasses(seeds, SeedRoot);
            var meters = MeterConstructions(seeds, SeedRoot);
            var instruments = InstrumentDeclarations(seeds, SeedRoot);

            Assert.Multiple(() =>
            {
                Assert.That(spans, Does.Contain(SeedRoot + ".SeededDirectSpanStarter.Bypass"),
                    "the span detector saw nothing, so its green result on the SDK and GUI proves nothing");
                Assert.That(spans, Does.Contain(SeedRoot + ".SeededAsyncDirectSpanStarter.BypassAsync"),
                    "the bypass inside an async body is emitted on a state machine; a scan that stops at "
                    + "authored types misses exactly the sites most likely to introduce one");
                Assert.That(meters, Does.Contain(SeedRoot + ".SeededMeterOwner"),
                    "the meter detector saw nothing");
                // ".ctor", not ".Rogue": the instrument is a property INITIALIZER, which the compiler emits
                // into the constructor. That is the shape both real registries use, so the detector reporting
                // the constructor is it reading the assembly correctly rather than approximately.
                Assert.That(instruments, Does.Contain(SeedRoot + ".SeededInstrumentOwner..ctor"),
                    "the instrument detector saw nothing");

                // The negative control must survive all three, or the rule forbids the shape it requires.
                string[] flagged = [.. spans, .. meters, .. instruments];
                Assert.That(flagged.Where(f => f.Contains("SeededCoreUser", StringComparison.Ordinal)), Is.Empty,
                    "going through the core is the required shape, so it must never be flagged");
            });
        }

        // ---- The scans -----------------------------------------------------------------------------------

        private static IReadOnlyList<string> SpanBypasses(Architecture arch, string root) =>
        [
            .. ArchRuleHelpers.MethodCallEdges(arch, root)
                .Where(e => e.TargetType == ActivitySourceType && e.Member == "StartActivity")
                .Where(e => ArchRuleHelpers.OutermostTypeName(e.Origin) != CoreTelemetry)
                .Select(e => $"{ArchRuleHelpers.OutermostTypeName(e.Origin)}.{e.OriginMember}")
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal),
        ];

        private static IReadOnlyList<string> MeterConstructions(Architecture arch, string root) =>
        [
            .. ArchRuleHelpers.ConstructorCallEdges(arch, root)
                .Where(e => e.Target == MeterType)
                .Select(e => ArchRuleHelpers.OutermostTypeName(e.Origin))
                .Where(origin => origin is not (CoreSurface or SdkRegistry or AppRegistry))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal),
        ];

        private static IReadOnlyList<string> InstrumentDeclarations(Architecture arch, string root) =>
        [
            .. ArchRuleHelpers.MethodCallEdges(arch, root)
                .Where(e => e.TargetType == MeterType && InstrumentFactories.Contains(e.Member))
                .Where(e => ArchRuleHelpers.OutermostTypeName(e.Origin) is not (SdkRegistry or AppRegistry))
                .Select(e => $"{ArchRuleHelpers.OutermostTypeName(e.Origin)}.{e.OriginMember}")
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal),
        ];
    }
}
