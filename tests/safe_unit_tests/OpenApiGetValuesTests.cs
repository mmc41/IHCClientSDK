using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using NUnit.Framework;
using Api = Ihc.Soap.Openapi;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="OpenAPIService.GetValues"/>, whose whole contract is POSITIONAL.
    ///
    /// The OpenAPI wire answers a multi-resource read with a bare array of values: unlike
    /// <c>WSResourceValueEnvelope</c> and <c>WSResourceValueEvent</c>, a value here carries no resource
    /// id of its own. Position in the answer is therefore the only thing binding a value to the
    /// resource it was asked for - which makes it both the thing a caller needs handed back and the
    /// thing that fails silently when the answer does not line up with the request.
    /// </summary>
    [TestFixture]
    public class OpenApiGetValuesTests
    {
        private sealed class Harness
        {
            internal required OpenAPIService Service { get; init; }
            internal Api.WSResourceValue[]? Answer { get; set; }
            internal int[]? Requested { get; set; }
        }

        private static Harness NewHarness()
        {
            var soap = A.Fake<Api.OpenAPIService>();
            var harness = new Harness { Service = new OpenAPIService(FakeSession.Over(), soap) };

            A.CallTo(() => soap.getValuesAsync(A<Api.inputMessageName6>._))
                .ReturnsLazily((Api.inputMessageName6 m) =>
                {
                    harness.Requested = m.getValues1;
                    return Task.FromResult(new Api.outputMessageName6(harness.Answer!));
                });

            return harness;
        }

        /// <summary>
        /// The id a caller gets back. Without it every value in the list answers <c>ResourceID</c> 0, so
        /// a caller reading more than one resource cannot tell which value belongs to which - and the
        /// twin read over <c>resourceinteraction</c>, whose envelope carries the id, would disagree with
        /// this one about the same resources.
        /// </summary>
        [Test]
        public async Task GetValues_PairsEachValueWithTheResourceItWasAskedFor()
        {
            Harness h = NewHarness();
            h.Answer =
            [
                new Api.WSBooleanValue { value = true },
                new Api.WSIntegerValue { integer = 4711 },
                new Api.WSFloatingPointValue { floatingPointValue = 21.5 }
            ];

            IReadOnlyList<ResourceValue> values = await h.Service.GetValues([101, 102, 103]);

            Assert.Multiple(() =>
            {
                Assert.That(h.Requested, Is.EqualTo(new[] { 101, 102, 103 }), "the request carries the ids as given");
                Assert.That(values.Select(v => v.ResourceID), Is.EqualTo(new[] { 101, 102, 103 }));
                Assert.That(values.Select(v => v.Value.ValueKind), Is.EqualTo(new[]
                {
                    ResourceValue.ValueKind.BOOL, ResourceValue.ValueKind.INT, ResourceValue.ValueKind.DOUBLE
                }), "and each value keeps the reading that arrived at its position");
            });
        }

        /// <summary>
        /// An answer of a different length cannot be paired at all: every value from the mismatch on
        /// would be labelled with someone else's resource. Refused by count rather than by truncating to
        /// the shorter of the two, which is how a wrong reading would reach a caller looking correct.
        /// </summary>
        [TestCase(0, TestName = "GetValues_WithNoValuesAtAll_IsRefused")]
        [TestCase(2, TestName = "GetValues_WithFewerValuesThanRequested_IsRefused")]
        [TestCase(4, TestName = "GetValues_WithMoreValuesThanRequested_IsRefused")]
        public void GetValues_WhenTheAnswerDoesNotMatchTheRequest_IsRefused(int answered)
        {
            Harness h = NewHarness();
            h.Answer = [.. Enumerable.Range(0, answered).Select(i => new Api.WSIntegerValue { integer = i })];

            var refusal = Assert.CatchAsync<InvalidOperationException>(
                async () => await h.Service.GetValues([101, 102, 103]))!;

            Assert.That(refusal.Message, Does.Contain(answered.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .And.Contain("3"));
        }

        /// <summary>
        /// An entry the wire left empty is named with the resource it stands for, rather than dropped -
        /// dropping it would slide every later value one resource to the left.
        /// </summary>
        [Test]
        public void GetValues_WithAnEmptyEntry_NamesTheResourceItStandsFor()
        {
            Harness h = NewHarness();
            h.Answer = [new Api.WSBooleanValue { value = true }, null!, new Api.WSBooleanValue { value = false }];

            var refusal = Assert.CatchAsync<InvalidOperationException>(
                async () => await h.Service.GetValues([101, 102, 103]))!;

            Assert.That(refusal.Message, Does.Contain("102"));
        }

        /// <summary>
        /// A NIL array is the same answer as an empty one: <c>getValues2</c> is
        /// <c>[XmlArray(IsNullable=true)]</c>, so "no values" arrives from the controller either as an
        /// omitted element or as an empty list, and neither pairs with a request for a resource. Read as
        /// a successful empty list instead, a caller that asked for a resource would be told the read
        /// worked and there was nothing there - the silent wrong answer this contract exists to prevent,
        /// and one the caller cannot tell from a resource that genuinely has no value.
        /// </summary>
        [Test]
        public void GetValues_WithNoArrayAtAll_IsRefusedLikeAnEmptyOne()
        {
            Harness h = NewHarness();
            h.Answer = null;

            var refusal = Assert.CatchAsync<InvalidOperationException>(
                async () => await h.Service.GetValues([101]))!;

            Assert.That(refusal.Message, Does.Contain("0").And.Contain("1"));
        }

        /// <summary>Asking for nothing is the one request an empty answer does pair with.</summary>
        [Test]
        public async Task GetValues_ForNoResources_IsEmpty()
        {
            Harness h = NewHarness();
            h.Answer = null;

            Assert.That(await h.Service.GetValues([]), Is.Empty);
        }
    }
}
