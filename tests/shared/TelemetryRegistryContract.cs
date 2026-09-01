using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// The naming and completeness rules every telemetry registry answers to, checked by reflection so they
    /// apply to whatever the registry declares rather than to a list someone remembered to update.
    ///
    /// <para>Shared because there are TWO registries in different assemblies and no test suite can see both:
    /// <c>ihcclient</c> grants internals access to <c>safe_unit_tests</c>, <c>ihc_openvisual</c> to
    /// <c>safe_visual_tests</c> only. Duplicating the rules would let the two drift apart, which is exactly
    /// the failure the registries exist to prevent.</para>
    /// </summary>
    internal static class TelemetryRegistryContract
    {
        /// <summary>
        /// Attribute names that are published semantic conventions rather than IHC names, and so are
        /// exempt from the <c>ihc.</c> prefix rule. Listed rather than pattern-matched: an exemption should
        /// be a decision someone took, not a shape a new name can accidentally acquire.
        /// </summary>
        private static readonly HashSet<string> SemanticConventions = new(StringComparer.Ordinal)
        {
            "error.type",
        };

        /// <summary>Applies every rule to one registry class.</summary>
        /// <param name="registry">The registry type: instruments as properties, names under a nested Attributes.</param>
        internal static void AssertHonoured(Type registry)
        {
            List<Instrument> instruments = Instruments(registry);
            List<string> attributeNames = ConstStrings(registry.GetNestedType("Attributes", BindingFlags.NonPublic | BindingFlags.Public)!);

            Assert.Multiple(() =>
            {
                Assert.That(instruments, Is.Not.Empty,
                    "a registry that declares no instrument would make every rule below vacuous");
                Assert.That(attributeNames, Is.Not.Empty);

                foreach (Instrument instrument in instruments)
                {
                    AssertNameFollowsTheRules(instrument.Name, requireIhcPrefix: true);

                    Assert.That(instrument.Unit, Is.Not.Null.And.Not.Empty,
                        $"instrument '{instrument.Name}' must declare a unit; a bare number is not a measurement");
                    Assert.That(instrument.Description, Is.Not.Null.And.Not.Empty,
                        $"instrument '{instrument.Name}' must describe what it measures");

                    // Both directions, because the bucket-boundary view selects on the NAME: it is installed by
                    // the wildcard "*.duration", so a second-scale histogram named anything else silently keeps
                    // OpenTelemetry's unitless 0-10000 defaults - every measurement in the first bucket, and a
                    // latency graph that cannot get worse. The suffix is what makes that wildcard exact.
                    bool measuresSeconds = instrument.Unit == "s";
                    bool namedAsDuration = instrument.Name.EndsWith(".duration", StringComparison.Ordinal);
                    Assert.That(namedAsDuration, Is.EqualTo(measuresSeconds),
                        $"instrument '{instrument.Name}' (unit '{instrument.Unit}') must end in '.duration' if and "
                        + "only if it measures seconds; the duration bucket view is selected by that suffix");
                }

                foreach (string name in attributeNames)
                {
                    AssertNameFollowsTheRules(name, requireIhcPrefix: !SemanticConventions.Contains(name));
                }

                Assert.That(instruments.Select(i => i.Name).ToList(), Is.Unique);
                Assert.That(attributeNames, Is.Unique);
            });
        }

        /// <summary>
        /// Every declared instrument is LIVE once the registry is touched - not merely declared. An
        /// instrument parked in a static nobody constructs never exists, and a metric that never exists
        /// looks exactly like one that is never recorded.
        /// </summary>
        internal static void AssertEveryInstrumentIsConstructed(Type registry, Meter expectedMeter)
        {
            foreach (Instrument instrument in Instruments(registry))
            {
                Assert.That(instrument.Meter, Is.SameAs(expectedMeter),
                    $"instrument '{instrument.Name}' must come from the layer's own surface");
            }
        }

        private static void AssertNameFollowsTheRules(string name, bool requireIhcPrefix)
        {
            Assert.That(name, Is.EqualTo(name.ToLowerInvariant()), $"'{name}' must be lowercase");

            if (requireIhcPrefix)
            {
                Assert.That(name, Does.StartWith("ihc."),
                    $"'{name}' must carry the ihc. prefix or be a listed semantic convention");
            }

            Assert.That(name, Does.Not.EndWith("_total"),
                $"'{name}' must not carry a _total suffix; the backend adds one where its model needs it");

            // Namespace segments are singular: ihc.problem.raised, never ihc.problems.raised. Words that
            // merely END in s (analysis, status) are not plurals, so they are exempt by their ending.
            string[] segments = name.Split('.');
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string segment = segments[i];
                bool looksPlural = segment.EndsWith("s", StringComparison.Ordinal)
                                   && !segment.EndsWith("ss", StringComparison.Ordinal)
                                   && !segment.EndsWith("is", StringComparison.Ordinal)
                                   && !segment.EndsWith("us", StringComparison.Ordinal);
                Assert.That(looksPlural, Is.False,
                    $"'{name}' has a pluralised namespace segment '{segment}'");
            }
        }

        private static List<Instrument> Instruments(Type registry) =>
            registry.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(p => typeof(Instrument).IsAssignableFrom(p.PropertyType))
                .Select(p => (Instrument)p.GetValue(null)!)
                .ToList();

        private static List<string> ConstStrings(Type type) =>
            type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();
    }
}
