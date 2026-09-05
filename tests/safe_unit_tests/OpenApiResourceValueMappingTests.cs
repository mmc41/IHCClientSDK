using System;
using System.Collections.Generic;
using System.Linq;
using KellermanSoftware.CompareNetObjects;
using NUnit.Framework;
using Api = Ihc.Soap.Openapi;
using Ri = Ihc.Soap.Resourceinteraction;

namespace Ihc.Tests
{
    /// <summary>
    /// The OpenAPI wire&#8596;model mapping for resource values, held against its twin.
    ///
    /// <see cref="OpenApiResourceValueMapper.ToDomain"/> and
    /// <see cref="ResourceValueEnvelopeMapper.ToDomain"/> are two independently written
    /// implementations of the SAME value mapping, over two generated namespaces that the vendor's
    /// WSDL happens to shape alike. That makes a DIFFERENTIAL test available here that no single-sided
    /// oracle can give: each mapper is the other's second opinion, sharing no code, so a case where
    /// they disagree is a defect in one of them rather than a disagreement with a fixture someone
    /// wrote at the same time as the code.
    ///
    /// The pairs below are the whole oracle: for each kind, the two wire representations of ONE value.
    /// Only the payload is compared - the OpenAPI mapper is handed a bare value while its twin is
    /// handed an envelope, so resource id, runtime flag and type string are not this test's subject.
    /// </summary>
    [TestFixture]
    public class OpenApiResourceValueMappingTests
    {
        /// <summary>A value expressed on both wires: what the two mappers must agree about.</summary>
        public sealed record WirePair(ResourceValue.ValueKind Kind, Api.WSResourceValue OpenApi, Ri.WSResourceValue Resourceinteraction);

        // One representative value per kind, written twice. Deliberately distinct per field so a
        // transposed assignment (delayTime into rampTime, say) cannot pass unnoticed.
        private static IEnumerable<WirePair> Pairs()
        {
            yield return new(ResourceValue.ValueKind.BOOL,
                new Api.WSBooleanValue { value = true },
                new Ri.WSBooleanValue { value = true });

            yield return new(ResourceValue.ValueKind.INT,
                new Api.WSIntegerValue { integer = 4711 },
                new Ri.WSIntegerValue { integer = 4711 });

            yield return new(ResourceValue.ValueKind.DOUBLE,
                new Api.WSFloatingPointValue { floatingPointValue = 21.5 },
                new Ri.WSFloatingPointValue { floatingPointValue = 21.5 });

            yield return new(ResourceValue.ValueKind.ENUM,
                new Api.WSEnumValue { definitionTypeID = 11, enumValueID = 22, enumName = "Aften" },
                new Ri.WSEnumValue { definitionTypeID = 11, enumValueID = 22, enumName = "Aften" });

            yield return new(ResourceValue.ValueKind.DATE,
                new Api.WSDateValue { year = 2026, month = 9, day = 4 },
                new Ri.WSDateValue { year = 2026, month = 9, day = 4 });

            yield return new(ResourceValue.ValueKind.TIME,
                new Api.WSTimeValue { hours = 13, minutes = 24, seconds = 35 },
                new Ri.WSTimeValue { hours = 13, minutes = 24, seconds = 35 });

            yield return new(ResourceValue.ValueKind.TIMER,
                new Api.WSTimerValue { milliseconds = 90_000L },
                new Ri.WSTimerValue { milliseconds = 90_000L });

            yield return new(ResourceValue.ValueKind.WEEKDAY,
                new Api.WSWeekdayValue { weekdayNumber = 5 },
                new Ri.WSWeekdayValue { weekdayNumber = 5 });

            yield return new(ResourceValue.ValueKind.PhoneNumber,
                new Api.WSPhoneNumberValue { number = "+4512345678" },
                new Ri.WSPhoneNumberValue { number = "+4512345678" });

            yield return new(ResourceValue.ValueKind.SceneDimmer,
                new Api.WSSceneDimmerValue { dimmerPercentage = 60, delayTime = 700, rampTime = 800 },
                new Ri.WSSceneDimmerValue { dimmerPercentage = 60, delayTime = 700, rampTime = 800 });

            yield return new(ResourceValue.ValueKind.SceneRelay,
                new Api.WSSceneRelayValue { relayValue = true, delayTime = 900 },
                new Ri.WSSceneRelayValue { relayValue = true, delayTime = 900 });

            yield return new(ResourceValue.ValueKind.SceneShutter,
                new Api.WSSceneShutterSimpleValue { shutterPositionIsUp = true, delayTime = 1100 },
                new Ri.WSSceneShutterSimpleValue { shutterPositionIsUp = true, delayTime = 1100 });
        }

