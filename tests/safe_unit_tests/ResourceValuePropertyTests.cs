using System;
using System.Collections.Generic;
using CsCheck;
using KellermanSoftware.CompareNetObjects;
using Ihc;
using Ihc.Soap.Resourceinteraction;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for the ResourceValue domain-to-wire mapping
    /// (<see cref="ResourceValueEnvelopeMapper"/>), using CsCheck.
    ///
    /// The mapping is 24 hand-written casts (12 kinds x 2 directions) with no round-trip coverage.
    /// The core law is ToDomain(ToWire(rv)) == rv. Ten of the twelve kinds round-trip exactly; DATE
    /// and TIME are intentionally lossy, so their generators are constrained to the lossless domain
    /// (see remarks on <see cref="GenDate"/>/<see cref="GenTime"/>) rather than comparing modulo the
    /// truncation.
    /// </summary>
    [TestFixture]
    public class ResourceValuePropertyTests
    {
        // ResourceValue is a record whose value-equality includes ValueTime, and the constructor
        // defaults ValueTime to DateTimeOffset.Now. The wire form does not carry it, so the
        // reconstructed value always has a different ValueTime - ignore it in the comparison.
        private static CompareLogic RoundTripCompare() =>
            new CompareLogic(new ComparisonConfig { MembersToIgnore = new List<string> { "ValueTime" } });

        // Short, non-null text for opaque string payloads (TypeString, enum name, phone number).
        // These are copied verbatim by the mapper, so the exact alphabet is irrelevant; Array[0,20]
        // includes the empty string.
        private const string Alphabet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static readonly Gen<string> ShortText =
            Gen.OneOfConst(Alphabet.ToCharArray()).Array[0, 20].Select(cs => new string(cs));

        // ---- Per-kind UnionValue generators: each sets ValueKind and ONLY its own field(s). ----

        private static readonly Gen<ResourceValue.UnionValue> GenBool =
            Gen.Bool.Select(b => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = b });

        private static readonly Gen<ResourceValue.UnionValue> GenInt =
            Gen.Int.Select(i => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.INT, IntValue = i });

        // Restrict to finite doubles so any failure is attributable (the mapper is a pure copy;
        // NaN would only muddy equality semantics).
        private static readonly Gen<ResourceValue.UnionValue> GenDouble =
            Gen.Double.Where(double.IsFinite).Select(d => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.DOUBLE, DoubleValue = d });

        private static readonly Gen<ResourceValue.UnionValue> GenTimer =
            Gen.Long.Select(l => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.TIMER, TimerValue = l });

        private static readonly Gen<ResourceValue.UnionValue> GenWeekday =
            Gen.Int.Select(w => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.WEEKDAY, WeekdayValue = w });

        private static readonly Gen<ResourceValue.UnionValue> GenPhone =
            ShortText.Select(s => new ResourceValue.UnionValue
            { ValueKind = ResourceValue.ValueKind.PhoneNumber, PhoneNumberValue = s });

        private static readonly Gen<ResourceValue.UnionValue> GenEnum =
            from tid in Gen.Int
            from vid in Gen.Int
            from name in ShortText
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.ENUM,
                EnumValue = new EnumValue { DefinitionTypeID = tid, EnumValueID = vid, EnumName = name }
            };

        // DATE is lossy: domain->wire keeps only year/month/day, and wire->domain rebuilds the value
        // at 00:00:00 with the WS offset. Generate midnight values at that offset so the round-trip
        // is the identity.
        private static readonly Gen<ResourceValue.UnionValue> GenDate =
            from y in Gen.Int[2000, 2099]
            from m in Gen.Int[1, 12]
            from d in Gen.Int[1, DateTime.DaysInMonth(y, m)]
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.DATE,
                DateValue = new DateTimeOffset(y, m, d, 0, 0, 0, DateHelper.GetWSTimeOffset())
            };

        // TIME is lossy: only whole Hours/Minutes/Seconds survive (no Days, no milliseconds,
        // magnitude < 24h). Generate within that lossless domain.
        private static readonly Gen<ResourceValue.UnionValue> GenTime =
            from h in Gen.Int[0, 23]
            from mi in Gen.Int[0, 59]
            from s in Gen.Int[0, 59]
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.TIME,
                TimeValue = new TimeSpan(h, mi, s)
            };

        private static readonly Gen<ResourceValue.UnionValue> GenSceneDimmer =
            from pct in Gen.Int
            from delay in Gen.Int
            from ramp in Gen.Int
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.SceneDimmer,
                DimmerPercentage = pct, DimmerDelayTime = delay, DimmerRampTime = ramp
            };

        private static readonly Gen<ResourceValue.UnionValue> GenSceneRelay =
            from delay in Gen.Int
            from relayOn in Gen.Bool
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.SceneRelay,
                RelayDelayTime = delay, RelayValue = relayOn
            };

        private static readonly Gen<ResourceValue.UnionValue> GenSceneShutter =
            from up in Gen.Bool
            from delay in Gen.Int
            select new ResourceValue.UnionValue
            {
                ValueKind = ResourceValue.ValueKind.SceneShutter,
                ShutterPositionIsUp = up, ShutterDelayTime = delay
            };

        private static readonly Gen<ResourceValue.UnionValue> GenUnion =
            Gen.OneOf(GenBool, GenDate, GenInt, GenDouble, GenEnum, GenTime, GenTimer, GenWeekday,
                      GenPhone, GenSceneDimmer, GenSceneRelay, GenSceneShutter);

        private static readonly Gen<ResourceValue> GenResourceValue =
            from union in GenUnion
            from id in Gen.Int
            from runtime in Gen.Bool
            from typeString in ShortText
            select new ResourceValue
            {
                ResourceID = id,
                IsValueRuntime = runtime,
                TypeString = typeString,
                Value = union
            };

        private static readonly Gen<ResourceValue> GenBoolResourceValue =
            from b in Gen.Bool
            from id in Gen.Int
            from runtime in Gen.Bool
            from typeString in ShortText
            select new ResourceValue
            {
                ResourceID = id,
                IsValueRuntime = runtime,
                TypeString = typeString,
                Value = new ResourceValue.UnionValue
                { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = b }
            };

        /// <summary>
        /// Law: mapping a ResourceValue to its wire envelope and back yields an equal value
        /// (ignoring the diagnostics-only ValueTime), for every one of the 12 value kinds.
        /// </summary>
        [Test]
        public void ToWire_ToDomain_RoundTripsAllValueKinds()
        {
            GenResourceValue.Sample(rv =>
            {
                var wire = ResourceValueEnvelopeMapper.ToWire(rv);
                var back = ResourceValueEnvelopeMapper.ToDomain(wire);
                return RoundTripCompare().Compare(back, rv).AreEqual;
            });
        }

        /// <summary>
        /// Law: ToogleBool is its own inverse for boolean values (modulo ValueTime).
        /// </summary>
        [Test]
        public void ToogleBool_IsInvolution()
        {
            GenBoolResourceValue.Sample(rv =>
            {
                var twice = ResourceValue.ToogleBool(ResourceValue.ToogleBool(rv));
                return RoundTripCompare().Compare(twice, rv).AreEqual;
            });
        }

        /// <summary>
        /// Pins the exact single-toggle behavior: the envelope (TypeString/ResourceID/IsValueRuntime)
        /// is preserved, the value kind stays BOOL, and only the boolean payload flips.
        /// </summary>
        [Test]
        public void ToogleBool_PreservesEnvelope_AndFlipsValue()
        {
            GenBoolResourceValue.Sample(rv =>
            {
                var toggled = ResourceValue.ToogleBool(rv);
                return toggled.TypeString == rv.TypeString
                    && toggled.ResourceID == rv.ResourceID
                    && toggled.IsValueRuntime == rv.IsValueRuntime
                    && toggled.Value.ValueKind == ResourceValue.ValueKind.BOOL
                    && toggled.Value.BoolValue == !rv.Value.BoolValue;
            });
        }

        /// <summary>
        /// Hardening contract for the unguarded Nullable&lt;T&gt;-&gt;T casts in ToWire: when a caller
        /// builds a UnionValue whose ValueKind is set but the matching payload was left null, ToWire
        /// must fail with a clear, typed ArgumentException instead of an opaque
        /// "Nullable object must not have a value" InvalidOperationException from the cast.
        /// (Reproduces review finding #3; the round-trip property above only ever generates
        /// fully-populated values, so it cannot exercise this edge.)
        /// </summary>
        [Test]
        public void ToWire_ValueKindWithoutMatchingPayload_ThrowsArgumentException()
        {
            var malformed = new ResourceValue
            {
                ResourceID = 42,
                TypeString = "dataline_output",
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT } // IntValue left null
            };

            Assert.Throws<ArgumentException>(() => ResourceValueEnvelopeMapper.ToWire(malformed));
        }

        /// <summary>
        /// Reproduces review finding #2, confirmed live: a resource with no readable runtime value
        /// (e.g. a scene, type 'resource_scene') comes back as an envelope whose inner &lt;value&gt; is
        /// null. ToDomain must classify that as ValueKind.NONE, not silently mislabel it as BOOL with a
        /// null payload (207 such scene resources were observed on a real controller).
        /// </summary>
        [Test]
        public void ToDomain_NullInnerValue_MapsToNoneKind_NotBool()
        {
            var envelope = new WSResourceValueEnvelope
            {
                resourceID = 47434,
                typeString = "resource_scene",
                isValueRuntime = true,
                value = null
            };

            var rv = ResourceValueEnvelopeMapper.ToDomain(envelope);

            Assert.Multiple(() =>
            {
                Assert.That(rv, Is.Not.Null);
                Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.NONE));
                Assert.That(rv!.Value.BoolValue, Is.Null);
                Assert.That(rv!.ResourceID, Is.EqualTo(47434));
            });
        }
    }
}
