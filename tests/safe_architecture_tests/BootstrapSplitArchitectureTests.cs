using System.Linq;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// The shared host bootstrap is split in two: <c>ihc_telemetrybootstrap</c> builds the logger factory and the
    /// tracer/meter providers and knows nothing about a UI toolkit, while <c>ihc_appbootstrap</c> adds the
    /// Avalonia-only extras on top. The split exists so a CONSOLE utility can build the same telemetry pipeline
    /// without referencing a windowing toolkit (R7).
    ///
    /// <para>That property is invisible in the source — nothing in a file says "no Avalonia here" — and it is
    /// undone by a single <c>using</c> that happens to compile, since the Avalonia package would arrive
    /// transitively the moment anyone added the reference back. So it is asserted rather than documented.</para>
    /// </summary>
    public class BootstrapSplitArchitectureTests
    {
        [Test]
        public void TheNeutralBootstrapHasNoAvaloniaDependency()
        {
            ArchRuleHelpers.AssertAssemblyHasNoDependency(ArchitectureModels.NeutralBootstrap,
                ArchRuleHelpers.AvaloniaNs,
                "a console utility references this half precisely to get the providers WITHOUT a UI toolkit; "
                + "one Avalonia edge here puts Avalonia back into every utility that uses it");
        }

        /// <summary>
        /// The arming counterpart, and it needs no seeded control: the assembly this half was split OUT of does
        /// depend on Avalonia, by design. Running the same rule against it proves the detector can fail — so the
        /// green result above means "no edge" rather than "the scan saw nothing".
        /// </summary>
        [Test]
        public void TheSameRuleFlagsTheAvaloniaHalf()
        {
            ArchRuleHelpers.AssertDependencyIsDetected(ArchitectureModels.AppBootstrap,
                typeof(global::Ihc.Bootstrap.AppTelemetryBootstrap), ArchRuleHelpers.AvaloniaNs,
                "the Avalonia half reported no Avalonia dependency, so the rule above is not measuring anything");
        }

        /// <summary>
        /// Where each member ended up. A split is only real while the pieces stay apart, and the easiest way to
        /// silently undo it is to move a provider member back to the Avalonia side "because that is where the app
        /// calls it from" — which compiles, passes every other test, and re-couples the utilities.
        /// </summary>
        [Test]
        public void TheProviderPipelineLivesOnTheNeutralSide()
        {
            var neutral = typeof(global::Ihc.Bootstrap.TelemetryBootstrap);
            var avalonia = typeof(global::Ihc.Bootstrap.AppTelemetryBootstrap);

            string[] neutralMembers = [.. neutral.GetMembers(BindingFlags.Public | BindingFlags.Static
                | BindingFlags.DeclaredOnly).Select(m => m.Name)];
            string[] avaloniaMembers = [.. avalonia.GetMembers(BindingFlags.Public | BindingFlags.Static
                | BindingFlags.DeclaredOnly).Select(m => m.Name)];

            Assert.Multiple(() =>
            {
                foreach (string member in new[]
                {
                    "SetupTelemetryAndLogging", "ConfigureDurationHistogramViews",
                    "UnhandledExceptionHandler", "UnobservedTaskExceptionHandler", "GetAppVersionStr",
                })
                {
                    Assert.That(neutralMembers, Does.Contain(member), $"{member} belongs on the neutral side");
                    Assert.That(avaloniaMembers, Does.Not.Contain(member),
                        $"{member} is back on the Avalonia side, which re-couples every console caller");
                }

                Assert.That(neutral.GetProperty("TracerProvider"), Is.Not.Null);
                Assert.That(neutral.GetProperty("MeterProvider"), Is.Not.Null);

                // And the toolkit-bound members stay put: a rule that only pushed things one way would be
                // satisfied by moving everything to the neutral side, which is not a split either.
                Assert.That(avaloniaMembers, Does.Contain("LogToSink"));
                Assert.That(avaloniaMembers, Does.Contain("DispatcherExceptionHandler"));
            });
        }
    }
}