        private static IEnumerable<TestCaseData> PairCases() =>
            Pairs().Select(p => new TestCaseData(p).SetName($"{nameof(BothMappers_AgreeOnPayload)}({p.Kind})"));

        private static ResourceValue.UnionValue ViaOpenApi(Api.WSResourceValue? v) =>
            OpenApiResourceValueMapper.ToDomain(v)?.Value
                ?? throw new InvalidOperationException("the OpenAPI mapper returned null for a non-null wire value");

        private static ResourceValue.UnionValue ViaTwin(Ri.WSResourceValue? v) =>
            ResourceValueEnvelopeMapper.ToDomain(new Ri.WSResourceValueEnvelope { resourceID = 1, typeString = "x", value = v })?.Value
                ?? throw new InvalidOperationException("the resourceinteraction mapper returned null for a non-null envelope");

        /// <summary>
        /// The differential itself: for every kind the wire carries, the two mappers must produce the
        /// same payload from the same value. Compared whole rather than field-by-field, so a member
        /// one mapper populates and the other leaves null is a failure rather than an omission.
        /// </summary>
        [TestCaseSource(nameof(PairCases))]
        public void BothMappers_AgreeOnPayload(WirePair pair)
        {
            ResourceValue.UnionValue openApi = ViaOpenApi(pair.OpenApi);
            ResourceValue.UnionValue twin = ViaTwin(pair.Resourceinteraction);

            ComparisonResult diff = new CompareLogic().Compare(twin, openApi);

            Assert.Multiple(() =>
            {
                Assert.That(openApi.ValueKind, Is.EqualTo(pair.Kind), "the OpenAPI mapper must classify the value by its wire subtype");
                Assert.That(diff.AreEqual, Is.True, diff.DifferencesString);
            });
        }

        /// <summary>
        /// The divergence this fixture was written for. A wire value the mapper does not recognise -
        /// the base <c>WSResourceValue</c> stands in for one here, and a scene resource produces the
        /// same shape live - carries no readable value, so it must be classified
        /// <see cref="ResourceValue.ValueKind.NONE"/>.
        ///
        /// Untreated, the payload's <c>ValueKind</c> is left at the enum's zero member, which is
        /// <see cref="ResourceValue.ValueKind.BOOL"/>: an OpenAPI read of a scene then answers
        /// BOOL/false, a value the caller has no way to tell from a genuine reading of a switch that
        /// is off. Its twin already defaults to NONE, with a comment recording the live confirmation.
        /// </summary>
        [Test]
        public void UnrecognizedWireValue_MapsToNone_NotTheEnumDefaultBool()
        {
            ResourceValue.UnionValue openApi = ViaOpenApi(new Api.WSResourceValue());
            ResourceValue.UnionValue twin = ViaTwin(null);

            Assert.Multiple(() =>
            {
                Assert.That(twin.ValueKind, Is.EqualTo(ResourceValue.ValueKind.NONE),
                    "the tested twin already classifies an unreadable value as NONE");
                Assert.That(openApi.ValueKind, Is.EqualTo(ResourceValue.ValueKind.NONE),
                    "an OpenAPI read of a resource with no readable value must not answer BOOL/false");
                Assert.That(openApi.BoolValue, Is.Null, "NONE carries no payload of any kind");
            });
        }

        /// <summary>
        /// A null wire value is not a value at all, in both mappers: <c>GetValues</c> pairs values with
        /// the requested resources by POSITION, so it turns the null into a named refusal rather than
        /// letting a NONE slide into the list.
        /// </summary>
        [Test]
        public void NullWireValue_MapsToNull_InBothMappers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OpenApiResourceValueMapper.ToDomain(null), Is.Null);
                Assert.That(ResourceValueEnvelopeMapper.ToDomain(null), Is.Null);
            });
        }

        /// <summary>
        /// The outbound half of the same divergence. A <c>UnionValue</c> whose <c>ValueKind</c> is set
        /// but whose matching payload was left null is malformed; both mappers must refuse it the same
        /// way. The twin raises a typed <see cref="ArgumentException"/> naming the missing field;
        /// unguarded, the OpenAPI side trips its <c>Nullable&lt;T&gt;</c> cast instead and surfaces an
        /// opaque "Nullable object must have a value" <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <remarks>
        /// The kinds come from the ENUM, not from a hand-written list, so a kind added later arrives with a
        /// refusal case rather than needing to be remembered into one. Written out by hand is exactly how
        /// PhoneNumber came to be covered by neither half of what used to be two fixtures here - its payload
        /// is a reference rather than a <c>Nullable&lt;T&gt;</c>, so it was counted among the wrong group and
        /// listed in neither. The mappers had parted company over the reference kinds in a way no caller
        /// could act on: the twin wrote an envelope carrying a null value - a write the controller cannot
        /// make sense of, sent without complaint - while the OpenAPI side dereferenced the null and raised a
        /// bare <see cref="NullReferenceException"/>. A selected kind without its payload is malformed
        /// whatever the payload's type, so one case covers both.
        /// </remarks>
        [TestCaseSource(nameof(KindsWithAPayload))]
        public void KindWithoutItsPayload_IsRefusedTheSameWayByBothMappers(ResourceValue.ValueKind kind)
        {
            var malformed = new ResourceValue
            {
                ResourceID = 42,
                TypeString = TypeStrings.DatalineOutput,
                Value = new ResourceValue.UnionValue { ValueKind = kind }
            };

            Assert.Multiple(() =>
            {
                Assert.That(() => ResourceValueEnvelopeMapper.ToWire(malformed), Throws.ArgumentException,
                    "the tested twin names the missing payload");
                Assert.That(() => OpenApiResourceValueMapper.ToWire(malformed), Throws.ArgumentException,
                    "the OpenAPI mapper must refuse a malformed value as clearly as its twin does");
            });
        }

        /// <summary>Every kind that HAS a payload to leave out. NONE is the one that does not, and it is
        /// refused outright by <see cref="NoneKind_IsUnwritable_InBothMappers"/>.</summary>
        private static IEnumerable<ResourceValue.ValueKind> KindsWithAPayload() =>
            Enum.GetValues<ResourceValue.ValueKind>().Where(kind => kind != ResourceValue.ValueKind.NONE);

        /// <summary>
        /// <see cref="ResourceValue.ValueKind.NONE"/> is a read-only classification: it exists to say a
        /// resource has no writable value, so writing one is refused rather than silently encoded. Both
        /// mappers already agree here - pinned so the alignment above does not disturb it.
        /// </summary>
        [Test]
        public void NoneKind_IsUnwritable_InBothMappers()
        {
            var scene = new ResourceValue
            {
                ResourceID = 47434,
                TypeString = "resource_scene",
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.NONE }
            };

            Assert.Multiple(() =>
            {
                Assert.That(() => ResourceValueEnvelopeMapper.ToWire(scene), Throws.ArgumentException);
                Assert.That(() => OpenApiResourceValueMapper.ToWire(scene), Throws.ArgumentException);
            });
        }

    }
}
